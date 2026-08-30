using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using AiCharacterKit.Speech;
using NUnit.Framework;

namespace AiCharacterKit.Core.Tests
{
    /// <summary>
    /// Verifies replace, cancellation, failure, and enable state in the pure speech controller.
    /// </summary>
    public sealed class NpcSpeechControllerTests
    {
        /// <summary>
        /// Plays a successful result and reports natural completion as idle.
        /// </summary>
        [Test]
        public async Task ReplaceAsync_Success_PlaysAndCompletes()
        {
            var playback = new RecordingPlaybackDriver();
            var client = new ImmediateClient(
                new SpeechAudioData(new byte[] { 0, 0 }));
            using (var controller = new NpcSpeechController(client, playback))
            {
                var succeeded = await controller.ReplaceAsync(
                    new SpeechSynthesisRequest("warm-friendly", "안녕"),
                    CancellationToken.None);

                Assert.That(succeeded, Is.True);
                Assert.That(controller.State, Is.EqualTo(NpcSpeechState.Playing));
                Assert.That(playback.Played, Has.Count.EqualTo(1));

                controller.NotifyPlaybackCompleted();
                Assert.That(controller.State, Is.EqualTo(NpcSpeechState.Idle));
            }
        }

        /// <summary>
        /// Cancels an older synthesis and allows only the newest result to play.
        /// </summary>
        [Test]
        public async Task ReplaceAsync_SecondRequest_SuppressesStalePlayback()
        {
            var client = new QueuedClient();
            var playback = new RecordingPlaybackDriver();
            using (var controller = new NpcSpeechController(client, playback))
            {
                var first = controller.ReplaceAsync(
                    new SpeechSynthesisRequest("warm-friendly", "첫 번째"),
                    CancellationToken.None);
                var second = controller.ReplaceAsync(
                    new SpeechSynthesisRequest("warm-friendly", "두 번째"),
                    CancellationToken.None);

                client.Complete(1, new byte[] { 1, 0 });
                Assert.That(await second, Is.True);
                Assert.That(await first, Is.False);
                Assert.That(playback.Played, Has.Count.EqualTo(1));
                Assert.That(playback.Played[0].PcmBytes[0], Is.EqualTo(1));
            }
        }

        /// <summary>
        /// Disabling cancels work, stops audio, and rejects synthesis until re-enabled.
        /// </summary>
        [Test]
        public async Task SetEnabled_DisableThenEnable_ControlsOptionalSpeech()
        {
            var client = new ImmediateClient(
                new SpeechAudioData(new byte[] { 0, 0 }));
            var playback = new RecordingPlaybackDriver();
            using (var controller = new NpcSpeechController(client, playback))
            {
                controller.SetEnabled(false);
                Assert.That(controller.State, Is.EqualTo(NpcSpeechState.Disabled));
                Assert.That(
                    await controller.ReplaceAsync(
                        new SpeechSynthesisRequest("warm-friendly", "무시"),
                        CancellationToken.None),
                    Is.False);
                Assert.That(client.CallCount, Is.Zero);

                controller.SetEnabled(true);
                Assert.That(
                    await controller.ReplaceAsync(
                        new SpeechSynthesisRequest("warm-friendly", "재생"),
                        CancellationToken.None),
                    Is.True);
                Assert.That(client.CallCount, Is.EqualTo(1));
            }
        }

        /// <summary>
        /// Converts provider and unexpected failures into safe Failed state without throwing.
        /// </summary>
        [Test]
        public async Task ReplaceAsync_Failure_ReturnsFalseAndPublishesSafeMessage()
        {
            var playback = new RecordingPlaybackDriver();
            var client = new ThrowingClient(
                new SpeechSynthesisException(
                    "upstream_timeout",
                    "음성 생성 시간이 초과되었습니다.",
                    true));
            var stateMessage = string.Empty;
            using (var controller = new NpcSpeechController(client, playback))
            {
                controller.StateChanged +=
                    (state, message) =>
                    {
                        if (state == NpcSpeechState.Failed)
                        {
                            stateMessage = message;
                        }
                    };

                var succeeded = await controller.ReplaceAsync(
                    new SpeechSynthesisRequest("warm-friendly", "안녕"),
                    CancellationToken.None);

                Assert.That(succeeded, Is.False);
                Assert.That(controller.State, Is.EqualTo(NpcSpeechState.Failed));
                Assert.That(stateMessage, Does.Contain("초과"));
                Assert.That(playback.Played, Is.Empty);
            }
        }

        /// <summary>
        /// Returns complete audio immediately for deterministic controller tests.
        /// </summary>
        private sealed class ImmediateClient : ISpeechSynthesisClient
        {
            private readonly SpeechAudioData audio;

            public int CallCount { get; private set; }

            /// <summary>
            /// Captures the audio returned by every synthesis call.
            /// </summary>
            public ImmediateClient(SpeechAudioData audio)
            {
                this.audio = audio;
            }

            /// <summary>
            /// Returns the configured audio after honoring caller cancellation.
            /// </summary>
            public Task<SpeechAudioData> SynthesizeAsync(
                SpeechSynthesisRequest request,
                CancellationToken cancellationToken)
            {
                cancellationToken.ThrowIfCancellationRequested();
                CallCount++;
                return Task.FromResult(audio);
            }
        }

        /// <summary>
        /// Holds independently completable synthesis calls and observes cancellation.
        /// </summary>
        private sealed class QueuedClient : ISpeechSynthesisClient
        {
            private readonly List<TaskCompletionSource<SpeechAudioData>> calls =
                new List<TaskCompletionSource<SpeechAudioData>>();

            /// <summary>
            /// Enqueues one completion source tied to the supplied cancellation token.
            /// </summary>
            public Task<SpeechAudioData> SynthesizeAsync(
                SpeechSynthesisRequest request,
                CancellationToken cancellationToken)
            {
                var completion = new TaskCompletionSource<SpeechAudioData>();
                cancellationToken.Register(() => completion.TrySetCanceled());
                calls.Add(completion);
                return completion.Task;
            }

            /// <summary>
            /// Completes the selected synthesis call with valid PCM data.
            /// </summary>
            public void Complete(int index, byte[] bytes)
            {
                calls[index].TrySetResult(new SpeechAudioData(bytes));
            }
        }

        /// <summary>
        /// Throws one configured safe speech failure for fallback tests.
        /// </summary>
        private sealed class ThrowingClient : ISpeechSynthesisClient
        {
            private readonly Exception exception;

            /// <summary>
            /// Captures the failure returned by every synthesis call.
            /// </summary>
            public ThrowingClient(Exception exception)
            {
                this.exception = exception;
            }

            /// <summary>
            /// Returns a faulted Task containing the configured failure.
            /// </summary>
            public Task<SpeechAudioData> SynthesizeAsync(
                SpeechSynthesisRequest request,
                CancellationToken cancellationToken)
            {
                return Task.FromException<SpeechAudioData>(exception);
            }
        }

        /// <summary>
        /// Records playback replacements without depending on Unity audio.
        /// </summary>
        private sealed class RecordingPlaybackDriver : ISpeechPlaybackDriver
        {
            public List<SpeechAudioData> Played { get; } =
                new List<SpeechAudioData>();

            public int StopCount { get; private set; }

            /// <summary>
            /// Records one complete audio payload.
            /// </summary>
            public void Play(SpeechAudioData audioData)
            {
                Played.Add(audioData);
            }

            /// <summary>
            /// Records one cancellation or replacement stop.
            /// </summary>
            public void Stop()
            {
                StopCount++;
            }
        }
    }
}
