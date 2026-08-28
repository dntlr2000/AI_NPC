using System;
using System.Collections.Generic;
using AiCharacterKit.Core;
using AiCharacterKit.Unity;
using UnityEditor;
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
        private const string ProfilePath = ProfilesFolder + "/PrototypeCharacter.asset";
        private const string ScenePath = ScenesFolder + "/MockNpcPrototype.unity";
        private const string LunaProfilePath = ProfilesFolder + "/Luna.asset";
        private const string GuardProfilePath = ProfilesFolder + "/Guard.asset";
        private const string MultiCharacterScenePath =
            ScenesFolder + "/MultiCharacterMock.unity";
        private const string InputActionsPath = "Assets/InputSystem_Actions.inputactions";
        private const float SinglePanelWidth = 560f;
        private const float MultiPanelWidth = 440f;

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
                SinglePanelWidth);
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
                MultiPanelWidth);
            CreateConfiguredNpc(
                guardProfile,
                new Vector3(1.7f, 0f, 0f),
                "Guard",
                false,
                MultiPanelWidth);
            CreateInputSystemEventSystem();

            SaveGeneratedScene(
                scene,
                MultiCharacterScenePath,
                "Created multi-character mock prototype");
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
                string.Empty);
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
                "Luna");
            RepairNpcConfiguration(
                guardProfile,
                "Mock NPC - Guard",
                "Guard");
            SaveGeneratedScene(
                scene,
                MultiCharacterScenePath,
                "Repaired multi-character mock prototype");
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
        private static GameObject CreateNpc(string displayName, Vector3 position)
        {
            var npc = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            npc.name = $"Mock NPC - {displayName}";
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
            float panelWidth)
        {
            var npc = CreateNpc(profile.DisplayName, position);
            var presentationDriver = npc.GetComponent<NpcTextPresentationDriver>();
            var conversationBehaviour = npc.GetComponent<NpcConversationBehaviour>();
            var ui = CreateUserInterface(
                profile.DisplayName,
                objectSuffix,
                alignPanelLeft,
                panelWidth);

            ConfigurePresentationDriver(
                presentationDriver,
                ui,
                npc.GetComponent<Renderer>(),
                npc.transform);
            ConfigureConversationBehaviour(
                conversationBehaviour,
                profile,
                presentationDriver);
            ConfigureInputView(
                ui.InputView,
                ui.InputField,
                ui.SendButton,
                conversationBehaviour);
        }

        /// <summary>
        /// Creates a screen-space uGUI panel with all required input and output controls.
        /// </summary>
        private static PrototypeUiReferences CreateUserInterface(
            string displayName,
            string objectSuffix,
            bool alignPanelLeft,
            float panelWidth)
        {
            var canvasObject = new GameObject(
                GetObjectName("Mock NPC Canvas", objectSuffix),
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

            var contentWidth = panelWidth - 40f;

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
                new Vector2(contentWidth, 170f));

            var emotion = CreateText(
                resources,
                panel.transform,
                GetObjectName("Emotion Output", objectSuffix),
                "감정: Neutral",
                20,
                TextAnchor.MiddleLeft,
                new Vector2(20f, -266f),
                new Vector2(contentWidth, 38f));

            var gesture = CreateText(
                resources,
                panel.transform,
                GetObjectName("Gesture Output", objectSuffix),
                "제스처: None",
                20,
                TextAnchor.MiddleLeft,
                new Vector2(20f, -310f),
                new Vector2(contentWidth, 38f));

            var status = CreateText(
                resources,
                panel.transform,
                GetObjectName("Request Status", objectSuffix),
                "상태: 준비",
                18,
                TextAnchor.MiddleLeft,
                new Vector2(20f, -354f),
                new Vector2(contentWidth, 38f));
            status.color = new Color(0.75f, 0.8f, 0.9f);

            var hint = CreateText(
                resources,
                panel.transform,
                GetObjectName("Input Hint", objectSuffix),
                "Try: 안녕 / 고마워 / 무엇을 좋아해?",
                16,
                TextAnchor.MiddleLeft,
                new Vector2(20f, -400f),
                new Vector2(contentWidth, 32f));
            hint.color = new Color(0.65f, 0.7f, 0.8f);

            var inputObject = DefaultControls.CreateInputField(resources);
            inputObject.name = GetObjectName("Player Input", objectSuffix);
            inputObject.transform.SetParent(panel.transform, false);
            SetTopLeftRect(
                inputObject.GetComponent<RectTransform>(),
                new Vector2(20f, -452f),
                new Vector2(panelWidth - 175f, 64f));

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
                new Vector2(panelWidth - 140f, -452f),
                new Vector2(120f, 64f));

            var sendButton = buttonObject.GetComponent<Button>();
            var buttonLabel = buttonObject.GetComponentInChildren<Text>();
            buttonLabel.text = "전송";
            buttonLabel.fontSize = 20;
            buttonLabel.fontStyle = FontStyle.Bold;

            var instructions = CreateText(
                resources,
                panel.transform,
                GetObjectName("Verification Instructions", objectSuffix),
                "응답 후 NPC 색상은 감정, 기울기는 제스처를 표시합니다.",
                15,
                TextAnchor.UpperLeft,
                new Vector2(20f, -532f),
                new Vector2(contentWidth, 58f));
            instructions.color = new Color(0.65f, 0.7f, 0.8f);

            var inputView = panel.AddComponent<NpcTextInputView>();
            return new PrototypeUiReferences
            {
                DialogueText = dialogue,
                EmotionText = emotion,
                GestureText = gesture,
                StatusText = status,
                InputField = inputField,
                SendButton = sendButton,
                InputView = inputView
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
            string objectSuffix)
        {
            var npc = FindRequiredGameObject(npcObjectName);
            var presentationDriver = npc.GetComponent<NpcTextPresentationDriver>();
            var conversationBehaviour = npc.GetComponent<NpcConversationBehaviour>();
            if (presentationDriver == null || conversationBehaviour == null)
            {
                throw new InvalidOperationException(
                    $"Generated NPC '{npcObjectName}' is missing required runtime components.");
            }

            var ui = FindUiReferences(objectSuffix);
            ConfigurePresentationDriver(
                presentationDriver,
                ui,
                npc.GetComponent<Renderer>(),
                npc.transform);
            ConfigureConversationBehaviour(
                conversationBehaviour,
                profile,
                presentationDriver);
            ConfigureInputView(
                ui.InputView,
                ui.InputField,
                ui.SendButton,
                conversationBehaviour);
        }

        /// <summary>
        /// Finds one generated UI set by its optional Phase 2 character suffix.
        /// </summary>
        private static PrototypeUiReferences FindUiReferences(string objectSuffix)
        {
            return new PrototypeUiReferences
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
            SetObjectReference(serializedDriver, "emotionRenderer", npcRenderer);
            SetObjectReference(serializedDriver, "gestureTarget", npcTransform);
            serializedDriver.ApplyModifiedPropertiesWithoutUndo();
        }

        /// <summary>
        /// Wires the profile and presentation interface source into the Unity bridge.
        /// </summary>
        private static void ConfigureConversationBehaviour(
            NpcConversationBehaviour conversationBehaviour,
            CharacterProfile profile,
            NpcTextPresentationDriver presentationDriver)
        {
            var serializedBehaviour = new SerializedObject(conversationBehaviour);
            SetObjectReference(serializedBehaviour, "characterProfile", profile);
            SetObjectReference(
                serializedBehaviour,
                "presentationDriverSource",
                presentationDriver);
            serializedBehaviour.ApplyModifiedPropertiesWithoutUndo();
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
            public NpcTextInputView InputView;
        }
    }
}
