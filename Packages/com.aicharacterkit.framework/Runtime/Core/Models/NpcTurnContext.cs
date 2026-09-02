using System;

namespace AiCharacterKit.Core
{
    /// <summary>
    /// Carries the immutable request and response of one successful conversation turn.
    /// </summary>
    public sealed class NpcTurnContext
    {
        public AiNpcRequest Request { get; }

        public AiNpcResponse Response { get; }

        /// <summary>
        /// Creates one complete successful turn snapshot.
        /// </summary>
        public NpcTurnContext(AiNpcRequest request, AiNpcResponse response)
        {
            Request = request ?? throw new ArgumentNullException(nameof(request));
            Response = response ?? throw new ArgumentNullException(nameof(response));
        }
    }
}
