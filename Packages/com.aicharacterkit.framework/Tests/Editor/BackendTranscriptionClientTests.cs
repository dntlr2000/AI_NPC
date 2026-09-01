using System;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using AiCharacterKit.Transcription;
using AiCharacterKit.Transport.Transcription.V1;
using AiCharacterKit.Unity.Networking;
using NUnit.Framework;

namespace AiCharacterKit.Core.Tests
{
    /// <summary>
    /// Verifies Transcription V1 correlation and safe failure behavior without HTTP.
    /// </summary>
    public sealed class BackendTranscriptionClientTests
    {
        /// <summary>
        /// Sends one WAV and accepts only its correlated successful transcript.
        /// </summary>
        [Test]
        public async Task TranscribeAsync_ValidResponse_PreservesAudioAndText()
        {
            byte[] capturedBytes = null;
            string capturedRequestId = null;
            var gateway = new StubGateway(
                (bytes, requestId, _) =>
                {
                    capturedBytes = bytes;
                    capturedRequestId = requestId;
                    return Task.FromResult(
                        TranscriptionContractMapper.CreateSuccessResponse(
                            requestId,
                            new TranscriptionResult("안녕하세요.")));
                });
            var client = new BackendTranscriptionClient(gateway);
            var audio = CreateAudio();

            var result = await client.TranscribeAsync(
                audio,
                CancellationToken.None);

            Assert.That(result.Text, Is.EqualTo("안녕하세요."));
            Assert.That(capturedBytes, Is.SameAs(audio.WaveBytes));
            Assert.That(
                Regex.IsMatch(
                    capturedRequestId,
                    "^transcription-[0-9a-f]{32}$"),
                Is.True);
        }

        /// <summary>
        /// Rejects mismatched correlation and preserves valid backend errors.
        /// </summary>
        [Test]
        public void TranscribeAsync_CorrelationAndError_UseSafeSemantics()
        {
            var mismatched = new BackendTranscriptionClient(
                new StubGateway(
                    (_, _, _) => Task.FromResult(
                        TranscriptionContractMapper.CreateSuccessResponse(
                            "transcription-wrong",
                            new TranscriptionResult("wrong")))));
            var protocolError = Assert.ThrowsAsync<TranscriptionException>(
                async () => await mismatched.TranscribeAsync(
                    CreateAudio(),
                    CancellationToken.None));
            Assert.That(
                protocolError.Code,
                Is.EqualTo(TranscriptionBackendErrorCodes.BackendProtocolError));

            var backendFailure = new BackendTranscriptionClient(
                new StubGateway(
                    (_, requestId, _) => Task.FromResult(
                        TranscriptionContractMapper.CreateErrorResponse(
                            requestId,
                            new TranscriptionException(
                                "rate_limited",
                                "잠시 후 다시 시도해 주세요.",
                                true)))));
            var safeError = Assert.ThrowsAsync<TranscriptionException>(
                async () => await backendFailure.TranscribeAsync(
                    CreateAudio(),
                    CancellationToken.None));
            Assert.That(safeError.Code, Is.EqualTo("rate_limited"));
            Assert.That(safeError.Retryable, Is.True);
        }

        /// <summary>
        /// Honors pre-cancellation before contacting the backend gateway.
        /// </summary>
        [Test]
        public void TranscribeAsync_PreCanceled_DoesNotCallGateway()
        {
            using (var cancellation = new CancellationTokenSource())
            {
                cancellation.Cancel();
                var client = new BackendTranscriptionClient(
                    new StubGateway(
                        (_, _, _) => throw new AssertionException(
                            "Gateway must not be called.")));
                Assert.CatchAsync<OperationCanceledException>(
                    async () => await client.TranscribeAsync(
                        CreateAudio(),
                        cancellation.Token));
            }
        }

        /// <summary>
        /// Accepts loopback configuration and rejects direct remote endpoints.
        /// </summary>
        [Test]
        public void Gateway_EndpointValidation_RequiresLoopback()
        {
            Assert.DoesNotThrow(
                () => new UnityWebRequestAiTranscriptionBackendGateway(
                    "http://127.0.0.1:8787/v1/speech/transcribe",
                    35));
            Assert.Throws<ArgumentException>(
                () => new UnityWebRequestAiTranscriptionBackendGateway(
                    "https://example.com/v1/speech/transcribe",
                    35));
            Assert.Throws<ArgumentOutOfRangeException>(
                () => new UnityWebRequestAiTranscriptionBackendGateway(
                    "http://localhost:8787/v1/speech/transcribe",
                    0));
        }

        /// <summary>
        /// Creates one canonical sample capture shared by client tests.
        /// </summary>
        private static CapturedAudioData CreateAudio()
        {
            return Pcm16WaveEncoder.Encode(new[] { 0f }, 1, 16000, 1);
        }

        /// <summary>
        /// Delegates gateway calls to one deterministic test-owned function.
        /// </summary>
        private sealed class StubGateway : IAiTranscriptionBackendGateway
        {
            private readonly Func<
                byte[],
                string,
                CancellationToken,
                Task<TranscriptionResponseEnvelopeDto>> send;

            /// <summary>
            /// Captures the deterministic gateway function for this instance.
            /// </summary>
            public StubGateway(
                Func<
                    byte[],
                    string,
                    CancellationToken,
                    Task<TranscriptionResponseEnvelopeDto>> send)
            {
                this.send = send;
            }

            /// <summary>
            /// Delegates one WAV request to the configured in-memory function.
            /// </summary>
            public Task<TranscriptionResponseEnvelopeDto> SendAsync(
                byte[] waveBytes,
                string requestId,
                CancellationToken cancellationToken)
            {
                return send(waveBytes, requestId, cancellationToken);
            }
        }
    }
}
