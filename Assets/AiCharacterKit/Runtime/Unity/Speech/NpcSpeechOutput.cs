using System;
using System.Threading;
using System.Threading.Tasks;
using AiCharacterKit.Speech;
using AiCharacterKit.Unity.Networking;
using UnityEngine;

namespace AiCharacterKit.Unity.Speech
{
    /// <summary>
    /// Owns one NPC's optional speech client, controller, playback, and enabled lifetime.
    /// </summary>
    [RequireComponent(typeof(UnityPcmSpeechPlaybackDriver))]
    public sealed class NpcSpeechOutput : MonoBehaviour
    {
        [SerializeField]
        private NpcVoiceProfile voiceProfile;

        [SerializeField]
        private UnityPcmSpeechPlaybackDriver playbackDriver;

        [SerializeField]
        private string backendEndpoint =
            "http://127.0.0.1:8787/v1/speech/synthesize";

        [SerializeField]
        [Min(1)]
        private int backendTimeoutSeconds = 35;

        [SerializeField]
        private bool speechEnabled = true;

        private NpcSpeechController controller;
        private CancellationTokenSource lifetimeCancellation;
        private bool observedActivePlayback;

        public event Action<NpcSpeechState, string> StateChanged;

        public NpcSpeechState State => controller?.State ?? NpcSpeechState.Disabled;

        public bool IsSpeechEnabled => controller != null && controller.IsEnabled;

        public bool IsReady => controller != null && lifetimeCancellation != null;

        /// <summary>
        /// Validates serialized data and creates the optional backend speech composition.
        /// </summary>
        private void Awake()
        {
            if (!TryInitialize())
            {
                enabled = false;
            }
        }

        /// <summary>
        /// Creates a cancellation scope for this enabled component lifetime.
        /// </summary>
        private void OnEnable()
        {
            lifetimeCancellation = new CancellationTokenSource();
        }

        /// <summary>
        /// Detects natural AudioSource completion and returns the controller to idle.
        /// </summary>
        private void Update()
        {
            if (controller == null
                || playbackDriver == null
                || controller.State != NpcSpeechState.Playing)
            {
                return;
            }

            if (playbackDriver.IsPlaying)
            {
                observedActivePlayback = true;
            }
            else if (observedActivePlayback)
            {
                observedActivePlayback = false;
                controller.NotifyPlaybackCompleted();
            }
        }

        /// <summary>
        /// Cancels synthesis and stops audio when this component is disabled.
        /// </summary>
        private void OnDisable()
        {
            if (lifetimeCancellation != null)
            {
                lifetimeCancellation.Cancel();
                lifetimeCancellation.Dispose();
                lifetimeCancellation = null;
            }

            controller?.Stop();
        }

        /// <summary>
        /// Releases the pure speech controller and event bridge on permanent destruction.
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
        /// Starts replaceable speech for the exact generated dialogue text.
        /// </summary>
        public bool RequestSpeech(string dialogue)
        {
            if (controller == null
                || lifetimeCancellation == null
                || !controller.IsEnabled
                || string.IsNullOrWhiteSpace(dialogue))
            {
                return false;
            }

            var request = new SpeechSynthesisRequest(
                voiceProfile.VoicePresetId,
                dialogue);
            _ = RunSpeechAsync(request, lifetimeCancellation.Token);
            return true;
        }

        /// <summary>
        /// Enables or disables optional speech without changing text presentation.
        /// </summary>
        public void SetSpeechEnabled(bool enabledValue)
        {
            speechEnabled = enabledValue;
            controller?.SetEnabled(enabledValue);
        }

        /// <summary>
        /// Cancels pending synthesis and stops current playback immediately.
        /// </summary>
        public void StopSpeech()
        {
            controller?.Stop();
        }

        /// <summary>
        /// Creates the local gateway, provider-neutral client, and playback controller.
        /// </summary>
        private bool TryInitialize()
        {
            if (voiceProfile == null)
            {
                Debug.LogError(
                    "NpcSpeechOutput requires a voice profile.",
                    this);
                return false;
            }

            if (!voiceProfile.TryValidate(out var profileError))
            {
                Debug.LogError(
                    $"NpcSpeechOutput requires a valid voice profile: {profileError}",
                    this);
                return false;
            }

            if (playbackDriver == null)
            {
                playbackDriver = GetComponent<UnityPcmSpeechPlaybackDriver>();
            }

            if (playbackDriver == null)
            {
                Debug.LogError(
                    "NpcSpeechOutput requires a PCM playback driver.",
                    this);
                return false;
            }

            try
            {
                var gateway = new UnityWebRequestAiSpeechBackendGateway(
                    backendEndpoint,
                    backendTimeoutSeconds);
                var client = new BackendSpeechSynthesisClient(gateway);
                controller = new NpcSpeechController(client, playbackDriver);
                controller.StateChanged += HandleControllerStateChanged;
                if (!speechEnabled)
                {
                    controller.SetEnabled(false);
                }

                return true;
            }
            catch (ArgumentException exception)
            {
                Debug.LogError(
                    $"Invalid NPC speech configuration: {exception.Message}",
                    this);
                return false;
            }
        }

        /// <summary>
        /// Observes one controller operation without allowing failures into Unity callbacks.
        /// </summary>
        private async Task RunSpeechAsync(
            SpeechSynthesisRequest request,
            CancellationToken cancellationToken)
        {
            try
            {
                await controller.ReplaceAsync(request, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                // Disable and replacement cancellation are expected lifecycle outcomes.
            }
            catch (Exception)
            {
                Debug.LogError(
                    "NPC speech output encountered an unexpected local failure.",
                    this);
            }
        }

        /// <summary>
        /// Relays safe speech state to optional UI without exposing provider details.
        /// </summary>
        private void HandleControllerStateChanged(
            NpcSpeechState nextState,
            string message)
        {
            if (nextState == NpcSpeechState.Playing)
            {
                observedActivePlayback = false;
            }

            var handlers = StateChanged;
            if (handlers == null)
            {
                return;
            }

            foreach (Action<NpcSpeechState, string> handler
                     in handlers.GetInvocationList())
            {
                try
                {
                    handler(nextState, message ?? string.Empty);
                }
                catch
                {
                    // Optional UI listeners must not affect dialogue or audio state.
                }
            }
        }
    }
}
