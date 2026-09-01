using AiCharacterKit.Speech;
using UnityEngine;
using UnityEngine.UI;

namespace AiCharacterKit.Unity.Speech
{
    /// <summary>
    /// Connects speech enable, stop, status, and AI-audio disclosure UI to one NPC.
    /// </summary>
    public sealed class NpcSpeechControlView : MonoBehaviour
    {
        [SerializeField]
        private NpcSpeechOutput speechOutput;

        [SerializeField]
        private Toggle speechToggle;

        [SerializeField]
        private Button stopButton;

        [SerializeField]
        private Text speechStatusText;

        [SerializeField]
        private Text disclosureText;

        /// <summary>
        /// Registers UI and state listeners for this enabled lifetime.
        /// </summary>
        private void OnEnable()
        {
            if (speechToggle != null)
            {
                speechToggle.onValueChanged.AddListener(HandleToggleChanged);
            }

            if (stopButton != null)
            {
                stopButton.onClick.AddListener(HandleStopClicked);
            }

            if (speechOutput != null)
            {
                speechOutput.StateChanged += HandleSpeechStateChanged;
            }
        }

        /// <summary>
        /// Initializes visible speech state and the required AI-generated disclosure.
        /// </summary>
        private void Start()
        {
            if (speechToggle != null)
            {
                speechToggle.SetIsOnWithoutNotify(
                    speechOutput != null && speechOutput.IsSpeechEnabled);
            }

            if (disclosureText != null)
            {
                disclosureText.text = "이 음성은 AI로 생성됩니다.";
            }

            HandleSpeechStateChanged(
                speechOutput?.State ?? NpcSpeechState.Disabled,
                string.Empty);
        }

        /// <summary>
        /// Removes listeners so repeated enable cycles do not duplicate callbacks.
        /// </summary>
        private void OnDisable()
        {
            if (speechToggle != null)
            {
                speechToggle.onValueChanged.RemoveListener(HandleToggleChanged);
            }

            if (stopButton != null)
            {
                stopButton.onClick.RemoveListener(HandleStopClicked);
            }

            if (speechOutput != null)
            {
                speechOutput.StateChanged -= HandleSpeechStateChanged;
            }
        }

        /// <summary>
        /// Applies the user's local speech preference without changing dialogue behavior.
        /// </summary>
        private void HandleToggleChanged(bool enabledValue)
        {
            speechOutput?.SetSpeechEnabled(enabledValue);
        }

        /// <summary>
        /// Stops only optional audio while leaving the displayed response intact.
        /// </summary>
        private void HandleStopClicked()
        {
            speechOutput?.StopSpeech();
        }

        /// <summary>
        /// Maps pure speech states and safe errors to concise prototype UI text.
        /// </summary>
        private void HandleSpeechStateChanged(
            NpcSpeechState state,
            string message)
        {
            if (speechStatusText != null)
            {
                switch (state)
                {
                    case NpcSpeechState.Synthesizing:
                        speechStatusText.text = "음성: 생성 중";
                        break;
                    case NpcSpeechState.Playing:
                        speechStatusText.text = "음성: 재생 중";
                        break;
                    case NpcSpeechState.Failed:
                        speechStatusText.text = string.IsNullOrWhiteSpace(message)
                            ? "음성: 실패"
                            : $"음성: 실패 ({message})";
                        break;
                    case NpcSpeechState.Disabled:
                        speechStatusText.text = "음성: 꺼짐";
                        break;
                    default:
                        speechStatusText.text = "음성: 준비";
                        break;
                }
            }

            if (stopButton != null)
            {
                stopButton.interactable = state == NpcSpeechState.Synthesizing
                    || state == NpcSpeechState.Playing;
            }
        }
    }
}
