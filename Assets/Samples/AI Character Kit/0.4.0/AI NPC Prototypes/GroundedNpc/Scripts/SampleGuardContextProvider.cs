using System.Collections.Generic;
using AiCharacterKit.Core;
using AiCharacterKit.Unity;
using UnityEngine;
using UnityEngine.UI;

namespace AiCharacterKit.Samples.Grounding
{
    /// <summary>
    /// Demonstrates how consumer game state becomes bounded request-time observations.
    /// </summary>
    public sealed class SampleGuardContextProvider : NpcContextProviderBehaviour
    {
        [SerializeField]
        private Toggle gateOpenToggle;

        [SerializeField]
        private Toggle townAlarmToggle;

        [SerializeField]
        private Text contextStatusText;

        [SerializeField]
        private NpcConversationBehaviour conversationBehaviour;

        private string lastCapturedSummary = string.Empty;

        /// <summary>
        /// Captures the latest sample toggles without mutating game state.
        /// </summary>
        public override IReadOnlyList<NpcContextFact> CaptureFacts()
        {
            var gateIsOpen = gateOpenToggle != null && gateOpenToggle.isOn;
            var alarmIsActive = townAlarmToggle != null && townAlarmToggle.isOn;
            lastCapturedSummary =
                "Captured context: gate "
                + (gateIsOpen ? "open" : "closed")
                + ", town alarm "
                + (alarmIsActive ? "active" : "inactive");
            RefreshStatus();

            return new[]
            {
                new NpcContextFact(
                    "gate_status",
                    NpcContextFactKind.Observation,
                    gateIsOpen
                        ? "The western gate is currently open."
                        : "The western gate is currently closed.",
                    100),
                new NpcContextFact(
                    "town_alarm_status",
                    NpcContextFactKind.Observation,
                    alarmIsActive
                        ? "The town alarm is currently active."
                        : "The town alarm is currently inactive.",
                    90)
            };
        }

        /// <summary>
        /// Refreshes revision diagnostics after the conversation finishes assembling a snapshot.
        /// </summary>
        private void LateUpdate()
        {
            RefreshStatus();
        }

        /// <summary>
        /// Displays the last captured values, stable revision, and any deterministic omissions.
        /// </summary>
        private void RefreshStatus()
        {
            if (contextStatusText == null)
            {
                return;
            }

            var message = string.IsNullOrEmpty(lastCapturedSummary)
                ? "No context captured yet."
                : lastCapturedSummary;
            var revision = conversationBehaviour != null
                ? conversationBehaviour.LastContextRevision
                : string.Empty;
            if (!string.IsNullOrEmpty(revision))
            {
                message += "\nRevision: " + revision;
            }

            var omitted = conversationBehaviour != null
                ? conversationBehaviour.LastOmittedContextFactIds
                : System.Array.Empty<string>();
            if (omitted.Count > 0)
            {
                message += "\nOmitted facts: " + string.Join(", ", omitted);
            }

            contextStatusText.text = message;
        }
    }
}
