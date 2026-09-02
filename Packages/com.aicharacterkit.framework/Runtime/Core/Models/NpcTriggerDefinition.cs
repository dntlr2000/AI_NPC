using System;
using System.Text;

namespace AiCharacterKit.Core
{
    /// <summary>
    /// Describes one bounded natural-language trigger and its consumer-owned action binding.
    /// </summary>
    public sealed class NpcTriggerDefinition
    {
        public const int MaxTriggerCount = 16;
        public const int MaxIdentifierLength = 64;
        public const int MaxConditionUtf8Bytes = 512;
        public const int MaxExampleUtf8Bytes = 1024;

        public string TriggerId { get; }

        public string ConditionDescription { get; }

        public string ExampleUserText { get; }

        public string ActionId { get; }

        public int Priority { get; }

        /// <summary>
        /// Creates and validates one trigger-to-action definition.
        /// </summary>
        public NpcTriggerDefinition(
            string triggerId,
            string conditionDescription,
            string exampleUserText,
            string actionId,
            int priority)
        {
            TriggerId = RequireIdentifier(triggerId, nameof(triggerId));
            ConditionDescription = RequireBoundedText(
                conditionDescription,
                MaxConditionUtf8Bytes,
                nameof(conditionDescription));
            ExampleUserText = RequireBoundedText(
                exampleUserText,
                MaxExampleUtf8Bytes,
                nameof(exampleUserText));
            ActionId = RequireIdentifier(actionId, nameof(actionId));
            Priority = priority;
        }

        /// <summary>
        /// Checks the lower snake_case identifier format shared by trigger and action IDs.
        /// </summary>
        public static bool IsValidIdentifier(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return false;
            }

            var trimmed = value.Trim();
            if (trimmed.Length > MaxIdentifierLength
                || trimmed[0] < 'a'
                || trimmed[0] > 'z')
            {
                return false;
            }

            var previousWasUnderscore = false;
            for (var index = 1; index < trimmed.Length; index++)
            {
                var character = trimmed[index];
                if (character == '_')
                {
                    if (previousWasUnderscore || index == trimmed.Length - 1)
                    {
                        return false;
                    }

                    previousWasUnderscore = true;
                    continue;
                }

                if ((character < 'a' || character > 'z')
                    && (character < '0' || character > '9'))
                {
                    return false;
                }

                previousWasUnderscore = false;
            }

            return true;
        }

        /// <summary>
        /// Returns a validated trimmed identifier or throws a field-specific exception.
        /// </summary>
        private static string RequireIdentifier(string value, string parameterName)
        {
            if (!IsValidIdentifier(value))
            {
                throw new ArgumentException(
                    "IDs must be lower snake_case and at most 64 characters.",
                    parameterName);
            }

            return value.Trim();
        }

        /// <summary>
        /// Returns required trimmed text after enforcing its UTF-8 byte budget.
        /// </summary>
        private static string RequireBoundedText(
            string value,
            int maxUtf8Bytes,
            string parameterName)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                throw new ArgumentException("Value must not be empty.", parameterName);
            }

            var trimmed = value.Trim();
            if (Encoding.UTF8.GetByteCount(trimmed) > maxUtf8Bytes)
            {
                throw new ArgumentException(
                    $"Value exceeds the {maxUtf8Bytes}-byte UTF-8 limit.",
                    parameterName);
            }

            return trimmed;
        }
    }
}
