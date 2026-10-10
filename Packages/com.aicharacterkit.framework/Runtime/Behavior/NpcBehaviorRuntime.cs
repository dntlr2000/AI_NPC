using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using AiCharacterKit.Core;

namespace AiCharacterKit.Behavior
{
    /// <summary>Owns bounded per-instance variables, deterministic rule evaluation and successful action gates.</summary>
    public sealed class NpcBehaviorRuntime
    {
        private readonly NpcBehaviorDefinition definition;
        private readonly Dictionary<string, NpcBehaviorVariable> variableDefinitions = new Dictionary<string, NpcBehaviorVariable>(StringComparer.Ordinal);
        private readonly Dictionary<string, NpcTriggerDefinition> signals = new Dictionary<string, NpcTriggerDefinition>(StringComparer.Ordinal);
        private readonly Dictionary<string, INpcActionHandler> handlers = new Dictionary<string, INpcActionHandler>(StringComparer.Ordinal);
        private readonly Dictionary<string, double> successes = new Dictionary<string, double>(StringComparer.Ordinal);
        private readonly Queue<NpcBehaviorTrace> traces = new Queue<NpcBehaviorTrace>();
        private readonly Func<double> monotonicSeconds;
        private readonly object sync = new object();
        private Dictionary<string, NpcBehaviorValue> state;
        private NpcBehaviorTurn activeTurn;
        private bool traceEnabled;

        public NpcActionResult LastResult { get; private set; }
        public bool IsBusy { get { lock (sync) return activeTurn != null; } }
        public IReadOnlyDictionary<string, NpcBehaviorValue> State { get { lock (sync) return Snapshot(state); } }
        public IReadOnlyList<NpcBehaviorTrace> Traces { get { lock (sync) return new List<NpcBehaviorTrace>(traces).AsReadOnly(); } }
        public NpcBehaviorTrace LastTrace { get { lock (sync) { NpcBehaviorTrace last = null; foreach (var trace in traces) last = trace; return last; } } }
        public bool TraceEnabled
        {
            get { lock (sync) return traceEnabled; }
            set { lock (sync) { traceEnabled = value; if (!value) traces.Clear(); } }
        }

        /// <summary>Validates and copies all definitions without invoking handlers or providers.</summary>
        public NpcBehaviorRuntime(NpcBehaviorDefinition source, IEnumerable<INpcActionHandler> actionHandlers = null,
            Func<double> monotonicSeconds = null, bool enableTrace = false)
        {
            if (!NpcBehaviorValidator.TryValidate(source, out var error)) throw new ArgumentException(error, nameof(source));
            definition = source.Copy();
            foreach (var variable in definition.variables) variableDefinitions.Add(variable.id, variable);
            foreach (var signal in definition.signals) signals.Add(signal.triggerId, signal.CreateTrigger());
            if (actionHandlers != null) foreach (var handler in actionHandlers)
            {
                if (handler == null || !NpcTriggerDefinition.IsValidIdentifier(handler.ActionId) || handlers.ContainsKey(handler.ActionId))
                    throw new ArgumentException("Handlers must have unique valid action IDs.", nameof(actionHandlers));
                handlers.Add(handler.ActionId, handler);
            }
            this.monotonicSeconds = monotonicSeconds ?? (() => Stopwatch.GetTimestamp() / (double)Stopwatch.Frequency);
            traceEnabled = enableTrace;
            state = CreateInitialState();
            LastResult = Result(NpcActionStatus.NoMatch, "not_evaluated");
        }

        /// <summary>Reserves the complete request lifetime so game writes and resets cannot race generation.</summary>
        public bool TryBeginTurn(out NpcBehaviorTurn turn)
        {
            lock (sync)
            {
                turn = null;
                if (activeTurn != null) return false;
                activeTurn = turn = new NpcBehaviorTurn(this);
                return true;
            }
        }

        /// <summary>Convenience entry point for consumers that already have a completed conversation response.</summary>
        public async Task<NpcActionResult> ProcessTurnAsync(NpcTurnContext context,
            IEnumerable<INpcBehaviorValueProvider> providers, CancellationToken cancellationToken)
        {
            if (!TryBeginTurn(out var turn)) return Result(NpcActionStatus.Busy, "busy");
            using (turn) return await turn.EvaluateAsync(context, providers, cancellationToken);
        }

        /// <summary>Sets a boolean between turns without touching authored definitions.</summary>
        public bool TrySetBoolean(string id, bool value, out string error) => TryWrite(id, new NpcBehaviorValue(value), false, out error);
        /// <summary>Sets a finite number and clamps it to its authored bounds.</summary>
        public bool TrySetNumber(string id, double value, out string error)
        {
            if (!NpcBehaviorValidator.Finite(value)) { error = "invalid_number"; return false; }
            return TryWrite(id, new NpcBehaviorValue(value), false, out error);
        }
        /// <summary>Adds a finite delta between turns, saturating at the authored bounds.</summary>
        public bool TryAddNumber(string id, double delta, out string error)
        {
            if (!NpcBehaviorValidator.Finite(delta)) { error = "invalid_number"; return false; }
            return TryWrite(id, new NpcBehaviorValue(delta), true, out error);
        }

        /// <summary>Resets variables, successful action gates and trace, independently of conversation memory.</summary>
        public bool TryResetBehaviorState(out string error)
        {
            lock (sync)
            {
                if (activeTurn != null) { error = "busy"; return false; }
                state = CreateInitialState(); successes.Clear(); traces.Clear();
                LastResult = Result(NpcActionStatus.NoMatch, "reset"); error = string.Empty; return true;
            }
        }

        /// <summary>Returns only explicitly selected variables as current-turn observations.</summary>
        public IReadOnlyList<NpcContextFact> CaptureDialogueFacts()
        {
            lock (sync)
            {
                var facts = new List<NpcContextFact>();
                foreach (var variable in definition.variables)
                    if (variable.exposeToDialogue)
                        facts.Add(new NpcContextFact(variable.id, NpcContextFactKind.Observation,
                            (string.IsNullOrWhiteSpace(variable.label) ? variable.id : variable.label.Trim()) + ": " + state[variable.id], 100));
                return facts.AsReadOnly();
            }
        }

        /// <summary>Validates a matched turn, atomically commits ordered changes and attempts at most one action.</summary>
        internal async Task<NpcActionResult> EvaluateAsync(NpcBehaviorTurn owner, NpcTurnContext context,
            IEnumerable<INpcBehaviorValueProvider> providers, CancellationToken token)
        {
            if (context == null) throw new ArgumentNullException(nameof(context));
            lock (sync) if (activeTurn != owner) return Result(NpcActionStatus.Busy, "busy");
            var matched = new HashSet<string>(StringComparer.Ordinal);
            var deltas = TraceEnabled ? new List<NpcBehaviorDelta>() : null;
            var evaluations = TraceEnabled ? new List<NpcBehaviorRuleTrace>() : null;
            var committed = false;
            try
            {
                token.ThrowIfCancellationRequested();
                foreach (var id in context.Response.MatchedTriggerIds)
                    if (id == null || !signals.ContainsKey(id) || !matched.Add(id))
                        return Finish(Result(NpcActionStatus.UnknownTriggerRejected, "invalid_trigger"), "invalid_trigger", Array.Empty<string>(), deltas, evaluations);
                if (matched.Count == 0) return Finish(Result(NpcActionStatus.NoMatch, "no_match"), "no_match", matched, deltas, evaluations);
                if (!TryCapture(providers, out var external))
                    return Finish(Result(NpcActionStatus.Failed, "provider_error"), "provider_error", matched, deltas, evaluations);
                var now = monotonicSeconds();
                if (!NpcBehaviorValidator.Finite(now)) return Finish(Result(NpcActionStatus.Failed, "clock_error"), "clock_error", matched, deltas, evaluations);
                Dictionary<string, NpcBehaviorValue> before;
                lock (sync) before = new Dictionary<string, NpcBehaviorValue>(state, StringComparer.Ordinal);
                var staged = new Dictionary<string, NpcBehaviorValue>(before, StringComparer.Ordinal);
                foreach (var rule in definition.mutationRules)
                {
                    var triggered = matched.Contains(rule.triggerId);
                    var passed = triggered && NpcBehaviorValidator.Evaluate(rule.condition, before, external);
                    evaluations?.Add(new NpcBehaviorRuleTrace(rule.ruleId, passed, !triggered ? "trigger_not_matched" : passed ? "applied" : "condition_false"));
                    if (!passed) continue;
                    foreach (var change in rule.changes)
                    {
                        var old = staged[change.variableId];
                        var variable = variableDefinitions[change.variableId];
                        var next = variable.kind == NpcBehaviorValueKind.Boolean ? new NpcBehaviorValue(change.boolean)
                            : Clamp(variable, change.operation == NpcBehaviorMutationKind.Set ? change.number
                                : old.Number + (change.operation == NpcBehaviorMutationKind.Add ? change.number : -change.number));
                        staged[change.variableId] = next;
                        deltas?.Add(new NpcBehaviorDelta(rule.ruleId, change.variableId, old, next));
                    }
                }
                lock (sync)
                {
                    token.ThrowIfCancellationRequested();
                    if (activeTurn != owner) throw new OperationCanceledException();
                    state = staged; committed = true;
                }
                NpcBehaviorActionRule selected = null, blocked = null;
                var reasons = new Dictionary<string, string>(StringComparer.Ordinal);
                foreach (var rule in definition.actionRules)
                {
                    var reason = "eligible";
                    if (!matched.Contains(rule.triggerId)) reason = "trigger_not_matched";
                    else if (!NpcBehaviorValidator.Evaluate(rule.condition, staged, external)) reason = "condition_false";
                    else if (successes.TryGetValue(rule.actionId, out var last) && rule.once) reason = "once";
                    else if (successes.TryGetValue(rule.actionId, out last) && now - last < rule.cooldownSeconds) reason = "cooldown";
                    reasons.Add(rule.ruleId, reason);
                    if (reason == "eligible" && (selected == null || rule.priority > selected.priority)) selected = rule;
                    if (reason != "trigger_not_matched" && (blocked == null || rule.priority > blocked.priority)) blocked = rule;
                }
                foreach (var rule in definition.actionRules)
                {
                    var reason = reasons[rule.ruleId];
                    evaluations?.Add(new NpcBehaviorRuleTrace(rule.ruleId, reason == "eligible", reason == "eligible" ? (rule == selected ? "selected" : "lower_priority") : reason));
                }
                if (selected == null)
                    return Finish(blocked == null ? Result(NpcActionStatus.NoMatch, "state_only")
                        : new NpcActionResult(NpcActionStatus.Rejected, blocked.triggerId, blocked.actionId, "Behavior conditions did not allow this action."),
                        blocked == null ? "state_only" : reasons[blocked.ruleId], matched, deltas, evaluations);
                var signal = signals[selected.triggerId];
                var binding = new NpcTriggerDefinition(signal.TriggerId, signal.ConditionDescription, signal.ExampleUserText, selected.actionId, selected.priority);
                handlers.TryGetValue(selected.actionId, out var handler);
                var result = await NpcActionExecutor.ExecuteAsync(new NpcActionContext(context.Request, context.Response, binding), handler, token);
                if (result.Executed)
                {
                    var completedAt = now;
                    try { completedAt = monotonicSeconds(); } catch { /* A diagnostic clock cannot undo a successful game effect. */ }
                    successes[selected.actionId] = NpcBehaviorValidator.Finite(completedAt) ? Math.Max(now, completedAt) : now;
                }
                return Finish(result, result.Status.ToString().ToLowerInvariant(), matched, deltas, evaluations);
            }
            catch (OperationCanceledException)
            {
                if (!committed) deltas?.Clear();
                return Finish(Result(NpcActionStatus.Cancelled, "cancelled"), "cancelled", matched, deltas, evaluations);
            }
            catch
            {
                if (!committed) deltas?.Clear();
                return Finish(Result(NpcActionStatus.Failed, "evaluation_failed"), "evaluation_failed", matched, deltas, evaluations);
            }
        }

        /// <summary>Copies each provider exactly once and rejects missing, duplicate, unknown or mistyped facts.</summary>
        private bool TryCapture(IEnumerable<INpcBehaviorValueProvider> providers, out IReadOnlyDictionary<string, NpcBehaviorValue> values)
        {
            var captured = new Dictionary<string, NpcBehaviorValue>(StringComparer.Ordinal); values = captured;
            try
            {
                var seen = new HashSet<INpcBehaviorValueProvider>();
                if (providers != null) foreach (var provider in providers)
                {
                    if (provider == null || !seen.Add(provider)) return false;
                    var supplied = provider.CaptureValues();
                    if (supplied == null) return false;
                    foreach (var pair in supplied)
                    {
                        if (pair.Key == null || captured.ContainsKey(pair.Key) || captured.Count >= NpcBehaviorDefinition.MaxExternalValues) return false;
                        captured.Add(pair.Key, pair.Value);
                    }
                }
                if (captured.Count != definition.externalValues.Count) return false;
                foreach (var declaration in definition.externalValues)
                    if (!captured.TryGetValue(declaration.id, out var value) || value.Kind != declaration.kind
                        || value.Kind == NpcBehaviorValueKind.Number && !NpcBehaviorValidator.Finite(value.Number)) return false;
                values = new ReadOnlyDictionary<string, NpcBehaviorValue>(captured); return true;
            }
            catch { return false; }
        }

        /// <summary>Releases only the reservation created by this caller.</summary>
        internal void EndTurn(NpcBehaviorTurn owner) { lock (sync) if (activeTurn == owner) activeTurn = null; }
        /// <summary>Writes one typed variable only when no conversation owns the state.</summary>
        private bool TryWrite(string id, NpcBehaviorValue value, bool add, out string error)
        {
            lock (sync)
            {
                if (activeTurn != null) { error = "busy"; return false; }
                if (id == null || !variableDefinitions.TryGetValue(id, out var variable) || variable.kind != value.Kind)
                { error = "invalid_variable"; return false; }
                state[id] = variable.kind == NpcBehaviorValueKind.Boolean ? value : Clamp(variable, value.Number + (add ? state[id].Number : 0));
                error = string.Empty; return true;
            }
        }
        /// <summary>Builds fresh instance defaults without reading live game state.</summary>
        private Dictionary<string, NpcBehaviorValue> CreateInitialState()
        {
            var initial = new Dictionary<string, NpcBehaviorValue>(StringComparer.Ordinal);
            foreach (var value in definition.variables) initial.Add(value.id, value.kind == NpcBehaviorValueKind.Boolean
                ? new NpcBehaviorValue(value.defaultBoolean) : new NpcBehaviorValue(value.defaultNumber));
            return initial;
        }
        /// <summary>Stores a safe result and, only when enabled, a bounded immutable diagnostic trace.</summary>
        private NpcActionResult Finish(NpcActionResult result, string code, IEnumerable<string> ids,
            List<NpcBehaviorDelta> deltas, List<NpcBehaviorRuleTrace> rules)
        {
            lock (sync)
            {
                LastResult = result;
                if (traceEnabled)
                {
                    traces.Enqueue(new NpcBehaviorTrace(ids, deltas ?? new List<NpcBehaviorDelta>(), rules ?? new List<NpcBehaviorRuleTrace>(), result, code));
                    while (traces.Count > 32) traces.Dequeue();
                }
            }
            return result;
        }
        /// <summary>Clamps arithmetic overflow as well as ordinary out-of-range finite values.</summary>
        private static NpcBehaviorValue Clamp(NpcBehaviorVariable variable, double value) => new NpcBehaviorValue(Math.Min(variable.maximum, Math.Max(variable.minimum, value)));
        /// <summary>Returns a detached read-only state dictionary.</summary>
        private static IReadOnlyDictionary<string, NpcBehaviorValue> Snapshot(Dictionary<string, NpcBehaviorValue> source) => new ReadOnlyDictionary<string, NpcBehaviorValue>(new Dictionary<string, NpcBehaviorValue>(source, StringComparer.Ordinal));
        /// <summary>Creates a fixed diagnostic with no consumer text.</summary>
        private static NpcActionResult Result(NpcActionStatus status, string code) => new NpcActionResult(status, string.Empty, string.Empty, code);
    }

    /// <summary>Reserves one request and prevents duplicate rule processing or early disposal during an action.</summary>
    public sealed class NpcBehaviorTurn : IDisposable
    {
        private readonly NpcBehaviorRuntime runtime;
        private readonly object sync = new object();
        private bool used, disposed, evaluating;
        /// <summary>Creates a reservation owned by one runtime.</summary>
        internal NpcBehaviorTurn(NpcBehaviorRuntime runtime) { this.runtime = runtime; }
        /// <summary>Processes a response at most once under this request reservation.</summary>
        public async Task<NpcActionResult> EvaluateAsync(NpcTurnContext context, IEnumerable<INpcBehaviorValueProvider> providers, CancellationToken token)
        {
            lock (sync)
            {
                if (disposed || used) return new NpcActionResult(NpcActionStatus.Busy, string.Empty, string.Empty, "busy");
                used = true; evaluating = true;
            }
            try { return await runtime.EvaluateAsync(this, context, providers, token); }
            finally { lock (sync) { evaluating = false; if (disposed) runtime.EndTurn(this); } }
        }
        /// <summary>Releases the request reservation after any active evaluation has completed.</summary>
        public void Dispose()
        {
            lock (sync) { disposed = true; if (!evaluating) runtime.EndTurn(this); }
        }
    }
}
