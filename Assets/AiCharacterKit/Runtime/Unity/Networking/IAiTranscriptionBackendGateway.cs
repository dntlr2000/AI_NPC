using System.Threading;
using System.Threading.Tasks;
using AiCharacterKit.Transport.Transcription.V1;

namespace AiCharacterKit.Unity.Networking
{
    /// <summary>
    /// Sends one canonical WAV without exposing HTTP to the transcription client.
    /// </summary>
    public interface IAiTranscriptionBackendGateway
    {
        /// <summary>
        /// Returns one correlated success or error Transcription V1 response.
        /// </summary>
        Task<TranscriptionResponseEnvelopeDto> SendAsync(
            byte[] waveBytes,
            string requestId,
            CancellationToken cancellationToken);
    }
}
