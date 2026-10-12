using System;
using System.Collections.Generic;
using AiCharacterKit.Behavior;
using AiCharacterKit.Core;
using AiCharacterKit.Editor;
using AiCharacterKit.Samples.Advanced;
using AiCharacterKit.Samples.Advanced.Editor;
using AiCharacterKit.Unity;
using AiCharacterKit.Unity.Actions;
using AiCharacterKit.Unity.Behavior;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace AiCharacterKit.Samples.Inference.Editor
{
    /// <summary>Creates a separate consumer-owned API comparison Scene without editing existing samples.</summary>
    public static class InferenceComparisonSampleBuilder
    {
        public static string Root => AiCharacterKitSamplePaths.Resolve("InferenceComparison");
        public static string ScenePath => Root + "/Scenes/InferenceComparison.unity";

        /// <summary>Protects unsaved work before creating the optional comparison Scene.</summary>
        [MenuItem("Tools/AI Character Kit/Samples/Create API Inference Comparison")]
        public static void Create()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            CreateBatch();
        }

        /// <summary>Offers this sample only when the source's API combination has completed qualification.</summary>
        [MenuItem("Tools/AI Character Kit/Samples/Create API Inference Comparison", true)]
        private static bool CanCreate() => NpcInferenceAvailability.DefaultApiTwoStage;

        /// <summary>Creates sample assets with Unity APIs, preserving GUIDs on subsequent generation.</summary>
        public static void CreateBatch()
        {
            if (!NpcInferenceAvailability.DefaultApiTwoStage)
                throw new InvalidOperationException("The API two-stage combination has not completed qualification.");
            EnsureFolder(Root + "/Profiles"); EnsureFolder(Root + "/Scenes");
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var host = new GameObject("Inference Comparison World");
            host.AddComponent<AdvancedRelationshipWorld>(); host.AddComponent<InferenceComparisonPanel>();
            var behavior = Asset<NpcBehaviorProfile>(Root + "/Profiles/ComparisonBehavior.asset");
            behavior.SetDefinition(CreateDefinition()); EditorUtility.SetDirty(behavior);
            CreateNpc(host.transform, "Mira", "advanced_mira", NpcInferenceStrategy.SinglePass, behavior);
            CreateNpc(host.transform, "Toma", "advanced_toma", NpcInferenceStrategy.ClassifyThenRespond, behavior);
            var camera = new GameObject("Camera").AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(.08f, .11f, .15f);
            EditorSceneManager.SaveScene(scene, ScenePath); AssetDatabase.SaveAssets();
        }

        /// <summary>Reuses Phase 12 policy with bounded authored examples; no held-out evaluation phrases are embedded.</summary>
        public static NpcBehaviorDefinition CreateDefinition()
        {
            var value = AdvancedRelationshipSampleBuilder.CreateDefinition();
            Configure(value.signals[0], "A direct compliment about this NPC in the latest message. Exclude recall, negated praise and descriptions of other people.",
                new[] { "당신은 참 다정해요.", "네 친절함이 마음에 들어." }, new[] { "다른 경비가 친절하더라.", "내가 다정하다고 말했었나?" });
            Configure(value.signals[1], "A direct insult about this NPC in the latest message. Exclude neutral questions, recall and descriptions of other people.",
                new[] { "당신은 참 무례해요.", "네 태도가 형편없어." }, new[] { "다른 경비가 무례했어.", "내가 말한 이름을 기억하니?" });
            Configure(value.signals[2], "An explicit greeting directed to this NPC now. Polite wording alone, quoted greetings and recall questions are not greetings.",
                new[] { "반가워요, 경비님.", "좋은 아침이에요!" }, new[] { "인사했던 사람을 기억해?", "부탁 좀 들어주세요." });
            Configure(value.signals[3], "A current request for this NPC to open the gate, regardless of game conditions. Include polite or repeated requests; exclude state questions, prohibitions, quotes and reports.",
                new[] { "성문을 열어 주시겠어요?", "성문을 열어라." }, new[] { "성문 상태가 어떤가요?", "성문을 열지 마세요.", "그가 성문을 열라고 말했다.", "성문을 열라는 문장을 인용했어요." });
            return value;
        }

        /// <summary>Stores author-owned examples separately so Builder can load and edit the same data.</summary>
        private static void Configure(NpcBehaviorSignal signal, string condition, string[] positive, string[] negative)
        {
            signal.conditionDescription = condition;
            signal.positiveExamples = new List<string>(positive); signal.negativeExamples = new List<string>(negative);
        }

        /// <summary>Connects one profile, deterministic behavior, providers and independently selected V4 strategy.</summary>
        private static void CreateNpc(Transform parent, string displayName, string id, NpcInferenceStrategy strategy, NpcBehaviorProfile behavior)
        {
            var profile = Asset<CharacterProfile>(Root + "/Profiles/" + displayName + ".asset");
            var fields = new SerializedObject(profile);
            fields.FindProperty("characterId").stringValue = id; fields.FindProperty("displayName").stringValue = displayName;
            fields.FindProperty("personality").stringValue = "A careful gatekeeper who appreciates sincere kindness.";
            fields.FindProperty("speechStyle").stringValue = "Reply in the user's language using one or two short sentences.";
            fields.FindProperty("exampleDialogue").stringValue = "안녕하세요. 이야기를 나누어 보세요.";
            fields.FindProperty("background").stringValue = "You guard a shared gate. Each NPC has its own affinity toward the player.";
            fields.FindProperty("goalsAndValues").stringValue = "Use current observations. Classify configured triggers from the current user message on every turn, even when repeated. Previous replies and game conditions must not suppress a matching intent. Unity separately checks affinity at least 60, permission and no alarm before opening the gate. Game code decides effects.";
            fields.ApplyModifiedPropertiesWithoutUndo();
            var target = new GameObject(displayName); target.transform.SetParent(parent, false);
            var adapter = target.AddComponent<AdvancedRelationshipNpc>(); var greeting = target.AddComponent<AdvancedGreetingHandler>();
            var actions = target.AddComponent<NpcActionCoordinator>(); var settings = new SerializedObject(actions);
            settings.FindProperty("behaviorMode").intValue = (int)NpcBehaviorMode.Advanced;
            settings.FindProperty("behaviorProfile").objectReferenceValue = behavior; settings.FindProperty("enableBehaviorTrace").boolValue = true;
            References(settings.FindProperty("actionHandlerSources"), adapter, greeting);
            References(settings.FindProperty("behaviorValueProviderSources"), adapter); settings.ApplyModifiedPropertiesWithoutUndo();
            var variables = target.AddComponent<NpcBehaviorContextProvider>(); var context = target.AddComponent<NpcContextCoordinator>();
            settings = new SerializedObject(context); References(settings.FindProperty("contextProviderSources"), adapter, variables); settings.ApplyModifiedPropertiesWithoutUndo();
            var conversation = target.AddComponent<NpcConversationBehaviour>(); settings = new SerializedObject(conversation);
            settings.FindProperty("characterProfile").objectReferenceValue = profile;
            settings.FindProperty("presentationDriverSource").objectReferenceValue = adapter;
            settings.FindProperty("conversationMode").intValue = (int)NpcConversationMode.BackendContext;
            settings.FindProperty("inferenceStrategy").intValue = (int)strategy;
            settings.FindProperty("actionCoordinator").objectReferenceValue = actions;
            settings.FindProperty("contextCoordinator").objectReferenceValue = context; settings.ApplyModifiedPropertiesWithoutUndo();
        }

        /// <summary>Writes explicit consumer component references through Unity serialization.</summary>
        private static void References(SerializedProperty field, params MonoBehaviour[] values)
        { field.arraySize = values.Length; for (int index = 0; index < values.Length; index++) field.GetArrayElementAtIndex(index).objectReferenceValue = values[index]; }

        /// <summary>Creates only missing consumer assets and reuses the identity of existing ones.</summary>
        private static T Asset<T>(string path) where T : ScriptableObject
        { var value = AssetDatabase.LoadAssetAtPath<T>(path); if (value == null) { value = ScriptableObject.CreateInstance<T>(); AssetDatabase.CreateAsset(value, path); } return value; }

        /// <summary>Creates missing consumer folders without editing asset YAML.</summary>
        private static void EnsureFolder(string path)
        { if (AssetDatabase.IsValidFolder(path)) return; int split = path.LastIndexOf('/'); EnsureFolder(path.Substring(0, split)); AssetDatabase.CreateFolder(path.Substring(0, split), path.Substring(split + 1)); }
    }
}
