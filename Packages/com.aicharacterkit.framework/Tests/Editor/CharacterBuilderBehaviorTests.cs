using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using AiCharacterKit.Behavior;
using AiCharacterKit.Editor;
using AiCharacterKit.Unity;
using AiCharacterKit.Unity.Actions;
using AiCharacterKit.Unity.Behavior;
using AiCharacterKit.Unity.LocalInference;
using AiCharacterKit.Unity.Networking;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using V3 = AiCharacterKit.Transport.V3;
using V4 = AiCharacterKit.Transport.V4;

namespace AiCharacterKit.Core.Tests
{
    /// <summary>Verifies static Builder wiring, Prefab safety, runtime recovery and memory/state independence.</summary>
    public sealed class CharacterBuilderBehaviorTests
    {
        private const string Folder = "Assets/__AICharacterKitPhase12Tests";
        private CharacterProfile character;
        private NpcBehaviorProfile behavior;

        /// <summary>Creates test-owned profiles inside the validation project's Assets directory.</summary>
        [SetUp] public void SetUp()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            if (AssetDatabase.IsValidFolder(Folder)) AssetDatabase.DeleteAsset(Folder);
            AssetDatabase.CreateFolder("Assets", "__AICharacterKitPhase12Tests");
            Assert.That(CharacterBuilderAssetService.TryCreateCharacterProfile(new CharacterProfileDraft(), Folder, out character, out var error), Is.True, error);
            Assert.That(CharacterBuilderAssetService.TrySaveBehaviorProfile(null, NpcBehaviorRuntimeTests.Definition(true, true), Folder, "Behavior", out behavior, out error), Is.True, error);
            BehaviorTestAdapter.CaptureCalls = BehaviorTestAdapter.AuthorizationCalls = BehaviorTestAdapter.ExecutionCalls = 0;
        }
        /// <summary>Removes only test-owned assets after closing all temporary Scene references.</summary>
        [TearDown] public void TearDown()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single); Undo.ClearAll();
            if (AssetDatabase.IsValidFolder(Folder)) AssetDatabase.DeleteAsset(Folder);
        }
        /// <summary>Creates an explicit target-owned Advanced configuration.</summary>
        private CharacterBuilderConfiguration Configuration(GameObject target, BehaviorTestAdapter adapter, NpcConversationMode mode = NpcConversationMode.Mock)
        {
            return new CharacterBuilderConfiguration { Target = target, CharacterProfile = character, VisualPresentationDriver = adapter,
                ConversationMode = mode, ConfigureActions = true, BehaviorMode = NpcBehaviorMode.Advanced, BehaviorProfile = behavior,
                BehaviorValueProviderSources = new MonoBehaviour[] { adapter }, ActionHandlerSources = new MonoBehaviour[] { adapter }, EnableBehaviorTrace = true,
                ConfigureGrounding = mode == NpcConversationMode.BackendContext || mode == NpcConversationMode.LocalInference };
        }

        /// <summary>Applies and reapplies Scene or Prefab settings without calling providers or authorization.</summary>
        [TestCase(false)][TestCase(true)] public void ApplyAndReapply_AreStaticAndIdempotent(bool prefab)
        {
            var target = new GameObject("Advanced"); var adapter = target.AddComponent<BehaviorTestAdapter>();
            if (prefab)
            {
                var asset = PrefabUtility.SaveAsPrefabAsset(target, Folder + "/Npc.prefab"); UnityEngine.Object.DestroyImmediate(target);
                target = asset; adapter = target.GetComponent<BehaviorTestAdapter>();
            }
            var configuration = Configuration(target, adapter, NpcConversationMode.BackendContext);
            Assert.That(CharacterBuilderService.TryApply(configuration, out _, out var error), Is.True, error);
            if (prefab) { target = AssetDatabase.LoadAssetAtPath<GameObject>(Folder + "/Npc.prefab"); adapter = target.GetComponent<BehaviorTestAdapter>(); configuration = Configuration(target, adapter, NpcConversationMode.BackendContext); }
            Assert.That(CharacterBuilderService.TryApply(configuration, out _, out error), Is.True, error);
            Assert.That(target.GetComponents<NpcActionCoordinator>(), Has.Length.EqualTo(1));
            Assert.That(target.GetComponents<NpcBehaviorContextProvider>(), Has.Length.EqualTo(1));
            Assert.That(BehaviorTestAdapter.CaptureCalls, Is.Zero); Assert.That(BehaviorTestAdapter.AuthorizationCalls, Is.Zero); Assert.That(BehaviorTestAdapter.ExecutionCalls, Is.Zero);
        }

        /// <summary>Rejects external Scene references in Prefab authoring before mutating the asset.</summary>
        [Test] public void Prefab_RejectsExternalValueProvider()
        {
            var target = new GameObject("Prefab"); target.AddComponent<BehaviorTestAdapter>();
            var prefab = PrefabUtility.SaveAsPrefabAsset(target, Folder + "/Npc.prefab"); UnityEngine.Object.DestroyImmediate(target);
            var external = new GameObject("External").AddComponent<BehaviorTestAdapter>();
            var configuration = Configuration(prefab, prefab.GetComponent<BehaviorTestAdapter>());
            configuration.BehaviorValueProviderSources = new MonoBehaviour[] { external };
            Assert.That(CharacterBuilderService.TryApply(configuration, out _, out _), Is.False);
            Assert.That(prefab.GetComponent<NpcActionCoordinator>(), Is.Null);
        }

        /// <summary>Rejects Advanced on V1/V2 while preserving existing Basic support.</summary>
        [TestCase(NpcConversationMode.Backend)][TestCase(NpcConversationMode.BackendSession)]
        public void UnsupportedMode_IsRejected(NpcConversationMode mode)
        {
            var target = new GameObject("NPC"); var adapter = target.AddComponent<BehaviorTestAdapter>();
            Assert.That(CharacterBuilderService.Validate(Configuration(target, adapter, mode)).HasErrors, Is.True);
            Assert.That(target.GetComponent<NpcActionCoordinator>(), Is.Null);
        }

        /// <summary>Initializes with an unready provider, recovers in the same NPC and keeps state through enable cycles.</summary>
        [Test] public async Task DelayedProvider_RecoversAndStateSurvivesDisable()
        {
            var target = new GameObject("NPC"); var adapter = target.AddComponent<BehaviorTestAdapter>();
            Assert.That(CharacterBuilderService.TryApply(Configuration(target, adapter), out var conversation, out var error), Is.True, error);
            Initialize(conversation);
            Assert.That(BehaviorTestAdapter.CaptureCalls, Is.Zero);
            Assert.That(await conversation.SubmitAsync("정말 친절하네요."), Is.True);
            var actions = target.GetComponent<NpcActionCoordinator>();
            Assert.That(actions.LastResult.Status, Is.EqualTo(NpcActionStatus.Failed)); Assert.That(actions.BehaviorState["affinity"].Number, Is.EqualTo(40));
            adapter.ValuesReady = true;
            await conversation.SubmitAsync("정말 친절하네요.");
            Assert.That(actions.BehaviorState["affinity"].Number, Is.EqualTo(50));
            Invoke(conversation, "OnDisable"); Invoke(conversation, "OnEnable");
            await conversation.SubmitAsync("정말 친절하네요.");
            Assert.That(actions.BehaviorState["affinity"].Number, Is.EqualTo(60)); Assert.That(BehaviorTestAdapter.ExecutionCalls, Is.EqualTo(1));
            Invoke(conversation, "OnDisable"); Invoke(conversation, "OnDestroy");
        }

        /// <summary>Blocks game state writes during generation and keeps behavior state during successful memory reset.</summary>
        [Test] public async Task ConversationReservation_AndMemoryReset_AreIndependent()
        {
            var target = new GameObject("NPC"); var adapter = target.AddComponent<BehaviorTestAdapter>(); adapter.ValuesReady = true;
            Assert.That(CharacterBuilderService.TryApply(Configuration(target, adapter), out var conversation, out _), Is.True);
            Initialize(conversation);
            var actions = target.GetComponent<NpcActionCoordinator>(); actions.TrySetNumber("affinity", 75, out _);
            var pending = conversation.SubmitAsync("정말 친절하네요.");
            Assert.That(actions.TryResetBehaviorState(out var error), Is.False); Assert.That(error, Is.EqualTo("busy"));
            Assert.That(actions.TryAddNumber("affinity", 1, out _), Is.False);
            Assert.That(await conversation.SubmitAsync("정말 친절하네요."), Is.False);
            await pending; Assert.That(actions.BehaviorState["affinity"].Number, Is.EqualTo(85));
            var field = typeof(NpcConversationBehaviour).GetField("controller", BindingFlags.Instance | BindingFlags.NonPublic);
            ((NpcAIController)field.GetValue(conversation)).Dispose();
            var client = new ResetClient(); field.SetValue(conversation, new NpcAIController(client, adapter, actions));
            Assert.That(await conversation.ResetConversationAsync(), Is.True);
            Assert.That(client.Resets, Is.EqualTo(1)); Assert.That(actions.BehaviorState["affinity"].Number, Is.EqualTo(85));
            Assert.That(actions.TryResetBehaviorState(out _), Is.True); Assert.That(client.Resets, Is.EqualTo(1));
            Assert.That(actions.BehaviorState["affinity"].Number, Is.EqualTo(40));
            Invoke(conversation, "OnDisable"); Invoke(conversation, "OnDestroy");
        }

        /// <summary>Imports Basic to an equivalent detached draft and previews only synthetic handlers and values.</summary>
        [Test] public void ImportAndPreview_DoNotCallConsumersOrModifyOriginal()
        {
            var basicDraft = new ActionProfileDraft(); basicDraft.Bindings.Clear();
            basicDraft.Bindings.Add(new ActionBindingDraft { TriggerId = "praise", ActionId = "open_gate", Priority = 7 });
            Assert.That(CharacterBuilderAssetService.TryCreateActionProfile(basicDraft, Folder, out var basic, out _), Is.True);
            var imported = CharacterBuilderAssetService.ImportBasicBehavior(basic);
            Assert.That(imported.actionRules[0].priority, Is.EqualTo(7)); imported.actionRules[0].priority = 10;
            Assert.That(basic.Bindings[0].priority, Is.EqualTo(7));
            var trace = CharacterBuilderAssetService.PreviewBehavior(behavior.CreateDraft(),
                new Dictionary<string, NpcBehaviorValue> { ["affinity"] = new NpcBehaviorValue(50d) },
                new Dictionary<string, NpcBehaviorValue> { ["permission"] = new NpcBehaviorValue(true) }, new[] { "praise" });
            Assert.That(trace.ActionResult.Executed, Is.True); Assert.That(trace.Changes[0].After.Number, Is.EqualTo(60));
            Assert.That(BehaviorTestAdapter.CaptureCalls + BehaviorTestAdapter.AuthorizationCalls + BehaviorTestAdapter.ExecutionCalls, Is.Zero);
        }

        /// <summary>Loads both grounding and Advanced authoring from an existing target without invoking providers.</summary>
        [Test] public void BuilderWindow_LoadsExistingAdvancedAndGrounding()
        {
            var target = new GameObject("NPC"); var adapter = target.AddComponent<BehaviorTestAdapter>();
            Assert.That(CharacterBuilderService.TryApply(Configuration(target, adapter, NpcConversationMode.BackendContext), out _, out _), Is.True);
            var window = ScriptableObject.CreateInstance<CharacterBuilderWindow>();
            try
            {
                typeof(CharacterBuilderWindow).GetMethod("LoadTarget", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(window, new object[] { target });
                var configuration = (CharacterBuilderConfiguration)typeof(CharacterBuilderWindow).GetField("configuration", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(window);
                Assert.That(configuration.BehaviorMode, Is.EqualTo(NpcBehaviorMode.Advanced));
                Assert.That(configuration.BehaviorProfile, Is.SameAs(behavior));
                Assert.That(configuration.BehaviorValueProviderSources, Is.EqualTo(new[] { adapter }));
                Assert.That(configuration.ConfigureGrounding, Is.True); Assert.That(BehaviorTestAdapter.CaptureCalls, Is.Zero);
            }
            finally { UnityEngine.Object.DestroyImmediate(window); }
        }

        /// <summary>Restores an edited definition through Unity Undo without affecting unrelated instances.</summary>
        [Test] public void SavedProfileEdit_SupportsUndoWithoutChangingInstanceState()
        {
            var draft = behavior.CreateDraft(); draft.variables[0].defaultNumber = 75;
            Assert.That(CharacterBuilderAssetService.TrySaveBehaviorProfile(behavior, draft, Folder, "Behavior", out _, out var error), Is.True, error);
            Undo.FlushUndoRecordObjects();
            Assert.That(behavior.CreateDraft().variables[0].defaultNumber, Is.EqualTo(75));
            Undo.PerformUndo();
            Assert.That(behavior.CreateDraft().variables[0].defaultNumber, Is.EqualTo(40));
        }

        /// <summary>Switches authoring back to Basic without leaving an inactive Advanced bridge in live grounding.</summary>
        [Test] public void AdvancedToBasic_ReapplyDisconnectsOnlyItsImplicitGrounding()
        {
            var target = new GameObject("Switchable NPC"); var adapter = target.AddComponent<BehaviorTestAdapter>();
            var configuration = Configuration(target, adapter, NpcConversationMode.BackendContext);
            Assert.That(CharacterBuilderService.TryApply(configuration, out _, out var error), Is.True, error);
            var bridge = target.GetComponent<NpcBehaviorContextProvider>();
            configuration.ContextProviderSources = new MonoBehaviour[] { bridge };
            var basicDraft = new ActionProfileDraft(); basicDraft.Bindings.Clear();
            basicDraft.Bindings.Add(new ActionBindingDraft { TriggerId = "praise", ActionId = "open_gate" });
            Assert.That(CharacterBuilderAssetService.TryCreateActionProfile(basicDraft, Folder, out var basic, out error), Is.True, error);
            configuration.BehaviorMode = NpcBehaviorMode.Basic; configuration.ActionProfile = basic;
            Assert.That(CharacterBuilderService.TryApply(configuration, out var conversation, out error), Is.True, error);
            Initialize(conversation);
            Assert.That(target.GetComponent<NpcContextCoordinator>().TryCreateSnapshot(character, out _, out _, out error), Is.True, error);
            Assert.That(target.GetComponent<NpcBehaviorContextProvider>(), Is.SameAs(bridge));
            Assert.That(target.GetComponent<NpcActionCoordinator>().BehaviorState, Is.Empty);
            Assert.That(BehaviorTestAdapter.CaptureCalls + BehaviorTestAdapter.AuthorizationCalls + BehaviorTestAdapter.ExecutionCalls, Is.Zero);
            Invoke(conversation, "OnDisable"); Invoke(conversation, "OnDestroy");
        }

        /// <summary>Initializes serialized composition and its cancellation lifetime explicitly in EditMode.</summary>
        private static void Initialize(NpcConversationBehaviour conversation) { Invoke(conversation, "Awake"); Invoke(conversation, "OnEnable"); }
        /// <summary>Invokes Unity lifecycle methods deterministically without starting an actual Play session.</summary>
        private static void Invoke(NpcConversationBehaviour conversation, string method) => typeof(NpcConversationBehaviour).GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(conversation, null);

        /// <summary>Exercises actual V3/V4 mapping and controller dispatch with signal-only definitions and fake gateways.</summary>
        [TestCase(NpcConversationMode.BackendActions)][TestCase(NpcConversationMode.BackendContext)]
        public async Task BackendSignals_ApplyStateOnce_AndRejectInvalidResponses(NpcConversationMode mode)
        {
            var target = new GameObject("Backend Advanced"); var adapter = target.AddComponent<BehaviorTestAdapter>(); adapter.ValuesReady = true;
            Assert.That(CharacterBuilderService.TryApply(Configuration(target, adapter, mode), out var conversation, out var error), Is.True, error);
            Initialize(conversation); var actions = target.GetComponent<NpcActionCoordinator>();
            var field = typeof(NpcConversationBehaviour).GetField("controller", BindingFlags.Instance | BindingFlags.NonPublic);
            ((NpcAIController)field.GetValue(conversation)).Dispose();
            var fake = new BehaviorBackendGateway();
            IAiConversationClient client = mode == NpcConversationMode.BackendActions
                ? (IAiConversationClient)new ActionBackendConversationClient(fake, character.CharacterId, actions.CreateTriggerDefinitions())
                : new ContextBackendConversationClient(fake, character.CharacterId, actions.CreateTriggerDefinitions());
            field.SetValue(conversation, new NpcAIController(client, adapter, actions));
            Assert.That(await conversation.SubmitAsync("synthetic praise"), Is.True);
            Assert.That(actions.BehaviorState["affinity"].Number, Is.EqualTo(50)); Assert.That(BehaviorTestAdapter.ExecutionCalls, Is.Zero);
            if (mode == NpcConversationMode.BackendContext) Assert.That(fake.AffinityFact, Does.EndWith(": 40"));
            Assert.That(await conversation.SubmitAsync("synthetic praise"), Is.True);
            Assert.That(actions.BehaviorState["affinity"].Number, Is.EqualTo(60)); Assert.That(BehaviorTestAdapter.ExecutionCalls, Is.EqualTo(1));
            if (mode == NpcConversationMode.BackendContext) Assert.That(fake.AffinityFact, Does.EndWith(": 50"));
            fake.InvalidIds = true;
            Assert.That(await conversation.SubmitAsync("synthetic invalid reply"), Is.False);
            Assert.That(actions.BehaviorState["affinity"].Number, Is.EqualTo(60)); Assert.That(BehaviorTestAdapter.CaptureCalls, Is.EqualTo(2));
            Assert.That(await conversation.ResetConversationAsync(), Is.True); Assert.That(fake.Resets, Is.EqualTo(1));
            Assert.That(actions.BehaviorState["affinity"].Number, Is.EqualTo(60));
            Invoke(conversation, "OnDisable"); Invoke(conversation, "OnDestroy");
        }

        /// <summary>Confirms Advanced wiring and Awake borrow a parent local host without preparing it or capturing live values.</summary>
        [Test] public void LocalAdvanced_ApplyReapplyAndAwake_AreStatic()
        {
            var host = new GameObject("Local Host"); var runtime = host.AddComponent<LocalInferenceRuntime>();
            var localProfile = ScriptableObject.CreateInstance<LocalInferenceProfile>();
            var localSettings = new SerializedObject(localProfile);
            localSettings.FindProperty("manifestPath").stringValue = "E:/synthetic-installation/manifest.json";
            localSettings.ApplyModifiedPropertiesWithoutUndo();
            AssetDatabase.CreateAsset(localProfile, Folder + "/Local.asset");
            var settings = new SerializedObject(runtime); settings.FindProperty("profile").objectReferenceValue = localProfile; settings.ApplyModifiedPropertiesWithoutUndo();
            var target = new GameObject("NPC"); target.transform.SetParent(host.transform); var adapter = target.AddComponent<BehaviorTestAdapter>();
            var configuration = Configuration(target, adapter, NpcConversationMode.LocalInference);
            Assert.That(CharacterBuilderService.TryApply(configuration, out var conversation, out var error), Is.True, error);
            Assert.That(CharacterBuilderService.TryApply(configuration, out conversation, out error), Is.True, error);
            Initialize(conversation);
            Assert.That(runtime.ProcessId, Is.Zero); Assert.That(conversation.enabled, Is.True);
            Assert.That(BehaviorTestAdapter.CaptureCalls + BehaviorTestAdapter.AuthorizationCalls + BehaviorTestAdapter.ExecutionCalls, Is.Zero);
            Invoke(conversation, "OnDisable"); Invoke(conversation, "OnDestroy");
        }

        private sealed class BehaviorBackendGateway : IAiNpcActionBackendGateway, IAiNpcContextBackendGateway
        {
            public bool InvalidIds; public int Resets; public string AffinityFact;
            /// <summary>Returns a controlled correlated V3 response without HTTP or vendor calls.</summary>
            public Task<V3.AiNpcResponseEnvelopeDto> SendAsync(V3.AiNpcRequestEnvelopeDto request, CancellationToken token)
            { token.ThrowIfCancellationRequested(); return Task.FromResult(V3.AiNpcContractMapper.CreateSuccessResponse(Reply(), request.requestId)); }
            /// <summary>Observes only the typed score fact and returns a controlled correlated V4 response.</summary>
            public Task<V4.AiNpcResponseEnvelopeDto> SendAsync(V4.AiNpcRequestEnvelopeDto request, CancellationToken token)
            {
                token.ThrowIfCancellationRequested();
                foreach (var fact in request.grounding.facts) if (fact.factId == "affinity") AffinityFact = fact.statement;
                return Task.FromResult(V4.AiNpcContractMapper.CreateSuccessResponse(Reply(), request.requestId));
            }
            /// <summary>Creates a valid signal or an unknown-ID protocol violation.</summary>
            private AiNpcResponse Reply() => new AiNpcResponse("Synthetic reply", NpcEmotion.Neutral, NpcGesture.None, new[] { InvalidIds ? "unknown_signal" : "praise" });
            /// <summary>Counts a V3 memory reset independently of game state.</summary>
            public Task<V3.AiNpcSessionResetResponseDto> ResetAsync(V3.AiNpcSessionResetRequestDto request, CancellationToken token)
            { token.ThrowIfCancellationRequested(); Resets++; return Task.FromResult(V3.AiNpcContractMapper.CreateResetSuccessResponse(request.requestId)); }
            /// <summary>Counts a V4 memory reset independently of game state.</summary>
            public Task<V4.AiNpcSessionResetResponseDto> ResetAsync(V4.AiNpcSessionResetRequestDto request, CancellationToken token)
            { token.ThrowIfCancellationRequested(); Resets++; return Task.FromResult(V4.AiNpcContractMapper.CreateResetSuccessResponse(request.requestId)); }
        }
        private sealed class ResetClient : IResettableAiConversationClient
        {
            public int Resets;
            /// <summary>Returns a synthetic response without network calls.</summary>
            public Task<AiNpcResponse> SendAsync(AiNpcRequest request, CancellationToken token) => Task.FromResult(new AiNpcResponse("ok", NpcEmotion.Neutral, NpcGesture.None));
            /// <summary>Counts only conversation memory reset operations.</summary>
            public Task ResetAsync(CancellationToken token) { Resets++; return Task.CompletedTask; }
        }
    }
}
