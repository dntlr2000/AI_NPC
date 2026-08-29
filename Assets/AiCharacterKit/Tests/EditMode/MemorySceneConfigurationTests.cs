using AiCharacterKit.Unity;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace AiCharacterKit.Core.Tests
{
    /// <summary>
    /// Verifies the generated Phase 5 scene has two independent session compositions.
    /// </summary>
    public sealed class MemorySceneConfigurationTests
    {
        private const string ScenePath =
            "Assets/AiCharacterKit/Samples/MemoryNpc/Scenes/MemoryNpcPrototype.unity";
        private const string ProfileRoot =
            "Assets/AiCharacterKit/Samples/MockNpc/Profiles/";

        /// <summary>
        /// Reloads the scene and checks both NPCs, endpoints, reset controls, and input wiring.
        /// </summary>
        [Test]
        public void MemoryScene_AfterReload_HasIndependentSessionConfiguration()
        {
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            Assert.That(scene.IsValid(), Is.True);
            Assert.That(scene.isLoaded, Is.True);

            AssertNpcConfiguration("Luna", "Luna.asset");
            AssertNpcConfiguration("Guard", "Guard.asset");

            Assert.That(
                Object.FindObjectsByType<NpcTextInputView>(FindObjectsSortMode.None),
                Has.Length.EqualTo(2));
            Assert.That(
                Object.FindObjectsByType<NpcSessionControlView>(
                    FindObjectsSortMode.None),
                Has.Length.EqualTo(2));
            Assert.That(
                Object.FindObjectsByType<InputField>(FindObjectsSortMode.None),
                Has.Length.EqualTo(2));
            Assert.That(
                Object.FindObjectsByType<Button>(FindObjectsSortMode.None),
                Has.Length.EqualTo(4));
            Assert.That(
                Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None),
                Has.Length.EqualTo(2));

            var eventSystem = GameObject.Find("EventSystem");
            Assert.That(eventSystem, Is.Not.Null);
            Assert.That(
                eventSystem.GetComponent<InputSystemUIInputModule>(),
                Is.Not.Null);
        }

        /// <summary>
        /// Checks one character profile, session mode, endpoints, and local UI references.
        /// </summary>
        private static void AssertNpcConfiguration(
            string suffix,
            string profileFileName)
        {
            var profile = AssetDatabase.LoadAssetAtPath<CharacterProfile>(
                ProfileRoot + profileFileName);
            Assert.That(profile, Is.Not.Null);

            var npc = GameObject.Find("Memory NPC - " + suffix);
            Assert.That(npc, Is.Not.Null);
            var conversation = npc.GetComponent<NpcConversationBehaviour>();
            var presentation = npc.GetComponent<NpcTextPresentationDriver>();
            Assert.That(conversation, Is.Not.Null);
            Assert.That(presentation, Is.Not.Null);

            var serializedConversation = new SerializedObject(conversation);
            AssertObjectReference(
                serializedConversation,
                "characterProfile",
                profile);
            AssertObjectReference(
                serializedConversation,
                "presentationDriverSource",
                presentation);
            Assert.That(
                serializedConversation.FindProperty("conversationMode").enumValueIndex,
                Is.EqualTo((int)NpcConversationMode.BackendSession));
            Assert.That(
                serializedConversation.FindProperty("sessionBackendEndpoint").stringValue,
                Is.EqualTo("http://127.0.0.1:8787/v2/npc/respond"));
            Assert.That(
                serializedConversation.FindProperty("sessionResetEndpoint").stringValue,
                Is.EqualTo("http://127.0.0.1:8787/v2/npc/sessions/reset"));
            Assert.That(
                serializedConversation.FindProperty("backendTimeoutSeconds").intValue,
                Is.EqualTo(35));

            var resetButton = GameObject.Find("Reset Button - " + suffix)
                .GetComponent<Button>();
            var memoryStatus = GameObject.Find("Memory Status - " + suffix)
                .GetComponent<Text>();
            var panel = GameObject.Find("Conversation Panel - " + suffix);
            var sessionView = panel.GetComponent<NpcSessionControlView>();
            Assert.That(resetButton, Is.Not.Null);
            Assert.That(memoryStatus, Is.Not.Null);
            Assert.That(sessionView, Is.Not.Null);

            var serializedPresentation = new SerializedObject(presentation);
            AssertObjectReference(
                serializedPresentation,
                "resetButton",
                resetButton);
            var serializedSessionView = new SerializedObject(sessionView);
            AssertObjectReference(
                serializedSessionView,
                "resetButton",
                resetButton);
            AssertObjectReference(
                serializedSessionView,
                "memoryStatusText",
                memoryStatus);
            AssertObjectReference(
                serializedSessionView,
                "conversationBehaviour",
                conversation);
        }

        /// <summary>
        /// Confirms one serialized reference points to the expected Unity object.
        /// </summary>
        private static void AssertObjectReference(
            SerializedObject serializedObject,
            string propertyName,
            UnityEngine.Object expected)
        {
            var property = serializedObject.FindProperty(propertyName);
            Assert.That(property, Is.Not.Null, propertyName);
            Assert.That(property.objectReferenceValue, Is.EqualTo(expected));
        }
    }
}
