using AiCharacterKit.Core;
using AiCharacterKit.Editor;
using AiCharacterKit.Unity;
using AiCharacterKit.Unity.Actions;
using AiCharacterKit.Unity.LocalInference;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace AiCharacterKit.Samples.Local.Editor
{
    /// <summary>Creates all local sample assets through Unity APIs, preserving existing asset GUIDs on regeneration.</summary>
    public static class LocalGroundedNpcSampleBuilder
    {
        public static string Root => AiCharacterKitSamplePaths.Resolve("LocalNpc");
        public static string ScenePath => Root + "/Scenes/LocalGroundedNpc.unity";
        public static string ProfilePath => Root + "/Profiles/LocalInference.asset";

        /// <summary>Generates the writable sample after allowing the author to save current scenes.</summary>
        [MenuItem("Tools/AI Character Kit/Samples/Create Local Grounded NPC")]
        public static void Create()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            CreateBatch();
            Selection.activeObject = AssetDatabase.LoadAssetAtPath<LocalInferenceProfile>(ProfilePath);
        }

        /// <summary>Generates two independent conversations sharing one gate, alarm and external process host.</summary>
        public static void CreateBatch()
        {
            EnsureFolder(Root + "/Profiles");
            EnsureFolder(Root + "/Scenes");
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var profile = Asset<LocalInferenceProfile>(ProfilePath);
            var host = new GameObject("Shared Local Inference");
            var runtime = host.AddComponent<LocalInferenceRuntime>();
            var serializedHost = new SerializedObject(runtime);
            serializedHost.FindProperty("profile").objectReferenceValue = profile;
            serializedHost.ApplyModifiedPropertiesWithoutUndo();
            host.AddComponent<LocalGroundedGate>();
            host.AddComponent<LocalGroundedNpcPanel>();
            CreateNpc(host.transform, "Mira", "local_mira", "Careful and formal.");
            CreateNpc(host.transform, "Toma", "local_toma", "Friendly and cheerful.");
            var camera = new GameObject("Camera").AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(.08f, .11f, .15f);
            EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.SaveAssets();
        }

        /// <summary>Creates character data, a live provider and a consumer-authorized gate action for one NPC.</summary>
        private static void CreateNpc(Transform parent, string displayName, string id, string personality)
        {
            var profile = Asset<CharacterProfile>(Root + "/Profiles/" + displayName + ".asset");
            var data = new SerializedObject(profile);
            data.FindProperty("characterId").stringValue = id;
            data.FindProperty("displayName").stringValue = displayName;
            data.FindProperty("personality").stringValue = personality;
            data.FindProperty("speechStyle").stringValue = "Reply in the user's language, with one or two short sentences.";
            data.FindProperty("exampleDialogue").stringValue = "안녕하세요. 문을 지키고 있습니다.";
            data.FindProperty("background").stringValue = "You guard the gate of Dawnfall. Use the supplied current observations.";
            data.FindProperty("goalsAndValues").stringValue = "Protect the town. Never open the gate while the alarm is active.";
            data.ApplyModifiedPropertiesWithoutUndo();
            var action = Asset<NpcActionProfile>(Root + "/Profiles/" + displayName + "Actions.asset");
            var actions = new SerializedObject(action);
            var bindings = actions.FindProperty("bindings");
            bindings.arraySize = 1;
            var binding = bindings.GetArrayElementAtIndex(0);
            binding.FindPropertyRelative("triggerId").stringValue = "request_open_gate";
            binding.FindPropertyRelative("conditionDescription").stringValue = "The user explicitly asks to open the gate. Do not match a question about its state.";
            binding.FindPropertyRelative("exampleUserText").stringValue = "문을 열어 주세요.";
            binding.FindPropertyRelative("actionId").stringValue = "open_gate";
            binding.FindPropertyRelative("priority").intValue = 50;
            actions.ApplyModifiedPropertiesWithoutUndo();
            var target = new GameObject(displayName);
            target.transform.SetParent(parent, false);
            var adapter = target.AddComponent<LocalGroundedNpc>();
            var context = target.AddComponent<NpcContextCoordinator>();
            var contextData = new SerializedObject(context);
            contextData.FindProperty("contextProviderSources").arraySize = 1;
            contextData.FindProperty("contextProviderSources").GetArrayElementAtIndex(0).objectReferenceValue = adapter;
            contextData.ApplyModifiedPropertiesWithoutUndo();
            var coordinator = target.AddComponent<NpcActionCoordinator>();
            var actionData = new SerializedObject(coordinator);
            actionData.FindProperty("actionProfile").objectReferenceValue = action;
            actionData.FindProperty("actionHandlerSources").arraySize = 1;
            actionData.FindProperty("actionHandlerSources").GetArrayElementAtIndex(0).objectReferenceValue = adapter;
            actionData.ApplyModifiedPropertiesWithoutUndo();
            var conversation = target.AddComponent<NpcConversationBehaviour>();
            var conversationData = new SerializedObject(conversation);
            conversationData.FindProperty("characterProfile").objectReferenceValue = profile;
            conversationData.FindProperty("presentationDriverSource").objectReferenceValue = adapter;
            conversationData.FindProperty("conversationMode").intValue = (int)NpcConversationMode.LocalInference;
            conversationData.FindProperty("contextCoordinator").objectReferenceValue = context;
            conversationData.FindProperty("actionCoordinator").objectReferenceValue = coordinator;
            conversationData.ApplyModifiedPropertiesWithoutUndo();
        }

        /// <summary>Reuses an existing sample asset or creates it without touching serialized YAML.</summary>
        private static T Asset<T>(string path) where T : ScriptableObject
        {
            var asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset == null) { asset = ScriptableObject.CreateInstance<T>(); AssetDatabase.CreateAsset(asset, path); }
            return asset;
        }

        /// <summary>Creates only missing writable sample directories using the asset database.</summary>
        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            var index = path.LastIndexOf('/');
            EnsureFolder(path.Substring(0, index));
            AssetDatabase.CreateFolder(path.Substring(0, index), path.Substring(index + 1));
        }
    }
}
