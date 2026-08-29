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
    /// Verifies the generated Phase 2 profiles and two-NPC scene wiring.
    /// </summary>
    public sealed class MultiCharacterSceneConfigurationTests
    {
        private const string LunaProfilePath =
            "Assets/AiCharacterKit/Samples/MockNpc/Profiles/Luna.asset";

        private const string GuardProfilePath =
            "Assets/AiCharacterKit/Samples/MockNpc/Profiles/Guard.asset";

        private const string ScenePath =
            "Assets/AiCharacterKit/Samples/MockNpc/Scenes/MultiCharacterMock.unity";

        /// <summary>
        /// Reloads the generated scene and checks both independent NPC compositions.
        /// </summary>
        [Test]
        public void MultiCharacterScene_AfterReload_HasTwoIndependentNpcConfigurations()
        {
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            Assert.That(scene.IsValid(), Is.True);
            Assert.That(scene.isLoaded, Is.True);

            var lunaProfile = AssetDatabase.LoadAssetAtPath<CharacterProfile>(
                LunaProfilePath);
            var guardProfile = AssetDatabase.LoadAssetAtPath<CharacterProfile>(
                GuardProfilePath);

            Assert.That(lunaProfile, Is.Not.Null);
            Assert.That(guardProfile, Is.Not.Null);
            Assert.That(lunaProfile.TryValidate(out var lunaError), Is.True, lunaError);
            Assert.That(guardProfile.TryValidate(out var guardError), Is.True, guardError);
            Assert.That(lunaProfile.CharacterId, Is.EqualTo("sample-luna"));
            Assert.That(guardProfile.CharacterId, Is.EqualTo("sample-guard"));
            Assert.That(lunaProfile.CharacterId, Is.Not.EqualTo(guardProfile.CharacterId));

            var lunaInputView = AssertNpcConfiguration(lunaProfile, "Luna");
            var guardInputView = AssertNpcConfiguration(guardProfile, "Guard");

            Assert.That(lunaInputView, Is.Not.SameAs(guardInputView));
            Assert.That(
                Object.FindObjectsByType<NpcConversationBehaviour>(),
                Has.Length.EqualTo(2));
            Assert.That(
                Object.FindObjectsByType<InputField>(),
                Has.Length.EqualTo(2));
            Assert.That(
                Object.FindObjectsByType<Button>(),
                Has.Length.EqualTo(2));
            Assert.That(
                Object.FindObjectsByType<Canvas>(),
                Has.Length.EqualTo(2));

            var eventSystemObject = GameObject.Find("EventSystem");
            Assert.That(eventSystemObject, Is.Not.Null);
            Assert.That(
                eventSystemObject.GetComponent<InputSystemUIInputModule>(),
                Is.Not.Null);
        }

        /// <summary>
        /// Checks one NPC's profile, presentation, input, output, and visual references.
        /// </summary>
        private static NpcTextInputView AssertNpcConfiguration(
            CharacterProfile profile,
            string suffix)
        {
            var npc = GameObject.Find($"Mock NPC - {suffix}");
            var panel = GameObject.Find($"Conversation Panel - {suffix}");
            var inputObject = GameObject.Find($"Player Input - {suffix}");
            var buttonObject = GameObject.Find($"Send Button - {suffix}");

            Assert.That(npc, Is.Not.Null, suffix);
            Assert.That(panel, Is.Not.Null, suffix);
            Assert.That(inputObject, Is.Not.Null, suffix);
            Assert.That(buttonObject, Is.Not.Null, suffix);

            var conversation = npc.GetComponent<NpcConversationBehaviour>();
            var presentation = npc.GetComponent<NpcTextPresentationDriver>();
            var inputView = panel.GetComponent<NpcTextInputView>();
            var inputField = inputObject.GetComponent<InputField>();
            var sendButton = buttonObject.GetComponent<Button>();

            Assert.That(conversation, Is.Not.Null, suffix);
            Assert.That(presentation, Is.Not.Null, suffix);
            Assert.That(inputView, Is.Not.Null, suffix);
            Assert.That(inputField, Is.Not.Null, suffix);
            Assert.That(sendButton, Is.Not.Null, suffix);

            AssertObjectReference(conversation, "characterProfile", profile);
            AssertObjectReference(
                conversation,
                "presentationDriverSource",
                presentation);
            AssertEnumValue(
                conversation,
                "conversationMode",
                (int)NpcConversationMode.Mock);
            AssertObjectReference(inputView, "inputField", inputField);
            AssertObjectReference(inputView, "sendButton", sendButton);
            AssertObjectReference(
                inputView,
                "conversationBehaviour",
                conversation);
            AssertObjectReference(presentation, "sendButton", sendButton);
            AssertObjectReference(
                presentation,
                "dialogueText",
                FindText($"Dialogue Output - {suffix}"));
            AssertObjectReference(
                presentation,
                "emotionText",
                FindText($"Emotion Output - {suffix}"));
            AssertObjectReference(
                presentation,
                "gestureText",
                FindText($"Gesture Output - {suffix}"));
            AssertObjectReference(
                presentation,
                "statusText",
                FindText($"Request Status - {suffix}"));
            AssertObjectReference(presentation, "emotionRenderer", npc.GetComponent<Renderer>());
            AssertObjectReference(presentation, "gestureTarget", npc.transform);
            return inputView;
        }

        /// <summary>
        /// Finds one required generated text label by its unique object name.
        /// </summary>
        private static Text FindText(string objectName)
        {
            var gameObject = GameObject.Find(objectName);
            Assert.That(gameObject, Is.Not.Null, objectName);

            var text = gameObject.GetComponent<Text>();
            Assert.That(text, Is.Not.Null, objectName);
            return text;
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
