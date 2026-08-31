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
    /// Verifies that the generated prototype scene retains all required serialized wiring.
    /// </summary>
    public sealed class PrototypeSceneConfigurationTests
    {
        private static string ProfilePath => AiCharacterKitTestPaths.Resolve(
            "Samples/MockNpc/Profiles/PrototypeCharacter.asset");

        private static string ScenePath => AiCharacterKitTestPaths.Resolve(
            "Samples/MockNpc/Scenes/MockNpcPrototype.unity");

        /// <summary>
        /// Reloads the generated scene and checks its NPC, UI, profile, and active input backend.
        /// </summary>
        [Test]
        public void PrototypeScene_AfterReload_HasRequiredConfiguration()
        {
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            Assert.That(scene.IsValid(), Is.True);
            Assert.That(scene.isLoaded, Is.True);

            var profile = AssetDatabase.LoadAssetAtPath<CharacterProfile>(ProfilePath);
            Assert.That(profile, Is.Not.Null);
            Assert.That(profile.CharacterId, Is.EqualTo("prototype-mina"));
            Assert.That(profile.DisplayName, Is.EqualTo("Mina"));

            var npc = GameObject.Find("Mock NPC - Mina");
            Assert.That(npc, Is.Not.Null);

            var conversation = npc.GetComponent<NpcConversationBehaviour>();
            var presentation = npc.GetComponent<NpcTextPresentationDriver>();
            Assert.That(conversation, Is.Not.Null);
            Assert.That(presentation, Is.Not.Null);

            AssertObjectReference(conversation, "characterProfile", profile);
            AssertObjectReference(
                conversation,
                "presentationDriverSource",
                presentation);
            AssertEnumValue(
                conversation,
                "conversationMode",
                (int)NpcConversationMode.Mock);

            var inputView = Object.FindAnyObjectByType<NpcTextInputView>();
            Assert.That(inputView, Is.Not.Null);
            Assert.That(Object.FindAnyObjectByType<InputField>(), Is.Not.Null);
            Assert.That(Object.FindAnyObjectByType<Button>(), Is.Not.Null);

            var eventSystemObject = GameObject.Find("EventSystem");
            Assert.That(eventSystemObject, Is.Not.Null);
            Assert.That(
                eventSystemObject.GetComponent<BaseInputModule>(),
                Is.Not.Null);
        }

        /// <summary>
        /// Confirms that one private serialized reference points to the expected Unity object.
        /// </summary>
        private static void AssertObjectReference(
            UnityEngine.Object target,
            string propertyName,
            UnityEngine.Object expected)
        {
            var serializedObject = new SerializedObject(target);
            var property = serializedObject.FindProperty(propertyName);

            Assert.That(property, Is.Not.Null, propertyName);
            Assert.That(property.objectReferenceValue, Is.EqualTo(expected));
        }

        /// <summary>
        /// Confirms that one private serialized enum retains the expected numeric value.
        /// </summary>
        private static void AssertEnumValue(
            UnityEngine.Object target,
            string propertyName,
            int expected)
        {
            var serializedObject = new SerializedObject(target);
            var property = serializedObject.FindProperty(propertyName);

            Assert.That(property, Is.Not.Null, propertyName);
            Assert.That(property.enumValueIndex, Is.EqualTo(expected));
        }
    }
}
