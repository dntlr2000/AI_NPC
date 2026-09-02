using System.Threading;
using System.Threading.Tasks;

namespace AiCharacterKit.Core
{
    /// <summary>
    /// Defines one consumer-owned game action behind a stable opaque action ID.
    /// </summary>
    public interface INpcActionHandler
    {
        string ActionId { get; }

        /// <summary>
        /// Performs the final consumer game-state authorization before execution.
        /// </summary>
        bool CanExecute(NpcActionContext context, out string rejectionReason);

        /// <summary>
        /// Executes the consumer action while honoring the turn cancellation token.
        /// </summary>
        Task ExecuteAsync(NpcActionContext context, CancellationToken cancellationToken);
    }
}
