using System;
using System.Threading;
using System.Threading.Tasks;

namespace AiCharacterKit.Core
{
    /// <summary>
    /// Produces deterministic local responses without networking or external state.
    /// </summary>
    public sealed class MockConversationClient : IAiConversationClient
    {
        private readonly TimeSpan simulatedLatency;

        /// <summary>
        /// Creates a mock with a short visible delay for Play Mode status feedback.
        /// </summary>
        public MockConversationClient()
            : this(TimeSpan.FromMilliseconds(350))
        {
        }

        /// <summary>
        /// Creates a mock with a caller-selected deterministic delay, including zero for tests.
        /// </summary>
        public MockConversationClient(TimeSpan simulatedLatency)
        {
            if (simulatedLatency < TimeSpan.Zero)
            {
                throw new ArgumentOutOfRangeException(nameof(simulatedLatency));
            }

            this.simulatedLatency = simulatedLatency;
        }

        /// <summary>
        /// Maps normalized user text to a stable response for repeatable tests and demos.
        /// </summary>
        public async Task<AiNpcResponse> SendAsync(
            AiNpcRequest request,
            CancellationToken cancellationToken)
        {
            if (request == null)
            {
                throw new ArgumentNullException(nameof(request));
            }

            cancellationToken.ThrowIfCancellationRequested();

            if (simulatedLatency > TimeSpan.Zero)
            {
                await Task.Delay(simulatedLatency, cancellationToken);
            }

            var userText = request.UserText.Trim();
            if (userText.Length == 0)
            {
                throw new ArgumentException("User text must not be empty.", nameof(request));
            }

            var normalizedText = userText.ToLowerInvariant();
            return CreateResponse(request, userText, normalizedText);
        }

        /// <summary>
        /// Selects a response rule using only request data and normalized text.
        /// </summary>
        private static AiNpcResponse CreateResponse(
            AiNpcRequest request,
            string userText,
            string normalizedText)
        {
            var speakerName = GetSpeakerName(request);

            if (ContainsGreeting(normalizedText))
            {
                var introduction = string.IsNullOrWhiteSpace(request.ExampleDialogue)
                    ? $"안녕하세요. 저는 {speakerName}입니다."
                    : $"안녕하세요. 저는 {speakerName}입니다. {request.ExampleDialogue.Trim()}";

                return new AiNpcResponse(introduction, NpcEmotion.Happy, NpcGesture.Wave);
            }

            if (ContainsThanks(normalizedText))
            {
                return new AiNpcResponse(
                    $"{speakerName}: 도움이 되었다니 기뻐요.",
                    NpcEmotion.Happy,
                    NpcGesture.Nod);
            }

            if (ContainsQuestion(userText, normalizedText))
            {
                var answer = string.IsNullOrWhiteSpace(request.ExampleDialogue)
                    ? "차분히 함께 생각해 볼게요."
                    : request.ExampleDialogue.Trim();

                return new AiNpcResponse(
                    $"{speakerName}: {answer}",
                    request.DefaultEmotion,
                    NpcGesture.Nod);
            }

            var fallback = string.IsNullOrWhiteSpace(request.ExampleDialogue)
                ? $"\"{userText}\"라고 말씀하셨군요."
                : request.ExampleDialogue.Trim();

            return new AiNpcResponse(
                $"{speakerName}: {fallback}",
                request.DefaultEmotion,
                NpcGesture.None);
        }

        /// <summary>
        /// Returns a stable display name when a profile omits one.
        /// </summary>
        private static string GetSpeakerName(AiNpcRequest request)
        {
            return string.IsNullOrWhiteSpace(request.DisplayName)
                ? "NPC"
                : request.DisplayName.Trim();
        }

        /// <summary>
        /// Detects the limited Korean and English greetings supported by the prototype.
        /// </summary>
        private static bool ContainsGreeting(string normalizedText)
        {
            return normalizedText.Contains("안녕")
                || normalizedText.Contains("hello")
                || normalizedText == "hi";
        }

        /// <summary>
        /// Detects the limited Korean and English gratitude phrases supported by the prototype.
        /// </summary>
        private static bool ContainsThanks(string normalizedText)
        {
            return normalizedText.Contains("고마")
                || normalizedText.Contains("감사")
                || normalizedText.Contains("thank");
        }

        /// <summary>
        /// Detects simple question markers without introducing language-processing dependencies.
        /// </summary>
        private static bool ContainsQuestion(string userText, string normalizedText)
        {
            return userText.Contains("?")
                || normalizedText.Contains("어떻게")
                || normalizedText.Contains("무엇")
                || normalizedText.Contains("뭐");
        }
    }
}
