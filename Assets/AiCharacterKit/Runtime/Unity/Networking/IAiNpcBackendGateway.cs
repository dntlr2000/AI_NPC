using System.Threading;
using System.Threading.Tasks;
using AiCharacterKit.Transport.V1;

namespace AiCharacterKit.Unity.Networking
{
    /// <summary>
    /// Sends one V1 envelope to a backend without exposing HTTP to the conversation client.
    /// </summary>
    public interface IAiNpcBackendGateway
    {
        /// <summary>
        /// Sends one validated request and returns its validated response envelope.
        /// </summary>
        Task<AiNpcResponseEnvelopeDto> SendAsync(
            AiNpcRequestEnvelopeDto request,
            CancellationToken cancellationToken);
    }
}
