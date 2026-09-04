using System;
using System.Linq;
using AiCharacterKit.Unity;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace AiCharacterKit.Core.Tests
{
    /// <summary>
    /// Verifies the imported V4 sample keeps authored canon and live context wiring.
    /// </summary>
    public sealed class GroundedSceneConfigurationTests
    {
        private static string CharacterPath => AiCharacterKitTestPaths.ResolveSample(
            "GroundedNpc/Profiles/GroundedGuard.asset");

        private static string LorePath => AiCharacterKitTestPaths.ResolveSample(
            "GroundedNpc/Profiles/DawnfallLore.asset");

        private static string ScenePath => AiCharacterKitTestPaths.ResolveSample(
            "GroundedNpc/Scenes/GroundedNpcPrototype.unity");

        /// <summary>
        /// Reloads the sample and checks its V4 endpoints, assets, provider, and UI references.
        /// </summary>
        [Test]
        public void GroundedScene_AfterReload_HasRequiredConfiguration()
        {
            var character = AssetDatabase.LoadAssetAtPath<CharacterProfile>(CharacterPath);
            var lore = AssetDatabase.LoadAssetAtPath<NpcLoreProfile>(LorePath);
            Assert.That(character, Is.Not.Null);
            Assert.That(character.TryValidate(out var error), Is.True, error);
            Assert.That(lore, Is.Not.Null);
            Assert.That(lore.TryValidate(out error), Is.True, error);
            Assert.That(lore.TryCreateFacts(out var loreFacts, out error), Is.True, error);
            Assert.That(loreFacts, Has.Count.EqualTo(3));

            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            Assert.That(scene.IsValid(), Is.True);
            Assert.That(scene.isLoaded, Is.True);

            var npc = GameObject.Find("Grounded Gate Guard");
            Assert.That(npc, Is.Not.Null);
            Assert.That(
                GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(npc),
                Is.Zero);

            var conversation = npc.GetComponent<NpcConversationBehaviour>();
            var presentation = npc.GetComponent<NpcTextPresentationDriver>();
            var coordinator = npc.GetComponent<NpcContextCoordinator>();
            var provider = npc.GetComponents<MonoBehaviour>().SingleOrDefault(
                component => string.Equals(
                    component.GetType().FullName,
                    "AiCharacterKit.Samples.Grounding.SampleGuardContextProvider",
                    StringComparison.Ordinal));
            Assert.That(conversation, Is.Not.Null);
            Assert.That(presentation, Is.Not.Null);
            Assert.That(coordinator, Is.Not.Null);
            Assert.That(provider, Is.Not.Null);
            Assert.That(coordinator.TryValidate(character, out error), Is.True, error);

            var providerState = new SerializedObject(provider);
            Assert.That(
                providerState.FindProperty("conversationBehaviour").objectReferenceValue,
                Is.EqualTo(conversation));
            Assert.That(
                providerState.FindProperty("contextStatusText").objectReferenceValue,
                Is.Not.Null);

            AssertConversationConfiguration(
                conversation,
                character,
                presentation,
                coordinator);
            AssertContextConfiguration(coordinator, lore, provider);

            Assert.That(
                UnityEngine.Object.FindObjectsByType<Toggle>(FindObjectsSortMode.None),
                Has.Length.EqualTo(2));
            Assert.That(
                UnityEngine.Object.FindObjectsByType<InputField>(FindObjectsSortMode.None),
                Has.Length.EqualTo(1));
            Assert.That(
                UnityEngine.Object.FindObjectsByType<Button>(FindObjectsSortMode.None),
                Has.Length.EqualTo(2));
            Assert.That(GameObject.Find("Gate Open"), Is.Not.Null);
            Assert.That(GameObject.Find("Town Alarm"), Is.Not.Null);
        }

        /// <summary>
        /// Checks the conversation uses the generated profile, V4 mode, and loopback endpoints.
        /// </summary>
        private static void AssertConversationConfiguration(
            NpcConversationBehaviour conversation,
            CharacterProfile character,
            NpcTextPresentationDriver presentation,
            NpcContextCoordinator coordinator)
        {
            var serialized = new SerializedObject(conversation);
            Assert.That(
                serialized.FindProperty("characterProfile").objectReferenceValue,
                Is.EqualTo(character));
            Assert.That(
                serialized.FindProperty("presentationDriverSource").objectReferenceValue,
                Is.EqualTo(presentation));
            Assert.That(
                serialized.FindProperty("conversationMode").enumValueIndex,
                Is.EqualTo((int)NpcConversationMode.BackendContext));
            Assert.That(
                serialized.FindProperty("contextCoordinator").objectReferenceValue,
                Is.EqualTo(coordinator));
            Assert.That(
                serialized.FindProperty("contextBackendEndpoint").stringValue,
                Is.EqualTo("http://127.0.0.1:8787/v4/npc/respond"));
            Assert.That(
                serialized.FindProperty("contextResetEndpoint").stringValue,
                Is.EqualTo("http://127.0.0.1:8787/v4/npc/sessions/reset"));
            Assert.That(
                serialized.FindProperty("backendTimeoutSeconds").intValue,
                Is.EqualTo(35));
        }

        /// <summary>
        /// Checks the coordinator retains its lore asset and consumer provider reference.
        /// </summary>
        private static void AssertContextConfiguration(
            NpcContextCoordinator coordinator,
            NpcLoreProfile lore,
            MonoBehaviour provider)
        {
            var serialized = new SerializedObject(coordinator);
            var loreProfiles = serialized.FindProperty("loreProfiles");
            var providers = serialized.FindProperty("contextProviderSources");
            Assert.That(loreProfiles.arraySize, Is.EqualTo(1));
            Assert.That(
                loreProfiles.GetArrayElementAtIndex(0).objectReferenceValue,
                Is.EqualTo(lore));
            Assert.That(providers.arraySize, Is.EqualTo(1));
            Assert.That(
                providers.GetArrayElementAtIndex(0).objectReferenceValue,
                Is.EqualTo(provider));
        }
    }
}
