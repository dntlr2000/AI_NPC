using System.Threading;
using System.Threading.Tasks;
using AiCharacterKit.Transport.V4;

namespace AiCharacterKit.Unity.Networking
{
    /// <summary>
    /// Sends V4 context-grounded envelopes without exposing HTTP to Core.
    /// </summary>
    public interface IAiNpcContextBackendGateway
    {
        /// <summary>
        /// Sends one validated V4 request and returns its validated response.
        /// </summary>
        Task<AiNpcResponseEnvelopeDto> SendAsync(
            AiNpcRequestEnvelopeDto request,
            CancellationToken cancellationToken);

        /// <summary>
        /// Sends one validated V4 reset and returns its acknowledgement.
        /// </summary>
        Task<AiNpcSessionResetResponseDto> ResetAsync(
            AiNpcSessionResetRequestDto request,
            CancellationToken cancellationToken);
    }
}
