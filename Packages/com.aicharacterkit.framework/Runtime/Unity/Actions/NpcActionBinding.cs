using System;

namespace AiCharacterKit.Unity.Actions
{
    /// <summary>
    /// Stores one authored natural-language trigger and consumer action binding.
    /// </summary>
    [Serializable]
    public sealed class NpcActionBinding
    {
        public string triggerId = string.Empty;

        public string conditionDescription = string.Empty;

        public string exampleUserText = string.Empty;

        public string actionId = string.Empty;

        public int priority;

        /// <summary>
        /// Creates an empty binding for Unity serialization and editor authoring.
        /// </summary>
        public NpcActionBinding()
        {
        }

        /// <summary>
        /// Creates one fully specified binding for consumer tooling and tests.
        /// </summary>
        public NpcActionBinding(
            string triggerId,
            string conditionDescription,
            string exampleUserText,
            string actionId,
            int priority)
        {
            this.triggerId = triggerId ?? string.Empty;
            this.conditionDescription = conditionDescription ?? string.Empty;
            this.exampleUserText = exampleUserText ?? string.Empty;
            this.actionId = actionId ?? string.Empty;
            this.priority = priority;
        }
    }
}
