using System;
using System.Collections.Generic;
using System.Linq;
using AiCharacterKit.Behavior;
using AiCharacterKit.Core;
using AiCharacterKit.Unity.Actions;
using AiCharacterKit.Unity.Behavior;
using UnityEditor;
using UnityEngine;

namespace AiCharacterKit.Editor
{
    internal sealed partial class CharacterBuilderWindow
    {
        private NpcBehaviorProfile selectedBehaviorProfile;
        private NpcActionProfile importBasicProfile;
        private NpcBehaviorDefinition behaviorDraft = new NpcBehaviorDefinition();
        private string behaviorAssetName = "NpcBehaviorProfile";
        private readonly Dictionary<string, NpcBehaviorValue> previewValues = new Dictionary<string, NpcBehaviorValue>();
        private readonly HashSet<string> previewSignals = new HashSet<string>();
        private NpcBehaviorTrace behaviorPreview;

        /// <summary>Draws detached Advanced authoring, explicit migration, reference selection and pure preview.</summary>
        private void DrawAdvancedBehavior()
        {
            EditorGUILayout.HelpBox("Variables belong to each NPC. Memory reset preserves variables; behavior reset clears variables and action gates. Runtime changes affect the next grounded request.", MessageType.Info);
            var profile = (NpcBehaviorProfile)EditorGUILayout.ObjectField("Existing Behavior Profile", selectedBehaviorProfile, typeof(NpcBehaviorProfile), false);
            if (profile != selectedBehaviorProfile) LoadBehaviorProfile(profile);
            behaviorAssetName = EditorGUILayout.TextField("Behavior Asset Name", behaviorAssetName);
            importBasicProfile = (NpcActionProfile)EditorGUILayout.ObjectField("Basic profile to import", importBasicProfile, typeof(NpcActionProfile), false);
            using (new EditorGUI.DisabledScope(importBasicProfile == null))
                if (GUILayout.Button("Import Basic into new unsaved draft"))
                {
                    try { LoadBehaviorProfile(null); behaviorDraft = CharacterBuilderAssetService.ImportBasicBehavior(importBasicProfile); }
                    catch (ArgumentException exception) { SetStatus(exception.Message, MessageType.Error); }
                }
            BehaviorAuthoringGUI.Draw(behaviorDraft);
            if (!NpcBehaviorValidator.TryValidate(behaviorDraft, out var error)) EditorGUILayout.HelpBox(error, MessageType.Error);
            using (new EditorGUI.DisabledScope(!NpcBehaviorValidator.TryValidate(behaviorDraft, out _)))
                if (GUILayout.Button(selectedBehaviorProfile == null ? "Create Behavior Profile" : "Save Behavior Profile"))
                {
                    if (CharacterBuilderAssetService.TrySaveBehaviorProfile(selectedBehaviorProfile, behaviorDraft, assetFolder, behaviorAssetName, out var saved, out error))
                    { LoadBehaviorProfile(saved); SetStatus("Behavior profile saved. Existing runtimes retain their snapshots until recreated.", MessageType.Info); }
                    else SetStatus(error, MessageType.Error);
                }
            if (GUILayout.Button("New behavior draft")) LoadBehaviorProfile(null);
            configuration.BehaviorProfile = selectedBehaviorProfile;
            configuration.EnableBehaviorTrace = EditorGUILayout.Toggle("Enable runtime trace", configuration.EnableBehaviorTrace);
            DrawActionHandlerSelections();
            DrawBehaviorProviderSelections();
            DrawBehaviorPreview();
        }

        /// <summary>Loads a detached authoring copy and clears stale preview inputs.</summary>
        private void LoadBehaviorProfile(NpcBehaviorProfile profile)
        {
            selectedBehaviorProfile = profile; configuration.BehaviorProfile = profile;
            behaviorDraft = profile == null ? new NpcBehaviorDefinition() : profile.CreateDraft();
            behaviorAssetName = profile == null ? "NpcBehaviorProfile" : profile.name;
            previewValues.Clear(); previewSignals.Clear(); behaviorPreview = null;
        }

        /// <summary>Reuses the existing handler chooser with only the active mode's required action IDs.</summary>
        private IEnumerable<ActionBindingDraft> GetActiveActionBindings()
        {
            if (configuration.BehaviorMode != NpcBehaviorMode.Advanced) return actionDraft.Bindings;
            return behaviorDraft.actionRules.Select(rule => new ActionBindingDraft { ActionId = rule.actionId });
        }

        /// <summary>Restores Advanced references when an existing Scene or Prefab target is selected.</summary>
        private void LoadAdvancedConfiguration(SerializedObject source)
        {
            configuration.BehaviorMode = (NpcBehaviorMode)source.FindProperty("behaviorMode").intValue;
            configuration.EnableBehaviorTrace = source.FindProperty("enableBehaviorTrace").boolValue;
            LoadBehaviorProfile(source.FindProperty("behaviorProfile").objectReferenceValue as NpcBehaviorProfile);
            var providers = source.FindProperty("behaviorValueProviderSources");
            var values = new List<MonoBehaviour>();
            for (var index = 0; index < providers.arraySize; index++) values.Add(providers.GetArrayElementAtIndex(index).objectReferenceValue as MonoBehaviour);
            configuration.BehaviorValueProviderSources = values.ToArray();
        }

        /// <summary>Selects explicit target-owned typed providers by interface without reading their values.</summary>
        private void DrawBehaviorProviderSelections()
        {
            EditorGUILayout.LabelField("Typed game value providers", EditorStyles.boldLabel);
            if (configuration.Target == null) { EditorGUILayout.HelpBox("Select a target to connect providers.", MessageType.Info); return; }
            var selected = new HashSet<MonoBehaviour>(configuration.BehaviorValueProviderSources ?? Array.Empty<MonoBehaviour>());
            foreach (var candidate in configuration.Target.GetComponentsInChildren<MonoBehaviour>(true))
            {
                if (!(candidate is INpcBehaviorValueProvider)) continue;
                var include = EditorGUILayout.Toggle(GetComponentLabel(configuration.Target, candidate), selected.Contains(candidate));
                if (include) selected.Add(candidate); else selected.Remove(candidate);
            }
            configuration.BehaviorValueProviderSources = selected.Where(value => value != null).ToArray();
        }

        /// <summary>Simulates one turn from explicit variables, external facts and matched IDs without live effects.</summary>
        private void DrawBehaviorPreview()
        {
            EditorGUILayout.Space(); EditorGUILayout.LabelField("Advanced preview (one simulated turn)", EditorStyles.boldLabel);
            var variables = new Dictionary<string, NpcBehaviorValue>(); var external = new Dictionary<string, NpcBehaviorValue>();
            foreach (var variable in behaviorDraft.variables)
                variables[variable.id ?? string.Empty] = DrawPreviewValue(variable.id, variable.kind,
                    variable.kind == NpcBehaviorValueKind.Boolean ? new NpcBehaviorValue(variable.defaultBoolean)
                        : new NpcBehaviorValue(double.IsNaN(variable.defaultNumber) || double.IsInfinity(variable.defaultNumber) ? 0 : variable.defaultNumber));
            foreach (var declaration in behaviorDraft.externalValues)
                external[declaration.id ?? string.Empty] = DrawPreviewValue(declaration.id, declaration.kind,
                    declaration.kind == NpcBehaviorValueKind.Boolean ? new NpcBehaviorValue(false) : new NpcBehaviorValue(0d));
            foreach (var signal in behaviorDraft.signals)
            {
                var selected = EditorGUILayout.Toggle("Match " + signal.triggerId, previewSignals.Contains(signal.triggerId));
                if (selected) previewSignals.Add(signal.triggerId); else previewSignals.Remove(signal.triggerId);
            }
            using (new EditorGUI.DisabledScope(!NpcBehaviorValidator.TryValidate(behaviorDraft, out _)))
                if (GUILayout.Button("Evaluate without game effects"))
                {
                    try { behaviorPreview = CharacterBuilderAssetService.PreviewBehavior(behaviorDraft, variables, external,
                        behaviorDraft.signals.Where(signal => previewSignals.Contains(signal.triggerId)).Select(signal => signal.triggerId).ToArray()); }
                    catch (ArgumentException exception) { SetStatus(exception.Message, MessageType.Error); }
                }
            if (behaviorPreview == null) return;
            EditorGUILayout.LabelField("Matched signals: " + string.Join(", ", behaviorPreview.TriggerIds));
            EditorGUILayout.LabelField("Result: " + behaviorPreview.Code + " / " + behaviorPreview.ActionResult.ActionId);
            foreach (var change in behaviorPreview.Changes) EditorGUILayout.LabelField(change.RuleId + ": " + change.VariableId + " " + change.Before + " → " + change.After);
            foreach (var rule in behaviorPreview.Rules) EditorGUILayout.LabelField(rule.RuleId + ": " + rule.Reason);
        }

        /// <summary>Edits one typed synthetic preview value and retains it until the draft changes.</summary>
        private NpcBehaviorValue DrawPreviewValue(string id, NpcBehaviorValueKind kind, NpcBehaviorValue fallback)
        {
            id = id ?? string.Empty;
            if (!previewValues.TryGetValue(id, out var value) || value.Kind != kind) value = fallback;
            if (kind == NpcBehaviorValueKind.Boolean) value = new NpcBehaviorValue(EditorGUILayout.Toggle("Preview " + id, value.Boolean));
            else
            {
                var number = EditorGUILayout.DoubleField("Preview " + id, value.Number);
                if (!double.IsNaN(number) && !double.IsInfinity(number)) value = new NpcBehaviorValue(number);
            }
            previewValues[id] = value; return value;
        }
    }
}
