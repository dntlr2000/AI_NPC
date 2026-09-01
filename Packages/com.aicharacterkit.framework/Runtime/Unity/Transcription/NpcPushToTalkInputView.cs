using System;
using AiCharacterKit.Transcription;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Events;
using UnityEngine.UI;

namespace AiCharacterKit.Unity.Transcription
{
    /// <summary>
    /// Connects pointer-held recording, cancellation, status, and reviewed text input.
    /// </summary>
    public sealed class NpcPushToTalkInputView
        : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, ICancelHandler
    {
        [SerializeField]
        private NpcVoiceInput voiceInput;

        [SerializeField]
        private NpcTextInputView textInputView;

        [SerializeField]
        private Button pushToTalkButton;

        [SerializeField]
        private Button cancelButton;

        [SerializeField]
        private Text transcriptionStatusText;

        [SerializeField]
        private Text disclosureText;

        [SerializeField]
        private UnityEvent recordingStarted = new UnityEvent();

        private bool ownsPointerCapture;

        public UnityEvent RecordingStarted => recordingStarted;

        /// <summary>
        /// Registers cancel and state listeners for this enabled lifetime.
        /// </summary>
        private void OnEnable()
        {
            if (cancelButton != null)
            {
                cancelButton.onClick.AddListener(HandleCancelClicked);
            }

            if (voiceInput != null)
            {
                voiceInput.StateChanged += HandleVoiceInputStateChanged;
            }
        }

        /// <summary>
        /// Initializes disclosure and visible state without starting microphone capture.
        /// </summary>
        private void Start()
        {
            if (disclosureText != null)
            {
                disclosureText.text =
                    "마이크 음성이 AI 전사를 위해 처리됩니다.";
            }

            HandleVoiceInputStateChanged(
                voiceInput?.State ?? VoiceInputState.Failed,
                string.Empty);
        }

        /// <summary>
        /// Removes listeners and cancels pointer-owned work on disable.
        /// </summary>
        private void OnDisable()
        {
            if (cancelButton != null)
            {
                cancelButton.onClick.RemoveListener(HandleCancelClicked);
            }

            if (voiceInput != null)
            {
                voiceInput.StateChanged -= HandleVoiceInputStateChanged;
                voiceInput.CancelInput();
            }

            ownsPointerCapture = false;
        }

        /// <summary>
        /// Starts recording on the primary pointer while stopping optional output first.
        /// </summary>
        public void OnPointerDown(PointerEventData eventData)
        {
            if (eventData == null
                || eventData.button != PointerEventData.InputButton.Left
                || pushToTalkButton == null
                || !pushToTalkButton.interactable
                || voiceInput == null)
            {
                return;
            }

            try
            {
                recordingStarted?.Invoke();
            }
            catch (Exception)
            {
                Debug.LogError(
                    "A recording-start listener encountered an unexpected failure.",
                    this);
            }

            ownsPointerCapture = voiceInput.StartRecording();
        }

        /// <summary>
        /// Stops pointer-owned recording and starts bounded transcription on release.
        /// </summary>
        public async void OnPointerUp(PointerEventData eventData)
        {
            if (eventData == null
                || eventData.button != PointerEventData.InputButton.Left
                || !ownsPointerCapture)
            {
                return;
            }

            ownsPointerCapture = false;
            try
            {
                var result = await voiceInput.StopAndTranscribeAsync();
                if (result != null)
                {
                    textInputView?.SetInputText(result.Text);
                }
            }
            catch (Exception)
            {
                Debug.LogError(
                    "NPC voice input encountered an unexpected local failure.",
                    this);
            }
        }

        /// <summary>
        /// Cancels active input through the EventSystem cancel action, normally Escape.
        /// </summary>
        public void OnCancel(BaseEventData eventData)
        {
            if (voiceInput == null
                || (voiceInput.State != VoiceInputState.Recording
                    && voiceInput.State != VoiceInputState.Transcribing))
            {
                return;
            }

            HandleCancelClicked();
            eventData?.Use();
        }

        /// <summary>
        /// Cancels recording or transcription while leaving existing typed text unchanged.
        /// </summary>
        private void HandleCancelClicked()
        {
            ownsPointerCapture = false;
            voiceInput?.CancelInput();
        }

        /// <summary>
        /// Maps pure input states and safe failures to concise prototype controls.
        /// </summary>
        private void HandleVoiceInputStateChanged(
            VoiceInputState state,
            string message)
        {
            if (transcriptionStatusText != null)
            {
                switch (state)
                {
                    case VoiceInputState.Recording:
                        transcriptionStatusText.text = "음성 입력: 녹음 중";
                        break;
                    case VoiceInputState.Transcribing:
                        transcriptionStatusText.text = "음성 입력: 전사 중";
                        break;
                    case VoiceInputState.Failed:
                        transcriptionStatusText.text = string.IsNullOrWhiteSpace(message)
                            ? "음성 입력: 실패"
                            : $"음성 입력: 실패 ({message})";
                        break;
                    default:
                        transcriptionStatusText.text = "음성 입력: 준비";
                        break;
                }
            }

            if (pushToTalkButton != null)
            {
                pushToTalkButton.interactable = state == VoiceInputState.Idle
                    || state == VoiceInputState.Failed;
            }

            if (cancelButton != null)
            {
                cancelButton.interactable = state == VoiceInputState.Recording
                    || state == VoiceInputState.Transcribing;
            }
        }
    }
}
