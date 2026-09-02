using System;
using System.Threading;
using System.Threading.Tasks;
using AiCharacterKit.Core;
using AiCharacterKit.Unity.Networking;
using NUnit.Framework;
using V3 = AiCharacterKit.Transport.V3;

namespace AiCharacterKit.Core.Tests
{
    /// <summary>
    /// Verifies V3 client correlation, stable sessions, reset, and trigger subset enforcement.
    /// </summary>
    public sealed class ActionBackendConversationClientTests
    {
        /// <summary>
        /// Confirms a successful response preserves a fixed session and matched trigger ID.
        /// </summary>
        [Test]
        public async Task SendAsync_ValidResponse_PreservesSessionAndMatchedIds()
        {
            var gateway = new RecordingGateway();
            var client = new ActionBackendConversationClient(
                gateway,
                "sample-guide",
                CreateDefinitions(),
                "session-action-test");

            var response = await client.SendAsync(CreateRequest(), CancellationToken.None);

            Assert.That(gateway.LastRequest.sessionId, Is.EqualTo("session-action-test"));
            Assert.That(gateway.LastRequest.triggers[0].triggerId, Is.EqualTo("open_gate"));
            Assert.That(response.MatchedTriggerIds, Is.EqualTo(new[] { "open_gate" }));
        }

        /// <summary>
        /// Confirms a valid-shaped but unknown backend trigger is rejected as a protocol error.
        /// </summary>
        [Test]
        public void SendAsync_UnknownTrigger_ThrowsSafeProtocolError()
        {
            var gateway = new RecordingGateway { ReturnUnknownTrigger = true };
            var client = new ActionBackendConversationClient(
                gateway,
                "sample-guide",
                CreateDefinitions(),
                "session-action-test");

            var exception = Assert.ThrowsAsync<AiConversationException>(async () =>
                await client.SendAsync(CreateRequest(), CancellationToken.None));

            Assert.That(exception.Code, Is.EqualTo("backend_protocol_error"));
        }

        /// <summary>
        /// Confirms reset reuses the same V3 session and character binding.
        /// </summary>
        [Test]
        public async Task ResetAsync_UsesStableSessionAndCorrelation()
        {
            var gateway = new RecordingGateway();
            var client = new ActionBackendConversationClient(
                gateway,
                "sample-guide",
                CreateDefinitions(),
                "session-action-test");

            await client.ResetAsync(CancellationToken.None);

            Assert.That(gateway.LastReset.sessionId, Is.EqualTo("session-action-test"));
            Assert.That(gateway.LastReset.characterId, Is.EqualTo("sample-guide"));
        }

        /// <summary>
        /// Confirms mismatched request correlation is rejected without exposing response data.
        /// </summary>
        [Test]
        public void SendAsync_MismatchedCorrelation_ThrowsSafeProtocolError()
        {
            var gateway = new RecordingGateway
            {
                ResponseRequestId = "req-from-another-call"
            };
            var client = new ActionBackendConversationClient(
                gateway,
                "sample-guide",
                CreateDefinitions(),
                "session-action-test");

            var exception = Assert.ThrowsAsync<AiConversationException>(async () =>
                await client.SendAsync(CreateRequest(), CancellationToken.None));

            Assert.That(exception.Code, Is.EqualTo("backend_protocol_error"));
        }

        /// <summary>
        /// Confirms V3 conversation and reset error branches preserve safe retry metadata.
        /// </summary>
        [Test]
        public void BackendErrors_MapToExistingConversationException()
        {
            var gateway = new RecordingGateway
            {
                ConversationErrorCode = "session_busy",
                ResetErrorCode = "session_capacity_reached"
            };
            var client = new ActionBackendConversationClient(
                gateway,
                "sample-guide",
                CreateDefinitions(),
                "session-action-test");

            var conversationError = Assert.ThrowsAsync<AiConversationException>(async () =>
                await client.SendAsync(CreateRequest(), CancellationToken.None));
            var resetError = Assert.ThrowsAsync<AiConversationException>(async () =>
                await client.ResetAsync(CancellationToken.None));

            Assert.That(conversationError.Code, Is.EqualTo("session_busy"));
            Assert.That(conversationError.Retryable, Is.True);
            Assert.That(resetError.Code, Is.EqualTo("session_capacity_reached"));
            Assert.That(resetError.Retryable, Is.True);
        }

        /// <summary>
        /// Confirms pre-cancelled V3 operations never call the gateway.
        /// </summary>
        [Test]
        public void CancelledOperations_DoNotSendOrReset()
        {
            var gateway = new RecordingGateway();
            var client = new ActionBackendConversationClient(
                gateway,
                "sample-guide",
                CreateDefinitions(),
                "session-action-test");
            using var cancellation = new CancellationTokenSource();
            cancellation.Cancel();

            Assert.CatchAsync<OperationCanceledException>(async () =>
                await client.SendAsync(CreateRequest(), cancellation.Token));
            Assert.CatchAsync<OperationCanceledException>(async () =>
                await client.ResetAsync(cancellation.Token));
            Assert.That(gateway.LastRequest, Is.Null);
            Assert.That(gateway.LastReset, Is.Null);
        }

        /// <summary>
        /// Creates one bounded action definition snapshot.
        /// </summary>
        private static NpcTriggerDefinition[] CreateDefinitions()
        {
            return new[]
            {
                new NpcTriggerDefinition(
                    "open_gate",
                    "The player asks to open the gate.",
                    "open",
                    "open_gate_action",
                    1)
            };
        }

        /// <summary>
        /// Creates one valid request for a fixed character binding.
        /// </summary>
        private static AiNpcRequest CreateRequest()
        {
            return new AiNpcRequest(
                "sample-guide",
                "Guide",
                "Helpful",
                "Brief",
                "Hello.",
                NpcEmotion.Neutral,
                "open");
        }

        private sealed class RecordingGateway : IAiNpcActionBackendGateway
        {
            public V3.AiNpcRequestEnvelopeDto LastRequest { get; private set; }
            public V3.AiNpcSessionResetRequestDto LastReset { get; private set; }
            public bool ReturnUnknownTrigger { get; set; }
            public string ResponseRequestId { get; set; }
            public string ConversationErrorCode { get; set; }
            public string ResetErrorCode { get; set; }

            /// <summary>
            /// Returns one correlated success with a controlled matched trigger ID.
            /// </summary>
            public Task<V3.AiNpcResponseEnvelopeDto> SendAsync(
                V3.AiNpcRequestEnvelopeDto request,
                CancellationToken cancellationToken)
            {
                LastRequest = request;
                var responseRequestId = ResponseRequestId ?? request.requestId;
                if (!string.IsNullOrWhiteSpace(ConversationErrorCode))
                {
                    return Task.FromResult(V3.AiNpcContractMapper.CreateErrorResponse(
                        responseRequestId,
                        ConversationErrorCode,
                        "Controlled conversation failure.",
                        true));
                }

                var triggerId = ReturnUnknownTrigger ? "invented_trigger" : "open_gate";
                return Task.FromResult(new V3.AiNpcResponseEnvelopeDto
                {
                    schemaVersion = 3,
                    requestId = responseRequestId,
                    status = "success",
                    result = new V3.AiNpcResponsePayloadDto
                    {
                        dialogue = "Done",
                        emotion = "neutral",
                        gesture = "none",
                        matchedTriggerIds = new[] { triggerId }
                    }
                });
            }

            /// <summary>
            /// Returns one correlated successful V3 reset acknowledgement.
            /// </summary>
            public Task<V3.AiNpcSessionResetResponseDto> ResetAsync(
                V3.AiNpcSessionResetRequestDto request,
                CancellationToken cancellationToken)
            {
                LastReset = request;
                if (!string.IsNullOrWhiteSpace(ResetErrorCode))
                {
                    return Task.FromResult(V3.AiNpcContractMapper.CreateResetErrorResponse(
                        request.requestId,
                        ResetErrorCode,
                        "Controlled reset failure.",
                        true));
                }

                return Task.FromResult(V3.AiNpcContractMapper.CreateResetSuccessResponse(
                    request.requestId));
            }
        }
    }
}
