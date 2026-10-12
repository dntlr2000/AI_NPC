using System.Reflection;
using AiCharacterKit.Editor;
using AiCharacterKit.Unity;
using AiCharacterKit.Unity.Actions;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace AiCharacterKit.Core.Tests
{
    public sealed partial class CharacterBuilderBehaviorTests
    {
        /// <summary>Persists independent endpoints and strategy through Scene/Prefab reapplication without live calls.</summary>
        [TestCase(false)][TestCase(true)]
        public void InferenceStrategy_ApplyReapplyAndAwakeRemainStatic(bool prefab)
        {
            var target = new GameObject("Strategy NPC"); var adapter = target.AddComponent<BehaviorTestAdapter>();
            if (prefab)
            {
                var saved = PrefabUtility.SaveAsPrefabAsset(target, Folder + "/Strategy.prefab");
                Object.DestroyImmediate(target); target = saved; adapter = target.GetComponent<BehaviorTestAdapter>();
            }
            var configuration = Configuration(target, adapter, NpcConversationMode.BackendContext);
            configuration.ContextBackendEndpoint = "http://127.0.0.1:8001/v4/npc/respond";
            configuration.TwoStageContextBackendEndpoint = "http://127.0.0.1:8002/v4/npc/respond-two-stage";
            configuration.InferenceStrategy = NpcInferenceStrategy.ClassifyThenRespond;
            if (!NpcInferenceAvailability.DefaultApiTwoStage)
            {
                Assert.That(CharacterBuilderService.TryApply(configuration, out _, out _), Is.False);
                configuration.InferenceStrategy = NpcInferenceStrategy.SinglePass;
            }
            Assert.That(CharacterBuilderService.TryApply(configuration, out _, out var error), Is.True, error);
            Assert.That(CharacterBuilderService.TryApply(configuration, out _, out error), Is.True, error);
            if (prefab) target = AssetDatabase.LoadAssetAtPath<GameObject>(Folder + "/Strategy.prefab");
            var npc = target.GetComponent<NpcConversationBehaviour>(); var settings = new SerializedObject(npc);
            Assert.That(npc.InferenceStrategy, Is.EqualTo(configuration.InferenceStrategy));
            Assert.That(settings.FindProperty("contextBackendEndpoint").stringValue, Is.EqualTo(configuration.ContextBackendEndpoint));
            Assert.That(settings.FindProperty("twoStageContextBackendEndpoint").stringValue, Is.EqualTo(configuration.TwoStageContextBackendEndpoint));
            Assert.That(target.GetComponents<NpcActionCoordinator>(), Has.Length.EqualTo(1));
            if (!prefab) { Initialize(npc); Assert.That(npc.enabled, Is.True); Invoke(npc, "OnDisable"); Invoke(npc, "OnDestroy"); }
            Assert.That(BehaviorTestAdapter.CaptureCalls + BehaviorTestAdapter.AuthorizationCalls + BehaviorTestAdapter.ExecutionCalls, Is.Zero);
        }

        /// <summary>Rejects unsupported combinations before any Builder or gameplay mutation.</summary>
        [TestCase(NpcConversationMode.Mock)][TestCase(NpcConversationMode.Backend)]
        [TestCase(NpcConversationMode.BackendSession)][TestCase(NpcConversationMode.BackendActions)]
        [TestCase(NpcConversationMode.LocalInference)]
        public void InferenceStrategy_UnsupportedModeHasNoSideEffects(NpcConversationMode mode)
        {
            var target = new GameObject("Unsupported strategy"); var adapter = target.AddComponent<BehaviorTestAdapter>();
            var configuration = Configuration(target, adapter, mode); configuration.InferenceStrategy = NpcInferenceStrategy.ClassifyThenRespond;
            Assert.That(CharacterBuilderService.TryApply(configuration, out _, out _), Is.False);
            Assert.That(target.GetComponent<NpcConversationBehaviour>(), Is.Null);
            Assert.That(BehaviorTestAdapter.CaptureCalls + BehaviorTestAdapter.AuthorizationCalls + BehaviorTestAdapter.ExecutionCalls, Is.Zero);
        }

        /// <summary>Loads the persisted strategy and clears it for a new target without touching endpoints or providers.</summary>
        [Test] public void InferenceStrategy_WindowLoadsAndDefaultsNewTarget()
        {
            var target = new GameObject("Loaded strategy"); var adapter = target.AddComponent<BehaviorTestAdapter>();
            var config = Configuration(target, adapter, NpcConversationMode.BackendContext);
            config.TwoStageContextBackendEndpoint = "http://127.0.0.1:8002/v4/npc/respond-two-stage";
            Assert.That(CharacterBuilderService.TryApply(config, out var npc, out _), Is.True);
            var fields = new SerializedObject(npc); fields.FindProperty("inferenceStrategy").intValue = 1; fields.ApplyModifiedPropertiesWithoutUndo();
            var window = ScriptableObject.CreateInstance<CharacterBuilderWindow>();
            try
            {
                var load = typeof(CharacterBuilderWindow).GetMethod("LoadTarget", BindingFlags.Instance | BindingFlags.NonPublic);
                var state = typeof(CharacterBuilderWindow).GetField("configuration", BindingFlags.Instance | BindingFlags.NonPublic);
                load.Invoke(window, new object[] { target }); var loaded = (CharacterBuilderConfiguration)state.GetValue(window);
                Assert.That(loaded.InferenceStrategy, Is.EqualTo(NpcInferenceStrategy.ClassifyThenRespond));
                Assert.That(loaded.TwoStageContextBackendEndpoint, Is.EqualTo(config.TwoStageContextBackendEndpoint));
                load.Invoke(window, new object[] { new GameObject("Fresh target") });
                Assert.That(loaded.InferenceStrategy, Is.EqualTo(NpcInferenceStrategy.SinglePass));
                Assert.That(BehaviorTestAdapter.CaptureCalls + BehaviorTestAdapter.AuthorizationCalls + BehaviorTestAdapter.ExecutionCalls, Is.Zero);
            }
            finally { Object.DestroyImmediate(window); }
        }

        /// <summary>Restores strategy and both endpoint settings with the same Undo operation as other NPC configuration.</summary>
        [Test] public void InferenceStrategy_UndoPreservesOriginalConfiguration()
        {
            var target = new GameObject("Strategy Undo"); var adapter = target.AddComponent<BehaviorTestAdapter>();
            var configuration = Configuration(target, adapter, NpcConversationMode.BackendContext);
            Assert.That(CharacterBuilderService.TryApply(configuration, out var npc, out _), Is.True);
            var original = new SerializedObject(npc).FindProperty("twoStageContextBackendEndpoint").stringValue;
            Undo.ClearAll();
            configuration.TwoStageContextBackendEndpoint = "http://127.0.0.1:8002/v4/npc/respond-two-stage";
            if (NpcInferenceAvailability.DefaultApiTwoStage) configuration.InferenceStrategy = NpcInferenceStrategy.ClassifyThenRespond;
            Assert.That(CharacterBuilderService.TryApply(configuration, out _, out _), Is.True);
            Undo.PerformUndo();
            Assert.That(npc.InferenceStrategy, Is.EqualTo(NpcInferenceStrategy.SinglePass));
            Assert.That(new SerializedObject(npc).FindProperty("twoStageContextBackendEndpoint").stringValue, Is.EqualTo(original));
        }
    }
}
