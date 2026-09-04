using System;
using System.Collections.Generic;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace AiCharacterKit.Core
{
    /// <summary>
    /// Captures the character canon and relevant knowledge used for exactly one turn.
    /// </summary>
    public sealed class NpcGroundingSnapshot
    {
        public const int MaxBackgroundUtf8Bytes = 2048;
        public const int MaxGoalsAndValuesUtf8Bytes = 2048;
        public const int MaxBehavioralRuleCount = 16;
        public const int MaxBehavioralRuleUtf8Bytes = 512;
        public const int MaxDialogueExampleCount = 8;
        public const int MaxDialogueExampleUtf8Bytes = 1024;
        public const int MaxFactCount = 32;
        public const int MaxTotalFactUtf8Bytes = 12 * 1024;

        private static readonly NpcGroundingSnapshot EmptySnapshot =
            new NpcGroundingSnapshot(
                string.Empty,
                string.Empty,
                Array.Empty<string>(),
                Array.Empty<string>(),
                Array.Empty<NpcContextFact>());

        public static NpcGroundingSnapshot Empty => EmptySnapshot;

        public string Background { get; }

        public string GoalsAndValues { get; }

        public IReadOnlyList<string> BehavioralRules { get; }

        public IReadOnlyList<string> DialogueExamples { get; }

        public IReadOnlyList<NpcContextFact> Facts { get; }

        public string Revision { get; }

        public bool IsEmpty =>
            Background.Length == 0
            && GoalsAndValues.Length == 0
            && BehavioralRules.Count == 0
            && DialogueExamples.Count == 0
            && Facts.Count == 0;

        /// <summary>
        /// Creates one normalized immutable snapshot and computes its stable revision.
        /// </summary>
        public NpcGroundingSnapshot(
            string background,
            string goalsAndValues,
            IEnumerable<string> behavioralRules,
            IEnumerable<string> dialogueExamples,
            IEnumerable<NpcContextFact> facts)
        {
            Background = NormalizeOptionalText(
                background,
                MaxBackgroundUtf8Bytes,
                nameof(background));
            GoalsAndValues = NormalizeOptionalText(
                goalsAndValues,
                MaxGoalsAndValuesUtf8Bytes,
                nameof(goalsAndValues));
            BehavioralRules = CopyRequiredTexts(
                behavioralRules,
                MaxBehavioralRuleCount,
                MaxBehavioralRuleUtf8Bytes,
                nameof(behavioralRules));
            DialogueExamples = CopyRequiredTexts(
                dialogueExamples,
                MaxDialogueExampleCount,
                MaxDialogueExampleUtf8Bytes,
                nameof(dialogueExamples));
            Facts = CopyAndSortFacts(facts);
            Revision = ComputeRevision();
        }

        /// <summary>
        /// Normalizes optional bounded text while allowing an omitted value.
        /// </summary>
        private static string NormalizeOptionalText(
            string value,
            int maxUtf8Bytes,
            string parameterName)
        {
            var normalized = NpcContextFact.NormalizeText(value);
            if (Encoding.UTF8.GetByteCount(normalized) > maxUtf8Bytes)
            {
                throw new ArgumentException(
                    $"Value must not exceed {maxUtf8Bytes} UTF-8 bytes.",
                    parameterName);
            }

            return normalized;
        }

        /// <summary>
        /// Copies ordered non-empty text values into an immutable bounded list.
        /// </summary>
        private static IReadOnlyList<string> CopyRequiredTexts(
            IEnumerable<string> values,
            int maxCount,
            int maxUtf8Bytes,
            string parameterName)
        {
            if (values == null)
            {
                return Array.Empty<string>();
            }

            var copy = new List<string>();
            foreach (var value in values)
            {
                if (copy.Count >= maxCount)
                {
                    throw new ArgumentException(
                        $"Collection must not contain more than {maxCount} values.",
                        parameterName);
                }

                var normalized = NpcContextFact.NormalizeText(value);
                if (normalized.Length == 0)
                {
                    throw new ArgumentException(
                        "Collection values must not be empty.",
                        parameterName);
                }

                if (Encoding.UTF8.GetByteCount(normalized) > maxUtf8Bytes)
                {
                    throw new ArgumentException(
                        $"Collection values must not exceed {maxUtf8Bytes} UTF-8 bytes.",
                        parameterName);
                }

                copy.Add(normalized);
            }

            return copy.AsReadOnly();
        }

        /// <summary>
        /// Copies, validates, and deterministically orders grounding facts.
        /// </summary>
        private static IReadOnlyList<NpcContextFact> CopyAndSortFacts(
            IEnumerable<NpcContextFact> facts)
        {
            if (facts == null)
            {
                return Array.Empty<NpcContextFact>();
            }

            var copy = new List<NpcContextFact>();
            var knownIds = new HashSet<string>(StringComparer.Ordinal);
            var totalUtf8Bytes = 0;
            foreach (var fact in facts)
            {
                if (fact == null)
                {
                    throw new ArgumentException("Facts must not contain null.", nameof(facts));
                }

                if (copy.Count >= MaxFactCount)
                {
                    throw new ArgumentException(
                        $"A snapshot must not contain more than {MaxFactCount} facts.",
                        nameof(facts));
                }

                if (!knownIds.Add(fact.FactId))
                {
                    throw new ArgumentException(
                        $"Fact IDs must be unique: {fact.FactId}.",
                        nameof(facts));
                }

                totalUtf8Bytes += Encoding.UTF8.GetByteCount(fact.Statement);
                if (totalUtf8Bytes > MaxTotalFactUtf8Bytes)
                {
                    throw new ArgumentException(
                        $"Fact statements exceed the {MaxTotalFactUtf8Bytes}-byte UTF-8 budget.",
                        nameof(facts));
                }

                copy.Add(fact);
            }

            copy.Sort(CompareFacts);
            return copy.AsReadOnly();
        }

        /// <summary>
        /// Orders higher-priority facts first and uses the identifier as a stable tie-breaker.
        /// </summary>
        private static int CompareFacts(NpcContextFact left, NpcContextFact right)
        {
            var priorityComparison = right.Priority.CompareTo(left.Priority);
            return priorityComparison != 0
                ? priorityComparison
                : string.Compare(left.FactId, right.FactId, StringComparison.Ordinal);
        }

        /// <summary>
        /// Hashes normalized content with length prefixes to produce one stable opaque revision.
        /// </summary>
        private string ComputeRevision()
        {
            var canonical = new StringBuilder();
            AppendValue(canonical, Background);
            AppendValue(canonical, GoalsAndValues);
            AppendValues(canonical, BehavioralRules);
            AppendValues(canonical, DialogueExamples);
            foreach (var fact in Facts)
            {
                AppendValue(canonical, fact.FactId);
                AppendValue(
                    canonical,
                    ((int)fact.Kind).ToString(CultureInfo.InvariantCulture));
                AppendValue(canonical, fact.Statement);
                AppendValue(
                    canonical,
                    fact.Priority.ToString(CultureInfo.InvariantCulture));
            }

            using (var algorithm = SHA256.Create())
            {
                var hash = algorithm.ComputeHash(Encoding.UTF8.GetBytes(canonical.ToString()));
                var hexadecimal = new StringBuilder(hash.Length * 2);
                foreach (var value in hash)
                {
                    hexadecimal.Append(value.ToString("x2"));
                }

                return "ctx-" + hexadecimal;
            }
        }

        /// <summary>
        /// Appends an ordered text collection to the canonical revision input.
        /// </summary>
        private static void AppendValues(
            StringBuilder destination,
            IReadOnlyList<string> values)
        {
            AppendValue(
                destination,
                values.Count.ToString(CultureInfo.InvariantCulture));
            for (var index = 0; index < values.Count; index++)
            {
                AppendValue(destination, values[index]);
            }
        }

        /// <summary>
        /// Appends one unambiguous length-prefixed value to the revision input.
        /// </summary>
        private static void AppendValue(StringBuilder destination, string value)
        {
            var safeValue = value ?? string.Empty;
            destination.Append(safeValue.Length);
            destination.Append(':');
            destination.Append(safeValue);
            destination.Append('|');
        }
    }
}
