using System;
using System.Collections.Generic;
using System.Text;
using AiCharacterKit.Core;

namespace AiCharacterKit.Behavior
{
    /// <summary>Validates bounded authored definitions without executing providers, handlers or inference.</summary>
    public static class NpcBehaviorValidator
    {
        /// <summary>Reports the first actionable configuration error without modifying the supplied draft.</summary>
        public static bool TryValidate(NpcBehaviorDefinition definition, out string error)
        {
            try { Validate(definition); error = string.Empty; return true; }
            catch (ArgumentException exception) { error = exception.Message; return false; }
        }

        /// <summary>Rejects invalid collections, references, constants and inconsistent shared action policies.</summary>
        private static void Validate(NpcBehaviorDefinition data)
        {
            Require(data != null, "A behavior definition is required.");
            Require(data.variables != null && data.variables.Count <= NpcBehaviorDefinition.MaxVariables, "Variables must contain 0 to 32 entries.");
            Require(data.externalValues != null && data.externalValues.Count <= NpcBehaviorDefinition.MaxExternalValues, "External values must contain 0 to 32 entries.");
            Require(data.signals != null && data.signals.Count > 0 && data.signals.Count <= NpcTriggerDefinition.MaxTriggerCount, "Advanced requires 1 to 16 signals.");
            Require(data.mutationRules != null && data.actionRules != null && data.mutationRules.Count + data.actionRules.Count <= NpcBehaviorDefinition.MaxRules,
                "Advanced supports at most 64 rules.");
            var values = new Dictionary<string, NpcBehaviorValueKind>(StringComparer.Ordinal);
            var variables = new HashSet<string>(StringComparer.Ordinal);
            foreach (var variable in data.variables)
            {
                Require(variable != null && ValidId(variable.id), "Variable IDs must be unique lower snake_case tokens.");
                Require(!values.ContainsKey(variable.id) && ValidKind(variable.kind), "Duplicate variable or invalid value type.");
                Require(Finite(variable.minimum) && Finite(variable.maximum) && Finite(variable.defaultNumber), "Variable numbers must be finite.");
                Require(variable.minimum <= variable.maximum && (variable.kind != NpcBehaviorValueKind.Number
                    || variable.defaultNumber >= variable.minimum && variable.defaultNumber <= variable.maximum), "Variable defaults must be within their ordered bounds.");
                Require(Encoding.UTF8.GetByteCount(variable.label ?? string.Empty) <= 128, "Variable labels must fit within 128 UTF-8 bytes.");
                values.Add(variable.id, variable.kind); variables.Add(variable.id);
            }
            foreach (var value in data.externalValues)
            {
                Require(value != null && ValidId(value.id) && ValidKind(value.kind), "External values need valid IDs and types.");
                Require(!values.ContainsKey(value.id), "Variable and external IDs must be unique.");
                values.Add(value.id, value.kind);
            }
            var signals = new HashSet<string>(StringComparer.Ordinal);
            foreach (var signal in data.signals)
            {
                Require(signal != null && ValidId(signal.triggerId) && signals.Add(signal.triggerId), "Signal IDs must be unique lower snake_case tokens.");
                signal.CreateTrigger();
            }
            var ruleIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (var rule in data.mutationRules)
            {
                Require(rule != null, "Mutation rules must not be null.");
                ValidateRule(rule.ruleId, rule.triggerId, rule.condition, ruleIds, signals, values);
                Require(rule.changes != null && rule.changes.Count > 0 && rule.changes.Count <= NpcBehaviorDefinition.MaxMutations, "Mutation rules need 1 to 16 changes.");
                foreach (var change in rule.changes)
                {
                    Require(change != null && variables.Contains(change.variableId ?? string.Empty), "A mutation must target a declared variable.");
                    Require(Enum.IsDefined(typeof(NpcBehaviorMutationKind), change.operation) && Finite(change.number), "Mutation operations and numeric constants must be valid.");
                    Require(values[change.variableId] == NpcBehaviorValueKind.Number || change.operation == NpcBehaviorMutationKind.Set, "Boolean variables support Set only.");
                }
            }
            var policies = new Dictionary<string, NpcBehaviorActionRule>(StringComparer.Ordinal);
            foreach (var rule in data.actionRules)
            {
                Require(rule != null, "Action rules must not be null.");
                ValidateRule(rule.ruleId, rule.triggerId, rule.condition, ruleIds, signals, values);
                Require(ValidId(rule.actionId) && Finite(rule.cooldownSeconds) && rule.cooldownSeconds >= 0, "Actions need valid IDs and finite non-negative cooldowns.");
                if (policies.TryGetValue(rule.actionId, out var previous))
                    Require(previous.cooldownSeconds == rule.cooldownSeconds && previous.once == rule.once, "Rules sharing an action must use the same cooldown and once policy.");
                else policies.Add(rule.actionId, rule);
            }
        }

        /// <summary>Checks rule identity, signal references and a flat, bounded condition tree.</summary>
        private static void ValidateRule(string id, string trigger, NpcBehaviorCondition condition, HashSet<string> ids,
            HashSet<string> signals, Dictionary<string, NpcBehaviorValueKind> values)
        {
            Require(ValidId(id) && ids.Add(id), "Rule IDs must be unique lower snake_case tokens.");
            Require(trigger != null && signals.Contains(trigger), "A rule must reference a declared signal.");
            Require(condition != null && condition.nodes != null && condition.nodes.Count <= NpcBehaviorDefinition.MaxConditionNodes, "Conditions must contain at most 32 nodes.");
            var nodes = condition.nodes;
            var depths = new int[nodes.Count];
            var children = new int[nodes.Count];
            for (var index = 0; index < nodes.Count; index++)
            {
                var node = nodes[index];
                Require(node != null && Enum.IsDefined(typeof(NpcBehaviorConditionKind), node.kind), "Condition nodes must have valid kinds.");
                Require(index == 0 ? node.parent == -1 : node.parent >= 0 && node.parent < index, "Condition parents must form one ordered tree.");
                depths[index] = index == 0 ? 1 : depths[node.parent] + 1;
                Require(depths[index] <= NpcBehaviorDefinition.MaxConditionDepth, "Condition trees support at most four levels.");
                if (index > 0) children[node.parent]++;
                if (node.kind != NpcBehaviorConditionKind.Compare) continue;
                Require(node.valueId != null && values.ContainsKey(node.valueId), "Conditions must reference a declared variable or external value.");
                Require(Enum.IsDefined(typeof(NpcBehaviorComparison), node.comparison) && Finite(node.number), "Condition comparisons and constants must be valid.");
                Require(values[node.valueId] == NpcBehaviorValueKind.Number || node.comparison == NpcBehaviorComparison.Equal
                    || node.comparison == NpcBehaviorComparison.NotEqual, "Booleans support Equal and NotEqual only.");
            }
            for (var index = 0; index < nodes.Count; index++)
            {
                var kind = nodes[index].kind;
                Require(kind == NpcBehaviorConditionKind.Compare ? children[index] == 0
                    : kind == NpcBehaviorConditionKind.Not ? children[index] == 1 : children[index] > 0,
                    "Compare has no children, NOT has one, and ALL/ANY need at least one child.");
            }
        }

        /// <summary>Evaluates a validated tree against immutable variables and one captured external snapshot.</summary>
        public static bool Evaluate(NpcBehaviorCondition condition, IReadOnlyDictionary<string, NpcBehaviorValue> variables,
            IReadOnlyDictionary<string, NpcBehaviorValue> external)
        {
            return condition.nodes.Count == 0 || EvaluateNode(condition.nodes, 0, variables, external);
        }

        /// <summary>Evaluates the bounded child tree using exact typed comparisons.</summary>
        private static bool EvaluateNode(List<NpcBehaviorConditionNode> nodes, int index,
            IReadOnlyDictionary<string, NpcBehaviorValue> variables, IReadOnlyDictionary<string, NpcBehaviorValue> external)
        {
            var node = nodes[index];
            if (node.kind == NpcBehaviorConditionKind.Compare)
            {
                if (!variables.TryGetValue(node.valueId, out var value)) value = external[node.valueId];
                if (value.Kind == NpcBehaviorValueKind.Boolean)
                    return node.comparison == NpcBehaviorComparison.Equal ? value.Boolean == node.boolean : value.Boolean != node.boolean;
                switch (node.comparison)
                {
                    case NpcBehaviorComparison.Equal: return value.Number == node.number;
                    case NpcBehaviorComparison.NotEqual: return value.Number != node.number;
                    case NpcBehaviorComparison.Greater: return value.Number > node.number;
                    case NpcBehaviorComparison.GreaterOrEqual: return value.Number >= node.number;
                    case NpcBehaviorComparison.Less: return value.Number < node.number;
                    default: return value.Number <= node.number;
                }
            }
            var result = node.kind == NpcBehaviorConditionKind.All;
            for (var child = index + 1; child < nodes.Count; child++)
            {
                if (nodes[child].parent != index) continue;
                var passed = EvaluateNode(nodes, child, variables, external);
                if (node.kind == NpcBehaviorConditionKind.Not) return !passed;
                if (node.kind == NpcBehaviorConditionKind.All) result &= passed;
                else result |= passed;
            }
            return result;
        }

        /// <summary>Checks canonical identifiers without silently merging whitespace variants.</summary>
        private static bool ValidId(string id) => NpcTriggerDefinition.IsValidIdentifier(id) && id == id.Trim();
        /// <summary>Checks the supported two-value type domain.</summary>
        private static bool ValidKind(NpcBehaviorValueKind kind) => kind == NpcBehaviorValueKind.Boolean || kind == NpcBehaviorValueKind.Number;
        /// <summary>Checks finite numeric constants without allocating a value wrapper.</summary>
        internal static bool Finite(double value) => !double.IsNaN(value) && !double.IsInfinity(value);
        /// <summary>Raises a bounded authoring diagnostic when a required invariant is false.</summary>
        private static void Require(bool valid, string message) { if (!valid) throw new ArgumentException(message); }
    }
}
