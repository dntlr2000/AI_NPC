using AiCharacterKit.Samples.Advanced;
using AiCharacterKit.Unity;
using UnityEngine;

namespace AiCharacterKit.Samples.Inference
{
    /// <summary>Shows opt-in API strategy outcomes without logging dialogue or changing game authorization.</summary>
    public sealed class InferenceComparisonPanel : MonoBehaviour
    {
        private AdvancedRelationshipWorld world;
        private AdvancedRelationshipNpc[] npcs;
        private Vector2 scroll;

        /// <summary>Reuses consumer-owned world/NPC controls while suppressing the original overlapping panel.</summary>
        private void Awake()
        {
            world = GetComponent<AdvancedRelationshipWorld>();
            world.enabled = false;
            npcs = GetComponentsInChildren<AdvancedRelationshipNpc>();
        }

        /// <summary>Displays independent memory/state, current signals, real effects and full submission duration.</summary>
        private void OnGUI()
        {
            GUILayout.BeginArea(new Rect(20, 20, Screen.width - 40, Screen.height - 40));
            scroll = GUILayout.BeginScrollView(scroll);
            GUILayout.Label("API inference comparison — SinglePass / ClassifyThenRespond");
            GUILayout.Label("Same authored rules; separate NPC memory and affinity; shared gate. Restore state before comparing turns.");
            GUILayout.Label("API dialogue is generated before gameplay results. Read Actual action separately.");
            world.GateOpen = GUILayout.Toggle(world.GateOpen, "Gate open");
            world.Permission = GUILayout.Toggle(world.Permission, "Permission granted");
            world.Alarm = GUILayout.Toggle(world.Alarm, "Alarm active");
            GUILayout.BeginHorizontal();
            foreach (var npc in npcs)
            {
                GUILayout.BeginVertical(GUILayout.Width(Mathf.Max(280, (Screen.width - 70) / 2)));
                var conversation = npc.GetComponent<NpcConversationBehaviour>();
                GUILayout.Label(conversation.InferenceStrategy + " / total " + conversation.LastSubmissionSeconds.ToString("F3") + " s");
                var trace = npc.Actions.LastBehaviorTrace;
                if (trace != null) GUILayout.Label("Signals: " + string.Join(", ", trace.TriggerIds));
                npc.DrawPanel();
                GUILayout.EndVertical();
            }
            GUILayout.EndHorizontal();
            GUILayout.EndScrollView();
            GUILayout.EndArea();
        }
    }
}
