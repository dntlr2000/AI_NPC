using System;
using System.Collections.Generic;
using AiCharacterKit.Core;
using AiCharacterKit.Unity.Actions;
using UnityEngine;

namespace AiCharacterKit.Unity.Behavior
{
    /// <summary>Exports only explicitly selected current variables through the existing grounding boundary.</summary>
    [DisallowMultipleComponent]
    public sealed class NpcBehaviorContextProvider : NpcContextProviderBehaviour
    {
        [SerializeField] private NpcActionCoordinator coordinator;
        /// <summary>Reads current instance variables without capturing consumer facts or executing actions.</summary>
        public override IReadOnlyList<NpcContextFact> CaptureFacts()
        {
            var source = coordinator != null ? coordinator : GetComponent<NpcActionCoordinator>();
            if (source == null || !source.IsAdvanced || !source.TryInitialize(out _))
                throw new InvalidOperationException("Advanced behavior is not configured.");
            return source.CaptureBehaviorDialogueFacts();
        }
    }
}
