using System;
using System.Threading;
using System.Threading.Tasks;
using AiCharacterKit.Transcription;
using NUnit.Framework;

namespace AiCharacterKit.Core.Tests
{
    /// <summary>
    /// Verifies capture, duplicate prevention, cancellation, and failure in pure voice input.
    /// </summary>
    public sealed class VoiceInputControllerTests
    {
        /// <summary>
        /// Moves a complete capture through transcription and returns to idle.
        /// </summary>
        [Test]
        public async Task StopAndTranscribeAsync_Success_ReturnsTextAndIdle()
        {
            var driver = new RecordingCaptureDriver();
            var client = new ImmediateTranscriptionClient(
                new TranscriptionResult("안녕하세요."));
            using (var controller = new VoiceInputController(driver, client))
            {
                Assert.That(controller.StartRecording(), Is.True);
                Assert.That(controller.State, Is.EqualTo(VoiceInputState.Recording));

                var result = await controller.StopAndTranscribeAsync(
                    CancellationToken.None);

                Assert.That(result.Text, Is.EqualTo("안녕하세요."));
                Assert.That(controller.State, Is.EqualTo(VoiceInputState.Idle));
                Assert.That(driver.StartCount, Is.EqualTo(1));
                Assert.That(driver.StopCount, Is.EqualTo(1));
                Assert.That(client.CallCount, Is.EqualTo(1));
            }
        }

        /// <summary>
        /// Rejects duplicate starts while recording and transcribing.
        /// </summary>
        [Test]
        public async Task StartRecording_DuringActiveWork_ReturnsFalse()
        {
            var driver = new RecordingCaptureDriver();
            var client = new PendingTranscriptionClient();
            using (var controller = new VoiceInputController(driver, client))
            {
                Assert.That(controller.StartRecording(), Is.True);
                Assert.That(controller.StartRecording(), Is.False);
                var pending = controller.StopAndTranscribeAsync(
                    CancellationToken.None);
                Assert.That(controller.State, Is.EqualTo(VoiceInputState.Transcribing));
                Assert.That(controller.StartRecording(), Is.False);

                client.Complete("완료");
                Assert.That((await pending).Text, Is.EqualTo("완료"));
                Assert.That(driver.StartCount, Is.EqualTo(1));
            }
        }

        /// <summary>
        /// Cancels recording and pending transcription without producing stale text.
        /// </summary>
        [Test]
        public async Task Cancel_RecordingAndTranscribing_ReturnsIdle()
        {
            var driver = new RecordingCaptureDriver();
            var client = new PendingTranscriptionClient();
            using (var controller = new VoiceInputController(driver, client))
            {
                controller.StartRecording();
                controller.Cancel();
                Assert.That(controller.State, Is.EqualTo(VoiceInputState.Idle));
                Assert.That(driver.CancelCount, Is.EqualTo(1));

                controller.StartRecording();
                var pending = controller.StopAndTranscribeAsync(
                    CancellationToken.None);
                controller.Cancel();

                Assert.That(await pending, Is.Null);
                Assert.That(controller.State, Is.EqualTo(VoiceInputState.Idle));
            }
        }

        /// <summary>
        /// Converts safe client and unexpected capture failures into retryable UI state.
        /// </summary>
        [Test]
        public async Task Failures_ReturnNullAndPublishSafeMessages()
        {
            var safeMessage = string.Empty;
            var captureFailure = new RecordingCaptureDriver
            {
                StartError = new InvalidOperationException("private device detail")
            };
            using (var captureController = new VoiceInputController(
                       captureFailure,
                       new ImmediateTranscriptionClient(
                           new TranscriptionResult("unused"))))
            {
                captureController.StateChanged +=
                    (state, message) =>
                    {
                        if (state == VoiceInputState.Failed)
                        {
                            safeMessage = message;
                        }
                    };
                Assert.That(captureController.StartRecording(), Is.False);
                Assert.That(safeMessage, Is.EqualTo("음성 입력을 완료하지 못했습니다."));
            }

            var driver = new RecordingCaptureDriver();
            using (var clientController = new VoiceInputController(
                       driver,
                       new ThrowingTranscriptionClient(
                           new TranscriptionException(
                               "rate_limited",
                               "잠시 후 다시 시도해 주세요.",
                               true))))
            {
                clientController.StartRecording();
                var result = await clientController.StopAndTranscribeAsync(
                    CancellationToken.None);
                Assert.That(result, Is.Null);
                Assert.That(clientController.State, Is.EqualTo(VoiceInputState.Failed));
            }
        }

        /// <summary>
        /// Records capture lifecycle calls and returns one deterministic WAV.
        /// </summary>
        private sealed class RecordingCaptureDriver : IAudioCaptureDriver
        {
            public Exception StartError { get; set; }

            public int StartCount { get; private set; }

            public int StopCount { get; private set; }

            public int CancelCount { get; private set; }

            /// <summary>
            /// Records capture start or throws the configured test failure.
            /// </summary>
            public void StartCapture()
            {
                if (StartError != null)
                {
                    throw StartError;
                }

                StartCount++;
            }

            /// <summary>
            /// Returns a one-sample canonical WAV for transcription tests.
            /// </summary>
            public CapturedAudioData StopCapture()
            {
                StopCount++;
                return Pcm16WaveEncoder.Encode(
                    new[] { 0f },
                    1,
                    16000,
                    1);
            }

            /// <summary>
            /// Records best-effort capture cancellation.
            /// </summary>
            public void CancelCapture()
            {
                CancelCount++;
            }
        }

        /// <summary>
        /// Returns one configured transcription immediately.
        /// </summary>
        private sealed class ImmediateTranscriptionClient : ITranscriptionClient
        {
            private readonly TranscriptionResult result;

            public int CallCount { get; private set; }

            /// <summary>
            /// Captures the result returned by this test client.
            /// </summary>
            public ImmediateTranscriptionClient(TranscriptionResult result)
            {
                this.result = result;
            }

            /// <summary>
            /// Returns configured text after honoring caller cancellation.
            /// </summary>
            public Task<TranscriptionResult> TranscribeAsync(
                CapturedAudioData audioData,
                CancellationToken cancellationToken)
            {
                cancellationToken.ThrowIfCancellationRequested();
                CallCount++;
                return Task.FromResult(result);
            }
        }

        /// <summary>
        /// Holds one transcription call until the test completes or cancels it.
        /// </summary>
        private sealed class PendingTranscriptionClient : ITranscriptionClient
        {
            private TaskCompletionSource<TranscriptionResult> completion;

            /// <summary>
            /// Creates a cancellable pending transcription Task.
            /// </summary>
            public Task<TranscriptionResult> TranscribeAsync(
                CapturedAudioData audioData,
                CancellationToken cancellationToken)
            {
                completion = new TaskCompletionSource<TranscriptionResult>();
                cancellationToken.Register(() => completion.TrySetCanceled());
                return completion.Task;
            }

            /// <summary>
            /// Completes the active call with deterministic text.
            /// </summary>
            public void Complete(string text)
            {
                completion.TrySetResult(new TranscriptionResult(text));
            }
        }

        /// <summary>
        /// Returns one configured capture-safe transcription failure.
        /// </summary>
        private sealed class ThrowingTranscriptionClient : ITranscriptionClient
        {
            private readonly Exception exception;

            /// <summary>
            /// Captures the failure returned by every call.
            /// </summary>
            public ThrowingTranscriptionClient(Exception exception)
            {
                this.exception = exception;
            }

            /// <summary>
            /// Returns a faulted transcription Task.
            /// </summary>
            public Task<TranscriptionResult> TranscribeAsync(
                CapturedAudioData audioData,
                CancellationToken cancellationToken)
            {
                return Task.FromException<TranscriptionResult>(exception);
            }
        }
    }
}
