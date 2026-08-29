using System.Threading;
using System.Threading.Tasks;
using AiCharacterKit.Core;
using AiCharacterKit.Unity.Networking;
using UnityEngine;

namespace AiCharacterKit.Unity
{
    /// <summary>
    /// Bridges a Unity character profile and lifecycle to the pure conversation controller.
    /// </summary>
    public sealed class NpcConversationBehaviour : MonoBehaviour
    {
        [SerializeField]
        private CharacterProfile characterProfile;

        [SerializeField]
        private MonoBehaviour presentationDriverSource;

        [SerializeField]
        private NpcConversationMode conversationMode = NpcConversationMode.Mock;

        [SerializeField]
        private string backendEndpoint =
            "http://127.0.0.1:8787/v1/npc/respond";

        [SerializeField]
        private string sessionBackendEndpoint =
            "http://127.0.0.1:8787/v2/npc/respond";

        [SerializeField]
        private string sessionResetEndpoint =
            "http://127.0.0.1:8787/v2/npc/sessions/reset";

        [SerializeField]
        [Min(1)]
        private int backendTimeoutSeconds = 35;

        private INpcPresentationDriver presentationDriver;
        private NpcAIController controller;
        private CancellationTokenSource lifetimeCancellation;

        public bool IsRequestInProgress =>
            controller != null && controller.IsRequestInProgress;

        public bool SupportsConversationReset =>
            controller != null && controller.SupportsReset;

        /// <summary>
        /// Validates serialized dependencies and creates the selected conversation composition.
        /// </summary>
        private void Awake()
        {
            if (!TryInitialize())
            {
                enabled = false;
            }
        }

        /// <summary>
        /// Creates a cancellation scope for the current enabled lifetime.
        /// </summary>
        private void OnEnable()
        {
            lifetimeCancellation = new CancellationTokenSource();
        }

        /// <summary>
        /// Presents the profile's initial dialogue and default command state.
        /// </summary>
        private void Start()
        {
            if (presentationDriver == null || characterProfile == null)
            {
                return;
            }

            PresentInitialState();
        }

        /// <summary>
        /// Cancels active work when the Unity component leaves its enabled lifetime.
        /// </summary>
        private void OnDisable()
        {
            controller?.CancelActiveRequest();

            if (lifetimeCancellation != null)
            {
                lifetimeCancellation.Cancel();
                lifetimeCancellation.Dispose();
                lifetimeCancellation = null;
            }
        }

        /// <summary>
        /// Releases the pure controller when the Unity component is destroyed.
        /// </summary>
        private void OnDestroy()
        {
            controller?.Dispose();
            controller = null;
        }

        /// <summary>
        /// Converts the active profile and user text into a pure request and submits it.
        /// </summary>
        public async Task<bool> SubmitAsync(string userText)
        {
            if (controller == null || characterProfile == null || lifetimeCancellation == null)
            {
                presentationDriver?.PresentError("NPC 대화 구성이 준비되지 않았습니다.");
                return false;
            }

            var request = new AiNpcRequest(
                characterProfile.CharacterId,
                characterProfile.DisplayName,
                characterProfile.Personality,
                characterProfile.SpeechStyle,
                characterProfile.ExampleDialogue,
                characterProfile.DefaultEmotion,
                userText);

            return await controller.SubmitAsync(
                request,
                lifetimeCancellation.Token);
        }

        /// <summary>
        /// Clears optional short memory and restores the profile's initial presentation.
        /// </summary>
        public async Task<bool> ResetConversationAsync()
        {
            if (controller == null || lifetimeCancellation == null)
            {
                presentationDriver?.PresentError("NPC 대화 구성이 준비되지 않았습니다.");
                return false;
            }

            var succeeded = await controller.ResetConversationAsync(
                lifetimeCancellation.Token);
            if (succeeded)
            {
                PresentInitialState();
            }

            return succeeded;
        }

        /// <summary>
        /// Resolves the interface adapter and creates the configured conversation controller.
        /// </summary>
        private bool TryInitialize()
        {
            if (characterProfile == null)
            {
                Debug.LogError(
                    "NpcConversationBehaviour requires a CharacterProfile.",
                    this);
                return false;
            }

            if (!characterProfile.TryValidate(out var validationError))
            {
                Debug.LogError(
                    $"NpcConversationBehaviour received an invalid CharacterProfile: {validationError}",
                    this);
                return false;
            }

            presentationDriver = presentationDriverSource as INpcPresentationDriver;
            if (presentationDriver == null)
            {
                Debug.LogError(
                    "Presentation Driver Source must implement INpcPresentationDriver.",
                    this);
                return false;
            }

            if (!TryCreateConversationClient(out var conversationClient))
            {
                return false;
            }

            controller = new NpcAIController(conversationClient, presentationDriver);
            return true;
        }

        /// <summary>
        /// Creates only the client selected by the serialized prototype mode.
        /// </summary>
        private bool TryCreateConversationClient(
            out IAiConversationClient conversationClient)
        {
            conversationClient = null;
            if (conversationMode == NpcConversationMode.Mock)
            {
                conversationClient = new MockConversationClient();
                return true;
            }

            if (conversationMode == NpcConversationMode.Backend)
            {
                try
                {
                    var gateway = new UnityWebRequestAiNpcBackendGateway(
                        backendEndpoint,
                        backendTimeoutSeconds);
                    conversationClient = new BackendConversationClient(gateway);
                    return true;
                }
                catch (System.ArgumentException exception)
                {
                    Debug.LogError(
                        $"Invalid NPC backend configuration: {exception.Message}",
                        this);
                    return false;
                }
            }

            if (conversationMode == NpcConversationMode.BackendSession)
            {
                try
                {
                    var gateway = new UnityWebRequestAiNpcSessionBackendGateway(
                        sessionBackendEndpoint,
                        sessionResetEndpoint,
                        backendTimeoutSeconds);
                    conversationClient = new SessionBackendConversationClient(
                        gateway,
                        characterProfile.CharacterId);
                    return true;
                }
                catch (System.ArgumentException exception)
                {
                    Debug.LogError(
                        $"Invalid NPC session backend configuration: {exception.Message}",
                        this);
                    return false;
                }
            }

            if (conversationMode != NpcConversationMode.Backend)
            {
                Debug.LogError(
                    $"Unsupported NPC conversation mode '{conversationMode}'.",
                    this);
                return false;
            }

            return false;
        }

        /// <summary>
        /// Restores the profile dialogue, default emotion, and neutral gesture pose.
        /// </summary>
        private void PresentInitialState()
        {
            if (presentationDriver == null || characterProfile == null)
            {
                return;
            }

            var initialDialogue = string.IsNullOrWhiteSpace(characterProfile.ExampleDialogue)
                ? $"{characterProfile.DisplayName}와 대화를 시작해 보세요."
                : $"{characterProfile.DisplayName}: {characterProfile.ExampleDialogue}";

            presentationDriver.PresentDialogue(initialDialogue);
            presentationDriver.PresentEmotion(characterProfile.DefaultEmotion);
            presentationDriver.PresentGesture(NpcGesture.None);
            presentationDriver.SetBusy(false);
        }
    }
}
