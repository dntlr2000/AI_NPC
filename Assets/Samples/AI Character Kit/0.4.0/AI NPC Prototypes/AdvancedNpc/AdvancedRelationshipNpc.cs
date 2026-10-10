using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using AiCharacterKit.Behavior;
using AiCharacterKit.Core;
using AiCharacterKit.Unity;
using AiCharacterKit.Unity.Actions;
using UnityEngine;

namespace AiCharacterKit.Samples.Advanced
{
    /// <summary>Supplies consumer facts, presentation and a final gate authorization for the Advanced example.</summary>
    public sealed class AdvancedRelationshipNpc : MonoBehaviour, INpcBehaviorValueProvider, INpcContextProvider,
        INpcActionHandler, INpcPresentationDriver, INpcActionResponseFormatter
    {
        public string ActionId => "open_gate";
        public bool ValuesReady = true;
        public int ValueCaptures { get; private set; }
        public int GateExecutions { get; private set; }
        public int Greetings { get; set; }
        public bool Busy { get; private set; }
        public string Dialogue { get; private set; } = string.Empty;
        public string Error { get; private set; } = string.Empty;
        [SerializeField] private string openedReply = "문을 열었습니다.";
        [SerializeField] private string rejectedReply = "지금은 문을 열어 드릴 조건이 충족되지 않았습니다.";
        [SerializeField] private string alreadyOpenReply = "문은 이미 열려 있습니다.";
        [SerializeField] private string failedReply = "현재 상태를 확인하거나 행동을 완료하지 못했습니다.";
        [SerializeField] private string greetedReply = "반갑습니다. 특별한 인사를 드립니다.";
        [SerializeField] private string alreadyGreetedReply = "특별한 인사는 이미 드렸습니다.";
        private string input = "정말 친절하네요.";
        public AdvancedRelationshipWorld World => GetComponentInParent<AdvancedRelationshipWorld>();
        public NpcActionCoordinator Actions => GetComponent<NpcActionCoordinator>();

        /// <summary>Captures authoritative typed values once when the behavior evaluator needs a matched turn.</summary>
        public IReadOnlyDictionary<string, NpcBehaviorValue> CaptureValues()
        {
            ValueCaptures++;
            if (!ValuesReady || World == null) throw new InvalidOperationException("Sample values are not ready.");
            return new Dictionary<string, NpcBehaviorValue> { ["permission"] = new NpcBehaviorValue(World.Permission), ["alarm"] = new NpcBehaviorValue(World.Alarm) };
        }
        /// <summary>Provides fresh world observations separately from typed rule evaluation and conversation memory.</summary>
        public IReadOnlyList<NpcContextFact> CaptureFacts()
        {
            if (World == null) throw new InvalidOperationException("Shared world is missing.");
            return new[] {
                new NpcContextFact("gate_state", NpcContextFactKind.Observation, "The shared gate is currently " + (World.GateOpen ? "open." : "closed."), 100),
                new NpcContextFact("alarm_state", NpcContextFactKind.Observation, "The alarm is currently " + (World.Alarm ? "active." : "inactive."), 100),
                new NpcContextFact("permission_state", NpcContextFactKind.Observation, "Current gate permission is " + (World.Permission ? "granted." : "not granted."), 100) };
        }
        /// <summary>Rechecks current world and affinity immediately before the consumer changes the gate.</summary>
        public bool CanExecute(NpcActionContext context, out string reason)
        {
            reason = "Gate conditions are not satisfied.";
            return isActiveAndEnabled && World != null && !World.GateOpen && !World.Alarm && World.Permission
                && Actions.BehaviorState.TryGetValue("affinity", out var affinity) && affinity.Number >= 60;
        }
        /// <summary>Applies the authorized shared gate effect and counts actual executions.</summary>
        public Task ExecuteAsync(NpcActionContext context, CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            if (!CanExecute(context, out _)) throw new InvalidOperationException("Gate state changed.");
            World.GateOpen = true; GateExecutions++; return Task.CompletedTask;
        }
        /// <summary>Finalizes opted-in local action replies from the real result before presentation and memory commit.</summary>
        public AiNpcResponse FormatResponse(AiNpcRequest request, AiNpcResponse proposed, NpcActionResult result)
        {
            if (result == null || result.Status == NpcActionStatus.NoMatch) return proposed;
            string reply;
            if (result.ActionId == "open_gate") reply = result.Executed && World.GateOpen ? openedReply
                : result.Status == NpcActionStatus.Rejected && World.GateOpen ? alreadyOpenReply
                : result.Status == NpcActionStatus.Rejected ? rejectedReply : failedReply;
            else if (result.ActionId == "special_greeting") reply = result.Executed ? greetedReply : alreadyGreetedReply;
            else reply = failedReply;
            return new AiNpcResponse(reply, NpcEmotion.Neutral, NpcGesture.None, proposed.MatchedTriggerIds);
        }
        /// <summary>Displays request activity without logging dialogue.</summary>
        public void SetBusy(bool value) { Busy = value; if (value) Error = string.Empty; }
        /// <summary>Retains only the currently visible dialogue.</summary>
        public void PresentDialogue(string dialogue) { Dialogue = dialogue; Error = string.Empty; }
        /// <summary>Accepts the structured emotion without coupling game actions to presentation.</summary>
        public void PresentEmotion(NpcEmotion emotion) { }
        /// <summary>Accepts the structured gesture without executing gameplay effects.</summary>
        public void PresentGesture(NpcGesture gesture) { }
        /// <summary>Displays a safe controller diagnostic.</summary>
        public void PresentError(string message) { Error = message; }
        /// <summary>Displays cancellation while preserving completed game state.</summary>
        public void PresentCancellation() { Error = "대화가 취소되었습니다."; }
        /// <summary>Shows one NPC's variables, actual result and independent memory/state reset controls.</summary>
        public void DrawPanel()
        {
            GUILayout.BeginVertical(GUI.skin.box, GUILayout.MinHeight(350)); GUILayout.Label(name);
            foreach (var pair in Actions.BehaviorState) GUILayout.Label(pair.Key + ": " + pair.Value);
            GUILayout.Label(Dialogue, GUILayout.MinHeight(65));
            var result = Actions.LastResult;
            if (result != null) GUILayout.Label("Actual action: " + result.ActionId + " / " + result.Status);
            var trace = Actions.LastBehaviorTrace;
            if (trace != null) foreach (var rule in trace.Rules) GUILayout.Label(rule.RuleId + ": " + rule.Reason);
            if (Error.Length > 0) GUILayout.Label(Error);
            input = GUILayout.TextField(input, 2000);
            var previous = GUI.enabled; GUI.enabled = previous && !Busy;
            var conversation = GetComponent<NpcConversationBehaviour>();
            if (GUILayout.Button("Send")) _ = conversation.SubmitAsync(input);
            GUILayout.BeginHorizontal();
            using (new GUIEnabledScope(conversation.SupportsConversationReset))
                if (GUILayout.Button("Reset memory")) _ = conversation.ResetConversationAsync();
            if (GUILayout.Button("Reset behavior state")) Actions.TryResetBehaviorState(out _);
            GUILayout.EndHorizontal(); GUI.enabled = previous; GUILayout.EndVertical();
        }

        private readonly struct GUIEnabledScope : IDisposable
        {
            private readonly bool previous;
            /// <summary>Temporarily disables an unsupported control without re-enabling a busy panel.</summary>
            public GUIEnabledScope(bool enabled) { previous = GUI.enabled; GUI.enabled = previous && enabled; }
            /// <summary>Restores the surrounding UI enabled state.</summary>
            public void Dispose() { GUI.enabled = previous; }
        }
    }
}
