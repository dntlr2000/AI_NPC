using System;
using System.Collections.Generic;
using AiCharacterKit.Core;
using UnityEngine;

namespace AiCharacterKit.Unity.Actions
{
    /// <summary>
    /// Stores bounded trigger-to-action data as a consumer-owned Unity asset.
    /// </summary>
    [CreateAssetMenu(
        fileName = "NpcActionProfile",
        menuName = "AI Character Kit/NPC Action Profile")]
    public sealed class NpcActionProfile : ScriptableObject
    {
        [SerializeField]
        private List<NpcActionBinding> bindings = new List<NpcActionBinding>();

        public IReadOnlyList<NpcActionBinding> Bindings => bindings;

        /// <summary>
        /// Validates required fields, bounds, and unique trigger and action IDs.
        /// </summary>
        public bool TryValidate(out string error)
        {
            if (bindings == null || bindings.Count == 0)
            {
                error = "At least one action binding is required.";
                return false;
            }

            if (bindings.Count > NpcTriggerDefinition.MaxTriggerCount)
            {
                error = $"Action profiles support at most {NpcTriggerDefinition.MaxTriggerCount} bindings.";
                return false;
            }

            var triggerIds = new HashSet<string>(StringComparer.Ordinal);
            var actionIds = new HashSet<string>(StringComparer.Ordinal);
            for (var index = 0; index < bindings.Count; index++)
            {
                var binding = bindings[index];
                if (binding == null)
                {
                    error = $"Binding {index + 1} must not be null.";
                    return false;
                }

                try
                {
                    var definition = CreateDefinition(binding);
                    if (!triggerIds.Add(definition.TriggerId))
                    {
                        error = $"Duplicate triggerId '{definition.TriggerId}'.";
                        return false;
                    }

                    if (!actionIds.Add(definition.ActionId))
                    {
                        error = $"Duplicate actionId '{definition.ActionId}'.";
                        return false;
                    }
                }
                catch (ArgumentException exception)
                {
                    error = $"Binding {index + 1} is invalid: {exception.Message}";
                    return false;
                }
            }

            error = string.Empty;
            return true;
        }

        /// <summary>
        /// Creates an immutable pure-Core snapshot in declaration order.
        /// </summary>
        public IReadOnlyList<NpcTriggerDefinition> CreateDefinitions()
        {
            if (!TryValidate(out var error))
            {
                throw new InvalidOperationException(error);
            }

            var definitions = new List<NpcTriggerDefinition>(bindings.Count);
            foreach (var binding in bindings)
            {
                definitions.Add(CreateDefinition(binding));
            }

            return definitions.AsReadOnly();
        }

        /// <summary>
        /// Converts one serialized binding into its validated pure-Core representation.
        /// </summary>
        private static NpcTriggerDefinition CreateDefinition(NpcActionBinding binding)
        {
            return new NpcTriggerDefinition(
                binding.triggerId,
                binding.conditionDescription,
                binding.exampleUserText,
                binding.actionId,
                binding.priority);
        }
    }
}
