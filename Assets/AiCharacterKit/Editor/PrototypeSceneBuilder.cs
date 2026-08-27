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
    /// Creates the Phase 1 profile and Play Mode scene through supported Unity Editor APIs.
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
        private const string InputActionsPath = "Assets/InputSystem_Actions.inputactions";

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
        /// Creates folders, profile data, scene objects, UI, and serialized component wiring.
        /// </summary>
        private static void CreatePrototypeSceneInternal()
        {
            EnsureFolder(RootFolder);
            EnsureFolder(SamplesFolder);
            EnsureFolder(MockNpcFolder);
            EnsureFolder(ProfilesFolder);
            EnsureFolder(ScenesFolder);

            CreateOrLoadProfile();
            var scene = EditorSceneManager.NewScene(
                NewSceneSetup.DefaultGameObjects,
                NewSceneMode.Single);
            var profile = AssetDatabase.LoadAssetAtPath<CharacterProfile>(ProfilePath);
            if (profile == null)
            {
                throw new InvalidOperationException(
                    $"Prototype profile was not found at {ProfilePath}.");
            }

            ConfigureDefaultSceneObjects();
            CreateGround();

            var npc = CreateNpc();
            var presentationDriver = npc.GetComponent<NpcTextPresentationDriver>();
            var conversationBehaviour = npc.GetComponent<NpcConversationBehaviour>();
            var ui = CreateUserInterface(profile.DisplayName);

            ConfigurePresentationDriver(
                presentationDriver,
                ui,
                npc.GetComponent<Renderer>(),
                npc.transform);
            ConfigureConversationBehaviour(
                conversationBehaviour,
                profile,
                presentationDriver);
            ConfigureInputView(ui.InputView, ui.InputField, ui.SendButton, conversationBehaviour);
            CreateInputSystemEventSystem();

            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene, ScenePath))
            {
                throw new InvalidOperationException(
                    $"Failed to save prototype scene at {ScenePath}.");
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            var sceneAsset = AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath);
            Selection.activeObject = sceneAsset;
            Debug.Log($"Created Mock NPC prototype at {ScenePath}.");
        }

        /// <summary>
        /// Creates the profile once and reuses it on later safe builder runs.
        /// </summary>
        private static CharacterProfile CreateOrLoadProfile()
        {
            var existingProfile = AssetDatabase.LoadAssetAtPath<CharacterProfile>(ProfilePath);
            if (existingProfile != null)
            {
                return existingProfile;
            }

            var profile = ScriptableObject.CreateInstance<CharacterProfile>();
            profile.name = "Prototype Character";

            var serializedProfile = new SerializedObject(profile);
            serializedProfile.FindProperty("characterId").stringValue = "prototype-mina";
            serializedProfile.FindProperty("displayName").stringValue = "Mina";
            serializedProfile.FindProperty("personality").stringValue =
                "Friendly, observant, and eager to help.";
            serializedProfile.FindProperty("speechStyle").stringValue =
                "Uses short, warm, and polite sentences.";
            serializedProfile.FindProperty("exampleDialogue").stringValue =
                "오늘은 무엇을 도와드릴까요?";
            serializedProfile.FindProperty("defaultEmotion").enumValueIndex =
                (int)NpcEmotion.Neutral;
            serializedProfile.ApplyModifiedPropertiesWithoutUndo();

            AssetDatabase.CreateAsset(profile, ProfilePath);
            AssetDatabase.SaveAssets();
            AssetDatabase.ImportAsset(ProfilePath, ImportAssetOptions.ForceSynchronousImport);

            var savedProfile = AssetDatabase.LoadAssetAtPath<CharacterProfile>(ProfilePath);
            if (savedProfile == null)
            {
                throw new InvalidOperationException(
                    $"Failed to reload the created profile at {ProfilePath}.");
            }

            return savedProfile;
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

            var npc = GameObject.Find("Mock NPC - Mina");
            var inputObject = GameObject.Find("Player Input");
            var buttonObject = GameObject.Find("Send Button");

            if (npc == null || inputObject == null || buttonObject == null)
            {
                throw new InvalidOperationException(
                    "The existing prototype scene is missing required generated objects.");
            }

            var presentationDriver = npc.GetComponent<NpcTextPresentationDriver>();
            var conversationBehaviour = npc.GetComponent<NpcConversationBehaviour>();
            var inputView = UnityEngine.Object.FindAnyObjectByType<NpcTextInputView>();
            var inputField = inputObject.GetComponent<InputField>();
            var sendButton = buttonObject.GetComponent<Button>();

            if (presentationDriver == null
                || conversationBehaviour == null
                || inputView == null
                || inputField == null
                || sendButton == null)
            {
                throw new InvalidOperationException(
                    "The existing prototype scene is missing required generated components.");
            }

            ConfigureConversationBehaviour(
                conversationBehaviour,
                profile,
                presentationDriver);
            ConfigureInputView(
                inputView,
                inputField,
                sendButton,
                conversationBehaviour);

            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene, ScenePath))
            {
                throw new InvalidOperationException(
                    $"Failed to repair prototype scene at {ScenePath}.");
            }

            AssetDatabase.SaveAssets();
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
        /// Creates the Capsule NPC and adds its runtime bridge and presentation adapter.
        /// </summary>
        private static GameObject CreateNpc()
        {
            var npc = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            npc.name = "Mock NPC - Mina";
            npc.transform.position = Vector3.zero;
            npc.AddComponent<NpcTextPresentationDriver>();
            npc.AddComponent<NpcConversationBehaviour>();
            return npc;
        }

        /// <summary>
        /// Creates a screen-space uGUI panel with all required input and output controls.
        /// </summary>
        private static PrototypeUiReferences CreateUserInterface(string displayName)
        {
            var canvasObject = new GameObject(
                "Mock NPC Canvas",
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
                "Conversation Panel",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image));
            panel.transform.SetParent(canvasObject.transform, false);

            var panelImage = panel.GetComponent<Image>();
            panelImage.color = new Color(0.04f, 0.055f, 0.08f, 0.94f);
            SetTopRightRect(
                panel.GetComponent<RectTransform>(),
                new Vector2(-24f, -24f),
                new Vector2(560f, 610f));

            var resources = new DefaultControls.Resources();
            var title = CreateText(
                resources,
                panel.transform,
                "Character Name",
                displayName,
                28,
                TextAnchor.MiddleLeft,
                new Vector2(20f, -20f),
                new Vector2(520f, 42f));
            title.fontStyle = FontStyle.Bold;
            title.color = new Color(0.55f, 0.85f, 1f);

            var dialogue = CreateText(
                resources,
                panel.transform,
                "Dialogue Output",
                "대화 출력",
                20,
                TextAnchor.UpperLeft,
                new Vector2(20f, -78f),
                new Vector2(520f, 170f));

            var emotion = CreateText(
                resources,
                panel.transform,
                "Emotion Output",
                "감정: Neutral",
                20,
                TextAnchor.MiddleLeft,
                new Vector2(20f, -266f),
                new Vector2(520f, 38f));

            var gesture = CreateText(
                resources,
                panel.transform,
                "Gesture Output",
                "제스처: None",
                20,
                TextAnchor.MiddleLeft,
                new Vector2(20f, -310f),
                new Vector2(520f, 38f));

            var status = CreateText(
                resources,
                panel.transform,
                "Request Status",
                "상태: 준비",
                18,
                TextAnchor.MiddleLeft,
                new Vector2(20f, -354f),
                new Vector2(520f, 38f));
            status.color = new Color(0.75f, 0.8f, 0.9f);

            var hint = CreateText(
                resources,
                panel.transform,
                "Input Hint",
                "Try: 안녕 / 고마워 / 무엇을 좋아해?",
                16,
                TextAnchor.MiddleLeft,
                new Vector2(20f, -400f),
                new Vector2(520f, 32f));
            hint.color = new Color(0.65f, 0.7f, 0.8f);

            var inputObject = DefaultControls.CreateInputField(resources);
            inputObject.name = "Player Input";
            inputObject.transform.SetParent(panel.transform, false);
            SetTopLeftRect(
                inputObject.GetComponent<RectTransform>(),
                new Vector2(20f, -452f),
                new Vector2(385f, 64f));

            var inputField = inputObject.GetComponent<InputField>();
            inputField.lineType = InputField.LineType.SingleLine;
            inputField.characterLimit = 240;
            inputField.placeholder.GetComponent<Text>().text = "NPC에게 메시지 입력";
            inputField.textComponent.fontSize = 18;
            inputField.placeholder.GetComponent<Text>().fontSize = 18;

            var buttonObject = DefaultControls.CreateButton(resources);
            buttonObject.name = "Send Button";
            buttonObject.transform.SetParent(panel.transform, false);
            SetTopLeftRect(
                buttonObject.GetComponent<RectTransform>(),
                new Vector2(420f, -452f),
                new Vector2(120f, 64f));

            var sendButton = buttonObject.GetComponent<Button>();
            var buttonLabel = buttonObject.GetComponentInChildren<Text>();
            buttonLabel.text = "전송";
            buttonLabel.fontSize = 20;
            buttonLabel.fontStyle = FontStyle.Bold;

            var instructions = CreateText(
                resources,
                panel.transform,
                "Verification Instructions",
                "응답 후 NPC 색상은 감정, 기울기는 제스처를 표시합니다.",
                15,
                TextAnchor.UpperLeft,
                new Vector2(20f, -532f),
                new Vector2(520f, 58f));
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
