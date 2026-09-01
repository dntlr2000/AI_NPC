using System;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using AiCharacterKit.Speech;
using AiCharacterKit.Transport.Speech.V1;
using AiCharacterKit.Unity.Networking;
using NUnit.Framework;

namespace AiCharacterKit.Core.Tests
{
    /// <summary>
    /// Verifies Speech V1 correlation and safe error behavior without HTTP.
    /// </summary>
    public sealed class BackendSpeechSynthesisClientTests
    {
        /// <summary>
        /// Maps one request and accepts only its correlated normalized PCM response.
        /// </summary>
        [Test]
        public async Task SynthesizeAsync_ValidResponse_PreservesRequestAndAudio()
        {
            SpeechSynthesisRequestDto captured = null;
            var expectedBytes = new byte[] { 0, 0, 255, 127 };
            var gateway = new StubGateway(
                (request, _) =>
                {
                    captured = request;
                    return Task.FromResult(
                        new SpeechBackendAudioResponse(
                            request.requestId,
                            expectedBytes));
                });
            var client = new BackendSpeechSynthesisClient(gateway);

            var audio = await client.SynthesizeAsync(
                new SpeechSynthesisRequest("warm-friendly", "정확한 대사"),
                CancellationToken.None);

            Assert.That(captured.schemaVersion, Is.EqualTo(1));
            Assert.That(captured.voicePresetId, Is.EqualTo("warm-friendly"));
            Assert.That(captured.text, Is.EqualTo("정확한 대사"));
            Assert.That(
                Regex.IsMatch(captured.requestId, "^speech-[0-9a-f]{32}$"),
                Is.True);
            Assert.That(audio.PcmBytes, Is.SameAs(expectedBytes));
        }

        /// <summary>
        /// Rejects a mismatched response ID as a stable non-retryable protocol failure.
        /// </summary>
        [Test]
        public void SynthesizeAsync_MismatchedCorrelation_ThrowsProtocolError()
        {
            var client = new BackendSpeechSynthesisClient(
                new StubGateway(
                    (_, _) => Task.FromResult(
                        new SpeechBackendAudioResponse(
                            "speech-wrong",
                            new byte[] { 0, 0 }))));

            var error = Assert.ThrowsAsync<SpeechSynthesisException>(
                async () => await client.SynthesizeAsync(
                    new SpeechSynthesisRequest("warm-friendly", "안녕"),
                    CancellationToken.None));

            Assert.That(
                error.Code,
                Is.EqualTo(SpeechBackendErrorCodes.BackendProtocolError));
            Assert.That(error.Retryable, Is.False);
        }

        /// <summary>
        /// Preserves safe server failures and caller cancellation unchanged.
        /// </summary>
        [Test]
        public void SynthesizeAsync_ErrorAndCancellation_PreserveExpectedSemantics()
        {
            var serverClient = new BackendSpeechSynthesisClient(
                new StubGateway(
                    (_, _) => throw new SpeechSynthesisException(
                        "rate_limited",
                        "잠시 후 다시 시도해 주세요.",
                        true)));
            var serverError = Assert.ThrowsAsync<SpeechSynthesisException>(
                async () => await serverClient.SynthesizeAsync(
                    new SpeechSynthesisRequest("warm-friendly", "안녕"),
                    CancellationToken.None));
            Assert.That(serverError.Code, Is.EqualTo("rate_limited"));
            Assert.That(serverError.Retryable, Is.True);

            using (var cancellation = new CancellationTokenSource())
            {
                cancellation.Cancel();
                var canceledClient = new BackendSpeechSynthesisClient(
                    new StubGateway(
                        (_, _) => throw new AssertionException(
                            "Gateway must not be called.")));
                Assert.CatchAsync<OperationCanceledException>(
                    async () => await canceledClient.SynthesizeAsync(
                        new SpeechSynthesisRequest("warm-friendly", "안녕"),
                        cancellation.Token));
            }
        }

        /// <summary>
        /// Accepts loopback configuration and rejects direct remote speech endpoints.
        /// </summary>
        [Test]
        public void Gateway_EndpointValidation_RequiresLoopback()
        {
            Assert.DoesNotThrow(
                () => new UnityWebRequestAiSpeechBackendGateway(
                    "http://127.0.0.1:8787/v1/speech/synthesize",
                    35));
            Assert.Throws<ArgumentException>(
                () => new UnityWebRequestAiSpeechBackendGateway(
                    "https://example.com/v1/speech/synthesize",
                    35));
            Assert.Throws<ArgumentOutOfRangeException>(
                () => new UnityWebRequestAiSpeechBackendGateway(
                    "http://localhost:8787/v1/speech/synthesize",
                    0));
        }

        /// <summary>
        /// Delegates gateway calls to one deterministic test-owned function.
        /// </summary>
        private sealed class StubGateway : IAiSpeechBackendGateway
        {
            private readonly Func<
                SpeechSynthesisRequestDto,
                CancellationToken,
                Task<SpeechBackendAudioResponse>> send;

            /// <summary>
            /// Captures the deterministic gateway function for this test instance.
            /// </summary>
            public StubGateway(
                Func<
                    SpeechSynthesisRequestDto,
                    CancellationToken,
                    Task<SpeechBackendAudioResponse>> send)
            {
                this.send = send;
            }

            /// <summary>
            /// Delegates one request to the configured in-memory function.
            /// </summary>
            public Task<SpeechBackendAudioResponse> SendAsync(
                SpeechSynthesisRequestDto request,
                CancellationToken cancellationToken)
            {
                return send(request, cancellationToken);
            }
        }
    }
}
