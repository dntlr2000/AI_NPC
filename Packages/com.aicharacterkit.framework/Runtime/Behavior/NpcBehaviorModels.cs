using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using AiCharacterKit.Core;

namespace AiCharacterKit.Behavior
{
    public enum NpcBehaviorValueKind { Boolean = 0, Number = 1 }
    public enum NpcBehaviorComparison { Equal = 0, NotEqual = 1, Greater = 2, GreaterOrEqual = 3, Less = 4, LessOrEqual = 5 }
    public enum NpcBehaviorConditionKind { Compare = 0, All = 1, Any = 2, Not = 3 }
    public enum NpcBehaviorMutationKind { Set = 0, Add = 1, Subtract = 2 }

    /// <summary>Contains one immutable typed game value; numbers must be finite.</summary>
    public readonly struct NpcBehaviorValue
    {
        public NpcBehaviorValueKind Kind { get; }
        public bool Boolean { get; }
        public double Number { get; }
        /// <summary>Creates a boolean value.</summary>
        public NpcBehaviorValue(bool value) { Kind = NpcBehaviorValueKind.Boolean; Boolean = value; Number = 0; }
        /// <summary>Creates a finite numeric value.</summary>
        public NpcBehaviorValue(double value)
        {
            if (double.IsNaN(value) || double.IsInfinity(value)) throw new ArgumentOutOfRangeException(nameof(value));
            Kind = NpcBehaviorValueKind.Number; Boolean = false; Number = value;
        }
        /// <summary>Formats a selected value without culture-dependent decimal separators.</summary>
        public override string ToString() => Kind == NpcBehaviorValueKind.Boolean ? (Boolean ? "true" : "false") : Number.ToString("R", CultureInfo.InvariantCulture);
    }

    public interface INpcBehaviorValueProvider
    {
        /// <summary>Captures typed consumer facts; called only immediately before a matched turn is evaluated.</summary>
        IReadOnlyDictionary<string, NpcBehaviorValue> CaptureValues();
    }

    [Serializable]
    public sealed class NpcBehaviorVariable
    {
        public string id = "affinity";
        public NpcBehaviorValueKind kind = NpcBehaviorValueKind.Number;
        public bool defaultBoolean;
        public double defaultNumber;
        public double minimum;
        public double maximum = 100;
        public bool exposeToDialogue;
        public string label = "Affinity";
        /// <summary>Copies authored fields independently of the original asset.</summary>
        public NpcBehaviorVariable Copy() => (NpcBehaviorVariable)MemberwiseClone();
    }

    [Serializable]
    public sealed class NpcBehaviorExternalValue
    {
        public string id = "permission";
        public NpcBehaviorValueKind kind;
        /// <summary>Copies a required external value declaration.</summary>
        public NpcBehaviorExternalValue Copy() => (NpcBehaviorExternalValue)MemberwiseClone();
    }

    [Serializable]
    public sealed class NpcBehaviorSignal
    {
        public const int MaxModelExamples = 4, MaxModelExampleUtf8Bytes = 128;
        public string triggerId = "praise";
        public string conditionDescription = "The user compliments this character.";
        public string exampleUserText = "정말 친절하네요.";
        public List<string> positiveExamples = new List<string>();
        public List<string> negativeExamples = new List<string>();
        /// <summary>Compiles optional authored examples into the existing bounded description, retaining the Mock-only example.</summary>
        public NpcTriggerDefinition CreateTrigger() => NpcTriggerDefinition.CreateSignal(triggerId, CompileModelConditionDescription(), exampleUserText);
        /// <summary>Builds the bounded model description and rejects duplicated or contradictory examples without changing authored text.</summary>
        public string CompileModelConditionDescription()
        {
            var text = new StringBuilder(NpcTriggerDefinition.CreateSignal(triggerId, conditionDescription, exampleUserText).ConditionDescription);
            var seen = new HashSet<string>(StringComparer.Ordinal);
            AppendExamples(text, positiveExamples, "Match examples", seen);
            AppendExamples(text, negativeExamples, "Non-match examples", seen);
            var result = text.ToString();
            var bytes = Encoding.UTF8.GetByteCount(result);
            if (bytes > NpcTriggerDefinition.MaxConditionUtf8Bytes)
                throw new ArgumentException("Compiled trigger description uses " + bytes + " UTF-8 bytes; maximum is " + NpcTriggerDefinition.MaxConditionUtf8Bytes + ".");
            return result;
        }
        /// <summary>Rejects repeated examples across both labels after trimming outer whitespace, while preserving the exact transmitted text.</summary>
        private static void AppendExamples(StringBuilder text, List<string> examples, string label, HashSet<string> seen)
        {
            if (examples == null || examples.Count == 0) return;
            if (examples.Count > MaxModelExamples) throw new ArgumentException("Each model example list allows at most four entries.");
            text.Append('\n').Append(label).Append(':');
            foreach (var example in examples)
            {
                if (string.IsNullOrWhiteSpace(example) || Encoding.UTF8.GetByteCount(example) > MaxModelExampleUtf8Bytes)
                    throw new ArgumentException("Model examples must be nonempty and at most 128 UTF-8 bytes each.");
                foreach (var character in example)
                    if (char.IsControl(character) || character == '\u2028' || character == '\u2029')
                        throw new ArgumentException("Model examples must each occupy one line without control characters.");
                if (!seen.Add(example.Trim()))
                    throw new ArgumentException("Model examples must be unique after trimming, including across match and non-match lists.");
                text.Append("\n- ").Append(example);
            }
        }
        /// <summary>Copies both optional example lists, treating absent legacy lists as empty.</summary>
        public NpcBehaviorSignal Copy() => new NpcBehaviorSignal { triggerId = triggerId, conditionDescription = conditionDescription,
            exampleUserText = exampleUserText, positiveExamples = new List<string>(positiveExamples ?? new List<string>()),
            negativeExamples = new List<string>(negativeExamples ?? new List<string>()) };
    }

    /// <summary>Uses parent indices to serialize a bounded condition tree without Unity recursive-serialization limits.</summary>
    [Serializable]
    public sealed class NpcBehaviorConditionNode
    {
        public int parent = -1;
        public NpcBehaviorConditionKind kind;
        public string valueId = "affinity";
        public NpcBehaviorComparison comparison = NpcBehaviorComparison.GreaterOrEqual;
        public bool boolean;
        public double number = 60;
        /// <summary>Copies a single condition node.</summary>
        public NpcBehaviorConditionNode Copy() => (NpcBehaviorConditionNode)MemberwiseClone();
    }

    [Serializable]
    public sealed class NpcBehaviorCondition
    {
        public List<NpcBehaviorConditionNode> nodes = new List<NpcBehaviorConditionNode>();
        /// <summary>Copies the flat tree, preserving invalid null entries for static validation.</summary>
        public NpcBehaviorCondition Copy()
        {
            var result = new NpcBehaviorCondition();
            result.nodes = nodes == null ? null : nodes.ConvertAll(node => node?.Copy());
            return result;
        }
    }

    [Serializable]
    public sealed class NpcBehaviorMutation
    {
        public string variableId = "affinity";
        public NpcBehaviorMutationKind operation = NpcBehaviorMutationKind.Add;
        public bool boolean;
        public double number = 10;
        /// <summary>Copies one authored constant mutation.</summary>
        public NpcBehaviorMutation Copy() => (NpcBehaviorMutation)MemberwiseClone();
    }

    [Serializable]
    public sealed class NpcBehaviorMutationRule
    {
        public string ruleId = "gain_affinity";
        public string triggerId = "praise";
        public NpcBehaviorCondition condition = new NpcBehaviorCondition();
        public List<NpcBehaviorMutation> changes = new List<NpcBehaviorMutation>();
        /// <summary>Copies the condition and every mutation before runtime ownership.</summary>
        public NpcBehaviorMutationRule Copy() => new NpcBehaviorMutationRule { ruleId = ruleId, triggerId = triggerId,
            condition = condition?.Copy(), changes = changes?.ConvertAll(change => change?.Copy()) };
    }

    [Serializable]
    public sealed class NpcBehaviorActionRule
    {
        public string ruleId = "open_gate_when_friendly";
        public string triggerId = "request_open_gate";
        public string actionId = "open_gate";
        public int priority;
        public double cooldownSeconds;
        public bool once;
        public NpcBehaviorCondition condition = new NpcBehaviorCondition();
        /// <summary>Copies authored execution policy and its condition.</summary>
        public NpcBehaviorActionRule Copy() => new NpcBehaviorActionRule { ruleId = ruleId, triggerId = triggerId,
            actionId = actionId, priority = priority, cooldownSeconds = cooldownSeconds, once = once, condition = condition?.Copy() };
    }

    [Serializable]
    public sealed class NpcBehaviorDefinition
    {
        public const int MaxVariables = 32, MaxExternalValues = 32, MaxRules = 64, MaxConditionNodes = 32, MaxConditionDepth = 4, MaxMutations = 16;
        public List<NpcBehaviorVariable> variables = new List<NpcBehaviorVariable>();
        public List<NpcBehaviorExternalValue> externalValues = new List<NpcBehaviorExternalValue>();
        public List<NpcBehaviorSignal> signals = new List<NpcBehaviorSignal>();
        public List<NpcBehaviorMutationRule> mutationRules = new List<NpcBehaviorMutationRule>();
        public List<NpcBehaviorActionRule> actionRules = new List<NpcBehaviorActionRule>();
        /// <summary>Copies all authoring collections; validation is explicit and never calls consumers.</summary>
        public NpcBehaviorDefinition Copy() => new NpcBehaviorDefinition {
            variables = variables?.ConvertAll(value => value?.Copy()), externalValues = externalValues?.ConvertAll(value => value?.Copy()),
            signals = signals?.ConvertAll(value => value?.Copy()), mutationRules = mutationRules?.ConvertAll(value => value?.Copy()),
            actionRules = actionRules?.ConvertAll(value => value?.Copy()) };
    }

    /// <summary>Records only typed deltas, identifiers and fixed diagnostic codes, never dialogue or provider text.</summary>
    public sealed class NpcBehaviorTrace
    {
        public IReadOnlyList<string> TriggerIds { get; }
        public IReadOnlyList<NpcBehaviorDelta> Changes { get; }
        public IReadOnlyList<NpcBehaviorRuleTrace> Rules { get; }
        public NpcActionResult ActionResult { get; }
        public string Code { get; }
        /// <summary>Creates an immutable detached per-turn trace.</summary>
        public NpcBehaviorTrace(IEnumerable<string> ids, IEnumerable<NpcBehaviorDelta> changes,
            IEnumerable<NpcBehaviorRuleTrace> rules, NpcActionResult result, string code)
        {
            TriggerIds = new List<string>(ids).AsReadOnly(); Changes = new List<NpcBehaviorDelta>(changes).AsReadOnly();
            Rules = new List<NpcBehaviorRuleTrace>(rules).AsReadOnly(); ActionResult = result; Code = code;
        }
    }

    public sealed class NpcBehaviorDelta
    {
        public string RuleId { get; }
        public string VariableId { get; }
        public NpcBehaviorValue Before { get; }
        public NpcBehaviorValue After { get; }
        /// <summary>Stores one applied constant operation for inspection.</summary>
        public NpcBehaviorDelta(string ruleId, string variableId, NpcBehaviorValue before, NpcBehaviorValue after)
        { RuleId = ruleId; VariableId = variableId; Before = before; After = after; }
    }

    public sealed class NpcBehaviorRuleTrace
    {
        public string RuleId { get; }
        public bool ConditionPassed { get; }
        public string Reason { get; }
        /// <summary>Stores an evaluation result using a fixed reason token.</summary>
        public NpcBehaviorRuleTrace(string ruleId, bool conditionPassed, string reason)
        { RuleId = ruleId; ConditionPassed = conditionPassed; Reason = reason; }
    }
}
