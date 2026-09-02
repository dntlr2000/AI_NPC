using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace AiCharacterKit.Core
{
    /// <summary>
    /// Selects at most one known trigger and invokes its consumer-owned action handler safely.
    /// </summary>
    public sealed class NpcActionRouter : INpcTurnObserver
    {
        private readonly IReadOnlyList<NpcTriggerDefinition> definitions;
        private readonly Dictionary<string, INpcActionHandler> handlers;
        private int isRouting;

        public NpcActionResult LastResult { get; private set; }

        public event Action<NpcActionResult> ActionCompleted;

        /// <summary>
        /// Creates a deterministic router from immutable trigger and handler snapshots.
        /// </summary>
        public NpcActionRouter(
            IReadOnlyList<NpcTriggerDefinition> definitions,
            IEnumerable<INpcActionHandler> handlers)
        {
            this.definitions = CopyAndValidateDefinitions(definitions);
            this.handlers = CopyAndValidateHandlers(handlers);
            LastResult = new NpcActionResult(
                NpcActionStatus.NoMatch,
                string.Empty,
                string.Empty,
                "No action has been evaluated.");
        }

        /// <summary>
        /// Routes one observed turn and stores its independent action result.
        /// </summary>
        public async Task ObserveAsync(
            NpcTurnContext context,
            CancellationToken cancellationToken)
        {
            LastResult = await RouteAsync(context, cancellationToken);
            NotifyCompleted(LastResult);
        }

        /// <summary>
        /// Rejects untrusted IDs, selects by priority and declaration order, and executes once.
        /// </summary>
        public async Task<NpcActionResult> RouteAsync(
            NpcTurnContext context,
            CancellationToken cancellationToken)
        {
            if (context == null)
            {
                throw new ArgumentNullException(nameof(context));
            }

            if (Interlocked.CompareExchange(ref isRouting, 1, 0) != 0)
            {
                return new NpcActionResult(
                    NpcActionStatus.Busy,
                    string.Empty,
                    string.Empty,
                    "Another NPC action is already running.");
            }

            try
            {
                if (!TrySelectDefinition(
                        context.Response.MatchedTriggerIds,
                        out var selected,
                        out var selectionFailure))
                {
                    return selectionFailure;
                }

                if (!handlers.TryGetValue(selected.ActionId, out var handler))
                {
                    return CreateResult(
                        NpcActionStatus.HandlerMissing,
                        selected,
                        "No handler is configured for the selected action ID.");
                }

                var actionContext = new NpcActionContext(
                    context.Request,
                    context.Response,
                    selected);
                bool canExecute;
                string rejectionReason;
                try
                {
                    canExecute = handler.CanExecute(
                        actionContext,
                        out rejectionReason);
                }
                catch (Exception exception)
                {
                    return CreateResult(
                        NpcActionStatus.Failed,
                        selected,
                        "Action authorization failed: " + exception.Message);
                }

                if (!canExecute)
                {
                    return CreateResult(
                        NpcActionStatus.Rejected,
                        selected,
                        string.IsNullOrWhiteSpace(rejectionReason)
                            ? "The consumer rejected the action in the current game state."
                            : rejectionReason);
                }

                try
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    await handler.ExecuteAsync(actionContext, cancellationToken);
                    cancellationToken.ThrowIfCancellationRequested();
                    return CreateResult(
                        NpcActionStatus.Succeeded,
                        selected,
                        "Action completed.");
                }
                catch (OperationCanceledException)
                {
                    return CreateResult(
                        NpcActionStatus.Cancelled,
                        selected,
                        "Action was cancelled.");
                }
                catch (Exception exception)
                {
                    return CreateResult(
                        NpcActionStatus.Failed,
                        selected,
                        "Action failed: " + exception.Message);
                }
            }
            finally
            {
                Volatile.Write(ref isRouting, 0);
            }
        }

        /// <summary>
        /// Validates all model-returned IDs and selects the highest priority stable match.
        /// </summary>
        private bool TrySelectDefinition(
            IReadOnlyList<string> matchedTriggerIds,
            out NpcTriggerDefinition selected,
            out NpcActionResult failure)
        {
            selected = null;
            failure = null;
            if (matchedTriggerIds == null || matchedTriggerIds.Count == 0)
            {
                failure = new NpcActionResult(
                    NpcActionStatus.NoMatch,
                    string.Empty,
                    string.Empty,
                    "No trigger matched this turn.");
                return false;
            }

            var matchedDefinitions = new HashSet<string>(StringComparer.Ordinal);
            foreach (var triggerId in matchedTriggerIds)
            {
                if (string.IsNullOrWhiteSpace(triggerId)
                    || !TryFindDefinition(triggerId, out var definition))
                {
                    failure = new NpcActionResult(
                        NpcActionStatus.UnknownTriggerRejected,
                        triggerId,
                        string.Empty,
                        "The response contained an unknown trigger ID.");
                    return false;
                }

                if (!matchedDefinitions.Add(definition.TriggerId))
                {
                    failure = new NpcActionResult(
                        NpcActionStatus.UnknownTriggerRejected,
                        triggerId,
                        string.Empty,
                        "The response contained a duplicate trigger ID.");
                    return false;
                }
            }

            foreach (var definition in definitions)
            {
                if (matchedDefinitions.Contains(definition.TriggerId)
                    && (selected == null || definition.Priority > selected.Priority))
                {
                    selected = definition;
                }
            }

            if (selected != null)
            {
                return true;
            }

            failure = new NpcActionResult(
                NpcActionStatus.NoMatch,
                string.Empty,
                string.Empty,
                "No trigger matched this turn.");
            return false;
        }

        /// <summary>
        /// Resolves one exact trigger ID from the authored definition snapshot.
        /// </summary>
        private bool TryFindDefinition(
            string triggerId,
            out NpcTriggerDefinition definition)
        {
            foreach (var candidate in definitions)
            {
                if (string.Equals(
                    candidate.TriggerId,
                    triggerId,
                    StringComparison.Ordinal))
                {
                    definition = candidate;
                    return true;
                }
            }

            definition = null;
            return false;
        }

        /// <summary>
        /// Copies definitions and rejects null, excessive, or duplicate trigger IDs.
        /// </summary>
        private static IReadOnlyList<NpcTriggerDefinition> CopyAndValidateDefinitions(
            IReadOnlyList<NpcTriggerDefinition> source)
        {
            if (source == null)
            {
                throw new ArgumentNullException(nameof(source));
            }

            if (source.Count > NpcTriggerDefinition.MaxTriggerCount)
            {
                throw new ArgumentException("Too many trigger definitions.", nameof(source));
            }

            var ids = new HashSet<string>(StringComparer.Ordinal);
            var copy = new List<NpcTriggerDefinition>(source.Count);
            foreach (var definition in source)
            {
                if (definition == null || !ids.Add(definition.TriggerId))
                {
                    throw new ArgumentException(
                        "Trigger definitions must be non-null with unique IDs.",
                        nameof(source));
                }

                copy.Add(definition);
            }

            return copy.AsReadOnly();
        }

        /// <summary>
        /// Copies handlers and rejects invalid or duplicate action IDs.
        /// </summary>
        private static Dictionary<string, INpcActionHandler> CopyAndValidateHandlers(
            IEnumerable<INpcActionHandler> source)
        {
            var copy = new Dictionary<string, INpcActionHandler>(StringComparer.Ordinal);
            if (source == null)
            {
                return copy;
            }

            foreach (var handler in source)
            {
                if (handler == null
                    || !NpcTriggerDefinition.IsValidIdentifier(handler.ActionId)
                    || copy.ContainsKey(handler.ActionId))
                {
                    throw new ArgumentException(
                        "Action handlers must expose unique lower snake_case IDs.",
                        nameof(source));
                }

                copy.Add(handler.ActionId, handler);
            }

            return copy;
        }

        /// <summary>
        /// Creates one result for a selected authored definition.
        /// </summary>
        private static NpcActionResult CreateResult(
            NpcActionStatus status,
            NpcTriggerDefinition definition,
            string message)
        {
            return new NpcActionResult(
                status,
                definition.TriggerId,
                definition.ActionId,
                message);
        }

        /// <summary>
        /// Notifies result listeners without allowing diagnostic callbacks to escape routing.
        /// </summary>
        private void NotifyCompleted(NpcActionResult result)
        {
            var callbacks = ActionCompleted;
            if (callbacks == null)
            {
                return;
            }

            foreach (Action<NpcActionResult> callback in callbacks.GetInvocationList())
            {
                try
                {
                    callback(result);
                }
                catch
                {
                    // Consumer diagnostics cannot change the action or dialogue outcome.
                }
            }
        }
    }
}
