using System.Threading;
using System.Threading.Tasks;

namespace AiCharacterKit.Core
{
    /// <summary>
    /// Defines a replaceable service that generates one structured NPC response.
    /// </summary>
    public interface IAiConversationClient
    {
        /// <summary>
        /// Generates a response for one independent request and observes cancellation.
        /// </summary>
        Task<AiNpcResponse> SendAsync(AiNpcRequest request, CancellationToken cancellationToken);
    }
}
