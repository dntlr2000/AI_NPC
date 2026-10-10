using System.Collections.Generic;
using AiCharacterKit.Behavior;
using AiCharacterKit.Editor;
using AiCharacterKit.Unity;
using AiCharacterKit.Unity.Actions;
using AiCharacterKit.Unity.Behavior;
using AiCharacterKit.Unity.LocalInference;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace AiCharacterKit.Samples.Advanced.Editor
{
    /// <summary>Creates consumer-owned Advanced examples through Unity APIs, preserving existing GUIDs.</summary>
    public static class AdvancedRelationshipSampleBuilder
    {
        public static string Root => AiCharacterKitSamplePaths.Resolve("AdvancedNpc");
        public static string ScenePath => Root + "/Scenes/AdvancedRelationshipNpc.unity";
        public static string LocalProfilePath => Root + "/Profiles/LocalInference.asset";
        /// <summary>Creates the free Mock example after offering to save the user's current Scene.</summary>
        [MenuItem("Tools/AI Character Kit/Samples/Create Advanced Relationship NPC")]
        public static void Create()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            CreateBatch();
        }
        /// <summary>Creates the default Mock sample without starting any inference process.</summary>
        public static void CreateBatch() => CreateForMode(NpcConversationMode.Mock);
        /// <summary>Creates the same rules and consumer effects for Mock, V3, V4 or an explicitly configured local host.</summary>
        public static void CreateForMode(NpcConversationMode mode)
        {
            EnsureFolder(Root + "/Profiles"); EnsureFolder(Root + "/Scenes");
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var host = new GameObject("Advanced Relationship World"); host.AddComponent<AdvancedRelationshipWorld>();
            if (mode == NpcConversationMode.LocalInference)
            {
                var runtime = host.AddComponent<LocalInferenceRuntime>();
                var serialized = new SerializedObject(runtime);
                serialized.FindProperty("profile").objectReferenceValue = Asset<LocalInferenceProfile>(LocalProfilePath);
                serialized.ApplyModifiedPropertiesWithoutUndo();
            }
            var behavior = Asset<NpcBehaviorProfile>(Root + "/Profiles/RelationshipBehavior.asset");
            behavior.SetDefinition(CreateDefinition()); EditorUtility.SetDirty(behavior);
            CreateNpc(host.transform, "Mira", "advanced_mira", mode, behavior);
            CreateNpc(host.transform, "Toma", "advanced_toma", mode, behavior);
            var camera = new GameObject("Camera").AddComponent<Camera>(); camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(.08f, .11f, .15f);
            EditorSceneManager.SaveScene(scene, ScenePath); AssetDatabase.SaveAssets();
        }
        /// <summary>Returns the explicit sample rules; all scores and action policy remain authored data.</summary>
        public static NpcBehaviorDefinition CreateDefinition()
        {
            var data = new NpcBehaviorDefinition();
            data.variables.Add(new NpcBehaviorVariable { id = "affinity", defaultNumber = 40, exposeToDialogue = true, label = "Current affinity toward the player (0 to 100)" });
            data.variables.Add(new NpcBehaviorVariable { id = "greeted", kind = NpcBehaviorValueKind.Boolean, exposeToDialogue = true, label = "The player has greeted this NPC" });
            data.externalValues.Add(new NpcBehaviorExternalValue { id = "permission" }); data.externalValues.Add(new NpcBehaviorExternalValue { id = "alarm" });
            data.signals.Add(new NpcBehaviorSignal { conditionDescription = "Match ONLY a direct compliment about this NPC in the latest user message, such as calling this NPC kind. A request, a state question, sharing a password or asking to recall one is NOT a compliment. Ignore compliments in earlier turns." });
            data.signals.Add(new NpcBehaviorSignal { triggerId = "insult", conditionDescription = "Match ONLY a direct insult about this NPC in the latest user message, such as calling this NPC unkind. Requests, state questions and password conversations are NOT insults. Ignore earlier turns.", exampleUserText = "불친절하군요." });
            data.signals.Add(new NpcBehaviorSignal { triggerId = "greet", conditionDescription = "Match ONLY an explicit greeting such as hello in the latest user message. A polite request or a memory question is NOT a greeting. Ignore greetings in earlier turns.", exampleUserText = "안녕하세요." });
            data.signals.Add(new NpcBehaviorSignal { triggerId = "request_open_gate", conditionDescription = "Match every current request to open the gate, including repeated requests after success or refusal. Match the user's intent regardless of affinity, permission, alarm or gate state: Unity checks those separately. Do not match a question asking only about the current gate state.", exampleUserText = "문을 열어 주세요." });
            data.mutationRules.Add(new NpcBehaviorMutationRule { changes = new List<NpcBehaviorMutation> { new NpcBehaviorMutation() } });
            data.mutationRules.Add(new NpcBehaviorMutationRule { ruleId = "lose_affinity", triggerId = "insult", changes = new List<NpcBehaviorMutation> { new NpcBehaviorMutation { operation = NpcBehaviorMutationKind.Subtract } } });
            data.mutationRules.Add(new NpcBehaviorMutationRule { ruleId = "remember_greeting", triggerId = "greet", changes = new List<NpcBehaviorMutation> { new NpcBehaviorMutation { variableId = "greeted", operation = NpcBehaviorMutationKind.Set, boolean = true } } });
            data.actionRules.Add(new NpcBehaviorActionRule { priority = 100, cooldownSeconds = 5, condition = new NpcBehaviorCondition { nodes = new List<NpcBehaviorConditionNode> {
                new NpcBehaviorConditionNode { kind = NpcBehaviorConditionKind.All }, new NpcBehaviorConditionNode { parent = 0, number = 60 },
                new NpcBehaviorConditionNode { parent = 0, valueId = "permission", comparison = NpcBehaviorComparison.Equal, boolean = true },
                new NpcBehaviorConditionNode { parent = 0, kind = NpcBehaviorConditionKind.Not },
                new NpcBehaviorConditionNode { parent = 3, valueId = "alarm", comparison = NpcBehaviorComparison.Equal, boolean = true } } } });
            data.actionRules.Add(new NpcBehaviorActionRule { ruleId = "greet_once", triggerId = "greet", actionId = "special_greeting", priority = 10, once = true });
            return data;
        }
        /// <summary>Connects one NPC's independent state, explicit providers, presentation and consumer handlers.</summary>
        private static void CreateNpc(Transform parent, string displayName, string id, NpcConversationMode mode, NpcBehaviorProfile behavior)
        {
            var profile = Asset<CharacterProfile>(Root + "/Profiles/" + displayName + ".asset"); var character = new SerializedObject(profile);
            character.FindProperty("characterId").stringValue = id; character.FindProperty("displayName").stringValue = displayName;
            character.FindProperty("personality").stringValue = "A careful gatekeeper who appreciates sincere kindness.";
            character.FindProperty("speechStyle").stringValue = "Reply in the user's language using one or two short sentences.";
            character.FindProperty("exampleDialogue").stringValue = "안녕하세요. 이야기를 나누어 보세요.";
            character.FindProperty("background").stringValue = "You guard a shared gate. Each NPC has its own affinity toward the player.";
            character.FindProperty("goalsAndValues").stringValue = "Use current observations. Classify configured triggers from the current user message on every turn, even when repeated. Previous replies and game conditions must not suppress a matching intent. Unity separately checks affinity at least 60, permission and no alarm before opening the gate. Game code decides effects.";
            character.ApplyModifiedPropertiesWithoutUndo();
            var target = new GameObject(displayName); target.transform.SetParent(parent, false);
            var adapter = target.AddComponent<AdvancedRelationshipNpc>(); var greeting = target.AddComponent<AdvancedGreetingHandler>();
            var actions = target.AddComponent<NpcActionCoordinator>(); var settings = new SerializedObject(actions);
            settings.FindProperty("behaviorMode").intValue = (int)NpcBehaviorMode.Advanced; settings.FindProperty("behaviorProfile").objectReferenceValue = behavior;
            settings.FindProperty("enableBehaviorTrace").boolValue = true;
            SetReferences(settings.FindProperty("actionHandlerSources"), adapter, greeting);
            SetReferences(settings.FindProperty("behaviorValueProviderSources"), adapter); settings.ApplyModifiedPropertiesWithoutUndo();
            NpcContextCoordinator context = null;
            if (mode == NpcConversationMode.BackendContext || mode == NpcConversationMode.LocalInference)
            {
                var bridge = target.AddComponent<NpcBehaviorContextProvider>(); context = target.AddComponent<NpcContextCoordinator>();
                var contextSettings = new SerializedObject(context); SetReferences(contextSettings.FindProperty("contextProviderSources"), adapter, bridge); contextSettings.ApplyModifiedPropertiesWithoutUndo();
            }
            var conversation = target.AddComponent<NpcConversationBehaviour>(); var conversationSettings = new SerializedObject(conversation);
            conversationSettings.FindProperty("characterProfile").objectReferenceValue = profile;
            conversationSettings.FindProperty("presentationDriverSource").objectReferenceValue = adapter;
            conversationSettings.FindProperty("conversationMode").intValue = (int)mode;
            conversationSettings.FindProperty("actionCoordinator").objectReferenceValue = actions;
            conversationSettings.FindProperty("contextCoordinator").objectReferenceValue = context;
            conversationSettings.ApplyModifiedPropertiesWithoutUndo();
        }
        /// <summary>Writes explicit component references using Unity serialization.</summary>
        private static void SetReferences(SerializedProperty property, params MonoBehaviour[] sources)
        { property.arraySize = sources.Length; for (var index = 0; index < sources.Length; index++) property.GetArrayElementAtIndex(index).objectReferenceValue = sources[index]; }
        /// <summary>Creates missing sample assets while preserving previously generated GUIDs.</summary>
        private static T Asset<T>(string path) where T : ScriptableObject
        {
            var asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset == null) { asset = ScriptableObject.CreateInstance<T>(); AssetDatabase.CreateAsset(asset, path); }
            return asset;
        }
        /// <summary>Creates only missing consumer folders through the asset database.</summary>
        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            var index = path.LastIndexOf('/'); EnsureFolder(path.Substring(0, index)); AssetDatabase.CreateFolder(path.Substring(0, index), path.Substring(index + 1));
        }
    }
}
