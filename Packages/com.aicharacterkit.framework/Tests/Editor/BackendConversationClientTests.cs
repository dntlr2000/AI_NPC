using System;
using System.Threading;
using System.Threading.Tasks;
using AiCharacterKit.Core;
using AiCharacterKit.Transport.V1;
using AiCharacterKit.Unity.Networking;
using NUnit.Framework;

namespace AiCharacterKit.Core.Tests
{
    /// <summary>
    /// Verifies Core-to-backend mapping, correlation, errors, and cancellation without HTTP.
    /// </summary>
    public sealed class BackendConversationClientTests
    {
        /// <summary>
        /// Preserves every request field and maps a correlated success response.
        /// </summary>
        [Test]
        public async Task SendAsync_CorrelatedSuccess_PreservesRequestAndMapsResponse()
        {
            AiNpcRequestEnvelopeDto capturedRequest = null;
            var gateway = new StubGateway(
                (request, _) =>
                {
                    capturedRequest = request;
                    return Task.FromResult(
                        AiNpcContractMapper.CreateSuccessResponse(
                            new AiNpcResponse(
                                "Luna: 좋아!",
                                NpcEmotion.Happy,
                                NpcGesture.Wave),
                            request.requestId));
                });
            var client = new BackendConversationClient(gateway);

            var response = await client.SendAsync(
                CreateRequest("무엇을 좋아해?"),
                CancellationToken.None);

            Assert.That(capturedRequest, Is.Not.Null);
            Assert.That(capturedRequest.requestId, Does.StartWith("req-"));
            Assert.That(capturedRequest.character.characterId, Is.EqualTo("sample-luna"));
            Assert.That(capturedRequest.character.displayName, Is.EqualTo("Luna"));
            Assert.That(capturedRequest.character.personality, Is.EqualTo("Playful"));
            Assert.That(capturedRequest.character.speechStyle, Is.EqualTo("Warm"));
            Assert.That(capturedRequest.character.exampleDialogue, Is.EqualTo("안녕!"));
            Assert.That(capturedRequest.character.defaultEmotion, Is.EqualTo("happy"));
            Assert.That(capturedRequest.userText, Is.EqualTo("무엇을 좋아해?"));
            Assert.That(response.Dialogue, Is.EqualTo("Luna: 좋아!"));
            Assert.That(response.Emotion, Is.EqualTo(NpcEmotion.Happy));
            Assert.That(response.Gesture, Is.EqualTo(NpcGesture.Wave));
        }

        /// <summary>
        /// Generates a fresh opaque request ID for every independent submission.
        /// </summary>
        [Test]
        public async Task SendAsync_TwoRequests_UsesDistinctRequestIds()
        {
            var firstId = string.Empty;
            var secondId = string.Empty;
            var callCount = 0;
            var gateway = new StubGateway(
                (request, _) =>
                {
                    callCount++;
                    if (callCount == 1)
                    {
                        firstId = request.requestId;
                    }
                    else
                    {
                        secondId = request.requestId;
                    }

                    return Task.FromResult(
                        AiNpcContractMapper.CreateSuccessResponse(
                            new AiNpcResponse(
                                "응답",
                                NpcEmotion.Neutral,
                                NpcGesture.None),
                            request.requestId));
                });
            var client = new BackendConversationClient(gateway);

            await client.SendAsync(CreateRequest("첫 번째"), CancellationToken.None);
            await client.SendAsync(CreateRequest("두 번째"), CancellationToken.None);

            Assert.That(firstId, Is.Not.Empty);
            Assert.That(secondId, Is.Not.Empty);
            Assert.That(secondId, Is.Not.EqualTo(firstId));
        }

        /// <summary>
        /// Preserves a server error code, safe message, and retryability metadata.
        /// </summary>
        [Test]
        public void SendAsync_ErrorEnvelope_ThrowsConversationException()
        {
            var gateway = new StubGateway(
                (request, _) => Task.FromResult(
                    AiNpcContractMapper.CreateErrorResponse(
                        request.requestId,
                        AiNpcContractV1.RateLimitedErrorCode,
                        "잠시 후 다시 시도해 주세요.",
                        true)));
            var client = new BackendConversationClient(gateway);

            var exception = Assert.ThrowsAsync<AiConversationException>(
                async () => await client.SendAsync(
                    CreateRequest("안녕"),
                    CancellationToken.None));

            Assert.That(exception.Code, Is.EqualTo("rate_limited"));
            Assert.That(exception.Message, Is.EqualTo("잠시 후 다시 시도해 주세요."));
            Assert.That(exception.Retryable, Is.True);
        }

        /// <summary>
        /// Rejects a valid response that does not correlate to the active request.
        /// </summary>
        [Test]
        public void SendAsync_MismatchedRequestId_ThrowsProtocolError()
        {
            var gateway = new StubGateway(
                (_, _) => Task.FromResult(
                    AiNpcContractMapper.CreateSuccessResponse(
                        new AiNpcResponse(
                            "응답",
                            NpcEmotion.Neutral,
                            NpcGesture.None),
                        "req-different")));
            var client = new BackendConversationClient(gateway);

            var exception = Assert.ThrowsAsync<AiConversationException>(
                async () => await client.SendAsync(
                    CreateRequest("안녕"),
                    CancellationToken.None));

            Assert.That(
                exception.Code,
                Is.EqualTo(AiNpcBackendErrorCodes.BackendProtocolError));
            Assert.That(exception.Retryable, Is.False);
        }

        /// <summary>
        /// Propagates caller cancellation instead of converting it into a backend failure.
        /// </summary>
        [Test]
        public void SendAsync_Cancelled_ThrowsOperationCanceledException()
        {
            var gateway = new StubGateway(
                async (_, cancellationToken) =>
                {
                    await Task.Delay(Timeout.Infinite, cancellationToken);
                    return null;
                });
            var client = new BackendConversationClient(gateway);

            using (var cancellation = new CancellationTokenSource())
            {
                cancellation.Cancel();
                Assert.CatchAsync<OperationCanceledException>(
                    async () => await client.SendAsync(
                        CreateRequest("안녕"),
                        cancellation.Token));
            }
        }

        /// <summary>
        /// Accepts the planned loopback endpoint and positive timeout.
        /// </summary>
        [Test]
        public void Gateway_LoopbackEndpoint_Constructs()
        {
            Assert.DoesNotThrow(
                () => new UnityWebRequestAiNpcBackendGateway(
                    "http://127.0.0.1:8787/v1/npc/respond",
                    35));
        }

        /// <summary>
        /// Rejects non-loopback endpoints so the prototype cannot silently become remote.
        /// </summary>
        [Test]
        public void Gateway_RemoteEndpoint_ThrowsArgumentException()
        {
            Assert.Throws<ArgumentException>(
                () => new UnityWebRequestAiNpcBackendGateway(
                    "https://example.com/v1/npc/respond",
                    35));
        }

        /// <summary>
        /// Creates one complete immutable Core request used by backend client tests.
        /// </summary>
        private static AiNpcRequest CreateRequest(string userText)
        {
            return new AiNpcRequest(
                "sample-luna",
                "Luna",
                "Playful",
                "Warm",
                "안녕!",
                NpcEmotion.Happy,
                userText);
        }

        /// <summary>
        /// Provides a deterministic in-memory backend boundary for client tests.
        /// </summary>
        private sealed class StubGateway : IAiNpcBackendGateway
        {
            private readonly Func<
                AiNpcRequestEnvelopeDto,
                CancellationToken,
                Task<AiNpcResponseEnvelopeDto>> send;

            /// <summary>
            /// Captures one test-owned send function.
            /// </summary>
            public StubGateway(
                Func<
                    AiNpcRequestEnvelopeDto,
                    CancellationToken,
                    Task<AiNpcResponseEnvelopeDto>> send)
            {
                this.send = send;
            }

            /// <summary>
            /// Delegates a request to the current deterministic test function.
            /// </summary>
            public Task<AiNpcResponseEnvelopeDto> SendAsync(
                AiNpcRequestEnvelopeDto request,
                CancellationToken cancellationToken)
            {
                return send(request, cancellationToken);
            }
        }
    }
}
