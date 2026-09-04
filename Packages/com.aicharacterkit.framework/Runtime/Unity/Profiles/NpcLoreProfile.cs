using System;
using System.Collections.Generic;
using AiCharacterKit.Core;
using UnityEngine;

namespace AiCharacterKit.Unity
{
    /// <summary>
    /// Stores reusable lore and subjective beliefs without retaining runtime game state.
    /// </summary>
    [CreateAssetMenu(
        fileName = "NpcLoreProfile",
        menuName = "AI Character Kit/NPC Lore Profile")]
    public sealed class NpcLoreProfile : ScriptableObject
    {
        [SerializeField]
        private List<NpcLoreEntry> loreFacts = new List<NpcLoreEntry>();

        [SerializeField]
        private List<NpcLoreEntry> beliefs = new List<NpcLoreEntry>();

        public IReadOnlyList<NpcLoreEntry> LoreFacts =>
            loreFacts != null
                ? (IReadOnlyList<NpcLoreEntry>)loreFacts
                : Array.Empty<NpcLoreEntry>();

        public IReadOnlyList<NpcLoreEntry> Beliefs =>
            beliefs != null
                ? (IReadOnlyList<NpcLoreEntry>)beliefs
                : Array.Empty<NpcLoreEntry>();

        /// <summary>
        /// Converts authored entries into validated immutable Core facts.
        /// </summary>
        public bool TryCreateFacts(
            out IReadOnlyList<NpcContextFact> facts,
            out string error)
        {
            var result = new List<NpcContextFact>();
            if (!TryAppendEntries(LoreFacts, NpcContextFactKind.Lore, result, out error)
                || !TryAppendEntries(Beliefs, NpcContextFactKind.Belief, result, out error))
            {
                facts = Array.Empty<NpcContextFact>();
                return false;
            }

            facts = result.AsReadOnly();
            error = string.Empty;
            return true;
        }

        /// <summary>
        /// Verifies every authored entry without exposing a mutable result.
        /// </summary>
        public bool TryValidate(out string error)
        {
            return TryCreateFacts(out _, out error);
        }

        /// <summary>
        /// Appends one serialized entry group using its trusted semantic kind.
        /// </summary>
        private static bool TryAppendEntries(
            IReadOnlyList<NpcLoreEntry> entries,
            NpcContextFactKind kind,
            ICollection<NpcContextFact> destination,
            out string error)
        {
            for (var index = 0; index < entries.Count; index++)
            {
                var entry = entries[index];
                if (entry == null)
                {
                    error = $"Lore entry {index} is missing.";
                    return false;
                }

                try
                {
                    destination.Add(entry.CreateFact(kind));
                }
                catch (ArgumentException exception)
                {
                    error = $"Lore entry {index} is invalid: {exception.Message}";
                    return false;
                }
            }

            error = string.Empty;
            return true;
        }
    }

    /// <summary>
    /// Stores one inspector-authored fact before it becomes an immutable Core value.
    /// </summary>
    [Serializable]
    public sealed class NpcLoreEntry
    {
        [SerializeField]
        private string factId = string.Empty;

        [SerializeField]
        [TextArea(2, 5)]
        private string statement = string.Empty;

        [SerializeField]
        [Range(NpcContextFact.MinPriority, NpcContextFact.MaxPriority)]
        private int priority = 50;

        public string FactId => factId ?? string.Empty;

        public string Statement => statement ?? string.Empty;

        public int Priority => priority;

        /// <summary>
        /// Creates one validated Core fact using the entry's assigned semantic kind.
        /// </summary>
        internal NpcContextFact CreateFact(NpcContextFactKind kind)
        {
            return new NpcContextFact(FactId, kind, Statement, Priority);
        }
    }
}
