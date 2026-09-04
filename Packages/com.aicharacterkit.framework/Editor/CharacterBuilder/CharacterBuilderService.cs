using System;
using System.Collections.Generic;
using AiCharacterKit.Core;
using AiCharacterKit.Unity;
using AiCharacterKit.Unity.Actions;
using AiCharacterKit.Unity.Networking;
using AiCharacterKit.Unity.Speech;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace AiCharacterKit.Editor
{
    /// <summary>
    /// Validates and non-destructively applies one Character Builder composition.
    /// </summary>
    internal static class CharacterBuilderService
    {
        /// <summary>
        /// Performs a complete read-only preflight for one Scene or Prefab configuration.
        /// </summary>
        public static CharacterBuilderValidationReport Validate(
            CharacterBuilderConfiguration configuration)
        {
            var report = new CharacterBuilderValidationReport();
            if (configuration == null)
            {
                report.AddError("Character Builder configuration is required.");
                return report;
            }

            ValidateProfile(configuration, report);
            var targetKindIsValid = ValidateTarget(configuration.Target, report);
            ValidatePresentation(configuration, targetKindIsValid, report);
            ValidateConversationSettings(configuration, report);
            ValidateOptionalViews(configuration, targetKindIsValid, report);
            ValidateExistingComponents(configuration, report);
            ValidateGrounding(configuration, targetKindIsValid, report);
            ValidateActions(configuration, targetKindIsValid, report);
            ValidateSpeech(configuration, targetKindIsValid, report);
            return report;
        }

        /// <summary>
        /// Applies a valid configuration atomically through Scene Undo or Prefab contents APIs.
        /// </summary>
        public static bool TryApply(
            CharacterBuilderConfiguration configuration,
            out NpcConversationBehaviour conversationBehaviour,
            out string error)
        {
            conversationBehaviour = null;
            error = string.Empty;
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                error = "Character Builder cannot modify objects while Play Mode is active.";
                return false;
            }

            var validation = Validate(configuration);
            if (validation.HasErrors)
            {
                error = GetFirstError(validation);
                return false;
            }

            try
            {
                if (EditorUtility.IsPersistent(configuration.Target))
                {
                    conversationBehaviour = ApplyToPrefabAsset(configuration);
                }
                else
                {
                    conversationBehaviour = ApplyToSceneObject(configuration);
                }

                if (conversationBehaviour == null)
                {
                    throw new InvalidOperationException(
                        "Unity did not retain the configured conversation component.");
                }

                return true;
            }
            catch (Exception exception)
            {
                conversationBehaviour = null;
                error = "Character Builder apply failed: " + exception.Message;
                return false;
            }
        }

        /// <summary>
        /// Returns every visual presentation component available under one target hierarchy.
        /// </summary>
        public static IReadOnlyList<MonoBehaviour> FindVisualPresentationDrivers(
            GameObject target)
        {
            var results = new List<MonoBehaviour>();
            if (target == null)
            {
                return results;
            }

            foreach (var component in target.GetComponentsInChildren<MonoBehaviour>(true))
            {
                if (component is INpcPresentationDriver
                    && !(component is SpeechAugmentedPresentationDriver))
                {
                    results.Add(component);
                }
            }

            return results;
        }

        /// <summary>
        /// Returns every consumer MonoBehaviour action handler under one target hierarchy.
        /// </summary>
        public static IReadOnlyList<MonoBehaviour> FindActionHandlers(GameObject target)
        {
            var results = new List<MonoBehaviour>();
            if (target == null)
            {
                return results;
            }

            foreach (var component in target.GetComponentsInChildren<MonoBehaviour>(true))
            {
                if (component is INpcActionHandler)
                {
                    results.Add(component);
                }
            }

            return results;
        }

        /// <summary>
        /// Returns every consumer context provider available under one target hierarchy.
        /// </summary>
        public static IReadOnlyList<MonoBehaviour> FindContextProviders(GameObject target)
        {
            var results = new List<MonoBehaviour>();
            if (target == null)
            {
                return results;
            }

            foreach (var component in target.GetComponentsInChildren<MonoBehaviour>(true))
            {
                if (component is INpcContextProvider)
                {
                    results.Add(component);
                }
            }

            return results;
        }

        /// <summary>
        /// Validates the persistent profile and reports duplicate opaque IDs as warnings only.
        /// </summary>
        private static void ValidateProfile(
            CharacterBuilderConfiguration configuration,
            CharacterBuilderValidationReport report)
        {
            var profile = configuration.CharacterProfile;
            if (profile == null || !EditorUtility.IsPersistent(profile))
            {
                report.AddError("A persistent CharacterProfile asset is required.");
                return;
            }

            if (!profile.TryValidate(out var profileError))
            {
                report.AddError("CharacterProfile is invalid: " + profileError);
                return;
            }

            var profilePath = AssetDatabase.GetAssetPath(profile).Replace('\\', '/');
            if (!profilePath.StartsWith("Assets/", StringComparison.Ordinal))
            {
                report.AddWarning(
                    "The selected CharacterProfile is package-owned; create an Assets copy before package removal or editing.");
            }

            var duplicatePaths =
                CharacterBuilderAssetService.FindDuplicateCharacterIdPaths(
                    profile.CharacterId,
                    profile);
            if (duplicatePaths.Count > 0)
            {
                report.AddWarning(
                    "Other CharacterProfile assets share characterId '"
                    + profile.CharacterId
                    + "': "
                    + string.Join(", ", duplicatePaths));
            }
        }

        /// <summary>
        /// Accepts loaded Scene objects and writable regular or variant Prefab roots only.
        /// </summary>
        private static bool ValidateTarget(
            GameObject target,
            CharacterBuilderValidationReport report)
        {
            if (target == null)
            {
                report.AddError("A target Scene GameObject or Prefab asset is required.");
                return false;
            }

            if (!EditorUtility.IsPersistent(target))
            {
                if (!target.scene.IsValid() || !target.scene.isLoaded)
                {
                    report.AddError("The target Scene GameObject must belong to a loaded Scene.");
                    return false;
                }

                return true;
            }

            var assetPath = AssetDatabase.GetAssetPath(target).Replace('\\', '/');
            if (!IsWritableConsumerPrefabPath(assetPath))
            {
                report.AddError("Prefab targets must be writable assets under Assets/.");
                return false;
            }

            if (AssetDatabase.LoadMainAssetAtPath(assetPath) != target)
            {
                report.AddError("Select the root GameObject of the Prefab asset.");
                return false;
            }

            var assetType = PrefabUtility.GetPrefabAssetType(target);
            if (assetType != PrefabAssetType.Regular
                && assetType != PrefabAssetType.Variant)
            {
                report.AddError("Only regular or variant Prefab assets can be configured.");
                return false;
            }

            return true;
        }

        /// <summary>
        /// Identifies the only asset namespace where the builder may persist Prefab changes.
        /// </summary>
        internal static bool IsWritableConsumerPrefabPath(string assetPath)
        {
            var normalized = (assetPath ?? string.Empty).Replace('\\', '/');
            return normalized.StartsWith("Assets/", StringComparison.Ordinal);
        }

        /// <summary>
        /// Verifies one consumer presentation adapter and its target-relative ownership.
        /// </summary>
        private static void ValidatePresentation(
            CharacterBuilderConfiguration configuration,
            bool targetKindIsValid,
            CharacterBuilderValidationReport report)
        {
            var driver = configuration.VisualPresentationDriver;
            if (driver == null || !(driver is INpcPresentationDriver))
            {
                report.AddError(
                    "Select a MonoBehaviour that implements INpcPresentationDriver.");
                return;
            }

            if (driver is SpeechAugmentedPresentationDriver)
            {
                report.AddError(
                    "SpeechAugmentedPresentationDriver cannot wrap itself; select the underlying visual driver.");
                return;
            }

            if (targetKindIsValid)
            {
                ValidateReferenceContext(
                    configuration.Target,
                    driver,
                    "Visual presentation driver",
                    true,
                    report);
            }
        }

        /// <summary>
        /// Validates timeout and only the loopback endpoints used by the selected dialogue mode.
        /// </summary>
        private static void ValidateConversationSettings(
            CharacterBuilderConfiguration configuration,
            CharacterBuilderValidationReport report)
        {
            if (configuration.BackendTimeoutSeconds < 1)
            {
                report.AddError("Backend timeout must be at least one second.");
                return;
            }

            try
            {
                switch (configuration.ConversationMode)
                {
                    case NpcConversationMode.Mock:
                        return;
                    case NpcConversationMode.Backend:
                        _ = new UnityWebRequestAiNpcBackendGateway(
                            configuration.BackendEndpoint,
                            configuration.BackendTimeoutSeconds);
                        return;
                    case NpcConversationMode.BackendSession:
                        _ = new UnityWebRequestAiNpcSessionBackendGateway(
                            configuration.SessionBackendEndpoint,
                            configuration.SessionResetEndpoint,
                            configuration.BackendTimeoutSeconds);
                        return;
                    case NpcConversationMode.BackendActions:
                        _ = new UnityWebRequestAiNpcActionBackendGateway(
                            configuration.ActionBackendEndpoint,
                            configuration.ActionResetEndpoint,
                            configuration.BackendTimeoutSeconds);
                        return;
                    case NpcConversationMode.BackendContext:
                        _ = new UnityWebRequestAiNpcContextBackendGateway(
                            configuration.ContextBackendEndpoint,
                            configuration.ContextResetEndpoint,
                            configuration.BackendTimeoutSeconds);
                        return;
                    default:
                        report.AddError(
                            "The selected conversation mode is not supported.");
                        return;
                }
            }
            catch (ArgumentException exception)
            {
                report.AddError("Conversation backend settings are invalid: " + exception.Message);
            }
        }

        /// <summary>
        /// Validates optional package UI adapters without requiring any UI to exist.
        /// </summary>
        private static void ValidateOptionalViews(
            CharacterBuilderConfiguration configuration,
            bool targetKindIsValid,
            CharacterBuilderValidationReport report)
        {
            if (configuration.TextInputView != null)
            {
                if (targetKindIsValid)
                {
                    ValidateReferenceContext(
                        configuration.Target,
                        configuration.TextInputView,
                        "Text input view",
                        false,
                        report);
                }

                ValidateRequiredReferences(
                    configuration.Target,
                    configuration.TextInputView,
                    report,
                    "inputField",
                    "sendButton");
            }

            if (configuration.SessionControlView != null)
            {
                if (targetKindIsValid)
                {
                    ValidateReferenceContext(
                        configuration.Target,
                        configuration.SessionControlView,
                        "Session control view",
                        false,
                        report);
                }

                ValidateRequiredReferences(
                    configuration.Target,
                    configuration.SessionControlView,
                    report,
                    "resetButton",
                    "memoryStatusText");
                if (configuration.ConversationMode != NpcConversationMode.BackendSession
                    && configuration.ConversationMode != NpcConversationMode.BackendActions
                    && configuration.ConversationMode != NpcConversationMode.BackendContext)
                {
                    report.AddWarning(
                        "The selected Session control view will report reset as unsupported outside BackendSession mode.");
                }
            }
        }

        /// <summary>
        /// Rejects ambiguous duplicate Kit components without deleting user configuration.
        /// </summary>
        private static void ValidateExistingComponents(
            CharacterBuilderConfiguration configuration,
            CharacterBuilderValidationReport report)
        {
            if (configuration.Target == null)
            {
                return;
            }

            if (configuration.Target.GetComponents<NpcConversationBehaviour>().Length > 1)
            {
                report.AddError(
                    "The target has multiple NpcConversationBehaviour components; resolve them manually before applying.");
            }

            if (configuration.Target.GetComponents<NpcActionCoordinator>().Length > 1)
            {
                report.AddError(
                    "The target has multiple NpcActionCoordinator components; resolve them manually before applying.");
            }

            if (configuration.Target.GetComponents<NpcContextCoordinator>().Length > 1)
            {
                report.AddError(
                    "The target has multiple NpcContextCoordinator components; resolve them manually before applying.");
            }
        }

        /// <summary>
        /// Validates optional lore assets, current-state providers, and V4 mode requirements.
        /// </summary>
        private static void ValidateGrounding(
            CharacterBuilderConfiguration configuration,
            bool targetKindIsValid,
            CharacterBuilderValidationReport report)
        {
            if (!configuration.ConfigureGrounding)
            {
                if (configuration.ConversationMode == NpcConversationMode.BackendContext)
                {
                    report.AddError("BackendContext mode requires Runtime Grounding.");
                }

                return;
            }

            if (configuration.ConversationMode != NpcConversationMode.BackendContext)
            {
                report.AddWarning(
                    "Runtime Grounding is configured but only sent in BackendContext mode.");
            }

            var knownProfiles = new HashSet<NpcLoreProfile>();
            foreach (var profile in configuration.LoreProfiles
                         ?? Array.Empty<NpcLoreProfile>())
            {
                if (profile == null || !EditorUtility.IsPersistent(profile))
                {
                    report.AddError(
                        "Every selected lore source must be a persistent NpcLoreProfile asset.");
                    continue;
                }

                if (!knownProfiles.Add(profile))
                {
                    report.AddError(
                        "The same NpcLoreProfile cannot be selected more than once.");
                    continue;
                }

                if (!profile.TryValidate(out var loreError))
                {
                    report.AddError(
                        "NpcLoreProfile is invalid: " + loreError);
                }
            }

            var knownProviders = new HashSet<MonoBehaviour>();
            foreach (var source in configuration.ContextProviderSources
                         ?? Array.Empty<MonoBehaviour>())
            {
                if (!(source is INpcContextProvider))
                {
                    report.AddError(
                        "Every selected context provider must implement INpcContextProvider.");
                    continue;
                }

                if (!knownProviders.Add(source))
                {
                    report.AddError(
                        "The same context provider cannot be selected more than once.");
                    continue;
                }

                if (targetKindIsValid)
                {
                    ValidateReferenceContext(
                        configuration.Target,
                        source,
                        "Context provider " + source.GetType().Name,
                        true,
                        report);
                }
            }
        }

        /// <summary>
        /// Validates optional action data, target-owned handlers, and mode compatibility.
        /// </summary>
        private static void ValidateActions(
            CharacterBuilderConfiguration configuration,
            bool targetKindIsValid,
            CharacterBuilderValidationReport report)
        {
            if (!configuration.ConfigureActions)
            {
                if (configuration.ConversationMode == NpcConversationMode.BackendActions)
                {
                    report.AddError("BackendActions mode requires Conversation Actions.");
                }

                return;
            }

            if (configuration.ConversationMode != NpcConversationMode.Mock
                && configuration.ConversationMode != NpcConversationMode.BackendActions
                && configuration.ConversationMode != NpcConversationMode.BackendContext)
            {
                report.AddError(
                    "Conversation Actions require Mock, BackendActions, or BackendContext mode.");
            }

            if (configuration.ActionProfile == null
                || !EditorUtility.IsPersistent(configuration.ActionProfile))
            {
                report.AddError("Conversation Actions require a persistent NpcActionProfile asset.");
                return;
            }

            if (!configuration.ActionProfile.TryValidate(out var profileError))
            {
                report.AddError("NpcActionProfile is invalid: " + profileError);
                return;
            }

            var sources = configuration.ActionHandlerSources
                ?? Array.Empty<MonoBehaviour>();
            var handlerIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (var source in sources)
            {
                if (!(source is INpcActionHandler handler))
                {
                    report.AddError(
                        "Every selected action handler must implement INpcActionHandler.");
                    continue;
                }

                if (!handlerIds.Add(handler.ActionId))
                {
                    report.AddError(
                        "Selected action handler IDs must be unique: " + handler.ActionId);
                }

                if (targetKindIsValid)
                {
                    ValidateReferenceContext(
                        configuration.Target,
                        source,
                        "Action handler " + handler.ActionId,
                        true,
                        report);
                }
            }

            foreach (var definition in configuration.ActionProfile.CreateDefinitions())
            {
                if (!handlerIds.Contains(definition.ActionId))
                {
                    report.AddError(
                        "No selected handler provides actionId '"
                        + definition.ActionId
                        + "'.");
                }
            }
        }

        /// <summary>
        /// Validates optional speech assets, endpoint, views, and unambiguous reusable components.
        /// </summary>
        private static void ValidateSpeech(
            CharacterBuilderConfiguration configuration,
            bool targetKindIsValid,
            CharacterBuilderValidationReport report)
        {
            if (!configuration.ConfigureSpeech)
            {
                if (configuration.SpeechControlView != null)
                {
                    report.AddError(
                        "Enable TTS before connecting an NpcSpeechControlView.");
                }

                return;
            }

            if (configuration.VoiceProfile == null
                || !EditorUtility.IsPersistent(configuration.VoiceProfile))
            {
                report.AddError("TTS requires a persistent NpcVoiceProfile asset.");
            }
            else if (!configuration.VoiceProfile.TryValidate(out var voiceError))
            {
                report.AddError("NpcVoiceProfile is invalid: " + voiceError);
            }

            try
            {
                _ = new UnityWebRequestAiSpeechBackendGateway(
                    configuration.SpeechEndpoint,
                    configuration.BackendTimeoutSeconds);
            }
            catch (ArgumentException exception)
            {
                report.AddError("Speech backend settings are invalid: " + exception.Message);
            }

            if (configuration.Target != null)
            {
                ValidateSingleComponent<NpcSpeechOutput>(configuration.Target, report);
                ValidateSingleComponent<UnityPcmSpeechPlaybackDriver>(
                    configuration.Target,
                    report);
                ValidateSingleComponent<SpeechAugmentedPresentationDriver>(
                    configuration.Target,
                    report);
            }

            if (configuration.SpeechControlView != null)
            {
                if (targetKindIsValid)
                {
                    ValidateReferenceContext(
                        configuration.Target,
                        configuration.SpeechControlView,
                        "Speech control view",
                        false,
                        report);
                }

                ValidateRequiredReferences(
                    configuration.Target,
                    configuration.SpeechControlView,
                    report,
                    "speechToggle",
                    "stopButton",
                    "speechStatusText",
                    "disclosureText");
            }
        }

        /// <summary>
        /// Applies one configuration to a loaded Scene object inside a recoverable Undo group.
        /// </summary>
        private static NpcConversationBehaviour ApplyToSceneObject(
            CharacterBuilderConfiguration configuration)
        {
            Undo.IncrementCurrentGroup();
            var undoGroup = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Configure AI Character");
            try
            {
                var conversation = ApplyToTarget(configuration, true);
                EditorSceneManager.MarkSceneDirty(configuration.Target.scene);
                Undo.CollapseUndoOperations(undoGroup);
                return conversation;
            }
            catch
            {
                Undo.RevertAllDownToGroup(undoGroup);
                throw;
            }
        }

        /// <summary>
        /// Applies one configuration to isolated Prefab contents and saves only after verification.
        /// </summary>
        private static NpcConversationBehaviour ApplyToPrefabAsset(
            CharacterBuilderConfiguration configuration)
        {
            var assetPath = AssetDatabase.GetAssetPath(configuration.Target);
            var visualLocator = ComponentLocator.Create(
                configuration.Target,
                configuration.VisualPresentationDriver);
            var inputLocator = ComponentLocator.CreateOptional(
                configuration.Target,
                configuration.TextInputView);
            var sessionLocator = ComponentLocator.CreateOptional(
                configuration.Target,
                configuration.SessionControlView);
            var speechViewLocator = ComponentLocator.CreateOptional(
                configuration.Target,
                configuration.SpeechControlView);
            var actionHandlerLocators = CreateLocators(
                configuration.Target,
                configuration.ActionHandlerSources);
            var contextProviderLocators = CreateLocators(
                configuration.Target,
                configuration.ContextProviderSources);

            GameObject contents = null;
            try
            {
                contents = PrefabUtility.LoadPrefabContents(assetPath);
                var stagedConfiguration = CopyForPrefabContents(
                    configuration,
                    contents,
                    visualLocator,
                    inputLocator,
                    sessionLocator,
                    speechViewLocator,
                    actionHandlerLocators,
                    contextProviderLocators);
                var stagedValidation = Validate(stagedConfiguration);
                if (stagedValidation.HasErrors)
                {
                    throw new InvalidOperationException(GetFirstError(stagedValidation));
                }

                ApplyToTarget(stagedConfiguration, false);
                var savedPrefab = PrefabUtility.SaveAsPrefabAsset(contents, assetPath);
                if (savedPrefab == null)
                {
                    throw new InvalidOperationException(
                        "Unity did not save the configured Prefab asset.");
                }
            }
            finally
            {
                if (contents != null)
                {
                    PrefabUtility.UnloadPrefabContents(contents);
                }
            }

            AssetDatabase.ImportAsset(
                assetPath,
                ImportAssetOptions.ForceSynchronousImport);
            var reloadedPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);
            return reloadedPrefab == null
                ? null
                : reloadedPrefab.GetComponent<NpcConversationBehaviour>();
        }

        /// <summary>
        /// Maps source Prefab component selections to their isolated editable counterparts.
        /// </summary>
        private static CharacterBuilderConfiguration CopyForPrefabContents(
            CharacterBuilderConfiguration source,
            GameObject contents,
            ComponentLocator visualLocator,
            ComponentLocator inputLocator,
            ComponentLocator sessionLocator,
            ComponentLocator speechViewLocator,
            ComponentLocator[] actionHandlerLocators,
            ComponentLocator[] contextProviderLocators)
        {
            return new CharacterBuilderConfiguration
            {
                Target = contents,
                CharacterProfile = source.CharacterProfile,
                VisualPresentationDriver =
                    visualLocator.Resolve(contents) as MonoBehaviour,
                ConversationMode = source.ConversationMode,
                BackendEndpoint = source.BackendEndpoint,
                SessionBackendEndpoint = source.SessionBackendEndpoint,
                SessionResetEndpoint = source.SessionResetEndpoint,
                ConfigureActions = source.ConfigureActions,
                ActionProfile = source.ActionProfile,
                ActionHandlerSources = ResolveLocators(
                    contents,
                    actionHandlerLocators),
                ActionBackendEndpoint = source.ActionBackendEndpoint,
                ActionResetEndpoint = source.ActionResetEndpoint,
                ConfigureGrounding = source.ConfigureGrounding,
                LoreProfiles = source.LoreProfiles,
                ContextProviderSources = ResolveLocators(
                    contents,
                    contextProviderLocators),
                ContextBackendEndpoint = source.ContextBackendEndpoint,
                ContextResetEndpoint = source.ContextResetEndpoint,
                BackendTimeoutSeconds = source.BackendTimeoutSeconds,
                TextInputView = inputLocator?.Resolve(contents) as NpcTextInputView,
                SessionControlView =
                    sessionLocator?.Resolve(contents) as NpcSessionControlView,
                ConfigureSpeech = source.ConfigureSpeech,
                VoiceProfile = source.VoiceProfile,
                SpeechEndpoint = source.SpeechEndpoint,
                SpeechControlView =
                    speechViewLocator?.Resolve(contents) as NpcSpeechControlView
            };
        }

        /// <summary>
        /// Adds or reuses Kit components and updates only fields owned by the visible builder form.
        /// </summary>
        private static NpcConversationBehaviour ApplyToTarget(
            CharacterBuilderConfiguration configuration,
            bool useUndo)
        {
            var conversation = GetOrAddComponent<NpcConversationBehaviour>(
                configuration.Target,
                useUndo);
            MonoBehaviour presentationSource = configuration.VisualPresentationDriver;
            NpcSpeechOutput speechOutput = null;
            NpcActionCoordinator actionCoordinator = null;
            NpcContextCoordinator contextCoordinator = null;

            if (configuration.ConfigureActions)
            {
                actionCoordinator = GetOrAddComponent<NpcActionCoordinator>(
                    configuration.Target,
                    useUndo);
                ConfigureActions(actionCoordinator, configuration, useUndo);
            }

            if (configuration.ConfigureGrounding)
            {
                contextCoordinator = GetOrAddComponent<NpcContextCoordinator>(
                    configuration.Target,
                    useUndo);
                ConfigureGrounding(contextCoordinator, configuration, useUndo);
            }

            if (configuration.ConfigureSpeech)
            {
                speechOutput = ConfigureSpeech(configuration, useUndo);
                var decorator = GetOrAddComponent<SpeechAugmentedPresentationDriver>(
                    configuration.Target,
                    useUndo);
                ConfigureSpeechDecorator(
                    decorator,
                    configuration.VisualPresentationDriver,
                    speechOutput,
                    useUndo);
                presentationSource = decorator;
            }

            ConfigureConversation(
                conversation,
                configuration,
                presentationSource,
                actionCoordinator,
                contextCoordinator,
                useUndo);
            ConfigureOptionalViews(
                configuration,
                conversation,
                speechOutput,
                useUndo);
            VerifyAppliedConfiguration(
                configuration,
                conversation,
                presentationSource,
                speechOutput,
                actionCoordinator,
                contextCoordinator);
            return conversation;
        }

        /// <summary>
        /// Creates or reuses a dedicated playback stack without borrowing a gameplay AudioSource.
        /// </summary>
        private static NpcSpeechOutput ConfigureSpeech(
            CharacterBuilderConfiguration configuration,
            bool useUndo)
        {
            var playback = configuration.Target
                .GetComponent<UnityPcmSpeechPlaybackDriver>();
            AudioSource dedicatedSource = null;
            if (playback != null)
            {
                dedicatedSource = GetObjectReference(
                    playback,
                    "audioSource") as AudioSource;
            }

            if (dedicatedSource == null)
            {
                dedicatedSource = AddComponent<AudioSource>(
                    configuration.Target,
                    useUndo);
                RecordObject(dedicatedSource, useUndo, "Configure AI Speech Audio");
                dedicatedSource.playOnAwake = false;
                MarkChanged(dedicatedSource);
            }

            if (playback == null)
            {
                playback = AddComponent<UnityPcmSpeechPlaybackDriver>(
                    configuration.Target,
                    useUndo);
            }

            ConfigureObjectReference(
                playback,
                "audioSource",
                dedicatedSource,
                useUndo,
                "Configure AI Speech Playback");

            var speechOutput = GetOrAddComponent<NpcSpeechOutput>(
                configuration.Target,
                useUndo);
            RecordObject(speechOutput, useUndo, "Configure AI Speech Output");
            var serializedOutput = new SerializedObject(speechOutput);
            SetObjectReference(serializedOutput, "voiceProfile", configuration.VoiceProfile);
            SetObjectReference(serializedOutput, "playbackDriver", playback);
            SetString(serializedOutput, "backendEndpoint", configuration.SpeechEndpoint);
            SetInteger(
                serializedOutput,
                "backendTimeoutSeconds",
                configuration.BackendTimeoutSeconds);
            SetBoolean(serializedOutput, "speechEnabled", true);
            serializedOutput.ApplyModifiedPropertiesWithoutUndo();
            MarkChanged(speechOutput);
            return speechOutput;
        }

        /// <summary>
        /// Wires the existing visual adapter and generated speech output into one decorator.
        /// </summary>
        private static void ConfigureSpeechDecorator(
            SpeechAugmentedPresentationDriver decorator,
            MonoBehaviour visualDriver,
            NpcSpeechOutput speechOutput,
            bool useUndo)
        {
            RecordObject(decorator, useUndo, "Configure AI Speech Presentation");
            var serializedDecorator = new SerializedObject(decorator);
            SetObjectReference(serializedDecorator, "visualDriverSource", visualDriver);
            SetObjectReference(serializedDecorator, "speechOutput", speechOutput);
            serializedDecorator.ApplyModifiedPropertiesWithoutUndo();
            MarkChanged(decorator);
        }

        /// <summary>
        /// Writes profile, presentation, mode, endpoints, and timeout into the runtime bridge.
        /// </summary>
        private static void ConfigureConversation(
            NpcConversationBehaviour conversation,
            CharacterBuilderConfiguration configuration,
            MonoBehaviour presentationSource,
            NpcActionCoordinator actionCoordinator,
            NpcContextCoordinator contextCoordinator,
            bool useUndo)
        {
            RecordObject(conversation, useUndo, "Configure AI NPC Conversation");
            var serializedConversation = new SerializedObject(conversation);
            SetObjectReference(
                serializedConversation,
                "characterProfile",
                configuration.CharacterProfile);
            SetObjectReference(
                serializedConversation,
                "presentationDriverSource",
                presentationSource);
            SetInteger(
                serializedConversation,
                "conversationMode",
                (int)configuration.ConversationMode);
            SetString(
                serializedConversation,
                "backendEndpoint",
                configuration.BackendEndpoint);
            SetString(
                serializedConversation,
                "sessionBackendEndpoint",
                configuration.SessionBackendEndpoint);
            SetString(
                serializedConversation,
                "sessionResetEndpoint",
                configuration.SessionResetEndpoint);
            SetString(
                serializedConversation,
                "actionBackendEndpoint",
                configuration.ActionBackendEndpoint);
            SetString(
                serializedConversation,
                "actionResetEndpoint",
                configuration.ActionResetEndpoint);
            SetString(
                serializedConversation,
                "contextBackendEndpoint",
                configuration.ContextBackendEndpoint);
            SetString(
                serializedConversation,
                "contextResetEndpoint",
                configuration.ContextResetEndpoint);
            SetObjectReference(
                serializedConversation,
                "actionCoordinator",
                actionCoordinator);
            SetObjectReference(
                serializedConversation,
                "contextCoordinator",
                contextCoordinator);
            SetInteger(
                serializedConversation,
                "backendTimeoutSeconds",
                configuration.BackendTimeoutSeconds);
            serializedConversation.ApplyModifiedPropertiesWithoutUndo();
            MarkChanged(conversation);
        }

        /// <summary>
        /// Writes the selected consumer action asset and handler references into one coordinator.
        /// </summary>
        private static void ConfigureActions(
            NpcActionCoordinator coordinator,
            CharacterBuilderConfiguration configuration,
            bool useUndo)
        {
            RecordObject(coordinator, useUndo, "Configure AI NPC Actions");
            var serializedCoordinator = new SerializedObject(coordinator);
            SetObjectReference(
                serializedCoordinator,
                "actionProfile",
                configuration.ActionProfile);
            var handlerProperty = GetRequiredProperty(
                serializedCoordinator,
                "actionHandlerSources");
            var sources = configuration.ActionHandlerSources
                ?? Array.Empty<MonoBehaviour>();
            handlerProperty.arraySize = sources.Length;
            for (var index = 0; index < sources.Length; index++)
            {
                handlerProperty.GetArrayElementAtIndex(index).objectReferenceValue =
                    sources[index];
            }

            serializedCoordinator.ApplyModifiedPropertiesWithoutUndo();
            MarkChanged(coordinator);
        }

        /// <summary>
        /// Writes reusable lore assets and target-owned providers into one context coordinator.
        /// </summary>
        private static void ConfigureGrounding(
            NpcContextCoordinator coordinator,
            CharacterBuilderConfiguration configuration,
            bool useUndo)
        {
            RecordObject(coordinator, useUndo, "Configure AI NPC Grounding");
            var serializedCoordinator = new SerializedObject(coordinator);
            var loreProperty = GetRequiredProperty(
                serializedCoordinator,
                "loreProfiles");
            var loreProfiles = configuration.LoreProfiles
                ?? Array.Empty<NpcLoreProfile>();
            loreProperty.arraySize = loreProfiles.Length;
            for (var index = 0; index < loreProfiles.Length; index++)
            {
                loreProperty.GetArrayElementAtIndex(index).objectReferenceValue =
                    loreProfiles[index];
            }

            var providerProperty = GetRequiredProperty(
                serializedCoordinator,
                "contextProviderSources");
            var providers = configuration.ContextProviderSources
                ?? Array.Empty<MonoBehaviour>();
            providerProperty.arraySize = providers.Length;
            for (var index = 0; index < providers.Length; index++)
            {
                providerProperty.GetArrayElementAtIndex(index).objectReferenceValue =
                    providers[index];
            }

            serializedCoordinator.ApplyModifiedPropertiesWithoutUndo();
            MarkChanged(coordinator);
        }

        /// <summary>
        /// Updates only the Kit controller references of optional existing uGUI views.
        /// </summary>
        private static void ConfigureOptionalViews(
            CharacterBuilderConfiguration configuration,
            NpcConversationBehaviour conversation,
            NpcSpeechOutput speechOutput,
            bool useUndo)
        {
            if (configuration.TextInputView != null)
            {
                ConfigureObjectReference(
                    configuration.TextInputView,
                    "conversationBehaviour",
                    conversation,
                    useUndo,
                    "Configure AI NPC Text Input");
            }

            if (configuration.SessionControlView != null)
            {
                ConfigureObjectReference(
                    configuration.SessionControlView,
                    "conversationBehaviour",
                    conversation,
                    useUndo,
                    "Configure AI NPC Session Controls");
            }

            if (configuration.SpeechControlView != null)
            {
                ConfigureObjectReference(
                    configuration.SpeechControlView,
                    "speechOutput",
                    speechOutput,
                    useUndo,
                    "Configure AI NPC Speech Controls");
            }
        }

        /// <summary>
        /// Verifies every builder-owned reference after mutation before reporting success.
        /// </summary>
        private static void VerifyAppliedConfiguration(
            CharacterBuilderConfiguration configuration,
            NpcConversationBehaviour conversation,
            MonoBehaviour presentationSource,
            NpcSpeechOutput speechOutput,
            NpcActionCoordinator actionCoordinator,
            NpcContextCoordinator contextCoordinator)
        {
            var serializedConversation = new SerializedObject(conversation);
            if (GetObjectReference(serializedConversation, "characterProfile")
                    != configuration.CharacterProfile
                || GetObjectReference(serializedConversation, "presentationDriverSource")
                    != presentationSource
                || GetInteger(serializedConversation, "conversationMode")
                    != (int)configuration.ConversationMode
                || GetObjectReference(serializedConversation, "actionCoordinator")
                    != actionCoordinator
                || GetObjectReference(serializedConversation, "contextCoordinator")
                    != contextCoordinator)
            {
                throw new InvalidOperationException(
                    "The conversation component did not retain the requested configuration.");
            }

            if (configuration.ConfigureActions)
            {
                if (actionCoordinator == null
                    || configuration.Target.GetComponents<NpcActionCoordinator>().Length != 1
                    || !actionCoordinator.TryValidateConfiguration(out _))
                {
                    throw new InvalidOperationException(
                        "The target did not retain one valid action composition.");
                }
            }

            if (configuration.ConfigureGrounding)
            {
                if (contextCoordinator == null
                    || configuration.Target.GetComponents<NpcContextCoordinator>().Length != 1
                    || !contextCoordinator.TryValidate(
                        configuration.CharacterProfile,
                        out _))
                {
                    throw new InvalidOperationException(
                        "The target did not retain one valid grounding composition.");
                }
            }

            if (configuration.TextInputView != null
                && GetObjectReference(
                    configuration.TextInputView,
                    "conversationBehaviour") != conversation)
            {
                throw new InvalidOperationException(
                    "The text input view did not retain its conversation reference.");
            }

            if (configuration.SessionControlView != null
                && GetObjectReference(
                    configuration.SessionControlView,
                    "conversationBehaviour") != conversation)
            {
                throw new InvalidOperationException(
                    "The session view did not retain its conversation reference.");
            }

            if (configuration.ConfigureSpeech)
            {
                if (speechOutput == null
                    || configuration.Target.GetComponents<NpcSpeechOutput>().Length != 1
                    || configuration.Target
                        .GetComponents<UnityPcmSpeechPlaybackDriver>().Length != 1
                    || configuration.Target
                        .GetComponents<SpeechAugmentedPresentationDriver>().Length != 1)
                {
                    throw new InvalidOperationException(
                        "The target did not retain one unambiguous TTS composition.");
                }

                if (configuration.SpeechControlView != null
                    && GetObjectReference(
                        configuration.SpeechControlView,
                        "speechOutput") != speechOutput)
                {
                    throw new InvalidOperationException(
                        "The speech view did not retain its output reference.");
                }
            }
        }

        /// <summary>
        /// Requires an optional component reference to share the target's writable context.
        /// </summary>
        private static void ValidateReferenceContext(
            GameObject target,
            Component component,
            string label,
            bool requireTargetHierarchy,
            CharacterBuilderValidationReport report)
        {
            if (target == null || component == null)
            {
                return;
            }

            if (EditorUtility.IsPersistent(target))
            {
                var targetPath = AssetDatabase.GetAssetPath(target);
                var componentPath = AssetDatabase.GetAssetPath(component);
                if (!EditorUtility.IsPersistent(component)
                    || !string.Equals(
                        targetPath,
                        componentPath,
                        StringComparison.Ordinal)
                    || !IsDescendantOrSelf(target.transform, component.transform))
                {
                    report.AddError(
                        label + " must be contained inside the selected Prefab asset.");
                }

                return;
            }

            if (EditorUtility.IsPersistent(component)
                || component.gameObject.scene != target.scene)
            {
                report.AddError(label + " must belong to the target's loaded Scene.");
                return;
            }

            if (requireTargetHierarchy
                && !IsDescendantOrSelf(target.transform, component.transform))
            {
                report.AddError(label + " must be on the target or one of its children.");
            }
        }

        /// <summary>
        /// Verifies that an optional existing view already owns its consumer-provided UI controls.
        /// </summary>
        private static void ValidateRequiredReferences(
            GameObject targetRoot,
            Object target,
            CharacterBuilderValidationReport report,
            params string[] propertyNames)
        {
            var serializedObject = new SerializedObject(target);
            foreach (var propertyName in propertyNames)
            {
                var property = serializedObject.FindProperty(propertyName);
                if (property == null || property.objectReferenceValue == null)
                {
                    report.AddError(
                        target.GetType().Name
                        + " requires its existing '"
                        + propertyName
                        + "' reference before Character Builder can connect it.");
                    continue;
                }

                if (targetRoot != null
                    && EditorUtility.IsPersistent(targetRoot)
                    && property.objectReferenceValue is Component referencedComponent)
                {
                    ValidateReferenceContext(
                        targetRoot,
                        referencedComponent,
                        target.GetType().Name + "." + propertyName,
                        true,
                        report);
                }
            }
        }

        /// <summary>
        /// Rejects more than one component of an optional Kit type on the configured target.
        /// </summary>
        private static void ValidateSingleComponent<T>(
            GameObject target,
            CharacterBuilderValidationReport report)
            where T : Component
        {
            if (target.GetComponents<T>().Length > 1)
            {
                report.AddError(
                    "The target has multiple "
                    + typeof(T).Name
                    + " components; resolve them manually before applying.");
            }
        }

        /// <summary>
        /// Returns or adds one unambiguous component using Scene Undo when requested.
        /// </summary>
        private static T GetOrAddComponent<T>(GameObject target, bool useUndo)
            where T : Component
        {
            var components = target.GetComponents<T>();
            if (components.Length > 1)
            {
                throw new InvalidOperationException(
                    "Multiple " + typeof(T).Name + " components are ambiguous.");
            }

            return components.Length == 1
                ? components[0]
                : AddComponent<T>(target, useUndo);
        }

        /// <summary>
        /// Adds one component through the correct Scene or isolated Prefab mutation path.
        /// </summary>
        private static T AddComponent<T>(GameObject target, bool useUndo)
            where T : Component
        {
            return useUndo
                ? Undo.AddComponent<T>(target)
                : target.AddComponent<T>();
        }

        /// <summary>
        /// Records one existing Scene object before changing its serialized state.
        /// </summary>
        private static void RecordObject(Object target, bool useUndo, string action)
        {
            if (useUndo)
            {
                Undo.RecordObject(target, action);
            }
        }

        /// <summary>
        /// Marks serialized changes and preserves prefab instance overrides without applying them.
        /// </summary>
        private static void MarkChanged(Object target)
        {
            EditorUtility.SetDirty(target);
            if (target is Component component
                && PrefabUtility.IsPartOfPrefabInstance(component))
            {
                PrefabUtility.RecordPrefabInstancePropertyModifications(component);
            }
        }

        /// <summary>
        /// Writes one required object reference with consistent Undo and dirty handling.
        /// </summary>
        private static void ConfigureObjectReference(
            Object target,
            string propertyName,
            Object value,
            bool useUndo,
            string undoName)
        {
            RecordObject(target, useUndo, undoName);
            var serializedObject = new SerializedObject(target);
            SetObjectReference(serializedObject, propertyName, value);
            serializedObject.ApplyModifiedPropertiesWithoutUndo();
            MarkChanged(target);
        }

        /// <summary>
        /// Assigns one required serialized object reference or fails with a stable message.
        /// </summary>
        private static void SetObjectReference(
            SerializedObject serializedObject,
            string propertyName,
            Object value)
        {
            var property = GetRequiredProperty(serializedObject, propertyName);
            property.objectReferenceValue = value;
        }

        /// <summary>
        /// Assigns one required serialized string property.
        /// </summary>
        private static void SetString(
            SerializedObject serializedObject,
            string propertyName,
            string value)
        {
            var property = GetRequiredProperty(serializedObject, propertyName);
            property.stringValue = value ?? string.Empty;
        }

        /// <summary>
        /// Assigns one required serialized integer or enum property.
        /// </summary>
        private static void SetInteger(
            SerializedObject serializedObject,
            string propertyName,
            int value)
        {
            var property = GetRequiredProperty(serializedObject, propertyName);
            property.intValue = value;
        }

        /// <summary>
        /// Assigns one required serialized boolean property.
        /// </summary>
        private static void SetBoolean(
            SerializedObject serializedObject,
            string propertyName,
            bool value)
        {
            var property = GetRequiredProperty(serializedObject, propertyName);
            property.boolValue = value;
        }

        /// <summary>
        /// Returns one required serialized property or explains an incompatible runtime change.
        /// </summary>
        private static SerializedProperty GetRequiredProperty(
            SerializedObject serializedObject,
            string propertyName)
        {
            var property = serializedObject.FindProperty(propertyName);
            if (property == null)
            {
                throw new InvalidOperationException(
                    serializedObject.targetObject.GetType().Name
                    + " no longer exposes serialized property '"
                    + propertyName
                    + "'.");
            }

            return property;
        }

        /// <summary>
        /// Reads one required object reference from an existing serialized component.
        /// </summary>
        private static Object GetObjectReference(Object target, string propertyName)
        {
            return GetObjectReference(new SerializedObject(target), propertyName);
        }

        /// <summary>
        /// Reads one required object reference from a prepared SerializedObject.
        /// </summary>
        private static Object GetObjectReference(
            SerializedObject serializedObject,
            string propertyName)
        {
            return GetRequiredProperty(serializedObject, propertyName)
                .objectReferenceValue;
        }

        /// <summary>
        /// Reads one required integer or enum from a prepared SerializedObject.
        /// </summary>
        private static int GetInteger(
            SerializedObject serializedObject,
            string propertyName)
        {
            return GetRequiredProperty(serializedObject, propertyName).intValue;
        }

        /// <summary>
        /// Tests whether one transform is the selected root or belongs to its hierarchy.
        /// </summary>
        private static bool IsDescendantOrSelf(Transform root, Transform candidate)
        {
            return root != null
                && candidate != null
                && (candidate == root || candidate.IsChildOf(root));
        }

        /// <summary>
        /// Creates optional Prefab-safe locators for selected handler components.
        /// </summary>
        private static ComponentLocator[] CreateLocators(
            GameObject root,
            MonoBehaviour[] components)
        {
            if (components == null || components.Length == 0)
            {
                return Array.Empty<ComponentLocator>();
            }

            var locators = new ComponentLocator[components.Length];
            for (var index = 0; index < components.Length; index++)
            {
                locators[index] = ComponentLocator.Create(root, components[index]);
            }

            return locators;
        }

        /// <summary>
        /// Resolves selected handler locators inside isolated Prefab contents.
        /// </summary>
        private static MonoBehaviour[] ResolveLocators(
            GameObject root,
            ComponentLocator[] locators)
        {
            if (locators == null || locators.Length == 0)
            {
                return Array.Empty<MonoBehaviour>();
            }

            var resolved = new MonoBehaviour[locators.Length];
            for (var index = 0; index < locators.Length; index++)
            {
                resolved[index] = locators[index].Resolve(root) as MonoBehaviour;
            }

            return resolved;
        }

        /// <summary>
        /// Returns the first blocking diagnostic for APIs that expose one concise error string.
        /// </summary>
        private static string GetFirstError(CharacterBuilderValidationReport report)
        {
            foreach (var diagnostic in report.Diagnostics)
            {
                if (diagnostic.Severity == CharacterBuilderDiagnosticSeverity.Error)
                {
                    return diagnostic.Message;
                }
            }

            return "Character Builder configuration is invalid.";
        }

        /// <summary>
        /// Locates one Prefab component by relative transform path, concrete type, and component index.
        /// </summary>
        private sealed class ComponentLocator
        {
            private readonly string transformPath;
            private readonly Type componentType;
            private readonly int componentIndex;

            /// <summary>
            /// Stores a stable component position within one Prefab hierarchy.
            /// </summary>
            private ComponentLocator(
                string transformPath,
                Type componentType,
                int componentIndex)
            {
                this.transformPath = transformPath;
                this.componentType = componentType;
                this.componentIndex = componentIndex;
            }

            /// <summary>
            /// Creates a required locator and rejects references outside the selected Prefab.
            /// </summary>
            public static ComponentLocator Create(
                GameObject root,
                Component component)
            {
                if (root == null || component == null
                    || !IsDescendantOrSelf(root.transform, component.transform))
                {
                    throw new InvalidOperationException(
                        "A selected Prefab component could not be located under its root.");
                }

                var matchingComponents = component.gameObject.GetComponents(
                    component.GetType());
                var index = Array.IndexOf(matchingComponents, component);
                if (index < 0)
                {
                    throw new InvalidOperationException(
                        "A selected Prefab component index could not be resolved.");
                }

                return new ComponentLocator(
                    AnimationUtility.CalculateTransformPath(
                        component.transform,
                        root.transform),
                    component.GetType(),
                    index);
            }

            /// <summary>
            /// Creates no locator for an omitted optional component.
            /// </summary>
            public static ComponentLocator CreateOptional(
                GameObject root,
                Component component)
            {
                return component == null ? null : Create(root, component);
            }

            /// <summary>
            /// Resolves the matching component inside one loaded Prefab contents hierarchy.
            /// </summary>
            public Component Resolve(GameObject root)
            {
                var transform = string.IsNullOrEmpty(transformPath)
                    ? root.transform
                    : root.transform.Find(transformPath);
                if (transform == null)
                {
                    throw new InvalidOperationException(
                        "A configured Prefab transform could not be resolved: "
                        + transformPath);
                }

                var components = transform.gameObject.GetComponents(componentType);
                if (componentIndex < 0 || componentIndex >= components.Length)
                {
                    throw new InvalidOperationException(
                        "A configured Prefab component could not be resolved: "
                        + componentType.Name);
                }

                return components[componentIndex];
            }
        }
    }
}
