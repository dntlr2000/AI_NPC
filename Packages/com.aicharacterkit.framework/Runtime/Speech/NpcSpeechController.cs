using System;
using System.Threading;
using System.Threading.Tasks;

namespace AiCharacterKit.Speech
{
    /// <summary>
    /// Coordinates replaceable synthesis and playback without depending on Unity.
    /// </summary>
    public sealed class NpcSpeechController : IDisposable
    {
        private readonly ISpeechSynthesisClient synthesisClient;
        private readonly ISpeechPlaybackDriver playbackDriver;
        private readonly object stateLock = new object();

        private CancellationTokenSource activeCancellation;
        private long operationVersion;
        private NpcSpeechState state = NpcSpeechState.Idle;
        private bool isEnabled = true;
        private bool isDisposed;

        public event Action<NpcSpeechState, string> StateChanged;

        public NpcSpeechState State
        {
            get
            {
                lock (stateLock)
                {
                    return state;
                }
            }
        }

        public bool IsEnabled
        {
            get
            {
                lock (stateLock)
                {
                    return isEnabled;
                }
            }
        }

        /// <summary>
        /// Creates a controller with explicit synthesis and playback dependencies.
        /// </summary>
        public NpcSpeechController(
            ISpeechSynthesisClient synthesisClient,
            ISpeechPlaybackDriver playbackDriver)
        {
            this.synthesisClient = synthesisClient
                ?? throw new ArgumentNullException(nameof(synthesisClient));
            this.playbackDriver = playbackDriver
                ?? throw new ArgumentNullException(nameof(playbackDriver));
        }

        /// <summary>
        /// Cancels older work, synthesizes the latest request, and starts its playback.
        /// </summary>
        public async Task<bool> ReplaceAsync(
            SpeechSynthesisRequest request,
            CancellationToken cancellationToken)
        {
            if (request == null)
            {
                throw new ArgumentNullException(nameof(request));
            }

            CancellationTokenSource previousCancellation;
            CancellationTokenSource requestCancellation;
            long version;
            lock (stateLock)
            {
                ThrowIfDisposed();
                if (!isEnabled)
                {
                    return false;
                }

                previousCancellation = activeCancellation;
                requestCancellation =
                    CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                activeCancellation = requestCancellation;
                version = ++operationVersion;
                state = NpcSpeechState.Synthesizing;
            }

            CancelWithoutThrowing(previousCancellation);
            StopPlaybackWithoutThrowing();
            PublishState(NpcSpeechState.Synthesizing, string.Empty);

            try
            {
                var audioData = await synthesisClient.SynthesizeAsync(
                    request,
                    requestCancellation.Token);
                requestCancellation.Token.ThrowIfCancellationRequested();

                if (audioData == null)
                {
                    throw new InvalidOperationException(
                        "Speech synthesis returned no audio data.");
                }

                if (!TryPlayCurrent(version, requestCancellation, audioData))
                {
                    return false;
                }

                PublishState(NpcSpeechState.Playing, string.Empty);
                return true;
            }
            catch (OperationCanceledException)
            {
                if (TrySetCurrentState(
                        version,
                        requestCancellation,
                        NpcSpeechState.Idle,
                        clearCancellation: true))
                {
                    StopPlaybackWithoutThrowing();
                    PublishState(NpcSpeechState.Idle, string.Empty);
                }

                return false;
            }
            catch (Exception exception)
            {
                if (TrySetCurrentState(
                        version,
                        requestCancellation,
                        NpcSpeechState.Failed,
                        clearCancellation: true))
                {
                    StopPlaybackWithoutThrowing();
                    PublishState(
                        NpcSpeechState.Failed,
                        GetSafeFailureMessage(exception));
                }

                return false;
            }
            finally
            {
                requestCancellation.Dispose();
            }
        }

        /// <summary>
        /// Enables or disables future speech while stopping any active output when disabled.
        /// </summary>
        public void SetEnabled(bool enabled)
        {
            CancellationTokenSource cancellation = null;
            NpcSpeechState nextState;
            lock (stateLock)
            {
                ThrowIfDisposed();
                if (isEnabled == enabled)
                {
                    return;
                }

                isEnabled = enabled;
                operationVersion++;
                cancellation = activeCancellation;
                activeCancellation = null;
                nextState = enabled ? NpcSpeechState.Idle : NpcSpeechState.Disabled;
                state = nextState;
            }

            CancelWithoutThrowing(cancellation);
            StopPlaybackWithoutThrowing();
            PublishState(nextState, string.Empty);
        }

        /// <summary>
        /// Cancels active synthesis and stops playback while retaining the enabled setting.
        /// </summary>
        public void Stop()
        {
            CancellationTokenSource cancellation;
            NpcSpeechState nextState;
            lock (stateLock)
            {
                ThrowIfDisposed();
                operationVersion++;
                cancellation = activeCancellation;
                activeCancellation = null;
                nextState = isEnabled
                    ? NpcSpeechState.Idle
                    : NpcSpeechState.Disabled;
                state = nextState;
            }

            CancelWithoutThrowing(cancellation);
            StopPlaybackWithoutThrowing();
            PublishState(nextState, string.Empty);
        }

        /// <summary>
        /// Marks natural playback completion without affecting a newer operation.
        /// </summary>
        public void NotifyPlaybackCompleted()
        {
            lock (stateLock)
            {
                ThrowIfDisposed();
                if (state != NpcSpeechState.Playing)
                {
                    return;
                }

                state = isEnabled ? NpcSpeechState.Idle : NpcSpeechState.Disabled;
            }

            PublishState(State, string.Empty);
        }

        /// <summary>
        /// Cancels work, stops playback, and prevents later controller use.
        /// </summary>
        public void Dispose()
        {
            CancellationTokenSource cancellation;
            lock (stateLock)
            {
                if (isDisposed)
                {
                    return;
                }

                isDisposed = true;
                operationVersion++;
                cancellation = activeCancellation;
                activeCancellation = null;
                state = NpcSpeechState.Disabled;
            }

            CancelWithoutThrowing(cancellation);
            StopPlaybackWithoutThrowing();
            StateChanged = null;
        }

        /// <summary>
        /// Starts playback and changes state atomically against replacement or disable.
        /// </summary>
        private bool TryPlayCurrent(
            long version,
            CancellationTokenSource cancellation,
            SpeechAudioData audioData)
        {
            lock (stateLock)
            {
                if (isDisposed
                    || operationVersion != version
                    || !ReferenceEquals(activeCancellation, cancellation))
                {
                    return false;
                }

                playbackDriver.Play(audioData);
                state = NpcSpeechState.Playing;
                activeCancellation = null;
                return true;
            }
        }

        /// <summary>
        /// Updates state only when the completing operation is still current.
        /// </summary>
        private bool TrySetCurrentState(
            long version,
            CancellationTokenSource cancellation,
            NpcSpeechState nextState,
            bool clearCancellation)
        {
            lock (stateLock)
            {
                if (isDisposed
                    || operationVersion != version
                    || !ReferenceEquals(activeCancellation, cancellation))
                {
                    return false;
                }

                state = nextState;
                if (clearCancellation)
                {
                    activeCancellation = null;
                }

                return true;
            }
        }

        /// <summary>
        /// Delivers optional state notifications without allowing UI callbacks to break speech.
        /// </summary>
        private void PublishState(NpcSpeechState nextState, string message)
        {
            var handlers = StateChanged;
            if (handlers == null)
            {
                return;
            }

            foreach (Action<NpcSpeechState, string> handler
                     in handlers.GetInvocationList())
            {
                try
                {
                    handler(nextState, message ?? string.Empty);
                }
                catch
                {
                    // Optional status consumers must not change speech or dialogue outcomes.
                }
            }
        }

        /// <summary>
        /// Cancels a superseded token source without racing its owning operation.
        /// </summary>
        private static void CancelWithoutThrowing(
            CancellationTokenSource cancellation)
        {
            if (cancellation == null)
            {
                return;
            }

            try
            {
                cancellation.Cancel();
            }
            catch (ObjectDisposedException)
            {
                // The owning operation completed while cancellation was requested.
            }
        }

        /// <summary>
        /// Prevents an engine playback failure from escaping cancellation or cleanup paths.
        /// </summary>
        private void StopPlaybackWithoutThrowing()
        {
            try
            {
                playbackDriver.Stop();
            }
            catch
            {
                // Cleanup remains best-effort after an engine-level playback failure.
            }
        }

        /// <summary>
        /// Selects a safe public failure message without exposing provider diagnostics.
        /// </summary>
        private static string GetSafeFailureMessage(Exception exception)
        {
            return exception is SpeechSynthesisException speechException
                ? speechException.Message
                : "음성 출력을 완료하지 못했습니다.";
        }

        /// <summary>
        /// Rejects work after controller resources have been released.
        /// </summary>
        private void ThrowIfDisposed()
        {
            if (isDisposed)
            {
                throw new ObjectDisposedException(nameof(NpcSpeechController));
            }
        }
    }
}
