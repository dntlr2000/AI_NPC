using AiCharacterKit.Unity.Actions;
using UnityEditor;
using UnityEngine;

namespace AiCharacterKit.Editor
{
    /// <summary>Exposes opt-in live state and safe evaluation diagnostics without invoking providers.</summary>
    [CustomEditor(typeof(NpcActionCoordinator))]
    internal sealed class NpcActionCoordinatorEditor : UnityEditor.Editor
    {
        private int selectedTrace;
        /// <summary>Draws authored references, then read-only instance state and an explicit in-memory trace toggle.</summary>
        public override void OnInspectorGUI()
        {
            var coordinator = (NpcActionCoordinator)target;
            var previousTraceEnabled = serializedObject.FindProperty("enableBehaviorTrace").boolValue;
            DrawDefaultInspector();
            if (!Application.isPlaying || !coordinator.IsAdvanced) return;
            var enabled = serializedObject.FindProperty("enableBehaviorTrace").boolValue;
            if (enabled != previousTraceEnabled) coordinator.SetBehaviorTraceEnabled(enabled);
            EditorGUILayout.Space(); EditorGUILayout.LabelField("Current instance state", EditorStyles.boldLabel);
            foreach (var value in coordinator.BehaviorState) EditorGUILayout.LabelField(value.Key, value.Value.ToString());
            var next = EditorGUILayout.Toggle("Capture live trace", enabled);
            if (next != enabled) coordinator.SetBehaviorTraceEnabled(next);
            var traces = coordinator.BehaviorTraces;
            if (traces.Count == 0) { EditorGUILayout.HelpBox("Trace is opt-in and retains at most 32 turns in memory.", MessageType.Info); return; }
            selectedTrace = Mathf.Clamp(EditorGUILayout.IntSlider("Turn (oldest first)", selectedTrace, 0, traces.Count - 1), 0, traces.Count - 1);
            var trace = traces[selectedTrace];
            EditorGUILayout.LabelField("Matched signals", string.Join(", ", trace.TriggerIds));
            EditorGUILayout.LabelField("Outcome", trace.Code);
            EditorGUILayout.LabelField("Action", trace.ActionResult.ActionId + " / " + trace.ActionResult.Status);
            foreach (var change in trace.Changes) EditorGUILayout.LabelField(change.VariableId, change.Before + " → " + change.After);
            foreach (var rule in trace.Rules) EditorGUILayout.LabelField(rule.RuleId, rule.Reason);
        }
    }
}
