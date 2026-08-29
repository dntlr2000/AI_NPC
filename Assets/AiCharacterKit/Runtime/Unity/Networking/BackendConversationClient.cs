using System;
using System.Threading;
using System.Threading.Tasks;
using AiCharacterKit.Core;
using AiCharacterKit.Transport.V1;

namespace AiCharacterKit.Unity.Networking
{
    /// <summary>
    /// Adapts the existing Core conversation contract to the versioned backend gateway.
    /// </summary>
    public sealed class BackendConversationClient : IAiConversationClient
    {
        private readonly IAiNpcBackendGateway gateway;

        /// <summary>
        /// Creates a backend client with an explicit, replaceable transport gateway.
        /// </summary>
        public BackendConversationClient(IAiNpcBackendGateway gateway)
        {
            this.gateway = gateway ?? throw new ArgumentNullException(nameof(gateway));
        }

        /// <summary>
        /// Correlates, sends, validates, and maps one Core conversation request.
        /// </summary>
        public async Task<AiNpcResponse> SendAsync(
            AiNpcRequest request,
            CancellationToken cancellationToken)
        {
            if (request == null)
            {
                throw new ArgumentNullException(nameof(request));
            }

            cancellationToken.ThrowIfCancellationRequested();
            var requestId = CreateRequestId();
            AiNpcRequestEnvelopeDto requestEnvelope;

            try
            {
                requestEnvelope = AiNpcContractMapper.CreateRequest(request, requestId);
            }
            catch (Exception exception)
            {
                throw CreateProtocolException(exception);
            }

            var responseEnvelope = await gateway.SendAsync(
                requestEnvelope,
                cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();

            if (!AiNpcContractValidator.TryValidateResponse(
                    responseEnvelope,
                    out _)
                || !string.Equals(
                    responseEnvelope.requestId,
                    requestId,
                    StringComparison.Ordinal))
            {
                throw CreateProtocolException();
            }

            if (responseEnvelope.status == AiNpcContractV1.ErrorStatus)
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
        /// Creates a locally unique opaque correlation value for one submission.
        /// </summary>
        private static string CreateRequestId()
        {
            return "req-" + Guid.NewGuid().ToString("N");
        }

        /// <summary>
        /// Hides internal contract details behind one stable client-side error.
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
