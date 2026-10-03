using AiCharacterKit.Unity.LocalInference;
using UnityEngine;

namespace AiCharacterKit.Samples.Local
{
    /// <summary>Small pipeline-independent UI for the shared local runner and two independent NPCs.</summary>
    public sealed class LocalGroundedNpcPanel : MonoBehaviour
    {
        private LocalInferenceRuntime runtime;
        private LocalGroundedNpc[] npcs;
        private LocalGroundedGate gate;
        /// <summary>Resolves shared state, upgrading an older generated scene in memory without rewriting its assets.</summary>
        private void Awake()
        {
            runtime = GetComponent<LocalInferenceRuntime>();
            npcs = GetComponentsInChildren<LocalGroundedNpc>();
            gate = GetComponent<LocalGroundedGate>();
            if (gate == null)
            {
                gate = gameObject.AddComponent<LocalGroundedGate>();
                if (npcs.Length > 0) { gate.GateOpen = npcs[0].InitialGateOpen; gate.Alarm = npcs[0].InitialAlarm; }
            }
        }
        /// <summary>Displays one shared gate and alarm plus separate conversations; toggles remain usable during generation.</summary>
        private void OnGUI()
        {
            GUILayout.BeginArea(new Rect(20, 20, Mathf.Min(Screen.width - 40, 1100), Screen.height - 40));
            GUILayout.Label("Local Grounded NPC — one model, one gate, independent conversations");
            GUILayout.Label("Model: " + runtime.State + "   Prepare: " + runtime.PreparationSeconds.ToString("F1") + " s");
            if (runtime.LastErrorMessage.Length > 0) GUILayout.Label(runtime.LastErrorMessage);
            if (GUILayout.Button("Prepare / retry", GUILayout.Width(180))) _ = runtime.PrepareAsync();
            gate.GateOpen = GUILayout.Toggle(gate.GateOpen, "Shared gate open");
            gate.Alarm = GUILayout.Toggle(gate.Alarm, "Shared town alarm");
            GUILayout.BeginHorizontal();
            foreach (var npc in npcs) npc.DrawPanel();
            GUILayout.EndHorizontal();
            GUILayout.EndArea();
        }
    }
}
