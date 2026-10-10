using System.Threading;
using System.Threading.Tasks;
using AiCharacterKit.Core;
using UnityEngine;

namespace AiCharacterKit.Samples.Advanced
{
    /// <summary>Consumer-owned example effect whose successful execution is once-gated by Advanced rules.</summary>
    public sealed class AdvancedGreetingHandler : MonoBehaviour, INpcActionHandler
    {
        public string ActionId => "special_greeting";
        /// <summary>Checks that the sample's effect receiver is currently enabled.</summary>
        public bool CanExecute(NpcActionContext context, out string reason)
        { reason = "The greeting receiver is unavailable."; return isActiveAndEnabled && GetComponent<AdvancedRelationshipNpc>() != null; }
        /// <summary>Records the actual greeting effect independently of generated emotion or gesture.</summary>
        public Task ExecuteAsync(NpcActionContext context, CancellationToken token)
        { token.ThrowIfCancellationRequested(); GetComponent<AdvancedRelationshipNpc>().Greetings++; return Task.CompletedTask; }
    }
}
