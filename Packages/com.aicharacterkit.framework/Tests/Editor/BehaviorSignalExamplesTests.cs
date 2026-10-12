using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using AiCharacterKit.Behavior;
using AiCharacterKit.LocalInference;
using AiCharacterKit.Unity.Networking;
using NUnit.Framework;

namespace AiCharacterKit.Core.Tests
{
    /// <summary>Checks model-example bounds, legacy semantics and shared local protocol behavior.</summary>
    public sealed class BehaviorSignalExamplesTests
    {
        /// <summary>Preserves absent legacy fields and keeps the exact Mock matcher independent of model examples.</summary>
        [Test] public void LegacyAndMockExamples_AreNotSilentlyConverted()
        {
            var signal = new NpcBehaviorSignal { positiveExamples = null, negativeExamples = null };
            Assert.That(signal.CreateTrigger().ConditionDescription, Is.EqualTo(signal.conditionDescription));
            Assert.That(signal.Copy().positiveExamples, Is.Empty);
            signal.positiveExamples = new List<string> { "model-only example" };
            Assert.That(NpcTriggerMatcher.MatchExampleTriggerIds("model-only example", new[] { signal.CreateTrigger() }), Is.Empty);
            Assert.That(NpcTriggerMatcher.MatchExampleTriggerIds(signal.exampleUserText, new[] { signal.CreateTrigger() }), Is.EqualTo(new[] { "praise" }));
            Assert.That(signal.CreateTrigger().ConditionDescription, Does.Not.Contain(signal.exampleUserText));
        }

        /// <summary>Freezes authored list contents before runtime ownership and maps the same description through V3 and V4.</summary>
        [Test] public void CopiedExamples_ReachBothContractsWithoutNewWireFields()
        {
            var signal = new NpcBehaviorSignal { positiveExamples = new List<string> { "한글 \"quote\" \\ path" }, negativeExamples = new List<string> { "not a compliment" } };
            var copy = signal.Copy(); signal.positiveExamples[0] = "changed"; signal.negativeExamples.Clear();
            var trigger = copy.CreateTrigger();
            Assert.That(trigger.ConditionDescription, Does.Contain("한글 \"quote\" \\ path"));
            Assert.That(trigger.ConditionDescription, Does.Contain("Non-match examples:\n- not a compliment"));
            var request = NpcBehaviorRuntimeTests.Turn().Request;
            var v3 = Transport.V3.AiNpcContractMapper.CreateRequest(request, "req", "session", new[] { trigger });
            var v4 = Transport.V4.AiNpcContractMapper.CreateRequest(request, "req", "session", new[] { trigger });
            Assert.That(v3.triggers.Single().conditionDescription, Is.EqualTo(trigger.ConditionDescription));
            Assert.That(v4.triggers.Single().conditionDescription, Is.EqualTo(trigger.ConditionDescription));
            Assert.That(typeof(Transport.V4.AiNpcTriggerDto).GetFields().Select(field => field.Name), Is.EquivalentTo(new[] { "triggerId", "conditionDescription" }));
        }

        /// <summary>Rejects malformed authored examples instead of clipping text or ignoring extra entries.</summary>
        [TestCase("count")][TestCase("bytes")][TestCase("newline")][TestCase("unicode_line")][TestCase("null")][TestCase("empty")][TestCase("missing_condition")]
        public void InvalidExamples_FailStaticDefinitionValidation(string fault)
        {
            var definition = NpcBehaviorRuntimeTests.Definition(false); var signal = definition.signals[0];
            signal.positiveExamples.Add("ok");
            switch (fault)
            {
                case "count": signal.negativeExamples = Enumerable.Repeat("x", 5).ToList(); break;
                case "bytes": signal.positiveExamples[0] = new string('한', 43); break;
                case "newline": signal.positiveExamples[0] = "a\nb"; break;
                case "unicode_line": signal.positiveExamples[0] = "a\u2028b"; break;
                case "null": signal.positiveExamples[0] = null; break;
                case "empty": signal.positiveExamples[0] = " "; break;
                case "missing_condition": signal.conditionDescription = ""; break;
            }
            Assert.That(NpcBehaviorValidator.TryValidate(definition, out _), Is.False);
        }

        /// <summary>Rejects contradictory or repeated examples before a profile can reach any model, without echoing example text.</summary>
        [TestCase("positive")][TestCase("negative")][TestCase("opposite")][TestCase("trimmed")]
        public void RepeatedExamples_FailStaticValidationWithoutMutatingAuthoring(string fault)
        {
            var definition = NpcBehaviorRuntimeTests.Definition(false);
            var signal = definition.signals[0];
            const string example = "고유한 검증용 문장";
            signal.positiveExamples.Add(example);
            if (fault == "positive") signal.positiveExamples.Add(example);
            else if (fault == "negative") signal.negativeExamples.AddRange(new[] { "other", "other" });
            else signal.negativeExamples.Add(fault == "trimmed" ? "  " + example + "  " : example);
            var positive = signal.positiveExamples.ToArray();
            var negative = signal.negativeExamples.ToArray();
            Assert.That(NpcBehaviorValidator.TryValidate(definition, out var error), Is.False);
            Assert.That(error, Does.Contain("unique after trimming").And.Not.Contain(example));
            Assert.Throws<ArgumentException>(() => signal.CreateTrigger());
            Assert.That(signal.positiveExamples, Is.EqualTo(positive));
            Assert.That(signal.negativeExamples, Is.EqualTo(negative));
        }

        /// <summary>Keeps case, internal spacing and separate trigger meanings distinct, and preserves authored whitespace on the wire.</summary>
        [Test] public void DistinctExamples_AreNotNormalizedOrSharedBetweenSignals()
        {
            var definition = NpcBehaviorRuntimeTests.Definition(false);
            var signal = definition.signals[0];
            signal.positiveExamples.AddRange(new[] { "  Polish  ", "a b" });
            signal.negativeExamples.AddRange(new[] { "polish", "a  b" });
            var other = signal.Copy(); other.triggerId = "other_signal";
            definition.signals.Add(other);
            Assert.That(NpcBehaviorValidator.TryValidate(definition, out _), Is.True);
            Assert.That(signal.CreateTrigger().ConditionDescription, Does.Contain("\n-   Polish  "));
        }

        /// <summary>Accepts the exact 128-byte example and 512-byte compiled boundary but rejects the next byte.</summary>
        [Test] public void CompiledDescription_EnforcesActualUtf8Boundary()
        {
            var signal = new NpcBehaviorSignal { conditionDescription = "x", positiveExamples = new List<string> { new string('한', 42) + "ab" } };
            var overhead = Encoding.UTF8.GetByteCount(signal.CreateTrigger().ConditionDescription) - 1;
            signal.conditionDescription = new string('x', NpcTriggerDefinition.MaxConditionUtf8Bytes - overhead);
            Assert.That(Encoding.UTF8.GetByteCount(signal.CreateTrigger().ConditionDescription), Is.EqualTo(512));
            signal.conditionDescription += "x";
            Assert.Throws<ArgumentException>(() => signal.CreateTrigger());
        }

    }
}
