using AiCharacterKit.Core;
using AiCharacterKit.Editor;
using AiCharacterKit.Samples.Grounding;
using AiCharacterKit.Unity;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace AiCharacterKit.Samples.Grounding.Editor
{
    /// <summary>
    /// Generates the writable V4 Guard grounding sample without hand-authored Scene YAML.
    /// </summary>
    public static class GroundedNpcSampleBuilder
    {
        private static string SampleRoot =>
            AiCharacterKitSamplePaths.Resolve("GroundedNpc");
        private static string ProfilesFolder => SampleRoot + "/Profiles";
        private static string ScenesFolder => SampleRoot + "/Scenes";
        private static string CharacterPath => ProfilesFolder + "/GroundedGuard.asset";
        private static string LorePath => ProfilesFolder + "/DawnfallLore.asset";
        private static string ScenePath => ScenesFolder + "/GroundedNpcPrototype.unity";

        /// <summary>
        /// Creates or refreshes the imported sample after offering to save current scenes.
        /// </summary>
        [MenuItem("Tools/AI Character Kit/Samples/Create Grounded Guard Prototype")]
        public static void Create()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                return;
            }

            CreateBatch();
            EditorUtility.DisplayDialog(
                "Grounded Guard Prototype",
                "Created the V4 grounding sample at:\n" + ScenePath,
                "OK");
        }

        /// <summary>
        /// Creates the grounded sample without prompts for batch validation.
        /// </summary>
        public static void CreateBatch()
        {
            EnsureFolder(ProfilesFolder);
            EnsureFolder(ScenesFolder);
            var character = CreateOrUpdateCharacterProfile();
            var lore = CreateOrUpdateLoreProfile();
            CreateScene(character, lore);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        }

        /// <summary>
        /// Creates or updates only the sample-owned Guard identity and canon fields.
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
            serialized.FindProperty("characterId").stringValue = "sample-grounded-guard";
            serialized.FindProperty("displayName").stringValue = "Gate Guard";
            serialized.FindProperty("personality").stringValue =
                "Disciplined, observant, and protective of Dawnfall.";
            serialized.FindProperty("speechStyle").stringValue =
                "Formal, concise, and calm unless the town alarm is active.";
            serialized.FindProperty("exampleDialogue").stringValue =
                "State your business at the western gate.";
            serialized.FindProperty("defaultEmotion").enumValueIndex =
                (int)NpcEmotion.Neutral;
            serialized.FindProperty("background").stringValue =
                "You are the appointed guard of Dawnfall's western gate.";
            serialized.FindProperty("goalsAndValues").stringValue =
                "Protect residents, uphold lawful entry rules, and avoid needless conflict.";
            SetStringArray(
                serialized.FindProperty("behavioralRules"),
                new[]
                {
                    "Never claim the gate is open when the current observation says it is closed.",
                    "Never invent permits, keys, or authority that are not supplied in context."
                });
            SetStringArray(
                serialized.FindProperty("additionalDialogueExamples"),
                new[] { "Gate Guard: The gate remains closed until the alarm ends." });
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(profile);
            return profile;
        }

        /// <summary>
        /// Creates or updates the reusable Dawnfall lore and Guard belief asset.
        /// </summary>
        private static NpcLoreProfile CreateOrUpdateLoreProfile()
        {
            var profile = AssetDatabase.LoadAssetAtPath<NpcLoreProfile>(LorePath);
            if (profile == null)
            {
                profile = ScriptableObject.CreateInstance<NpcLoreProfile>();
                AssetDatabase.CreateAsset(profile, LorePath);
            }

            var serialized = new SerializedObject(profile);
            var lore = serialized.FindProperty("loreFacts");
            lore.arraySize = 2;
            SetLoreEntry(
                lore.GetArrayElementAtIndex(0),
                "city_name",
                "The fortified town is named Dawnfall.",
                60);
            SetLoreEntry(
                lore.GetArrayElementAtIndex(1),
                "western_gate_role",
                "The western gate is the main entrance used by travelers.",
                50);
            var beliefs = serialized.FindProperty("beliefs");
            beliefs.arraySize = 1;
            SetLoreEntry(
                beliefs.GetArrayElementAtIndex(0),
                "guard_duty_belief",
                "A careful question is safer than an immediate accusation.",
                40);
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(profile);
            return profile;
        }

        /// <summary>
        /// Builds and saves the V4 scene, UI, provider, and package adapters through Editor APIs.
        /// </summary>
        private static void CreateScene(
            CharacterProfile character,
            NpcLoreProfile lore)
        {
            var scene = EditorSceneManager.NewScene(
                NewSceneSetup.EmptyScene,
                NewSceneMode.Single);
            var npc = new GameObject("Grounded Gate Guard");
            var visual = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            visual.name = "Guard Visual";
            visual.transform.SetParent(npc.transform, false);

            var canvas = CreateCanvas();
            var dialogue = CreateText(canvas.transform, "Dialogue", new Vector2(0f, 200f));
            var emotion = CreateText(canvas.transform, "Emotion", new Vector2(0f, 160f));
            var gesture = CreateText(canvas.transform, "Gesture", new Vector2(0f, 125f));
            var status = CreateText(canvas.transform, "Status", new Vector2(0f, 90f));
            var contextStatus = CreateText(
                canvas.transform,
                "Captured Context",
                new Vector2(0f, 50f));
            contextStatus.text = "Captured context appears after each Send.";
            var gateToggle = CreateToggle(
                canvas.transform,
                "Gate Open",
                new Vector2(-160f, 5f),
                false);
            var alarmToggle = CreateToggle(
                canvas.transform,
                "Town Alarm",
                new Vector2(160f, 5f),
                false);
            var input = CreateInput(canvas.transform);
            var send = CreateButton(canvas.transform, "Send", new Vector2(190f, -65f));
            var reset = CreateButton(canvas.transform, "Reset", new Vector2(300f, -65f));
            var memoryStatus = CreateText(
                canvas.transform,
                "Session Status",
                new Vector2(0f, -115f));

            var presentation = npc.AddComponent<NpcTextPresentationDriver>();
            var serializedPresentation = new SerializedObject(presentation);
            serializedPresentation.FindProperty("dialogueText").objectReferenceValue = dialogue;
            serializedPresentation.FindProperty("emotionText").objectReferenceValue = emotion;
            serializedPresentation.FindProperty("gestureText").objectReferenceValue = gesture;
            serializedPresentation.FindProperty("statusText").objectReferenceValue = status;
            serializedPresentation.FindProperty("sendButton").objectReferenceValue = send;
            serializedPresentation.FindProperty("resetButton").objectReferenceValue = reset;
            serializedPresentation.FindProperty("emotionRenderer").objectReferenceValue =
                visual.GetComponent<Renderer>();
            serializedPresentation.FindProperty("gestureTarget").objectReferenceValue =
                visual.transform;
            serializedPresentation.ApplyModifiedPropertiesWithoutUndo();

            var provider = npc.AddComponent<SampleGuardContextProvider>();
            var serializedProvider = new SerializedObject(provider);
            serializedProvider.FindProperty("gateOpenToggle").objectReferenceValue = gateToggle;
            serializedProvider.FindProperty("townAlarmToggle").objectReferenceValue = alarmToggle;
            serializedProvider.FindProperty("contextStatusText").objectReferenceValue =
                contextStatus;
            serializedProvider.ApplyModifiedPropertiesWithoutUndo();

            var contextCoordinator = npc.AddComponent<NpcContextCoordinator>();
            var serializedContext = new SerializedObject(contextCoordinator);
            var loreProfiles = serializedContext.FindProperty("loreProfiles");
            loreProfiles.arraySize = 1;
            loreProfiles.GetArrayElementAtIndex(0).objectReferenceValue = lore;
            var providers = serializedContext.FindProperty("contextProviderSources");
            providers.arraySize = 1;
            providers.GetArrayElementAtIndex(0).objectReferenceValue = provider;
            serializedContext.ApplyModifiedPropertiesWithoutUndo();

            var conversation = npc.AddComponent<NpcConversationBehaviour>();
            var serializedConversation = new SerializedObject(conversation);
            serializedConversation.FindProperty("characterProfile").objectReferenceValue =
                character;
            serializedConversation.FindProperty("presentationDriverSource").objectReferenceValue =
                presentation;
            serializedConversation.FindProperty("conversationMode").intValue =
                (int)NpcConversationMode.BackendContext;
            serializedConversation.FindProperty("contextCoordinator").objectReferenceValue =
                contextCoordinator;
            serializedConversation.ApplyModifiedPropertiesWithoutUndo();

            serializedProvider.Update();
            serializedProvider.FindProperty("conversationBehaviour").objectReferenceValue =
                conversation;
            serializedProvider.ApplyModifiedPropertiesWithoutUndo();

            var inputView = canvas.gameObject.AddComponent<NpcTextInputView>();
            var serializedInput = new SerializedObject(inputView);
            serializedInput.FindProperty("inputField").objectReferenceValue = input;
            serializedInput.FindProperty("sendButton").objectReferenceValue = send;
            serializedInput.FindProperty("conversationBehaviour").objectReferenceValue =
                conversation;
            serializedInput.ApplyModifiedPropertiesWithoutUndo();

            var sessionView = canvas.gameObject.AddComponent<NpcSessionControlView>();
            var serializedSession = new SerializedObject(sessionView);
            serializedSession.FindProperty("conversationBehaviour").objectReferenceValue =
                conversation;
            serializedSession.FindProperty("resetButton").objectReferenceValue = reset;
            serializedSession.FindProperty("memoryStatusText").objectReferenceValue =
                memoryStatus;
            serializedSession.ApplyModifiedPropertiesWithoutUndo();

            UiEventSystemFactory.EnsureCompatibleEventSystem();
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, ScenePath);
        }

        /// <summary>
        /// Writes one sample lore entry through Unity serialization.
        /// </summary>
        private static void SetLoreEntry(
            SerializedProperty entry,
            string factId,
            string statement,
            int priority)
        {
            entry.FindPropertyRelative("factId").stringValue = factId;
            entry.FindPropertyRelative("statement").stringValue = statement;
            entry.FindPropertyRelative("priority").intValue = priority;
        }

        /// <summary>
        /// Writes one sample string collection through Unity serialization.
        /// </summary>
        private static void SetStringArray(
            SerializedProperty property,
            string[] values)
        {
            property.arraySize = values.Length;
            for (var index = 0; index < values.Length; index++)
            {
                property.GetArrayElementAtIndex(index).stringValue = values[index];
            }
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
            rect.sizeDelta = new Vector2(680f, 34f);
            return value;
        }

        /// <summary>
        /// Creates one state toggle used by the sample context provider.
        /// </summary>
        private static Toggle CreateToggle(
            Transform parent,
            string label,
            Vector2 position,
            bool initialValue)
        {
            var resource = new DefaultControls.Resources();
            var toggle = DefaultControls.CreateToggle(resource).GetComponent<Toggle>();
            toggle.name = label;
            toggle.transform.SetParent(parent, false);
            toggle.isOn = initialValue;
            toggle.GetComponent<RectTransform>().anchoredPosition = position;
            var text = toggle.GetComponentInChildren<Text>();
            if (text != null)
            {
                text.text = label;
            }

            return toggle;
        }

        /// <summary>
        /// Creates the sample user input with a grounding-oriented prompt.
        /// </summary>
        private static InputField CreateInput(Transform parent)
        {
            var resource = new DefaultControls.Resources();
            var input = DefaultControls.CreateInputField(resource).GetComponent<InputField>();
            input.name = "User Input";
            input.transform.SetParent(parent, false);
            input.text = "Is the gate open, and is the town safe?";
            var rect = input.GetComponent<RectTransform>();
            rect.anchoredPosition = new Vector2(-90f, -65f);
            rect.sizeDelta = new Vector2(430f, 34f);
            return input;
        }

        /// <summary>
        /// Creates one labeled sample button at a fixed anchored position.
        /// </summary>
        private static Button CreateButton(
            Transform parent,
            string label,
            Vector2 position)
        {
            var resource = new DefaultControls.Resources();
            var button = DefaultControls.CreateButton(resource).GetComponent<Button>();
            button.name = label;
            button.transform.SetParent(parent, false);
            button.GetComponentInChildren<Text>().text = label;
            button.GetComponent<RectTransform>().anchoredPosition = position;
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
