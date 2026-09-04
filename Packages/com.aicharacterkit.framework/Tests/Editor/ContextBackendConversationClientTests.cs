using System;
using System.Threading;
using System.Threading.Tasks;
using AiCharacterKit.Core;
using AiCharacterKit.Unity.Networking;
using NUnit.Framework;
using V4 = AiCharacterKit.Transport.V4;

namespace AiCharacterKit.Core.Tests
{
    /// <summary>
    /// Verifies V4 client correlation, grounding transfer, stable sessions, and trigger safety.
    /// </summary>
    public sealed class ContextBackendConversationClientTests
    {
        /// <summary>
        /// Confirms a grounded request supports no actions and preserves its fixed session.
        /// </summary>
        [Test]
        public async Task SendAsync_EmptyTriggers_PreservesGroundingAndSession()
        {
            var gateway = new RecordingContextGateway();
            var client = new ContextBackendConversationClient(
                gateway,
                "sample-guard",
                Array.Empty<NpcTriggerDefinition>(),
                "session-context-test");

            var response = await client.SendAsync(
                CreateRequest(),
                CancellationToken.None);

            Assert.That(gateway.LastRequest.sessionId, Is.EqualTo("session-context-test"));
            Assert.That(gateway.LastRequest.triggers, Is.Empty);
            Assert.That(gateway.LastRequest.grounding.facts[0].factId,
                Is.EqualTo("gate_status"));
            Assert.That(response.Dialogue, Is.EqualTo("The gate is closed."));
        }

        /// <summary>
        /// Confirms optional configured action IDs survive while unknown IDs are rejected.
        /// </summary>
        [Test]
        public void SendAsync_UnknownTrigger_ThrowsSafeProtocolError()
        {
            var gateway = new RecordingContextGateway
            {
                MatchedTriggerIds = new[] { "invented_trigger" }
            };
            var definition = new NpcTriggerDefinition(
                "open_gate",
                "The player asks to open the gate.",
                "open",
                "open_gate_action",
                1);
            var client = new ContextBackendConversationClient(
                gateway,
                "sample-guard",
                new[] { definition },
                "session-context-test");

            var exception = Assert.ThrowsAsync<AiConversationException>(async () =>
                await client.SendAsync(CreateRequest(), CancellationToken.None));

            Assert.That(exception.Code, Is.EqualTo("backend_protocol_error"));
        }

        /// <summary>
        /// Confirms reset reuses the V4 session and maps safe backend failures.
        /// </summary>
        [Test]
        public async Task ResetAsync_UsesStableSessionAndMapsErrors()
        {
            var gateway = new RecordingContextGateway();
            var client = new ContextBackendConversationClient(
                gateway,
                "sample-guard",
                Array.Empty<NpcTriggerDefinition>(),
                "session-context-test");

            await client.ResetAsync(CancellationToken.None);
            Assert.That(gateway.LastReset.sessionId, Is.EqualTo("session-context-test"));

            gateway.ResetErrorCode = "session_busy";
            var error = Assert.ThrowsAsync<AiConversationException>(async () =>
                await client.ResetAsync(CancellationToken.None));
            Assert.That(error.Code, Is.EqualTo("session_busy"));
            Assert.That(error.Retryable, Is.True);
        }

        /// <summary>
        /// Creates one valid domain request carrying a current observation.
        /// </summary>
        private static AiNpcRequest CreateRequest()
        {
            var grounding = new NpcGroundingSnapshot(
                "The western gate protects Dawnfall.",
                "Protect citizens.",
                new[] { "Do not invent access permissions." },
                Array.Empty<string>(),
                new[]
                {
                    new NpcContextFact(
                        "gate_status",
                        NpcContextFactKind.Observation,
                        "The western gate is closed.",
                        90)
                });
            return new AiNpcRequest(
                "sample-guard",
                "Guard",
                "Disciplined",
                "Formal",
                "State your business.",
                NpcEmotion.Neutral,
                "Is the gate open?",
                grounding);
        }

        private sealed class RecordingContextGateway : IAiNpcContextBackendGateway
        {
            public V4.AiNpcRequestEnvelopeDto LastRequest { get; private set; }
            public V4.AiNpcSessionResetRequestDto LastReset { get; private set; }
            public string[] MatchedTriggerIds { get; set; } = Array.Empty<string>();
            public string ResetErrorCode { get; set; }

            /// <summary>
            /// Returns one correlated success with controlled matched trigger IDs.
            /// </summary>
            public Task<V4.AiNpcResponseEnvelopeDto> SendAsync(
                V4.AiNpcRequestEnvelopeDto request,
                CancellationToken cancellationToken)
            {
                LastRequest = request;
                return Task.FromResult(new V4.AiNpcResponseEnvelopeDto
                {
                    schemaVersion = V4.AiNpcContractV4.SchemaVersion,
                    requestId = request.requestId,
                    status = V4.AiNpcContractV4.SuccessStatus,
                    result = new V4.AiNpcResponsePayloadDto
                    {
                        dialogue = "The gate is closed.",
                        emotion = "neutral",
                        gesture = "nod",
                        matchedTriggerIds = MatchedTriggerIds
                    }
                });
            }

            /// <summary>
            /// Returns one correlated V4 reset success or controlled safe error.
            /// </summary>
            public Task<V4.AiNpcSessionResetResponseDto> ResetAsync(
                V4.AiNpcSessionResetRequestDto request,
                CancellationToken cancellationToken)
            {
                LastReset = request;
                return Task.FromResult(string.IsNullOrWhiteSpace(ResetErrorCode)
                    ? V4.AiNpcContractMapper.CreateResetSuccessResponse(request.requestId)
                    : V4.AiNpcContractMapper.CreateResetErrorResponse(
                        request.requestId,
                        ResetErrorCode,
                        "Controlled reset failure.",
                        true));
            }
        }
    }
}
