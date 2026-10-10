using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AiCharacterKit.Behavior;
using NUnit.Framework;

namespace AiCharacterKit.Core.Tests
{
    /// <summary>Verifies deterministic state, typed facts, cancellation, bounded traces and final authorization.</summary>
    public sealed class NpcBehaviorRuntimeTests
    {
        /// <summary>Builds a minimal profile in which praise raises affinity and an optional gate requires 60.</summary>
        internal static NpcBehaviorDefinition Definition(bool action = true, bool external = false)
        {
            var definition = new NpcBehaviorDefinition();
            definition.variables.Add(new NpcBehaviorVariable { id = "affinity", defaultNumber = 40, exposeToDialogue = true });
            definition.variables.Add(new NpcBehaviorVariable { id = "greeted", kind = NpcBehaviorValueKind.Boolean });
            definition.signals.Add(new NpcBehaviorSignal());
            definition.mutationRules.Add(new NpcBehaviorMutationRule { changes = new List<NpcBehaviorMutation> { new NpcBehaviorMutation() } });
            if (action) definition.actionRules.Add(new NpcBehaviorActionRule { triggerId = "praise", condition = AtLeast(60) });
            if (external) definition.externalValues.Add(new NpcBehaviorExternalValue());
            return definition;
        }
        /// <summary>Creates one simple threshold tree.</summary>
        internal static NpcBehaviorCondition AtLeast(double number) => new NpcBehaviorCondition { nodes = new List<NpcBehaviorConditionNode> { new NpcBehaviorConditionNode { number = number } } };
        /// <summary>Creates a synthetic successful turn without relying on model wording.</summary>
        internal static NpcTurnContext Turn(params string[] ids) => new NpcTurnContext(
            new AiNpcRequest("test", "Test", "Neutral", "Brief", "Hello", NpcEmotion.Neutral, "synthetic"),
            new AiNpcResponse("Synthetic reply", NpcEmotion.Neutral, NpcGesture.None, ids));

        /// <summary>Preserves the Basic constructor while permitting signal-only transport and Mock matching.</summary>
        [Test] public void SignalFactory_DoesNotRelaxBasicActionValidation()
        {
            var signal = NpcTriggerDefinition.CreateSignal("praise", "A compliment", "thank you");
            Assert.That(signal.HasActionBinding, Is.False);
            Assert.Throws<ArgumentException>(() => new NpcTriggerDefinition("praise", "A compliment", "thank you", "", 0));
            Assert.Throws<ArgumentException>(() => new NpcActionRouter(new[] { signal }, null));
            var envelope = Transport.V4.AiNpcContractMapper.CreateRequest(Turn().Request, "req", "session", new[] { signal });
            Assert.That(envelope.triggers.Single().triggerId, Is.EqualTo("praise"));
            Assert.That(NpcTriggerMatcher.MatchExampleTriggerIds("Thank YOU", new[] { signal }), Is.EqualTo(new[] { "praise" }));
        }

        /// <summary>Rejects each unsupported authoring shape before any runtime is created.</summary>
        [TestCase("duplicate_variable")][TestCase("duplicate_signal")][TestCase("duplicate_rule")][TestCase("missing_signal")]
        [TestCase("missing_variable")][TestCase("boolean_add")][TestCase("nan")][TestCase("infinity")][TestCase("default_range")]
        [TestCase("reverse_bounds")][TestCase("condition_depth")][TestCase("condition_nodes")][TestCase("not_children")]
        [TestCase("bad_parent")][TestCase("variable_count")][TestCase("external_count")][TestCase("signal_count")]
        [TestCase("rule_count")][TestCase("change_count")][TestCase("conflicting_policy")][TestCase("bool_order")]
        public void InvalidDefinitions_AreRejected(string fault)
        {
            var data = Definition();
            switch (fault)
            {
                case "duplicate_variable": data.variables.Add(data.variables[0].Copy()); break;
                case "duplicate_signal": data.signals.Add(data.signals[0].Copy()); break;
                case "duplicate_rule": data.mutationRules.Add(data.mutationRules[0].Copy()); break;
                case "missing_signal": data.actionRules[0].triggerId = "missing"; break;
                case "missing_variable": data.mutationRules[0].changes[0].variableId = "missing"; break;
                case "boolean_add": data.mutationRules[0].changes[0].variableId = "greeted"; break;
                case "nan": data.mutationRules[0].changes[0].number = double.NaN; break;
                case "infinity": data.variables[0].maximum = double.PositiveInfinity; break;
                case "default_range": data.variables[0].defaultNumber = 101; break;
                case "reverse_bounds": data.variables[0].minimum = 101; break;
                case "condition_depth":
                    data.actionRules[0].condition.nodes.Clear();
                    for (var i = 0; i < 5; i++) data.actionRules[0].condition.nodes.Add(new NpcBehaviorConditionNode { parent = i - 1, kind = i == 4 ? NpcBehaviorConditionKind.Compare : NpcBehaviorConditionKind.Not });
                    break;
                case "condition_nodes": for (var i = 0; i < 32; i++) data.actionRules[0].condition.nodes.Add(new NpcBehaviorConditionNode()); break;
                case "not_children": data.actionRules[0].condition.nodes[0].kind = NpcBehaviorConditionKind.Not; break;
                case "bad_parent": data.actionRules[0].condition.nodes[0].parent = 0; break;
                case "variable_count": for (var i = 0; i < 33; i++) data.variables.Add(new NpcBehaviorVariable { id = "value_" + i }); break;
                case "external_count": for (var i = 0; i < 33; i++) data.externalValues.Add(new NpcBehaviorExternalValue { id = "fact_" + i }); break;
                case "signal_count": for (var i = 0; i < 16; i++) data.signals.Add(new NpcBehaviorSignal { triggerId = "signal_" + i }); break;
                case "rule_count": for (var i = 0; i < 64; i++) data.actionRules.Add(new NpcBehaviorActionRule { ruleId = "rule_" + i, triggerId = "praise" }); break;
                case "change_count": for (var i = 0; i < 16; i++) data.mutationRules[0].changes.Add(new NpcBehaviorMutation()); break;
                case "conflicting_policy": data.actionRules.Add(new NpcBehaviorActionRule { ruleId = "other", triggerId = "praise", once = true }); break;
                case "bool_order": data.actionRules[0].condition.nodes[0].valueId = "greeted"; break;
            }
            Assert.That(NpcBehaviorValidator.TryValidate(data, out _), Is.False, fault);
        }

        /// <summary>Snapshots definitions and isolates state even when two runtimes share one authoring object.</summary>
        [Test] public async Task InstanceStateAndDefinitions_AreIndependent()
        {
            var data = Definition(false); var first = new NpcBehaviorRuntime(data); var second = new NpcBehaviorRuntime(data);
            var snapshot = first.State;
            data.mutationRules[0].changes[0].number = 100;
            await first.ProcessTurnAsync(Turn("praise"), null, CancellationToken.None);
            Assert.That(first.State["affinity"].Number, Is.EqualTo(50));
            Assert.That(second.State["affinity"].Number, Is.EqualTo(40));
            Assert.That(snapshot["affinity"].Number, Is.EqualTo(40));
        }

        /// <summary>Uses pre-change conditions for every mutation but post-change values for actions.</summary>
        [Test] public async Task OrderedChanges_DoNotCascade_AndActionUsesCommittedThreshold()
        {
            var data = Definition(); var handler = new Handler();
            data.mutationRules.Add(new NpcBehaviorMutationRule { ruleId = "second", changes = new List<NpcBehaviorMutation> { new NpcBehaviorMutation() } });
            data.mutationRules.Add(new NpcBehaviorMutationRule { ruleId = "not_this_turn", condition = AtLeast(60), changes = new List<NpcBehaviorMutation> { new NpcBehaviorMutation { variableId = "greeted", operation = NpcBehaviorMutationKind.Set, boolean = true } } });
            var runtime = new NpcBehaviorRuntime(data, new[] { handler });
            var result = await runtime.ProcessTurnAsync(Turn("praise"), null, CancellationToken.None);
            Assert.That(runtime.State["affinity"].Number, Is.EqualTo(60));
            Assert.That(runtime.State["greeted"].Boolean, Is.False);
            Assert.That(result.Executed, Is.True); Assert.That(handler.Executions, Is.EqualTo(1));
        }

        /// <summary>Applies set/add/subtract in authored order and handles fractional values and overflow bounds.</summary>
        [Test] public async Task NumericChanges_AreOrderedFiniteAndBounded()
        {
            var data = Definition(false); var changes = data.mutationRules[0].changes;
            changes.Clear(); changes.Add(new NpcBehaviorMutation { operation = NpcBehaviorMutationKind.Set, number = 0.25 });
            changes.Add(new NpcBehaviorMutation { number = 0.5 });
            changes.Add(new NpcBehaviorMutation { operation = NpcBehaviorMutationKind.Subtract, number = 0.1 });
            var runtime = new NpcBehaviorRuntime(data);
            await runtime.ProcessTurnAsync(Turn("praise"), null, CancellationToken.None);
            Assert.That(runtime.State["affinity"].Number, Is.EqualTo(.65));
            Assert.That(runtime.TryAddNumber("affinity", double.MaxValue, out _), Is.True);
            Assert.That(runtime.State["affinity"].Number, Is.EqualTo(100));
            runtime.TryAddNumber("affinity", -double.MaxValue, out _);
            Assert.That(runtime.State["affinity"].Number, Is.Zero);
            Assert.That(runtime.TrySetNumber("affinity", double.NaN, out _), Is.False);
            Assert.That(runtime.TrySetBoolean("affinity", true, out _), Is.False);
        }

        /// <summary>Checks ALL/ANY/NOT with an external boolean and an exact fractional comparison.</summary>
        [Test] public async Task CompoundConditions_ReadTypedExternalValues()
        {
            var data = Definition(true, true); var handler = new Handler();
            data.actionRules[0].condition.nodes = new List<NpcBehaviorConditionNode> {
                new NpcBehaviorConditionNode { kind = NpcBehaviorConditionKind.All },
                new NpcBehaviorConditionNode { parent = 0, number = 50 },
                new NpcBehaviorConditionNode { parent = 0, kind = NpcBehaviorConditionKind.Not },
                new NpcBehaviorConditionNode { parent = 2, valueId = "permission", comparison = NpcBehaviorComparison.Equal, boolean = false },
                new NpcBehaviorConditionNode { parent = 0, kind = NpcBehaviorConditionKind.Any },
                new NpcBehaviorConditionNode { parent = 4, valueId = "greeted", comparison = NpcBehaviorComparison.Equal, boolean = true },
                new NpcBehaviorConditionNode { parent = 4, number = 50, comparison = NpcBehaviorComparison.Equal } };
            var provider = new Provider { Values = new Dictionary<string, NpcBehaviorValue> { ["permission"] = new NpcBehaviorValue(true) } };
            var runtime = new NpcBehaviorRuntime(data, new[] { handler });
            Assert.That((await runtime.ProcessTurnAsync(Turn("praise"), new[] { provider }, CancellationToken.None)).Executed, Is.True);
            Assert.That(provider.Captures, Is.EqualTo(1));
        }

        /// <summary>Rejects every provider failure atomically and recovers on a later turn in the same runtime.</summary>
        [TestCase("throw")][TestCase("null")][TestCase("missing")][TestCase("type")][TestCase("duplicate")][TestCase("unknown")]
        public async Task ProviderFailure_DoesNotMutate_AndRecovers(string fault)
        {
            var data = Definition(false, true);
            var values = new Dictionary<string, NpcBehaviorValue> { ["permission"] = new NpcBehaviorValue(true) };
            var provider = new Provider { Values = values, Throw = fault == "throw" };
            if (fault == "null") provider.Values = null;
            if (fault == "missing") values.Clear();
            if (fault == "type") values["permission"] = new NpcBehaviorValue(1d);
            if (fault == "unknown") values["unknown"] = new NpcBehaviorValue(true);
            var providers = fault == "duplicate" ? new[] { provider, new Provider { Values = values } } : new[] { provider };
            var runtime = new NpcBehaviorRuntime(data, enableTrace: true);
            var result = await runtime.ProcessTurnAsync(Turn("praise"), providers, CancellationToken.None);
            Assert.That(result.Status, Is.EqualTo(NpcActionStatus.Failed)); Assert.That(result.Message, Does.Not.Contain("private"));
            Assert.That(runtime.State["affinity"].Number, Is.EqualTo(40)); Assert.That(runtime.LastTrace.Changes, Is.Empty);
            provider.Throw = false; provider.Values = new Dictionary<string, NpcBehaviorValue> { ["permission"] = new NpcBehaviorValue(true) };
            await runtime.ProcessTurnAsync(Turn("praise"), new[] { provider }, CancellationToken.None);
            Assert.That(runtime.State["affinity"].Number, Is.EqualTo(50));
        }

        /// <summary>Contains unknown or duplicate model IDs before any provider or state mutation.</summary>
        [TestCase("unknown")][TestCase("praise,praise")]
        public async Task UntrustedIds_AreRejectedBeforeCapture(string ids)
        {
            var provider = new Provider(); var runtime = new NpcBehaviorRuntime(Definition(false));
            Assert.That((await runtime.ProcessTurnAsync(Turn(ids.Split(',')), new[] { provider }, CancellationToken.None)).Status, Is.EqualTo(NpcActionStatus.UnknownTriggerRejected));
            Assert.That(provider.Captures, Is.Zero); Assert.That(runtime.State["affinity"].Number, Is.EqualTo(40));
        }

        /// <summary>Chooses one stable highest-priority eligible action and does not fall through after rejection.</summary>
        [Test] public async Task PriorityTieAndRejectedAction_NeverExecuteFallback()
        {
            var data = Definition(); data.actionRules[0].condition = AtLeast(0); data.actionRules[0].priority = 5;
            data.actionRules.Add(new NpcBehaviorActionRule { ruleId = "other", triggerId = "praise", actionId = "wave", priority = 5 });
            var first = new Handler { Allowed = false }; var second = new Handler { ActionId = "wave" };
            var runtime = new NpcBehaviorRuntime(data, new[] { first, second });
            var result = await runtime.ProcessTurnAsync(Turn("praise"), null, CancellationToken.None);
            Assert.That(result.Status, Is.EqualTo(NpcActionStatus.Rejected)); Assert.That(first.Checks, Is.EqualTo(1)); Assert.That(second.Checks, Is.Zero);
            Assert.That(runtime.State["affinity"].Number, Is.EqualTo(50));
        }

        /// <summary>Records shared-action cooldown and once only after success and resets them explicitly.</summary>
        [TestCase(false)][TestCase(true)] public async Task ActionGates_RecordOnlySuccess_AndReset(bool once)
        {
            var data = Definition(); data.actionRules[0].condition = AtLeast(0); data.actionRules[0].cooldownSeconds = 5; data.actionRules[0].once = once;
            data.signals.Add(new NpcBehaviorSignal { triggerId = "another_request" });
            data.actionRules.Add(new NpcBehaviorActionRule { ruleId = "shared_action", triggerId = "another_request", cooldownSeconds = 5, once = once });
            var handler = new Handler { Throw = true }; var now = 10d;
            var runtime = new NpcBehaviorRuntime(data, new[] { handler }, () => now);
            Assert.That((await runtime.ProcessTurnAsync(Turn("praise"), null, CancellationToken.None)).Status, Is.EqualTo(NpcActionStatus.Failed));
            handler.Throw = false;
            Assert.That((await runtime.ProcessTurnAsync(Turn("praise"), null, CancellationToken.None)).Executed, Is.True);
            Assert.That((await runtime.ProcessTurnAsync(Turn("another_request"), null, CancellationToken.None)).Status, Is.EqualTo(NpcActionStatus.Rejected));
            now = 15;
            Assert.That((await runtime.ProcessTurnAsync(Turn("another_request"), null, CancellationToken.None)).Executed, Is.EqualTo(!once));
            Assert.That(runtime.TryResetBehaviorState(out _), Is.True);
            Assert.That(runtime.State["affinity"].Number, Is.EqualTo(40));
            Assert.That((await runtime.ProcessTurnAsync(Turn("praise"), null, CancellationToken.None)).Executed, Is.True);
        }

        /// <summary>Reserves state across generation and prevents duplicate evaluation under one request.</summary>
        [Test] public async Task Reservation_BlocksWritesResetAndDuplicateEvaluation()
        {
            var runtime = new NpcBehaviorRuntime(Definition(false));
            Assert.That(runtime.TryBeginTurn(out var turn), Is.True);
            using (turn)
            {
                Assert.That(runtime.TrySetNumber("affinity", 0, out var error), Is.False); Assert.That(error, Is.EqualTo("busy"));
                Assert.That(runtime.TryResetBehaviorState(out _), Is.False); Assert.That(runtime.TryBeginTurn(out _), Is.False);
                await turn.EvaluateAsync(Turn("praise"), null, CancellationToken.None);
                Assert.That((await turn.EvaluateAsync(Turn("praise"), null, CancellationToken.None)).Status, Is.EqualTo(NpcActionStatus.Busy));
            }
            Assert.That(runtime.State["affinity"].Number, Is.EqualTo(50)); Assert.That(runtime.TrySetBoolean("greeted", true, out _), Is.True);
        }

        /// <summary>Cancellation before commit has no effects; cancellation in a started action retains committed changes.</summary>
        [Test] public async Task Cancellation_RespectsCommitBoundary()
        {
            var data = Definition(); data.actionRules[0].condition = AtLeast(0);
            using (var cancellation = new CancellationTokenSource())
            {
                var handler = new Handler { OnExecute = () => cancellation.Cancel() }; var runtime = new NpcBehaviorRuntime(data, new[] { handler });
                cancellation.Cancel();
                Assert.That((await runtime.ProcessTurnAsync(Turn("praise"), null, cancellation.Token)).Status, Is.EqualTo(NpcActionStatus.Cancelled));
                Assert.That(runtime.State["affinity"].Number, Is.EqualTo(40)); Assert.That(handler.Checks, Is.Zero);
            }
            using (var cancellation = new CancellationTokenSource())
            {
                var handler = new Handler { OnExecute = () => cancellation.Cancel() }; var runtime = new NpcBehaviorRuntime(data, new[] { handler });
                Assert.That((await runtime.ProcessTurnAsync(Turn("praise"), null, cancellation.Token)).Status, Is.EqualTo(NpcActionStatus.Cancelled));
                Assert.That(runtime.State["affinity"].Number, Is.EqualTo(50));
            }
        }

        /// <summary>Early reservation disposal cannot unlock state while its asynchronous handler is still running.</summary>
        [Test] public async Task RunningAction_RetainsBusyUntilCompletion()
        {
            var data = Definition(); data.actionRules[0].condition = AtLeast(0);
            var completion = new TaskCompletionSource<bool>(); var handler = new Handler { Wait = completion.Task };
            var runtime = new NpcBehaviorRuntime(data, new[] { handler }); runtime.TryBeginTurn(out var turn);
            var pending = turn.EvaluateAsync(Turn("praise"), null, CancellationToken.None); turn.Dispose();
            Assert.That(runtime.IsBusy, Is.True); Assert.That(runtime.TryResetBehaviorState(out _), Is.False);
            completion.SetResult(true); await pending; Assert.That(runtime.IsBusy, Is.False);
        }

        /// <summary>Keeps trace opt-in, bounded, detached and cleared by disable or explicit reset.</summary>
        [Test] public async Task TraceAndGrounding_AreBoundedAndOptIn()
        {
            var runtime = new NpcBehaviorRuntime(Definition(false));
            var before = runtime.CaptureDialogueFacts();
            await runtime.ProcessTurnAsync(Turn("praise"), null, CancellationToken.None);
            Assert.That(runtime.Traces, Is.Empty); runtime.TraceEnabled = true;
            for (var index = 0; index < 35; index++) await runtime.ProcessTurnAsync(Turn("praise"), null, CancellationToken.None);
            Assert.That(runtime.Traces.Count, Is.EqualTo(32)); Assert.That(runtime.LastTrace.Changes, Has.Count.EqualTo(1));
            Assert.That(before.Single().Statement, Does.EndWith("40")); Assert.That(runtime.CaptureDialogueFacts().Single().Statement, Does.EndWith("100"));
            runtime.TraceEnabled = false; Assert.That(runtime.LastTrace, Is.Null);
        }

        internal sealed class Provider : INpcBehaviorValueProvider
        {
            public int Captures; public bool Throw; public IReadOnlyDictionary<string, NpcBehaviorValue> Values = new Dictionary<string, NpcBehaviorValue>();
            /// <summary>Counts live reads and can simulate delayed preparation or failure.</summary>
            public IReadOnlyDictionary<string, NpcBehaviorValue> CaptureValues() { Captures++; if (Throw) throw new InvalidOperationException("private provider detail"); return Values; }
        }
        internal sealed class Handler : INpcActionHandler
        {
            public string ActionId { get; set; } = "open_gate";
            public bool Allowed = true, Throw; public int Checks, Executions; public Action OnExecute; public Task Wait;
            /// <summary>Counts final authorization and optionally refuses the current state.</summary>
            public bool CanExecute(NpcActionContext context, out string reason) { Checks++; reason = "private reason"; return Allowed; }
            /// <summary>Simulates completion, failure, cancellation or a long-running consumer action.</summary>
            public async Task ExecuteAsync(NpcActionContext context, CancellationToken token)
            { if (Throw) throw new InvalidOperationException("private action detail"); Executions++; OnExecute?.Invoke(); if (Wait != null) await Wait; }
        }
    }
}
