using System.Collections.Generic;
using AiCharacterKit.Core;
using AiCharacterKit.Unity;
using AiCharacterKit.Unity.Actions;
using AiCharacterKit.Unity.Speech;
using UnityEngine;

namespace AiCharacterKit.Editor
{
    /// <summary>
    /// Stores editable CharacterProfile values without changing a persistent asset until save.
    /// </summary>
    internal sealed class CharacterProfileDraft
    {
        public string AssetName { get; set; } = "CharacterProfile";

        public string CharacterId { get; set; } = "new-character";

        public string DisplayName { get; set; } = "New Character";

        public string Personality { get; set; } = "Friendly and curious.";

        public string SpeechStyle { get; set; } = "Short and polite.";

        public string ExampleDialogue { get; set; } = "무엇을 도와드릴까요?";

        public NpcEmotion DefaultEmotion { get; set; } = NpcEmotion.Neutral;

        /// <summary>
        /// Copies the current values of one runtime profile into a detached editor draft.
        /// </summary>
        public static CharacterProfileDraft FromProfile(CharacterProfile profile)
        {
            if (profile == null)
            {
                return new CharacterProfileDraft();
            }

            return new CharacterProfileDraft
            {
                AssetName = profile.name,
                CharacterId = profile.CharacterId,
                DisplayName = profile.DisplayName,
                Personality = profile.Personality,
                SpeechStyle = profile.SpeechStyle,
                ExampleDialogue = profile.ExampleDialogue,
                DefaultEmotion = profile.DefaultEmotion
            };
        }
    }

    /// <summary>
    /// Stores one unsaved opaque speech preset selection for editor asset creation.
    /// </summary>
    internal sealed class VoiceProfileDraft
    {
        public string AssetName { get; set; } = "NpcVoiceProfile";

        public string VoicePresetId { get; set; } = string.Empty;

        /// <summary>
        /// Copies one persisted voice profile into a detached editor draft.
        /// </summary>
        public static VoiceProfileDraft FromProfile(NpcVoiceProfile profile)
        {
            if (profile == null)
            {
                return new VoiceProfileDraft();
            }

            return new VoiceProfileDraft
            {
                AssetName = profile.name,
                VoicePresetId = profile.VoicePresetId
            };
        }
    }

    /// <summary>
    /// Stores one detached trigger-to-action binding edited by Character Builder.
    /// </summary>
    internal sealed class ActionBindingDraft
    {
        public string TriggerId { get; set; } = "greet_player";
        public string ConditionDescription { get; set; } =
            "The player greets the character.";
        public string ExampleUserText { get; set; } = "hello";
        public string ActionId { get; set; } = "wave_to_player";
        public int Priority { get; set; }

        /// <summary>
        /// Copies one serialized action binding into detached editor values.
        /// </summary>
        public static ActionBindingDraft FromBinding(NpcActionBinding binding)
        {
            return binding == null
                ? new ActionBindingDraft()
                : new ActionBindingDraft
                {
                    TriggerId = binding.triggerId,
                    ConditionDescription = binding.conditionDescription,
                    ExampleUserText = binding.exampleUserText,
                    ActionId = binding.actionId,
                    Priority = binding.priority
                };
        }
    }

    /// <summary>
    /// Stores unsaved NpcActionProfile values without mutating consumer assets.
    /// </summary>
    internal sealed class ActionProfileDraft
    {
        public string AssetName { get; set; } = "NpcActionProfile";

        public List<ActionBindingDraft> Bindings { get; } =
            new List<ActionBindingDraft> { new ActionBindingDraft() };

        /// <summary>
        /// Copies one persisted action profile into an independent editor draft.
        /// </summary>
        public static ActionProfileDraft FromProfile(NpcActionProfile profile)
        {
            var draft = new ActionProfileDraft();
            if (profile == null)
            {
                return draft;
            }

            draft.AssetName = profile.name;
            draft.Bindings.Clear();
            foreach (var binding in profile.Bindings)
            {
                draft.Bindings.Add(ActionBindingDraft.FromBinding(binding));
            }

            return draft;
        }
    }

    /// <summary>
    /// Describes the explicit non-destructive settings applied to one Scene or Prefab NPC.
    /// </summary>
    internal sealed class CharacterBuilderConfiguration
    {
        public const string DefaultCharacterFolder =
            "Assets/AI Character Kit/Characters";

        public const string DefaultBackendEndpoint =
            "http://127.0.0.1:8787/v1/npc/respond";

        public const string DefaultSessionBackendEndpoint =
            "http://127.0.0.1:8787/v2/npc/respond";

        public const string DefaultSessionResetEndpoint =
            "http://127.0.0.1:8787/v2/npc/sessions/reset";

        public const string DefaultSpeechEndpoint =
            "http://127.0.0.1:8787/v1/speech/synthesize";

        public const string DefaultActionBackendEndpoint =
            "http://127.0.0.1:8787/v3/npc/respond";

        public const string DefaultActionResetEndpoint =
            "http://127.0.0.1:8787/v3/npc/sessions/reset";

        public const int DefaultTimeoutSeconds = 35;

        public GameObject Target { get; set; }

        public CharacterProfile CharacterProfile { get; set; }

        public MonoBehaviour VisualPresentationDriver { get; set; }

        public NpcConversationMode ConversationMode { get; set; } =
            NpcConversationMode.Mock;

        public string BackendEndpoint { get; set; } = DefaultBackendEndpoint;

        public string SessionBackendEndpoint { get; set; } =
            DefaultSessionBackendEndpoint;

        public string SessionResetEndpoint { get; set; } =
            DefaultSessionResetEndpoint;

        public int BackendTimeoutSeconds { get; set; } = DefaultTimeoutSeconds;

        public NpcTextInputView TextInputView { get; set; }

        public NpcSessionControlView SessionControlView { get; set; }

        public bool ConfigureActions { get; set; }

        public NpcActionProfile ActionProfile { get; set; }

        public MonoBehaviour[] ActionHandlerSources { get; set; } =
            System.Array.Empty<MonoBehaviour>();

        public string ActionBackendEndpoint { get; set; } =
            DefaultActionBackendEndpoint;

        public string ActionResetEndpoint { get; set; } =
            DefaultActionResetEndpoint;

        public bool ConfigureSpeech { get; set; }

        public NpcVoiceProfile VoiceProfile { get; set; }

        public string SpeechEndpoint { get; set; } = DefaultSpeechEndpoint;

        public NpcSpeechControlView SpeechControlView { get; set; }
    }

    /// <summary>
    /// Distinguishes blocking builder errors from actionable non-blocking warnings.
    /// </summary>
    internal enum CharacterBuilderDiagnosticSeverity
    {
        Warning = 0,
        Error = 1
    }

    /// <summary>
    /// Represents one concise validation result shown by the Character Builder.
    /// </summary>
    internal sealed class CharacterBuilderDiagnostic
    {
        public CharacterBuilderDiagnosticSeverity Severity { get; }

        public string Message { get; }

        /// <summary>
        /// Creates one immutable diagnostic entry.
        /// </summary>
        public CharacterBuilderDiagnostic(
            CharacterBuilderDiagnosticSeverity severity,
            string message)
        {
            Severity = severity;
            Message = message ?? string.Empty;
        }
    }

    /// <summary>
    /// Collects all preflight results so the window can explain every visible issue at once.
    /// </summary>
    internal sealed class CharacterBuilderValidationReport
    {
        private readonly List<CharacterBuilderDiagnostic> diagnostics =
            new List<CharacterBuilderDiagnostic>();

        public IReadOnlyList<CharacterBuilderDiagnostic> Diagnostics => diagnostics;

        public bool HasErrors
        {
            get
            {
                foreach (var diagnostic in diagnostics)
                {
                    if (diagnostic.Severity == CharacterBuilderDiagnosticSeverity.Error)
                    {
                        return true;
                    }
                }

                return false;
            }
        }

        /// <summary>
        /// Adds one blocking issue to the report.
        /// </summary>
        public void AddError(string message)
        {
            diagnostics.Add(new CharacterBuilderDiagnostic(
                CharacterBuilderDiagnosticSeverity.Error,
                message));
        }

        /// <summary>
        /// Adds one non-blocking issue to the report.
        /// </summary>
        public void AddWarning(string message)
        {
            diagnostics.Add(new CharacterBuilderDiagnostic(
                CharacterBuilderDiagnosticSeverity.Warning,
                message));
        }
    }
}
