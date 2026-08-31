using System;
using System.Collections.Generic;
using AiCharacterKit.Core;
using AiCharacterKit.Unity;
using AiCharacterKit.Unity.Speech;
using AiCharacterKit.Unity.Transcription;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace AiCharacterKit.Editor
{
    /// <summary>
    /// Creates the mock profile and Play Mode sample scenes through supported Unity Editor APIs.
    /// </summary>
    public static class PrototypeSceneBuilder
    {
        private const string RootFolder = "Assets/AiCharacterKit";
        private const string SamplesFolder = RootFolder + "/Samples";
        private const string MockNpcFolder = SamplesFolder + "/MockNpc";
        private const string ProfilesFolder = MockNpcFolder + "/Profiles";
        private const string ScenesFolder = MockNpcFolder + "/Scenes";
        private const string BackendNpcFolder = SamplesFolder + "/BackendNpc";
        private const string BackendScenesFolder = BackendNpcFolder + "/Scenes";
        private const string MemoryNpcFolder = SamplesFolder + "/MemoryNpc";
        private const string MemoryScenesFolder = MemoryNpcFolder + "/Scenes";
        private const string SpeechNpcFolder = SamplesFolder + "/SpeechNpc";
        private const string SpeechProfilesFolder = SpeechNpcFolder + "/Profiles";
        private const string SpeechScenesFolder = SpeechNpcFolder + "/Scenes";
        private const string VoiceInputNpcFolder = SamplesFolder + "/VoiceInputNpc";
        private const string VoiceInputScenesFolder =
            VoiceInputNpcFolder + "/Scenes";
        private const string ProfilePath = ProfilesFolder + "/PrototypeCharacter.asset";
        private const string ScenePath = ScenesFolder + "/MockNpcPrototype.unity";
        private const string LunaProfilePath = ProfilesFolder + "/Luna.asset";
        private const string GuardProfilePath = ProfilesFolder + "/Guard.asset";
        private const string MultiCharacterScenePath =
            ScenesFolder + "/MultiCharacterMock.unity";
        private const string BackendScenePath =
            BackendScenesFolder + "/BackendNpcPrototype.unity";
        private const string MemoryScenePath =
            MemoryScenesFolder + "/MemoryNpcPrototype.unity";
        private const string SpeechScenePath =
            SpeechScenesFolder + "/SpeechNpcPrototype.unity";
        private const string VoiceInputScenePath =
            VoiceInputScenesFolder + "/VoiceInputNpcPrototype.unity";
        private const string WarmVoiceProfilePath =
            SpeechProfilesFolder + "/WarmFriendlyVoice.asset";
        private const string CalmVoiceProfilePath =
            SpeechProfilesFolder + "/CalmFormalVoice.asset";
        private const string DefaultBackendEndpoint =
            "http://127.0.0.1:8787/v1/npc/respond";
        private const string DefaultSessionBackendEndpoint =
            "http://127.0.0.1:8787/v2/npc/respond";
        private const string DefaultSessionResetEndpoint =
            "http://127.0.0.1:8787/v2/npc/sessions/reset";
        private const string DefaultSpeechEndpoint =
            "http://127.0.0.1:8787/v1/speech/synthesize";
        private const string DefaultTranscriptionEndpoint =
            "http://127.0.0.1:8787/v1/speech/transcribe";
        private const int DefaultBackendTimeoutSeconds = 35;
        private const string InputActionsPath = "Assets/InputSystem_Actions.inputactions";
        private const float SinglePanelWidth = 560f;
        private const float MultiPanelWidth = 440f;
        private const float VoicePanelWidth = 600f;

        /// <summary>
        /// Creates the prototype from the Unity menu after protecting unsaved user scenes.
        /// </summary>
        [MenuItem("Tools/AI Character Kit/Create Mock NPC Prototype")]
        public static void CreatePrototypeScene()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                return;
            }

            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) != null)
            {
                RepairPrototypeScene();
                EditorUtility.DisplayDialog(
                    "Mock NPC Prototype",
                    $"The prototype already exists at:\n{ScenePath}\n\nIts required references were refreshed.",
                    "OK");
                Selection.activeObject = AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath);
                return;
            }

            CreatePrototypeSceneInternal();
        }

        /// <summary>
        /// Creates the prototype non-interactively for batch verification without overwriting it.
        /// </summary>
        public static void CreatePrototypeSceneBatch()
        {
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) != null)
            {
                RepairPrototypeScene();
                Debug.Log($"Refreshed Mock NPC prototype references at {ScenePath}.");
                return;
            }

            CreatePrototypeSceneInternal();
        }

        /// <summary>
        /// Creates or repairs the Phase 2 two-character sample after protecting unsaved scenes.
        /// </summary>
        [MenuItem("Tools/AI Character Kit/Create Multi-Character Mock Prototype")]
        public static void CreateMultiCharacterScene()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                return;
            }

            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(MultiCharacterScenePath) != null)
            {
                RepairMultiCharacterScene();
                EditorUtility.DisplayDialog(
                    "Multi-Character Mock Prototype",
                    "The prototype already exists at:\n"
                    + MultiCharacterScenePath
                    + "\n\nIts required references were refreshed.",
                    "OK");
                Selection.activeObject = AssetDatabase.LoadAssetAtPath<SceneAsset>(
                    MultiCharacterScenePath);
                return;
            }

            CreateMultiCharacterSceneInternal();
        }

        /// <summary>
        /// Creates or repairs the Phase 2 sample non-interactively for batch automation.
        /// </summary>
        public static void CreateMultiCharacterSceneBatch()
        {
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(MultiCharacterScenePath) != null)
            {
                RepairMultiCharacterScene();
                Debug.Log(
                    $"Refreshed multi-character mock prototype references at {MultiCharacterScenePath}.");
                return;
            }

            CreateMultiCharacterSceneInternal();
        }

        /// <summary>
        /// Creates or repairs the Phase 4 backend sample after protecting unsaved scenes.
        /// </summary>
        [MenuItem("Tools/AI Character Kit/Create Backend NPC Prototype")]
        public static void CreateBackendScene()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                return;
            }

            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(BackendScenePath) != null)
            {
                RepairBackendScene();
                EditorUtility.DisplayDialog(
                    "Backend NPC Prototype",
                    "The prototype already exists at:\n"
                    + BackendScenePath
                    + "\n\nIts required references were refreshed.",
                    "OK");
                Selection.activeObject = AssetDatabase.LoadAssetAtPath<SceneAsset>(
                    BackendScenePath);
                return;
            }

            CreateBackendSceneInternal();
        }

        /// <summary>
        /// Creates or repairs the Phase 4 backend sample non-interactively for automation.
        /// </summary>
        public static void CreateBackendSceneBatch()
        {
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(BackendScenePath) != null)
            {
                RepairBackendScene();
                Debug.Log(
                    $"Refreshed backend NPC prototype references at {BackendScenePath}.");
                return;
            }

            CreateBackendSceneInternal();
        }

        /// <summary>
        /// Creates or repairs the Phase 5 two-session memory sample interactively.
        /// </summary>
        [MenuItem("Tools/AI Character Kit/Create Memory NPC Prototype")]
        public static void CreateMemoryScene()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                return;
            }

            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(MemoryScenePath) != null)
            {
                RepairMemoryScene();
                EditorUtility.DisplayDialog(
                    "Memory NPC Prototype",
                    "The prototype already exists at:\n"
                    + MemoryScenePath
                    + "\n\nIts required references were refreshed.",
                    "OK");
                Selection.activeObject = AssetDatabase.LoadAssetAtPath<SceneAsset>(
                    MemoryScenePath);
                return;
            }

            CreateMemorySceneInternal();
        }

        /// <summary>
        /// Creates or repairs the Phase 5 memory sample non-interactively for automation.
        /// </summary>
        public static void CreateMemorySceneBatch()
        {
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(MemoryScenePath) != null)
            {
                RepairMemoryScene();
                Debug.Log(
                    $"Refreshed memory NPC prototype references at {MemoryScenePath}.");
                return;
            }

            CreateMemorySceneInternal();
        }

        /// <summary>
        /// Creates or repairs the Phase 6 reusable speech sample interactively.
        /// </summary>
        [MenuItem("Tools/AI Character Kit/Create Speech NPC Prototype")]
        public static void CreateSpeechScene()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                return;
            }

            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(SpeechScenePath) != null)
            {
                RepairSpeechScene();
                EditorUtility.DisplayDialog(
                    "Speech NPC Prototype",
                    "The prototype already exists at:\n"
                    + SpeechScenePath
                    + "\n\nIts required references were refreshed.",
                    "OK");
                Selection.activeObject = AssetDatabase.LoadAssetAtPath<SceneAsset>(
                    SpeechScenePath);
                return;
            }

            CreateSpeechSceneInternal();
        }

        /// <summary>
        /// Creates or repairs the Phase 6 speech sample non-interactively for automation.
        /// </summary>
        public static void CreateSpeechSceneBatch()
        {
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(SpeechScenePath) != null)
            {
                RepairSpeechScene();
                Debug.Log(
                    $"Refreshed speech NPC prototype references at {SpeechScenePath}.");
                return;
            }

            CreateSpeechSceneInternal();
        }

        /// <summary>
        /// Creates or repairs the Phase 7 push-to-talk sample interactively.
        /// </summary>
        [MenuItem("Tools/AI Character Kit/Create Voice Input NPC Prototype")]
        public static void CreateVoiceInputScene()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                return;
            }

            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(VoiceInputScenePath) != null)
            {
                RepairVoiceInputScene();
                EditorUtility.DisplayDialog(
                    "Voice Input NPC Prototype",
                    "The prototype already exists at:\n"
                    + VoiceInputScenePath
                    + "\n\nIts required references were refreshed.",
                    "OK");
                Selection.activeObject = AssetDatabase.LoadAssetAtPath<SceneAsset>(
                    VoiceInputScenePath);
                return;
            }

            CreateVoiceInputSceneInternal();
        }

        /// <summary>
        /// Creates or repairs the Phase 7 sample non-interactively for automation.
        /// </summary>
        public static void CreateVoiceInputSceneBatch()
        {
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(VoiceInputScenePath) != null)
            {
                RepairVoiceInputScene();
                Debug.Log(
                    $"Refreshed voice input NPC prototype references at {VoiceInputScenePath}.");
                return;
            }

            CreateVoiceInputSceneInternal();
        }

        /// <summary>
        /// Creates folders, profile data, scene objects, UI, and serialized component wiring.
        /// </summary>
        private static void CreatePrototypeSceneInternal()
        {
            EnsureSampleFolders();
            var profile = CreateOrLoadProfile(CreateMinaProfileDefinition());
            var scene = EditorSceneManager.NewScene(
                NewSceneSetup.DefaultGameObjects,
                NewSceneMode.Single);

            ConfigureDefaultSceneObjects();
            CreateGround();
            CreateConfiguredNpc(
                profile,
                Vector3.zero,
                string.Empty,
                false,
                SinglePanelWidth,
                "Mock NPC",
                NpcConversationMode.Mock,
                DefaultBackendEndpoint,
                DefaultBackendTimeoutSeconds);
            CreateInputSystemEventSystem();

            SaveGeneratedScene(
                scene,
                ScenePath,
                "Created Mock NPC prototype");
        }

        /// <summary>
        /// Creates both Phase 2 profiles and wires two independent NPCs into a new scene.
        /// </summary>
        private static void CreateMultiCharacterSceneInternal()
        {
            EnsureSampleFolders();
            var lunaProfile = CreateOrLoadProfile(CreateLunaProfileDefinition());
            var guardProfile = CreateOrLoadProfile(CreateGuardProfileDefinition());
            EnsureDistinctProfileIds(lunaProfile, guardProfile);

            var scene = EditorSceneManager.NewScene(
                NewSceneSetup.DefaultGameObjects,
                NewSceneMode.Single);

            ConfigureDefaultSceneObjects();
            CreateGround();
            CreateConfiguredNpc(
                lunaProfile,
                new Vector3(-1.7f, 0f, 0f),
                "Luna",
                true,
                MultiPanelWidth,
                "Mock NPC",
                NpcConversationMode.Mock,
                DefaultBackendEndpoint,
                DefaultBackendTimeoutSeconds);
            CreateConfiguredNpc(
                guardProfile,
                new Vector3(1.7f, 0f, 0f),
                "Guard",
                false,
                MultiPanelWidth,
                "Mock NPC",
                NpcConversationMode.Mock,
                DefaultBackendEndpoint,
                DefaultBackendTimeoutSeconds);
            CreateInputSystemEventSystem();

            SaveGeneratedScene(
                scene,
                MultiCharacterScenePath,
                "Created multi-character mock prototype");
        }

        /// <summary>
        /// Reuses Luna's profile and creates one backend-only vertical-slice scene.
        /// </summary>
        private static void CreateBackendSceneInternal()
        {
            EnsureSampleFolders();
            var profile = CreateOrLoadProfile(CreateLunaProfileDefinition());
            var scene = EditorSceneManager.NewScene(
                NewSceneSetup.DefaultGameObjects,
                NewSceneMode.Single);

            ConfigureDefaultSceneObjects();
            CreateGround();
            CreateConfiguredNpc(
                profile,
                Vector3.zero,
                string.Empty,
                false,
                SinglePanelWidth,
                "Backend NPC",
                NpcConversationMode.Backend,
                DefaultBackendEndpoint,
                DefaultBackendTimeoutSeconds);
            CreateInputSystemEventSystem();

            SaveGeneratedScene(
                scene,
                BackendScenePath,
                "Created backend NPC prototype");
        }

        /// <summary>
        /// Creates two independently resettable backend sessions in one generated scene.
        /// </summary>
        private static void CreateMemorySceneInternal()
        {
            EnsureSampleFolders();
            var scene = EditorSceneManager.NewScene(
                NewSceneSetup.DefaultGameObjects,
                NewSceneMode.Single);
            var lunaProfile = CreateOrLoadProfile(CreateLunaProfileDefinition());
            var guardProfile = CreateOrLoadProfile(CreateGuardProfileDefinition());
            EnsureDistinctProfileIds(lunaProfile, guardProfile);

            ConfigureDefaultSceneObjects();
            CreateGround();
            CreateConfiguredNpc(
                lunaProfile,
                new Vector3(-1.7f, 0f, 0f),
                "Luna",
                true,
                MultiPanelWidth,
                "Memory NPC",
                NpcConversationMode.BackendSession,
                DefaultBackendEndpoint,
                DefaultBackendTimeoutSeconds,
                true);
            CreateConfiguredNpc(
                guardProfile,
                new Vector3(1.7f, 0f, 0f),
                "Guard",
                false,
                MultiPanelWidth,
                "Memory NPC",
                NpcConversationMode.BackendSession,
                DefaultBackendEndpoint,
                DefaultBackendTimeoutSeconds,
                true);
            CreateInputSystemEventSystem();

            SaveGeneratedScene(
                scene,
                MemoryScenePath,
                "Created memory NPC prototype");
        }

        /// <summary>
        /// Creates two V2 NPCs with independent sessions and data-selected speech presets.
        /// </summary>
        private static void CreateSpeechSceneInternal()
        {
            EnsureSampleFolders();
            CreateOrLoadProfile(CreateLunaProfileDefinition());
            CreateOrLoadProfile(CreateGuardProfileDefinition());
            CreateOrLoadVoiceProfile(
                WarmVoiceProfilePath,
                "WarmFriendlyVoice",
                "warm-friendly");
            CreateOrLoadVoiceProfile(
                CalmVoiceProfilePath,
                "CalmFormalVoice",
                "calm-formal");
            var scene = EditorSceneManager.NewScene(
                NewSceneSetup.DefaultGameObjects,
                NewSceneMode.Single);
            var lunaProfile = LoadRequiredCharacterProfile(LunaProfilePath);
            var guardProfile = LoadRequiredCharacterProfile(GuardProfilePath);
            var lunaVoice = LoadRequiredVoiceProfile(WarmVoiceProfilePath);
            var guardVoice = LoadRequiredVoiceProfile(CalmVoiceProfilePath);
            EnsureDistinctProfileIds(lunaProfile, guardProfile);
            ConfigureDefaultSceneObjects();
            CreateGround();
            CreateConfiguredSpeechNpc(
                lunaProfile,
                lunaVoice,
                new Vector3(-1.7f, 0f, 0f),
                "Luna",
                true);
            CreateConfiguredSpeechNpc(
                guardProfile,
                guardVoice,
                new Vector3(1.7f, 0f, 0f),
                "Guard",
                false);
            CreateInputSystemEventSystem();

            SaveGeneratedScene(
                scene,
                SpeechScenePath,
                "Created speech NPC prototype");
        }

        /// <summary>
        /// Creates one V2 NPC with optional TTS and reviewed push-to-talk text input.
        /// </summary>
        private static void CreateVoiceInputSceneInternal()
        {
            EnsureSampleFolders();
            CreateOrLoadProfile(CreateLunaProfileDefinition());
            CreateOrLoadVoiceProfile(
                WarmVoiceProfilePath,
                "WarmFriendlyVoice",
                "warm-friendly");
            var scene = EditorSceneManager.NewScene(
                NewSceneSetup.DefaultGameObjects,
                NewSceneMode.Single);
            var characterProfile = LoadRequiredCharacterProfile(LunaProfilePath);
            var voiceProfile = LoadRequiredVoiceProfile(WarmVoiceProfilePath);

            ConfigureDefaultSceneObjects();
            CreateGround();
            CreateConfiguredVoiceInputNpc(characterProfile, voiceProfile);
            CreateInputSystemEventSystem();

            SaveGeneratedScene(
                scene,
                VoiceInputScenePath,
                "Created voice input NPC prototype");
        }

        /// <summary>
        /// Creates one profile once and validates existing assets without overwriting them.
        /// </summary>
        private static CharacterProfile CreateOrLoadProfile(
            SampleProfileDefinition definition)
        {
            var existingProfile = AssetDatabase.LoadAssetAtPath<CharacterProfile>(
                definition.AssetPath);
            if (existingProfile != null)
            {
                ValidateProfile(existingProfile, definition.AssetPath);
                return existingProfile;
            }

            var profile = ScriptableObject.CreateInstance<CharacterProfile>();
            profile.name = definition.AssetName;

            var serializedProfile = new SerializedObject(profile);
            serializedProfile.FindProperty("characterId").stringValue =
                definition.CharacterId;
            serializedProfile.FindProperty("displayName").stringValue =
                definition.DisplayName;
            serializedProfile.FindProperty("personality").stringValue =
                definition.Personality;
            serializedProfile.FindProperty("speechStyle").stringValue =
                definition.SpeechStyle;
            serializedProfile.FindProperty("exampleDialogue").stringValue =
                definition.ExampleDialogue;
            serializedProfile.FindProperty("defaultEmotion").enumValueIndex =
                (int)definition.DefaultEmotion;
            serializedProfile.ApplyModifiedPropertiesWithoutUndo();

            AssetDatabase.CreateAsset(profile, definition.AssetPath);
            AssetDatabase.SaveAssets();
            AssetDatabase.ImportAsset(
                definition.AssetPath,
                ImportAssetOptions.ForceSynchronousImport);

            var savedProfile = AssetDatabase.LoadAssetAtPath<CharacterProfile>(
                definition.AssetPath);
            if (savedProfile == null)
            {
                throw new InvalidOperationException(
                    $"Failed to reload the created profile at {definition.AssetPath}.");
            }

            ValidateProfile(savedProfile, definition.AssetPath);
            return savedProfile;
        }

        /// <summary>
        /// Creates one reusable voice preset asset once and validates existing data.
        /// </summary>
        private static NpcVoiceProfile CreateOrLoadVoiceProfile(
            string assetPath,
            string assetName,
            string voicePresetId)
        {
            var existingProfile = AssetDatabase.LoadAssetAtPath<NpcVoiceProfile>(
                assetPath);
            if (existingProfile != null)
            {
                ValidateVoiceProfile(existingProfile, assetPath);
                return existingProfile;
            }

            var profile = ScriptableObject.CreateInstance<NpcVoiceProfile>();
            profile.name = assetName;
            var serializedProfile = new SerializedObject(profile);
            serializedProfile.FindProperty("voicePresetId").stringValue =
                voicePresetId;
            serializedProfile.ApplyModifiedPropertiesWithoutUndo();

            AssetDatabase.CreateAsset(profile, assetPath);
            AssetDatabase.SaveAssets();
            AssetDatabase.ImportAsset(
                assetPath,
                ImportAssetOptions.ForceSynchronousImport);

            var savedProfile = AssetDatabase.LoadAssetAtPath<NpcVoiceProfile>(
                assetPath);
            if (savedProfile == null)
            {
                throw new InvalidOperationException(
                    $"Failed to reload the created voice profile at {assetPath}.");
            }

            ValidateVoiceProfile(savedProfile, assetPath);
            return savedProfile;
        }

        /// <summary>
        /// Reloads a voice asset after all imports so its Unity object handle remains current.
        /// </summary>
        private static NpcVoiceProfile LoadRequiredVoiceProfile(string assetPath)
        {
            var profile = AssetDatabase.LoadAssetAtPath<NpcVoiceProfile>(assetPath);
            if (profile == null)
            {
                throw new InvalidOperationException(
                    $"Voice profile was not found at {assetPath}.");
            }

            ValidateVoiceProfile(profile, assetPath);
            return profile;
        }

        /// <summary>
        /// Reloads a character asset after all imports so its Unity object handle remains current.
        /// </summary>
        private static CharacterProfile LoadRequiredCharacterProfile(
            string assetPath)
        {
            var profile = AssetDatabase.LoadAssetAtPath<CharacterProfile>(assetPath);
            if (profile == null)
            {
                throw new InvalidOperationException(
                    $"Character profile was not found at {assetPath}.");
            }

            ValidateProfile(profile, assetPath);
            return profile;
        }

        /// <summary>
        /// Describes the existing Phase 1 Mina profile without changing its asset path or values.
        /// </summary>
        private static SampleProfileDefinition CreateMinaProfileDefinition()
        {
            return new SampleProfileDefinition(
                ProfilePath,
                "Prototype Character",
                "prototype-mina",
                "Mina",
                "Friendly, observant, and eager to help.",
                "Uses short, warm, and polite sentences.",
                "오늘은 무엇을 도와드릴까요?",
                NpcEmotion.Neutral);
        }

        /// <summary>
        /// Describes the playful Phase 2 Luna sample profile.
        /// </summary>
        private static SampleProfileDefinition CreateLunaProfileDefinition()
        {
            return new SampleProfileDefinition(
                LunaProfilePath,
                "Luna",
                "sample-luna",
                "Luna",
                "Playful, curious, and friendly.",
                "Warm, casual, short sentences.",
                "새로운 모험 이야기를 들려줄래?",
                NpcEmotion.Happy);
        }

        /// <summary>
        /// Describes the disciplined Phase 2 Guard sample profile.
        /// </summary>
        private static SampleProfileDefinition CreateGuardProfileDefinition()
        {
            return new SampleProfileDefinition(
                GuardProfilePath,
                "Guard",
                "sample-guard",
                "Guard",
                "Disciplined, vigilant, and duty-bound.",
                "Formal, concise, respectful sentences.",
                "성문 주변에서는 질서를 지켜 주십시오.",
                NpcEmotion.Concerned);
        }

        /// <summary>
        /// Fails scene generation early when a profile asset is incomplete or unsupported.
        /// </summary>
        private static void ValidateProfile(CharacterProfile profile, string assetPath)
        {
            if (!profile.TryValidate(out var validationError))
            {
                throw new InvalidOperationException(
                    $"Character profile at {assetPath} is invalid: {validationError}");
            }
        }

        /// <summary>
        /// Rejects invalid reusable voice assets without silently replacing user data.
        /// </summary>
        private static void ValidateVoiceProfile(
            NpcVoiceProfile profile,
            string assetPath)
        {
            if (profile == null)
            {
                throw new InvalidOperationException(
                    $"Voice profile '{assetPath}' is missing.");
            }

            if (!profile.TryValidate(out var error))
            {
                throw new InvalidOperationException(
                    $"Voice profile '{assetPath}' is invalid: {error}");
            }
        }

        /// <summary>
        /// Ensures the Phase 2 samples cannot accidentally share a character identity.
        /// </summary>
        private static void EnsureDistinctProfileIds(
            CharacterProfile first,
            CharacterProfile second)
        {
            if (string.Equals(
                first.CharacterId.Trim(),
                second.CharacterId.Trim(),
                StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    "The multi-character sample profiles must use distinct character IDs.");
            }
        }

        /// <summary>
        /// Reloads an existing generated scene and restores only its required component references.
        /// </summary>
        private static void RepairPrototypeScene()
        {
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            var profile = AssetDatabase.LoadAssetAtPath<CharacterProfile>(ProfilePath);
            if (profile == null)
            {
                throw new InvalidOperationException(
                    $"Prototype profile was not found at {ProfilePath}.");
            }

            ValidateProfile(profile, ProfilePath);
            RepairNpcConfiguration(
                profile,
                "Mock NPC - Mina",
                string.Empty,
                NpcConversationMode.Mock,
                DefaultBackendEndpoint,
                DefaultBackendTimeoutSeconds);
            SaveGeneratedScene(
                scene,
                ScenePath,
                "Repaired Mock NPC prototype");
        }

        /// <summary>
        /// Reloads the Phase 2 scene and restores both NPCs' independent serialized references.
        /// </summary>
        private static void RepairMultiCharacterScene()
        {
            var scene = EditorSceneManager.OpenScene(
                MultiCharacterScenePath,
                OpenSceneMode.Single);
            var lunaProfile = AssetDatabase.LoadAssetAtPath<CharacterProfile>(
                LunaProfilePath);
            var guardProfile = AssetDatabase.LoadAssetAtPath<CharacterProfile>(
                GuardProfilePath);

            if (lunaProfile == null || guardProfile == null)
            {
                throw new InvalidOperationException(
                    "The multi-character sample profiles are missing.");
            }

            ValidateProfile(lunaProfile, LunaProfilePath);
            ValidateProfile(guardProfile, GuardProfilePath);
            EnsureDistinctProfileIds(lunaProfile, guardProfile);
            RepairNpcConfiguration(
                lunaProfile,
                "Mock NPC - Luna",
                "Luna",
                NpcConversationMode.Mock,
                DefaultBackendEndpoint,
                DefaultBackendTimeoutSeconds);
            RepairNpcConfiguration(
                guardProfile,
                "Mock NPC - Guard",
                "Guard",
                NpcConversationMode.Mock,
                DefaultBackendEndpoint,
                DefaultBackendTimeoutSeconds);
            SaveGeneratedScene(
                scene,
                MultiCharacterScenePath,
                "Repaired multi-character mock prototype");
        }

        /// <summary>
        /// Reloads the Phase 4 scene and restores its backend-mode serialized references.
        /// </summary>
        private static void RepairBackendScene()
        {
            var scene = EditorSceneManager.OpenScene(
                BackendScenePath,
                OpenSceneMode.Single);
            var profile = AssetDatabase.LoadAssetAtPath<CharacterProfile>(LunaProfilePath);
            if (profile == null)
            {
                throw new InvalidOperationException(
                    $"Backend sample profile was not found at {LunaProfilePath}.");
            }

            ValidateProfile(profile, LunaProfilePath);
            RepairNpcConfiguration(
                profile,
                "Backend NPC - Luna",
                string.Empty,
                NpcConversationMode.Backend,
                DefaultBackendEndpoint,
                DefaultBackendTimeoutSeconds);
            SaveGeneratedScene(
                scene,
                BackendScenePath,
                "Repaired backend NPC prototype");
        }

        /// <summary>
        /// Reloads the Phase 5 scene and restores both independent session UI sets.
        /// </summary>
        private static void RepairMemoryScene()
        {
            var scene = EditorSceneManager.OpenScene(
                MemoryScenePath,
                OpenSceneMode.Single);
            var lunaProfile = AssetDatabase.LoadAssetAtPath<CharacterProfile>(
                LunaProfilePath);
            var guardProfile = AssetDatabase.LoadAssetAtPath<CharacterProfile>(
                GuardProfilePath);
            if (lunaProfile == null || guardProfile == null)
            {
                throw new InvalidOperationException(
                    "The memory sample profiles are missing.");
            }

            ValidateProfile(lunaProfile, LunaProfilePath);
            ValidateProfile(guardProfile, GuardProfilePath);
            EnsureDistinctProfileIds(lunaProfile, guardProfile);
            RepairNpcConfiguration(
                lunaProfile,
                "Memory NPC - Luna",
                "Luna",
                NpcConversationMode.BackendSession,
                DefaultBackendEndpoint,
                DefaultBackendTimeoutSeconds,
                true);
            RepairNpcConfiguration(
                guardProfile,
                "Memory NPC - Guard",
                "Guard",
                NpcConversationMode.BackendSession,
                DefaultBackendEndpoint,
                DefaultBackendTimeoutSeconds,
                true);
            SaveGeneratedScene(
                scene,
                MemoryScenePath,
                "Repaired memory NPC prototype");
        }

        /// <summary>
        /// Reloads the Phase 6 scene and restores conversation, session, and speech wiring.
        /// </summary>
        private static void RepairSpeechScene()
        {
            var scene = EditorSceneManager.OpenScene(
                SpeechScenePath,
                OpenSceneMode.Single);
            var lunaProfile = AssetDatabase.LoadAssetAtPath<CharacterProfile>(
                LunaProfilePath);
            var guardProfile = AssetDatabase.LoadAssetAtPath<CharacterProfile>(
                GuardProfilePath);
            var lunaVoice = AssetDatabase.LoadAssetAtPath<NpcVoiceProfile>(
                WarmVoiceProfilePath);
            var guardVoice = AssetDatabase.LoadAssetAtPath<NpcVoiceProfile>(
                CalmVoiceProfilePath);
            if (lunaProfile == null || guardProfile == null)
            {
                throw new InvalidOperationException(
                    "The speech sample character profiles are missing.");
            }

            ValidateProfile(lunaProfile, LunaProfilePath);
            ValidateProfile(guardProfile, GuardProfilePath);
            ValidateVoiceProfile(lunaVoice, WarmVoiceProfilePath);
            ValidateVoiceProfile(guardVoice, CalmVoiceProfilePath);
            EnsureDistinctProfileIds(lunaProfile, guardProfile);
            RepairSpeechNpcConfiguration(lunaProfile, lunaVoice, "Luna");
            RepairSpeechNpcConfiguration(guardProfile, guardVoice, "Guard");
            SaveGeneratedScene(
                scene,
                SpeechScenePath,
                "Repaired speech NPC prototype");
        }

        /// <summary>
        /// Reloads the Phase 7 scene and restores conversation, speech, and voice input wiring.
        /// </summary>
        private static void RepairVoiceInputScene()
        {
            var scene = EditorSceneManager.OpenScene(
                VoiceInputScenePath,
                OpenSceneMode.Single);
            var characterProfile = AssetDatabase.LoadAssetAtPath<CharacterProfile>(
                LunaProfilePath);
            var voiceProfile = AssetDatabase.LoadAssetAtPath<NpcVoiceProfile>(
                WarmVoiceProfilePath);
            if (characterProfile == null)
            {
                throw new InvalidOperationException(
                    "The voice input sample character profile is missing.");
            }

            ValidateProfile(characterProfile, LunaProfilePath);
            ValidateVoiceProfile(voiceProfile, WarmVoiceProfilePath);
            RepairVoiceInputNpcConfiguration(characterProfile, voiceProfile);
            SaveGeneratedScene(
                scene,
                VoiceInputScenePath,
                "Repaired voice input NPC prototype");
        }

        /// <summary>
        /// Positions the default camera and directional light for the prototype NPC.
        /// </summary>
        private static void ConfigureDefaultSceneObjects()
        {
            var cameraObject = GameObject.Find("Main Camera");
            if (cameraObject != null)
            {
                cameraObject.transform.position = new Vector3(0f, 1.4f, -7f);
                cameraObject.transform.LookAt(new Vector3(0f, 0.5f, 0f));

                var camera = cameraObject.GetComponent<Camera>();
                if (camera != null)
                {
                    camera.backgroundColor = new Color(0.08f, 0.1f, 0.14f);
                }
            }

            var lightObject = GameObject.Find("Directional Light");
            if (lightObject != null)
            {
                lightObject.transform.rotation = Quaternion.Euler(45f, -35f, 0f);
            }
        }

        /// <summary>
        /// Creates a simple floor so the prototype NPC has a clear visual reference.
        /// </summary>
        private static void CreateGround()
        {
            var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.name = "Prototype Ground";
            ground.transform.position = new Vector3(0f, -1f, 0f);
            ground.transform.localScale = new Vector3(0.6f, 1f, 0.6f);
        }

        /// <summary>
        /// Creates one Capsule NPC and adds its runtime bridge and presentation adapter.
        /// </summary>
        private static GameObject CreateNpc(
            string displayName,
            Vector3 position,
            string compositionLabel)
        {
            var npc = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            npc.name = $"{compositionLabel} - {displayName}";
            npc.transform.position = position;
            npc.AddComponent<NpcTextPresentationDriver>();
            npc.AddComponent<NpcConversationBehaviour>();
            return npc;
        }

        /// <summary>
        /// Creates and wires one NPC, its profile, its presentation driver, and its UI panel.
        /// </summary>
        private static void CreateConfiguredNpc(
            CharacterProfile profile,
            Vector3 position,
            string objectSuffix,
            bool alignPanelLeft,
            float panelWidth,
            string compositionLabel,
            NpcConversationMode conversationMode,
            string backendEndpoint,
            int backendTimeoutSeconds,
            bool includeSessionControls = false)
        {
            var npc = CreateNpc(profile.DisplayName, position, compositionLabel);
            var presentationDriver = npc.GetComponent<NpcTextPresentationDriver>();
            var conversationBehaviour = npc.GetComponent<NpcConversationBehaviour>();
            var ui = CreateUserInterface(
                profile.DisplayName,
                objectSuffix,
                alignPanelLeft,
                panelWidth,
                compositionLabel,
                includeSessionControls);

            ConfigurePresentationDriver(
                presentationDriver,
                ui,
                npc.GetComponent<Renderer>(),
                npc.transform);
            ConfigureConversationBehaviour(
                conversationBehaviour,
                profile,
                presentationDriver,
                conversationMode,
                backendEndpoint,
                backendTimeoutSeconds);
            ConfigureInputView(
                ui.InputView,
                ui.InputField,
                ui.SendButton,
                conversationBehaviour);
            if (includeSessionControls)
            {
                ConfigureSessionControlView(
                    ui.SessionControlView,
                    ui.ResetButton,
                    ui.MemoryStatusText,
                    conversationBehaviour);
            }
        }

        /// <summary>
        /// Creates one V2 NPC with visual fallback, optional speech, and independent controls.
        /// </summary>
        private static void CreateConfiguredSpeechNpc(
            CharacterProfile characterProfile,
            NpcVoiceProfile voiceProfile,
            Vector3 position,
            string objectSuffix,
            bool alignPanelLeft)
        {
            var npc = CreateNpc(
                characterProfile.DisplayName,
                position,
                "Speech NPC");
            var playback = npc.AddComponent<UnityPcmSpeechPlaybackDriver>();
            var audioSource = npc.GetComponent<AudioSource>();
            audioSource.playOnAwake = false;
            audioSource.loop = false;
            audioSource.spatialBlend = 0f;
            var speechOutput = npc.AddComponent<NpcSpeechOutput>();
            var augmentedPresentation =
                npc.AddComponent<SpeechAugmentedPresentationDriver>();
            var visualPresentation = npc.GetComponent<NpcTextPresentationDriver>();
            var conversation = npc.GetComponent<NpcConversationBehaviour>();
            var ui = CreateUserInterface(
                characterProfile.DisplayName,
                objectSuffix,
                alignPanelLeft,
                MultiPanelWidth,
                "Speech NPC",
                true,
                true);

            ConfigurePresentationDriver(
                visualPresentation,
                ui,
                npc.GetComponent<Renderer>(),
                npc.transform);
            ConfigureSpeechOutput(
                speechOutput,
                voiceProfile,
                playback);
            ConfigureSpeechPresentation(
                augmentedPresentation,
                visualPresentation,
                speechOutput);
            ConfigureConversationBehaviour(
                conversation,
                characterProfile,
                augmentedPresentation,
                NpcConversationMode.BackendSession,
                DefaultBackendEndpoint,
                DefaultBackendTimeoutSeconds);
            ConfigureInputView(
                ui.InputView,
                ui.InputField,
                ui.SendButton,
                conversation);
            ConfigureSessionControlView(
                ui.SessionControlView,
                ui.ResetButton,
                ui.MemoryStatusText,
                conversation);
            ConfigureSpeechControlView(
                ui.SpeechControlView,
                speechOutput,
                ui.SpeechToggle,
                ui.StopSpeechButton,
                ui.SpeechStatusText,
                ui.SpeechDisclosureText);
        }

        /// <summary>
        /// Creates one V2 NPC with visual fallback, TTS, and reviewed push-to-talk input.
        /// </summary>
        private static void CreateConfiguredVoiceInputNpc(
            CharacterProfile characterProfile,
            NpcVoiceProfile voiceProfile)
        {
            var npc = CreateNpc(
                characterProfile.DisplayName,
                Vector3.zero,
                "Voice Input NPC");
            var playback = npc.AddComponent<UnityPcmSpeechPlaybackDriver>();
            var audioSource = npc.GetComponent<AudioSource>();
            audioSource.playOnAwake = false;
            audioSource.loop = false;
            audioSource.spatialBlend = 0f;
            var speechOutput = npc.AddComponent<NpcSpeechOutput>();
            var augmentedPresentation =
                npc.AddComponent<SpeechAugmentedPresentationDriver>();
            var captureDriver = npc.AddComponent<UnityMicrophoneCaptureDriver>();
            var voiceInput = npc.AddComponent<NpcVoiceInput>();
            var visualPresentation = npc.GetComponent<NpcTextPresentationDriver>();
            var conversation = npc.GetComponent<NpcConversationBehaviour>();
            var ui = CreateUserInterface(
                characterProfile.DisplayName,
                string.Empty,
                false,
                VoicePanelWidth,
                "Voice Input NPC",
                true,
                true,
                true);

            ConfigurePresentationDriver(
                visualPresentation,
                ui,
                npc.GetComponent<Renderer>(),
                npc.transform);
            ConfigureSpeechOutput(speechOutput, voiceProfile, playback);
            ConfigureSpeechPresentation(
                augmentedPresentation,
                visualPresentation,
                speechOutput);
            ConfigureConversationBehaviour(
                conversation,
                characterProfile,
                augmentedPresentation,
                NpcConversationMode.BackendSession,
                DefaultBackendEndpoint,
                DefaultBackendTimeoutSeconds);
            ConfigureInputView(
                ui.InputView,
                ui.InputField,
                ui.SendButton,
                conversation);
            ConfigureSessionControlView(
                ui.SessionControlView,
                ui.ResetButton,
                ui.MemoryStatusText,
                conversation);
            ConfigureSpeechControlView(
                ui.SpeechControlView,
                speechOutput,
                ui.SpeechToggle,
                ui.StopSpeechButton,
                ui.SpeechStatusText,
                ui.SpeechDisclosureText);
            ConfigureVoiceInput(
                voiceInput,
                captureDriver,
                DefaultTranscriptionEndpoint,
                DefaultBackendTimeoutSeconds);
            ConfigurePushToTalkView(
                ui.PushToTalkInputView,
                voiceInput,
                ui.InputView,
                ui.PushToTalkButton,
                ui.CancelTranscriptionButton,
                ui.TranscriptionStatusText,
                ui.TranscriptionDisclosureText,
                speechOutput);
        }

        /// <summary>
        /// Creates a screen-space uGUI panel with all required input and output controls.
        /// </summary>
        private static PrototypeUiReferences CreateUserInterface(
            string displayName,
            string objectSuffix,
            bool alignPanelLeft,
            float panelWidth,
            string compositionLabel,
            bool includeSessionControls,
            bool includeSpeechControls = false,
            bool includeTranscriptionControls = false)
        {
            var canvasObject = new GameObject(
                GetObjectName(compositionLabel + " Canvas", objectSuffix),
                typeof(RectTransform),
                typeof(Canvas),
                typeof(CanvasScaler),
                typeof(GraphicRaycaster));

            var canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;

            var scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1280f, 720f);
            scaler.matchWidthOrHeight = 0.5f;

            var panel = new GameObject(
                GetObjectName("Conversation Panel", objectSuffix),
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image));
            panel.transform.SetParent(canvasObject.transform, false);

            var panelImage = panel.GetComponent<Image>();
            panelImage.color = new Color(0.04f, 0.055f, 0.08f, 0.94f);
            SetPanelRect(
                panel.GetComponent<RectTransform>(),
                alignPanelLeft,
                panelWidth);
            if (includeSpeechControls)
            {
                panel.GetComponent<RectTransform>().sizeDelta =
                    new Vector2(panelWidth, includeTranscriptionControls ? 700f : 690f);
            }

            var contentWidth = panelWidth - 40f;
            var dialogueHeight = includeTranscriptionControls ? 112f : 170f;
            var emotionY = includeTranscriptionControls ? -192f : -266f;
            var gestureY = includeTranscriptionControls ? -222f : -310f;
            var requestStatusY = includeTranscriptionControls ? -252f : -354f;
            var memoryStatusY = includeTranscriptionControls ? -282f : -392f;
            var hintY = includeTranscriptionControls
                ? -308f
                : includeSessionControls ? -424f : -400f;
            var inputY = includeTranscriptionControls
                ? -340f
                : includeSessionControls ? -464f : -452f;

            var resources = new DefaultControls.Resources();
            var title = CreateText(
                resources,
                panel.transform,
                GetObjectName("Character Name", objectSuffix),
                displayName,
                28,
                TextAnchor.MiddleLeft,
                new Vector2(20f, -20f),
                new Vector2(contentWidth, 42f));
            title.fontStyle = FontStyle.Bold;
            title.color = new Color(0.55f, 0.85f, 1f);

            var dialogue = CreateText(
                resources,
                panel.transform,
                GetObjectName("Dialogue Output", objectSuffix),
                "대화 출력",
                20,
                TextAnchor.UpperLeft,
                new Vector2(20f, -78f),
                new Vector2(contentWidth, dialogueHeight));

            var emotion = CreateText(
                resources,
                panel.transform,
                GetObjectName("Emotion Output", objectSuffix),
                "감정: Neutral",
                20,
                TextAnchor.MiddleLeft,
                new Vector2(20f, emotionY),
                new Vector2(contentWidth, includeTranscriptionControls ? 28f : 38f));

            var gesture = CreateText(
                resources,
                panel.transform,
                GetObjectName("Gesture Output", objectSuffix),
                "제스처: None",
                20,
                TextAnchor.MiddleLeft,
                new Vector2(20f, gestureY),
                new Vector2(contentWidth, includeTranscriptionControls ? 28f : 38f));

            var status = CreateText(
                resources,
                panel.transform,
                GetObjectName("Request Status", objectSuffix),
                "상태: 준비",
                18,
                TextAnchor.MiddleLeft,
                new Vector2(20f, requestStatusY),
                new Vector2(contentWidth, includeTranscriptionControls ? 28f : 38f));
            status.color = new Color(0.75f, 0.8f, 0.9f);

            Text memoryStatus = null;
            if (includeSessionControls)
            {
                memoryStatus = CreateText(
                    resources,
                    panel.transform,
                    GetObjectName("Memory Status", objectSuffix),
                    "단기 기억: 활성",
                    16,
                    TextAnchor.MiddleLeft,
                    new Vector2(20f, memoryStatusY),
                    new Vector2(contentWidth, includeTranscriptionControls ? 24f : 28f));
                memoryStatus.color = new Color(0.55f, 0.9f, 0.72f);
            }

            var hint = CreateText(
                resources,
                panel.transform,
                GetObjectName("Input Hint", objectSuffix),
                includeSessionControls
                    ? "사실을 말한 뒤 재질문하고 Reset을 눌러 보세요."
                    : "Try: 안녕 / 고마워 / 무엇을 좋아해?",
                16,
                TextAnchor.MiddleLeft,
                new Vector2(20f, hintY),
                new Vector2(contentWidth, includeTranscriptionControls ? 28f : 32f));
            hint.color = new Color(0.65f, 0.7f, 0.8f);

            var inputObject = DefaultControls.CreateInputField(resources);
            inputObject.name = GetObjectName("Player Input", objectSuffix);
            inputObject.transform.SetParent(panel.transform, false);
            SetTopLeftRect(
                inputObject.GetComponent<RectTransform>(),
                new Vector2(20f, inputY),
                new Vector2(
                    includeSessionControls ? panelWidth - 240f : panelWidth - 175f,
                    includeTranscriptionControls
                        ? 48f
                        : includeSessionControls ? 56f : 64f));

            var inputField = inputObject.GetComponent<InputField>();
            inputField.lineType = InputField.LineType.SingleLine;
            inputField.characterLimit = 240;
            inputField.placeholder.GetComponent<Text>().text = "NPC에게 메시지 입력";
            inputField.textComponent.fontSize = 18;
            inputField.placeholder.GetComponent<Text>().fontSize = 18;

            var buttonObject = DefaultControls.CreateButton(resources);
            buttonObject.name = GetObjectName("Send Button", objectSuffix);
            buttonObject.transform.SetParent(panel.transform, false);
            SetTopLeftRect(
                buttonObject.GetComponent<RectTransform>(),
                new Vector2(
                    includeSessionControls ? panelWidth - 205f : panelWidth - 140f,
                    inputY),
                new Vector2(
                    includeSessionControls ? 80f : 120f,
                    includeTranscriptionControls
                        ? 48f
                        : includeSessionControls ? 56f : 64f));

            var sendButton = buttonObject.GetComponent<Button>();
            var buttonLabel = buttonObject.GetComponentInChildren<Text>();
            buttonLabel.text = "전송";
            buttonLabel.fontSize = 20;
            buttonLabel.fontStyle = FontStyle.Bold;

            Button resetButton = null;
            if (includeSessionControls)
            {
                var resetObject = DefaultControls.CreateButton(resources);
                resetObject.name = GetObjectName("Reset Button", objectSuffix);
                resetObject.transform.SetParent(panel.transform, false);
                SetTopLeftRect(
                    resetObject.GetComponent<RectTransform>(),
                    new Vector2(panelWidth - 110f, inputY),
                    new Vector2(90f, includeTranscriptionControls ? 48f : 56f));
                resetButton = resetObject.GetComponent<Button>();
                var resetLabel = resetObject.GetComponentInChildren<Text>();
                resetLabel.text = "Reset";
                resetLabel.fontSize = 18;
                resetLabel.fontStyle = FontStyle.Bold;
            }

            Button pushToTalkButton = null;
            Button cancelTranscriptionButton = null;
            Text transcriptionStatus = null;
            Text transcriptionDisclosure = null;
            NpcPushToTalkInputView pushToTalkInputView = null;
            if (includeTranscriptionControls)
            {
                var pushToTalkObject = DefaultControls.CreateButton(resources);
                pushToTalkObject.name = GetObjectName(
                    "Push To Talk Button",
                    objectSuffix);
                pushToTalkObject.transform.SetParent(panel.transform, false);
                SetTopLeftRect(
                    pushToTalkObject.GetComponent<RectTransform>(),
                    new Vector2(20f, -396f),
                    new Vector2(190f, 42f));
                pushToTalkButton = pushToTalkObject.GetComponent<Button>();
                var pushToTalkLabel = pushToTalkObject.GetComponentInChildren<Text>();
                pushToTalkLabel.text = "누르는 동안 말하기";
                pushToTalkLabel.fontSize = 16;
                pushToTalkLabel.fontStyle = FontStyle.Bold;
                pushToTalkInputView =
                    pushToTalkObject.AddComponent<NpcPushToTalkInputView>();

                var cancelObject = DefaultControls.CreateButton(resources);
                cancelObject.name = GetObjectName(
                    "Cancel Transcription Button",
                    objectSuffix);
                cancelObject.transform.SetParent(panel.transform, false);
                SetTopLeftRect(
                    cancelObject.GetComponent<RectTransform>(),
                    new Vector2(220f, -396f),
                    new Vector2(100f, 42f));
                cancelTranscriptionButton = cancelObject.GetComponent<Button>();
                var cancelLabel = cancelObject.GetComponentInChildren<Text>();
                cancelLabel.text = "취소";
                cancelLabel.fontSize = 16;

                transcriptionStatus = CreateText(
                    resources,
                    panel.transform,
                    GetObjectName("Transcription Status", objectSuffix),
                    "음성 입력: 준비",
                    15,
                    TextAnchor.MiddleLeft,
                    new Vector2(330f, -396f),
                    new Vector2(contentWidth - 310f, 42f));
                transcriptionStatus.color = new Color(0.55f, 0.85f, 1f);

                transcriptionDisclosure = CreateText(
                    resources,
                    panel.transform,
                    GetObjectName("Transcription Disclosure", objectSuffix),
                    "마이크 음성이 AI 전사를 위해 처리됩니다.",
                    14,
                    TextAnchor.MiddleLeft,
                    new Vector2(20f, -442f),
                    new Vector2(contentWidth, 34f));
                transcriptionDisclosure.color = new Color(0.9f, 0.75f, 0.45f);
            }

            var instructions = CreateText(
                resources,
                panel.transform,
                GetObjectName("Verification Instructions", objectSuffix),
                includeTranscriptionControls
                    ? "전사 결과를 확인·수정한 뒤 전송하세요. 자동 전송되지 않습니다."
                    : includeSpeechControls
                    ? "응답 텍스트는 음성 실패와 무관하게 유지됩니다."
                    : includeSessionControls
                    ? "각 NPC는 독립 세션입니다. Reset은 선택한 NPC 기억만 지웁니다."
                    : "응답 후 NPC 색상은 감정, 기울기는 제스처를 표시합니다.",
                15,
                TextAnchor.UpperLeft,
                new Vector2(
                    20f,
                    includeTranscriptionControls
                        ? -480f
                        : includeSessionControls ? -536f : -532f),
                new Vector2(
                    contentWidth,
                    includeTranscriptionControls ? 54f : 58f));
            instructions.color = new Color(0.65f, 0.7f, 0.8f);

            Text speechStatus = null;
            Text speechDisclosure = null;
            Toggle speechToggle = null;
            Button stopSpeechButton = null;
            if (includeSpeechControls)
            {
                speechStatus = CreateText(
                    resources,
                    panel.transform,
                    GetObjectName("Speech Status", objectSuffix),
                    "음성: 준비",
                    15,
                    TextAnchor.MiddleLeft,
                    new Vector2(
                        20f,
                        includeTranscriptionControls ? -538f : -590f),
                    new Vector2(contentWidth, 26f));
                speechStatus.color = new Color(0.55f, 0.85f, 1f);

                var toggleObject = DefaultControls.CreateToggle(resources);
                toggleObject.name = GetObjectName("Speech Toggle", objectSuffix);
                toggleObject.transform.SetParent(panel.transform, false);
                SetTopLeftRect(
                    toggleObject.GetComponent<RectTransform>(),
                    new Vector2(
                        20f,
                        includeTranscriptionControls ? -568f : -620f),
                    new Vector2(180f, 30f));
                speechToggle = toggleObject.GetComponent<Toggle>();
                speechToggle.isOn = true;
                var toggleLabel = toggleObject.GetComponentInChildren<Text>();
                toggleLabel.text = "음성 출력";
                toggleLabel.fontSize = 15;

                var stopObject = DefaultControls.CreateButton(resources);
                stopObject.name = GetObjectName("Stop Speech Button", objectSuffix);
                stopObject.transform.SetParent(panel.transform, false);
                SetTopLeftRect(
                    stopObject.GetComponent<RectTransform>(),
                    new Vector2(
                        panelWidth - 125f,
                        includeTranscriptionControls ? -568f : -620f),
                    new Vector2(105f, 30f));
                stopSpeechButton = stopObject.GetComponent<Button>();
                var stopLabel = stopObject.GetComponentInChildren<Text>();
                stopLabel.text = "음성 정지";
                stopLabel.fontSize = 14;

                speechDisclosure = CreateText(
                    resources,
                    panel.transform,
                    GetObjectName("Speech Disclosure", objectSuffix),
                    "이 음성은 AI로 생성됩니다.",
                    14,
                    TextAnchor.MiddleLeft,
                    new Vector2(
                        20f,
                        includeTranscriptionControls ? -602f : -654f),
                    new Vector2(contentWidth, 24f));
                speechDisclosure.color = new Color(0.9f, 0.75f, 0.45f);
            }

            var inputView = panel.AddComponent<NpcTextInputView>();
            var sessionControlView = includeSessionControls
                ? panel.AddComponent<NpcSessionControlView>()
                : null;
            var speechControlView = includeSpeechControls
                ? panel.AddComponent<NpcSpeechControlView>()
                : null;
            return new PrototypeUiReferences
            {
                DialogueText = dialogue,
                EmotionText = emotion,
                GestureText = gesture,
                StatusText = status,
                InputField = inputField,
                SendButton = sendButton,
                ResetButton = resetButton,
                MemoryStatusText = memoryStatus,
                InputView = inputView,
                SessionControlView = sessionControlView,
                SpeechToggle = speechToggle,
                StopSpeechButton = stopSpeechButton,
                SpeechStatusText = speechStatus,
                SpeechDisclosureText = speechDisclosure,
                SpeechControlView = speechControlView,
                PushToTalkButton = pushToTalkButton,
                CancelTranscriptionButton = cancelTranscriptionButton,
                TranscriptionStatusText = transcriptionStatus,
                TranscriptionDisclosureText = transcriptionDisclosure,
                PushToTalkInputView = pushToTalkInputView
            };
        }

        /// <summary>
        /// Creates one configured legacy uGUI Text under the selected parent.
        /// </summary>
        private static Text CreateText(
            DefaultControls.Resources resources,
            Transform parent,
            string objectName,
            string value,
            int fontSize,
            TextAnchor alignment,
            Vector2 anchoredPosition,
            Vector2 size)
        {
            var textObject = DefaultControls.CreateText(resources);
            textObject.name = objectName;
            textObject.transform.SetParent(parent, false);
            SetTopLeftRect(
                textObject.GetComponent<RectTransform>(),
                anchoredPosition,
                size);

            var text = textObject.GetComponent<Text>();
            text.text = value;
            text.fontSize = fontSize;
            text.alignment = alignment;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Truncate;
            text.color = Color.white;
            return text;
        }

        /// <summary>
        /// Restores every serialized connection for one existing generated NPC and UI panel.
        /// </summary>
        private static void RepairNpcConfiguration(
            CharacterProfile profile,
            string npcObjectName,
            string objectSuffix,
            NpcConversationMode conversationMode,
            string backendEndpoint,
            int backendTimeoutSeconds,
            bool includeSessionControls = false)
        {
            var npc = FindRequiredGameObject(npcObjectName);
            var presentationDriver = npc.GetComponent<NpcTextPresentationDriver>();
            var conversationBehaviour = npc.GetComponent<NpcConversationBehaviour>();
            if (presentationDriver == null || conversationBehaviour == null)
            {
                throw new InvalidOperationException(
                    $"Generated NPC '{npcObjectName}' is missing required runtime components.");
            }

            var ui = FindUiReferences(objectSuffix, includeSessionControls);
            ConfigurePresentationDriver(
                presentationDriver,
                ui,
                npc.GetComponent<Renderer>(),
                npc.transform);
            ConfigureConversationBehaviour(
                conversationBehaviour,
                profile,
                presentationDriver,
                conversationMode,
                backendEndpoint,
                backendTimeoutSeconds);
            ConfigureInputView(
                ui.InputView,
                ui.InputField,
                ui.SendButton,
                conversationBehaviour);
            if (includeSessionControls)
            {
                ConfigureSessionControlView(
                    ui.SessionControlView,
                    ui.ResetButton,
                    ui.MemoryStatusText,
                    conversationBehaviour);
            }
        }

        /// <summary>
        /// Restores every serialized connection for one generated speech NPC.
        /// </summary>
        private static void RepairSpeechNpcConfiguration(
            CharacterProfile characterProfile,
            NpcVoiceProfile voiceProfile,
            string objectSuffix)
        {
            var npc = FindRequiredGameObject("Speech NPC - " + objectSuffix);
            var visualPresentation = npc.GetComponent<NpcTextPresentationDriver>();
            var augmentedPresentation =
                npc.GetComponent<SpeechAugmentedPresentationDriver>();
            var conversation = npc.GetComponent<NpcConversationBehaviour>();
            var playback = npc.GetComponent<UnityPcmSpeechPlaybackDriver>();
            var speechOutput = npc.GetComponent<NpcSpeechOutput>();
            var audioSource = npc.GetComponent<AudioSource>();
            if (visualPresentation == null
                || augmentedPresentation == null
                || conversation == null
                || playback == null
                || speechOutput == null
                || audioSource == null)
            {
                throw new InvalidOperationException(
                    $"Generated speech NPC '{objectSuffix}' is missing runtime components.");
            }

            audioSource.playOnAwake = false;
            audioSource.loop = false;
            audioSource.spatialBlend = 0f;
            var ui = FindUiReferences(objectSuffix, true, true);
            ConfigurePresentationDriver(
                visualPresentation,
                ui,
                npc.GetComponent<Renderer>(),
                npc.transform);
            ConfigureSpeechOutput(speechOutput, voiceProfile, playback);
            ConfigureSpeechPresentation(
                augmentedPresentation,
                visualPresentation,
                speechOutput);
            ConfigureConversationBehaviour(
                conversation,
                characterProfile,
                augmentedPresentation,
                NpcConversationMode.BackendSession,
                DefaultBackendEndpoint,
                DefaultBackendTimeoutSeconds);
            ConfigureInputView(
                ui.InputView,
                ui.InputField,
                ui.SendButton,
                conversation);
            ConfigureSessionControlView(
                ui.SessionControlView,
                ui.ResetButton,
                ui.MemoryStatusText,
                conversation);
            ConfigureSpeechControlView(
                ui.SpeechControlView,
                speechOutput,
                ui.SpeechToggle,
                ui.StopSpeechButton,
                ui.SpeechStatusText,
                ui.SpeechDisclosureText);
        }

        /// <summary>
        /// Restores every serialized connection for the generated push-to-talk NPC.
        /// </summary>
        private static void RepairVoiceInputNpcConfiguration(
            CharacterProfile characterProfile,
            NpcVoiceProfile voiceProfile)
        {
            var npc = FindRequiredGameObject("Voice Input NPC - Luna");
            var visualPresentation = npc.GetComponent<NpcTextPresentationDriver>();
            var augmentedPresentation =
                npc.GetComponent<SpeechAugmentedPresentationDriver>();
            var conversation = npc.GetComponent<NpcConversationBehaviour>();
            var playback = npc.GetComponent<UnityPcmSpeechPlaybackDriver>();
            var speechOutput = npc.GetComponent<NpcSpeechOutput>();
            var captureDriver = npc.GetComponent<UnityMicrophoneCaptureDriver>();
            var voiceInput = npc.GetComponent<NpcVoiceInput>();
            var audioSource = npc.GetComponent<AudioSource>();
            if (visualPresentation == null
                || augmentedPresentation == null
                || conversation == null
                || playback == null
                || speechOutput == null
                || captureDriver == null
                || voiceInput == null
                || audioSource == null)
            {
                throw new InvalidOperationException(
                    "Generated voice input NPC is missing runtime components.");
            }

            audioSource.playOnAwake = false;
            audioSource.loop = false;
            audioSource.spatialBlend = 0f;
            var ui = FindUiReferences(
                string.Empty,
                true,
                true,
                true);
            ConfigurePresentationDriver(
                visualPresentation,
                ui,
                npc.GetComponent<Renderer>(),
                npc.transform);
            ConfigureSpeechOutput(speechOutput, voiceProfile, playback);
            ConfigureSpeechPresentation(
                augmentedPresentation,
                visualPresentation,
                speechOutput);
            ConfigureConversationBehaviour(
                conversation,
                characterProfile,
                augmentedPresentation,
                NpcConversationMode.BackendSession,
                DefaultBackendEndpoint,
                DefaultBackendTimeoutSeconds);
            ConfigureInputView(
                ui.InputView,
                ui.InputField,
                ui.SendButton,
                conversation);
            ConfigureSessionControlView(
                ui.SessionControlView,
                ui.ResetButton,
                ui.MemoryStatusText,
                conversation);
            ConfigureSpeechControlView(
                ui.SpeechControlView,
                speechOutput,
                ui.SpeechToggle,
                ui.StopSpeechButton,
                ui.SpeechStatusText,
                ui.SpeechDisclosureText);
            ConfigureVoiceInput(
                voiceInput,
                captureDriver,
                DefaultTranscriptionEndpoint,
                DefaultBackendTimeoutSeconds);
            ConfigurePushToTalkView(
                ui.PushToTalkInputView,
                voiceInput,
                ui.InputView,
                ui.PushToTalkButton,
                ui.CancelTranscriptionButton,
                ui.TranscriptionStatusText,
                ui.TranscriptionDisclosureText,
                speechOutput);
        }

        /// <summary>
        /// Finds one generated UI set by its optional Phase 2 character suffix.
        /// </summary>
        private static PrototypeUiReferences FindUiReferences(
            string objectSuffix,
            bool includeSessionControls,
            bool includeSpeechControls = false,
            bool includeTranscriptionControls = false)
        {
            var references = new PrototypeUiReferences
            {
                DialogueText = FindRequiredComponent<Text>(
                    GetObjectName("Dialogue Output", objectSuffix)),
                EmotionText = FindRequiredComponent<Text>(
                    GetObjectName("Emotion Output", objectSuffix)),
                GestureText = FindRequiredComponent<Text>(
                    GetObjectName("Gesture Output", objectSuffix)),
                StatusText = FindRequiredComponent<Text>(
                    GetObjectName("Request Status", objectSuffix)),
                InputField = FindRequiredComponent<InputField>(
                    GetObjectName("Player Input", objectSuffix)),
                SendButton = FindRequiredComponent<Button>(
                    GetObjectName("Send Button", objectSuffix)),
                InputView = FindRequiredComponent<NpcTextInputView>(
                    GetObjectName("Conversation Panel", objectSuffix))
            };

            if (includeSessionControls)
            {
                references.ResetButton = FindRequiredComponent<Button>(
                    GetObjectName("Reset Button", objectSuffix));
                references.MemoryStatusText = FindRequiredComponent<Text>(
                    GetObjectName("Memory Status", objectSuffix));
                references.SessionControlView =
                    FindRequiredComponent<NpcSessionControlView>(
                        GetObjectName("Conversation Panel", objectSuffix));
            }

            if (includeSpeechControls)
            {
                references.SpeechToggle = FindRequiredComponent<Toggle>(
                    GetObjectName("Speech Toggle", objectSuffix));
                references.StopSpeechButton = FindRequiredComponent<Button>(
                    GetObjectName("Stop Speech Button", objectSuffix));
                references.SpeechStatusText = FindRequiredComponent<Text>(
                    GetObjectName("Speech Status", objectSuffix));
                references.SpeechDisclosureText = FindRequiredComponent<Text>(
                    GetObjectName("Speech Disclosure", objectSuffix));
                references.SpeechControlView =
                    FindRequiredComponent<NpcSpeechControlView>(
                        GetObjectName("Conversation Panel", objectSuffix));
            }

            if (includeTranscriptionControls)
            {
                references.PushToTalkButton = FindRequiredComponent<Button>(
                    GetObjectName("Push To Talk Button", objectSuffix));
                references.CancelTranscriptionButton = FindRequiredComponent<Button>(
                    GetObjectName("Cancel Transcription Button", objectSuffix));
                references.TranscriptionStatusText = FindRequiredComponent<Text>(
                    GetObjectName("Transcription Status", objectSuffix));
                references.TranscriptionDisclosureText = FindRequiredComponent<Text>(
                    GetObjectName("Transcription Disclosure", objectSuffix));
                references.PushToTalkInputView =
                    FindRequiredComponent<NpcPushToTalkInputView>(
                        GetObjectName("Push To Talk Button", objectSuffix));
            }

            return references;
        }

        /// <summary>
        /// Finds one active generated object or reports the exact missing name.
        /// </summary>
        private static GameObject FindRequiredGameObject(string objectName)
        {
            var gameObject = GameObject.Find(objectName);
            if (gameObject == null)
            {
                throw new InvalidOperationException(
                    $"Generated object '{objectName}' was not found.");
            }

            return gameObject;
        }

        /// <summary>
        /// Finds one generated object and returns its required component.
        /// </summary>
        private static T FindRequiredComponent<T>(string objectName)
            where T : Component
        {
            var gameObject = FindRequiredGameObject(objectName);
            var component = gameObject.GetComponent<T>();
            if (component == null)
            {
                throw new InvalidOperationException(
                    $"Generated object '{objectName}' is missing {typeof(T).Name}.");
            }

            return component;
        }

        /// <summary>
        /// Wires UI and visual targets into the serialized presentation adapter.
        /// </summary>
        private static void ConfigurePresentationDriver(
            NpcTextPresentationDriver presentationDriver,
            PrototypeUiReferences ui,
            Renderer npcRenderer,
            Transform npcTransform)
        {
            var serializedDriver = new SerializedObject(presentationDriver);
            SetObjectReference(serializedDriver, "dialogueText", ui.DialogueText);
            SetObjectReference(serializedDriver, "emotionText", ui.EmotionText);
            SetObjectReference(serializedDriver, "gestureText", ui.GestureText);
            SetObjectReference(serializedDriver, "statusText", ui.StatusText);
            SetObjectReference(serializedDriver, "sendButton", ui.SendButton);
            SetObjectReference(serializedDriver, "resetButton", ui.ResetButton);
            SetObjectReference(serializedDriver, "emotionRenderer", npcRenderer);
            SetObjectReference(serializedDriver, "gestureTarget", npcTransform);
            serializedDriver.ApplyModifiedPropertiesWithoutUndo();
        }

        /// <summary>
        /// Wires one voice asset, PCM driver, and local Speech V1 endpoint into an NPC.
        /// </summary>
        private static void ConfigureSpeechOutput(
            NpcSpeechOutput speechOutput,
            NpcVoiceProfile voiceProfile,
            UnityPcmSpeechPlaybackDriver playbackDriver)
        {
            if (voiceProfile == null)
            {
                throw new InvalidOperationException(
                    "Speech output requires an NpcVoiceProfile asset.");
            }

            ValidateVoiceProfile(
                voiceProfile,
                AssetDatabase.GetAssetPath(voiceProfile));

            var serializedOutput = new SerializedObject(speechOutput);
            SetObjectReference(serializedOutput, "voiceProfile", voiceProfile);
            SetObjectReference(serializedOutput, "playbackDriver", playbackDriver);
            SetStringValue(
                serializedOutput,
                "backendEndpoint",
                DefaultSpeechEndpoint);
            SetIntegerValue(
                serializedOutput,
                "backendTimeoutSeconds",
                DefaultBackendTimeoutSeconds);
            var enabledProperty = serializedOutput.FindProperty("speechEnabled");
            if (enabledProperty == null)
            {
                throw new InvalidOperationException(
                    "Serialized property 'speechEnabled' was not found.");
            }

            enabledProperty.boolValue = true;
            serializedOutput.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(speechOutput);
        }

        /// <summary>
        /// Wires the visual fallback and optional speech output into the presentation decorator.
        /// </summary>
        private static void ConfigureSpeechPresentation(
            SpeechAugmentedPresentationDriver augmentedPresentation,
            NpcTextPresentationDriver visualPresentation,
            NpcSpeechOutput speechOutput)
        {
            var serializedPresentation = new SerializedObject(augmentedPresentation);
            SetObjectReference(
                serializedPresentation,
                "visualDriverSource",
                visualPresentation);
            SetObjectReference(
                serializedPresentation,
                "speechOutput",
                speechOutput);
            serializedPresentation.ApplyModifiedPropertiesWithoutUndo();
        }

        /// <summary>
        /// Wires the profile and presentation interface source into the Unity bridge.
        /// </summary>
        private static void ConfigureConversationBehaviour(
            NpcConversationBehaviour conversationBehaviour,
            CharacterProfile profile,
            MonoBehaviour presentationDriver,
            NpcConversationMode conversationMode,
            string backendEndpoint,
            int backendTimeoutSeconds)
        {
            if (profile == null || !EditorUtility.IsPersistent(profile))
            {
                throw new InvalidOperationException(
                    "Conversation profiles must be persistent Unity assets.");
            }

            var serializedBehaviour = new SerializedObject(conversationBehaviour);
            serializedBehaviour.Update();
            SetObjectReference(serializedBehaviour, "characterProfile", profile);
            SetObjectReference(
                serializedBehaviour,
                "presentationDriverSource",
                presentationDriver);
            SetEnumValue(
                serializedBehaviour,
                "conversationMode",
                (int)conversationMode);
            SetStringValue(
                serializedBehaviour,
                "backendEndpoint",
                backendEndpoint);
            SetStringValue(
                serializedBehaviour,
                "sessionBackendEndpoint",
                DefaultSessionBackendEndpoint);
            SetStringValue(
                serializedBehaviour,
                "sessionResetEndpoint",
                DefaultSessionResetEndpoint);
            SetIntegerValue(
                serializedBehaviour,
                "backendTimeoutSeconds",
                backendTimeoutSeconds);
            serializedBehaviour.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(conversationBehaviour);

            var savedProfile = serializedBehaviour.FindProperty(
                "characterProfile").objectReferenceValue;
            if (savedProfile != profile)
            {
                throw new InvalidOperationException(
                    "Failed to assign the persistent CharacterProfile reference.");
            }
        }

        /// <summary>
        /// Wires the generated uGUI controls and conversation bridge into the input view.
        /// </summary>
        private static void ConfigureInputView(
            NpcTextInputView inputView,
            InputField inputField,
            Button sendButton,
            NpcConversationBehaviour conversationBehaviour)
        {
            var serializedInputView = new SerializedObject(inputView);
            SetObjectReference(serializedInputView, "inputField", inputField);
            SetObjectReference(serializedInputView, "sendButton", sendButton);
            SetObjectReference(
                serializedInputView,
                "conversationBehaviour",
                conversationBehaviour);
            serializedInputView.ApplyModifiedPropertiesWithoutUndo();
        }

        /// <summary>
        /// Wires one reset button and memory label into the optional session view.
        /// </summary>
        private static void ConfigureSessionControlView(
            NpcSessionControlView sessionControlView,
            Button resetButton,
            Text memoryStatusText,
            NpcConversationBehaviour conversationBehaviour)
        {
            if (sessionControlView == null
                || resetButton == null
                || memoryStatusText == null)
            {
                throw new InvalidOperationException(
                    "Session controls require a view, reset button, and status label.");
            }

            var serializedView = new SerializedObject(sessionControlView);
            SetObjectReference(serializedView, "resetButton", resetButton);
            SetObjectReference(serializedView, "memoryStatusText", memoryStatusText);
            SetObjectReference(
                serializedView,
                "conversationBehaviour",
                conversationBehaviour);
            serializedView.ApplyModifiedPropertiesWithoutUndo();
        }

        /// <summary>
        /// Wires local speech preference, stop, status, and disclosure controls.
        /// </summary>
        private static void ConfigureSpeechControlView(
            NpcSpeechControlView speechControlView,
            NpcSpeechOutput speechOutput,
            Toggle speechToggle,
            Button stopSpeechButton,
            Text speechStatusText,
            Text speechDisclosureText)
        {
            if (speechControlView == null
                || speechOutput == null
                || speechToggle == null
                || stopSpeechButton == null
                || speechStatusText == null
                || speechDisclosureText == null)
            {
                throw new InvalidOperationException(
                    "Speech controls require complete output and UI references.");
            }

            var serializedView = new SerializedObject(speechControlView);
            SetObjectReference(serializedView, "speechOutput", speechOutput);
            SetObjectReference(serializedView, "speechToggle", speechToggle);
            SetObjectReference(serializedView, "stopButton", stopSpeechButton);
            SetObjectReference(
                serializedView,
                "speechStatusText",
                speechStatusText);
            SetObjectReference(
                serializedView,
                "disclosureText",
                speechDisclosureText);
            serializedView.ApplyModifiedPropertiesWithoutUndo();
        }

        /// <summary>
        /// Wires one microphone capture driver and local Transcription V1 endpoint.
        /// </summary>
        private static void ConfigureVoiceInput(
            NpcVoiceInput voiceInput,
            UnityMicrophoneCaptureDriver captureDriver,
            string backendEndpoint,
            int backendTimeoutSeconds)
        {
            if (voiceInput == null || captureDriver == null)
            {
                throw new InvalidOperationException(
                    "Voice input requires a component and microphone capture driver.");
            }

            var serializedInput = new SerializedObject(voiceInput);
            SetObjectReference(
                serializedInput,
                "captureDriver",
                captureDriver);
            SetStringValue(
                serializedInput,
                "backendEndpoint",
                backendEndpoint);
            SetIntegerValue(
                serializedInput,
                "backendTimeoutSeconds",
                backendTimeoutSeconds);
            serializedInput.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(voiceInput);
        }

        /// <summary>
        /// Wires push-to-talk controls, reviewed text target, disclosure, and TTS stop event.
        /// </summary>
        private static void ConfigurePushToTalkView(
            NpcPushToTalkInputView pushToTalkView,
            NpcVoiceInput voiceInput,
            NpcTextInputView textInputView,
            Button pushToTalkButton,
            Button cancelButton,
            Text statusText,
            Text disclosureText,
            NpcSpeechOutput speechOutput)
        {
            if (pushToTalkView == null
                || voiceInput == null
                || textInputView == null
                || pushToTalkButton == null
                || cancelButton == null
                || statusText == null
                || disclosureText == null
                || speechOutput == null)
            {
                throw new InvalidOperationException(
                    "Push-to-talk controls require complete input, output, and UI references.");
            }

            var serializedView = new SerializedObject(pushToTalkView);
            SetObjectReference(serializedView, "voiceInput", voiceInput);
            SetObjectReference(serializedView, "textInputView", textInputView);
            SetObjectReference(
                serializedView,
                "pushToTalkButton",
                pushToTalkButton);
            SetObjectReference(serializedView, "cancelButton", cancelButton);
            SetObjectReference(
                serializedView,
                "transcriptionStatusText",
                statusText);
            SetObjectReference(serializedView, "disclosureText", disclosureText);
            serializedView.ApplyModifiedPropertiesWithoutUndo();

            var recordingStarted = pushToTalkView.RecordingStarted;
            for (var index = recordingStarted.GetPersistentEventCount() - 1;
                 index >= 0;
                 index--)
            {
                if (recordingStarted.GetPersistentTarget(index) == speechOutput
                    && recordingStarted.GetPersistentMethodName(index)
                    == nameof(NpcSpeechOutput.StopSpeech))
                {
                    UnityEventTools.RemovePersistentListener(
                        recordingStarted,
                        index);
                }
            }

            UnityEventTools.AddPersistentListener(
                recordingStarted,
                speechOutput.StopSpeech);
            EditorUtility.SetDirty(pushToTalkView);
        }

        /// <summary>
        /// Creates a new Input System EventSystem and connects persistent UI action references.
        /// </summary>
        private static void CreateInputSystemEventSystem()
        {
            var eventSystemObject = new GameObject("EventSystem");
            eventSystemObject.SetActive(false);
            eventSystemObject.AddComponent<EventSystem>();
            var inputModule = eventSystemObject.AddComponent<InputSystemUIInputModule>();

            if (!TryConfigureProjectInputActions(inputModule))
            {
                inputModule.AssignDefaultActions();
                Debug.LogWarning(
                    "Project UI action references were unavailable; the prototype will use Input System defaults.");
            }

            eventSystemObject.SetActive(true);
        }

        /// <summary>
        /// Loads persistent InputActionReference sub-assets from the project's existing action asset.
        /// </summary>
        private static bool TryConfigureProjectInputActions(
            InputSystemUIInputModule inputModule)
        {
            var actionsAsset = AssetDatabase.LoadAssetAtPath<InputActionAsset>(
                InputActionsPath);
            if (actionsAsset == null)
            {
                return false;
            }

            var references = new Dictionary<string, InputActionReference>(
                StringComparer.Ordinal);
            foreach (var asset in AssetDatabase.LoadAllAssetsAtPath(InputActionsPath))
            {
                if (asset is InputActionReference reference
                    && reference.action != null
                    && reference.action.actionMap != null
                    && reference.action.actionMap.name == "UI")
                {
                    references[reference.action.name] = reference;
                }
            }

            if (!TryGetReference(references, "Navigate", out var move)
                || !TryGetReference(references, "Submit", out var submit)
                || !TryGetReference(references, "Cancel", out var cancel)
                || !TryGetReference(references, "Point", out var point)
                || !TryGetReference(references, "Click", out var leftClick)
                || !TryGetReference(references, "RightClick", out var rightClick)
                || !TryGetReference(references, "MiddleClick", out var middleClick)
                || !TryGetReference(references, "ScrollWheel", out var scrollWheel)
                || !TryGetReference(
                    references,
                    "TrackedDevicePosition",
                    out var trackedPosition)
                || !TryGetReference(
                    references,
                    "TrackedDeviceOrientation",
                    out var trackedOrientation))
            {
                return false;
            }

            inputModule.actionsAsset = actionsAsset;
            inputModule.move = move;
            inputModule.submit = submit;
            inputModule.cancel = cancel;
            inputModule.point = point;
            inputModule.leftClick = leftClick;
            inputModule.rightClick = rightClick;
            inputModule.middleClick = middleClick;
            inputModule.scrollWheel = scrollWheel;
            inputModule.trackedDevicePosition = trackedPosition;
            inputModule.trackedDeviceOrientation = trackedOrientation;
            return true;
        }

        /// <summary>
        /// Retrieves one named action reference without relying on exceptions.
        /// </summary>
        private static bool TryGetReference(
            IReadOnlyDictionary<string, InputActionReference> references,
            string actionName,
            out InputActionReference reference)
        {
            return references.TryGetValue(actionName, out reference)
                && reference != null;
        }

        /// <summary>
        /// Assigns one object reference and fails early if a serialized field was renamed.
        /// </summary>
        private static void SetObjectReference(
            SerializedObject serializedObject,
            string propertyName,
            UnityEngine.Object value)
        {
            var property = serializedObject.FindProperty(propertyName);
            if (property == null)
            {
                throw new InvalidOperationException(
                    $"Serialized property '{propertyName}' was not found on "
                    + serializedObject.targetObject.GetType().Name
                    + ".");
            }

            property.objectReferenceValue = value;
        }

        /// <summary>
        /// Assigns one enum value and fails early if a serialized field was renamed.
        /// </summary>
        private static void SetEnumValue(
            SerializedObject serializedObject,
            string propertyName,
            int value)
        {
            var property = serializedObject.FindProperty(propertyName);
            if (property == null)
            {
                throw new InvalidOperationException(
                    $"Serialized property '{propertyName}' was not found on "
                    + serializedObject.targetObject.GetType().Name
                    + ".");
            }

            property.enumValueIndex = value;
        }

        /// <summary>
        /// Assigns one string value and fails early if a serialized field was renamed.
        /// </summary>
        private static void SetStringValue(
            SerializedObject serializedObject,
            string propertyName,
            string value)
        {
            var property = serializedObject.FindProperty(propertyName);
            if (property == null)
            {
                throw new InvalidOperationException(
                    $"Serialized property '{propertyName}' was not found on "
                    + serializedObject.targetObject.GetType().Name
                    + ".");
            }

            property.stringValue = value;
        }

        /// <summary>
        /// Assigns one integer value and fails early if a serialized field was renamed.
        /// </summary>
        private static void SetIntegerValue(
            SerializedObject serializedObject,
            string propertyName,
            int value)
        {
            var property = serializedObject.FindProperty(propertyName);
            if (property == null)
            {
                throw new InvalidOperationException(
                    $"Serialized property '{propertyName}' was not found on "
                    + serializedObject.targetObject.GetType().Name
                    + ".");
            }

            property.intValue = value;
        }

        /// <summary>
        /// Keeps Phase 1 names unchanged and adds a stable character suffix for Phase 2 objects.
        /// </summary>
        private static string GetObjectName(string baseName, string objectSuffix)
        {
            return string.IsNullOrWhiteSpace(objectSuffix)
                ? baseName
                : $"{baseName} - {objectSuffix.Trim()}";
        }

        /// <summary>
        /// Positions a conversation panel on the selected top screen edge.
        /// </summary>
        private static void SetPanelRect(
            RectTransform rectTransform,
            bool alignLeft,
            float panelWidth)
        {
            var size = new Vector2(panelWidth, 610f);
            if (alignLeft)
            {
                SetTopLeftRect(rectTransform, new Vector2(24f, -24f), size);
                return;
            }

            SetTopRightRect(rectTransform, new Vector2(-24f, -24f), size);
        }

        /// <summary>
        /// Anchors a UI element to the top-left of its parent.
        /// </summary>
        private static void SetTopLeftRect(
            RectTransform rectTransform,
            Vector2 anchoredPosition,
            Vector2 size)
        {
            rectTransform.anchorMin = new Vector2(0f, 1f);
            rectTransform.anchorMax = new Vector2(0f, 1f);
            rectTransform.pivot = new Vector2(0f, 1f);
            rectTransform.anchoredPosition = anchoredPosition;
            rectTransform.sizeDelta = size;
        }

        /// <summary>
        /// Anchors a UI panel to the top-right of the screen.
        /// </summary>
        private static void SetTopRightRect(
            RectTransform rectTransform,
            Vector2 anchoredPosition,
            Vector2 size)
        {
            rectTransform.anchorMin = Vector2.one;
            rectTransform.anchorMax = Vector2.one;
            rectTransform.pivot = Vector2.one;
            rectTransform.anchoredPosition = anchoredPosition;
            rectTransform.sizeDelta = size;
        }

        /// <summary>
        /// Creates every shared sample folder required by either mock prototype.
        /// </summary>
        private static void EnsureSampleFolders()
        {
            EnsureFolder(RootFolder);
            EnsureFolder(SamplesFolder);
            EnsureFolder(MockNpcFolder);
            EnsureFolder(ProfilesFolder);
            EnsureFolder(ScenesFolder);
            EnsureFolder(BackendNpcFolder);
            EnsureFolder(BackendScenesFolder);
            EnsureFolder(MemoryNpcFolder);
            EnsureFolder(MemoryScenesFolder);
            EnsureFolder(SpeechNpcFolder);
            EnsureFolder(SpeechProfilesFolder);
            EnsureFolder(SpeechScenesFolder);
            EnsureFolder(VoiceInputNpcFolder);
            EnsureFolder(VoiceInputScenesFolder);
        }

        /// <summary>
        /// Saves one generated scene, persists assets, and selects the saved scene asset.
        /// </summary>
        private static void SaveGeneratedScene(
            Scene scene,
            string scenePath,
            string logAction)
        {
            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene, scenePath))
            {
                throw new InvalidOperationException(
                    $"Failed to save generated scene at {scenePath}.");
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Selection.activeObject = AssetDatabase.LoadAssetAtPath<SceneAsset>(scenePath);
            Debug.Log($"{logAction} at {scenePath}.");
        }

        /// <summary>
        /// Creates every missing segment of an Assets-relative folder path.
        /// </summary>
        private static void EnsureFolder(string folderPath)
        {
            var segments = folderPath.Split(
                new[] { '/' },
                StringSplitOptions.RemoveEmptyEntries);
            if (segments.Length == 0 || segments[0] != "Assets")
            {
                throw new ArgumentException(
                    "Folder paths must begin with Assets.",
                    nameof(folderPath));
            }

            var currentPath = segments[0];
            for (var index = 1; index < segments.Length; index++)
            {
                var nextPath = currentPath + "/" + segments[index];
                if (!AssetDatabase.IsValidFolder(nextPath))
                {
                    AssetDatabase.CreateFolder(currentPath, segments[index]);
                }

                currentPath = nextPath;
            }
        }

        /// <summary>
        /// Holds only the serialized values needed to create one sample profile asset.
        /// </summary>
        private sealed class SampleProfileDefinition
        {
            public string AssetPath { get; }

            public string AssetName { get; }

            public string CharacterId { get; }

            public string DisplayName { get; }

            public string Personality { get; }

            public string SpeechStyle { get; }

            public string ExampleDialogue { get; }

            public NpcEmotion DefaultEmotion { get; }

            /// <summary>
            /// Captures one immutable set of sample profile values for Editor generation.
            /// </summary>
            public SampleProfileDefinition(
                string assetPath,
                string assetName,
                string characterId,
                string displayName,
                string personality,
                string speechStyle,
                string exampleDialogue,
                NpcEmotion defaultEmotion)
            {
                AssetPath = assetPath;
                AssetName = assetName;
                CharacterId = characterId;
                DisplayName = displayName;
                Personality = personality;
                SpeechStyle = speechStyle;
                ExampleDialogue = exampleDialogue;
                DefaultEmotion = defaultEmotion;
            }
        }

        /// <summary>
        /// Groups generated UI references for concise and type-safe scene wiring.
        /// </summary>
        private sealed class PrototypeUiReferences
        {
            public Text DialogueText;
            public Text EmotionText;
            public Text GestureText;
            public Text StatusText;
            public InputField InputField;
            public Button SendButton;
            public Button ResetButton;
            public Text MemoryStatusText;
            public NpcTextInputView InputView;
            public NpcSessionControlView SessionControlView;
            public Toggle SpeechToggle;
            public Button StopSpeechButton;
            public Text SpeechStatusText;
            public Text SpeechDisclosureText;
            public NpcSpeechControlView SpeechControlView;
            public Button PushToTalkButton;
            public Button CancelTranscriptionButton;
            public Text TranscriptionStatusText;
            public Text TranscriptionDisclosureText;
            public NpcPushToTalkInputView PushToTalkInputView;
        }
    }
}
