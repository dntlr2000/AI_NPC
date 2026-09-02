using System.Threading;
using System.Threading.Tasks;
using AiCharacterKit.Core;
using UnityEngine;

namespace AiCharacterKit.Unity.Actions
{
    /// <summary>
    /// Provides an optional Unity base class for consumer-owned NPC game actions.
    /// </summary>
    public abstract class NpcActionHandlerBase : MonoBehaviour, INpcActionHandler
    {
        public abstract string ActionId { get; }

        /// <summary>
        /// Allows enabled handlers by default and can be overridden for game-state checks.
        /// </summary>
        public virtual bool CanExecute(
            NpcActionContext context,
            out string rejectionReason)
        {
            rejectionReason = isActiveAndEnabled
                ? string.Empty
                : "The action handler is not active and enabled.";
            return isActiveAndEnabled;
        }

        /// <summary>
        /// Executes the consumer-specific game effect.
        /// </summary>
        public abstract Task ExecuteAsync(
            NpcActionContext context,
            CancellationToken cancellationToken);
    }
}
