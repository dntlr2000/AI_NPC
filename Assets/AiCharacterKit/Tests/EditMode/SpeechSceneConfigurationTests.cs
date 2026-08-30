using AiCharacterKit.Unity;
using AiCharacterKit.Unity.Speech;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace AiCharacterKit.Core.Tests
{
    /// <summary>
    /// Verifies the generated Phase 6 scene composes speech independently for two NPCs.
    /// </summary>
    public sealed class SpeechSceneConfigurationTests
    {
        private const string ScenePath =
            "Assets/AiCharacterKit/Samples/SpeechNpc/Scenes/SpeechNpcPrototype.unity";
        private const string CharacterProfileRoot =
            "Assets/AiCharacterKit/Samples/MockNpc/Profiles/";
        private const string VoiceProfileRoot =
            "Assets/AiCharacterKit/Samples/SpeechNpc/Profiles/";

        /// <summary>
        /// Reloads the scene and checks independent V2, voice, PCM, and UI composition.
        /// </summary>
        [Test]
        public void SpeechScene_AfterReload_HasReusableTwoNpcComposition()
        {
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            Assert.That(scene.IsValid(), Is.True);
            Assert.That(scene.isLoaded, Is.True);

            AssertNpcConfiguration(
                "Luna",
                "Luna.asset",
                "WarmFriendlyVoice.asset",
                "warm-friendly");
            AssertNpcConfiguration(
                "Guard",
                "Guard.asset",
                "CalmFormalVoice.asset",
                "calm-formal");

            Assert.That(
                Object.FindObjectsByType<SpeechAugmentedPresentationDriver>(),
                Has.Length.EqualTo(2));
            Assert.That(
                Object.FindObjectsByType<NpcSpeechOutput>(),
                Has.Length.EqualTo(2));
            Assert.That(
                Object.FindObjectsByType<UnityPcmSpeechPlaybackDriver>(),
                Has.Length.EqualTo(2));
            Assert.That(
                Object.FindObjectsByType<NpcSpeechControlView>(),
                Has.Length.EqualTo(2));
            Assert.That(
                Object.FindObjectsByType<Toggle>(),
                Has.Length.EqualTo(2));
            Assert.That(
                Object.FindObjectsByType<Button>(),
                Has.Length.EqualTo(6));

            var eventSystem = GameObject.Find("EventSystem");
            Assert.That(eventSystem, Is.Not.Null);
            Assert.That(
                eventSystem.GetComponent<InputSystemUIInputModule>(),
                Is.Not.Null);
        }

        /// <summary>
        /// Checks one character's data assets, decorator, endpoint, PCM, and UI references.
        /// </summary>
        private static void AssertNpcConfiguration(
            string suffix,
            string characterProfileFile,
            string voiceProfileFile,
            string expectedPresetId)
        {
            var characterProfile = AssetDatabase.LoadAssetAtPath<CharacterProfile>(
                CharacterProfileRoot + characterProfileFile);
            var voiceProfile = AssetDatabase.LoadAssetAtPath<NpcVoiceProfile>(
                VoiceProfileRoot + voiceProfileFile);
            Assert.That(characterProfile, Is.Not.Null);
            Assert.That(voiceProfile, Is.Not.Null);
            Assert.That(voiceProfile.VoicePresetId, Is.EqualTo(expectedPresetId));

            var npc = GameObject.Find("Speech NPC - " + suffix);
            Assert.That(npc, Is.Not.Null);
            var conversation = npc.GetComponent<NpcConversationBehaviour>();
            var visual = npc.GetComponent<NpcTextPresentationDriver>();
            var decorated = npc.GetComponent<SpeechAugmentedPresentationDriver>();
            var speechOutput = npc.GetComponent<NpcSpeechOutput>();
            var playback = npc.GetComponent<UnityPcmSpeechPlaybackDriver>();
            var audioSource = npc.GetComponent<AudioSource>();
            Assert.That(conversation, Is.Not.Null);
            Assert.That(visual, Is.Not.Null);
            Assert.That(decorated, Is.Not.Null);
            Assert.That(speechOutput, Is.Not.Null);
            Assert.That(playback, Is.Not.Null);
            Assert.That(audioSource, Is.Not.Null);
            Assert.That(audioSource.playOnAwake, Is.False);
            Assert.That(audioSource.loop, Is.False);
            Assert.That(audioSource.spatialBlend, Is.EqualTo(0f));

            var serializedConversation = new SerializedObject(conversation);
            AssertObjectReference(
                serializedConversation,
                "characterProfile",
                characterProfile);
            AssertObjectReference(
                serializedConversation,
                "presentationDriverSource",
                decorated);
            Assert.That(
                serializedConversation.FindProperty("conversationMode").enumValueIndex,
                Is.EqualTo((int)NpcConversationMode.BackendSession));

            var serializedOutput = new SerializedObject(speechOutput);
            AssertObjectReference(serializedOutput, "voiceProfile", voiceProfile);
            AssertObjectReference(serializedOutput, "playbackDriver", playback);
            Assert.That(
                serializedOutput.FindProperty("backendEndpoint").stringValue,
                Is.EqualTo(
                    "http://127.0.0.1:8787/v1/speech/synthesize"));
            Assert.That(
                serializedOutput.FindProperty("backendTimeoutSeconds").intValue,
                Is.EqualTo(35));
            Assert.That(
                serializedOutput.FindProperty("speechEnabled").boolValue,
                Is.True);

            var serializedDecorator = new SerializedObject(decorated);
            AssertObjectReference(
                serializedDecorator,
                "visualDriverSource",
                visual);
            AssertObjectReference(
                serializedDecorator,
                "speechOutput",
                speechOutput);

            var panel = GameObject.Find("Conversation Panel - " + suffix);
            var view = panel.GetComponent<NpcSpeechControlView>();
            var toggle = GameObject.Find("Speech Toggle - " + suffix)
                .GetComponent<Toggle>();
            var stopButton = GameObject.Find("Stop Speech Button - " + suffix)
                .GetComponent<Button>();
            var status = GameObject.Find("Speech Status - " + suffix)
                .GetComponent<Text>();
            var disclosure = GameObject.Find("Speech Disclosure - " + suffix)
                .GetComponent<Text>();
            Assert.That(view, Is.Not.Null);
            Assert.That(disclosure.text, Does.Contain("AI"));

            var serializedView = new SerializedObject(view);
            AssertObjectReference(serializedView, "speechOutput", speechOutput);
            AssertObjectReference(serializedView, "speechToggle", toggle);
            AssertObjectReference(serializedView, "stopButton", stopButton);
            AssertObjectReference(serializedView, "speechStatusText", status);
            AssertObjectReference(serializedView, "disclosureText", disclosure);
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
