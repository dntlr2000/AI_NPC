using System.Threading;
using System.Threading.Tasks;
using AiCharacterKit.Core;
using AiCharacterKit.Unity.Actions;
using UnityEngine;
using UnityEngine.UI;

namespace AiCharacterKit.Samples.Actions
{
    /// <summary>
    /// Demonstrates a consumer game-state gate that can reject an otherwise matched trigger.
    /// </summary>
    public sealed class SampleGuardedActionHandler : NpcActionHandlerBase
    {
        [SerializeField]
        private bool gateUnlocked;

        [SerializeField]
        private GameObject gateIndicator;

        [SerializeField]
        private Text actionStatusText;

        public override string ActionId => "open_gate";

        /// <summary>
        /// Requires both the normal enabled state and the sample gate permission.
        /// </summary>
        public override bool CanExecute(
            NpcActionContext context,
            out string rejectionReason)
        {
            if (!base.CanExecute(context, out rejectionReason))
            {
                return false;
            }

            if (gateUnlocked)
            {
                return true;
            }

            rejectionReason = "The sample gate is locked in the Inspector.";
            if (actionStatusText != null)
            {
                actionStatusText.text = "Action: open_gate rejected (locked)";
            }

            return false;
        }

        /// <summary>
        /// Hides the sample gate indicator after consumer authorization succeeds.
        /// </summary>
        public override Task ExecuteAsync(
            NpcActionContext context,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (gateIndicator != null)
            {
                gateIndicator.SetActive(false);
            }

            if (actionStatusText != null)
            {
                actionStatusText.text = "Action: open_gate executed";
            }

            return Task.CompletedTask;
        }
    }
}
