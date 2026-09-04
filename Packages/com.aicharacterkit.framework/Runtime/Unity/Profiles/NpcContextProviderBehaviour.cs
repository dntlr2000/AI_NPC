using AiCharacterKit.Core;
using UnityEngine;

namespace AiCharacterKit.Unity
{
    /// <summary>
    /// Gives consumer MonoBehaviours a discoverable base for read-only NPC context providers.
    /// </summary>
    public abstract class NpcContextProviderBehaviour : MonoBehaviour, INpcContextProvider
    {
        /// <summary>
        /// Captures current consumer facts without changing the game state.
        /// </summary>
        public abstract System.Collections.Generic.IReadOnlyList<NpcContextFact> CaptureFacts();
    }
}
