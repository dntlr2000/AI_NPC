using System.Threading;
using System.Threading.Tasks;
using AiCharacterKit.Transport.V3;

namespace AiCharacterKit.Unity.Networking
{
    /// <summary>
    /// Sends V3 action-aware conversation and reset envelopes without exposing HTTP to Core.
    /// </summary>
    public interface IAiNpcActionBackendGateway
    {
        /// <summary>
        /// Sends one validated V3 request and returns its validated response.
        /// </summary>
        Task<AiNpcResponseEnvelopeDto> SendAsync(
            AiNpcRequestEnvelopeDto request,
            CancellationToken cancellationToken);

        /// <summary>
        /// Sends one validated V3 reset and returns its acknowledgement.
        /// </summary>
        Task<AiNpcSessionResetResponseDto> ResetAsync(
            AiNpcSessionResetRequestDto request,
            CancellationToken cancellationToken);
    }
}
