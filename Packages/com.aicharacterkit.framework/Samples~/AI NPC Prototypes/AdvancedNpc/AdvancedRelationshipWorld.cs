using AiCharacterKit.Unity.LocalInference;
using UnityEngine;

namespace AiCharacterKit.Samples.Advanced
{
    /// <summary>Consumer-owned shared gate and permission state; this is not a framework quest or save system.</summary>
    public sealed class AdvancedRelationshipWorld : MonoBehaviour
    {
        public bool GateOpen;
        public bool Alarm;
        public bool Permission = true;
        private AdvancedRelationshipNpc[] npcs;
        private LocalInferenceRuntime runtime;
        /// <summary>Finds the sample's shared runner and independent NPC adapters.</summary>
        private void Awake() { npcs = GetComponentsInChildren<AdvancedRelationshipNpc>(); runtime = GetComponent<LocalInferenceRuntime>(); }
        /// <summary>Shows shared game controls and each NPC's independent dialogue, variables and action results.</summary>
        private void OnGUI()
        {
            GUILayout.BeginArea(new Rect(20, 20, Mathf.Min(1200, Screen.width - 40), Screen.height - 40));
            GUILayout.Label("Advanced Relationship NPC — independent affinity, shared gate");
            if (runtime != null)
            {
                GUILayout.Label("Local model: " + runtime.State + " / " + runtime.LastErrorMessage);
                if (GUILayout.Button("Prepare / retry", GUILayout.Width(200))) _ = runtime.PrepareAsync();
            }
            GateOpen = GUILayout.Toggle(GateOpen, "Gate open");
            Alarm = GUILayout.Toggle(Alarm, "Alarm active");
            Permission = GUILayout.Toggle(Permission, "Consumer permission granted");
            GUILayout.Label("Mock examples: 정말 친절하네요. / 불친절하군요. / 안녕하세요. / 문을 열어 주세요.");
            GUILayout.BeginHorizontal();
            foreach (var npc in npcs) npc.DrawPanel();
            GUILayout.EndHorizontal(); GUILayout.EndArea();
        }
    }
}
