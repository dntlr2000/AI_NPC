using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using AiCharacterKit.Core;
using AiCharacterKit.Unity;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace AiCharacterKit.Core.Tests
{
    /// <summary>
    /// Supplies mutable test observations through the public consumer extension point.
    /// </summary>
    public sealed class TestNpcContextProvider : NpcContextProviderBehaviour
    {
        public IReadOnlyList<NpcContextFact> Facts { get; set; } =
            Array.Empty<NpcContextFact>();

        public bool ThrowOnCapture { get; set; }

        public int CaptureCount { get; private set; }

        /// <summary>
        /// Returns controlled current facts or simulates one provider failure.
        /// </summary>
        public override IReadOnlyList<NpcContextFact> CaptureFacts()
        {
            CaptureCount++;
            if (ThrowOnCapture)
            {
                throw new InvalidOperationException("Private provider failure.");
            }

            return Facts;
        }
    }

    /// <summary>
    /// Verifies Unity context composition without retaining mutable provider data.
    /// </summary>
    public sealed class NpcContextCoordinatorTests
    {
        private readonly List<Object> createdObjects = new List<Object>();

        /// <summary>
        /// Destroys every transient Unity object created by a context test.
        /// </summary>
        [TearDown]
        public void TearDown()
        {
            for (var index = createdObjects.Count - 1; index >= 0; index--)
            {
                if (createdObjects[index] != null)
                {
                    Object.DestroyImmediate(createdObjects[index]);
                }
            }

            createdObjects.Clear();
        }

        /// <summary>
        /// Restores Edit Mode even when a lifecycle assertion fails after entering Play Mode.
        /// </summary>
        [UnityTearDown]
        public IEnumerator RestoreEditMode()
        {
            if (EditorApplication.isPlaying)
            {
                yield return new ExitPlayMode();
            }
        }

        /// <summary>
        /// Exercises real Awake/Start ordering and recovers the same NPC after one unready turn.
        /// </summary>
        [UnityTest]
        public IEnumerator Conversation_ProviderReadyInStart_RecoversWithoutReinitialization()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            yield return new EnterPlayMode();
            var profile = CreateCharacterProfile();
            var root = Track(new GameObject("Deferred Lifecycle NPC"));
            root.SetActive(false);
            var provider = root.AddComponent<DeferredTestContextProvider>();
            provider.Facts = new[]
            {
                new NpcContextFact("gate_status", NpcContextFactKind.Observation, "Closed.", 90)
            };
            var coordinator = root.AddComponent<NpcContextCoordinator>();
            ConfigureCoordinator(coordinator, null, provider);
            var presentation = root.AddComponent<NpcTextPresentationDriver>();
            var statusObject = Track(new GameObject("Lifecycle Status", typeof(RectTransform)));
            var status = statusObject.AddComponent<Text>();
            var presentationSettings = new SerializedObject(presentation);
            presentationSettings.FindProperty("statusText").objectReferenceValue = status;
            presentationSettings.ApplyModifiedPropertiesWithoutUndo();
            var conversation = root.AddComponent<NpcConversationBehaviour>();
            var settings = new SerializedObject(conversation);
            settings.FindProperty("characterProfile").objectReferenceValue = profile;
            settings.FindProperty("presentationDriverSource").objectReferenceValue = presentation;
            settings.FindProperty("contextCoordinator").objectReferenceValue = coordinator;
            settings.FindProperty("conversationMode").intValue = (int)NpcConversationMode.BackendContext;
            settings.ApplyModifiedPropertiesWithoutUndo();
            root.SetActive(true);

            Assert.That(conversation.enabled, Is.True);
            Assert.That(conversation.SupportsConversationReset, Is.True);
            Assert.That(provider.IsReady, Is.False);
            Assert.That(provider.CaptureCount, Is.Zero);

            var client = new LifecycleConversationClient();
            var controller = ReplaceControllerForTest(conversation, client, presentation);
            var beforeReady = conversation.SubmitAsync("Before ready");
            Assert.That(beforeReady.IsCompleted, Is.True);
            Assert.That(beforeReady.Result, Is.False);
            Assert.That(client.RequestCount, Is.Zero);
            Assert.That(status.text, Does.Not.Contain("Private game-state service"));
            Assert.That(conversation.enabled, Is.True);
            Assert.That(conversation.LastContextRevision, Is.Empty);

            yield return null;
            Assert.That(provider.IsReady, Is.True);
            var afterReady = conversation.SubmitAsync("After ready");
            while (!afterReady.IsCompleted) yield return null;
            Assert.That(afterReady.Result, Is.True);
            Assert.That(client.RequestCount, Is.EqualTo(1));
            Assert.That(provider.CaptureCount, Is.EqualTo(2));
            Assert.That(GetControllerForTest(conversation), Is.SameAs(controller));
            var firstSnapshot = client.LastRequest.Grounding;
            Assert.That(conversation.LastContextRevision, Is.EqualTo(firstSnapshot.Revision));

            var reset = conversation.ResetConversationAsync();
            while (!reset.IsCompleted) yield return null;
            Assert.That(reset.Result, Is.True);
            Assert.That(client.ResetCount, Is.EqualTo(1));
            Assert.That(provider.CaptureCount, Is.EqualTo(2));
            provider.Facts = new[]
            {
                new NpcContextFact("gate_status", NpcContextFactKind.Observation, "Open.", 90)
            };
            var afterReset = conversation.SubmitAsync("After reset");
            while (!afterReset.IsCompleted) yield return null;
            Assert.That(afterReset.Result, Is.True);
            Assert.That(client.LastRequest.Grounding.Background, Is.EqualTo(profile.Background));
            Assert.That(client.LastRequest.Grounding.Revision, Is.Not.EqualTo(firstSnapshot.Revision));
            Assert.That(firstSnapshot.Facts[0].Statement, Is.EqualTo("Closed."));
            Assert.That(provider.CaptureCount, Is.EqualTo(3));
        }

        /// <summary>
        /// Replaces only the test instance's client composition to avoid HTTP without adding a Runtime API.
        /// </summary>
        private static NpcAIController ReplaceControllerForTest(
            NpcConversationBehaviour conversation,
            IAiConversationClient client,
            INpcPresentationDriver presentation)
        {
            GetControllerForTest(conversation).Dispose();
            var controller = new NpcAIController(client, presentation);
            typeof(NpcConversationBehaviour).GetField("controller", BindingFlags.NonPublic | BindingFlags.Instance)
                .SetValue(conversation, controller);
            return controller;
        }

        /// <summary>
        /// Reads the test instance's private composition to verify it survives a failed capture.
        /// </summary>
        private static NpcAIController GetControllerForTest(NpcConversationBehaviour conversation)
        {
            return (NpcAIController)typeof(NpcConversationBehaviour)
                .GetField("controller", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(conversation);
        }

        private sealed class LifecycleConversationClient : IResettableAiConversationClient
        {
            public int RequestCount { get; private set; }
            public int ResetCount { get; private set; }
            public AiNpcRequest LastRequest { get; private set; }

            /// <summary>
            /// Records the captured grounding and returns one deterministic response without a provider call.
            /// </summary>
            public Task<AiNpcResponse> SendAsync(AiNpcRequest request, CancellationToken cancellationToken)
            {
                cancellationToken.ThrowIfCancellationRequested();
                RequestCount++;
                LastRequest = request;
                return Task.FromResult(new AiNpcResponse("Synthetic reply.", NpcEmotion.Neutral, NpcGesture.None));
            }

            /// <summary>
            /// Records reset separately from capture and dialogue generation.
            /// </summary>
            public Task ResetAsync(CancellationToken cancellationToken)
            {
                cancellationToken.ThrowIfCancellationRequested();
                ResetCount++;
                return Task.CompletedTask;
            }
        }

        /// <summary>
        /// Keeps authoring validation independent of live readiness while preserving live validation.
        /// </summary>
        [Test]
        public void Configuration_ThrowingProvider_DoesNotCaptureButLegacyValidationDoes()
        {
            var profile = CreateCharacterProfile();
            var root = Track(new GameObject("Deferred Context NPC"));
            var provider = root.AddComponent<TestNpcContextProvider>();
            provider.ThrowOnCapture = true;
            var coordinator = root.AddComponent<NpcContextCoordinator>();
            ConfigureCoordinator(coordinator, null, provider);

            Assert.That(coordinator.TryValidateConfiguration(profile, out var error), Is.True, error);
            Assert.That(provider.CaptureCount, Is.Zero);
            Assert.That(coordinator.TryValidate(profile, out error), Is.False);
            Assert.That(provider.CaptureCount, Is.EqualTo(1));
            Assert.That(error, Does.Not.Contain("Private provider failure"));
            provider.ThrowOnCapture = false;
            Assert.That(coordinator.TryValidate(profile, out error), Is.True, error);
            Assert.That(provider.CaptureCount, Is.EqualTo(2));
        }

        /// <summary>
        /// Rejects invalid references, interfaces, and authored collisions without invoking providers.
        /// </summary>
        [TestCase("missing_profile")]
        [TestCase("invalid_profile")]
        [TestCase("missing_lore")]
        [TestCase("duplicate_lore")]
        [TestCase("authored_collision")]
        [TestCase("missing_provider")]
        [TestCase("duplicate_provider")]
        [TestCase("wrong_interface")]
        public void Configuration_InvalidAuthoring_FailsBeforeCapture(string invalidCase)
        {
            var profile = CreateCharacterProfile();
            var lore = CreateLoreProfile("city_name", "Synthetic city.");
            var root = Track(new GameObject("Invalid Context NPC"));
            var provider = root.AddComponent<TestNpcContextProvider>();
            var coordinator = root.AddComponent<NpcContextCoordinator>();
            ConfigureCoordinator(coordinator, lore, provider);
            var serialized = new SerializedObject(coordinator);
            var loreSources = serialized.FindProperty("loreProfiles");
            var providers = serialized.FindProperty("contextProviderSources");
            switch (invalidCase)
            {
                case "missing_profile": profile = null; break;
                case "invalid_profile":
                    var character = new SerializedObject(profile);
                    character.FindProperty("characterId").stringValue = string.Empty;
                    character.ApplyModifiedPropertiesWithoutUndo();
                    break;
                case "missing_lore":
                    loreSources.GetArrayElementAtIndex(0).objectReferenceValue = null;
                    break;
                case "duplicate_lore":
                case "authored_collision":
                    loreSources.arraySize = 2;
                    loreSources.GetArrayElementAtIndex(1).objectReferenceValue =
                        invalidCase == "duplicate_lore" ? lore : CreateLoreProfile("city_name", "Other city.");
                    break;
                case "missing_provider":
                    providers.GetArrayElementAtIndex(0).objectReferenceValue = null;
                    break;
                case "duplicate_provider":
                    providers.arraySize = 2;
                    providers.GetArrayElementAtIndex(1).objectReferenceValue = provider;
                    break;
                case "wrong_interface":
                    providers.GetArrayElementAtIndex(0).objectReferenceValue = coordinator;
                    break;
            }
            serialized.ApplyModifiedPropertiesWithoutUndo();

            Assert.That(coordinator.TryValidateConfiguration(profile, out var error), Is.False);
            Assert.That(error, Is.Not.Empty);
            Assert.That(provider.CaptureCount, Is.Zero);
        }

        /// <summary>
        /// Fails one invalid capture without poisoning a later valid immutable snapshot.
        /// </summary>
        [TestCase("null_collection")]
        [TestCase("null_fact")]
        [TestCase("live_collision")]
        public void Capture_InvalidLiveFacts_RecoversOnNextCapture(string invalidCase)
        {
            var profile = CreateCharacterProfile();
            var root = Track(new GameObject("Recoverable Context NPC"));
            var provider = root.AddComponent<TestNpcContextProvider>();
            var coordinator = root.AddComponent<NpcContextCoordinator>();
            var lore = CreateLoreProfile("city_name", "Synthetic city.");
            ConfigureCoordinator(coordinator, lore, provider);
            provider.Facts = invalidCase == "null_collection" ? null
                : invalidCase == "null_fact" ? new NpcContextFact[] { null }
                : new[] { new NpcContextFact("city_name", NpcContextFactKind.Observation, "Duplicate.", 90) };

            Assert.That(coordinator.TryValidateConfiguration(profile, out var error), Is.True, error);
            Assert.That(coordinator.TryCreateSnapshot(profile, out var rejected, out _, out _), Is.False);
            Assert.That(rejected.IsEmpty, Is.True);
            provider.Facts = Array.Empty<NpcContextFact>();
            Assert.That(coordinator.TryCreateSnapshot(profile, out var accepted, out _, out error), Is.True, error);
            Assert.That(accepted.Facts, Has.Count.EqualTo(1));
            Assert.That(provider.CaptureCount, Is.EqualTo(2));
        }

        /// <summary>
        /// Confirms profile, lore, and current observations enter one detached snapshot.
        /// </summary>
        [Test]
        public void TryCreateSnapshot_ConfiguredSources_CombinesAllGrounding()
        {
            var profile = CreateCharacterProfile();
            var lore = CreateLoreProfile("city_founder", "Dawnfall has stood for centuries.");
            var root = Track(new GameObject("Context NPC"));
            var provider = root.AddComponent<TestNpcContextProvider>();
            provider.Facts = new[]
            {
                new NpcContextFact(
                    "gate_status",
                    NpcContextFactKind.Observation,
                    "The gate is closed.",
                    90)
            };
            var coordinator = root.AddComponent<NpcContextCoordinator>();
            ConfigureCoordinator(coordinator, lore, provider);

            var succeeded = coordinator.TryCreateSnapshot(
                profile,
                out var snapshot,
                out var omitted,
                out var error);

            Assert.That(succeeded, Is.True, error);
            Assert.That(snapshot.Background, Does.Contain("Dawnfall"));
            Assert.That(snapshot.Facts, Has.Count.EqualTo(2));
            Assert.That(snapshot.Facts[0].FactId, Is.EqualTo("gate_status"));
            Assert.That(omitted, Is.Empty);
        }

        /// <summary>
        /// Confirms each capture is current and provider exceptions remain a safe local failure.
        /// </summary>
        [Test]
        public void TryCreateSnapshot_ProviderChangesOrThrows_RefreshesSafely()
        {
            var profile = CreateCharacterProfile();
            var root = Track(new GameObject("Context NPC"));
            var provider = root.AddComponent<TestNpcContextProvider>();
            var coordinator = root.AddComponent<NpcContextCoordinator>();
            ConfigureCoordinator(coordinator, null, provider);
            provider.Facts = new[]
            {
                new NpcContextFact(
                    "gate_status",
                    NpcContextFactKind.Observation,
                    "The gate is closed.",
                    90)
            };
            Assert.That(coordinator.TryCreateSnapshot(
                profile,
                out var first,
                out _,
                out var firstError), Is.True, firstError);

            provider.Facts = new[]
            {
                new NpcContextFact(
                    "gate_status",
                    NpcContextFactKind.Observation,
                    "The gate is open.",
                    90)
            };
            Assert.That(coordinator.TryCreateSnapshot(
                profile,
                out var second,
                out _,
                out var secondError), Is.True, secondError);
            Assert.That(second.Revision, Is.Not.EqualTo(first.Revision));

            provider.ThrowOnCapture = true;
            Assert.That(coordinator.TryCreateSnapshot(
                profile,
                out _,
                out _,
                out var failure), Is.False);
            Assert.That(failure, Does.Not.Contain("Private provider failure"));
        }

        /// <summary>
        /// Creates one transient valid character with authored canon and rules.
        /// </summary>
        private CharacterProfile CreateCharacterProfile()
        {
            var profile = Track(ScriptableObject.CreateInstance<CharacterProfile>());
            var serialized = new SerializedObject(profile);
            serialized.FindProperty("characterId").stringValue = "sample-guard";
            serialized.FindProperty("displayName").stringValue = "Guard";
            serialized.FindProperty("personality").stringValue = "Disciplined.";
            serialized.FindProperty("speechStyle").stringValue = "Formal.";
            serialized.FindProperty("exampleDialogue").stringValue = "State your business.";
            serialized.FindProperty("background").stringValue =
                "Dawnfall's western gate is the character's assigned post.";
            var rules = serialized.FindProperty("behavioralRules");
            rules.arraySize = 1;
            rules.GetArrayElementAtIndex(0).stringValue = "Do not invent permissions.";
            serialized.ApplyModifiedPropertiesWithoutUndo();
            return profile;
        }

        /// <summary>
        /// Creates one transient lore asset containing a single validated fact.
        /// </summary>
        private NpcLoreProfile CreateLoreProfile(string factId, string statement)
        {
            var profile = Track(ScriptableObject.CreateInstance<NpcLoreProfile>());
            var serialized = new SerializedObject(profile);
            var entries = serialized.FindProperty("loreFacts");
            entries.arraySize = 1;
            var entry = entries.GetArrayElementAtIndex(0);
            entry.FindPropertyRelative("factId").stringValue = factId;
            entry.FindPropertyRelative("statement").stringValue = statement;
            entry.FindPropertyRelative("priority").intValue = 50;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            return profile;
        }

        /// <summary>
        /// Assigns optional lore and one provider through the coordinator's serialized boundary.
        /// </summary>
        private static void ConfigureCoordinator(
            NpcContextCoordinator coordinator,
            NpcLoreProfile lore,
            MonoBehaviour provider)
        {
            var serialized = new SerializedObject(coordinator);
            var loreProfiles = serialized.FindProperty("loreProfiles");
            loreProfiles.arraySize = lore == null ? 0 : 1;
            if (lore != null)
            {
                loreProfiles.GetArrayElementAtIndex(0).objectReferenceValue = lore;
            }

            var providers = serialized.FindProperty("contextProviderSources");
            providers.arraySize = 1;
            providers.GetArrayElementAtIndex(0).objectReferenceValue = provider;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        /// <summary>
        /// Tracks one transient Unity object for deterministic cleanup.
        /// </summary>
        private T Track<T>(T value) where T : Object
        {
            createdObjects.Add(value);
            return value;
        }
    }
}
