namespace AiCharacterKit.Core
{
    /// <summary>
    /// Identifies the terminal outcome of an optional conversation action.
    /// </summary>
    public enum NpcActionStatus
    {
        NoMatch = 0,
        UnknownTriggerRejected = 1,
        HandlerMissing = 2,
        Rejected = 3,
        Succeeded = 4,
        Failed = 5,
        Cancelled = 6,
        Busy = 7
    }

    /// <summary>
    /// Reports an action outcome separately from the successful dialogue response.
    /// </summary>
    public sealed class NpcActionResult
    {
        public NpcActionStatus Status { get; }

        public string TriggerId { get; }

        public string ActionId { get; }

        public string Message { get; }

        public bool Executed => Status == NpcActionStatus.Succeeded;

        /// <summary>
        /// Creates one immutable action routing result.
        /// </summary>
        public NpcActionResult(
            NpcActionStatus status,
            string triggerId,
            string actionId,
            string message)
        {
            Status = status;
            TriggerId = triggerId ?? string.Empty;
            ActionId = actionId ?? string.Empty;
            Message = message ?? string.Empty;
        }
    }
}
