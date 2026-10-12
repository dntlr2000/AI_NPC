using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using AiCharacterKit.Core;
using UnityEngine;

namespace AiCharacterKit.Unity.Actions
{
    /// <summary>
    /// Adapts Unity assets and MonoBehaviours to the pure-Core action router.
    /// </summary>
    public sealed partial class NpcActionCoordinator : MonoBehaviour, INpcTurnObserver
    {
        [SerializeField]
        private NpcActionProfile actionProfile;

        [SerializeField]
        private MonoBehaviour[] actionHandlerSources = Array.Empty<MonoBehaviour>();

        private NpcActionRouter router;

        public NpcActionProfile ActionProfile => actionProfile;

        public NpcActionResult LastResult => IsAdvanced ? behaviorRuntime?.LastResult : router?.LastResult;

        /// <summary>
        /// Validates the profile and every explicitly selected consumer action handler.
        /// </summary>
        public bool TryValidateConfiguration(out string error)
        {
            if (IsAdvanced) return TryValidateAdvanced(out error);
            if (behaviorMode != NpcBehaviorMode.Basic) { error = "Unknown behavior mode."; return false; }
            if (actionProfile == null)
            {
                error = "NpcActionCoordinator requires an NpcActionProfile.";
                return false;
            }

            if (!actionProfile.TryValidate(out error))
            {
                return false;
            }

            var configuredIds = new HashSet<string>(StringComparer.Ordinal);
            var handlerSources = actionHandlerSources ?? Array.Empty<MonoBehaviour>();

            foreach (var source in handlerSources)
            {
                if (!(source is INpcActionHandler handler))
                {
                    error = "Every action handler source must implement INpcActionHandler.";
                    return false;
                }

                if (!NpcTriggerDefinition.IsValidIdentifier(handler.ActionId)
                    || !configuredIds.Add(handler.ActionId))
                {
                    error = "Action handler IDs must be unique lower snake_case tokens.";
                    return false;
                }
            }

            foreach (var definition in actionProfile.CreateDefinitions())
            {
                if (!configuredIds.Contains(definition.ActionId))
                {
                    error = $"No selected handler provides actionId '{definition.ActionId}'.";
                    return false;
                }
            }

            error = string.Empty;
            return true;
        }

        /// <summary>
        /// Returns the immutable authored trigger snapshot used by Mock and V3 clients.
        /// </summary>
        public IReadOnlyList<NpcTriggerDefinition> CreateTriggerDefinitions()
        {
            if (IsAdvanced) return behaviorProfile == null ? Array.Empty<NpcTriggerDefinition>() : behaviorProfile.CreateTriggerDefinitions();
            if (actionProfile == null)
            {
                return Array.Empty<NpcTriggerDefinition>();
            }

            return actionProfile.CreateDefinitions();
        }

        /// <summary>
        /// Builds the pure router once after validating serialized consumer references.
        /// </summary>
        public bool TryInitialize(out string error)
        {
            if (IsAdvanced) return TryInitializeAdvanced(out error);
            if (router != null)
            {
                error = string.Empty;
                return true;
            }

            if (!TryValidateConfiguration(out error))
            {
                return false;
            }

            var handlerSources = actionHandlerSources ?? Array.Empty<MonoBehaviour>();
            var handlers = new List<INpcActionHandler>(handlerSources.Length);
            foreach (var source in handlerSources)
            {
                handlers.Add((INpcActionHandler)source);
            }

            try
            {
                router = new NpcActionRouter(
                    actionProfile.CreateDefinitions(),
                    handlers);
                return true;
            }
            catch (ArgumentException exception)
            {
                error = exception.Message;
                return false;
            }
        }

        /// <summary>
        /// Routes one response exactly once and records Advanced diagnostics independently of optional trace retention.
        /// </summary>
        public async Task ObserveAsync(
            NpcTurnContext context,
            CancellationToken cancellationToken)
        {
            if (!TryInitialize(out _))
            {
                return;
            }

            if (IsAdvanced)
            {
                IDisposable reservation = null;
                var ownsSubmission = activeBehaviorTurn == null;
                if (ownsSubmission && !TryReserveBehaviorTurn(out reservation, out _)) return;
                var ownsOutcome = LastBehaviorTurnOutcome == null || !LastBehaviorTurnOutcome.IsPending;
                var number = ownsOutcome ? BeginBehaviorConversationTurn() : LastBehaviorTurnOutcome.TurnNumber;
                var evaluated = false;
                try
                {
                    var result = await activeBehaviorTurn.EvaluateAsync(context, CreateValueProviders(), cancellationToken);
                    RecordBehaviorEvaluation(context, result);
                    evaluated = true;
                }
                finally
                {
                    if (ownsOutcome) EndBehaviorConversationTurn(number, evaluated);
                    reservation?.Dispose();
                }
                return;
            }

            var ownsBasicOutcome = LastBehaviorTurnOutcome?.IsPending != true;
            var basicNumber = ownsBasicOutcome ? BeginBehaviorConversationTurn() : LastBehaviorTurnOutcome.TurnNumber;
            var completed = false;
            try
            {
                await router.ObserveAsync(context, cancellationToken);
                RecordBehaviorEvaluation(context, router.LastResult);
                completed = true;
            }
            finally { if (ownsBasicOutcome) EndBehaviorConversationTurn(basicNumber, completed); }
        }
    }
}
