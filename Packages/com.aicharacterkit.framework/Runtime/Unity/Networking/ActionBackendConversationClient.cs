using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using AiCharacterKit.Core;
using AiCharacterKit.Transport.V3;

namespace AiCharacterKit.Unity.Networking
{
    /// <summary>
    /// Adapts one stable V3 session and bounded trigger snapshot to the Core conversation API.
    /// </summary>
    public sealed class ActionBackendConversationClient : IResettableAiConversationClient
    {
        private readonly IAiNpcActionBackendGateway gateway;
        private readonly string characterId;
        private readonly string sessionId;
        private readonly IReadOnlyList<NpcTriggerDefinition> definitions;
        private readonly HashSet<string> knownTriggerIds;

        /// <summary>
        /// Creates a component-lifetime action session with a generated opaque identifier.
        /// </summary>
        public ActionBackendConversationClient(
            IAiNpcActionBackendGateway gateway,
            string characterId,
            IReadOnlyList<NpcTriggerDefinition> definitions)
            : this(gateway, characterId, definitions, CreateSessionId())
        {
        }

        /// <summary>
        /// Creates an action session with an explicit identifier for deterministic tests.
        /// </summary>
        public ActionBackendConversationClient(
            IAiNpcActionBackendGateway gateway,
            string characterId,
            IReadOnlyList<NpcTriggerDefinition> definitions,
            string sessionId)
        {
            this.gateway = gateway ?? throw new ArgumentNullException(nameof(gateway));
            if (string.IsNullOrWhiteSpace(characterId))
            {
                throw new ArgumentException("Character ID must not be empty.", nameof(characterId));
            }

            if (string.IsNullOrWhiteSpace(sessionId)
                || sessionId.Length > AiNpcContractV3.MaxSessionIdLength)
            {
                throw new ArgumentException(
                    "Session ID must be non-empty and within the V3 limit.",
                    nameof(sessionId));
            }

            if (definitions == null || definitions.Count == 0
                || definitions.Count > AiNpcContractV3.MaxTriggerCount)
            {
                throw new ArgumentException(
                    "V3 requires a bounded non-empty trigger snapshot.",
                    nameof(definitions));
            }

            var copy = new List<NpcTriggerDefinition>(definitions.Count);
            knownTriggerIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (var definition in definitions)
            {
                if (definition == null || !knownTriggerIds.Add(definition.TriggerId))
                {
                    throw new ArgumentException(
                        "Trigger definitions must be non-null with unique IDs.",
                        nameof(definitions));
                }

                copy.Add(definition);
            }

            this.characterId = characterId;
            this.sessionId = sessionId;
            this.definitions = copy.AsReadOnly();
        }

        /// <summary>
        /// Sends one action-aware request and rejects any model ID outside its snapshot.
        /// </summary>
        public async Task<AiNpcResponse> SendAsync(
            AiNpcRequest request,
            CancellationToken cancellationToken)
        {
            if (request == null)
            {
                throw new ArgumentNullException(nameof(request));
            }

            if (!string.Equals(request.CharacterId, characterId, StringComparison.Ordinal))
            {
                throw CreateProtocolException();
            }

            cancellationToken.ThrowIfCancellationRequested();
            var requestId = CreateRequestId();
            AiNpcRequestEnvelopeDto requestEnvelope;
            try
            {
                requestEnvelope = AiNpcContractMapper.CreateRequest(
                    request,
                    requestId,
                    sessionId,
                    definitions);
            }
            catch (Exception exception)
            {
                throw CreateProtocolException(exception);
            }

            var responseEnvelope = await gateway.SendAsync(
                requestEnvelope,
                cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            ValidateResponse(responseEnvelope, requestId);
            if (responseEnvelope.status == AiNpcContractV3.ErrorStatus)
            {
                throw new AiConversationException(
                    responseEnvelope.error.code,
                    responseEnvelope.error.message,
                    responseEnvelope.error.retryable);
            }

            foreach (var triggerId in responseEnvelope.result.matchedTriggerIds)
            {
                if (!knownTriggerIds.Contains(triggerId))
                {
                    throw CreateProtocolException();
                }
            }

            try
            {
                return AiNpcContractMapper.ReadSuccessResponse(responseEnvelope);
            }
            catch (Exception exception)
            {
                throw CreateProtocolException(exception);
            }
        }

        /// <summary>
        /// Clears the current V3 server session without changing its opaque identifier.
        /// </summary>
        public async Task ResetAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var requestId = CreateRequestId();
            AiNpcSessionResetRequestDto resetRequest;
            try
            {
                resetRequest = AiNpcContractMapper.CreateResetRequest(
                    requestId,
                    sessionId,
                    characterId);
            }
            catch (Exception exception)
            {
                throw CreateProtocolException(exception);
            }

            var resetResponse = await gateway.ResetAsync(
                resetRequest,
                cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            if (!AiNpcContractValidator.TryValidateResetResponse(resetResponse, out _)
                || !string.Equals(resetResponse.requestId, requestId, StringComparison.Ordinal))
            {
                throw CreateProtocolException();
            }

            if (resetResponse.status == AiNpcContractV3.ErrorStatus)
            {
                throw new AiConversationException(
                    resetResponse.error.code,
                    resetResponse.error.message,
                    resetResponse.error.retryable);
            }
        }

        /// <summary>
        /// Verifies one V3 response and its caller-owned correlation ID.
        /// </summary>
        private static void ValidateResponse(
            AiNpcResponseEnvelopeDto response,
            string requestId)
        {
            if (!AiNpcContractValidator.TryValidateResponse(response, out _)
                || !string.Equals(response.requestId, requestId, StringComparison.Ordinal))
            {
                throw CreateProtocolException();
            }
        }

        /// <summary>
        /// Creates one opaque identifier shared by this V3 client lifetime.
        /// </summary>
        private static string CreateSessionId()
        {
            return "session-" + Guid.NewGuid().ToString("N");
        }

        /// <summary>
        /// Creates one locally unique V3 request correlation value.
        /// </summary>
        private static string CreateRequestId()
        {
            return "req-" + Guid.NewGuid().ToString("N");
        }

        /// <summary>
        /// Hides V3 validation details behind the existing safe client error.
        /// </summary>
        private static AiConversationException CreateProtocolException(
            Exception innerException = null)
        {
            return new AiConversationException(
                AiNpcBackendErrorCodes.BackendProtocolError,
                "백엔드 응답 계약이 올바르지 않습니다.",
                false,
                innerException);
        }
    }
}
