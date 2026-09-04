using System;
using System.Text;

namespace AiCharacterKit.Core
{
    /// <summary>
    /// Carries one bounded fact that an NPC is allowed to use for the current turn.
    /// </summary>
    public sealed class NpcContextFact
    {
        public const int MaxStatementUtf8Bytes = 512;
        public const int MinPriority = 0;
        public const int MaxPriority = 100;

        public string FactId { get; }

        public NpcContextFactKind Kind { get; }

        public string Statement { get; }

        public int Priority { get; }

        /// <summary>
        /// Creates one validated immutable grounding fact.
        /// </summary>
        public NpcContextFact(
            string factId,
            NpcContextFactKind kind,
            string statement,
            int priority)
        {
            if (!NpcTriggerDefinition.IsValidIdentifier(factId))
            {
                throw new ArgumentException(
                    "Fact IDs must be lower snake_case and at most 64 characters.",
                    nameof(factId));
            }

            if (!Enum.IsDefined(typeof(NpcContextFactKind), kind))
            {
                throw new ArgumentOutOfRangeException(nameof(kind));
            }

            if (string.IsNullOrWhiteSpace(statement))
            {
                throw new ArgumentException(
                    "Fact statements must not be empty.",
                    nameof(statement));
            }

            var normalizedStatement = NormalizeText(statement);
            if (Encoding.UTF8.GetByteCount(normalizedStatement) > MaxStatementUtf8Bytes)
            {
                throw new ArgumentException(
                    $"Fact statements must not exceed {MaxStatementUtf8Bytes} UTF-8 bytes.",
                    nameof(statement));
            }

            if (priority < MinPriority || priority > MaxPriority)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(priority),
                    $"Fact priority must be between {MinPriority} and {MaxPriority}.");
            }

            FactId = factId.Trim();
            Kind = kind;
            Statement = normalizedStatement;
            Priority = priority;
        }

        /// <summary>
        /// Normalizes authored line endings and surrounding whitespace for stable snapshots.
        /// </summary>
        internal static string NormalizeText(string value)
        {
            return (value ?? string.Empty)
                .Replace("\r\n", "\n")
                .Replace('\r', '\n')
                .Trim();
        }
    }
}
