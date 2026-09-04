using System.IO;
using AiCharacterKit.Core;
using AiCharacterKit.Editor;
using AiCharacterKit.Unity;
using AiCharacterKit.Unity.Speech;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace AiCharacterKit.Core.Tests
{
    /// <summary>
    /// Verifies non-destructive Scene, Prefab, uGUI, and optional TTS composition.
    /// </summary>
    public sealed class CharacterBuilderServiceTests
    {
        private const string TestFolder = "Assets/__AICharacterKitPhase10Tests";
        private CharacterProfile profile;

        /// <summary>
        /// Creates isolated consumer data and an empty loaded Scene for each composition test.
        /// </summary>
        [SetUp]
        public void SetUp()
        {
            DeleteTestFolder();
            AssetDatabase.CreateFolder("Assets", "__AICharacterKitPhase10Tests");
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var draft = new CharacterProfileDraft
            {
                AssetName = "Phase10Guide",
                CharacterId = "phase10-test-guide",
                DisplayName = "Guide",
                Personality = "Helpful and observant.",
                SpeechStyle = "Warm and concise.",
                ExampleDialogue = "안내해 드리겠습니다.",
                DefaultEmotion = NpcEmotion.Happy
            };
            Assert.That(
                CharacterBuilderAssetService.TryCreateCharacterProfile(
                    draft,
                    TestFolder,
                    out profile,
                    out var error),
                Is.True,
                error);
        }

        /// <summary>
        /// Clears Scene objects, Undo state, and only the fixed test-owned asset folder.
        /// </summary>
        [TearDown]
        public void TearDown()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            Undo.ClearAll();
            DeleteTestFolder();
        }

        /// <summary>
        /// Confirms applying and reapplying a Scene target creates one stable conversation bridge.
        /// </summary>
        [Test]
        public void ApplySceneTarget_Twice_IsIdempotentAndPreservesUserComponents()
        {
            var target = CreateTarget("Scene NPC", out var presentation);
            var userCollider = target.AddComponent<BoxCollider>();
            var configuration = CreateConfiguration(target, presentation);

            Assert.That(
                CharacterBuilderService.TryApply(
                    configuration,
                    out var firstConversation,
                    out var firstError),
                Is.True,
                firstError);
            Assert.That(
                CharacterBuilderService.TryApply(
                    configuration,
                    out var secondConversation,
                    out var secondError),
                Is.True,
                secondError);

            Assert.That(secondConversation, Is.SameAs(firstConversation));
            Assert.That(target.GetComponents<NpcConversationBehaviour>(), Has.Length.EqualTo(1));
            Assert.That(target.GetComponent<BoxCollider>(), Is.SameAs(userCollider));
            AssertConversationReferences(
                secondConversation,
                profile,
                presentation,
                NpcConversationMode.Mock);
        }

        /// <summary>
        /// Confirms one Scene apply can be reverted without removing the consumer presentation.
        /// </summary>
        [Test]
        public void ApplySceneTarget_Undo_RemovesOnlyBuilderAddition()
        {
            var target = CreateTarget("Undo NPC", out var presentation);
            var configuration = CreateConfiguration(target, presentation);

            Assert.That(
                CharacterBuilderService.TryApply(
                    configuration,
                    out _,
                    out var error),
                Is.True,
                error);
            Assert.That(target.GetComponent<NpcConversationBehaviour>(), Is.Not.Null);

            Undo.PerformUndo();

            Assert.That(target.GetComponent<NpcConversationBehaviour>(), Is.Null);
            Assert.That(target.GetComponent<NpcTextPresentationDriver>(), Is.SameAs(presentation));
        }

        /// <summary>
        /// Confirms ambiguous or invalid input is rejected before adding any new component.
        /// </summary>
        [Test]
        public void ValidateInvalidConfiguration_DoesNotPartiallyMutateTarget()
        {
            var target = CreateTarget("Invalid NPC", out var presentation);
            var first = target.AddComponent<NpcConversationBehaviour>();
            var second = target.AddComponent<NpcConversationBehaviour>();
            var configuration = CreateConfiguration(target, presentation);

            var report = CharacterBuilderService.Validate(configuration);
            Assert.That(report.HasErrors, Is.True);
            Assert.That(
                CharacterBuilderService.TryApply(
                    configuration,
                    out var conversation,
                    out var error),
                Is.False);

            Assert.That(conversation, Is.Null);
            Assert.That(error, Does.Contain("multiple").IgnoreCase);
            Assert.That(target.GetComponents<NpcConversationBehaviour>(),
                Is.EqualTo(new[] { first, second }));
        }

        /// <summary>
        /// Confirms Backend and BackendSession modes retain their explicit loopback settings.
        /// </summary>
        [Test]
        public void ApplyBackendModes_PreservesModeSpecificEndpoints()
        {
            var backendTarget = CreateTarget("Backend NPC", out var backendPresentation);
            var backend = CreateConfiguration(backendTarget, backendPresentation);
            backend.ConversationMode = NpcConversationMode.Backend;
            backend.BackendEndpoint = "http://localhost:8787/v1/npc/respond";
            backend.BackendTimeoutSeconds = 17;
            Assert.That(
                CharacterBuilderService.TryApply(
                    backend,
                    out var backendConversation,
                    out var backendError),
                Is.True,
                backendError);
            AssertSerializedString(
                backendConversation,
                "backendEndpoint",
                backend.BackendEndpoint);
            Assert.That(
                new SerializedObject(backendConversation)
                    .FindProperty("backendTimeoutSeconds").intValue,
                Is.EqualTo(17));

            var sessionTarget = CreateTarget("Session NPC", out var sessionPresentation);
            var session = CreateConfiguration(sessionTarget, sessionPresentation);
            session.ConversationMode = NpcConversationMode.BackendSession;
            session.SessionBackendEndpoint = "http://localhost:8787/v2/npc/respond";
            session.SessionResetEndpoint = "http://localhost:8787/v2/npc/sessions/reset";
            Assert.That(
                CharacterBuilderService.TryApply(
                    session,
                    out var sessionConversation,
                    out var sessionError),
                Is.True,
                sessionError);
            AssertConversationReferences(
                sessionConversation,
                profile,
                sessionPresentation,
                NpcConversationMode.BackendSession);
            AssertSerializedString(
                sessionConversation,
                "sessionBackendEndpoint",
                session.SessionBackendEndpoint);
            AssertSerializedString(
                sessionConversation,
                "sessionResetEndpoint",
                session.SessionResetEndpoint);

            session.SessionBackendEndpoint = "https://example.com/v2/npc/respond";
            Assert.That(CharacterBuilderService.Validate(session).HasErrors, Is.True);
        }

        /// <summary>
        /// Confirms a complete existing text view is connected while an incomplete view blocks apply.
        /// </summary>
        [Test]
        public void OptionalConversationViews_CompleteConnectAndIncompleteBlocksApply()
        {
            var target = CreateTarget("UI NPC", out var presentation);
            var configuration = CreateConfiguration(target, presentation);
            var viewObject = new GameObject("Text View");
            var view = viewObject.AddComponent<NpcTextInputView>();
            configuration.TextInputView = view;

            Assert.That(CharacterBuilderService.Validate(configuration).HasErrors, Is.True);
            Assert.That(target.GetComponent<NpcConversationBehaviour>(), Is.Null);

            var inputField = CreateUiComponent<InputField>("Input");
            var sendButton = CreateUiComponent<Button>("Send");
            SetObjectReference(view, "inputField", inputField);
            SetObjectReference(view, "sendButton", sendButton);
            var sessionObject = new GameObject("Session View");
            var sessionView = sessionObject.AddComponent<NpcSessionControlView>();
            SetObjectReference(
                sessionView,
                "resetButton",
                CreateUiComponent<Button>("Reset"));
            SetObjectReference(
                sessionView,
                "memoryStatusText",
                CreateUiComponent<Text>("Memory Status"));
            configuration.SessionControlView = sessionView;
            configuration.ConversationMode = NpcConversationMode.BackendSession;
            Assert.That(
                CharacterBuilderService.TryApply(
                    configuration,
                    out var conversation,
                    out var error),
                Is.True,
                error);
            Assert.That(
                GetObjectReference(view, "conversationBehaviour"),
                Is.SameAs(conversation));
            Assert.That(
                GetObjectReference(sessionView, "conversationBehaviour"),
                Is.SameAs(conversation));
        }

        /// <summary>
        /// Confirms TTS uses a dedicated source, reuses its stack, and is never deleted when disabled.
        /// </summary>
        [Test]
        public void ApplySpeech_UsesDedicatedAudioAndDisablingIsNonDestructive()
        {
            var target = CreateTarget("Speech NPC", out var presentation);
            var gameplayAudio = target.AddComponent<AudioSource>();
            var voiceDraft = new VoiceProfileDraft
            {
                AssetName = "Phase10Voice",
                VoicePresetId = "phase10-test-voice"
            };
            Assert.That(
                CharacterBuilderAssetService.TryCreateVoiceProfile(
                    voiceDraft,
                    TestFolder,
                    out var voiceProfile,
                    out var voiceError),
                Is.True,
                voiceError);

            var configuration = CreateConfiguration(target, presentation);
            configuration.ConfigureSpeech = true;
            configuration.VoiceProfile = voiceProfile;
            Assert.That(
                CharacterBuilderService.TryApply(
                    configuration,
                    out var conversation,
                    out var error),
                Is.True,
                error);

            var playback = target.GetComponent<UnityPcmSpeechPlaybackDriver>();
            var speechOutput = target.GetComponent<NpcSpeechOutput>();
            var decorator = target.GetComponent<SpeechAugmentedPresentationDriver>();
            var dedicatedAudio = GetObjectReference(playback, "audioSource") as AudioSource;
            Assert.That(dedicatedAudio, Is.Not.Null);
            Assert.That(dedicatedAudio, Is.Not.SameAs(gameplayAudio));
            Assert.That(target.GetComponents<AudioSource>(), Has.Length.EqualTo(2));
            Assert.That(GetObjectReference(speechOutput, "voiceProfile"), Is.SameAs(voiceProfile));
            Assert.That(GetObjectReference(decorator, "visualDriverSource"), Is.SameAs(presentation));
            Assert.That(GetObjectReference(conversation, "presentationDriverSource"), Is.SameAs(decorator));

            Assert.That(
                CharacterBuilderService.TryApply(
                    configuration,
                    out _,
                    out var reapplyError),
                Is.True,
                reapplyError);
            Assert.That(target.GetComponents<NpcSpeechOutput>(), Has.Length.EqualTo(1));
            Assert.That(target.GetComponents<AudioSource>(), Has.Length.EqualTo(2));

            configuration.ConfigureSpeech = false;
            Assert.That(
                CharacterBuilderService.TryApply(
                    configuration,
                    out var visualConversation,
                    out var disableError),
                Is.True,
                disableError);
            Assert.That(target.GetComponent<NpcSpeechOutput>(), Is.SameAs(speechOutput));
            Assert.That(target.GetComponent<SpeechAugmentedPresentationDriver>(),
                Is.SameAs(decorator));
            Assert.That(
                AssetDatabase.LoadAssetAtPath<NpcVoiceProfile>(
                    AssetDatabase.GetAssetPath(voiceProfile)),
                Is.SameAs(voiceProfile));
            Assert.That(
                GetObjectReference(visualConversation, "presentationDriverSource"),
                Is.SameAs(presentation));
        }

        /// <summary>
        /// Confirms one complete existing speech view receives the generated output reference.
        /// </summary>
        [Test]
        public void ApplySpeech_WithExistingControlView_ConnectsOnlyOutputReference()
        {
            var target = CreateTarget("Speech UI NPC", out var presentation);
            var voiceDraft = new VoiceProfileDraft
            {
                AssetName = "SpeechUiVoice",
                VoicePresetId = "speech-ui-voice"
            };
            Assert.That(
                CharacterBuilderAssetService.TryCreateVoiceProfile(
                    voiceDraft,
                    TestFolder,
                    out var voiceProfile,
                    out var voiceError),
                Is.True,
                voiceError);

            var viewObject = new GameObject("Speech View");
            var view = viewObject.AddComponent<NpcSpeechControlView>();
            var toggle = CreateUiComponent<Toggle>("Toggle");
            var stop = CreateUiComponent<Button>("Stop");
            var status = CreateUiComponent<Text>("Status");
            var disclosure = CreateUiComponent<Text>("Disclosure");
            SetObjectReference(view, "speechToggle", toggle);
            SetObjectReference(view, "stopButton", stop);
            SetObjectReference(view, "speechStatusText", status);
            SetObjectReference(view, "disclosureText", disclosure);

            var configuration = CreateConfiguration(target, presentation);
            configuration.ConfigureSpeech = true;
            configuration.VoiceProfile = voiceProfile;
            configuration.SpeechControlView = view;
            Assert.That(
                CharacterBuilderService.TryApply(
                    configuration,
                    out _,
                    out var error),
                Is.True,
                error);
            Assert.That(
                GetObjectReference(view, "speechOutput"),
                Is.SameAs(target.GetComponent<NpcSpeechOutput>()));
        }

        /// <summary>
        /// Confirms a regular Prefab is configured through isolated contents and survives reload.
        /// </summary>
        [Test]
        public void ApplyRegularPrefab_ReloadsWithInternalReferences()
        {
            var prefabPath = TestFolder + "/Guide.prefab";
            var source = CreateTarget("Guide Prefab", out _);
            PrefabUtility.SaveAsPrefabAsset(source, prefabPath);
            Object.DestroyImmediate(source);

            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            var presentation = prefab.GetComponent<NpcTextPresentationDriver>();
            var configuration = CreateConfiguration(prefab, presentation);
            Assert.That(
                CharacterBuilderService.TryApply(
                    configuration,
                    out var savedConversation,
                    out var error),
                Is.True,
                error);

            prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            presentation = prefab.GetComponent<NpcTextPresentationDriver>();
            savedConversation = prefab.GetComponent<NpcConversationBehaviour>();
            Assert.That(savedConversation, Is.Not.Null);
            AssertConversationReferences(
                savedConversation,
                profile,
                presentation,
                NpcConversationMode.Mock);

            configuration = CreateConfiguration(prefab, presentation);
            Assert.That(
                CharacterBuilderService.TryApply(
                    configuration,
                    out _,
                    out var reapplyError),
                Is.True,
                reapplyError);
            Assert.That(
                AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath)
                    .GetComponents<NpcConversationBehaviour>(),
                Has.Length.EqualTo(1));
        }

        /// <summary>
        /// Confirms a Prefab variant remains a variant after non-destructive configuration.
        /// </summary>
        [Test]
        public void ApplyVariantPrefab_PreservesVariantType()
        {
            var basePath = TestFolder + "/Base.prefab";
            var variantPath = TestFolder + "/Variant.prefab";
            var source = CreateTarget("Base NPC", out _);
            var basePrefab = PrefabUtility.SaveAsPrefabAsset(source, basePath);
            Object.DestroyImmediate(source);

            var instance = PrefabUtility.InstantiatePrefab(basePrefab) as GameObject;
            Assert.That(instance, Is.Not.Null);
            instance.name = "Variant NPC";
            PrefabUtility.SaveAsPrefabAsset(instance, variantPath);
            Object.DestroyImmediate(instance);

            var variant = AssetDatabase.LoadAssetAtPath<GameObject>(variantPath);
            Assert.That(PrefabUtility.GetPrefabAssetType(variant),
                Is.EqualTo(PrefabAssetType.Variant));
            var configuration = CreateConfiguration(
                variant,
                variant.GetComponent<NpcTextPresentationDriver>());
            Assert.That(
                CharacterBuilderService.TryApply(
                    configuration,
                    out _,
                    out var error),
                Is.True,
                error);
            variant = AssetDatabase.LoadAssetAtPath<GameObject>(variantPath);
            Assert.That(PrefabUtility.GetPrefabAssetType(variant),
                Is.EqualTo(PrefabAssetType.Variant));
            Assert.That(variant.GetComponent<NpcConversationBehaviour>(), Is.Not.Null);
        }

        /// <summary>
        /// Confirms a Prefab target rejects Scene-owned optional references before saving.
        /// </summary>
        [Test]
        public void ValidatePrefab_WithExternalSceneView_IsRejected()
        {
            var prefabPath = TestFolder + "/ExternalReference.prefab";
            var source = CreateTarget("Prefab NPC", out _);
            PrefabUtility.SaveAsPrefabAsset(source, prefabPath);
            Object.DestroyImmediate(source);
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);

            var viewObject = new GameObject("External View");
            var view = viewObject.AddComponent<NpcTextInputView>();
            SetObjectReference(
                view,
                "inputField",
                CreateUiComponent<InputField>("Input"));
            SetObjectReference(
                view,
                "sendButton",
                CreateUiComponent<Button>("Send"));
            var configuration = CreateConfiguration(
                prefab,
                prefab.GetComponent<NpcTextPresentationDriver>());
            configuration.TextInputView = view;

            Assert.That(CharacterBuilderService.Validate(configuration).HasErrors, Is.True);
            Assert.That(prefab.GetComponent<NpcConversationBehaviour>(), Is.Null);
        }

        /// <summary>
        /// Confirms a Scene Prefab instance receives only local component overrides.
        /// </summary>
        [Test]
        public void ApplyScenePrefabInstance_LeavesSourcePrefabUnchanged()
        {
            var prefabPath = TestFolder + "/InstanceSource.prefab";
            var source = CreateTarget("Instance Source", out _);
            PrefabUtility.SaveAsPrefabAsset(source, prefabPath);
            Object.DestroyImmediate(source);

            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            var instance = PrefabUtility.InstantiatePrefab(prefab) as GameObject;
            Assert.That(instance, Is.Not.Null);
            var presentation = instance.GetComponent<NpcTextPresentationDriver>();
            var configuration = CreateConfiguration(instance, presentation);

            Assert.That(
                CharacterBuilderService.TryApply(
                    configuration,
                    out var conversation,
                    out var error),
                Is.True,
                error);

            Assert.That(conversation, Is.Not.Null);
            Assert.That(PrefabUtility.IsAddedComponentOverride(conversation), Is.True);
            Assert.That(
                AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath)
                    .GetComponent<NpcConversationBehaviour>(),
                Is.Null);
        }

        /// <summary>
        /// Confirms Prefab Views cannot retain uGUI controls from another Prefab asset.
        /// </summary>
        [Test]
        public void ValidatePrefab_WithExternalUiControls_IsRejected()
        {
            var externalPath = TestFolder + "/ExternalControls.prefab";
            var externalRoot = new GameObject("External Controls");
            var externalInput = CreateUiComponent<InputField>("External Input");
            externalInput.transform.SetParent(externalRoot.transform, false);
            var externalButton = CreateUiComponent<Button>("External Button");
            externalButton.transform.SetParent(externalRoot.transform, false);
            PrefabUtility.SaveAsPrefabAsset(externalRoot, externalPath);
            Object.DestroyImmediate(externalRoot);

            var targetPath = TestFolder + "/UiTarget.prefab";
            var source = CreateTarget("UI Target", out _);
            var viewObject = new GameObject("Text View");
            viewObject.transform.SetParent(source.transform, false);
            viewObject.AddComponent<NpcTextInputView>();
            PrefabUtility.SaveAsPrefabAsset(source, targetPath);
            Object.DestroyImmediate(source);

            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(targetPath);
            var view = prefab.GetComponentInChildren<NpcTextInputView>(true);
            var externalPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(externalPath);
            SetObjectReference(
                view,
                "inputField",
                externalPrefab.GetComponentInChildren<InputField>(true));
            SetObjectReference(
                view,
                "sendButton",
                externalPrefab.GetComponentInChildren<Button>(true));
            EditorUtility.SetDirty(view);
            AssetDatabase.SaveAssets();

            var configuration = CreateConfiguration(
                prefab,
                prefab.GetComponent<NpcTextPresentationDriver>());
            configuration.TextInputView = view;
            var report = CharacterBuilderService.Validate(configuration);

            Assert.That(report.HasErrors, Is.True);
            Assert.That(ReportContains(report, "contained inside"), Is.True);
            Assert.That(prefab.GetComponent<NpcConversationBehaviour>(), Is.Null);
        }

        /// <summary>
        /// Confirms V4 grounding composition is valid, idempotent, and preserves provider wiring.
        /// </summary>
        [Test]
        public void ApplyGrounding_Twice_CreatesOneCoordinatorAndV4Endpoints()
        {
            var loreDraft = new LoreProfileDraft { AssetName = "GuardLore" };
            loreDraft.LoreFacts.Add(new LoreEntryDraft
            {
                FactId = "city_name",
                Statement = "The city is called Dawnfall.",
                Priority = 50
            });
            Assert.That(CharacterBuilderAssetService.TryCreateLoreProfile(
                loreDraft,
                TestFolder,
                out var lore,
                out var loreError), Is.True, loreError);
            var target = CreateTarget("Grounded NPC", out var presentation);
            var provider = target.AddComponent<TestNpcContextProvider>();
            provider.Facts = new[]
            {
                new NpcContextFact(
                    "gate_status",
                    NpcContextFactKind.Observation,
                    "The gate is closed.",
                    90)
            };
            var configuration = CreateConfiguration(target, presentation);
            configuration.ConversationMode = NpcConversationMode.BackendContext;
            configuration.ConfigureGrounding = true;
            configuration.LoreProfiles = new[] { lore };
            configuration.ContextProviderSources = new MonoBehaviour[] { provider };
            configuration.ContextBackendEndpoint =
                "http://localhost:8787/v4/npc/respond";
            configuration.ContextResetEndpoint =
                "http://localhost:8787/v4/npc/sessions/reset";

            Assert.That(CharacterBuilderService.TryApply(
                configuration,
                out var first,
                out var firstError), Is.True, firstError);
            Assert.That(CharacterBuilderService.TryApply(
                configuration,
                out var second,
                out var secondError), Is.True, secondError);

            var coordinator = target.GetComponent<NpcContextCoordinator>();
            Assert.That(second, Is.SameAs(first));
            Assert.That(target.GetComponents<NpcContextCoordinator>(), Has.Length.EqualTo(1));
            Assert.That(GetObjectReference(second, "contextCoordinator"),
                Is.SameAs(coordinator));
            AssertSerializedString(
                second,
                "contextBackendEndpoint",
                configuration.ContextBackendEndpoint);
            Assert.That(coordinator.TryCreateSnapshot(
                profile,
                out var snapshot,
                out _,
                out var contextError), Is.True, contextError);
            Assert.That(snapshot.Facts, Has.Count.EqualTo(2));
        }

        /// <summary>
        /// Confirms Model Prefabs and package namespace paths are not writable targets.
        /// </summary>
        [Test]
        public void ValidateTarget_ModelAndPackagePaths_AreRejected()
        {
            Assert.That(
                CharacterBuilderService.IsWritableConsumerPrefabPath(
                    "Packages/com.example.framework/Npc.prefab"),
                Is.False);
            Assert.That(
                CharacterBuilderService.IsWritableConsumerPrefabPath(
                    "Assets/Characters/Npc.prefab"),
                Is.True);

            var modelPath = TestFolder + "/Triangle.obj";
            File.WriteAllText(
                Path.GetFullPath(modelPath),
                "o Triangle\nv 0 0 0\nv 1 0 0\nv 0 1 0\nf 1 2 3\n");
            AssetDatabase.ImportAsset(
                modelPath,
                ImportAssetOptions.ForceSynchronousImport);
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(modelPath);
            Assert.That(model, Is.Not.Null);
            Assert.That(
                PrefabUtility.GetPrefabAssetType(model),
                Is.EqualTo(PrefabAssetType.Model));

            var configuration = new CharacterBuilderConfiguration
            {
                Target = model,
                CharacterProfile = profile
            };
            var report = CharacterBuilderService.Validate(configuration);
            Assert.That(report.HasErrors, Is.True);
            Assert.That(ReportContains(report, "regular or variant"), Is.True);
        }

        /// <summary>
        /// Creates one consumer target with an existing replaceable visual presentation driver.
        /// </summary>
        private static GameObject CreateTarget(
            string name,
            out NpcTextPresentationDriver presentation)
        {
            var target = new GameObject(name);
            presentation = target.AddComponent<NpcTextPresentationDriver>();
            return target;
        }

        /// <summary>
        /// Creates one temporary uGUI component on the required RectTransform host.
        /// </summary>
        private static T CreateUiComponent<T>(string name)
            where T : Component
        {
            return new GameObject(name, typeof(RectTransform)).AddComponent<T>();
        }

        /// <summary>
        /// Builds one default valid Mock configuration for a selected target and presentation.
        /// </summary>
        private CharacterBuilderConfiguration CreateConfiguration(
            GameObject target,
            MonoBehaviour presentation)
        {
            return new CharacterBuilderConfiguration
            {
                Target = target,
                CharacterProfile = profile,
                VisualPresentationDriver = presentation,
                ConversationMode = NpcConversationMode.Mock
            };
        }

        /// <summary>
        /// Verifies the profile, presentation source, and serialized mode on one bridge.
        /// </summary>
        private static void AssertConversationReferences(
            NpcConversationBehaviour conversation,
            CharacterProfile expectedProfile,
            MonoBehaviour expectedPresentation,
            NpcConversationMode expectedMode)
        {
            var serializedConversation = new SerializedObject(conversation);
            Assert.That(
                serializedConversation.FindProperty("characterProfile")
                    .objectReferenceValue,
                Is.SameAs(expectedProfile));
            Assert.That(
                serializedConversation.FindProperty("presentationDriverSource")
                    .objectReferenceValue,
                Is.SameAs(expectedPresentation));
            Assert.That(
                serializedConversation.FindProperty("conversationMode").intValue,
                Is.EqualTo((int)expectedMode));
        }

        /// <summary>
        /// Writes one private object reference through supported Editor serialization.
        /// </summary>
        private static void SetObjectReference(
            Object target,
            string propertyName,
            Object value)
        {
            var serializedObject = new SerializedObject(target);
            serializedObject.FindProperty(propertyName).objectReferenceValue = value;
            serializedObject.ApplyModifiedPropertiesWithoutUndo();
        }

        /// <summary>
        /// Reads one private object reference through supported Editor serialization.
        /// </summary>
        private static Object GetObjectReference(Object target, string propertyName)
        {
            return new SerializedObject(target)
                .FindProperty(propertyName)
                .objectReferenceValue;
        }

        /// <summary>
        /// Verifies one private serialized string without exposing it through Runtime APIs.
        /// </summary>
        private static void AssertSerializedString(
            Object target,
            string propertyName,
            string expected)
        {
            Assert.That(
                new SerializedObject(target).FindProperty(propertyName).stringValue,
                Is.EqualTo(expected));
        }

        /// <summary>
        /// Finds one stable diagnostic fragment without depending on diagnostic ordering.
        /// </summary>
        private static bool ReportContains(
            CharacterBuilderValidationReport report,
            string fragment)
        {
            foreach (var diagnostic in report.Diagnostics)
            {
                if (diagnostic.Message.Contains(fragment))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Deletes only the fixed test-owned Assets folder when it exists.
        /// </summary>
        private static void DeleteTestFolder()
        {
            if (AssetDatabase.IsValidFolder(TestFolder))
            {
                AssetDatabase.DeleteAsset(TestFolder);
                AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            }
        }
    }
}
