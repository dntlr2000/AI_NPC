using System;
using System.Threading;
using System.Threading.Tasks;
using AiCharacterKit.Speech;
using AiCharacterKit.Transport.Speech.V1;

namespace AiCharacterKit.Unity.Networking
{
    /// <summary>
    /// Adapts the Speech V1 gateway to the provider-neutral synthesis interface.
    /// </summary>
    public sealed class BackendSpeechSynthesisClient : ISpeechSynthesisClient
    {
        private readonly IAiSpeechBackendGateway gateway;

        /// <summary>
        /// Creates a client with an explicit backend gateway.
        /// </summary>
        public BackendSpeechSynthesisClient(IAiSpeechBackendGateway gateway)
        {
            this.gateway = gateway ?? throw new ArgumentNullException(nameof(gateway));
        }

        /// <summary>
        /// Correlates, sends, validates, and normalizes one synthesis request.
        /// </summary>
        public async Task<SpeechAudioData> SynthesizeAsync(
            SpeechSynthesisRequest request,
            CancellationToken cancellationToken)
        {
            if (request == null)
            {
                throw new ArgumentNullException(nameof(request));
            }

            cancellationToken.ThrowIfCancellationRequested();
            var requestId = CreateRequestId();
            SpeechSynthesisRequestDto requestDto;
            try
            {
                requestDto = SpeechContractMapper.CreateRequest(request, requestId);
            }
            catch (Exception exception)
            {
                throw CreateProtocolException(exception);
            }

            var response = await gateway.SendAsync(requestDto, cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            if (response == null
                || !string.Equals(
                    response.RequestId,
                    requestId,
                    StringComparison.Ordinal))
            {
                throw CreateProtocolException();
            }

            try
            {
                return new SpeechAudioData(response.PcmBytes);
            }
            catch (Exception exception)
            {
                throw CreateProtocolException(exception);
            }
        }

        /// <summary>
        /// Creates a locally unique opaque correlation value for one speech operation.
        /// </summary>
        private static string CreateRequestId()
        {
            return "speech-" + Guid.NewGuid().ToString("N");
        }

        /// <summary>
        /// Hides transport validation details behind one stable public failure.
        /// </summary>
        private static SpeechSynthesisException CreateProtocolException(
            Exception innerException = null)
        {
            return new SpeechSynthesisException(
                SpeechBackendErrorCodes.BackendProtocolError,
                "음성 백엔드 응답 계약이 올바르지 않습니다.",
                false,
                innerException);
        }
    }
}
