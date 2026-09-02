using System;

namespace AiCharacterKit.Core
{
    /// <summary>
    /// Provides one selected trigger and successful turn to a consumer action handler.
    /// </summary>
    public sealed class NpcActionContext
    {
        public AiNpcRequest Request { get; }

        public AiNpcResponse Response { get; }

        public NpcTriggerDefinition Trigger { get; }

        public string ActionId => Trigger.ActionId;

        /// <summary>
        /// Creates the context for one authorized routing attempt.
        /// </summary>
        public NpcActionContext(
            AiNpcRequest request,
            AiNpcResponse response,
            NpcTriggerDefinition trigger)
        {
            Request = request ?? throw new ArgumentNullException(nameof(request));
            Response = response ?? throw new ArgumentNullException(nameof(response));
            Trigger = trigger ?? throw new ArgumentNullException(nameof(trigger));
        }
    }
}
