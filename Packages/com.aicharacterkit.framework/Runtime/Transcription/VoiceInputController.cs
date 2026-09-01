using System;
using System.Threading;
using System.Threading.Tasks;

namespace AiCharacterKit.Transcription
{
    /// <summary>
    /// Coordinates one exclusive capture and transcription operation without Unity dependencies.
    /// </summary>
    public sealed class VoiceInputController : IDisposable
    {
        private readonly IAudioCaptureDriver captureDriver;
        private readonly ITranscriptionClient transcriptionClient;
        private readonly object stateLock = new object();

        private CancellationTokenSource activeCancellation;
        private VoiceInputState state = VoiceInputState.Idle;
        private long operationVersion;
        private bool isDisposed;

        public event Action<VoiceInputState, string> StateChanged;

        public VoiceInputState State
        {
            get
            {
                lock (stateLock)
                {
                    return state;
                }
            }
        }

        /// <summary>
        /// Creates a controller with explicit capture and transcription boundaries.
        /// </summary>
        public VoiceInputController(
            IAudioCaptureDriver captureDriver,
            ITranscriptionClient transcriptionClient)
        {
            this.captureDriver = captureDriver
                ?? throw new ArgumentNullException(nameof(captureDriver));
            this.transcriptionClient = transcriptionClient
                ?? throw new ArgumentNullException(nameof(transcriptionClient));
        }

        /// <summary>
        /// Starts recording only when no capture or transcription operation is active.
        /// </summary>
        public bool StartRecording()
        {
            lock (stateLock)
            {
                ThrowIfDisposed();
                if (state == VoiceInputState.Recording
                    || state == VoiceInputState.Transcribing)
                {
                    return false;
                }

                state = VoiceInputState.Recording;
                operationVersion++;
            }

            try
            {
                captureDriver.StartCapture();
                PublishState(VoiceInputState.Recording, string.Empty);
                return true;
            }
            catch (Exception exception)
            {
                SetFailed(GetSafeFailureMessage(exception));
                return false;
            }
        }

        /// <summary>
        /// Stops the active recording and returns its transcript without auto-submitting text.
        /// </summary>
        public async Task<TranscriptionResult> StopAndTranscribeAsync(
            CancellationToken cancellationToken)
        {
            CancellationTokenSource operationCancellation;
            long version;
            lock (stateLock)
            {
                ThrowIfDisposed();
                if (state != VoiceInputState.Recording)
                {
                    return null;
                }

                operationCancellation =
                    CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                activeCancellation = operationCancellation;
                state = VoiceInputState.Transcribing;
                version = ++operationVersion;
            }

            CapturedAudioData audioData;
            try
            {
                audioData = captureDriver.StopCapture();
                PublishState(VoiceInputState.Transcribing, string.Empty);
            }
            catch (Exception exception)
            {
                operationCancellation.Dispose();
                if (TrySetCurrentState(
                        version,
                        operationCancellation,
                        VoiceInputState.Failed,
                        clearCancellation: true))
                {
                    PublishState(
                        VoiceInputState.Failed,
                        GetSafeFailureMessage(exception));
                }

                return null;
            }

            try
            {
                var result = await transcriptionClient.TranscribeAsync(
                    audioData,
                    operationCancellation.Token);
                operationCancellation.Token.ThrowIfCancellationRequested();
                if (result == null)
                {
                    throw new InvalidOperationException(
                        "Transcription client returned no result.");
                }

                if (TrySetCurrentState(
                        version,
                        operationCancellation,
                        VoiceInputState.Idle,
                        clearCancellation: true))
                {
                    PublishState(VoiceInputState.Idle, string.Empty);
                    return result;
                }

                return null;
            }
            catch (OperationCanceledException)
            {
                if (TrySetCurrentState(
                        version,
                        operationCancellation,
                        VoiceInputState.Idle,
                        clearCancellation: true))
                {
                    PublishState(VoiceInputState.Idle, string.Empty);
                }

                return null;
            }
            catch (Exception exception)
            {
                if (TrySetCurrentState(
                        version,
                        operationCancellation,
                        VoiceInputState.Failed,
                        clearCancellation: true))
                {
                    PublishState(
                        VoiceInputState.Failed,
                        GetSafeFailureMessage(exception));
                }

                return null;
            }
            finally
            {
                operationCancellation.Dispose();
            }
        }

        /// <summary>
        /// Cancels recording or transcription and returns the controller to idle.
        /// </summary>
        public void Cancel()
        {
            CancellationTokenSource cancellation;
            lock (stateLock)
            {
                ThrowIfDisposed();
                operationVersion++;
                cancellation = activeCancellation;
                activeCancellation = null;
                state = VoiceInputState.Idle;
            }

            CancelWithoutThrowing(cancellation);
            CancelCaptureWithoutThrowing();
            PublishState(VoiceInputState.Idle, string.Empty);
        }

        /// <summary>
        /// Cancels active resources and prevents later controller use.
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
                state = VoiceInputState.Idle;
            }

            CancelWithoutThrowing(cancellation);
            CancelCaptureWithoutThrowing();
            StateChanged = null;
        }

        /// <summary>
        /// Changes one current operation state without allowing stale completion to win.
        /// </summary>
        private bool TrySetCurrentState(
            long version,
            CancellationTokenSource cancellation,
            VoiceInputState nextState,
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
        /// Stores a synchronous capture failure and publishes only a safe message.
        /// </summary>
        private void SetFailed(string message)
        {
            lock (stateLock)
            {
                if (isDisposed)
                {
                    return;
                }

                state = VoiceInputState.Failed;
            }

            CancelCaptureWithoutThrowing();
            PublishState(VoiceInputState.Failed, message);
        }

        /// <summary>
        /// Delivers optional state notifications without allowing consumers to break input.
        /// </summary>
        private void PublishState(VoiceInputState nextState, string message)
        {
            var handlers = StateChanged;
            if (handlers == null)
            {
                return;
            }

            foreach (Action<VoiceInputState, string> handler
                     in handlers.GetInvocationList())
            {
                try
                {
                    handler(nextState, message ?? string.Empty);
                }
                catch
                {
                    // Optional status consumers must not change input outcomes.
                }
            }
        }

        /// <summary>
        /// Cancels a token source without racing its owning asynchronous operation.
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
        /// Releases an engine capture operation on every cancellation path.
        /// </summary>
        private void CancelCaptureWithoutThrowing()
        {
            try
            {
                captureDriver.CancelCapture();
            }
            catch
            {
                // Engine cleanup remains best-effort after capture failure.
            }
        }

        /// <summary>
        /// Selects a safe public message without exposing provider diagnostics.
        /// </summary>
        private static string GetSafeFailureMessage(Exception exception)
        {
            return exception is TranscriptionException transcriptionException
                ? transcriptionException.Message
                : "음성 입력을 완료하지 못했습니다.";
        }

        /// <summary>
        /// Rejects work after controller resources have been released.
        /// </summary>
        private void ThrowIfDisposed()
        {
            if (isDisposed)
            {
                throw new ObjectDisposedException(nameof(VoiceInputController));
            }
        }
    }
}
