using NUnit.Framework;

namespace AiCharacterKit.Core.Tests
{
    /// <summary>
    /// Verifies that the pure request and response models preserve structured values.
    /// </summary>
    public sealed class AiNpcModelsTests
    {
        /// <summary>
        /// Confirms that the request constructor keeps a complete profile snapshot and user text.
        /// </summary>
        [Test]
        public void Constructor_AllRequestValues_PreservesValues()
        {
            var request = new AiNpcRequest(
                "npc-01",
                "Mina",
                "Friendly",
                "Polite",
                "Hello there.",
                NpcEmotion.Happy,
                "Hi");

            Assert.That(request.CharacterId, Is.EqualTo("npc-01"));
            Assert.That(request.DisplayName, Is.EqualTo("Mina"));
            Assert.That(request.Personality, Is.EqualTo("Friendly"));
            Assert.That(request.SpeechStyle, Is.EqualTo("Polite"));
            Assert.That(request.ExampleDialogue, Is.EqualTo("Hello there."));
            Assert.That(request.DefaultEmotion, Is.EqualTo(NpcEmotion.Happy));
            Assert.That(request.UserText, Is.EqualTo("Hi"));
        }

        /// <summary>
        /// Confirms that the response constructor keeps every presentation command.
        /// </summary>
        [Test]
        public void Constructor_AllResponseValues_PreservesValues()
        {
            var response = new AiNpcResponse(
                "Welcome.",
                NpcEmotion.Happy,
                NpcGesture.Wave);

            Assert.That(response.Dialogue, Is.EqualTo("Welcome."));
            Assert.That(response.Emotion, Is.EqualTo(NpcEmotion.Happy));
            Assert.That(response.Gesture, Is.EqualTo(NpcGesture.Wave));
        }
    }
}
