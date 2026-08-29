using System.Threading;
using System.Threading.Tasks;
using AiCharacterKit.Transport.V2;

namespace AiCharacterKit.Unity.Networking
{
    /// <summary>
    /// Sends V2 conversation and reset envelopes without exposing HTTP to Core.
    /// </summary>
    public interface IAiNpcSessionBackendGateway
    {
        /// <summary>
        /// Sends one validated session-aware request and returns its validated response.
        /// </summary>
        Task<AiNpcResponseEnvelopeDto> SendAsync(
            AiNpcRequestEnvelopeDto request,
            CancellationToken cancellationToken);

        /// <summary>
        /// Sends one validated reset request and returns its validated acknowledgement.
        /// </summary>
        Task<AiNpcSessionResetResponseDto> ResetAsync(
            AiNpcSessionResetRequestDto request,
            CancellationToken cancellationToken);
    }
}
