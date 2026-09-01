using System;
using System.Threading;
using System.Threading.Tasks;

namespace AiCharacterKit.Core
{
    /// <summary>
    /// Coordinates one conversation request at a time and routes its outcome to presentation.
    /// </summary>
    public sealed class NpcAIController : IDisposable
    {
        private readonly IAiConversationClient conversationClient;
        private readonly INpcPresentationDriver presentationDriver;
        private readonly object requestLock = new object();

        private CancellationTokenSource activeRequestCancellation;
        private bool isRequestInProgress;
        private bool isDisposed;

        public bool IsRequestInProgress
        {
            get
            {
                lock (requestLock)
                {
                    return isRequestInProgress;
                }
            }
        }

        public bool SupportsReset =>
            conversationClient is IResettableAiConversationClient;

        /// <summary>
        /// Creates a controller with explicit conversation and presentation dependencies.
        /// </summary>
        public NpcAIController(
            IAiConversationClient conversationClient,
            INpcPresentationDriver presentationDriver)
        {
            this.conversationClient = conversationClient
                ?? throw new ArgumentNullException(nameof(conversationClient));
            this.presentationDriver = presentationDriver
                ?? throw new ArgumentNullException(nameof(presentationDriver));
        }

        /// <summary>
        /// Processes one valid request, rejects duplicates, and reports every terminal outcome.
        /// </summary>
        public async Task<bool> SubmitAsync(
            AiNpcRequest request,
            CancellationToken cancellationToken)
        {
            if (request == null)
            {
                presentationDriver.PresentError("요청 정보가 없습니다.");
                return false;
            }

            if (string.IsNullOrWhiteSpace(request.UserText))
            {
                presentationDriver.PresentError("대화 내용을 입력해 주세요.");
                return false;
            }

            if (!TryBeginOperation(cancellationToken, out var requestCancellation))
            {
                presentationDriver.PresentError("이미 대화 요청을 처리하고 있습니다.");
                return false;
            }

            try
            {
                presentationDriver.SetBusy(true);
                var response = await conversationClient.SendAsync(
                    request,
                    requestCancellation.Token);

                requestCancellation.Token.ThrowIfCancellationRequested();

                if (response == null)
                {
                    throw new InvalidOperationException("Conversation client returned no response.");
                }

                presentationDriver.PresentDialogue(response.Dialogue);
                presentationDriver.PresentEmotion(response.Emotion);
                presentationDriver.PresentGesture(response.Gesture);
                return true;
            }
            catch (OperationCanceledException)
            {
                presentationDriver.PresentCancellation();
                return false;
            }
            catch (Exception exception)
            {
                presentationDriver.PresentError($"대화 요청에 실패했습니다: {exception.Message}");
                return false;
            }
            finally
            {
                CompleteRequest(requestCancellation);
                presentationDriver.SetBusy(false);
            }
        }

        /// <summary>
        /// Clears optional conversation memory while sharing the normal single-operation gate.
        /// </summary>
        public async Task<bool> ResetConversationAsync(
            CancellationToken cancellationToken)
        {
            if (!(conversationClient is IResettableAiConversationClient resettableClient))
            {
                presentationDriver.PresentError(
                    "현재 대화 클라이언트는 기억 초기화를 지원하지 않습니다.");
                return false;
            }

            if (!TryBeginOperation(cancellationToken, out var resetCancellation))
            {
                presentationDriver.PresentError("이미 대화 요청을 처리하고 있습니다.");
                return false;
            }

            try
            {
                presentationDriver.SetBusy(true);
                await resettableClient.ResetAsync(resetCancellation.Token);
                resetCancellation.Token.ThrowIfCancellationRequested();
                return true;
            }
            catch (OperationCanceledException)
            {
                presentationDriver.PresentCancellation();
                return false;
            }
            catch (Exception exception)
            {
                presentationDriver.PresentError(
                    $"대화 기억 초기화에 실패했습니다: {exception.Message}");
                return false;
            }
            finally
            {
                CompleteRequest(resetCancellation);
                presentationDriver.SetBusy(false);
            }
        }

        /// <summary>
        /// Cancels the currently active request without affecting later submissions.
        /// </summary>
        public void CancelActiveRequest()
        {
            CancellationTokenSource cancellation;
            lock (requestLock)
            {
                cancellation = activeRequestCancellation;
            }

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
                // The request completed between capturing and cancelling its token source.
            }
        }

        /// <summary>
        /// Cancels active work and prevents future submissions.
        /// </summary>
        public void Dispose()
        {
            CancellationTokenSource cancellation;
            lock (requestLock)
            {
                if (isDisposed)
                {
                    return;
                }

                isDisposed = true;
                cancellation = activeRequestCancellation;
            }

            if (cancellation != null)
            {
                try
                {
                    cancellation.Cancel();
                }
                catch (ObjectDisposedException)
                {
                    // The active request already completed and disposed its token source.
                }
            }
        }

        /// <summary>
        /// Clears active request state and disposes its linked cancellation source.
        /// </summary>
        private void CompleteRequest(CancellationTokenSource requestCancellation)
        {
            lock (requestLock)
            {
                if (ReferenceEquals(activeRequestCancellation, requestCancellation))
                {
                    activeRequestCancellation = null;
                    isRequestInProgress = false;
                }
            }

            requestCancellation.Dispose();
        }

        /// <summary>
        /// Reserves the shared send-or-reset gate and links caller cancellation.
        /// </summary>
        private bool TryBeginOperation(
            CancellationToken cancellationToken,
            out CancellationTokenSource operationCancellation)
        {
            lock (requestLock)
            {
                ThrowIfDisposed();
                if (isRequestInProgress)
                {
                    operationCancellation = null;
                    return false;
                }

                isRequestInProgress = true;
                operationCancellation =
                    CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                activeRequestCancellation = operationCancellation;
                return true;
            }
        }

        /// <summary>
        /// Rejects use after the controller has released its request resources.
        /// </summary>
        private void ThrowIfDisposed()
        {
            if (isDisposed)
            {
                throw new ObjectDisposedException(nameof(NpcAIController));
            }
        }
    }
}
