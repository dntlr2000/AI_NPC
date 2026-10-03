using System.Threading;
using System.Threading.Tasks;
using AiCharacterKit.Core;
using AiCharacterKit.Unity.Actions;
using UnityEngine;
using UnityEngine.UI;

namespace AiCharacterKit.Samples.Actions
{
    /// <summary>
    /// Immediately rotates a consumer-owned target when the greeting action runs.
    /// </summary>
    public sealed class SampleWaveActionHandler : NpcActionHandlerBase
    {
        [SerializeField]
        private Transform actionTarget;

        [SerializeField]
        private Text actionStatusText;

        public override string ActionId => "wave_to_player";

        /// <summary>
        /// Applies one visible immediate sample effect without any game-system dependency.
        /// </summary>
        public override Task ExecuteAsync(
            NpcActionContext context,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (actionTarget != null)
            {
                actionTarget.localRotation *= Quaternion.Euler(0f, 0f, -15f);
            }

            if (actionStatusText != null)
            {
                actionStatusText.text = "Action: wave_to_player executed";
            }

            return Task.CompletedTask;
        }
    }
}
