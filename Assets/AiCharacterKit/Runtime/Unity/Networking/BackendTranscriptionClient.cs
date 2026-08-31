using System;
using System.Threading;
using System.Threading.Tasks;
using AiCharacterKit.Transcription;
using AiCharacterKit.Transport.Transcription.V1;

namespace AiCharacterKit.Unity.Networking
{
    /// <summary>
    /// Adapts the Transcription V1 gateway to the provider-neutral transcription interface.
    /// </summary>
    public sealed class BackendTranscriptionClient : ITranscriptionClient
    {
        private readonly IAiTranscriptionBackendGateway gateway;

        /// <summary>
        /// Creates a client with an explicit backend gateway.
        /// </summary>
        public BackendTranscriptionClient(IAiTranscriptionBackendGateway gateway)
        {
            this.gateway = gateway ?? throw new ArgumentNullException(nameof(gateway));
        }

        /// <summary>
        /// Correlates one captured WAV and maps its validated response to pure models.
        /// </summary>
        public async Task<TranscriptionResult> TranscribeAsync(
            CapturedAudioData audioData,
            CancellationToken cancellationToken)
        {
            if (audioData == null)
            {
                throw new ArgumentNullException(nameof(audioData));
            }

            cancellationToken.ThrowIfCancellationRequested();
            var requestId = CreateRequestId();
            var response = await gateway.SendAsync(
                audioData.WaveBytes,
                requestId,
                cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();

            if (response == null
                || !string.Equals(
                    response.requestId,
                    requestId,
                    StringComparison.Ordinal)
                || !TranscriptionContractValidator.TryValidateResponse(
                    response,
                    out _))
            {
                throw CreateProtocolException();
            }

            try
            {
                if (response.status == TranscriptionContractV1.ErrorStatus)
                {
                    throw TranscriptionContractMapper.ReadError(response);
                }

                return TranscriptionContractMapper.ReadResult(response);
            }
            catch (TranscriptionException)
            {
                throw;
            }
            catch (Exception exception)
            {
                throw CreateProtocolException(exception);
            }
        }

        /// <summary>
        /// Creates one locally unique opaque correlation value for one audio operation.
        /// </summary>
        private static string CreateRequestId()
        {
            return "transcription-" + Guid.NewGuid().ToString("N");
        }

        /// <summary>
        /// Hides transport validation details behind one stable public failure.
        /// </summary>
        private static TranscriptionException CreateProtocolException(
            Exception innerException = null)
        {
            return new TranscriptionException(
                TranscriptionBackendErrorCodes.BackendProtocolError,
                "음성 전사 백엔드 응답 계약이 올바르지 않습니다.",
                false,
                innerException);
        }
    }
}
