using System.Threading;
using System.Threading.Tasks;
using AiCharacterKit.Core;
using AiCharacterKit.Editor;
using AiCharacterKit.Unity;
using AiCharacterKit.Unity.Actions;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace AiCharacterKit.Core.Tests
{
    /// <summary>
    /// Verifies action profile authoring and non-destructive Character Builder composition.
    /// </summary>
    public sealed class CharacterBuilderActionTests
    {
        private const string TestFolder = "Assets/__AICharacterKitPhase11Tests";
        private CharacterProfile characterProfile;
        private NpcActionProfile actionProfile;

        /// <summary>
        /// Creates isolated consumer-owned character and action assets for each test.
        /// </summary>
        [SetUp]
        public void SetUp()
        {
            DeleteTestFolder();
            AssetDatabase.CreateFolder("Assets", "__AICharacterKitPhase11Tests");
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            Assert.That(
                CharacterBuilderAssetService.TryCreateCharacterProfile(
                    new CharacterProfileDraft
                    {
                        AssetName = "Guide",
                        CharacterId = "phase11-guide",
                        DisplayName = "Guide",
                        Personality = "Helpful",
                        SpeechStyle = "Brief",
                        ExampleDialogue = "Hello.",
                        DefaultEmotion = NpcEmotion.Neutral
                    },
                    TestFolder,
                    out characterProfile,
                    out var characterError),
                Is.True,
                characterError);
            Assert.That(
                CharacterBuilderAssetService.TryCreateActionProfile(
                    CreateActionDraft(),
                    TestFolder,
                    out actionProfile,
                    out var actionError),
                Is.True,
                actionError);
        }

        /// <summary>
        /// Removes only the fixed test-owned asset folder and resets the loaded Scene.
        /// </summary>
        [TearDown]
        public void TearDown()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            Undo.ClearAll();
            DeleteTestFolder();
        }

        /// <summary>
        /// Confirms action apply and reapply retain one coordinator and consumer handler reference.
        /// </summary>
        [Test]
        public void TryApply_ActionSceneTwice_IsIdempotentAndConsumerOwned()
        {
            var target = new GameObject("Action NPC");
            var presentation = target.AddComponent<TestPresentationDriver>();
            var handler = target.AddComponent<TestActionHandler>();
            var configuration = CreateConfiguration(target, presentation, handler);

            Assert.That(
                CharacterBuilderService.TryApply(
                    configuration,
                    out var first,
                    out var firstError),
                Is.True,
                firstError);
            Assert.That(
                CharacterBuilderService.TryApply(
                    configuration,
                    out var second,
                    out var secondError),
                Is.True,
                secondError);

            Assert.That(second, Is.SameAs(first));
            Assert.That(target.GetComponents<NpcActionCoordinator>(), Has.Length.EqualTo(1));
            Assert.That(target.GetComponent<TestActionHandler>(), Is.SameAs(handler));
            var conversation = new SerializedObject(second);
            Assert.That(
                conversation.FindProperty("actionCoordinator").objectReferenceValue,
                Is.SameAs(target.GetComponent<NpcActionCoordinator>()));
        }

        /// <summary>
        /// Confirms a missing action handler blocks apply before any Kit component is added.
        /// </summary>
        [Test]
        public void TryApply_MissingHandler_DoesNotPartiallyMutateTarget()
        {
            var target = new GameObject("Invalid Action NPC");
            var presentation = target.AddComponent<TestPresentationDriver>();
            var configuration = CreateConfiguration(target, presentation, null);

            Assert.That(CharacterBuilderService.Validate(configuration).HasErrors, Is.True);
            Assert.That(
                CharacterBuilderService.TryApply(configuration, out _, out _),
                Is.False);
            Assert.That(target.GetComponent<NpcConversationBehaviour>(), Is.Null);
            Assert.That(target.GetComponent<NpcActionCoordinator>(), Is.Null);
        }

        /// <summary>
        /// Confirms the Builder preview reports deterministic matched and selected action data.
        /// </summary>
        [Test]
        public void TryPreviewMockAction_ExactExample_IsDeterministic()
        {
            var characterDraft = new CharacterProfileDraft
            {
                AssetName = "Guide",
                CharacterId = "phase11-guide",
                DisplayName = "Guide",
                Personality = "Helpful",
                SpeechStyle = "Brief",
                ExampleDialogue = "Hello.",
                DefaultEmotion = NpcEmotion.Neutral
            };
            var actionDraft = CreateActionDraft();

            Assert.That(
                CharacterBuilderAssetService.TryPreviewMockAction(
                    characterDraft,
                    actionDraft,
                    "  HELLO  ",
                    out var response,
                    out var selected,
                    out var error),
                Is.True,
                error);
            Assert.That(response.MatchedTriggerIds, Is.EqualTo(new[] { "greet_player" }));
            Assert.That(selected, Is.Not.Null);
            Assert.That(selected.ActionId, Is.EqualTo("wave_to_player"));
        }

        /// <summary>
        /// Confirms invalid duplicate action data and package-owned save paths are rejected.
        /// </summary>
        [Test]
        public void TryCreateActionProfile_InvalidOrReadOnlyInput_DoesNotCreateAsset()
        {
            var duplicateDraft = CreateActionDraft();
            duplicateDraft.Bindings.Add(new ActionBindingDraft
            {
                TriggerId = "second_greeting",
                ConditionDescription = "The player greets again.",
                ExampleUserText = "hi",
                ActionId = "wave_to_player",
                Priority = 1
            });

            Assert.That(
                CharacterBuilderAssetService.TryCreateActionProfile(
                    duplicateDraft,
                    TestFolder,
                    out var duplicate,
                    out _),
                Is.False);
            Assert.That(duplicate, Is.Null);
            Assert.That(
                CharacterBuilderAssetService.TryCreateActionProfile(
                    CreateActionDraft(),
                    "Packages/com.aicharacterkit.framework",
                    out var readOnly,
                    out _),
                Is.False);
            Assert.That(readOnly, Is.Null);
            Assert.That(
                AssetDatabase.FindAssets(
                    "t:NpcActionProfile",
                    new[] { TestFolder }),
                Has.Length.EqualTo(1));
        }

        /// <summary>
        /// Confirms an existing consumer action profile is edited without replacing its asset.
        /// </summary>
        [Test]
        public void TryUpdateActionProfile_ValidDraft_PreservesAssetIdentity()
        {
            var originalPath = AssetDatabase.GetAssetPath(actionProfile);
            var draft = CreateActionDraft();
            draft.AssetName = "UpdatedGuideActions";
            draft.Bindings[0].ExampleUserText = "good morning";
            draft.Bindings[0].Priority = 42;

            Assert.That(
                CharacterBuilderAssetService.TryUpdateActionProfile(
                    actionProfile,
                    draft,
                    out var error),
                Is.True,
                error);

            Assert.That(AssetDatabase.GetAssetPath(actionProfile), Is.EqualTo(originalPath));
            var definition = actionProfile.CreateDefinitions()[0];
            Assert.That(definition.ExampleUserText, Is.EqualTo("good morning"));
            Assert.That(definition.Priority, Is.EqualTo(42));
        }

        /// <summary>
        /// Confirms BackendActions retains explicit loopback V3 endpoints and timeout settings.
        /// </summary>
        [Test]
        public void TryApply_BackendActions_RetainsV3SettingsAndRejectsRemoteUrl()
        {
            var target = new GameObject("Backend Action NPC");
            var presentation = target.AddComponent<TestPresentationDriver>();
            var handler = target.AddComponent<TestActionHandler>();
            var configuration = CreateConfiguration(target, presentation, handler);
            configuration.ConversationMode = NpcConversationMode.BackendActions;
            configuration.ActionBackendEndpoint =
                "http://localhost:8787/v3/npc/respond";
            configuration.ActionResetEndpoint =
                "http://127.0.0.1:8787/v3/npc/sessions/reset";
            configuration.BackendTimeoutSeconds = 19;

            Assert.That(
                CharacterBuilderService.TryApply(
                    configuration,
                    out var conversation,
                    out var error),
                Is.True,
                error);
            var serialized = new SerializedObject(conversation);
            Assert.That(
                serialized.FindProperty("conversationMode").enumValueIndex,
                Is.EqualTo((int)NpcConversationMode.BackendActions));
            Assert.That(
                serialized.FindProperty("actionBackendEndpoint").stringValue,
                Is.EqualTo(configuration.ActionBackendEndpoint));
            Assert.That(
                serialized.FindProperty("actionResetEndpoint").stringValue,
                Is.EqualTo(configuration.ActionResetEndpoint));
            Assert.That(
                serialized.FindProperty("backendTimeoutSeconds").intValue,
                Is.EqualTo(19));

            configuration.ActionBackendEndpoint =
                "https://example.com/v3/npc/respond";
            Assert.That(CharacterBuilderService.Validate(configuration).HasErrors, Is.True);
        }

        /// <summary>
        /// Confirms regular Prefab action references survive isolated save, reload, and reapply.
        /// </summary>
        [Test]
        public void TryApply_ActionRegularPrefab_PreservesInternalHandlerReference()
        {
            var prefabPath = TestFolder + "/ActionGuide.prefab";
            var source = new GameObject("Action Guide Prefab");
            source.AddComponent<NpcTextPresentationDriver>();
            AddPersistentSampleActionHandler(source);
            PrefabUtility.SaveAsPrefabAsset(source, prefabPath);
            Object.DestroyImmediate(source);

            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            var configuration = CreateConfiguration(
                prefab,
                prefab.GetComponent<NpcTextPresentationDriver>(),
                FindActionHandler(prefab));
            Assert.That(
                CharacterBuilderService.TryApply(configuration, out _, out var error),
                Is.True,
                error);

            prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            AssertActionComposition(prefab);
            configuration = CreateConfiguration(
                prefab,
                prefab.GetComponent<NpcTextPresentationDriver>(),
                FindActionHandler(prefab));
            Assert.That(
                CharacterBuilderService.TryApply(configuration, out _, out error),
                Is.True,
                error);
            AssertActionComposition(
                AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath));
        }

        /// <summary>
        /// Confirms an action-enabled Prefab variant remains a variant after configuration.
        /// </summary>
        [Test]
        public void TryApply_ActionVariantPrefab_PreservesVariantAndReferences()
        {
            var basePath = TestFolder + "/ActionBase.prefab";
            var variantPath = TestFolder + "/ActionVariant.prefab";
            var source = new GameObject("Action Base");
            source.AddComponent<NpcTextPresentationDriver>();
            AddPersistentSampleActionHandler(source);
            var basePrefab = PrefabUtility.SaveAsPrefabAsset(source, basePath);
            Object.DestroyImmediate(source);
            var instance = PrefabUtility.InstantiatePrefab(basePrefab) as GameObject;
            Assert.That(instance, Is.Not.Null);
            instance.name = "Action Variant";
            PrefabUtility.SaveAsPrefabAsset(instance, variantPath);
            Object.DestroyImmediate(instance);

            var variant = AssetDatabase.LoadAssetAtPath<GameObject>(variantPath);
            var configuration = CreateConfiguration(
                variant,
                variant.GetComponent<NpcTextPresentationDriver>(),
                FindActionHandler(variant));
            Assert.That(
                CharacterBuilderService.TryApply(configuration, out _, out var error),
                Is.True,
                error);

            variant = AssetDatabase.LoadAssetAtPath<GameObject>(variantPath);
            Assert.That(
                PrefabUtility.GetPrefabAssetType(variant),
                Is.EqualTo(PrefabAssetType.Variant));
            AssertActionComposition(variant);
        }

        /// <summary>
        /// Creates one valid detached action profile draft.
        /// </summary>
        private static ActionProfileDraft CreateActionDraft()
        {
            var draft = new ActionProfileDraft { AssetName = "GuideActions" };
            var binding = draft.Bindings[0];
            binding.TriggerId = "greet_player";
            binding.ConditionDescription = "The player greets the guide.";
            binding.ExampleUserText = "hello";
            binding.ActionId = "wave_to_player";
            binding.Priority = 10;
            return draft;
        }

        /// <summary>
        /// Creates one action-enabled Mock builder configuration.
        /// </summary>
        private CharacterBuilderConfiguration CreateConfiguration(
            GameObject target,
            MonoBehaviour presentation,
            MonoBehaviour handler)
        {
            return new CharacterBuilderConfiguration
            {
                Target = target,
                CharacterProfile = characterProfile,
                VisualPresentationDriver = presentation,
                ConversationMode = NpcConversationMode.Mock,
                ConfigureActions = true,
                ActionProfile = actionProfile,
                ActionHandlerSources = handler == null
                    ? System.Array.Empty<MonoBehaviour>()
                    : new[] { handler }
            };
        }

        /// <summary>
        /// Confirms one persisted target retains exactly one valid action composition.
        /// </summary>
        private static void AssertActionComposition(GameObject target)
        {
            Assert.That(
                target.GetComponents<NpcConversationBehaviour>(),
                Has.Length.EqualTo(1));
            Assert.That(
                target.GetComponents<NpcActionCoordinator>(),
                Has.Length.EqualTo(1));
            var coordinator = target.GetComponent<NpcActionCoordinator>();
            Assert.That(
                coordinator.TryValidateConfiguration(out var error),
                Is.True,
                error);
            var serialized = new SerializedObject(coordinator);
            var handlers = serialized.FindProperty("actionHandlerSources");
            Assert.That(handlers.arraySize, Is.EqualTo(1));
            Assert.That(
                handlers.GetArrayElementAtIndex(0).objectReferenceValue,
                Is.SameAs(FindActionHandler(target)));
        }

        /// <summary>
        /// Adds the imported sample's persistent consumer handler without a test assembly dependency.
        /// </summary>
        private static MonoBehaviour AddPersistentSampleActionHandler(GameObject target)
        {
            foreach (var type in TypeCache.GetTypesDerivedFrom<NpcActionHandlerBase>())
            {
                if (type.FullName ==
                    "AiCharacterKit.Samples.Actions.SampleWaveActionHandler")
                {
                    var component = target.AddComponent(type) as MonoBehaviour;
                    Assert.That(component, Is.Not.Null);
                    return component;
                }
            }

            Assert.Fail(
                "Import AI NPC Prototypes before running Prefab action tests.");
            return null;
        }

        /// <summary>
        /// Finds the one consumer action handler attached to a test target.
        /// </summary>
        private static MonoBehaviour FindActionHandler(GameObject target)
        {
            foreach (var component in target.GetComponents<MonoBehaviour>())
            {
                if (component is INpcActionHandler handler
                    && handler.ActionId == "wave_to_player")
                {
                    return component;
                }
            }

            return null;
        }

        /// <summary>
        /// Deletes only the fixed test-owned Assets folder.
        /// </summary>
        private static void DeleteTestFolder()
        {
            if (AssetDatabase.IsValidFolder(TestFolder))
            {
                AssetDatabase.DeleteAsset(TestFolder);
            }
        }

        private sealed class TestActionHandler : MonoBehaviour, INpcActionHandler
        {
            public string ActionId => "wave_to_player";

            /// <summary>
            /// Allows this deterministic builder test action.
            /// </summary>
            public bool CanExecute(
                NpcActionContext context,
                out string rejectionReason)
            {
                rejectionReason = string.Empty;
                return true;
            }

            /// <summary>
            /// Completes immediately without creating game-specific state.
            /// </summary>
            public Task ExecuteAsync(
                NpcActionContext context,
                CancellationToken cancellationToken)
            {
                return Task.CompletedTask;
            }
        }

        private sealed class TestPresentationDriver : MonoBehaviour, INpcPresentationDriver
        {
            /// <summary>
            /// Accepts test dialogue.
            /// </summary>
            public void PresentDialogue(string dialogue)
            {
            }

            /// <summary>
            /// Accepts test emotion.
            /// </summary>
            public void PresentEmotion(NpcEmotion emotion)
            {
            }

            /// <summary>
            /// Accepts test gesture.
            /// </summary>
            public void PresentGesture(NpcGesture gesture)
            {
            }

            /// <summary>
            /// Accepts test busy state.
            /// </summary>
            public void SetBusy(bool isBusy)
            {
            }

            /// <summary>
            /// Accepts test errors.
            /// </summary>
            public void PresentError(string message)
            {
            }

            /// <summary>
            /// Accepts test cancellation.
            /// </summary>
            public void PresentCancellation()
            {
            }
        }
    }
}
