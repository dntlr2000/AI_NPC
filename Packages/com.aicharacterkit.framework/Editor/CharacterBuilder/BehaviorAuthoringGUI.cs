using System;
using System.Collections.Generic;
using System.Linq;
using AiCharacterKit.Behavior;
using UnityEditor;
using UnityEngine;

namespace AiCharacterKit.Editor
{
    /// <summary>Draws bounded typed rule controls over a detached draft, never executing game code.</summary>
    internal static class BehaviorAuthoringGUI
    {
        /// <summary>Edits variables, explicit external declarations, signals and ordered rules.</summary>
        public static void Draw(NpcBehaviorDefinition draft)
        {
            DrawList("Variables", draft.variables, 32, () => new NpcBehaviorVariable(), variable =>
            {
                variable.id = EditorGUILayout.TextField("ID", variable.id);
                variable.kind = (NpcBehaviorValueKind)EditorGUILayout.EnumPopup("Type", variable.kind);
                if (variable.kind == NpcBehaviorValueKind.Boolean) variable.defaultBoolean = EditorGUILayout.Toggle("Default", variable.defaultBoolean);
                else
                {
                    variable.defaultNumber = EditorGUILayout.DoubleField("Default", variable.defaultNumber);
                    variable.minimum = EditorGUILayout.DoubleField("Minimum", variable.minimum);
                    variable.maximum = EditorGUILayout.DoubleField("Maximum", variable.maximum);
                }
                variable.exposeToDialogue = EditorGUILayout.Toggle("Send as grounding", variable.exposeToDialogue);
                if (variable.exposeToDialogue) variable.label = EditorGUILayout.TextField("Grounding label", variable.label);
            });
            DrawList("External values (read only)", draft.externalValues, 32, () => new NpcBehaviorExternalValue(), value =>
            {
                value.id = EditorGUILayout.TextField("ID", value.id);
                value.kind = (NpcBehaviorValueKind)EditorGUILayout.EnumPopup("Type", value.kind);
            });
            DrawList("Conversation signals", draft.signals, 16, () => new NpcBehaviorSignal(), signal =>
            {
                signal.triggerId = EditorGUILayout.TextField("Trigger ID", signal.triggerId);
                EditorGUILayout.LabelField("Natural-language meaning");
                signal.conditionDescription = EditorGUILayout.TextArea(signal.conditionDescription, GUILayout.MinHeight(38));
                signal.exampleUserText = EditorGUILayout.TextField("Exact Mock example", signal.exampleUserText);
            });
            var triggerIds = draft.signals.Select(signal => signal.triggerId).ToArray();
            DrawList("State change rules (declaration order)", draft.mutationRules, 64 - draft.actionRules.Count, () => new NpcBehaviorMutationRule(), rule =>
            {
                rule.ruleId = EditorGUILayout.TextField("Rule ID", rule.ruleId);
                rule.triggerId = IdPopup("When signal matches", rule.triggerId, triggerIds);
                DrawCondition(rule.condition, draft);
                DrawList("Changes", rule.changes, 16, () => new NpcBehaviorMutation(), change =>
                {
                    change.variableId = IdPopup("Variable", change.variableId, draft.variables.Select(value => value.id).ToArray());
                    var variable = draft.variables.Find(value => value.id == change.variableId);
                    if (variable != null && variable.kind == NpcBehaviorValueKind.Boolean)
                    { change.operation = NpcBehaviorMutationKind.Set; change.boolean = EditorGUILayout.Toggle("Set to", change.boolean); }
                    else
                    { change.operation = (NpcBehaviorMutationKind)EditorGUILayout.EnumPopup("Operation", change.operation); change.number = EditorGUILayout.DoubleField("Constant", change.number); }
                });
            });
            DrawList("Action rules (priority, then declaration order)", draft.actionRules, 64 - draft.mutationRules.Count, () => new NpcBehaviorActionRule(), rule =>
            {
                rule.ruleId = EditorGUILayout.TextField("Rule ID", rule.ruleId);
                rule.triggerId = IdPopup("When signal matches", rule.triggerId, triggerIds);
                rule.actionId = EditorGUILayout.TextField("Consumer action ID", rule.actionId);
                rule.priority = EditorGUILayout.IntField("Priority", rule.priority);
                rule.cooldownSeconds = EditorGUILayout.DoubleField("Cooldown seconds", rule.cooldownSeconds);
                rule.once = EditorGUILayout.Toggle("Once per state lifetime", rule.once);
                DrawCondition(rule.condition, draft);
            });
        }

        /// <summary>Draws an explicit tree with bounded child insertion and subtree removal.</summary>
        private static void DrawCondition(NpcBehaviorCondition condition, NpcBehaviorDefinition draft)
        {
            EditorGUILayout.LabelField("Condition", EditorStyles.miniBoldLabel);
            if (condition.nodes.Count == 0)
            {
                EditorGUILayout.LabelField("Always true for this matched signal.");
                if (GUILayout.Button("Add condition")) condition.nodes.Add(new NpcBehaviorConditionNode());
                return;
            }
            var remove = -1; var addParent = -1;
            var valueIds = draft.variables.Select(value => value.id).Concat(draft.externalValues.Select(value => value.id)).ToArray();
            for (var index = 0; index < condition.nodes.Count; index++)
            {
                var node = condition.nodes[index];
                var depth = 1; var parent = node.parent;
                while (parent >= 0 && parent < index && depth <= 4) { depth++; parent = condition.nodes[parent].parent; }
                EditorGUI.indentLevel += depth - 1;
                EditorGUILayout.BeginVertical("box");
                EditorGUILayout.LabelField("Node " + (index + 1) + (node.parent < 0 ? " (root)" : " — child of " + (node.parent + 1)));
                node.kind = (NpcBehaviorConditionKind)EditorGUILayout.EnumPopup("Test", node.kind);
                if (node.kind == NpcBehaviorConditionKind.Compare)
                {
                    node.valueId = IdPopup("Value", node.valueId, valueIds);
                    var kind = draft.variables.Find(value => value.id == node.valueId)?.kind
                        ?? draft.externalValues.Find(value => value.id == node.valueId)?.kind ?? NpcBehaviorValueKind.Number;
                    if (kind == NpcBehaviorValueKind.Boolean)
                    {
                        node.comparison = (NpcBehaviorComparison)EditorGUILayout.Popup("Comparison", Math.Min(1, (int)node.comparison), new[] { "Equal", "Not equal" });
                        node.boolean = EditorGUILayout.Toggle("Constant", node.boolean);
                    }
                    else
                    { node.comparison = (NpcBehaviorComparison)EditorGUILayout.EnumPopup("Comparison", node.comparison); node.number = EditorGUILayout.DoubleField("Constant", node.number); }
                }
                else using (new EditorGUI.DisabledScope(depth >= 4 || condition.nodes.Count >= 32
                    || node.kind == NpcBehaviorConditionKind.Not && condition.nodes.Exists(child => child.parent == index)))
                    if (GUILayout.Button("Add child")) addParent = index;
                if (GUILayout.Button("Remove subtree")) remove = index;
                EditorGUILayout.EndVertical();
                EditorGUI.indentLevel -= depth - 1;
            }
            if (remove >= 0) RemoveSubtree(condition.nodes, remove);
            else if (addParent >= 0) condition.nodes.Add(new NpcBehaviorConditionNode { parent = addParent });
        }

        /// <summary>Removes descendants and remaps remaining parent indices without changing their meaning.</summary>
        private static void RemoveSubtree(List<NpcBehaviorConditionNode> nodes, int root)
        {
            var removed = new HashSet<int> { root };
            for (var index = root + 1; index < nodes.Count; index++) if (removed.Contains(nodes[index].parent)) removed.Add(index);
            var map = new Dictionary<int, int>(); var remaining = new List<NpcBehaviorConditionNode>();
            for (var index = 0; index < nodes.Count; index++) if (!removed.Contains(index)) { map[index] = remaining.Count; remaining.Add(nodes[index]); }
            foreach (var node in remaining) if (node.parent >= 0) node.parent = map[node.parent];
            nodes.Clear(); nodes.AddRange(remaining);
        }

        /// <summary>Shows valid references while retaining missing IDs until the author explicitly fixes them.</summary>
        private static string IdPopup(string label, string selected, string[] ids)
        {
            var choices = new List<string>(ids.Distinct()); var index = choices.IndexOf(selected);
            if (index < 0) { choices.Insert(0, selected ?? string.Empty); index = 0; }
            return choices[EditorGUILayout.Popup(label, index, choices.ToArray())];
        }

        /// <summary>Edits an ordered bounded collection with explicit move and remove controls.</summary>
        private static void DrawList<T>(string title, List<T> list, int max, Func<T> create, Action<T> draw)
        {
            EditorGUILayout.Space(); EditorGUILayout.LabelField(title, EditorStyles.boldLabel);
            var remove = -1; var move = -1;
            for (var index = 0; index < list.Count; index++)
            {
                EditorGUILayout.BeginVertical("box"); draw(list[index]);
                EditorGUILayout.BeginHorizontal();
                using (new EditorGUI.DisabledScope(index == 0)) if (GUILayout.Button("Move up")) move = index;
                if (GUILayout.Button("Remove")) remove = index;
                EditorGUILayout.EndHorizontal(); EditorGUILayout.EndVertical();
            }
            if (remove >= 0) list.RemoveAt(remove);
            else if (move > 0) { var item = list[move]; list[move] = list[move - 1]; list[move - 1] = item; }
            using (new EditorGUI.DisabledScope(list.Count >= max)) if (GUILayout.Button("Add to " + title)) list.Add(create());
        }
    }
}
