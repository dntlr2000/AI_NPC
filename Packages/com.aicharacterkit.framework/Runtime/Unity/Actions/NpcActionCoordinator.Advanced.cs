using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using AiCharacterKit.Behavior;
using AiCharacterKit.Core;
using AiCharacterKit.Unity.Behavior;
using UnityEngine;

namespace AiCharacterKit.Unity.Actions
{
    public enum NpcBehaviorMode { Basic = 0, Advanced = 1 }

    public sealed partial class NpcActionCoordinator
    {
        [SerializeField] private NpcBehaviorMode behaviorMode;
        [SerializeField] private NpcBehaviorProfile behaviorProfile;
        [SerializeField] private MonoBehaviour[] behaviorValueProviderSources = Array.Empty<MonoBehaviour>();
        [SerializeField] private bool enableBehaviorTrace;
        private NpcBehaviorRuntime behaviorRuntime;
        private NpcBehaviorTurn activeBehaviorTurn;

        public bool IsAdvanced => behaviorMode == NpcBehaviorMode.Advanced;
        public NpcBehaviorProfile BehaviorProfile => behaviorProfile;
        public NpcBehaviorTrace LastBehaviorTrace => behaviorRuntime?.LastTrace;
        public IReadOnlyList<NpcBehaviorTrace> BehaviorTraces => behaviorRuntime?.Traces ?? Array.Empty<NpcBehaviorTrace>();
        public IReadOnlyDictionary<string, NpcBehaviorValue> BehaviorState => behaviorRuntime?.State
            ?? new ReadOnlyDictionary<string, NpcBehaviorValue>(new Dictionary<string, NpcBehaviorValue>());

        /// <summary>Validates authored rules and explicit references without evaluating any live behavior.</summary>
        private bool TryValidateAdvanced(out string error)
        {
            if (behaviorProfile == null) { error = "Advanced requires an NpcBehaviorProfile."; return false; }
            if (!behaviorProfile.TryValidate(out error)) return false;
            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (var source in actionHandlerSources ?? Array.Empty<MonoBehaviour>())
            {
                if (source == null || !(source is INpcActionHandler handler) || !NpcTriggerDefinition.IsValidIdentifier(handler.ActionId) || !ids.Add(handler.ActionId))
                { error = "Select handlers with unique valid action IDs."; return false; }
            }
            foreach (var rule in behaviorProfile.CreateDraft().actionRules)
                if (!ids.Contains(rule.actionId)) { error = "No selected handler provides actionId '" + rule.actionId + "'."; return false; }
            var providers = new HashSet<MonoBehaviour>();
            foreach (var source in behaviorValueProviderSources ?? Array.Empty<MonoBehaviour>())
                if (source == null || !(source is INpcBehaviorValueProvider) || !providers.Add(source))
                { error = "Select unique INpcBehaviorValueProvider components."; return false; }
            if (behaviorProfile.CreateDraft().externalValues.Count > 0 && providers.Count == 0)
            { error = "External values require an explicit behavior value provider."; return false; }
            error = string.Empty; return true;
        }

        /// <summary>Creates the independent variable store while preserving static-only initialization.</summary>
        private bool TryInitializeAdvanced(out string error)
        {
            if (behaviorRuntime != null) { error = string.Empty; return true; }
            if (!TryValidateAdvanced(out error)) return false;
            var handlers = new List<INpcActionHandler>();
            foreach (var source in actionHandlerSources ?? Array.Empty<MonoBehaviour>()) handlers.Add((INpcActionHandler)source);
            behaviorRuntime = new NpcBehaviorRuntime(behaviorProfile.CreateDraft(), handlers, enableTrace: enableBehaviorTrace);
            return true;
        }

        /// <summary>Reserves Advanced state during generation or memory reset; Basic allocates no state.</summary>
        public bool TryReserveBehaviorTurn(out IDisposable reservation, out string error)
        {
            reservation = null; error = string.Empty;
            if (!IsAdvanced) return true;
            if (!TryInitializeAdvanced(out error)) return false;
            if (!behaviorRuntime.TryBeginTurn(out var turn)) { error = "busy"; return false; }
            activeBehaviorTurn = turn;
            reservation = new BehaviorReservation(this, turn);
            return true;
        }

        /// <summary>Sets one boolean value between conversations.</summary>
        public bool TrySetBoolean(string id, bool value, out string error)
        {
            if (!RequireAdvanced(out error)) return false;
            return behaviorRuntime.TrySetBoolean(id, value, out error);
        }
        /// <summary>Sets and clamps one numeric value between conversations.</summary>
        public bool TrySetNumber(string id, double value, out string error)
        {
            if (!RequireAdvanced(out error)) return false;
            return behaviorRuntime.TrySetNumber(id, value, out error);
        }
        /// <summary>Applies a bounded numeric delta between conversations.</summary>
        public bool TryAddNumber(string id, double delta, out string error)
        {
            if (!RequireAdvanced(out error)) return false;
            return behaviorRuntime.TryAddNumber(id, delta, out error);
        }
        /// <summary>Clears only behavior state and successful action gates, retaining conversation memory.</summary>
        public bool TryResetBehaviorState(out string error)
        {
            if (!RequireAdvanced(out error)) return false;
            return behaviorRuntime.TryResetBehaviorState(out error);
        }
        /// <summary>Enables or clears bounded in-memory diagnostics without logging any turn contents.</summary>
        public void SetBehaviorTraceEnabled(bool value)
        {
            enableBehaviorTrace = value;
            if (behaviorRuntime != null) behaviorRuntime.TraceEnabled = value;
        }
        /// <summary>Exports explicitly selected variables as immutable grounding facts.</summary>
        public IReadOnlyList<NpcContextFact> CaptureBehaviorDialogueFacts()
        {
            if (!RequireAdvanced(out _)) return Array.Empty<NpcContextFact>();
            return behaviorRuntime.CaptureDialogueFacts();
        }
        /// <summary>Rejects state APIs in Basic mode without silently enabling Advanced.</summary>
        private bool RequireAdvanced(out string error)
        {
            if (!IsAdvanced) { error = "advanced_not_configured"; return false; }
            return TryInitializeAdvanced(out error);
        }
        /// <summary>Resolves providers at evaluation time so destroyed Unity objects fail safely.</summary>
        private IReadOnlyList<INpcBehaviorValueProvider> CreateValueProviders()
        {
            var result = new List<INpcBehaviorValueProvider>();
            foreach (var source in behaviorValueProviderSources ?? Array.Empty<MonoBehaviour>())
                result.Add(source == null ? null : source as INpcBehaviorValueProvider);
            return result.AsReadOnly();
        }

        private sealed class BehaviorReservation : IDisposable
        {
            private readonly NpcActionCoordinator owner;
            private readonly NpcBehaviorTurn turn;
            /// <summary>Associates one controller request with its pure state reservation.</summary>
            public BehaviorReservation(NpcActionCoordinator owner, NpcBehaviorTurn turn) { this.owner = owner; this.turn = turn; }
            /// <summary>Releases only this request, without disturbing another turn or resetting state.</summary>
            public void Dispose()
            {
                if (owner.activeBehaviorTurn == turn) owner.activeBehaviorTurn = null;
                turn.Dispose();
            }
        }
    }
}
