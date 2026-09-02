using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;

namespace AiCharacterKit.Core.Tests
{
    /// <summary>
    /// Verifies deterministic trigger matching, routing, authorization, and failure isolation.
    /// </summary>
    public sealed class NpcActionPipelineTests
    {
        /// <summary>
        /// Confirms Mock matching normalizes case and whitespace but does not infer semantics.
        /// </summary>
        [Test]
        public async Task MockConversationClient_ExampleMatch_IsExactAndDeterministic()
        {
            var definitions = new[]
            {
                CreateDefinition("greet_player", "wave_to_player", 1, "  Hello   there ")
            };
            var client = new MockConversationClient(TimeSpan.Zero, definitions);

            var matched = await client.SendAsync(
                CreateRequest("HELLO there"),
                CancellationToken.None);
            var unmatched = await client.SendAsync(
                CreateRequest("hello friend"),
                CancellationToken.None);

            Assert.That(matched.MatchedTriggerIds, Is.EqualTo(new[] { "greet_player" }));
            Assert.That(unmatched.MatchedTriggerIds, Is.Empty);
        }

        /// <summary>
        /// Confirms the highest priority wins and declaration order breaks equal-priority ties.
        /// </summary>
        [Test]
        public async Task RouteAsync_MultipleMatches_SelectsPriorityThenDeclarationOrder()
        {
            var first = CreateDefinition("first_trigger", "first_action", 10, "one");
            var second = CreateDefinition("second_trigger", "second_action", 10, "two");
            var lower = CreateDefinition("lower_trigger", "lower_action", 1, "three");
            var handlers = new[]
            {
                new RecordingHandler("first_action"),
                new RecordingHandler("second_action"),
                new RecordingHandler("lower_action")
            };
            var router = new NpcActionRouter(new[] { first, second, lower }, handlers);

            var result = await router.RouteAsync(
                CreateTurn("lower_trigger", "second_trigger", "first_trigger"),
                CancellationToken.None);

            Assert.That(result.Status, Is.EqualTo(NpcActionStatus.Succeeded));
            Assert.That(result.TriggerId, Is.EqualTo("first_trigger"));
            Assert.That(handlers[0].ExecutionCount, Is.EqualTo(1));
            Assert.That(handlers[1].ExecutionCount, Is.Zero);
            Assert.That(handlers[2].ExecutionCount, Is.Zero);
        }

        /// <summary>
        /// Confirms any unknown model-returned ID rejects the whole routing attempt.
        /// </summary>
        [Test]
        public async Task RouteAsync_UnknownTrigger_RejectsWithoutExecutingKnownMatch()
        {
            var definition = CreateDefinition(
                "known_trigger",
                "known_action",
                1,
                "hello");
            var handler = new RecordingHandler("known_action");
            var router = new NpcActionRouter(new[] { definition }, new[] { handler });

            var result = await router.RouteAsync(
                CreateTurn("known_trigger", "invented_trigger"),
                CancellationToken.None);

            Assert.That(result.Status, Is.EqualTo(NpcActionStatus.UnknownTriggerRejected));
            Assert.That(handler.ExecutionCount, Is.Zero);
        }

        /// <summary>
        /// Confirms duplicate model-returned IDs are rejected before consumer execution.
        /// </summary>
        [Test]
        public async Task RouteAsync_DuplicateTrigger_RejectsWithoutExecution()
        {
            var definition = CreateDefinition(
                "known_trigger",
                "known_action",
                1,
                "hello");
            var handler = new RecordingHandler("known_action");
            var router = new NpcActionRouter(new[] { definition }, new[] { handler });

            var result = await router.RouteAsync(
                CreateTurn("known_trigger", "known_trigger"),
                CancellationToken.None);

            Assert.That(result.Status, Is.EqualTo(NpcActionStatus.UnknownTriggerRejected));
            Assert.That(handler.ExecutionCount, Is.Zero);
        }

        /// <summary>
        /// Confirms consumer CanExecute rejection remains separate from conversation success.
        /// </summary>
        [Test]
        public async Task SubmitAsync_ActionRejected_KeepsPresentedDialogueSuccessful()
        {
            var definition = CreateDefinition(
                "open_gate",
                "open_gate_action",
                1,
                "open");
            var handler = new RecordingHandler("open_gate_action")
            {
                Allowed = false
            };
            var router = new NpcActionRouter(new[] { definition }, new[] { handler });
            var presentation = new RecordingPresentation();
            var client = new FixedClient(new AiNpcResponse(
                "The gate stays closed.",
                NpcEmotion.Neutral,
                NpcGesture.None,
                new[] { "open_gate" }));
            var controller = new NpcAIController(client, presentation, router);

            var succeeded = await controller.SubmitAsync(
                CreateRequest("open"),
                CancellationToken.None);

            Assert.That(succeeded, Is.True);
            Assert.That(presentation.Dialogue, Is.EqualTo("The gate stays closed."));
            Assert.That(presentation.Error, Is.Null);
            Assert.That(router.LastResult.Status, Is.EqualTo(NpcActionStatus.Rejected));
            controller.Dispose();
        }

        /// <summary>
        /// Confirms handler exceptions are reported as action failures without escaping routing.
        /// </summary>
        [Test]
        public async Task RouteAsync_HandlerFailure_ReturnsFailedResult()
        {
            var definition = CreateDefinition("fail_trigger", "fail_action", 1, "fail");
            var handler = new RecordingHandler("fail_action")
            {
                Failure = new InvalidOperationException("consumer failure")
            };
            var router = new NpcActionRouter(new[] { definition }, new[] { handler });

            var result = await router.RouteAsync(
                CreateTurn("fail_trigger"),
                CancellationToken.None);

            Assert.That(result.Status, Is.EqualTo(NpcActionStatus.Failed));
            Assert.That(result.Message, Does.Contain("consumer failure"));
        }

        /// <summary>
        /// Confirms an authored action without a selected consumer handler is diagnosed safely.
        /// </summary>
        [Test]
        public async Task RouteAsync_MissingHandler_ReturnsHandlerMissing()
        {
            var definition = CreateDefinition(
                "missing_trigger",
                "missing_action",
                1,
                "missing");
            var router = new NpcActionRouter(
                new[] { definition },
                Array.Empty<INpcActionHandler>());

            var result = await router.RouteAsync(
                CreateTurn("missing_trigger"),
                CancellationToken.None);

            Assert.That(result.Status, Is.EqualTo(NpcActionStatus.HandlerMissing));
            Assert.That(result.ActionId, Is.EqualTo("missing_action"));
        }

        /// <summary>
        /// Confirms cancellation is reported separately and releases the routing gate.
        /// </summary>
        [Test]
        public async Task RouteAsync_CancelledHandler_ReturnsCancelled()
        {
            var definition = CreateDefinition(
                "cancel_trigger",
                "cancel_action",
                1,
                "cancel");
            var handler = new RecordingHandler("cancel_action") { Block = true };
            var router = new NpcActionRouter(new[] { definition }, new[] { handler });
            using var cancellation = new CancellationTokenSource();
            var pending = router.RouteAsync(
                CreateTurn("cancel_trigger"),
                cancellation.Token);

            cancellation.Cancel();
            var result = await pending;

            Assert.That(result.Status, Is.EqualTo(NpcActionStatus.Cancelled));
            Assert.That(handler.ExecutionCount, Is.EqualTo(1));
        }

        /// <summary>
        /// Confirms overlapping direct routing calls cannot execute the same handler twice.
        /// </summary>
        [Test]
        public async Task RouteAsync_OverlappingCalls_ReturnsBusyForSecond()
        {
            var definition = CreateDefinition("wait_trigger", "wait_action", 1, "wait");
            var handler = new RecordingHandler("wait_action") { Block = true };
            var router = new NpcActionRouter(new[] { definition }, new[] { handler });
            var first = router.RouteAsync(CreateTurn("wait_trigger"), CancellationToken.None);

            var second = await router.RouteAsync(
                CreateTurn("wait_trigger"),
                CancellationToken.None);
            handler.Release();
            var firstResult = await first;

            Assert.That(second.Status, Is.EqualTo(NpcActionStatus.Busy));
            Assert.That(firstResult.Status, Is.EqualTo(NpcActionStatus.Succeeded));
            Assert.That(handler.ExecutionCount, Is.EqualTo(1));
        }

        /// <summary>
        /// Confirms Core IDs follow the exact lower snake_case grammar used by V3.
        /// </summary>
        [TestCase("open_gate", true)]
        [TestCase("open_gate2", true)]
        [TestCase("open__gate", false)]
        [TestCase("open_", false)]
        [TestCase("Open_gate", false)]
        public void IsValidIdentifier_UsesWireCompatibleSnakeCase(
            string value,
            bool expected)
        {
            Assert.That(
                NpcTriggerDefinition.IsValidIdentifier(value),
                Is.EqualTo(expected));
        }

        /// <summary>
        /// Creates one valid test trigger definition.
        /// </summary>
        private static NpcTriggerDefinition CreateDefinition(
            string triggerId,
            string actionId,
            int priority,
            string example)
        {
            return new NpcTriggerDefinition(
                triggerId,
                "The current user message satisfies " + triggerId + ".",
                example,
                actionId,
                priority);
        }

        /// <summary>
        /// Creates one valid domain request for action pipeline tests.
        /// </summary>
        private static AiNpcRequest CreateRequest(string userText)
        {
            return new AiNpcRequest(
                "sample-guide",
                "Guide",
                "Helpful",
                "Brief",
                "Hello.",
                NpcEmotion.Neutral,
                userText);
        }

        /// <summary>
        /// Creates one successful turn with selected response trigger IDs.
        /// </summary>
        private static NpcTurnContext CreateTurn(params string[] matchedIds)
        {
            return new NpcTurnContext(
                CreateRequest("test"),
                new AiNpcResponse(
                    "response",
                    NpcEmotion.Neutral,
                    NpcGesture.None,
                    matchedIds));
        }

        private sealed class RecordingHandler : INpcActionHandler
        {
            private readonly TaskCompletionSource<bool> completion =
                new TaskCompletionSource<bool>();

            public string ActionId { get; }
            public bool Allowed { get; set; } = true;
            public bool Block { get; set; }
            public Exception Failure { get; set; }
            public int ExecutionCount { get; private set; }

            /// <summary>
            /// Creates one controlled consumer handler.
            /// </summary>
            public RecordingHandler(string actionId)
            {
                ActionId = actionId;
            }

            /// <summary>
            /// Returns the controlled game-state authorization result.
            /// </summary>
            public bool CanExecute(
                NpcActionContext context,
                out string rejectionReason)
            {
                rejectionReason = Allowed ? string.Empty : "Gate is locked.";
                return Allowed;
            }

            /// <summary>
            /// Records execution and optionally blocks or throws.
            /// </summary>
            public async Task ExecuteAsync(
                NpcActionContext context,
                CancellationToken cancellationToken)
            {
                ExecutionCount++;
                if (Failure != null)
                {
                    throw Failure;
                }

                if (Block)
                {
                    using (cancellationToken.Register(() => completion.TrySetCanceled()))
                    {
                        await completion.Task;
                    }
                }
            }

            /// <summary>
            /// Completes one deliberately blocked action.
            /// </summary>
            public void Release()
            {
                completion.TrySetResult(true);
            }
        }

        private sealed class FixedClient : IAiConversationClient
        {
            private readonly AiNpcResponse response;

            /// <summary>
            /// Stores one fixed response for controller isolation tests.
            /// </summary>
            public FixedClient(AiNpcResponse response)
            {
                this.response = response;
            }

            /// <summary>
            /// Returns the fixed action-aware response.
            /// </summary>
            public Task<AiNpcResponse> SendAsync(
                AiNpcRequest request,
                CancellationToken cancellationToken)
            {
                return Task.FromResult(response);
            }
        }

        private sealed class RecordingPresentation : INpcPresentationDriver
        {
            public string Dialogue { get; private set; }
            public string Error { get; private set; }

            /// <summary>
            /// Records dialogue text.
            /// </summary>
            public void PresentDialogue(string dialogue) => Dialogue = dialogue;

            /// <summary>
            /// Accepts emotion without adding test state.
            /// </summary>
            public void PresentEmotion(NpcEmotion emotion)
            {
            }

            /// <summary>
            /// Accepts gesture without adding test state.
            /// </summary>
            public void PresentGesture(NpcGesture gesture)
            {
            }

            /// <summary>
            /// Accepts busy state without adding test state.
            /// </summary>
            public void SetBusy(bool isBusy)
            {
            }

            /// <summary>
            /// Records an unexpected dialogue error.
            /// </summary>
            public void PresentError(string message) => Error = message;

            /// <summary>
            /// Accepts cancellation without adding test state.
            /// </summary>
            public void PresentCancellation()
            {
            }
        }
    }
}
