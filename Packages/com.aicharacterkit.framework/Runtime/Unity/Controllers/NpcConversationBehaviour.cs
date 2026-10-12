using System.Threading;
using System.Threading.Tasks;
using AiCharacterKit.Core;
using AiCharacterKit.Unity.Actions;
using AiCharacterKit.Unity.Networking;
using AiCharacterKit.Unity.LocalInference;
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
        private string actionBackendEndpoint =
            "http://127.0.0.1:8787/v3/npc/respond";

        [SerializeField]
        private string actionResetEndpoint =
            "http://127.0.0.1:8787/v3/npc/sessions/reset";

        [SerializeField]
        private string contextBackendEndpoint =
            "http://127.0.0.1:8787/v4/npc/respond";

        [SerializeField]
        private NpcInferenceStrategy inferenceStrategy = NpcInferenceStrategy.SinglePass;

        [SerializeField]
        private string twoStageContextBackendEndpoint = "http://127.0.0.1:8787/v4/npc/respond-two-stage";

        [SerializeField]
        private string contextResetEndpoint =
            "http://127.0.0.1:8787/v4/npc/sessions/reset";

        [SerializeField]
        private NpcContextCoordinator contextCoordinator;

        [SerializeField]
        private NpcActionCoordinator actionCoordinator;

        [SerializeField]
        private LocalInferenceRuntime localInferenceRuntime;

        private System.IDisposable ownedConversationClient;

        [SerializeField]
        [Min(1)]
        private int backendTimeoutSeconds = 35;

        private INpcPresentationDriver presentationDriver;
        private INpcActionResponseFormatter localActionResponseFormatter;
        private NpcAIController controller;
        private bool submissionInProgress;
        private string submissionFailureCode;
        public double LastSubmissionSeconds { get; private set; }
        public AiCharacterKit.LocalInference.LocalInferenceTurnDiagnostics LastInferenceDiagnostics { get; private set; }
        private CancellationTokenSource lifetimeCancellation;

        public NpcInferenceStrategy InferenceStrategy => inferenceStrategy;

        public string LastContextRevision { get; private set; } = string.Empty;

        public System.Collections.Generic.IReadOnlyList<string> LastOmittedContextFactIds
        {
            get;
            private set;
        } = System.Array.Empty<string>();

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
            ownedConversationClient?.Dispose();
            ownedConversationClient = null;
            controller = null;
        }

        /// <summary>
        /// Reserves optional Advanced state throughout capture, generation, presentation and action processing.
        /// </summary>
        public async Task<bool> SubmitAsync(string userText)
        {
            if (submissionInProgress || IsRequestInProgress)
            {
                presentationDriver?.PresentError("이미 대화 요청을 처리하고 있습니다.");
                return false;
            }
            System.IDisposable reservation = null;
            if (controller != null && actionCoordinator != null
                && !actionCoordinator.TryReserveBehaviorTurn(out reservation, out _))
            {
                presentationDriver?.PresentError("이미 대화 요청을 처리하고 있습니다.");
                return false;
            }
            submissionInProgress = true;
            LastInferenceDiagnostics = null;
            submissionFailureCode = "generation_failed";
            var number = actionCoordinator?.BeginBehaviorConversationTurn() ?? 0;
            var watch = System.Diagnostics.Stopwatch.StartNew();
            var succeeded = false;
            try { succeeded = await SubmitPreparedAsync(userText); return succeeded; }
            finally
            {
                LastSubmissionSeconds = watch.Elapsed.TotalSeconds;
                var failure = lifetimeCancellation == null || lifetimeCancellation.IsCancellationRequested
                    ? "cancelled" : submissionFailureCode;
                actionCoordinator?.EndBehaviorConversationTurn(number, succeeded, failure);
                reservation?.Dispose();
                submissionInProgress = false;
            }
        }

        /// <summary>Builds one immutable request under the caller's optional behavior reservation.</summary>
        private async Task<bool> SubmitPreparedAsync(string userText)
        {
            if (controller == null || characterProfile == null || lifetimeCancellation == null)
            {
                presentationDriver?.PresentError("NPC 대화 구성이 준비되지 않았습니다.");
                return false;
            }

            var grounding = NpcGroundingSnapshot.Empty;
            if (UsesGrounding)
            {
                var omittedFactIds =
                    (System.Collections.Generic.IReadOnlyList<string>)System.Array.Empty<string>();
                var contextError = string.Empty;
                if (contextCoordinator == null
                    || !contextCoordinator.TryCreateSnapshot(
                        characterProfile,
                        out grounding,
                        out omittedFactIds,
                        out contextError))
                {
                    submissionFailureCode = "context_capture_failed";
                    presentationDriver?.PresentError(
                        string.IsNullOrWhiteSpace(contextError)
                            ? "NPC 컨텍스트 구성이 준비되지 않았습니다."
                            : contextError);
                    return false;
                }

                LastContextRevision = grounding.Revision;
                LastOmittedContextFactIds = omittedFactIds;
            }

            var request = new AiNpcRequest(
                characterProfile.CharacterId,
                characterProfile.DisplayName,
                characterProfile.Personality,
                characterProfile.SpeechStyle,
                characterProfile.ExampleDialogue,
                characterProfile.DefaultEmotion,
                userText,
                grounding);

            var localClient = ownedConversationClient as AiCharacterKit.LocalInference.LocalInferenceConversationClient;
            var previousDiagnostics = localClient?.LastDiagnostics;
            try
            {
                return await controller.SubmitAsync(request, lifetimeCancellation.Token);
            }
            finally
            {
                var completed = localClient?.LastDiagnostics;
                if (!ReferenceEquals(completed, previousDiagnostics)) LastInferenceDiagnostics = completed;
            }
        }

        /// <summary>
        /// Reserves Advanced state during memory reset while retaining variables and action gates.
        /// </summary>
        public async Task<bool> ResetConversationAsync()
        {
            if (submissionInProgress || IsRequestInProgress)
            {
                presentationDriver?.PresentError("이미 대화 요청을 처리하고 있습니다.");
                return false;
            }
            System.IDisposable reservation = null;
            if (controller != null && actionCoordinator != null
                && !actionCoordinator.TryReserveBehaviorTurn(out reservation, out _))
            {
                presentationDriver?.PresentError("이미 대화 요청을 처리하고 있습니다.");
                return false;
            }
            try { return await ResetPreparedConversationAsync(); }
            finally { reservation?.Dispose(); }
        }

        /// <summary>Clears only optional conversation memory and restores the initial presentation.</summary>
        private async Task<bool> ResetPreparedConversationAsync()
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
        /// Resolves adapters, opting local consumers into action-before-presentation only when a formatter exists.
        /// </summary>
        private bool TryInitialize()
        {
            if (!NpcInferenceAvailability.TryValidate(conversationMode, inferenceStrategy, out var strategyError))
            {
                Debug.LogError(strategyError, this);
                return false;
            }
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

            if (actionCoordinator != null && actionCoordinator.IsAdvanced
                && (conversationMode == NpcConversationMode.Backend || conversationMode == NpcConversationMode.BackendSession))
            {
                Debug.LogError("Advanced behavior requires Mock, BackendActions, BackendContext or LocalInference.", this);
                return false;
            }

            if (actionCoordinator != null
                && !actionCoordinator.TryInitialize(out var actionError))
            {
                Debug.LogError(
                    $"Invalid NPC action configuration: {actionError}",
                    this);
                return false;
            }

            if (UsesGrounding)
            {
                if (contextCoordinator == null)
                {
                    Debug.LogError(
                        "Grounded conversation modes require an NpcContextCoordinator.",
                        this);
                    return false;
                }

                if (!contextCoordinator.TryValidateConfiguration(
                        characterProfile,
                        out var contextError))
                {
                    Debug.LogError(
                        $"Invalid NPC context configuration: {contextError}",
                        this);
                    return false;
                }
            }

            localActionResponseFormatter = conversationMode == NpcConversationMode.LocalInference && actionCoordinator != null
                ? GetComponent<INpcActionResponseFormatter>() : null;
            if (!TryCreateConversationClient(out var conversationClient))
            {
                return false;
            }

            controller = new NpcAIController(
                conversationClient,
                presentationDriver,
                localActionResponseFormatter == null ? actionCoordinator : null);
            return true;
        }

        /// <summary>
        /// Creates the selected client, finalizing opted-in local action replies before their memory commit.
        /// </summary>
        private bool TryCreateConversationClient(
            out IAiConversationClient conversationClient)
        {
            conversationClient = null;
            var triggerDefinitions = actionCoordinator == null
                ? System.Array.Empty<NpcTriggerDefinition>()
                : actionCoordinator.CreateTriggerDefinitions();
            if (conversationMode == NpcConversationMode.LocalInference)
            {
                var runtime = localInferenceRuntime != null
                    ? localInferenceRuntime : GetComponentInParent<LocalInferenceRuntime>();
                if (runtime == null || !runtime.TryValidateConfiguration(out _))
                {
                    Debug.LogError("LocalInference requires a configured explicit or parent LocalInferenceRuntime.", this);
                    return false;
                }
                try
                {
                    var client = runtime.CreateClient(characterProfile.CharacterId, triggerDefinitions, inferenceStrategy,
                        localActionResponseFormatter == null ? null : FinalizeLocalResponseAsync);
                    conversationClient = client;
                    ownedConversationClient = client;
                    return true;
                }
                catch (System.Exception)
                {
                    Debug.LogError("Local inference configuration is invalid.", this);
                    return false;
                }
            }
            if (conversationMode == NpcConversationMode.Mock)
            {
                conversationClient = new MockConversationClient(
                    System.TimeSpan.FromMilliseconds(350),
                    triggerDefinitions);
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

            if (conversationMode == NpcConversationMode.BackendActions)
            {
                if (actionCoordinator == null || triggerDefinitions.Count == 0)
                {
                    Debug.LogError(
                        "BackendActions mode requires a configured NpcActionCoordinator.",
                        this);
                    return false;
                }

                try
                {
                    var gateway = new UnityWebRequestAiNpcActionBackendGateway(
                        actionBackendEndpoint,
                        actionResetEndpoint,
                        backendTimeoutSeconds);
                    conversationClient = new ActionBackendConversationClient(
                        gateway,
                        characterProfile.CharacterId,
                        triggerDefinitions);
                    return true;
                }
                catch (System.ArgumentException exception)
                {
                    Debug.LogError(
                        $"Invalid NPC action backend configuration: {exception.Message}",
                        this);
                    return false;
                }
            }

            if (conversationMode == NpcConversationMode.BackendContext)
            {
                try
                {
                    var gateway = new UnityWebRequestAiNpcContextBackendGateway(
                        inferenceStrategy == NpcInferenceStrategy.ClassifyThenRespond ? twoStageContextBackendEndpoint : contextBackendEndpoint,
                        contextResetEndpoint,
                        backendTimeoutSeconds);
                    conversationClient = new ContextBackendConversationClient(
                        gateway,
                        characterProfile.CharacterId,
                        triggerDefinitions);
                    return true;
                }
                catch (System.ArgumentException exception)
                {
                    Debug.LogError(
                        $"Invalid NPC context backend configuration: {exception.Message}",
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

        /// <summary>Routes a validated local turn once, then formats the actual outcome without a second model call.</summary>
        private async Task<AiNpcResponse> FinalizeLocalResponseAsync(
            AiNpcRequest request, AiNpcResponse proposedResponse, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await actionCoordinator.ObserveAsync(new NpcTurnContext(request, proposedResponse), cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            return localActionResponseFormatter.FormatResponse(request, proposedResponse, actionCoordinator.LastResult);
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

        private bool UsesGrounding => conversationMode == NpcConversationMode.BackendContext
            || conversationMode == NpcConversationMode.LocalInference;
    }
}
