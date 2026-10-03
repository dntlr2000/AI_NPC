using System;
using System.Collections.Generic;
using AiCharacterKit.Core;
using UnityEngine;

namespace AiCharacterKit.Unity
{
    /// <summary>
    /// Combines character canon, reusable lore, and current consumer facts for one turn.
    /// </summary>
    public sealed class NpcContextCoordinator : MonoBehaviour
    {
        [SerializeField]
        private List<NpcLoreProfile> loreProfiles = new List<NpcLoreProfile>();

        [SerializeField]
        private List<MonoBehaviour> contextProviderSources = new List<MonoBehaviour>();

        public IReadOnlyList<NpcLoreProfile> LoreProfiles =>
            loreProfiles != null
                ? (IReadOnlyList<NpcLoreProfile>)loreProfiles
                : Array.Empty<NpcLoreProfile>();

        public IReadOnlyList<MonoBehaviour> ContextProviderSources =>
            contextProviderSources != null
                ? (IReadOnlyList<MonoBehaviour>)contextProviderSources
                : Array.Empty<MonoBehaviour>();

        /// <summary>
        /// Captures and assembles one bounded snapshot without retaining mutable provider data.
        /// </summary>
        public bool TryCreateSnapshot(
            CharacterProfile characterProfile,
            out NpcGroundingSnapshot snapshot,
            out IReadOnlyList<string> omittedFactIds,
            out string error)
        {
            snapshot = NpcGroundingSnapshot.Empty;
            omittedFactIds = Array.Empty<string>();
            if (characterProfile == null)
            {
                error = "A CharacterProfile is required to build NPC grounding.";
                return false;
            }

            if (!TryCollectFacts(out var facts, out error))
            {
                return false;
            }

            return TryAssembleSnapshot(characterProfile, facts, out snapshot, out omittedFactIds, out error);
        }

        /// <summary>
        /// Validates authored data and provider references without capturing live game state.
        /// </summary>
        public bool TryValidateConfiguration(CharacterProfile characterProfile, out string error)
        {
            if (characterProfile == null)
            {
                error = "A CharacterProfile is required to build NPC grounding.";
                return false;
            }

            if (!characterProfile.TryValidate(out error)
                || !TryCollectAuthoredFacts(out var facts, out error)
                || !TryValidateProviderSources(out error))
            {
                return false;
            }

            return TryAssembleSnapshot(characterProfile, facts, out _, out _, out error);
        }

        /// <summary>
        /// Converts collected facts and canon to the same bounded immutable snapshot for both checks.
        /// </summary>
        private static bool TryAssembleSnapshot(
            CharacterProfile characterProfile,
            IReadOnlyList<NpcContextFact> facts,
            out NpcGroundingSnapshot snapshot,
            out IReadOnlyList<string> omittedFactIds,
            out string error)
        {
            snapshot = NpcGroundingSnapshot.Empty;
            omittedFactIds = Array.Empty<string>();
            try
            {
                snapshot = NpcContextAssembler.CreateSnapshot(
                    characterProfile.Background,
                    characterProfile.GoalsAndValues,
                    characterProfile.BehavioralRules,
                    characterProfile.AdditionalDialogueExamples,
                    facts,
                    out omittedFactIds);
                error = string.Empty;
                return true;
            }
            catch (Exception exception) when (
                exception is ArgumentException
                || exception is InvalidOperationException)
            {
                error = $"NPC grounding could not be assembled: {exception.Message}";
                return false;
            }
        }

        /// <summary>
        /// Preserves the live validation API, including provider capture and current fact validation.
        /// </summary>
        public bool TryValidate(CharacterProfile characterProfile, out string error)
        {
            return TryCreateSnapshot(
                characterProfile,
                out _,
                out _,
                out error);
        }

        /// <summary>
        /// Collects authored lore and rejects missing or repeated assets without reading providers.
        /// </summary>
        private bool TryCollectAuthoredFacts(
            out List<NpcContextFact> facts,
            out string error)
        {
            facts = new List<NpcContextFact>();
            var knownProfiles = new HashSet<NpcLoreProfile>();
            for (var index = 0; index < LoreProfiles.Count; index++)
            {
                var profile = LoreProfiles[index];
                if (profile == null)
                {
                    error = $"Lore profile {index} is missing.";
                    return false;
                }

                if (!knownProfiles.Add(profile))
                {
                    error = $"Lore profile '{profile.name}' is assigned more than once.";
                    return false;
                }

                if (!profile.TryCreateFacts(out var profileFacts, out var profileError))
                {
                    error = $"Lore profile '{profile.name}' is invalid: {profileError}";
                    return false;
                }

                facts.AddRange(profileFacts);
            }

            error = string.Empty;
            return true;
        }

        /// <summary>
        /// Checks provider references and interfaces without calling consumer capture methods.
        /// </summary>
        private bool TryValidateProviderSources(out string error)
        {
            var knownProviders = new HashSet<MonoBehaviour>();
            for (var index = 0; index < ContextProviderSources.Count; index++)
            {
                var source = ContextProviderSources[index];
                if (source == null)
                {
                    error = $"Context provider source {index} is missing.";
                    return false;
                }

                if (!knownProviders.Add(source))
                {
                    error = $"Context provider '{source.name}' is assigned more than once.";
                    return false;
                }

                if (!(source is INpcContextProvider))
                {
                    error = $"Context provider '{source.name}' must implement INpcContextProvider.";
                    return false;
                }
            }

            error = string.Empty;
            return true;
        }

        /// <summary>
        /// Captures each live provider once and copies its facts while containing consumer failures.
        /// </summary>
        private bool TryCollectFacts(out IReadOnlyList<NpcContextFact> facts, out string error)
        {
            facts = Array.Empty<NpcContextFact>();
            if (!TryCollectAuthoredFacts(out var collected, out error)
                || !TryValidateProviderSources(out error))
            {
                return false;
            }

            foreach (var source in ContextProviderSources)
            {
                try
                {
                    var providerFacts = ((INpcContextProvider)source).CaptureFacts();
                    if (providerFacts == null)
                    {
                        error = $"Context provider '{source.name}' returned no fact collection.";
                        return false;
                    }

                    for (var index = 0; index < providerFacts.Count; index++)
                    {
                        var fact = providerFacts[index];
                        if (fact == null)
                        {
                            error = $"Context provider '{source.name}' returned a null fact.";
                            return false;
                        }

                        collected.Add(fact);
                    }
                }
                catch
                {
                    error = $"Context provider '{source.name}' failed while capturing facts.";
                    return false;
                }
            }

            facts = collected.AsReadOnly();
            error = string.Empty;
            return true;
        }
    }
}
