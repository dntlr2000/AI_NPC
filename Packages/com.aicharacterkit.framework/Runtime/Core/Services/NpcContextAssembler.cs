using System;
using System.Collections.Generic;
using System.Text;

namespace AiCharacterKit.Core
{
    /// <summary>
    /// Selects a deterministic bounded fact set before constructing one grounding snapshot.
    /// </summary>
    public static class NpcContextAssembler
    {
        /// <summary>
        /// Builds one bounded snapshot and reports low-priority fact IDs omitted by its budget.
        /// </summary>
        public static NpcGroundingSnapshot CreateSnapshot(
            string background,
            string goalsAndValues,
            IEnumerable<string> behavioralRules,
            IEnumerable<string> dialogueExamples,
            IEnumerable<NpcContextFact> facts,
            out IReadOnlyList<string> omittedFactIds)
        {
            var orderedFacts = CopyAndOrderFacts(facts);
            var selectedFacts = new List<NpcContextFact>();
            var omittedIds = new List<string>();
            var selectedBytes = 0;
            foreach (var fact in orderedFacts)
            {
                var factBytes = Encoding.UTF8.GetByteCount(fact.Statement);
                if (selectedFacts.Count >= NpcGroundingSnapshot.MaxFactCount
                    || selectedBytes + factBytes > NpcGroundingSnapshot.MaxTotalFactUtf8Bytes)
                {
                    omittedIds.Add(fact.FactId);
                    continue;
                }

                selectedFacts.Add(fact);
                selectedBytes += factBytes;
            }

            omittedFactIds = omittedIds.AsReadOnly();
            return new NpcGroundingSnapshot(
                background,
                goalsAndValues,
                behavioralRules,
                dialogueExamples,
                selectedFacts);
        }

        /// <summary>
        /// Copies facts, rejects duplicate IDs, and applies the shared deterministic order.
        /// </summary>
        private static List<NpcContextFact> CopyAndOrderFacts(
            IEnumerable<NpcContextFact> facts)
        {
            var copy = new List<NpcContextFact>();
            if (facts == null)
            {
                return copy;
            }

            var knownIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (var fact in facts)
            {
                if (fact == null)
                {
                    throw new ArgumentException("Facts must not contain null.", nameof(facts));
                }

                if (!knownIds.Add(fact.FactId))
                {
                    throw new ArgumentException(
                        $"Fact IDs must be unique: {fact.FactId}.",
                        nameof(facts));
                }

                copy.Add(fact);
            }

            copy.Sort((left, right) =>
            {
                var priorityComparison = right.Priority.CompareTo(left.Priority);
                return priorityComparison != 0
                    ? priorityComparison
                    : string.Compare(left.FactId, right.FactId, StringComparison.Ordinal);
            });
            return copy;
        }
    }
}
