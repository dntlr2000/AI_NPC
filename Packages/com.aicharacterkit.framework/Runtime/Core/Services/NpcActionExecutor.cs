using System;
using System.Threading;
using System.Threading.Tasks;

namespace AiCharacterKit.Core
{
    /// <summary>Shares final consumer authorization and execution between Basic and Advanced routing.</summary>
    public static class NpcActionExecutor
    {
        /// <summary>Executes one selected binding and contains consumer failures without retrying another action.</summary>
        public static async Task<NpcActionResult> ExecuteAsync(NpcActionContext context, INpcActionHandler handler,
            CancellationToken cancellationToken, bool preserveBasicDiagnostics = false)
        {
            if (context == null) throw new ArgumentNullException(nameof(context));
            if (handler == null) return Result(context, NpcActionStatus.HandlerMissing, "No handler is configured for the selected action ID.");
            if (cancellationToken.IsCancellationRequested) return Result(context, NpcActionStatus.Cancelled, "Action was cancelled.");
            try
            {
                if (!handler.CanExecute(context, out var reason))
                    return Result(context, NpcActionStatus.Rejected,
                        preserveBasicDiagnostics && !string.IsNullOrWhiteSpace(reason) ? reason : "The consumer rejected the action in the current game state.");
            }
            catch (Exception exception)
            {
                return Result(context, NpcActionStatus.Failed,
                    preserveBasicDiagnostics ? "Action authorization failed: " + exception.Message : "Action authorization failed.");
            }
            try
            {
                cancellationToken.ThrowIfCancellationRequested();
                await handler.ExecuteAsync(context, cancellationToken);
                cancellationToken.ThrowIfCancellationRequested();
                return Result(context, NpcActionStatus.Succeeded, "Action completed.");
            }
            catch (OperationCanceledException) { return Result(context, NpcActionStatus.Cancelled, "Action was cancelled."); }
            catch (Exception exception)
            {
                return Result(context, NpcActionStatus.Failed,
                    preserveBasicDiagnostics ? "Action failed: " + exception.Message : "Action failed.");
            }
        }

        /// <summary>Creates a result retaining the authored trigger and selected action identifiers.</summary>
        private static NpcActionResult Result(NpcActionContext context, NpcActionStatus status, string message)
        {
            return new NpcActionResult(status, context.Trigger.TriggerId, context.ActionId, message);
        }
    }
}
