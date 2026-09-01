using System.Threading;
using System.Threading.Tasks;
using AiCharacterKit.Transport.Speech.V1;

namespace AiCharacterKit.Unity.Networking
{
    /// <summary>
    /// Sends one Speech V1 request without exposing HTTP to the synthesis client.
    /// </summary>
    public interface IAiSpeechBackendGateway
    {
        /// <summary>
        /// Returns correlated fixed-format PCM bytes or throws a safe speech failure.
        /// </summary>
        Task<SpeechBackendAudioResponse> SendAsync(
            SpeechSynthesisRequestDto request,
            CancellationToken cancellationToken);
    }
}
