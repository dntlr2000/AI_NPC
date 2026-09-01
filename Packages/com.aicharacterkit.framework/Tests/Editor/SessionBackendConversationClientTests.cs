using System;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using AiCharacterKit.Core;
using AiCharacterKit.Transport.V2;
using AiCharacterKit.Unity.Networking;
using NUnit.Framework;

namespace AiCharacterKit.Core.Tests
{
    /// <summary>
    /// Verifies stable session correlation, reset, errors, and endpoint safety without HTTP.
    /// </summary>
    public sealed class SessionBackendConversationClientTests
    {
        /// <summary>
        /// Reuses one session ID while generating distinct operation correlation IDs.
        /// </summary>
        [Test]
        public async Task SendAndReset_ValidResponses_ReuseStableSessionId()
        {
            AiNpcRequestEnvelopeDto firstRequest = null;
            AiNpcRequestEnvelopeDto secondRequest = null;
            AiNpcSessionResetRequestDto resetRequest = null;
            var sendCount = 0;
            var gateway = new StubGateway(
                (request, _) =>
                {
                    sendCount++;
                    if (sendCount == 1)
                    {
                        firstRequest = request;
                    }
                    else
                    {
                        secondRequest = request;
                    }

                    return Task.FromResult(
                        AiNpcContractMapper.CreateSuccessResponse(
                            new AiNpcResponse(
                                "응답",
                                NpcEmotion.Happy,
                                NpcGesture.Nod),
                            request.requestId));
                },
                (request, _) =>
                {
                    resetRequest = request;
                    return Task.FromResult(
                        AiNpcContractMapper.CreateResetSuccessResponse(
                            request.requestId));
                });
            var client = new SessionBackendConversationClient(
                gateway,
                "sample-luna",
                "session-stable");

            await client.SendAsync(CreateRequest("첫 번째"), CancellationToken.None);
            await client.SendAsync(CreateRequest("두 번째"), CancellationToken.None);
            await client.ResetAsync(CancellationToken.None);

            Assert.That(firstRequest.sessionId, Is.EqualTo("session-stable"));
            Assert.That(secondRequest.sessionId, Is.EqualTo("session-stable"));
            Assert.That(resetRequest.sessionId, Is.EqualTo("session-stable"));
            Assert.That(resetRequest.characterId, Is.EqualTo("sample-luna"));
            Assert.That(secondRequest.requestId, Is.Not.EqualTo(firstRequest.requestId));
            Assert.That(resetRequest.requestId, Is.Not.EqualTo(secondRequest.requestId));
        }

        /// <summary>
        /// Generates one stable component-lifetime ID with the documented opaque format.
        /// </summary>
        [Test]
        public async Task Constructor_WithoutSessionId_GeneratesStableGuidSessionId()
        {
            var capturedSessionIds = new string[2];
            var requestCount = 0;
            var gateway = new StubGateway(
                (request, _) =>
                {
                    capturedSessionIds[requestCount++] = request.sessionId;
                    return Task.FromResult(
                        AiNpcContractMapper.CreateSuccessResponse(
                            new AiNpcResponse(
                                "응답",
                                NpcEmotion.Neutral,
                                NpcGesture.None),
                            request.requestId));
                },
                (request, _) => Task.FromResult(
                    AiNpcContractMapper.CreateResetSuccessResponse(
                        request.requestId)));
            var client = new SessionBackendConversationClient(
                gateway,
                "sample-luna");

            await client.SendAsync(CreateRequest("첫 번째"), CancellationToken.None);
            await client.SendAsync(CreateRequest("두 번째"), CancellationToken.None);

            Assert.That(capturedSessionIds[1], Is.EqualTo(capturedSessionIds[0]));
            Assert.That(
                Regex.IsMatch(
                    capturedSessionIds[0],
                    "^session-[0-9a-f]{32}$",
                    RegexOptions.CultureInvariant),
                Is.True);
        }

        /// <summary>
        /// Preserves safe server error metadata from both conversation and reset operations.
        /// </summary>
        [Test]
        public void SendAndReset_ErrorResponses_ThrowConversationExceptions()
        {
            var gateway = new StubGateway(
                (request, _) => Task.FromResult(
                    AiNpcContractMapper.CreateErrorResponse(
                        request.requestId,
                        "session_busy",
                        "Session busy.",
                        true)),
                (request, _) => Task.FromResult(
                    AiNpcContractMapper.CreateResetErrorResponse(
                        request.requestId,
                        "session_character_mismatch",
                        "Character mismatch.",
                        false)));
            var client = new SessionBackendConversationClient(
                gateway,
                "sample-luna",
                "session-errors");

            var sendError = Assert.ThrowsAsync<AiConversationException>(
                async () => await client.SendAsync(
                    CreateRequest("안녕"),
                    CancellationToken.None));
            var resetError = Assert.ThrowsAsync<AiConversationException>(
                async () => await client.ResetAsync(CancellationToken.None));

            Assert.That(sendError.Code, Is.EqualTo("session_busy"));
            Assert.That(sendError.Retryable, Is.True);
            Assert.That(
                resetError.Code,
                Is.EqualTo("session_character_mismatch"));
            Assert.That(resetError.Retryable, Is.False);
        }

        /// <summary>
        /// Rejects response correlation mismatches for both V2 operations.
        /// </summary>
        [Test]
        public void SendAndReset_MismatchedRequestIds_ThrowProtocolErrors()
        {
            var gateway = new StubGateway(
                (_, _) => Task.FromResult(
                    AiNpcContractMapper.CreateSuccessResponse(
                        new AiNpcResponse(
                            "응답",
                            NpcEmotion.Neutral,
                            NpcGesture.None),
                        "req-wrong-send")),
                (_, _) => Task.FromResult(
                    AiNpcContractMapper.CreateResetSuccessResponse(
                        "req-wrong-reset")));
            var client = new SessionBackendConversationClient(
                gateway,
                "sample-luna",
                "session-protocol");

            var sendError = Assert.ThrowsAsync<AiConversationException>(
                async () => await client.SendAsync(
                    CreateRequest("안녕"),
                    CancellationToken.None));
            var resetError = Assert.ThrowsAsync<AiConversationException>(
                async () => await client.ResetAsync(CancellationToken.None));

            Assert.That(
                sendError.Code,
                Is.EqualTo(AiNpcBackendErrorCodes.BackendProtocolError));
            Assert.That(
                resetError.Code,
                Is.EqualTo(AiNpcBackendErrorCodes.BackendProtocolError));
        }

        /// <summary>
        /// Rejects a Core request whose character differs from the bound session.
        /// </summary>
        [Test]
        public void SendAsync_DifferentCharacter_ThrowsProtocolErrorBeforeGateway()
        {
            var gateway = new StubGateway(
                (_, _) => throw new AssertionException("Gateway must not be called."),
                (_, _) => throw new AssertionException("Gateway must not be called."));
            var client = new SessionBackendConversationClient(
                gateway,
                "sample-luna",
                "session-character");

            var exception = Assert.ThrowsAsync<AiConversationException>(
                async () => await client.SendAsync(
                    CreateRequest("안녕", "sample-guard"),
                    CancellationToken.None));

            Assert.That(
                exception.Code,
                Is.EqualTo(AiNpcBackendErrorCodes.BackendProtocolError));
        }

        /// <summary>
        /// Accepts two loopback endpoints and rejects either remote endpoint.
        /// </summary>
        [Test]
        public void Gateway_EndpointValidation_RequiresLoopback()
        {
            Assert.DoesNotThrow(
                () => new UnityWebRequestAiNpcSessionBackendGateway(
                    "http://127.0.0.1:8787/v2/npc/respond",
                    "http://127.0.0.1:8787/v2/npc/sessions/reset",
                    35));
            Assert.Throws<ArgumentException>(
                () => new UnityWebRequestAiNpcSessionBackendGateway(
                    "https://example.com/v2/npc/respond",
                    "http://127.0.0.1:8787/v2/npc/sessions/reset",
                    35));
            Assert.Throws<ArgumentException>(
                () => new UnityWebRequestAiNpcSessionBackendGateway(
                    "http://127.0.0.1:8787/v2/npc/respond",
                    "https://example.com/v2/npc/sessions/reset",
                    35));
        }

        /// <summary>
        /// Creates one complete request for the selected character.
        /// </summary>
        private static AiNpcRequest CreateRequest(
            string userText,
            string characterId = "sample-luna")
        {
            return new AiNpcRequest(
                characterId,
                "Luna",
                "Playful",
                "Warm",
                "안녕!",
                NpcEmotion.Happy,
                userText);
        }

        /// <summary>
        /// Provides deterministic in-memory V2 conversation and reset boundaries.
        /// </summary>
        private sealed class StubGateway : IAiNpcSessionBackendGateway
        {
            private readonly Func<
                AiNpcRequestEnvelopeDto,
                CancellationToken,
                Task<AiNpcResponseEnvelopeDto>> send;
            private readonly Func<
                AiNpcSessionResetRequestDto,
                CancellationToken,
                Task<AiNpcSessionResetResponseDto>> reset;

            /// <summary>
            /// Captures test-owned send and reset functions.
            /// </summary>
            public StubGateway(
                Func<
                    AiNpcRequestEnvelopeDto,
                    CancellationToken,
                    Task<AiNpcResponseEnvelopeDto>> send,
                Func<
                    AiNpcSessionResetRequestDto,
                    CancellationToken,
                    Task<AiNpcSessionResetResponseDto>> reset)
            {
                this.send = send;
                this.reset = reset;
            }

            /// <summary>
            /// Delegates one conversation request to its deterministic test function.
            /// </summary>
            public Task<AiNpcResponseEnvelopeDto> SendAsync(
                AiNpcRequestEnvelopeDto request,
                CancellationToken cancellationToken)
            {
                return send(request, cancellationToken);
            }

            /// <summary>
            /// Delegates one reset request to its deterministic test function.
            /// </summary>
            public Task<AiNpcSessionResetResponseDto> ResetAsync(
                AiNpcSessionResetRequestDto request,
                CancellationToken cancellationToken)
            {
                return reset(request, cancellationToken);
            }
        }
    }
}
