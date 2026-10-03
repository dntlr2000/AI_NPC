using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using AiCharacterKit.Core;
using AiCharacterKit.Unity.Networking;
using AiCharacterKit.Unity.Transport;
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
        /// Rejects serialized byte overflow locally rather than contacting an unavailable HTTP endpoint.
        /// </summary>
        [TestCase(1)]
        [TestCase(64)]
        public void Gateway_OverSerializedLimit_RejectsBeforeHttp(int excessBytes)
        {
            Assert.That(V4.AiNpcContractV4.MaxRequestBodyUtf8Bytes, Is.EqualTo(524288));
            var request = CreateSizedEnvelope(V4.AiNpcContractV4.MaxRequestBodyUtf8Bytes + excessBytes);
            var gateway = new UnityWebRequestAiNpcContextBackendGateway(
                "http://127.0.0.1:1/v4/npc/respond", "http://127.0.0.1:1/v4/npc/sessions/reset", 1);

            var error = Assert.ThrowsAsync<AiConversationException>(async () =>
                await gateway.SendAsync(request, CancellationToken.None));

            Assert.That(error.Code, Is.EqualTo("invalid_request"));
            Assert.That(error.Retryable, Is.False);
            Assert.That(error.Message, Is.EqualTo("NPC 대화 요청이 최대 허용 크기(512 KiB)를 초과했습니다."));
        }

        /// <summary>
        /// Sends the exact inclusive UTF-8 boundary to a loopback fake and verifies the received byte count.
        /// </summary>
        [Test]
        public async Task Gateway_ExactSerializedLimit_ReachesLoopbackFake()
        {
            var request = CreateSizedEnvelope(V4.AiNpcContractV4.MaxRequestBodyUtf8Bytes);
            var response = new V4.AiNpcResponseEnvelopeDto
            {
                schemaVersion = 4, requestId = request.requestId, status = "success",
                result = new V4.AiNpcResponsePayloadDto
                {
                    dialogue = "Synthetic reply.", emotion = "neutral", gesture = "none",
                    matchedTriggerIds = Array.Empty<string>()
                }
            };
            Assert.That(AiNpcJsonCodecV4.TrySerializeResponse(response, out var responseJson, out var error), Is.True, error);
            var listener = new TcpListener(IPAddress.Loopback, 0);
            using (var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(15)))
            {
                listener.Start();
                using (cancellation.Token.Register(listener.Stop))
                {
                    var port = ((IPEndPoint)listener.LocalEndpoint).Port;
                    var receiving = Task.Run(
                        () => ReceiveOneRequestAsync(listener, responseJson, cancellation.Token),
                        cancellation.Token);
                    try
                    {
                        var gateway = new UnityWebRequestAiNpcContextBackendGateway(
                            $"http://127.0.0.1:{port}/v4/npc/respond",
                            $"http://127.0.0.1:{port}/v4/npc/sessions/reset", 10);
                        var result = await gateway.SendAsync(request, cancellation.Token);
                        Assert.That(result.result.dialogue, Is.EqualTo("Synthetic reply."));
                        Assert.That(await receiving, Is.EqualTo(V4.AiNpcContractV4.MaxRequestBodyUtf8Bytes));
                    }
                    finally
                    {
                        cancellation.Cancel();
                        listener.Stop();
                        try { await receiving; }
                        catch when (cancellation.IsCancellationRequested) { }
                    }
                }
            }
        }

        /// <summary>
        /// Builds an exact serialized byte count including Korean text, quotes, slashes, and escapes.
        /// </summary>
        private static V4.AiNpcRequestEnvelopeDto CreateSizedEnvelope(int targetBytes)
        {
            var request = V4.AiNpcContractMapper.CreateRequest(
                CreateRequest(), "req-body-boundary", "session-body-boundary", Array.Empty<NpcTriggerDefinition>());
            request.character.personality = "한\"\\\n\u0001x";
            Assert.That(AiNpcJsonCodecV4.TrySerializeRequest(request, out var initial, out var error), Is.True, error);
            request.character.personality += new string('x', targetBytes - Encoding.UTF8.GetByteCount(initial));
            Assert.That(AiNpcJsonCodecV4.TrySerializeRequest(request, out var complete, out error), Is.True, error);
            Assert.That(Encoding.UTF8.GetByteCount(complete), Is.EqualTo(targetBytes));
            return request;
        }

        /// <summary>
        /// Consumes one bounded HTTP request in memory and returns a fixed response without logging its body.
        /// </summary>
        private static async Task<int> ReceiveOneRequestAsync(
            TcpListener listener, string responseJson, CancellationToken cancellationToken)
        {
            using (var client = await listener.AcceptTcpClientAsync())
            using (var stream = client.GetStream())
            {
                var headerBytes = new List<byte>();
                var singleByte = new byte[1];
                while (headerBytes.Count < 16 * 1024)
                {
                    if (await stream.ReadAsync(singleByte, 0, 1, cancellationToken) == 0)
                        throw new EndOfStreamException("Incomplete synthetic HTTP headers.");
                    headerBytes.Add(singleByte[0]);
                    var count = headerBytes.Count;
                    if (count >= 4 && headerBytes[count - 4] == 13 && headerBytes[count - 3] == 10
                        && headerBytes[count - 2] == 13 && headerBytes[count - 1] == 10) break;
                }
                var headers = Encoding.ASCII.GetString(headerBytes.ToArray());
                var contentLength = -1;
                foreach (var line in headers.Split(new[] { "\r\n" }, StringSplitOptions.None))
                {
                    if (line.StartsWith("Content-Length:", StringComparison.OrdinalIgnoreCase))
                        contentLength = int.Parse(line.Substring("Content-Length:".Length).Trim());
                }
                if (contentLength < 0 || contentLength > V4.AiNpcContractV4.MaxRequestBodyUtf8Bytes)
                    throw new InvalidDataException("Unexpected synthetic HTTP body size.");
                if (headers.IndexOf("100-continue", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    var interim = Encoding.ASCII.GetBytes("HTTP/1.1 100 Continue\r\n\r\n");
                    await stream.WriteAsync(interim, 0, interim.Length, cancellationToken);
                }
                var buffer = new byte[8192];
                var remaining = contentLength;
                while (remaining > 0)
                {
                    var read = await stream.ReadAsync(buffer, 0, Math.Min(buffer.Length, remaining), cancellationToken);
                    if (read == 0) throw new EndOfStreamException("Incomplete synthetic HTTP body.");
                    remaining -= read;
                }
                var body = Encoding.UTF8.GetBytes(responseJson);
                var responseHeaders = Encoding.ASCII.GetBytes(
                    $"HTTP/1.1 200 OK\r\nContent-Type: application/json\r\nContent-Length: {body.Length}\r\nConnection: close\r\n\r\n");
                await stream.WriteAsync(responseHeaders, 0, responseHeaders.Length, cancellationToken);
                await stream.WriteAsync(body, 0, body.Length, cancellationToken);
                return contentLength;
            }
        }

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
