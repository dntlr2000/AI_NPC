using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using AiCharacterKit.Core;
using AiCharacterKit.Unity;
using AiCharacterKit.Unity.Actions;
using AiCharacterKit.Unity.Speech;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace AiCharacterKit.Editor
{
    /// <summary>
    /// Creates, updates, validates, and previews consumer-owned character data assets.
    /// </summary>
    internal static class CharacterBuilderAssetService
    {
        /// <summary>
        /// Creates one validated CharacterProfile at a unique writable Assets path.
        /// </summary>
        public static bool TryCreateCharacterProfile(
            CharacterProfileDraft draft,
            string folderPath,
            out CharacterProfile profile,
            out string error)
        {
            profile = null;
            error = string.Empty;
            if (!TryNormalizeWritableFolder(folderPath, out var folder, out error))
            {
                return false;
            }

            if (!TryCreateTransientProfile(draft, out var transient, out error))
            {
                return false;
            }

            var assetPath = string.Empty;
            try
            {
                EnsureFolder(folder);
                var assetName = GetSafeAssetName(
                    draft.AssetName,
                    draft.DisplayName,
                    "CharacterProfile");
                assetPath = AssetDatabase.GenerateUniqueAssetPath(
                    folder + "/" + assetName + ".asset");
                transient.name = assetName;
                AssetDatabase.CreateAsset(transient, assetPath);
                Undo.RegisterCreatedObjectUndo(
                    transient,
                    "Create AI Character Profile");
                AssetDatabase.SaveAssets();
                AssetDatabase.ImportAsset(
                    assetPath,
                    ImportAssetOptions.ForceSynchronousImport);
                profile = AssetDatabase.LoadAssetAtPath<CharacterProfile>(assetPath);
                if (profile == null || !profile.TryValidate(out error))
                {
                    throw new InvalidOperationException(
                        string.IsNullOrWhiteSpace(error)
                            ? "Unity could not reload the created CharacterProfile."
                            : error);
                }

                return true;
            }
            catch (Exception exception)
            {
                if (!string.IsNullOrEmpty(assetPath)
                    && AssetDatabase.LoadMainAssetAtPath(assetPath) != null)
                {
                    AssetDatabase.DeleteAsset(assetPath);
                }

                if (transient != null && !EditorUtility.IsPersistent(transient))
                {
                    Object.DestroyImmediate(transient);
                }

                profile = null;
                error = "CharacterProfile creation failed: " + exception.Message;
                return false;
            }
        }

        /// <summary>
        /// Updates one consumer-owned CharacterProfile only after validating a detached copy.
        /// </summary>
        public static bool TryUpdateCharacterProfile(
            CharacterProfile profile,
            CharacterProfileDraft draft,
            out string error)
        {
            error = string.Empty;
            if (!TryValidateWritableAsset(profile, "CharacterProfile", out error))
            {
                return false;
            }

            if (!TryCreateTransientProfile(draft, out var validatedCopy, out error))
            {
                return false;
            }

            Object.DestroyImmediate(validatedCopy);
            Undo.IncrementCurrentGroup();
            var undoGroup = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Update AI Character Profile");
            try
            {
                Undo.RecordObject(profile, "Update AI Character Profile");
                ApplyProfileDraft(profile, draft);
                profile.name = GetSafeAssetName(
                    draft.AssetName,
                    draft.DisplayName,
                    profile.name);
                EditorUtility.SetDirty(profile);
                AssetDatabase.SaveAssets();
                Undo.CollapseUndoOperations(undoGroup);
                return true;
            }
            catch (Exception exception)
            {
                Undo.RevertAllDownToGroup(undoGroup);
                error = "CharacterProfile update failed: " + exception.Message;
                return false;
            }
        }

        /// <summary>
        /// Creates one validated opaque voice preset asset at a unique writable Assets path.
        /// </summary>
        public static bool TryCreateVoiceProfile(
            VoiceProfileDraft draft,
            string folderPath,
            out NpcVoiceProfile profile,
            out string error)
        {
            profile = null;
            error = string.Empty;
            if (!TryNormalizeWritableFolder(folderPath, out var folder, out error))
            {
                return false;
            }

            if (!TryCreateTransientVoiceProfile(draft, out var transient, out error))
            {
                return false;
            }

            var assetPath = string.Empty;
            try
            {
                EnsureFolder(folder);
                var assetName = GetSafeAssetName(
                    draft.AssetName,
                    null,
                    "NpcVoiceProfile");
                assetPath = AssetDatabase.GenerateUniqueAssetPath(
                    folder + "/" + assetName + ".asset");
                transient.name = assetName;
                AssetDatabase.CreateAsset(transient, assetPath);
                Undo.RegisterCreatedObjectUndo(
                    transient,
                    "Create AI NPC Voice Profile");
                AssetDatabase.SaveAssets();
                AssetDatabase.ImportAsset(
                    assetPath,
                    ImportAssetOptions.ForceSynchronousImport);
                profile = AssetDatabase.LoadAssetAtPath<NpcVoiceProfile>(assetPath);
                if (profile == null || !profile.TryValidate(out error))
                {
                    throw new InvalidOperationException(
                        string.IsNullOrWhiteSpace(error)
                            ? "Unity could not reload the created voice profile."
                            : error);
                }

                return true;
            }
            catch (Exception exception)
            {
                if (!string.IsNullOrEmpty(assetPath)
                    && AssetDatabase.LoadMainAssetAtPath(assetPath) != null)
                {
                    AssetDatabase.DeleteAsset(assetPath);
                }

                if (transient != null && !EditorUtility.IsPersistent(transient))
                {
                    Object.DestroyImmediate(transient);
                }

                profile = null;
                error = "NpcVoiceProfile creation failed: " + exception.Message;
                return false;
            }
        }

        /// <summary>
        /// Updates one consumer-owned opaque voice profile after validating the new preset.
        /// </summary>
        public static bool TryUpdateVoiceProfile(
            NpcVoiceProfile profile,
            VoiceProfileDraft draft,
            out string error)
        {
            error = string.Empty;
            if (!TryValidateWritableAsset(profile, "NpcVoiceProfile", out error))
            {
                return false;
            }

            if (!TryCreateTransientVoiceProfile(draft, out var validatedCopy, out error))
            {
                return false;
            }

            Object.DestroyImmediate(validatedCopy);
            Undo.IncrementCurrentGroup();
            var undoGroup = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Update AI NPC Voice Profile");
            try
            {
                Undo.RecordObject(profile, "Update AI NPC Voice Profile");
                var serializedProfile = new SerializedObject(profile);
                serializedProfile.FindProperty("voicePresetId").stringValue =
                    draft.VoicePresetId;
                serializedProfile.ApplyModifiedPropertiesWithoutUndo();
                profile.name = GetSafeAssetName(
                    draft.AssetName,
                    null,
                    profile.name);
                EditorUtility.SetDirty(profile);
                AssetDatabase.SaveAssets();
                Undo.CollapseUndoOperations(undoGroup);
                return true;
            }
            catch (Exception exception)
            {
                Undo.RevertAllDownToGroup(undoGroup);
                error = "NpcVoiceProfile update failed: " + exception.Message;
                return false;
            }
        }

        /// <summary>
        /// Creates one validated consumer-owned NpcActionProfile at a unique Assets path.
        /// </summary>
        public static bool TryCreateActionProfile(
            ActionProfileDraft draft,
            string folderPath,
            out NpcActionProfile profile,
            out string error)
        {
            profile = null;
            error = string.Empty;
            if (!TryNormalizeWritableFolder(folderPath, out var folder, out error)
                || !TryCreateTransientActionProfile(draft, out var transient, out error))
            {
                return false;
            }

            var assetPath = string.Empty;
            try
            {
                EnsureFolder(folder);
                var assetName = GetSafeAssetName(
                    draft.AssetName,
                    null,
                    "NpcActionProfile");
                assetPath = AssetDatabase.GenerateUniqueAssetPath(
                    folder + "/" + assetName + ".asset");
                transient.name = assetName;
                AssetDatabase.CreateAsset(transient, assetPath);
                Undo.RegisterCreatedObjectUndo(transient, "Create AI NPC Action Profile");
                AssetDatabase.SaveAssets();
                AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceSynchronousImport);
                profile = AssetDatabase.LoadAssetAtPath<NpcActionProfile>(assetPath);
                if (profile == null || !profile.TryValidate(out error))
                {
                    throw new InvalidOperationException(
                        string.IsNullOrWhiteSpace(error)
                            ? "Unity could not reload the created NpcActionProfile."
                            : error);
                }

                return true;
            }
            catch (Exception exception)
            {
                if (!string.IsNullOrEmpty(assetPath)
                    && AssetDatabase.LoadMainAssetAtPath(assetPath) != null)
                {
                    AssetDatabase.DeleteAsset(assetPath);
                }

                if (transient != null && !EditorUtility.IsPersistent(transient))
                {
                    Object.DestroyImmediate(transient);
                }

                profile = null;
                error = "NpcActionProfile creation failed: " + exception.Message;
                return false;
            }
        }

        /// <summary>
        /// Updates one consumer-owned NpcActionProfile after validating a detached copy.
        /// </summary>
        public static bool TryUpdateActionProfile(
            NpcActionProfile profile,
            ActionProfileDraft draft,
            out string error)
        {
            error = string.Empty;
            if (!TryValidateWritableAsset(profile, "NpcActionProfile", out error)
                || !TryCreateTransientActionProfile(draft, out var copy, out error))
            {
                return false;
            }

            Object.DestroyImmediate(copy);
            Undo.IncrementCurrentGroup();
            var group = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Update AI NPC Action Profile");
            try
            {
                Undo.RecordObject(profile, "Update AI NPC Action Profile");
                ApplyActionProfileDraft(profile, draft);
                profile.name = GetSafeAssetName(draft.AssetName, null, profile.name);
                EditorUtility.SetDirty(profile);
                AssetDatabase.SaveAssets();
                Undo.CollapseUndoOperations(group);
                return true;
            }
            catch (Exception exception)
            {
                Undo.RevertAllDownToGroup(group);
                error = "NpcActionProfile update failed: " + exception.Message;
                return false;
            }
        }

        /// <summary>
        /// Creates one validated consumer-owned NpcLoreProfile at a unique Assets path.
        /// </summary>
        public static bool TryCreateLoreProfile(
            LoreProfileDraft draft,
            string folderPath,
            out NpcLoreProfile profile,
            out string error)
        {
            profile = null;
            error = string.Empty;
            if (!TryNormalizeWritableFolder(folderPath, out var folder, out error)
                || !TryCreateTransientLoreProfile(draft, out var transient, out error))
            {
                return false;
            }

            var assetPath = string.Empty;
            try
            {
                EnsureFolder(folder);
                var assetName = GetSafeAssetName(
                    draft.AssetName,
                    null,
                    "NpcLoreProfile");
                assetPath = AssetDatabase.GenerateUniqueAssetPath(
                    folder + "/" + assetName + ".asset");
                transient.name = assetName;
                AssetDatabase.CreateAsset(transient, assetPath);
                Undo.RegisterCreatedObjectUndo(transient, "Create AI NPC Lore Profile");
                AssetDatabase.SaveAssets();
                AssetDatabase.ImportAsset(
                    assetPath,
                    ImportAssetOptions.ForceSynchronousImport);
                profile = AssetDatabase.LoadAssetAtPath<NpcLoreProfile>(assetPath);
                if (profile == null || !profile.TryValidate(out error))
                {
                    throw new InvalidOperationException(
                        string.IsNullOrWhiteSpace(error)
                            ? "Unity could not reload the created NpcLoreProfile."
                            : error);
                }

                return true;
            }
            catch (Exception exception)
            {
                if (!string.IsNullOrEmpty(assetPath)
                    && AssetDatabase.LoadMainAssetAtPath(assetPath) != null)
                {
                    AssetDatabase.DeleteAsset(assetPath);
                }

                if (transient != null && !EditorUtility.IsPersistent(transient))
                {
                    Object.DestroyImmediate(transient);
                }

                profile = null;
                error = "NpcLoreProfile creation failed: " + exception.Message;
                return false;
            }
        }

        /// <summary>
        /// Updates one consumer-owned NpcLoreProfile after validating a detached copy.
        /// </summary>
        public static bool TryUpdateLoreProfile(
            NpcLoreProfile profile,
            LoreProfileDraft draft,
            out string error)
        {
            error = string.Empty;
            if (!TryValidateWritableAsset(profile, "NpcLoreProfile", out error)
                || !TryCreateTransientLoreProfile(draft, out var copy, out error))
            {
                return false;
            }

            Object.DestroyImmediate(copy);
            Undo.IncrementCurrentGroup();
            var group = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Update AI NPC Lore Profile");
            try
            {
                Undo.RecordObject(profile, "Update AI NPC Lore Profile");
                ApplyLoreProfileDraft(profile, draft);
                profile.name = GetSafeAssetName(draft.AssetName, null, profile.name);
                EditorUtility.SetDirty(profile);
                AssetDatabase.SaveAssets();
                Undo.CollapseUndoOperations(group);
                return true;
            }
            catch (Exception exception)
            {
                Undo.RevertAllDownToGroup(group);
                error = "NpcLoreProfile update failed: " + exception.Message;
                return false;
            }
        }

        /// <summary>
        /// Builds an authored-only grounding preview without invoking runtime providers or a network.
        /// </summary>
        public static bool TryPreviewGrounding(
            CharacterProfileDraft characterDraft,
            LoreProfileDraft loreDraft,
            out NpcGroundingSnapshot snapshot,
            out string error)
        {
            snapshot = NpcGroundingSnapshot.Empty;
            error = string.Empty;
            if (!TryCreateTransientProfile(characterDraft, out var character, out error)
                || !TryCreateTransientLoreProfile(loreDraft, out var lore, out error))
            {
                if (character != null)
                {
                    Object.DestroyImmediate(character);
                }

                return false;
            }

            try
            {
                if (!lore.TryCreateFacts(out var facts, out error))
                {
                    return false;
                }

                snapshot = NpcContextAssembler.CreateSnapshot(
                    character.Background,
                    character.GoalsAndValues,
                    character.BehavioralRules,
                    character.AdditionalDialogueExamples,
                    facts,
                    out _);
                return true;
            }
            catch (Exception exception)
            {
                snapshot = NpcGroundingSnapshot.Empty;
                error = "Grounding preview failed: " + exception.Message;
                return false;
            }
            finally
            {
                Object.DestroyImmediate(character);
                Object.DestroyImmediate(lore);
            }
        }

        /// <summary>
        /// Produces one deterministic network-free response from unsaved profile values.
        /// </summary>
        public static bool TryPreviewMock(
            CharacterProfileDraft draft,
            string userText,
            out AiNpcResponse response,
            out string error)
        {
            response = null;
            error = string.Empty;
            if (!TryCreateTransientProfile(draft, out var profile, out error))
            {
                return false;
            }

            try
            {
                var request = new AiNpcRequest(
                    profile.CharacterId,
                    profile.DisplayName,
                    profile.Personality,
                    profile.SpeechStyle,
                    profile.ExampleDialogue,
                    profile.DefaultEmotion,
                    userText);
                var client = new MockConversationClient(TimeSpan.Zero);
                response = client.SendAsync(request, CancellationToken.None)
                    .GetAwaiter()
                    .GetResult();
                return true;
            }
            catch (Exception exception)
            {
                error = "Mock preview failed: " + exception.Message;
                return false;
            }
            finally
            {
                Object.DestroyImmediate(profile);
            }
        }

        /// <summary>
        /// Previews deterministic Mock dialogue, matched IDs, and stable action selection.
        /// </summary>
        public static bool TryPreviewMockAction(
            CharacterProfileDraft characterDraft,
            ActionProfileDraft actionDraft,
            string userText,
            out AiNpcResponse response,
            out NpcTriggerDefinition selectedDefinition,
            out string error)
        {
            response = null;
            selectedDefinition = null;
            error = string.Empty;
            if (!TryCreateTransientProfile(characterDraft, out var character, out error)
                || !TryCreateTransientActionProfile(actionDraft, out var actions, out error))
            {
                if (character != null)
                {
                    Object.DestroyImmediate(character);
                }

                return false;
            }

            try
            {
                var definitions = actions.CreateDefinitions();
                var request = new AiNpcRequest(
                    character.CharacterId,
                    character.DisplayName,
                    character.Personality,
                    character.SpeechStyle,
                    character.ExampleDialogue,
                    character.DefaultEmotion,
                    userText);
                response = new MockConversationClient(TimeSpan.Zero, definitions)
                    .SendAsync(request, CancellationToken.None)
                    .GetAwaiter()
                    .GetResult();
                foreach (var definition in definitions)
                {
                    var matched = false;
                    foreach (var triggerId in response.MatchedTriggerIds)
                    {
                        if (string.Equals(
                            definition.TriggerId,
                            triggerId,
                            StringComparison.Ordinal))
                        {
                            matched = true;
                            break;
                        }
                    }

                    if (matched
                        && (selectedDefinition == null
                            || definition.Priority > selectedDefinition.Priority))
                    {
                        selectedDefinition = definition;
                    }
                }

                return true;
            }
            catch (Exception exception)
            {
                response = null;
                selectedDefinition = null;
                error = "Mock action preview failed: " + exception.Message;
                return false;
            }
            finally
            {
                Object.DestroyImmediate(character);
                Object.DestroyImmediate(actions);
            }
        }

        /// <summary>
        /// Finds other consumer or imported profile assets that share one opaque character ID.
        /// </summary>
        public static IReadOnlyList<string> FindDuplicateCharacterIdPaths(
            string characterId,
            CharacterProfile excludedProfile)
        {
            var duplicates = new List<string>();
            if (string.IsNullOrWhiteSpace(characterId))
            {
                return duplicates;
            }

            var excludedPath = excludedProfile == null
                ? string.Empty
                : AssetDatabase.GetAssetPath(excludedProfile);
            foreach (var guid in AssetDatabase.FindAssets(
                         "t:CharacterProfile",
                         new[] { "Assets" }))
            {
                var assetPath = AssetDatabase.GUIDToAssetPath(guid);
                if (string.Equals(assetPath, excludedPath, StringComparison.Ordinal))
                {
                    continue;
                }

                var profile = AssetDatabase.LoadAssetAtPath<CharacterProfile>(assetPath);
                var candidateId = profile == null ? string.Empty : profile.CharacterId;
                if (!string.IsNullOrWhiteSpace(candidateId)
                    && string.Equals(
                        candidateId.Trim(),
                        characterId.Trim(),
                        StringComparison.Ordinal))
                {
                    duplicates.Add(assetPath);
                }
            }

            duplicates.Sort(StringComparer.Ordinal);
            return duplicates;
        }

        /// <summary>
        /// Builds and validates one temporary CharacterProfile from detached values.
        /// </summary>
        private static bool TryCreateTransientProfile(
            CharacterProfileDraft draft,
            out CharacterProfile profile,
            out string error)
        {
            profile = null;
            error = string.Empty;
            if (draft == null)
            {
                error = "Character profile values are required.";
                return false;
            }

            profile = ScriptableObject.CreateInstance<CharacterProfile>();
            try
            {
                ApplyProfileDraft(profile, draft);
                if (!profile.TryValidate(out error))
                {
                    Object.DestroyImmediate(profile);
                    profile = null;
                    return false;
                }

                return true;
            }
            catch (Exception exception)
            {
                Object.DestroyImmediate(profile);
                profile = null;
                error = "Character profile values are invalid: " + exception.Message;
                return false;
            }
        }

        /// <summary>
        /// Copies draft values into the existing serialized CharacterProfile fields.
        /// </summary>
        private static void ApplyProfileDraft(
            CharacterProfile profile,
            CharacterProfileDraft draft)
        {
            var serializedProfile = new SerializedObject(profile);
            serializedProfile.FindProperty("characterId").stringValue =
                draft.CharacterId;
            serializedProfile.FindProperty("displayName").stringValue =
                draft.DisplayName;
            serializedProfile.FindProperty("personality").stringValue =
                draft.Personality;
            serializedProfile.FindProperty("speechStyle").stringValue =
                draft.SpeechStyle;
            serializedProfile.FindProperty("exampleDialogue").stringValue =
                draft.ExampleDialogue;
            serializedProfile.FindProperty("defaultEmotion").enumValueIndex =
                (int)draft.DefaultEmotion;
            serializedProfile.FindProperty("background").stringValue =
                draft.Background ?? string.Empty;
            serializedProfile.FindProperty("goalsAndValues").stringValue =
                draft.GoalsAndValues ?? string.Empty;
            SetStringArray(
                serializedProfile.FindProperty("behavioralRules"),
                draft.BehavioralRules);
            SetStringArray(
                serializedProfile.FindProperty("additionalDialogueExamples"),
                draft.AdditionalDialogueExamples);
            serializedProfile.ApplyModifiedPropertiesWithoutUndo();
        }

        /// <summary>
        /// Builds and validates one temporary voice profile from an opaque preset draft.
        /// </summary>
        private static bool TryCreateTransientVoiceProfile(
            VoiceProfileDraft draft,
            out NpcVoiceProfile profile,
            out string error)
        {
            profile = null;
            error = string.Empty;
            if (draft == null)
            {
                error = "Voice profile values are required.";
                return false;
            }

            profile = ScriptableObject.CreateInstance<NpcVoiceProfile>();
            try
            {
                var serializedProfile = new SerializedObject(profile);
                serializedProfile.FindProperty("voicePresetId").stringValue =
                    draft.VoicePresetId ?? string.Empty;
                serializedProfile.ApplyModifiedPropertiesWithoutUndo();
                if (profile.TryValidate(out error))
                {
                    return true;
                }

                Object.DestroyImmediate(profile);
                profile = null;
                return false;
            }
            catch (Exception exception)
            {
                Object.DestroyImmediate(profile);
                profile = null;
                error = "Voice profile values are invalid: " + exception.Message;
                return false;
            }
        }

        /// <summary>
        /// Builds and validates one temporary NpcActionProfile from detached values.
        /// </summary>
        private static bool TryCreateTransientActionProfile(
            ActionProfileDraft draft,
            out NpcActionProfile profile,
            out string error)
        {
            profile = null;
            error = string.Empty;
            if (draft == null)
            {
                error = "Action profile values are required.";
                return false;
            }

            profile = ScriptableObject.CreateInstance<NpcActionProfile>();
            try
            {
                ApplyActionProfileDraft(profile, draft);
                if (profile.TryValidate(out error))
                {
                    return true;
                }

                Object.DestroyImmediate(profile);
                profile = null;
                return false;
            }
            catch (Exception exception)
            {
                Object.DestroyImmediate(profile);
                profile = null;
                error = "Action profile values are invalid: " + exception.Message;
                return false;
            }
        }

        /// <summary>
        /// Builds and validates one temporary lore profile from detached values.
        /// </summary>
        private static bool TryCreateTransientLoreProfile(
            LoreProfileDraft draft,
            out NpcLoreProfile profile,
            out string error)
        {
            profile = null;
            error = string.Empty;
            if (draft == null)
            {
                error = "Lore profile values are required.";
                return false;
            }

            profile = ScriptableObject.CreateInstance<NpcLoreProfile>();
            try
            {
                ApplyLoreProfileDraft(profile, draft);
                if (profile.TryValidate(out error))
                {
                    return true;
                }

                Object.DestroyImmediate(profile);
                profile = null;
                return false;
            }
            catch (Exception exception)
            {
                Object.DestroyImmediate(profile);
                profile = null;
                error = "Lore profile values are invalid: " + exception.Message;
                return false;
            }
        }

        /// <summary>
        /// Copies detached binding values into serialized NpcActionProfile fields.
        /// </summary>
        private static void ApplyActionProfileDraft(
            NpcActionProfile profile,
            ActionProfileDraft draft)
        {
            var serializedProfile = new SerializedObject(profile);
            var bindings = serializedProfile.FindProperty("bindings");
            bindings.arraySize = draft.Bindings.Count;
            for (var index = 0; index < draft.Bindings.Count; index++)
            {
                var source = draft.Bindings[index] ?? new ActionBindingDraft();
                var target = bindings.GetArrayElementAtIndex(index);
                target.FindPropertyRelative("triggerId").stringValue = source.TriggerId;
                target.FindPropertyRelative("conditionDescription").stringValue =
                    source.ConditionDescription;
                target.FindPropertyRelative("exampleUserText").stringValue =
                    source.ExampleUserText;
                target.FindPropertyRelative("actionId").stringValue = source.ActionId;
                target.FindPropertyRelative("priority").intValue = source.Priority;
            }

            serializedProfile.ApplyModifiedPropertiesWithoutUndo();
        }

        /// <summary>
        /// Copies detached lore and belief entries into serialized NpcLoreProfile fields.
        /// </summary>
        private static void ApplyLoreProfileDraft(
            NpcLoreProfile profile,
            LoreProfileDraft draft)
        {
            var serializedProfile = new SerializedObject(profile);
            SetLoreEntries(
                serializedProfile.FindProperty("loreFacts"),
                draft.LoreFacts);
            SetLoreEntries(
                serializedProfile.FindProperty("beliefs"),
                draft.Beliefs);
            serializedProfile.ApplyModifiedPropertiesWithoutUndo();
        }

        /// <summary>
        /// Writes one detached text list into a serialized string array.
        /// </summary>
        private static void SetStringArray(
            SerializedProperty property,
            IReadOnlyList<string> values)
        {
            property.arraySize = values?.Count ?? 0;
            for (var index = 0; index < property.arraySize; index++)
            {
                property.GetArrayElementAtIndex(index).stringValue =
                    values[index] ?? string.Empty;
            }
        }

        /// <summary>
        /// Writes one detached lore group into its serialized entry array.
        /// </summary>
        private static void SetLoreEntries(
            SerializedProperty property,
            IReadOnlyList<LoreEntryDraft> entries)
        {
            property.arraySize = entries?.Count ?? 0;
            for (var index = 0; index < property.arraySize; index++)
            {
                var source = entries[index] ?? new LoreEntryDraft();
                var target = property.GetArrayElementAtIndex(index);
                target.FindPropertyRelative("factId").stringValue = source.FactId;
                target.FindPropertyRelative("statement").stringValue = source.Statement;
                target.FindPropertyRelative("priority").intValue = source.Priority;
            }
        }

        /// <summary>
        /// Ensures an existing profile asset is writable consumer data under Assets.
        /// </summary>
        private static bool TryValidateWritableAsset(
            Object asset,
            string displayName,
            out string error)
        {
            error = string.Empty;
            if (asset == null || !EditorUtility.IsPersistent(asset))
            {
                error = displayName + " must be a persistent Unity asset.";
                return false;
            }

            var assetPath = AssetDatabase.GetAssetPath(asset).Replace('\\', '/');
            if (!assetPath.StartsWith("Assets/", StringComparison.Ordinal))
            {
                error = displayName + " can be edited only when it is stored under Assets/.";
                return false;
            }

            return true;
        }

        /// <summary>
        /// Normalizes and restricts a requested creation folder to writable Assets data.
        /// </summary>
        private static bool TryNormalizeWritableFolder(
            string folderPath,
            out string normalized,
            out string error)
        {
            normalized = (folderPath ?? string.Empty)
                .Replace('\\', '/')
                .TrimEnd('/');
            error = string.Empty;
            if (!string.Equals(normalized, "Assets", StringComparison.Ordinal)
                && !normalized.StartsWith("Assets/", StringComparison.Ordinal))
            {
                error = "Character Builder asset folders must be under Assets/.";
                return false;
            }

            if (normalized.Contains("/../")
                || normalized.EndsWith("/..", StringComparison.Ordinal))
            {
                error = "Character Builder asset folders cannot traverse parent paths.";
                return false;
            }

            return true;
        }

        /// <summary>
        /// Creates every missing segment of one prevalidated Assets-relative folder.
        /// </summary>
        private static void EnsureFolder(string folderPath)
        {
            var segments = folderPath.Split(
                new[] { '/' },
                StringSplitOptions.RemoveEmptyEntries);
            var current = segments[0];
            for (var index = 1; index < segments.Length; index++)
            {
                var next = current + "/" + segments[index];
                if (!AssetDatabase.IsValidFolder(next))
                {
                    AssetDatabase.CreateFolder(current, segments[index]);
                }

                current = next;
            }
        }

        /// <summary>
        /// Converts user-facing names into one non-empty Unity asset filename segment.
        /// </summary>
        private static string GetSafeAssetName(
            string preferredName,
            string secondaryName,
            string fallbackName)
        {
            var source = !string.IsNullOrWhiteSpace(preferredName)
                ? preferredName.Trim()
                : !string.IsNullOrWhiteSpace(secondaryName)
                    ? secondaryName.Trim()
                    : fallbackName;
            var invalidCharacters = new HashSet<char>(Path.GetInvalidFileNameChars());
            var characters = source.ToCharArray();
            for (var index = 0; index < characters.Length; index++)
            {
                if (invalidCharacters.Contains(characters[index])
                    || characters[index] == '/'
                    || characters[index] == '\\')
                {
                    characters[index] = '_';
                }
            }

            var safeName = new string(characters).Trim().Trim('.');
            if (string.IsNullOrWhiteSpace(safeName))
            {
                return fallbackName;
            }

            return IsReservedWindowsFileName(safeName) ? safeName + "_" : safeName;
        }

        /// <summary>
        /// Detects Windows device names that cannot be used as portable asset filenames.
        /// </summary>
        private static bool IsReservedWindowsFileName(string value)
        {
            var baseName = value.Split('.')[0];
            if (string.Equals(baseName, "CON", StringComparison.OrdinalIgnoreCase)
                || string.Equals(baseName, "PRN", StringComparison.OrdinalIgnoreCase)
                || string.Equals(baseName, "AUX", StringComparison.OrdinalIgnoreCase)
                || string.Equals(baseName, "NUL", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            if (baseName.Length != 4)
            {
                return false;
            }

            var prefix = baseName.Substring(0, 3);
            var suffix = baseName[3];
            return suffix >= '1'
                && suffix <= '9'
                && (string.Equals(prefix, "COM", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(prefix, "LPT", StringComparison.OrdinalIgnoreCase));
        }
    }
}
