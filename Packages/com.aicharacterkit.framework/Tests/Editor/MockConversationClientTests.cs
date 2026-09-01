using System;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;

namespace AiCharacterKit.Core.Tests
{
    /// <summary>
    /// Verifies deterministic mock response rules without Unity or network dependencies.
    /// </summary>
    public sealed class MockConversationClientTests
    {
        /// <summary>
        /// Confirms that identical requests produce identical structured responses.
        /// </summary>
        [Test]
        public async Task SendAsync_SameRequest_ReturnsSameResponse()
        {
            var client = new MockConversationClient(TimeSpan.Zero);
            var request = CreateRequest("무엇을 좋아해?");

            var first = await client.SendAsync(request, CancellationToken.None);
            var second = await client.SendAsync(request, CancellationToken.None);

            Assert.That(second.Dialogue, Is.EqualTo(first.Dialogue));
            Assert.That(second.Emotion, Is.EqualTo(first.Emotion));
            Assert.That(second.Gesture, Is.EqualTo(first.Gesture));
        }

        /// <summary>
        /// Confirms that greeting normalization selects the happy wave response.
        /// </summary>
        [Test]
        public async Task SendAsync_TrimmedMixedCaseGreeting_ReturnsGreetingResponse()
        {
            var client = new MockConversationClient(TimeSpan.Zero);
            var request = CreateRequest("  HeLLo  ");

            var response = await client.SendAsync(request, CancellationToken.None);

            Assert.That(response.Dialogue, Does.Contain("Mina"));
            Assert.That(response.Emotion, Is.EqualTo(NpcEmotion.Happy));
            Assert.That(response.Gesture, Is.EqualTo(NpcGesture.Wave));
        }

        /// <summary>
        /// Confirms that gratitude selects the stable happy nod response.
        /// </summary>
        [Test]
        public async Task SendAsync_Thanks_ReturnsHappyNod()
        {
            var client = new MockConversationClient(TimeSpan.Zero);
            var request = CreateRequest("고마워");

            var response = await client.SendAsync(request, CancellationToken.None);

            Assert.That(response.Dialogue, Is.EqualTo("Mina: 도움이 되었다니 기뻐요."));
            Assert.That(response.Emotion, Is.EqualTo(NpcEmotion.Happy));
            Assert.That(response.Gesture, Is.EqualTo(NpcGesture.Nod));
        }

        /// <summary>
        /// Confirms that profile data differentiates the same question without character ID branches.
        /// </summary>
        [Test]
        public async Task SendAsync_DifferentProfilesSameQuestion_ReturnsProfileSpecificResponses()
        {
            var client = new MockConversationClient(TimeSpan.Zero);
            var lunaRequest = new AiNpcRequest(
                "sample-luna",
                "Luna",
                "Playful, curious, and friendly.",
                "Warm, casual, short sentences.",
                "새로운 모험 이야기를 들려줄래?",
                NpcEmotion.Happy,
                "무엇을 좋아해?");
            var guardRequest = new AiNpcRequest(
                "sample-guard",
                "Guard",
                "Disciplined, vigilant, and duty-bound.",
                "Formal, concise, respectful sentences.",
                "성문 주변에서는 질서를 지켜 주십시오.",
                NpcEmotion.Concerned,
                "무엇을 좋아해?");

            var lunaResponse = await client.SendAsync(
                lunaRequest,
                CancellationToken.None);
            var guardResponse = await client.SendAsync(
                guardRequest,
                CancellationToken.None);

            Assert.That(lunaResponse.Dialogue, Is.Not.EqualTo(guardResponse.Dialogue));
            Assert.That(lunaResponse.Emotion, Is.EqualTo(NpcEmotion.Happy));
            Assert.That(guardResponse.Emotion, Is.EqualTo(NpcEmotion.Concerned));
            Assert.That(lunaResponse.Gesture, Is.EqualTo(NpcGesture.Nod));
            Assert.That(guardResponse.Gesture, Is.EqualTo(NpcGesture.Nod));
        }

        /// <summary>
        /// Confirms that an already cancelled request does not generate a response.
        /// </summary>
        [Test]
        public void SendAsync_CancelledToken_ThrowsOperationCanceledException()
        {
            var client = new MockConversationClient(TimeSpan.Zero);
            var request = CreateRequest("안녕");
            var cancellation = new CancellationTokenSource();
            cancellation.Cancel();

            Assert.CatchAsync<OperationCanceledException>(
                async () => await client.SendAsync(request, cancellation.Token));

            cancellation.Dispose();
        }

        /// <summary>
        /// Creates a stable profile snapshot for mock response tests.
        /// </summary>
        private static AiNpcRequest CreateRequest(string userText)
        {
            return new AiNpcRequest(
                "npc-01",
                "Mina",
                "Friendly",
                "Polite",
                "오늘도 좋은 하루예요.",
                NpcEmotion.Neutral,
                userText);
        }
    }
}
