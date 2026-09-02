using System;
using System.Collections.Generic;
using System.Text;

namespace AiCharacterKit.Core
{
    /// <summary>
    /// Matches Mock inputs only against normalized authored examples.
    /// </summary>
    public static class NpcTriggerMatcher
    {
        /// <summary>
        /// Returns every definition whose normalized example exactly matches the input.
        /// </summary>
        public static IReadOnlyList<string> MatchExampleTriggerIds(
            string userText,
            IReadOnlyList<NpcTriggerDefinition> definitions)
        {
            var matches = new List<string>();
            if (definitions == null || definitions.Count == 0)
            {
                return matches.AsReadOnly();
            }

            var normalizedInput = Normalize(userText);
            if (normalizedInput.Length == 0)
            {
                return matches.AsReadOnly();
            }

            foreach (var definition in definitions)
            {
                if (definition != null
                    && string.Equals(
                        normalizedInput,
                        Normalize(definition.ExampleUserText),
                        StringComparison.Ordinal))
                {
                    matches.Add(definition.TriggerId);
                }
            }

            return matches.AsReadOnly();
        }

        /// <summary>
        /// Trims, lowercases, and collapses whitespace for deterministic example matching.
        /// </summary>
        public static string Normalize(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return string.Empty;
            }

            var builder = new StringBuilder(value.Length);
            var pendingSpace = false;
            foreach (var character in value.Trim().ToLowerInvariant())
            {
                if (char.IsWhiteSpace(character))
                {
                    pendingSpace = builder.Length > 0;
                    continue;
                }

                if (pendingSpace)
                {
                    builder.Append(' ');
                    pendingSpace = false;
                }

                builder.Append(character);
            }

            return builder.ToString();
        }
    }
}
