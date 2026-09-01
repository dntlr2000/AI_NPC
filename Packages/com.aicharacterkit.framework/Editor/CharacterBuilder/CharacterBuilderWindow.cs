using System;
using AiCharacterKit.Core;
using AiCharacterKit.Unity;
using AiCharacterKit.Unity.Speech;
using UnityEditor;
using UnityEngine;

namespace AiCharacterKit.Editor
{
    /// <summary>
    /// Authors reusable profiles and connects existing consumer presentation and UI objects.
    /// </summary>
    internal sealed class CharacterBuilderWindow : EditorWindow
    {
        private CharacterProfile selectedProfile;
        private CharacterProfileDraft profileDraft = new CharacterProfileDraft();
        private NpcVoiceProfile selectedVoiceProfile;
        private VoiceProfileDraft voiceDraft = new VoiceProfileDraft();
        private CharacterBuilderConfiguration configuration =
            new CharacterBuilderConfiguration();
        private CharacterBuilderValidationReport validationReport;
        private Vector2 scrollPosition;
        private string assetFolder =
            CharacterBuilderConfiguration.DefaultCharacterFolder;
        private string previewInput = "hello";
        private AiNpcResponse previewResponse;
        private string statusMessage = string.Empty;
        private MessageType statusType = MessageType.Info;

        /// <summary>
        /// Opens the package Character Builder from its stable Tools menu entry.
        /// </summary>
        [MenuItem("Tools/AI Character Kit/Character Builder")]
        private static void OpenWindow()
        {
            var window = GetWindow<CharacterBuilderWindow>();
            window.titleContent = new GUIContent("Character Builder");
            window.minSize = new Vector2(520f, 680f);
            window.Show();
        }

        /// <summary>
        /// Draws profile, preview, target, optional speech, validation, and apply controls.
        /// </summary>
        private void OnGUI()
        {
            scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);
            DrawHeader();
            DrawProfileSection();
            DrawPreviewSection();
            DrawTargetSection();
            DrawSpeechSection();
            DrawValidationSection();
            EditorGUILayout.EndScrollView();
        }

        /// <summary>
        /// Introduces the non-generative boundary of the Character Builder.
        /// </summary>
        private static void DrawHeader()
        {
            EditorGUILayout.LabelField("AI Character Kit Character Builder", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Creates consumer-owned profiles and connects existing GameObjects, presentation drivers, and optional uGUI views. It does not generate models, UI, presentation code, or prefabs.",
                MessageType.Info);
            EditorGUILayout.Space();
        }

        /// <summary>
        /// Draws detached CharacterProfile fields and explicit create or update actions.
        /// </summary>
        private void DrawProfileSection()
        {
            EditorGUILayout.LabelField("1. Character Profile", EditorStyles.boldLabel);
            EditorGUI.BeginChangeCheck();
            var nextProfile = (CharacterProfile)EditorGUILayout.ObjectField(
                "Existing Profile",
                selectedProfile,
                typeof(CharacterProfile),
                false);
            if (EditorGUI.EndChangeCheck())
            {
                LoadCharacterProfile(nextProfile);
            }

            profileDraft.AssetName = EditorGUILayout.TextField(
                "Asset Name",
                profileDraft.AssetName);
            profileDraft.CharacterId = EditorGUILayout.TextField(
                "Character ID",
                profileDraft.CharacterId);
            profileDraft.DisplayName = EditorGUILayout.TextField(
                "Display Name",
                profileDraft.DisplayName);
            profileDraft.Personality = DrawTextArea(
                "Personality",
                profileDraft.Personality,
                48f);
            profileDraft.SpeechStyle = DrawTextArea(
                "Speech Style",
                profileDraft.SpeechStyle,
                44f);
            profileDraft.ExampleDialogue = DrawTextArea(
                "Example Dialogue",
                profileDraft.ExampleDialogue,
                48f);
            profileDraft.DefaultEmotion = (NpcEmotion)EditorGUILayout.EnumPopup(
                "Default Emotion",
                profileDraft.DefaultEmotion);
            DrawAssetFolder();

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button(selectedProfile == null
                    ? "Create Profile"
                    : "Save Profile"))
            {
                SaveCharacterProfile();
            }

            if (GUILayout.Button("New Draft"))
            {
                LoadCharacterProfile(null);
                SetStatus("Started a new unsaved CharacterProfile draft.", MessageType.Info);
            }

            EditorGUILayout.EndHorizontal();
            DrawDuplicateIdWarning();
            EditorGUILayout.Space();
        }

        /// <summary>
        /// Draws the network-free deterministic response preview for current draft values.
        /// </summary>
        private void DrawPreviewSection()
        {
            EditorGUILayout.LabelField("2. Mock Preview", EditorStyles.boldLabel);
            previewInput = EditorGUILayout.TextField("User Text", previewInput);
            if (GUILayout.Button("Preview Mock Response"))
            {
                if (CharacterBuilderAssetService.TryPreviewMock(
                        profileDraft,
                        previewInput,
                        out previewResponse,
                        out var error))
                {
                    SetStatus("Mock preview completed without networking.", MessageType.Info);
                }
                else
                {
                    previewResponse = null;
                    SetStatus(error, MessageType.Error);
                }
            }

            if (previewResponse != null)
            {
                EditorGUILayout.HelpBox(
                    "Dialogue: " + previewResponse.Dialogue
                    + "\nEmotion: " + previewResponse.Emotion
                    + "\nGesture: " + previewResponse.Gesture,
                    MessageType.None);
            }

            EditorGUILayout.Space();
        }

        /// <summary>
        /// Draws the existing Scene or Prefab target, presentation, mode, endpoints, and views.
        /// </summary>
        private void DrawTargetSection()
        {
            EditorGUILayout.LabelField("3. NPC Connection", EditorStyles.boldLabel);
            EditorGUI.BeginChangeCheck();
            var nextTarget = (GameObject)EditorGUILayout.ObjectField(
                "Scene / Prefab Target",
                configuration.Target,
                typeof(GameObject),
                true);
            if (EditorGUI.EndChangeCheck())
            {
                LoadTarget(nextTarget);
            }

            DrawPresentationDriverPopup();
            configuration.ConversationMode =
                (NpcConversationMode)EditorGUILayout.EnumPopup(
                    "Conversation Mode",
                    configuration.ConversationMode);
            DrawConversationEndpoints();
            configuration.BackendTimeoutSeconds = EditorGUILayout.IntField(
                "Timeout Seconds",
                configuration.BackendTimeoutSeconds);

            EditorGUILayout.LabelField("Optional Existing uGUI Views", EditorStyles.miniBoldLabel);
            configuration.TextInputView =
                (NpcTextInputView)EditorGUILayout.ObjectField(
                    "Text Input View",
                    configuration.TextInputView,
                    typeof(NpcTextInputView),
                    true);
            configuration.SessionControlView =
                (NpcSessionControlView)EditorGUILayout.ObjectField(
                    "Session Control View",
                    configuration.SessionControlView,
                    typeof(NpcSessionControlView),
                    true);
            EditorGUILayout.Space();
        }

        /// <summary>
        /// Draws optional opaque voice asset authoring and existing speech view connection.
        /// </summary>
        private void DrawSpeechSection()
        {
            EditorGUILayout.LabelField("4. Optional TTS", EditorStyles.boldLabel);
            configuration.ConfigureSpeech = EditorGUILayout.Toggle(
                "Configure TTS",
                configuration.ConfigureSpeech);
            if (!configuration.ConfigureSpeech)
            {
                EditorGUILayout.HelpBox(
                    "TTS components are left untouched. Applying connects the conversation directly to the selected visual driver.",
                    MessageType.Info);
                configuration.SpeechControlView = null;
                EditorGUILayout.Space();
                return;
            }

            EditorGUI.BeginChangeCheck();
            var nextVoiceProfile = (NpcVoiceProfile)EditorGUILayout.ObjectField(
                "Existing Voice Profile",
                selectedVoiceProfile,
                typeof(NpcVoiceProfile),
                false);
            if (EditorGUI.EndChangeCheck())
            {
                LoadVoiceProfile(nextVoiceProfile);
            }

            voiceDraft.AssetName = EditorGUILayout.TextField(
                "Voice Asset Name",
                voiceDraft.AssetName);
            voiceDraft.VoicePresetId = EditorGUILayout.TextField(
                "Voice Preset ID",
                voiceDraft.VoicePresetId);
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button(selectedVoiceProfile == null
                    ? "Create Voice Profile"
                    : "Save Voice Profile"))
            {
                SaveVoiceProfile();
            }

            if (GUILayout.Button("New Voice Draft"))
            {
                LoadVoiceProfile(null);
                SetStatus("Started a new unsaved voice profile draft.", MessageType.Info);
            }

            EditorGUILayout.EndHorizontal();

            configuration.SpeechEndpoint = EditorGUILayout.TextField(
                "Speech Endpoint",
                configuration.SpeechEndpoint);
            configuration.SpeechControlView =
                (NpcSpeechControlView)EditorGUILayout.ObjectField(
                    "Speech Control View",
                    configuration.SpeechControlView,
                    typeof(NpcSpeechControlView),
                    true);
            EditorGUILayout.HelpBox(
                "Voice preset IDs are opaque. Provider voice names and API credentials remain in the backend.",
                MessageType.Info);
            EditorGUILayout.Space();
        }

        /// <summary>
        /// Draws preflight diagnostics and the single non-destructive apply action.
        /// </summary>
        private void DrawValidationSection()
        {
            EditorGUILayout.LabelField("5. Validate and Apply", EditorStyles.boldLabel);
            configuration.CharacterProfile = selectedProfile;
            configuration.VoiceProfile = selectedVoiceProfile;

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Validate Configuration"))
            {
                validationReport = CharacterBuilderService.Validate(configuration);
                SetStatus(
                    validationReport.HasErrors
                        ? "Configuration has blocking errors."
                        : "Configuration is ready to apply.",
                    validationReport.HasErrors ? MessageType.Error : MessageType.Info);
            }

            using (new EditorGUI.DisabledScope(
                       EditorApplication.isPlayingOrWillChangePlaymode))
            {
                if (GUILayout.Button("Apply to Target"))
                {
                    ApplyConfiguration();
                }
            }

            EditorGUILayout.EndHorizontal();
            DrawValidationReport();
            if (!string.IsNullOrWhiteSpace(statusMessage))
            {
                EditorGUILayout.HelpBox(statusMessage, statusType);
            }
        }

        /// <summary>
        /// Draws one labeled multi-line text field without creating any runtime UI.
        /// </summary>
        private static string DrawTextArea(string label, string value, float height)
        {
            EditorGUILayout.LabelField(label);
            return EditorGUILayout.TextArea(
                value ?? string.Empty,
                GUILayout.MinHeight(height));
        }

        /// <summary>
        /// Draws the writable Assets folder and converts an absolute folder picker result safely.
        /// </summary>
        private void DrawAssetFolder()
        {
            EditorGUILayout.BeginHorizontal();
            assetFolder = EditorGUILayout.TextField("Asset Folder", assetFolder);
            if (GUILayout.Button("Browse", GUILayout.Width(70f)))
            {
                var absoluteFolder = EditorUtility.OpenFolderPanel(
                    "Character Builder Asset Folder",
                    Application.dataPath,
                    string.Empty);
                if (!string.IsNullOrEmpty(absoluteFolder))
                {
                    var projectPath = FileUtil.GetProjectRelativePath(absoluteFolder);
                    if (string.IsNullOrEmpty(projectPath)
                        || (!string.Equals(projectPath, "Assets", StringComparison.Ordinal)
                            && !projectPath.StartsWith("Assets/", StringComparison.Ordinal)))
                    {
                        SetStatus(
                            "Choose a writable folder under this project's Assets directory.",
                            MessageType.Error);
                    }
                    else
                    {
                        assetFolder = projectPath;
                    }
                }
            }

            EditorGUILayout.EndHorizontal();
        }

        /// <summary>
        /// Selects one existing consumer presentation implementation from the target hierarchy.
        /// </summary>
        private void DrawPresentationDriverPopup()
        {
            var drivers = CharacterBuilderService.FindVisualPresentationDrivers(
                configuration.Target);
            if (drivers.Count == 0)
            {
                configuration.VisualPresentationDriver = null;
                EditorGUILayout.HelpBox(
                    "The target needs a consumer MonoBehaviour that implements INpcPresentationDriver.",
                    MessageType.Warning);
                return;
            }

            var labels = new string[drivers.Count];
            var selectedIndex = 0;
            for (var index = 0; index < drivers.Count; index++)
            {
                labels[index] = GetComponentLabel(configuration.Target, drivers[index]);
                if (drivers[index] == configuration.VisualPresentationDriver)
                {
                    selectedIndex = index;
                }
            }

            selectedIndex = EditorGUILayout.Popup(
                "Visual Presentation",
                selectedIndex,
                labels);
            configuration.VisualPresentationDriver = drivers[selectedIndex];
        }

        /// <summary>
        /// Draws only the endpoint fields used by the selected conversation mode.
        /// </summary>
        private void DrawConversationEndpoints()
        {
            if (configuration.ConversationMode == NpcConversationMode.Backend)
            {
                configuration.BackendEndpoint = EditorGUILayout.TextField(
                    "V1 Respond Endpoint",
                    configuration.BackendEndpoint);
            }
            else if (configuration.ConversationMode == NpcConversationMode.BackendSession)
            {
                configuration.SessionBackendEndpoint = EditorGUILayout.TextField(
                    "V2 Respond Endpoint",
                    configuration.SessionBackendEndpoint);
                configuration.SessionResetEndpoint = EditorGUILayout.TextField(
                    "V2 Reset Endpoint",
                    configuration.SessionResetEndpoint);
            }
        }

        /// <summary>
        /// Loads a persistent profile into a detached draft and active composition selection.
        /// </summary>
        private void LoadCharacterProfile(CharacterProfile profile)
        {
            selectedProfile = profile;
            profileDraft = CharacterProfileDraft.FromProfile(profile);
            configuration.CharacterProfile = profile;
            validationReport = null;
            previewResponse = null;
        }

        /// <summary>
        /// Creates a new profile or writes validated draft values into the selected consumer asset.
        /// </summary>
        private void SaveCharacterProfile()
        {
            CharacterProfile createdProfile = null;
            string error;
            bool succeeded;
            if (selectedProfile == null)
            {
                succeeded = CharacterBuilderAssetService.TryCreateCharacterProfile(
                    profileDraft,
                    assetFolder,
                    out createdProfile,
                    out error);
            }
            else
            {
                succeeded = CharacterBuilderAssetService.TryUpdateCharacterProfile(
                    selectedProfile,
                    profileDraft,
                    out error);
            }

            if (!succeeded)
            {
                SetStatus(error, MessageType.Error);
                return;
            }

            if (selectedProfile == null)
            {
                LoadCharacterProfile(createdProfile);
            }
            else
            {
                profileDraft = CharacterProfileDraft.FromProfile(selectedProfile);
            }

            Selection.activeObject = selectedProfile;
            SetStatus(
                "CharacterProfile saved at " + AssetDatabase.GetAssetPath(selectedProfile),
                MessageType.Info);
        }

        /// <summary>
        /// Shows duplicate IDs as guidance without blocking intentional profile reuse.
        /// </summary>
        private void DrawDuplicateIdWarning()
        {
            var duplicates = CharacterBuilderAssetService.FindDuplicateCharacterIdPaths(
                profileDraft.CharacterId,
                selectedProfile);
            if (duplicates.Count > 0)
            {
                EditorGUILayout.HelpBox(
                    "Other profiles share this character ID: "
                    + string.Join(", ", duplicates),
                    MessageType.Warning);
            }
        }

        /// <summary>
        /// Loads a Scene or Prefab target and reuses any existing Kit serialization as form defaults.
        /// </summary>
        private void LoadTarget(GameObject target)
        {
            configuration.Target = target;
            configuration.VisualPresentationDriver = null;
            configuration.TextInputView = null;
            configuration.SessionControlView = null;
            configuration.SpeechControlView = null;
            configuration.ConfigureSpeech = false;
            validationReport = null;
            if (target == null)
            {
                return;
            }

            var conversations = target.GetComponents<NpcConversationBehaviour>();
            if (conversations.Length == 1)
            {
                LoadExistingConversation(conversations[0]);
            }

            var inputViews = target.GetComponentsInChildren<NpcTextInputView>(true);
            configuration.TextInputView = inputViews.Length == 1 ? inputViews[0] : null;
            var sessionViews = target.GetComponentsInChildren<NpcSessionControlView>(true);
            configuration.SessionControlView =
                sessionViews.Length == 1 ? sessionViews[0] : null;
            var speechViews = target.GetComponentsInChildren<NpcSpeechControlView>(true);
            configuration.SpeechControlView =
                speechViews.Length == 1 ? speechViews[0] : null;

            if (configuration.VisualPresentationDriver == null)
            {
                var drivers = CharacterBuilderService.FindVisualPresentationDrivers(target);
                if (drivers.Count == 1)
                {
                    configuration.VisualPresentationDriver = drivers[0];
                }
            }
        }

        /// <summary>
        /// Reads existing builder-owned conversation and speech fields without changing the target.
        /// </summary>
        private void LoadExistingConversation(NpcConversationBehaviour conversation)
        {
            var serializedConversation = new SerializedObject(conversation);
            LoadCharacterProfile(
                serializedConversation.FindProperty("characterProfile")
                    .objectReferenceValue as CharacterProfile);
            configuration.ConversationMode =
                (NpcConversationMode)serializedConversation
                    .FindProperty("conversationMode").intValue;
            configuration.BackendEndpoint = serializedConversation
                .FindProperty("backendEndpoint").stringValue;
            configuration.SessionBackendEndpoint = serializedConversation
                .FindProperty("sessionBackendEndpoint").stringValue;
            configuration.SessionResetEndpoint = serializedConversation
                .FindProperty("sessionResetEndpoint").stringValue;
            configuration.BackendTimeoutSeconds = serializedConversation
                .FindProperty("backendTimeoutSeconds").intValue;

            var presentation = serializedConversation
                .FindProperty("presentationDriverSource")
                .objectReferenceValue as MonoBehaviour;
            if (presentation is SpeechAugmentedPresentationDriver decorator)
            {
                configuration.ConfigureSpeech = true;
                var serializedDecorator = new SerializedObject(decorator);
                configuration.VisualPresentationDriver = serializedDecorator
                    .FindProperty("visualDriverSource")
                    .objectReferenceValue as MonoBehaviour;
                var output = serializedDecorator.FindProperty("speechOutput")
                    .objectReferenceValue as NpcSpeechOutput;
                LoadExistingSpeech(output);
            }
            else
            {
                configuration.VisualPresentationDriver = presentation;
            }
        }

        /// <summary>
        /// Reads an existing optional TTS output into the voice draft and endpoint form.
        /// </summary>
        private void LoadExistingSpeech(NpcSpeechOutput output)
        {
            if (output == null)
            {
                return;
            }

            var serializedOutput = new SerializedObject(output);
            LoadVoiceProfile(
                serializedOutput.FindProperty("voiceProfile")
                    .objectReferenceValue as NpcVoiceProfile);
            configuration.SpeechEndpoint = serializedOutput
                .FindProperty("backendEndpoint").stringValue;
        }

        /// <summary>
        /// Loads one persistent opaque voice profile into a detached editor draft.
        /// </summary>
        private void LoadVoiceProfile(NpcVoiceProfile profile)
        {
            selectedVoiceProfile = profile;
            voiceDraft = VoiceProfileDraft.FromProfile(profile);
            configuration.VoiceProfile = profile;
            validationReport = null;
        }

        /// <summary>
        /// Creates or updates one consumer-owned opaque voice profile.
        /// </summary>
        private void SaveVoiceProfile()
        {
            NpcVoiceProfile createdProfile = null;
            string error;
            bool succeeded;
            if (selectedVoiceProfile == null)
            {
                succeeded = CharacterBuilderAssetService.TryCreateVoiceProfile(
                    voiceDraft,
                    assetFolder,
                    out createdProfile,
                    out error);
            }
            else
            {
                succeeded = CharacterBuilderAssetService.TryUpdateVoiceProfile(
                    selectedVoiceProfile,
                    voiceDraft,
                    out error);
            }

            if (!succeeded)
            {
                SetStatus(error, MessageType.Error);
                return;
            }

            if (selectedVoiceProfile == null)
            {
                LoadVoiceProfile(createdProfile);
            }
            else
            {
                voiceDraft = VoiceProfileDraft.FromProfile(selectedVoiceProfile);
            }

            Selection.activeObject = selectedVoiceProfile;
            SetStatus(
                "NpcVoiceProfile saved at "
                + AssetDatabase.GetAssetPath(selectedVoiceProfile),
                MessageType.Info);
        }

        /// <summary>
        /// Validates and applies the current form while keeping exceptions inside the Editor UI.
        /// </summary>
        private void ApplyConfiguration()
        {
            configuration.CharacterProfile = selectedProfile;
            configuration.VoiceProfile = selectedVoiceProfile;
            validationReport = CharacterBuilderService.Validate(configuration);
            if (!CharacterBuilderService.TryApply(
                    configuration,
                    out var conversation,
                    out var error))
            {
                SetStatus(error, MessageType.Error);
                return;
            }

            Selection.activeObject = conversation;
            validationReport = CharacterBuilderService.Validate(configuration);
            SetStatus(
                "Character configuration applied without deleting existing components or assets.",
                MessageType.Info);
        }

        /// <summary>
        /// Draws all current validation diagnostics with severity-appropriate message styles.
        /// </summary>
        private void DrawValidationReport()
        {
            if (validationReport == null)
            {
                return;
            }

            if (validationReport.Diagnostics.Count == 0)
            {
                EditorGUILayout.HelpBox(
                    "No configuration issues were found.",
                    MessageType.Info);
                return;
            }

            foreach (var diagnostic in validationReport.Diagnostics)
            {
                EditorGUILayout.HelpBox(
                    diagnostic.Message,
                    diagnostic.Severity == CharacterBuilderDiagnosticSeverity.Error
                        ? MessageType.Error
                        : MessageType.Warning);
            }
        }

        /// <summary>
        /// Builds one stable hierarchy label for a selectable presentation component.
        /// </summary>
        private static string GetComponentLabel(GameObject root, Component component)
        {
            var path = AnimationUtility.CalculateTransformPath(
                component.transform,
                root.transform);
            return (string.IsNullOrEmpty(path) ? root.name : path)
                + " ("
                + component.GetType().Name
                + ")";
        }

        /// <summary>
        /// Replaces the current concise window outcome without logging character content.
        /// </summary>
        private void SetStatus(string message, MessageType messageType)
        {
            statusMessage = message ?? string.Empty;
            statusType = messageType;
            Repaint();
        }
    }
}
