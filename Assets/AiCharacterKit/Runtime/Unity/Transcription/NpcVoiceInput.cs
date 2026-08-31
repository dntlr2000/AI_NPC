using System;
using System.Threading;
using System.Threading.Tasks;
using AiCharacterKit.Transcription;
using AiCharacterKit.Unity.Networking;
using UnityEngine;

namespace AiCharacterKit.Unity.Transcription
{
    /// <summary>
    /// Owns one NPC input target's microphone, backend client, controller, and lifetime.
    /// </summary>
    [RequireComponent(typeof(UnityMicrophoneCaptureDriver))]
    public sealed class NpcVoiceInput : MonoBehaviour
    {
        [SerializeField]
        private UnityMicrophoneCaptureDriver captureDriver;

        [SerializeField]
        private string backendEndpoint =
            "http://127.0.0.1:8787/v1/speech/transcribe";

        [SerializeField]
        [Min(1)]
        private int backendTimeoutSeconds = 35;

        private VoiceInputController controller;
        private CancellationTokenSource lifetimeCancellation;

        public event Action<VoiceInputState, string> StateChanged;

        public VoiceInputState State => controller?.State ?? VoiceInputState.Failed;

        public bool IsReady => controller != null && lifetimeCancellation != null;

        /// <summary>
        /// Validates serialized data and creates the provider-neutral input composition.
        /// </summary>
        private void Awake()
        {
            if (!TryInitialize())
            {
                enabled = false;
            }
        }

        /// <summary>
        /// Creates one cancellation scope for this enabled component lifetime.
        /// </summary>
        private void OnEnable()
        {
            lifetimeCancellation = new CancellationTokenSource();
        }

        /// <summary>
        /// Cancels capture and transcription when the component is disabled.
        /// </summary>
        private void OnDisable()
        {
            if (lifetimeCancellation != null)
            {
                lifetimeCancellation.Cancel();
                lifetimeCancellation.Dispose();
                lifetimeCancellation = null;
            }

            controller?.Cancel();
        }

        /// <summary>
        /// Releases the pure controller and event bridge on permanent destruction.
        /// </summary>
        private void OnDestroy()
        {
            if (controller != null)
            {
                controller.StateChanged -= HandleControllerStateChanged;
                controller.Dispose();
                controller = null;
            }

            StateChanged = null;
        }

        /// <summary>
        /// Starts one push-to-talk capture if this input target is ready and idle.
        /// </summary>
        public bool StartRecording()
        {
            return controller != null
                && lifetimeCancellation != null
                && controller.StartRecording();
        }

        /// <summary>
        /// Stops recording and returns text for caller-controlled review and submission.
        /// </summary>
        public Task<TranscriptionResult> StopAndTranscribeAsync()
        {
            if (controller == null || lifetimeCancellation == null)
            {
                return Task.FromResult<TranscriptionResult>(null);
            }

            return controller.StopAndTranscribeAsync(lifetimeCancellation.Token);
        }

        /// <summary>
        /// Cancels active capture or transcription without changing conversation state.
        /// </summary>
        public void CancelInput()
        {
            controller?.Cancel();
        }

        /// <summary>
        /// Creates the loopback gateway, provider-neutral client, and controller.
        /// </summary>
        private bool TryInitialize()
        {
            if (captureDriver == null)
            {
                captureDriver = GetComponent<UnityMicrophoneCaptureDriver>();
            }

            if (captureDriver == null)
            {
                Debug.LogError(
                    "NpcVoiceInput requires a microphone capture driver.",
                    this);
                return false;
            }

            try
            {
                var gateway = new UnityWebRequestAiTranscriptionBackendGateway(
                    backendEndpoint,
                    backendTimeoutSeconds);
                var client = new BackendTranscriptionClient(gateway);
                controller = new VoiceInputController(captureDriver, client);
                controller.StateChanged += HandleControllerStateChanged;
                return true;
            }
            catch (ArgumentException exception)
            {
                Debug.LogError(
                    $"Invalid NPC voice input configuration: {exception.Message}",
                    this);
                return false;
            }
        }

        /// <summary>
        /// Relays safe controller state to optional UI without exposing provider details.
        /// </summary>
        private void HandleControllerStateChanged(
            VoiceInputState nextState,
            string message)
        {
            var handlers = StateChanged;
            if (handlers == null)
            {
                return;
            }

            foreach (Action<VoiceInputState, string> handler
                     in handlers.GetInvocationList())
            {
                try
                {
                    handler(nextState, message ?? string.Empty);
                }
                catch
                {
                    // Optional UI listeners must not affect capture or transcription.
                }
            }
        }
    }
}
