using System.Collections.Generic;

namespace AiCharacterKit.Core
{
    /// <summary>
    /// Supplies a read-only snapshot of facts that one NPC may use for the current turn.
    /// </summary>
    public interface INpcContextProvider
    {
        /// <summary>
        /// Captures current facts without mutating the consumer game state.
        /// </summary>
        IReadOnlyList<NpcContextFact> CaptureFacts();
    }
}
