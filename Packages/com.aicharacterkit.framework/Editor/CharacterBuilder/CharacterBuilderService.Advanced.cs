using System;
using System.Collections.Generic;
using AiCharacterKit.Behavior;
using AiCharacterKit.Core;
using AiCharacterKit.Unity;
using AiCharacterKit.Unity.Actions;
using AiCharacterKit.Unity.Behavior;
using UnityEditor;
using UnityEngine;

namespace AiCharacterKit.Editor
{
    internal static partial class CharacterBuilderService
    {
        /// <summary>Validates Advanced references and authored facts without invoking any consumer callback.</summary>
        private static void ValidateAdvanced(CharacterBuilderConfiguration configuration, bool validTarget, CharacterBuilderValidationReport report)
        {
            var profile = configuration.BehaviorProfile;
            if (profile == null || !EditorUtility.IsPersistent(profile)) { report.AddError("Advanced requires a saved NpcBehaviorProfile asset."); return; }
            if (!profile.TryValidate(out var error)) { report.AddError(error); return; }
            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (var source in configuration.ActionHandlerSources ?? Array.Empty<MonoBehaviour>())
            {
                if (source == null || !(source is INpcActionHandler handler) || !NpcTriggerDefinition.IsValidIdentifier(handler.ActionId) || !ids.Add(handler.ActionId))
                    report.AddError("Advanced handlers must expose unique valid action IDs.");
                if (validTarget && source != null) ValidateReferenceContext(configuration.Target, source, "Action handler", true, report);
            }
            var draft = profile.CreateDraft();
            foreach (var rule in draft.actionRules)
                if (!ids.Contains(rule.actionId)) report.AddError("No selected handler provides actionId '" + rule.actionId + "'.");
            var providers = new HashSet<MonoBehaviour>();
            foreach (var source in configuration.BehaviorValueProviderSources ?? Array.Empty<MonoBehaviour>())
            {
                if (source == null || !(source is INpcBehaviorValueProvider) || !providers.Add(source)) report.AddError("Select unique INpcBehaviorValueProvider components.");
                if (validTarget && source != null) ValidateReferenceContext(configuration.Target, source, "Behavior value provider", true, report);
            }
            if (draft.externalValues.Count > 0 && providers.Count == 0) report.AddError("Declared external values require an explicit provider.");
            var factIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (var variable in draft.variables) if (variable.exposeToDialogue) factIds.Add(variable.id);
            if (factIds.Count > 0 && configuration.ConversationMode == NpcConversationMode.BackendActions)
                report.AddWarning("V3 evaluates Advanced rules but does not send variables as grounding. Use BackendContext or LocalInference for dialogue grounding.");
            if (configuration.ConfigureGrounding)
                foreach (var lore in configuration.LoreProfiles ?? Array.Empty<NpcLoreProfile>())
                    if (lore != null && lore.TryCreateFacts(out var facts, out _))
                        foreach (var fact in facts) if (factIds.Contains(fact.FactId)) report.AddError("Behavior grounding ID conflicts with authored lore: " + fact.FactId);
        }

        /// <summary>Connects the target's optional variable bridge only for active Advanced grounding, without deleting components.</summary>
        private static MonoBehaviour[] AppendBehaviorGroundingProvider(CharacterBuilderConfiguration configuration, MonoBehaviour[] sources, bool useUndo)
        {
            var result = new List<MonoBehaviour>(sources);
            var enabled = configuration.ConfigureActions && configuration.BehaviorMode == NpcBehaviorMode.Advanced
                && configuration.BehaviorProfile != null && configuration.BehaviorProfile.CreateDraft().variables.Exists(variable => variable.exposeToDialogue);
            if (!enabled)
            {
                result.RemoveAll(source => source is NpcBehaviorContextProvider && source.gameObject == configuration.Target);
                return result.ToArray();
            }
            var bridge = GetOrAddComponent<NpcBehaviorContextProvider>(configuration.Target, useUndo);
            if (!result.Contains(bridge)) result.Add(bridge);
            return result.ToArray();
        }
    }
}
