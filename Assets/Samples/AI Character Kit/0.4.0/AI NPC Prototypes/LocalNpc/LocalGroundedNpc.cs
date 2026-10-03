using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using AiCharacterKit.Core;
using AiCharacterKit.Unity;
using AiCharacterKit.Unity.Actions;
using UnityEngine;
using UnityEngine.Serialization;

namespace AiCharacterKit.Samples.Local
{
    /// <summary>Consumer-owned example presentation, live gate state and authorized gate action.</summary>
    public sealed class LocalGroundedNpc : MonoBehaviour, INpcPresentationDriver, INpcContextProvider, INpcActionHandler, INpcActionResponseFormatter
    {
        [FormerlySerializedAs("GateOpen"), SerializeField, HideInInspector] private bool initialGateOpen;
        [FormerlySerializedAs("Alarm"), SerializeField, HideInInspector] private bool initialAlarm;
        [SerializeField] private GateReplies koreanReplies = new GateReplies();
        [SerializeField] private GateReplies englishReplies = new GateReplies
        {
            opened = "I opened the gate.", alreadyOpen = "The gate is already open.",
            alarmBlocked = "The alarm is active, so I have not opened the gate.",
            failed = "I could not complete the gate action."
        };
        private LocalGroundedGate gate;
        public bool GateOpen
        {
            // Resolves the shared gate lazily, retaining old standalone sample data when there is no host.
            get { ResolveGate(); return gate != null ? gate.GateOpen : initialGateOpen; }
            set { ResolveGate(); if (gate != null) gate.GateOpen = value; else initialGateOpen = value; }
        }
        public bool Alarm
        {
            // Uses the same live alarm for both NPCs without sharing their conversations.
            get { ResolveGate(); return gate != null ? gate.Alarm : initialAlarm; }
            set { ResolveGate(); if (gate != null) gate.Alarm = value; else initialAlarm = value; }
        }
        public bool InitialGateOpen => initialGateOpen;
        public bool InitialAlarm => initialAlarm;
        public bool ProviderReady { get; private set; }
        public int CaptureCount { get; private set; }
        public int ExecutedActions { get; private set; }
        public string Dialogue { get; private set; } = string.Empty;
        public string Error { get; private set; } = string.Empty;
        public bool Busy { get; private set; }
        public NpcEmotion Emotion { get; private set; }
        public NpcGesture Gesture { get; private set; }
        public string ActionId => "open_gate";
        private string input = "안녕하세요. 현재 문 상태를 알려 주세요.";

        /// <summary>Authored factual action receipts, editable in the sample Inspector independently of persona data.</summary>
        [Serializable]
        public sealed class GateReplies
        {
            public string opened = "문을 열었습니다.";
            public string alreadyOpen = "문이 이미 열려 있습니다.";
            public string alarmBlocked = "경보가 울리고 있어 문을 열지 않았습니다.";
            public string failed = "문 열기 동작을 완료하지 못했습니다.";
        }

        /// <summary>Finds the scene-owned gate without invoking live providers or creating serialized assets.</summary>
        private void ResolveGate()
        {
            if (gate == null) gate = GetComponentInParent<LocalGroundedGate>();
        }

        /// <summary>Demonstrates a live provider which becomes available after all NPC Awake methods.</summary>
        private void Start() { ProviderReady = true; }

        /// <summary>Captures current shared state and action capability only when a turn is submitted.</summary>
        public IReadOnlyList<NpcContextFact> CaptureFacts()
        {
            CaptureCount++;
            if (!ProviderReady) throw new InvalidOperationException();
            return new[]
            {
                new NpcContextFact("gate_status", NpcContextFactKind.Observation, GateOpen ? "The gate is open." : "The gate is closed.", 100),
                new NpcContextFact("alarm_status", NpcContextFactKind.Observation, Alarm ? "The town alarm is active. Opening the gate is forbidden." : "The town alarm is inactive.", 100),
                new NpcContextFact("gate_capability", NpcContextFactKind.Observation,
                    GateOpen ? "The shared gate is already open; no opening action is needed."
                    : Alarm ? "The guard cannot open the gate while the alarm is active."
                    : "The guard can open the shared gate now when the user requests it. The game executes the action.", 100)
            };
        }

        /// <summary>Checks the shared game state again at execution time, independently of generated dialogue.</summary>
        public bool CanExecute(NpcActionContext context, out string reason)
        {
            reason = GateOpen ? "The gate is already open." : Alarm ? "The alarm prevents opening the gate." : string.Empty;
            return !Alarm && !GateOpen;
        }

        /// <summary>Changes the shared gate or fails explicitly; a skipped effect must never be reported as successful.</summary>
        public Task ExecuteAsync(NpcActionContext context, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!CanExecute(context, out _)) throw new InvalidOperationException("Gate action is no longer available.");
            GateOpen = true;
            ExecutedActions++;
            return Task.CompletedTask;
        }

        /// <summary>Replaces action prose with an authored receipt from the actual result; ordinary dialogue remains generated.</summary>
        public AiNpcResponse FormatResponse(AiNpcRequest request, AiNpcResponse proposedResponse, NpcActionResult result)
        {
            if (result == null || result.Status == NpcActionStatus.NoMatch || result.ActionId != ActionId) return proposedResponse;
            var replies = UsesKorean(request.UserText) ? koreanReplies : englishReplies;
            var dialogue = result.Status == NpcActionStatus.Succeeded && GateOpen ? replies.opened
                : result.Status == NpcActionStatus.Rejected && GateOpen ? replies.alreadyOpen
                : result.Status == NpcActionStatus.Rejected && Alarm ? replies.alarmBlocked : replies.failed;
            return new AiNpcResponse(dialogue, NpcEmotion.Neutral, NpcGesture.None, proposedResponse.MatchedTriggerIds);
        }

        /// <summary>Selects the sample's Korean or English receipt text; this is never used for action authorization.</summary>
        private static bool UsesKorean(string text)
        {
            foreach (var character in text)
                if ((character >= '\uac00' && character <= '\ud7af') || (character >= '\u3130' && character <= '\u318f')) return true;
            return false;
        }

        /// <summary>Retains presentation state in memory without logging conversations.</summary>
        public void SetBusy(bool isBusy) { Busy = isBusy; if (isBusy) Error = string.Empty; }
        /// <summary>Shows the successful dialogue in the sample panel.</summary>
        public void PresentDialogue(string dialogue) { Dialogue = dialogue; Error = string.Empty; }
        /// <summary>Shows the validated emotion in the sample panel.</summary>
        public void PresentEmotion(NpcEmotion emotion) { Emotion = emotion; }
        /// <summary>Shows the validated gesture in the sample panel.</summary>
        public void PresentGesture(NpcGesture gesture) { Gesture = gesture; }
        /// <summary>Shows only the controller's fixed recoverable error.</summary>
        public void PresentError(string message) { Error = message; }
        /// <summary>Shows cancellation without altering the last successful dialogue.</summary>
        public void PresentCancellation() { Error = "대화가 취소되었습니다."; }

        /// <summary>Draws individual dialogue and the last actual action outcome; shared state controls live on the host.</summary>
        public void DrawPanel()
        {
            GUILayout.BeginVertical(GUI.skin.box, GUILayout.MinHeight(260));
            GUILayout.Label(gameObject.name);
            GUILayout.Label("Shared gate: " + (GateOpen ? "open" : "closed") + " / alarm: " + (Alarm ? "active" : "inactive"));
            GUILayout.Label(Dialogue, GUILayout.MinHeight(70));
            GUILayout.Label(Emotion + " / " + Gesture);
            var action = GetComponent<NpcActionCoordinator>()?.LastResult;
            if (action != null) GUILayout.Label("Action: " + action.Status);
            if (Error.Length > 0) GUILayout.Label(Error);
            input = GUILayout.TextField(input, 2000);
            var previous = GUI.enabled;
            GUI.enabled = previous && !Busy;
            var conversation = GetComponent<NpcConversationBehaviour>();
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Send")) _ = conversation.SubmitAsync(input);
            if (GUILayout.Button("Reset memory")) _ = conversation.ResetConversationAsync();
            GUILayout.EndHorizontal();
            GUI.enabled = previous;
            GUILayout.EndVertical();
        }
    }
}
