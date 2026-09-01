using AiCharacterKit.Unity;
using AiCharacterKit.Unity.Speech;
using AiCharacterKit.Unity.Transcription;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace AiCharacterKit.Core.Tests
{
    /// <summary>
    /// Verifies the generated Phase 7 scene composes reviewed STT beside V2 and TTS.
    /// </summary>
    public sealed class VoiceInputSceneConfigurationTests
    {
        private static string ScenePath => AiCharacterKitTestPaths.ResolveSample(
            "VoiceInputNpc/Scenes/VoiceInputNpcPrototype.unity");
        private static string CharacterProfilePath => AiCharacterKitTestPaths.ResolveSample(
            "MockNpc/Profiles/Luna.asset");
        private static string VoiceProfilePath => AiCharacterKitTestPaths.ResolveSample(
            "SpeechNpc/Profiles/WarmFriendlyVoice.asset");

        /// <summary>
        /// Reloads the scene and checks its V2, TTS, microphone, UI, and event wiring.
        /// </summary>
        [Test]
        public void VoiceInputScene_AfterReload_HasReusableReviewedInputComposition()
        {
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            Assert.That(scene.IsValid(), Is.True);
            Assert.That(scene.isLoaded, Is.True);

            var characterProfile = AssetDatabase.LoadAssetAtPath<CharacterProfile>(
                CharacterProfilePath);
            var voiceProfile = AssetDatabase.LoadAssetAtPath<NpcVoiceProfile>(
                VoiceProfilePath);
            Assert.That(characterProfile, Is.Not.Null);
            Assert.That(voiceProfile, Is.Not.Null);

            var npc = GameObject.Find("Voice Input NPC - Luna");
            Assert.That(npc, Is.Not.Null);
            var conversation = npc.GetComponent<NpcConversationBehaviour>();
            var visual = npc.GetComponent<NpcTextPresentationDriver>();
            var decorated = npc.GetComponent<SpeechAugmentedPresentationDriver>();
            var speechOutput = npc.GetComponent<NpcSpeechOutput>();
            var playback = npc.GetComponent<UnityPcmSpeechPlaybackDriver>();
            var capture = npc.GetComponent<UnityMicrophoneCaptureDriver>();
            var voiceInput = npc.GetComponent<NpcVoiceInput>();
            Assert.That(conversation, Is.Not.Null);
            Assert.That(visual, Is.Not.Null);
            Assert.That(decorated, Is.Not.Null);
            Assert.That(speechOutput, Is.Not.Null);
            Assert.That(playback, Is.Not.Null);
            Assert.That(capture, Is.Not.Null);
            Assert.That(voiceInput, Is.Not.Null);

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

            var serializedVoiceInput = new SerializedObject(voiceInput);
            AssertObjectReference(serializedVoiceInput, "captureDriver", capture);
            Assert.That(
                serializedVoiceInput.FindProperty("backendEndpoint").stringValue,
                Is.EqualTo("http://127.0.0.1:8787/v1/speech/transcribe"));
            Assert.That(
                serializedVoiceInput.FindProperty("backendTimeoutSeconds").intValue,
                Is.EqualTo(35));

            var pushButton = GameObject.Find("Push To Talk Button")
                .GetComponent<Button>();
            var cancelButton = GameObject.Find("Cancel Transcription Button")
                .GetComponent<Button>();
            var status = GameObject.Find("Transcription Status")
                .GetComponent<Text>();
            var disclosure = GameObject.Find("Transcription Disclosure")
                .GetComponent<Text>();
            var inputView = GameObject.Find("Conversation Panel")
                .GetComponent<NpcTextInputView>();
            var pushView = pushButton.GetComponent<NpcPushToTalkInputView>();
            Assert.That(pushView, Is.Not.Null);
            Assert.That(disclosure.text, Does.Contain("AI 전사"));

            var serializedPushView = new SerializedObject(pushView);
            AssertObjectReference(serializedPushView, "voiceInput", voiceInput);
            AssertObjectReference(serializedPushView, "textInputView", inputView);
            AssertObjectReference(serializedPushView, "pushToTalkButton", pushButton);
            AssertObjectReference(serializedPushView, "cancelButton", cancelButton);
            AssertObjectReference(
                serializedPushView,
                "transcriptionStatusText",
                status);
            AssertObjectReference(serializedPushView, "disclosureText", disclosure);
            Assert.That(pushView.RecordingStarted.GetPersistentEventCount(), Is.EqualTo(1));
            Assert.That(
                pushView.RecordingStarted.GetPersistentTarget(0),
                Is.EqualTo(speechOutput));
            Assert.That(
                pushView.RecordingStarted.GetPersistentMethodName(0),
                Is.EqualTo(nameof(NpcSpeechOutput.StopSpeech)));

            Assert.That(
                Object.FindObjectsByType<NpcPushToTalkInputView>(),
                Has.Length.EqualTo(1));
            Assert.That(
                Object.FindObjectsByType<NpcSpeechOutput>(),
                Has.Length.EqualTo(1));
            Assert.That(Object.FindObjectsByType<Button>(), Has.Length.EqualTo(5));
            Assert.That(
                Object.FindObjectsByType<Toggle>(),
                Has.Length.EqualTo(1));
            Assert.That(
                GameObject.Find("EventSystem").GetComponent<BaseInputModule>(),
                Is.Not.Null);
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
