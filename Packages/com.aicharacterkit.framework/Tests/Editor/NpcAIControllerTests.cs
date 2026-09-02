using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;

namespace AiCharacterKit.Core.Tests
{
    /// <summary>
    /// Verifies request gating and every terminal controller outcome.
    /// </summary>
    public sealed class NpcAIControllerTests
    {
        /// <summary>
        /// Confirms that successful requests set busy state and route all response fields.
        /// </summary>
        [Test]
        public async Task SubmitAsync_Success_PresentsResponseAndBusyTransitions()
        {
            var presentation = new RecordingPresentationDriver();
            var controller = new NpcAIController(
                new MockConversationClient(TimeSpan.Zero),
                presentation);

            var succeeded = await controller.SubmitAsync(
                CreateRequest("안녕"),
                CancellationToken.None);

            Assert.That(succeeded, Is.True);
            Assert.That(presentation.BusyStates, Is.EqualTo(new[] { true, false }));
            Assert.That(presentation.Dialogue, Does.Contain("Mina"));
            Assert.That(presentation.Emotion, Is.EqualTo(NpcEmotion.Happy));
            Assert.That(presentation.Gesture, Is.EqualTo(NpcGesture.Wave));
            Assert.That(controller.IsRequestInProgress, Is.False);

            controller.Dispose();
        }

        /// <summary>
        /// Confirms that a second submission cannot invoke the client while one is active.
        /// </summary>
        [Test]
        public async Task SubmitAsync_DuplicateRequest_InvokesClientOnlyOnce()
        {
            var client = new BlockingConversationClient();
            var presentation = new RecordingPresentationDriver();
            var controller = new NpcAIController(client, presentation);

            var firstSubmission = controller.SubmitAsync(
                CreateRequest("첫 번째"),
                CancellationToken.None);
            var duplicateSucceeded = await controller.SubmitAsync(
                CreateRequest("두 번째"),
                CancellationToken.None);

            Assert.That(duplicateSucceeded, Is.False);
            Assert.That(client.CallCount, Is.EqualTo(1));
            Assert.That(presentation.Error, Does.Contain("이미"));

            client.Complete(new AiNpcResponse(
                "완료",
                NpcEmotion.Neutral,
                NpcGesture.None));

            Assert.That(await firstSubmission, Is.True);
            controller.Dispose();
        }

        /// <summary>
        /// Confirms that cancellation is reported separately and always clears busy state.
        /// </summary>
        [Test]
        public async Task CancelActiveRequest_InFlight_PresentsCancellationAndClearsBusy()
        {
            var client = new BlockingConversationClient();
            var presentation = new RecordingPresentationDriver();
            var controller = new NpcAIController(client, presentation);

            var submission = controller.SubmitAsync(
                CreateRequest("기다려"),
                CancellationToken.None);
            controller.CancelActiveRequest();

            Assert.That(await submission, Is.False);
            Assert.That(presentation.WasCancelled, Is.True);
            Assert.That(presentation.Error, Is.Null);
            Assert.That(presentation.BusyStates, Is.EqualTo(new[] { true, false }));
            Assert.That(controller.IsRequestInProgress, Is.False);

            controller.Dispose();
        }

        /// <summary>
        /// Confirms that client failures are converted to presentation errors and unlock the gate.
        /// </summary>
        [Test]
        public async Task SubmitAsync_ClientFailure_PresentsErrorAndClearsBusy()
        {
            var presentation = new RecordingPresentationDriver();
            var controller = new NpcAIController(
                new FailingConversationClient(),
                presentation);

            var succeeded = await controller.SubmitAsync(
                CreateRequest("실패"),
                CancellationToken.None);

            Assert.That(succeeded, Is.False);
            Assert.That(presentation.Error, Does.Contain("대화 요청에 실패"));
            Assert.That(presentation.BusyStates, Is.EqualTo(new[] { true, false }));
            Assert.That(controller.IsRequestInProgress, Is.False);

            controller.Dispose();
        }

        /// <summary>
        /// Confirms optional behavior failures cannot replace an already presented dialogue success.
        /// </summary>
        [Test]
        public async Task SubmitAsync_TurnObserverFailure_KeepsDialogueSuccessful()
        {
            var presentation = new RecordingPresentationDriver();
            var observer = new ThrowingTurnObserver();
            var controller = new NpcAIController(
                new MockConversationClient(TimeSpan.Zero),
                presentation,
                observer);

            var succeeded = await controller.SubmitAsync(
                CreateRequest("안녕"),
                CancellationToken.None);

            Assert.That(succeeded, Is.True);
            Assert.That(observer.CallCount, Is.EqualTo(1));
            Assert.That(presentation.Dialogue, Is.Not.Empty);
            Assert.That(presentation.Error, Is.Null);
            Assert.That(presentation.WasCancelled, Is.False);
            controller.Dispose();
        }

        /// <summary>
        /// Confirms that a supported reset shares busy presentation and completes successfully.
        /// </summary>
        [Test]
        public async Task ResetConversationAsync_SupportedClient_UsesSharedBusyGate()
        {
            var client = new RecordingResettableConversationClient();
            var presentation = new RecordingPresentationDriver();
            var controller = new NpcAIController(client, presentation);

            var succeeded = await controller.ResetConversationAsync(
                CancellationToken.None);

            Assert.That(controller.SupportsReset, Is.True);
            Assert.That(succeeded, Is.True);
            Assert.That(client.ResetCount, Is.EqualTo(1));
            Assert.That(presentation.BusyStates, Is.EqualTo(new[] { true, false }));
            Assert.That(controller.IsRequestInProgress, Is.False);
            controller.Dispose();
        }

        /// <summary>
        /// Confirms that stateless clients reject reset without entering busy state.
        /// </summary>
        [Test]
        public async Task ResetConversationAsync_UnsupportedClient_ReturnsFalse()
        {
            var presentation = new RecordingPresentationDriver();
            var controller = new NpcAIController(
                new MockConversationClient(TimeSpan.Zero),
                presentation);

            var succeeded = await controller.ResetConversationAsync(
                CancellationToken.None);

            Assert.That(controller.SupportsReset, Is.False);
            Assert.That(succeeded, Is.False);
            Assert.That(presentation.Error, Does.Contain("지원하지 않습니다"));
            Assert.That(presentation.BusyStates, Is.Empty);
            controller.Dispose();
        }

        /// <summary>
        /// Confirms that reset cannot overlap a pending submission on the same controller.
        /// </summary>
        [Test]
        public async Task ResetConversationAsync_WhileSubmissionActive_IsRejected()
        {
            var client = new BlockingResettableConversationClient();
            var presentation = new RecordingPresentationDriver();
            var controller = new NpcAIController(client, presentation);
            var submission = controller.SubmitAsync(
                CreateRequest("대기"),
                CancellationToken.None);

            var resetSucceeded = await controller.ResetConversationAsync(
                CancellationToken.None);

            Assert.That(resetSucceeded, Is.False);
            Assert.That(client.ResetCount, Is.Zero);
            Assert.That(presentation.Error, Does.Contain("이미"));
            client.Complete(new AiNpcResponse(
                "완료",
                NpcEmotion.Neutral,
                NpcGesture.None));
            Assert.That(await submission, Is.True);
            controller.Dispose();
        }

        /// <summary>
        /// Confirms that reset failures are presented safely and release the shared gate.
        /// </summary>
        [Test]
        public async Task ResetConversationAsync_ClientFailure_PresentsErrorAndUnlocks()
        {
            var client = new RecordingResettableConversationClient(
                new InvalidOperationException("reset failure"));
            var presentation = new RecordingPresentationDriver();
            var controller = new NpcAIController(client, presentation);

            var succeeded = await controller.ResetConversationAsync(
                CancellationToken.None);

            Assert.That(succeeded, Is.False);
            Assert.That(presentation.Error, Does.Contain("기억 초기화에 실패"));
            Assert.That(presentation.BusyStates, Is.EqualTo(new[] { true, false }));
            Assert.That(controller.IsRequestInProgress, Is.False);
            controller.Dispose();
        }

        /// <summary>
        /// Creates a valid request used by controller behavior tests.
        /// </summary>
        private static AiNpcRequest CreateRequest(string userText)
        {
            return new AiNpcRequest(
                "npc-01",
                "Mina",
                "Friendly",
                "Polite",
                "반가워요.",
                NpcEmotion.Neutral,
                userText);
        }

        /// <summary>
        /// Records presentation calls for controller assertions.
        /// </summary>
        private sealed class RecordingPresentationDriver : INpcPresentationDriver
        {
            public List<bool> BusyStates { get; } = new List<bool>();

            public string Dialogue { get; private set; }

            public NpcEmotion Emotion { get; private set; }

            public NpcGesture Gesture { get; private set; }

            public string Error { get; private set; }

            public bool WasCancelled { get; private set; }

            /// <summary>
            /// Records each busy transition.
            /// </summary>
            public void SetBusy(bool isBusy)
            {
                BusyStates.Add(isBusy);
            }

            /// <summary>
            /// Records the latest dialogue.
            /// </summary>
            public void PresentDialogue(string dialogue)
            {
                Dialogue = dialogue;
            }

            /// <summary>
            /// Records the latest emotion.
            /// </summary>
            public void PresentEmotion(NpcEmotion emotion)
            {
                Emotion = emotion;
            }

            /// <summary>
            /// Records the latest gesture.
            /// </summary>
            public void PresentGesture(NpcGesture gesture)
            {
                Gesture = gesture;
            }

            /// <summary>
            /// Records the latest recoverable error.
            /// </summary>
            public void PresentError(string message)
            {
                Error = message;
            }

            /// <summary>
            /// Records a cancellation outcome.
            /// </summary>
            public void PresentCancellation()
            {
                WasCancelled = true;
            }
        }

        /// <summary>
        /// Holds a request open so duplicate and cancellation behavior can be observed.
        /// </summary>
        private sealed class BlockingConversationClient : IAiConversationClient
        {
            private readonly TaskCompletionSource<AiNpcResponse> completion =
                new TaskCompletionSource<AiNpcResponse>(
                    TaskCreationOptions.RunContinuationsAsynchronously);

            public int CallCount { get; private set; }

            /// <summary>
            /// Returns the shared pending task and links it to caller cancellation.
            /// </summary>
            public Task<AiNpcResponse> SendAsync(
                AiNpcRequest request,
                CancellationToken cancellationToken)
            {
                CallCount++;
                cancellationToken.Register(
                    () => completion.TrySetCanceled(cancellationToken));
                return completion.Task;
            }

            /// <summary>
            /// Completes the pending fake request with a selected response.
            /// </summary>
            public void Complete(AiNpcResponse response)
            {
                completion.TrySetResult(response);
            }
        }

        /// <summary>
        /// Produces a stable exception for controller failure handling tests.
        /// </summary>
        private sealed class FailingConversationClient : IAiConversationClient
        {
            /// <summary>
            /// Returns a failed task without performing external work.
            /// </summary>
            public Task<AiNpcResponse> SendAsync(
                AiNpcRequest request,
                CancellationToken cancellationToken)
            {
                return Task.FromException<AiNpcResponse>(
                    new InvalidOperationException("mock failure"));
            }
        }

        /// <summary>
        /// Throws after a successful presentation to verify behavior isolation.
        /// </summary>
        private sealed class ThrowingTurnObserver : INpcTurnObserver
        {
            public int CallCount { get; private set; }

            /// <summary>
            /// Records one observation and returns a controlled failure.
            /// </summary>
            public Task ObserveAsync(
                NpcTurnContext context,
                CancellationToken cancellationToken)
            {
                CallCount++;
                return Task.FromException(
                    new InvalidOperationException("consumer observer failure"));
            }
        }

        /// <summary>
        /// Provides completed sends and configurable reset outcomes.
        /// </summary>
        private sealed class RecordingResettableConversationClient
            : IResettableAiConversationClient
        {
            private readonly Exception resetFailure;

            public int ResetCount { get; private set; }

            /// <summary>
            /// Captures an optional deterministic reset failure.
            /// </summary>
            public RecordingResettableConversationClient(
                Exception resetFailure = null)
            {
                this.resetFailure = resetFailure;
            }

            /// <summary>
            /// Returns one fixed response without external work.
            /// </summary>
            public Task<AiNpcResponse> SendAsync(
                AiNpcRequest request,
                CancellationToken cancellationToken)
            {
                return Task.FromResult(new AiNpcResponse(
                    "완료",
                    NpcEmotion.Neutral,
                    NpcGesture.None));
            }

            /// <summary>
            /// Records reset and returns the configured success or failure.
            /// </summary>
            public Task ResetAsync(CancellationToken cancellationToken)
            {
                ResetCount++;
                return resetFailure == null
                    ? Task.CompletedTask
                    : Task.FromException(resetFailure);
            }
        }

        /// <summary>
        /// Holds one send open while exposing a separately counted reset operation.
        /// </summary>
        private sealed class BlockingResettableConversationClient
            : IResettableAiConversationClient
        {
            private readonly TaskCompletionSource<AiNpcResponse> completion =
                new TaskCompletionSource<AiNpcResponse>(
                    TaskCreationOptions.RunContinuationsAsynchronously);

            public int ResetCount { get; private set; }

            /// <summary>
            /// Returns the pending task until the test completes it.
            /// </summary>
            public Task<AiNpcResponse> SendAsync(
                AiNpcRequest request,
                CancellationToken cancellationToken)
            {
                cancellationToken.Register(
                    () => completion.TrySetCanceled(cancellationToken));
                return completion.Task;
            }

            /// <summary>
            /// Records an unexpected reset call.
            /// </summary>
            public Task ResetAsync(CancellationToken cancellationToken)
            {
                ResetCount++;
                return Task.CompletedTask;
            }

            /// <summary>
            /// Completes the pending fake send with a selected response.
            /// </summary>
            public void Complete(AiNpcResponse response)
            {
                completion.TrySetResult(response);
            }
        }
    }
}
