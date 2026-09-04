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
        /// Validates configured assets and provider components without requiring a conversation.
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
        /// Reads all configured sources and rejects missing, duplicate, or throwing providers.
        /// </summary>
        private bool TryCollectFacts(
            out IReadOnlyList<NpcContextFact> facts,
            out string error)
        {
            var collected = new List<NpcContextFact>();
            var knownProfiles = new HashSet<NpcLoreProfile>();
            for (var index = 0; index < LoreProfiles.Count; index++)
            {
                var profile = LoreProfiles[index];
                if (profile == null)
                {
                    facts = Array.Empty<NpcContextFact>();
                    error = $"Lore profile {index} is missing.";
                    return false;
                }

                if (!knownProfiles.Add(profile))
                {
                    facts = Array.Empty<NpcContextFact>();
                    error = $"Lore profile '{profile.name}' is assigned more than once.";
                    return false;
                }

                if (!profile.TryCreateFacts(out var profileFacts, out var profileError))
                {
                    facts = Array.Empty<NpcContextFact>();
                    error = $"Lore profile '{profile.name}' is invalid: {profileError}";
                    return false;
                }

                collected.AddRange(profileFacts);
            }

            var knownProviders = new HashSet<MonoBehaviour>();
            for (var index = 0; index < ContextProviderSources.Count; index++)
            {
                var source = ContextProviderSources[index];
                if (source == null)
                {
                    facts = Array.Empty<NpcContextFact>();
                    error = $"Context provider source {index} is missing.";
                    return false;
                }

                if (!knownProviders.Add(source))
                {
                    facts = Array.Empty<NpcContextFact>();
                    error = $"Context provider '{source.name}' is assigned more than once.";
                    return false;
                }

                if (!(source is INpcContextProvider provider))
                {
                    facts = Array.Empty<NpcContextFact>();
                    error = $"Context provider '{source.name}' must implement INpcContextProvider.";
                    return false;
                }

                IReadOnlyList<NpcContextFact> providerFacts;
                try
                {
                    providerFacts = provider.CaptureFacts();
                }
                catch
                {
                    facts = Array.Empty<NpcContextFact>();
                    error = $"Context provider '{source.name}' failed while capturing facts.";
                    return false;
                }

                if (providerFacts == null)
                {
                    facts = Array.Empty<NpcContextFact>();
                    error = $"Context provider '{source.name}' returned no fact collection.";
                    return false;
                }

                for (var factIndex = 0; factIndex < providerFacts.Count; factIndex++)
                {
                    if (providerFacts[factIndex] == null)
                    {
                        facts = Array.Empty<NpcContextFact>();
                        error = $"Context provider '{source.name}' returned a null fact.";
                        return false;
                    }

                    collected.Add(providerFacts[factIndex]);
                }
            }

            facts = collected.AsReadOnly();
            error = string.Empty;
            return true;
        }
    }
}
