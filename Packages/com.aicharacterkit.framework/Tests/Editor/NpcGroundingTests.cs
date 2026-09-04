using System;
using System.Collections.Generic;
using AiCharacterKit.Core;
using NUnit.Framework;

namespace AiCharacterKit.Core.Tests
{
    /// <summary>
    /// Verifies immutable grounding facts, deterministic selection, and revision stability.
    /// </summary>
    public sealed class NpcGroundingTests
    {
        /// <summary>
        /// Confirms normalized content and priority ordering produce the cross-runtime revision.
        /// </summary>
        [Test]
        public void Snapshot_KnownContent_ProducesStableRevisionAndOrder()
        {
            var snapshot = CreateKnownSnapshot(new[]
            {
                new NpcContextFact(
                    "city_founder",
                    NpcContextFactKind.Lore,
                    "Dawnfall was founded by Queen Mira.",
                    40),
                new NpcContextFact(
                    "gate_status",
                    NpcContextFactKind.Observation,
                    "The western gate is closed.",
                    90),
                new NpcContextFact(
                    "guard_suspicion",
                    NpcContextFactKind.Belief,
                    "The traveler may be hiding something.",
                    50)
            });

            Assert.That(snapshot.Facts[0].FactId, Is.EqualTo("gate_status"));
            Assert.That(snapshot.Facts[1].FactId, Is.EqualTo("guard_suspicion"));
            Assert.That(snapshot.Revision, Is.EqualTo(
                "ctx-0fbb1fef8071da13b9476369537500347025c3762df5df65449f89b5275022bc"));
        }

        /// <summary>
        /// Confirms the assembler keeps higher-priority facts and reports deterministic omissions.
        /// </summary>
        [Test]
        public void Assembler_OverCountBudget_OmitsLowestPriorityFact()
        {
            var facts = new List<NpcContextFact>();
            for (var index = 0; index < NpcGroundingSnapshot.MaxFactCount + 1; index++)
            {
                facts.Add(new NpcContextFact(
                    "fact_" + index,
                    NpcContextFactKind.Observation,
                    "Fact " + index,
                    index));
            }

            var snapshot = NpcContextAssembler.CreateSnapshot(
                string.Empty,
                string.Empty,
                Array.Empty<string>(),
                Array.Empty<string>(),
                facts,
                out var omitted);

            Assert.That(snapshot.Facts, Has.Count.EqualTo(32));
            Assert.That(snapshot.Facts[0].FactId, Is.EqualTo("fact_32"));
            Assert.That(omitted, Is.EqualTo(new[] { "fact_0" }));
        }

        /// <summary>
        /// Confirms the assembler never slices text and omits whole facts at the byte budget.
        /// </summary>
        [Test]
        public void Assembler_OverByteBudget_OmitsWholeFactsDeterministically()
        {
            var facts = new List<NpcContextFact>();
            for (var index = 0; index < 25; index++)
            {
                facts.Add(new NpcContextFact(
                    "fact_" + index.ToString("D2"),
                    NpcContextFactKind.Lore,
                    new string('x', NpcContextFact.MaxStatementUtf8Bytes),
                    50));
            }

            var snapshot = NpcContextAssembler.CreateSnapshot(
                string.Empty,
                string.Empty,
                Array.Empty<string>(),
                Array.Empty<string>(),
                facts,
                out var omitted);

            Assert.That(snapshot.Facts, Has.Count.EqualTo(24));
            Assert.That(snapshot.Facts[23].FactId, Is.EqualTo("fact_23"));
            Assert.That(omitted, Is.EqualTo(new[] { "fact_24" }));
            Assert.That(snapshot.Facts[0].Statement, Has.Length.EqualTo(512));
        }

        /// <summary>
        /// Confirms invalid IDs, duplicate facts, and out-of-range priorities are rejected.
        /// </summary>
        [Test]
        public void Grounding_InvalidValues_AreRejected()
        {
            Assert.Throws<ArgumentException>(() => new NpcContextFact(
                "Invalid Id",
                NpcContextFactKind.Lore,
                "Fact.",
                1));
            Assert.Throws<ArgumentOutOfRangeException>(() => new NpcContextFact(
                "valid_id",
                NpcContextFactKind.Lore,
                "Fact.",
                101));
            var duplicate = new NpcContextFact(
                "same_id",
                NpcContextFactKind.Lore,
                "Fact.",
                1);
            Assert.Throws<ArgumentException>(() => new NpcGroundingSnapshot(
                string.Empty,
                string.Empty,
                Array.Empty<string>(),
                Array.Empty<string>(),
                new[] { duplicate, duplicate }));
        }

        /// <summary>
        /// Confirms the legacy request constructor retains an explicit empty snapshot.
        /// </summary>
        [Test]
        public void Request_LegacyConstructor_UsesEmptyGrounding()
        {
            var request = new AiNpcRequest(
                "guide",
                "Guide",
                "Helpful",
                "Brief",
                "Hello.",
                NpcEmotion.Neutral,
                "Hi");

            Assert.That(request.Grounding, Is.SameAs(NpcGroundingSnapshot.Empty));
            Assert.That(request.Grounding.IsEmpty, Is.True);
        }

        /// <summary>
        /// Creates the canonical cross-runtime fixture snapshot in any requested fact order.
        /// </summary>
        private static NpcGroundingSnapshot CreateKnownSnapshot(
            IEnumerable<NpcContextFact> facts)
        {
            return new NpcGroundingSnapshot(
                "The western gate protects Dawnfall.",
                "Protect citizens and honor lawful travelers.",
                new[]
                {
                    "Never reveal guard rotations.",
                    "Prefer de-escalation."
                },
                new[] { "Guard: State your business." },
                facts);
        }
    }
}
