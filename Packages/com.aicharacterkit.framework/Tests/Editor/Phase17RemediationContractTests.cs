using System;
using System.Linq;
using AiCharacterKit.Core;
using AiCharacterKit.LocalInference;
using AiCharacterKit.Transport.V4;
using AiCharacterKit.Unity.Networking;
using NUnit.Framework;

namespace AiCharacterKit.Core.Tests
{
    /// <summary>Separates deterministic signal mapping guarantees from probabilistic model order sensitivity.</summary>
    public sealed class Phase17RemediationContractTests
    {
        /// <summary>Preserves trusted ID authority at the sixteen-trigger boundary under reordered opaque declarations.</summary>
        [TestCase(false)]
        [TestCase(true)]
        public void FullDecisionVectorMapsOpaqueDeclarationsInTheirActualOrder(bool reverse)
        {
            var triggers = Enumerable.Range(0, 16).Select(index => new AiNpcTriggerDto {
                triggerId = "signal_" + index.ToString("D2") + new string('x', 55),
                conditionDescription = "An authored synthetic condition."
            }).ToArray();
            if (reverse) Array.Reverse(triggers);
            var decisions = triggers.Select(trigger => trigger.triggerId.StartsWith("signal_03", StringComparison.Ordinal)
                || trigger.triggerId.StartsWith("signal_14", StringComparison.Ordinal) ? "match" : "uncertain").ToArray();
            var json = "{\"decisions\":[" + string.Join(",", decisions.Select(value => "\"" + value + "\"")) + "]}";
            var parsed = LocalInferenceJsonCodec.ReadClassificationPayload(json, 16);
            var ids = LocalInferenceIntentValidation.MatchedIds(parsed, triggers);
            Assert.That(ids.OrderBy(id => id), Is.EqualTo(new[] {
                "signal_03" + new string('x', 55), "signal_14" + new string('x', 55)
            }));
            Assert.That(parsed.Count(value => value == "uncertain"), Is.EqualTo(14));
        }

        /// <summary>Rejects target-description expansion beyond the existing byte contract instead of silently truncating author data.</summary>
        [Test]
        public void AuthorTargetDescriptionsRetainTheirExistingUtf8Budget()
        {
            var exact = new string('가', 170) + "ab";
            var accepted = NpcTriggerDefinition.CreateSignal("opaque_target", exact, "Example.");
            Assert.That(accepted.ConditionDescription, Is.EqualTo(exact));
            Assert.Throws<ArgumentException>(() => NpcTriggerDefinition.CreateSignal("opaque_target", exact + "c", "Example."));
        }
    }
}
