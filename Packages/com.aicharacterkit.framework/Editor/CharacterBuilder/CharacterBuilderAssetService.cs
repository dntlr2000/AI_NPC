using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using AiCharacterKit.Core;
using AiCharacterKit.Unity;
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
