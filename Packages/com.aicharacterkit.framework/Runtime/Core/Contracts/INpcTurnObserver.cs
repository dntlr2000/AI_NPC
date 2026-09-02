using System.Threading;
using System.Threading.Tasks;

namespace AiCharacterKit.Core
{
    /// <summary>
    /// Observes a successfully presented dialogue turn without changing its outcome.
    /// </summary>
    public interface INpcTurnObserver
    {
        /// <summary>
        /// Handles one successful turn and honors cancellation independently of dialogue success.
        /// </summary>
        Task ObserveAsync(NpcTurnContext context, CancellationToken cancellationToken);
    }
}
