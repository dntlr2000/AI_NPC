using AiCharacterKit.Unity;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace AiCharacterKit.Core.Tests
{
    /// <summary>
    /// Verifies the generated Phase 4 scene uses the local backend composition only.
    /// </summary>
    public sealed class BackendSceneConfigurationTests
    {
        private static string ProfilePath => AiCharacterKitTestPaths.Resolve(
            "Samples/MockNpc/Profiles/Luna.asset");

        private static string ScenePath => AiCharacterKitTestPaths.Resolve(
            "Samples/BackendNpc/Scenes/BackendNpcPrototype.unity");

        /// <summary>
        /// Reloads the backend scene and checks its mode, endpoint, profile, UI, and input wiring.
        /// </summary>
        [Test]
        public void BackendScene_AfterReload_HasRequiredConfiguration()
        {
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            Assert.That(scene.IsValid(), Is.True);
            Assert.That(scene.isLoaded, Is.True);

            var profile = AssetDatabase.LoadAssetAtPath<CharacterProfile>(ProfilePath);
            Assert.That(profile, Is.Not.Null);
            Assert.That(profile.CharacterId, Is.EqualTo("sample-luna"));

            var npc = GameObject.Find("Backend NPC - Luna");
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
                Is.EqualTo((int)NpcConversationMode.Backend));
            Assert.That(
                serializedConversation.FindProperty("backendEndpoint").stringValue,
                Is.EqualTo("http://127.0.0.1:8787/v1/npc/respond"));
            Assert.That(
                serializedConversation.FindProperty("backendTimeoutSeconds").intValue,
                Is.EqualTo(35));

            var inputView = Object.FindAnyObjectByType<NpcTextInputView>();
            Assert.That(inputView, Is.Not.Null);
            Assert.That(Object.FindObjectsByType<InputField>(), Has.Length.EqualTo(1));
            Assert.That(Object.FindObjectsByType<Button>(), Has.Length.EqualTo(1));
            Assert.That(Object.FindObjectsByType<Canvas>(), Has.Length.EqualTo(1));

            var eventSystemObject = GameObject.Find("EventSystem");
            Assert.That(eventSystemObject, Is.Not.Null);
            Assert.That(
                eventSystemObject.GetComponent<BaseInputModule>(),
                Is.Not.Null);
        }

        /// <summary>
        /// Confirms that one serialized reference points to the expected Unity object.
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
