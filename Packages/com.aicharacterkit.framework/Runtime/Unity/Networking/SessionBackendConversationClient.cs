using System;
using System.Threading;
using System.Threading.Tasks;
using AiCharacterKit.Core;
using AiCharacterKit.Transport.V2;

namespace AiCharacterKit.Unity.Networking
{
    /// <summary>
    /// Adapts one stable character-bound session to the existing Core conversation API.
    /// </summary>
    public sealed class SessionBackendConversationClient : IResettableAiConversationClient
    {
        private readonly IAiNpcSessionBackendGateway gateway;
        private readonly string characterId;
        private readonly string sessionId;

        /// <summary>
        /// Creates a component-lifetime session with a generated opaque identifier.
        /// </summary>
        public SessionBackendConversationClient(
            IAiNpcSessionBackendGateway gateway,
            string characterId)
            : this(gateway, characterId, CreateSessionId())
        {
        }

        /// <summary>
        /// Creates a session with an explicit identifier for deterministic integration tests.
        /// </summary>
        public SessionBackendConversationClient(
            IAiNpcSessionBackendGateway gateway,
            string characterId,
            string sessionId)
        {
            this.gateway = gateway ?? throw new ArgumentNullException(nameof(gateway));
            if (string.IsNullOrWhiteSpace(characterId))
            {
                throw new ArgumentException(
                    "Character ID must not be empty.",
                    nameof(characterId));
            }

            if (string.IsNullOrWhiteSpace(sessionId)
                || sessionId.Length > AiNpcContractV2.MaxSessionIdLength)
            {
                throw new ArgumentException(
                    "Session ID must be non-empty and within the V2 limit.",
                    nameof(sessionId));
            }

            this.characterId = characterId;
            this.sessionId = sessionId;
        }

        /// <summary>
        /// Correlates and maps one request while preserving the stable session ID.
        /// </summary>
        public async Task<AiNpcResponse> SendAsync(
            AiNpcRequest request,
            CancellationToken cancellationToken)
        {
            if (request == null)
            {
                throw new ArgumentNullException(nameof(request));
            }

            if (!string.Equals(
                    request.CharacterId,
                    characterId,
                    StringComparison.Ordinal))
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
                    sessionId);
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

            if (responseEnvelope.status == AiNpcContractV2.ErrorStatus)
            {
                throw new AiConversationException(
                    responseEnvelope.error.code,
                    responseEnvelope.error.message,
                    responseEnvelope.error.retryable);
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
        /// Clears the current server session without changing its opaque identifier.
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
            if (!AiNpcContractValidator.TryValidateResetResponse(
                    resetResponse,
                    out _)
                || !string.Equals(
                    resetResponse.requestId,
                    requestId,
                    StringComparison.Ordinal))
            {
                throw CreateProtocolException();
            }

            if (resetResponse.status == AiNpcContractV2.ErrorStatus)
            {
                throw new AiConversationException(
                    resetResponse.error.code,
                    resetResponse.error.message,
                    resetResponse.error.retryable);
            }
        }

        /// <summary>
        /// Verifies one conversation response and its caller-owned correlation ID.
        /// </summary>
        private static void ValidateResponse(
            AiNpcResponseEnvelopeDto response,
            string requestId)
        {
            if (!AiNpcContractValidator.TryValidateResponse(response, out _)
                || !string.Equals(
                    response.requestId,
                    requestId,
                    StringComparison.Ordinal))
            {
                throw CreateProtocolException();
            }
        }

        /// <summary>
        /// Creates one opaque identifier shared by all requests in this client lifetime.
        /// </summary>
        private static string CreateSessionId()
        {
            return "session-" + Guid.NewGuid().ToString("N");
        }

        /// <summary>
        /// Creates a locally unique correlation value for one network operation.
        /// </summary>
        private static string CreateRequestId()
        {
            return "req-" + Guid.NewGuid().ToString("N");
        }

        /// <summary>
        /// Hides V2 validation details behind the existing safe client-side error.
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
