using AiCharacterKit.Core;
using AiCharacterKit.Editor;
using AiCharacterKit.Samples.Actions;
using AiCharacterKit.Unity;
using AiCharacterKit.Unity.Actions;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace AiCharacterKit.Samples.Actions.Editor
{
    /// <summary>
    /// Generates the writable conversation-action sample without hand-authored Scene YAML.
    /// </summary>
    public static class ActionNpcSampleBuilder
    {
        /// <summary>
        /// Creates or refreshes the imported sample profiles and Mock action Scene.
        /// </summary>
        [MenuItem("Tools/AI Character Kit/Samples/Create Conversation Action Prototype")]
        public static void Create()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                return;
            }

            CreateBatch();
            EditorUtility.DisplayDialog(
                "Conversation Action Prototype",
                "Created the network-free action sample at:\n" + ScenePath,
                "OK");
        }

        /// <summary>
        /// Creates the action sample without prompts for batch validation.
        /// </summary>
        public static void CreateBatch()
        {
            EnsureFolder(ProfilesFolder);
            EnsureFolder(ScenesFolder);
            var character = CreateOrUpdateCharacterProfile();
            var actions = CreateOrUpdateActionProfile();
            CreateScene(character, actions);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        }

        private static string ActionRoot =>
            AiCharacterKitSamplePaths.Resolve("ActionNpc");
        private static string ProfilesFolder => ActionRoot + "/Profiles";
        private static string ScenesFolder => ActionRoot + "/Scenes";
        private static string CharacterPath => ProfilesFolder + "/ActionGuide.asset";
        private static string ActionProfilePath => ProfilesFolder + "/ActionGuideActions.asset";
        private static string ScenePath => ScenesFolder + "/ActionNpcPrototype.unity";

        /// <summary>
        /// Creates or overwrites only the sample-owned character profile fields.
        /// </summary>
        private static CharacterProfile CreateOrUpdateCharacterProfile()
        {
            var profile = AssetDatabase.LoadAssetAtPath<CharacterProfile>(CharacterPath);
            if (profile == null)
            {
                profile = ScriptableObject.CreateInstance<CharacterProfile>();
                AssetDatabase.CreateAsset(profile, CharacterPath);
            }

            var serialized = new SerializedObject(profile);
            serialized.FindProperty("characterId").stringValue = "sample-action-guide";
            serialized.FindProperty("displayName").stringValue = "Action Guide";
            serialized.FindProperty("personality").stringValue =
                "Helpful, cautious, and attentive to player requests.";
            serialized.FindProperty("speechStyle").stringValue =
                "Warm, concise, and practical.";
            serialized.FindProperty("exampleDialogue").stringValue =
                "Say hello, or ask me to open the gate.";
            serialized.FindProperty("defaultEmotion").enumValueIndex =
                (int)NpcEmotion.Neutral;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(profile);
            return profile;
        }

        /// <summary>
        /// Creates the two bounded trigger bindings used by the sample handlers.
        /// </summary>
        private static NpcActionProfile CreateOrUpdateActionProfile()
        {
            var profile = AssetDatabase.LoadAssetAtPath<NpcActionProfile>(ActionProfilePath);
            if (profile == null)
            {
                profile = ScriptableObject.CreateInstance<NpcActionProfile>();
                AssetDatabase.CreateAsset(profile, ActionProfilePath);
            }

            var serialized = new SerializedObject(profile);
            var bindings = serialized.FindProperty("bindings");
            bindings.arraySize = 2;
            ConfigureBinding(
                bindings.GetArrayElementAtIndex(0),
                "greet_player",
                "The player greets the character.",
                "hello",
                "wave_to_player",
                10);
            ConfigureBinding(
                bindings.GetArrayElementAtIndex(1),
                "request_open_gate",
                "The player asks the character to open the gate.",
                "open the gate",
                "open_gate",
                5);
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(profile);
            return profile;
        }

        /// <summary>
        /// Writes one sample binding through Unity serialization.
        /// </summary>
        private static void ConfigureBinding(
            SerializedProperty binding,
            string triggerId,
            string condition,
            string example,
            string actionId,
            int priority)
        {
            binding.FindPropertyRelative("triggerId").stringValue = triggerId;
            binding.FindPropertyRelative("conditionDescription").stringValue = condition;
            binding.FindPropertyRelative("exampleUserText").stringValue = example;
            binding.FindPropertyRelative("actionId").stringValue = actionId;
            binding.FindPropertyRelative("priority").intValue = priority;
        }

        /// <summary>
        /// Builds and saves one network-free action sample Scene through Editor APIs.
        /// </summary>
        private static void CreateScene(
            CharacterProfile character,
            NpcActionProfile actions)
        {
            var scene = EditorSceneManager.NewScene(
                NewSceneSetup.EmptyScene,
                NewSceneMode.Single);
            var npc = new GameObject("Action Guide NPC");
            var visual = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            visual.name = "Visual";
            visual.transform.SetParent(npc.transform, false);
            var gate = GameObject.CreatePrimitive(PrimitiveType.Cube);
            gate.name = "Locked Gate Indicator";
            gate.transform.position = new Vector3(2f, 0f, 0f);

            var canvas = CreateCanvas();
            var dialogue = CreateText(canvas.transform, "Dialogue", new Vector2(0f, 170f));
            var emotion = CreateText(canvas.transform, "Emotion", new Vector2(0f, 125f));
            var gesture = CreateText(canvas.transform, "Gesture", new Vector2(0f, 85f));
            var status = CreateText(canvas.transform, "Status", new Vector2(0f, 45f));
            var actionStatus = CreateText(canvas.transform, "Action Status", new Vector2(0f, 5f));
            actionStatus.text = "Action: none (gate starts locked)";
            var input = CreateInput(canvas.transform);
            var send = CreateButton(canvas.transform);

            var presentation = npc.AddComponent<NpcTextPresentationDriver>();
            var serializedPresentation = new SerializedObject(presentation);
            serializedPresentation.FindProperty("dialogueText").objectReferenceValue = dialogue;
            serializedPresentation.FindProperty("emotionText").objectReferenceValue = emotion;
            serializedPresentation.FindProperty("gestureText").objectReferenceValue = gesture;
            serializedPresentation.FindProperty("statusText").objectReferenceValue = status;
            serializedPresentation.FindProperty("sendButton").objectReferenceValue = send;
            serializedPresentation.FindProperty("emotionRenderer").objectReferenceValue =
                visual.GetComponent<Renderer>();
            serializedPresentation.FindProperty("gestureTarget").objectReferenceValue =
                visual.transform;
            serializedPresentation.ApplyModifiedPropertiesWithoutUndo();

            var wave = npc.AddComponent<SampleWaveActionHandler>();
            var serializedWave = new SerializedObject(wave);
            serializedWave.FindProperty("actionTarget").objectReferenceValue = visual.transform;
            serializedWave.FindProperty("actionStatusText").objectReferenceValue = actionStatus;
            serializedWave.ApplyModifiedPropertiesWithoutUndo();
            var guarded = npc.AddComponent<SampleGuardedActionHandler>();
            var serializedGuarded = new SerializedObject(guarded);
            serializedGuarded.FindProperty("gateIndicator").objectReferenceValue = gate;
            serializedGuarded.FindProperty("actionStatusText").objectReferenceValue = actionStatus;
            serializedGuarded.ApplyModifiedPropertiesWithoutUndo();

            var coordinator = npc.AddComponent<NpcActionCoordinator>();
            var serializedCoordinator = new SerializedObject(coordinator);
            serializedCoordinator.FindProperty("actionProfile").objectReferenceValue = actions;
            var handlers = serializedCoordinator.FindProperty("actionHandlerSources");
            handlers.arraySize = 2;
            handlers.GetArrayElementAtIndex(0).objectReferenceValue = wave;
            handlers.GetArrayElementAtIndex(1).objectReferenceValue = guarded;
            serializedCoordinator.ApplyModifiedPropertiesWithoutUndo();

            var conversation = npc.AddComponent<NpcConversationBehaviour>();
            var serializedConversation = new SerializedObject(conversation);
            serializedConversation.FindProperty("characterProfile").objectReferenceValue = character;
            serializedConversation.FindProperty("presentationDriverSource").objectReferenceValue =
                presentation;
            serializedConversation.FindProperty("conversationMode").intValue =
                (int)NpcConversationMode.Mock;
            serializedConversation.FindProperty("actionCoordinator").objectReferenceValue =
                coordinator;
            serializedConversation.ApplyModifiedPropertiesWithoutUndo();

            var inputView = canvas.gameObject.AddComponent<NpcTextInputView>();
            var serializedInput = new SerializedObject(inputView);
            serializedInput.FindProperty("inputField").objectReferenceValue = input;
            serializedInput.FindProperty("sendButton").objectReferenceValue = send;
            serializedInput.FindProperty("conversationBehaviour").objectReferenceValue =
                conversation;
            serializedInput.ApplyModifiedPropertiesWithoutUndo();
            UiEventSystemFactory.EnsureCompatibleEventSystem();
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, ScenePath);
        }

        /// <summary>
        /// Creates one screen-space Canvas for the sample controls.
        /// </summary>
        private static Canvas CreateCanvas()
        {
            var canvasObject = new GameObject(
                "Canvas",
                typeof(Canvas),
                typeof(CanvasScaler),
                typeof(GraphicRaycaster));
            var canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            return canvas;
        }

        /// <summary>
        /// Creates one anchored sample text label.
        /// </summary>
        private static Text CreateText(
            Transform parent,
            string name,
            Vector2 position)
        {
            var resource = new DefaultControls.Resources();
            var value = DefaultControls.CreateText(resource).GetComponent<Text>();
            value.name = name;
            value.text = name;
            value.transform.SetParent(parent, false);
            var rect = value.rectTransform;
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = new Vector2(620f, 36f);
            return value;
        }

        /// <summary>
        /// Creates the reviewed user text input used by the sample.
        /// </summary>
        private static InputField CreateInput(Transform parent)
        {
            var resource = new DefaultControls.Resources();
            var input = DefaultControls.CreateInputField(resource).GetComponent<InputField>();
            input.name = "User Input";
            input.transform.SetParent(parent, false);
            input.text = "hello";
            input.GetComponent<RectTransform>().anchoredPosition = new Vector2(-80f, -65f);
            return input;
        }

        /// <summary>
        /// Creates the sample send button beside its text input.
        /// </summary>
        private static Button CreateButton(Transform parent)
        {
            var resource = new DefaultControls.Resources();
            var button = DefaultControls.CreateButton(resource).GetComponent<Button>();
            button.name = "Send";
            button.transform.SetParent(parent, false);
            button.GetComponentInChildren<Text>().text = "Send";
            button.GetComponent<RectTransform>().anchoredPosition = new Vector2(230f, -65f);
            return button;
        }

        /// <summary>
        /// Creates every missing segment of one Assets-relative sample folder.
        /// </summary>
        private static void EnsureFolder(string folder)
        {
            var parts = folder.Split('/');
            var current = parts[0];
            for (var index = 1; index < parts.Length; index++)
            {
                var next = current + "/" + parts[index];
                if (!AssetDatabase.IsValidFolder(next))
                {
                    AssetDatabase.CreateFolder(current, parts[index]);
                }

                current = next;
            }
        }
    }
}
