using System.Threading;
using System.Threading.Tasks;

namespace AiCharacterKit.Core
{
    /// <summary>
    /// Adds optional short-memory reset support without changing stateless clients.
    /// </summary>
    public interface IResettableAiConversationClient : IAiConversationClient
    {
        /// <summary>
        /// Clears the current client-owned conversation session.
        /// </summary>
        Task ResetAsync(CancellationToken cancellationToken);
    }
}
