using System;
using System.Collections.Generic;
using AiCharacterKit.Behavior;
using AiCharacterKit.Core;
using UnityEngine;

namespace AiCharacterKit.Unity.Behavior
{
    /// <summary>Stores authored behavior definitions only; every NPC owns a separate runtime copy.</summary>
    [CreateAssetMenu(fileName = "NpcBehaviorProfile", menuName = "AI Character Kit/NPC Behavior Profile")]
    public sealed class NpcBehaviorProfile : ScriptableObject
    {
        [SerializeField] private NpcBehaviorDefinition definition = new NpcBehaviorDefinition();
        /// <summary>Returns a detached authoring draft, including incomplete values for Builder diagnostics.</summary>
        public NpcBehaviorDefinition CreateDraft() => definition?.Copy() ?? new NpcBehaviorDefinition();
        /// <summary>Checks authored values without touching live providers, handlers or runtime state.</summary>
        public bool TryValidate(out string error) => NpcBehaviorValidator.TryValidate(definition, out error);
        /// <summary>Replaces the authored data with a validated detached copy; existing runtimes retain their snapshots.</summary>
        public void SetDefinition(NpcBehaviorDefinition value)
        {
            if (!NpcBehaviorValidator.TryValidate(value, out var error)) throw new ArgumentException(error, nameof(value));
            definition = value.Copy();
        }
        /// <summary>Returns the validated immutable trigger snapshot used by existing clients.</summary>
        public IReadOnlyList<NpcTriggerDefinition> CreateTriggerDefinitions()
        {
            if (!TryValidate(out var error)) throw new InvalidOperationException(error);
            return definition.signals.ConvertAll(signal => signal.CreateTrigger()).AsReadOnly();
        }
    }
}
