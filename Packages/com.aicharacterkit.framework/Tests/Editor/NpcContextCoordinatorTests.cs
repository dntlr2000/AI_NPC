using System;
using System.Collections.Generic;
using AiCharacterKit.Core;
using AiCharacterKit.Unity;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace AiCharacterKit.Core.Tests
{
    /// <summary>
    /// Supplies mutable test observations through the public consumer extension point.
    /// </summary>
    public sealed class TestNpcContextProvider : NpcContextProviderBehaviour
    {
        public IReadOnlyList<NpcContextFact> Facts { get; set; } =
            Array.Empty<NpcContextFact>();

        public bool ThrowOnCapture { get; set; }

        /// <summary>
        /// Returns controlled current facts or simulates one provider failure.
        /// </summary>
        public override IReadOnlyList<NpcContextFact> CaptureFacts()
        {
            if (ThrowOnCapture)
            {
                throw new InvalidOperationException("Private provider failure.");
            }

            return Facts;
        }
    }

    /// <summary>
    /// Verifies Unity context composition without retaining mutable provider data.
    /// </summary>
    public sealed class NpcContextCoordinatorTests
    {
        private readonly List<Object> createdObjects = new List<Object>();

        /// <summary>
        /// Destroys every transient Unity object created by a context test.
        /// </summary>
        [TearDown]
        public void TearDown()
        {
            for (var index = createdObjects.Count - 1; index >= 0; index--)
            {
                if (createdObjects[index] != null)
                {
                    Object.DestroyImmediate(createdObjects[index]);
                }
            }

            createdObjects.Clear();
        }

        /// <summary>
        /// Confirms profile, lore, and current observations enter one detached snapshot.
        /// </summary>
        [Test]
        public void TryCreateSnapshot_ConfiguredSources_CombinesAllGrounding()
        {
            var profile = CreateCharacterProfile();
            var lore = CreateLoreProfile("city_founder", "Dawnfall has stood for centuries.");
            var root = Track(new GameObject("Context NPC"));
            var provider = root.AddComponent<TestNpcContextProvider>();
            provider.Facts = new[]
            {
                new NpcContextFact(
                    "gate_status",
                    NpcContextFactKind.Observation,
                    "The gate is closed.",
                    90)
            };
            var coordinator = root.AddComponent<NpcContextCoordinator>();
            ConfigureCoordinator(coordinator, lore, provider);

            var succeeded = coordinator.TryCreateSnapshot(
                profile,
                out var snapshot,
                out var omitted,
                out var error);

            Assert.That(succeeded, Is.True, error);
            Assert.That(snapshot.Background, Does.Contain("Dawnfall"));
            Assert.That(snapshot.Facts, Has.Count.EqualTo(2));
            Assert.That(snapshot.Facts[0].FactId, Is.EqualTo("gate_status"));
            Assert.That(omitted, Is.Empty);
        }

        /// <summary>
        /// Confirms each capture is current and provider exceptions remain a safe local failure.
        /// </summary>
        [Test]
        public void TryCreateSnapshot_ProviderChangesOrThrows_RefreshesSafely()
        {
            var profile = CreateCharacterProfile();
            var root = Track(new GameObject("Context NPC"));
            var provider = root.AddComponent<TestNpcContextProvider>();
            var coordinator = root.AddComponent<NpcContextCoordinator>();
            ConfigureCoordinator(coordinator, null, provider);
            provider.Facts = new[]
            {
                new NpcContextFact(
                    "gate_status",
                    NpcContextFactKind.Observation,
                    "The gate is closed.",
                    90)
            };
            Assert.That(coordinator.TryCreateSnapshot(
                profile,
                out var first,
                out _,
                out var firstError), Is.True, firstError);

            provider.Facts = new[]
            {
                new NpcContextFact(
                    "gate_status",
                    NpcContextFactKind.Observation,
                    "The gate is open.",
                    90)
            };
            Assert.That(coordinator.TryCreateSnapshot(
                profile,
                out var second,
                out _,
                out var secondError), Is.True, secondError);
            Assert.That(second.Revision, Is.Not.EqualTo(first.Revision));

            provider.ThrowOnCapture = true;
            Assert.That(coordinator.TryCreateSnapshot(
                profile,
                out _,
                out _,
                out var failure), Is.False);
            Assert.That(failure, Does.Not.Contain("Private provider failure"));
        }

        /// <summary>
        /// Creates one transient valid character with authored canon and rules.
        /// </summary>
        private CharacterProfile CreateCharacterProfile()
        {
            var profile = Track(ScriptableObject.CreateInstance<CharacterProfile>());
            var serialized = new SerializedObject(profile);
            serialized.FindProperty("characterId").stringValue = "sample-guard";
            serialized.FindProperty("displayName").stringValue = "Guard";
            serialized.FindProperty("personality").stringValue = "Disciplined.";
            serialized.FindProperty("speechStyle").stringValue = "Formal.";
            serialized.FindProperty("exampleDialogue").stringValue = "State your business.";
            serialized.FindProperty("background").stringValue =
                "Dawnfall's western gate is the character's assigned post.";
            var rules = serialized.FindProperty("behavioralRules");
            rules.arraySize = 1;
            rules.GetArrayElementAtIndex(0).stringValue = "Do not invent permissions.";
            serialized.ApplyModifiedPropertiesWithoutUndo();
            return profile;
        }

        /// <summary>
        /// Creates one transient lore asset containing a single validated fact.
        /// </summary>
        private NpcLoreProfile CreateLoreProfile(string factId, string statement)
        {
            var profile = Track(ScriptableObject.CreateInstance<NpcLoreProfile>());
            var serialized = new SerializedObject(profile);
            var entries = serialized.FindProperty("loreFacts");
            entries.arraySize = 1;
            var entry = entries.GetArrayElementAtIndex(0);
            entry.FindPropertyRelative("factId").stringValue = factId;
            entry.FindPropertyRelative("statement").stringValue = statement;
            entry.FindPropertyRelative("priority").intValue = 50;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            return profile;
        }

        /// <summary>
        /// Assigns optional lore and one provider through the coordinator's serialized boundary.
        /// </summary>
        private static void ConfigureCoordinator(
            NpcContextCoordinator coordinator,
            NpcLoreProfile lore,
            MonoBehaviour provider)
        {
            var serialized = new SerializedObject(coordinator);
            var loreProfiles = serialized.FindProperty("loreProfiles");
            loreProfiles.arraySize = lore == null ? 0 : 1;
            if (lore != null)
            {
                loreProfiles.GetArrayElementAtIndex(0).objectReferenceValue = lore;
            }

            var providers = serialized.FindProperty("contextProviderSources");
            providers.arraySize = 1;
            providers.GetArrayElementAtIndex(0).objectReferenceValue = provider;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        /// <summary>
        /// Tracks one transient Unity object for deterministic cleanup.
        /// </summary>
        private T Track<T>(T value) where T : Object
        {
            createdObjects.Add(value);
            return value;
        }
    }
}
