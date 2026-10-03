using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using AiCharacterKit.Core;
using AiCharacterKit.LocalInference;
using AiCharacterKit.Transport.LocalInference;
using AiCharacterKit.Transport.V4;
using AiCharacterKit.Unity.LocalInference;
using AiCharacterKit.Unity.Networking;
using NUnit.Framework;
using UnityEngine;

namespace AiCharacterKit.Core.Tests
{
    /// <summary>Exercises independent memory, cancellation, FIFO admission and untrusted local JSON.</summary>
    public sealed class LocalInferenceTests
    {
        /// <summary>Confirms temporary preparation failure does not prevent a later submission through the same client.</summary>
        [TestCase(LocalInferenceState.Preparing)]
        [TestCase(LocalInferenceState.Faulted)]
        public async Task NotReady_ThenReady_ReusesClient(LocalInferenceState initialState)
        {
            var runner = new FakeRunner { State = initialState };
            using (var client = CreateClient(runner))
            {
                var error = Assert.ThrowsAsync<AiConversationException>(() => client.SendAsync(Request(), CancellationToken.None));
                Assert.That(error.Code, Is.EqualTo(initialState == LocalInferenceState.Faulted ? "local_runtime_unavailable" : "local_not_ready"));
                Assert.That(runner.Requests, Is.Empty);
                await runner.PrepareAsync(CancellationToken.None);
                Assert.That((await client.SendAsync(Request(), CancellationToken.None)).Dialogue, Is.EqualTo("안녕하세요."));
            }
        }

        /// <summary>Ensures clients sharing one runner cannot see each other's memory and reset stays local.</summary>
        [Test]
        public async Task SharedRunner_IsolatesMemoryAndReset()
        {
            var runner = new FakeRunner();
            using (var first = CreateClient(runner))
            using (var second = CreateClient(runner))
            {
                await first.SendAsync(Request("첫 번째 비밀"), CancellationToken.None);
                await second.SendAsync(Request("두 번째 대화"), CancellationToken.None);
                Assert.That(runner.Requests[1].History, Is.Empty);
                await first.SendAsync(Request("기억"), CancellationToken.None);
                Assert.That(runner.Requests[2].History[0].UserText, Is.EqualTo("첫 번째 비밀"));
                await first.ResetAsync(CancellationToken.None);
                await first.SendAsync(Request(), CancellationToken.None);
                Assert.That(runner.Requests[3].History, Is.Empty);
                await second.SendAsync(Request(), CancellationToken.None);
                Assert.That(runner.Requests[4].History.Count, Is.EqualTo(1));
                Assert.That(runner.StopCalls, Is.Zero);
            }
        }

        /// <summary>Invalid model IDs must not enter memory or be forwarded for game action execution.</summary>
        [TestCase("unknown")]
        [TestCase("duplicate")]
        [TestCase("null")]
        [TestCase("oversized")]
        public async Task InvalidResponses_DoNotCommit(string failure)
        {
            var runner = new FakeRunner();
            runner.Generate = (r, c) => Task.FromResult(failure == "null" ? null : new AiNpcResponse(
                failure == "oversized" ? new string('x', 601) : "안녕", NpcEmotion.Neutral, NpcGesture.None,
                failure == "unknown" ? new[] { "admin_override" } : new[] { "greet_guard", "greet_guard" }));
            using (var client = CreateClient(runner))
            {
                Assert.That(Assert.ThrowsAsync<AiConversationException>(() => client.SendAsync(Request(), CancellationToken.None)).Code,
                    Is.EqualTo("invalid_model_response"));
                runner.Generate = null;
                await client.SendAsync(Request(), CancellationToken.None);
                Assert.That(runner.Requests[1].History, Is.Empty);
            }
        }

        /// <summary>Provider exceptions are replaced by safe errors and failed user input is never remembered.</summary>
        [TestCase(false)]
        [TestCase(true)]
        public async Task RunnerException_IsSanitizedAndRecoverable(bool outOfMemory)
        {
            var runner = new FakeRunner { Generate = (r, c) => throw (outOfMemory
                ? (Exception)new OutOfMemoryException("PRIVATE native prompt") : new InvalidOperationException("PRIVATE native prompt")) };
            using (var client = CreateClient(runner))
            {
                var error = Assert.ThrowsAsync<AiConversationException>(() => client.SendAsync(Request(), CancellationToken.None));
                Assert.That(error.Message, Does.Not.Contain("PRIVATE"));
                runner.Generate = null;
                await client.SendAsync(Request(), CancellationToken.None);
                Assert.That(runner.Requests[1].History, Is.Empty);
            }
        }

        /// <summary>Cancellation rejects a late result and leaves both the shared runner and future submissions usable.</summary>
        [Test]
        public async Task CancellationAndDuplicate_DoNotCommitLateResponse()
        {
            var pending = new TaskCompletionSource<AiNpcResponse>();
            var runner = new FakeRunner { Generate = (r, c) => pending.Task };
            using (var client = CreateClient(runner))
            using (var cancellation = new CancellationTokenSource())
            {
                var request = client.SendAsync(Request(), cancellation.Token);
                Assert.That(Assert.ThrowsAsync<AiConversationException>(() => client.SendAsync(Request(), CancellationToken.None)).Code,
                    Is.EqualTo("local_busy"));
                cancellation.Cancel();
                pending.SetResult(Reply());
                await ExpectFailureAsync<OperationCanceledException>(request);
                runner.Generate = null;
                await client.SendAsync(Request(), CancellationToken.None);
                Assert.That(runner.Requests[1].History, Is.Empty);
                Assert.That(runner.StopCalls, Is.Zero);
            }
        }

        /// <summary>Uses the existing eight-turn, UTF-8 byte and idle-expiration limits.</summary>
        [Test]
        public async Task History_IsBoundedAndExpires()
        {
            var now = new DateTime(2026, 9, 30, 0, 0, 0, DateTimeKind.Utc);
            var runner = new FakeRunner();
            using (var client = new LocalInferenceConversationClient(runner, "guard", Triggers(), () => now))
            {
                for (var i = 0; i < 11; i++) await client.SendAsync(Request("turn-" + i), CancellationToken.None);
                Assert.That(runner.Requests.Last().History.Count, Is.EqualTo(8));
                for (var i = 0; i < 8; i++) await client.SendAsync(Request(new string('가', 1500)), CancellationToken.None);
                var history = runner.Requests.Last().History;
                Assert.That(history.Sum(t => Encoding.UTF8.GetByteCount(t.UserText) + Encoding.UTF8.GetByteCount(t.AssistantText)),
                    Is.LessThanOrEqualTo(16 * 1024));
                now = now.AddMinutes(31);
                await client.SendAsync(Request(), CancellationToken.None);
                Assert.That(runner.Requests.Last().History, Is.Empty);
            }
        }

        /// <summary>Rejects mismatched characters before model generation.</summary>
        [Test]
        public void CharacterMismatch_DoesNotReachRunner()
        {
            var runner = new FakeRunner();
            using (var client = CreateClient(runner))
            {
                var request = new AiNpcRequest("other", "Other", "Kind", "Brief", "Hi", NpcEmotion.Neutral, "Hello");
                Assert.That(Assert.ThrowsAsync<AiConversationException>(() => client.SendAsync(request, CancellationToken.None)).Code,
                    Is.EqualTo("invalid_request"));
                Assert.That(runner.Requests, Is.Empty);
            }
        }

        /// <summary>Preserves arrival order, rejects overflow and removes cancelled waiters.</summary>
        [Test]
        public async Task Queue_PreservesOrderAndCapacity()
        {
            using (var queue = new LocalInferenceQueue(2))
            using (var cancellation = new CancellationTokenSource())
            {
                var active = await queue.EnterAsync(CancellationToken.None);
                var first = queue.EnterAsync(cancellation.Token);
                var second = queue.EnterAsync(CancellationToken.None);
                Assert.That(Assert.ThrowsAsync<AiConversationException>(() => queue.EnterAsync(CancellationToken.None)).Code,
                    Is.EqualTo("local_busy"));
                cancellation.Cancel();
                await ExpectFailureAsync<OperationCanceledException>(first);
                Assert.That(second.IsCompleted, Is.False);
                active.Dispose();
                using (await second) { }
                using (await queue.EnterAsync(CancellationToken.None)) { }
            }
        }

        /// <summary>Queue deadlines and shutdown cannot strand pending requests.</summary>
        [Test]
        public async Task Queue_TimeoutAndShutdown_ReleaseWaiters()
        {
            var queue = new LocalInferenceQueue(2, TimeSpan.FromMilliseconds(20));
            using (await queue.EnterAsync(CancellationToken.None))
            {
                var waiting = queue.EnterAsync(CancellationToken.None);
                Assert.That((await ExpectFailureAsync<AiConversationException>(waiting)).Code, Is.EqualTo("local_busy"));
                var stopped = queue.EnterAsync(CancellationToken.None);
                queue.Dispose();
                Assert.That((await ExpectFailureAsync<AiConversationException>(stopped)).Code, Is.EqualTo("local_runtime_unavailable"));
            }
        }

        /// <summary>Accepts escaped Korean output and validates the existing action-aware domain response.</summary>
        [Test]
        public void Json_ValidEscapes_MapToCore()
        {
            var payload = "{\"dialogue\":\"안녕 \\\"친구\\\" \\\\ 길\\n다음\",\"emotion\":\"happy\",\"gesture\":\"wave\",\"matchedTriggerIds\":[\"greet_guard\"]}";
            var response = LocalInferenceJsonCodec.ReadResponse(Completion(payload), Envelope());
            Assert.That(response.Dialogue, Does.Contain("\"친구\" \\ 길\n다음"));
            Assert.That(response.MatchedTriggerIds, Is.EqualTo(new[] { "greet_guard" }));
        }

        /// <summary>Rejects malformed JSON, undeclared fields, duplicate fields and unknown action IDs.</summary>
        [TestCase("{\"dialogue\":\"hi\"}")]
        [TestCase("{\"dialogue\":\"hi\",\"emotion\":\"neutral\",\"gesture\":\"none\",\"matchedTriggerIds\":null}")]
        [TestCase("{\"dialogue\":\"hi\",\"emotion\":\"neutral\",\"gesture\":\"none\",\"matchedTriggerIds\":[\"admin_override\"]}")]
        [TestCase("{\"dialogue\":\"hi\",\"dialogue\":\"other\",\"emotion\":\"neutral\",\"gesture\":\"none\",\"matchedTriggerIds\":[]}")]
        [TestCase("{\"dialogue\":\"hi\",\"emotion\":\"neutral\",\"gesture\":\"none\",\"matchedTriggerIds\":[],\"action\":\"run\"}")]
        [TestCase("{\"dialogue\":\"hi\",\"emotion\":\"neutral\",\"gesture\":\"none\",\"matchedTriggerIds\":[],}")]
        public void Json_InvalidPayload_FailsSafely(string payload)
        {
            Assert.That(Assert.Throws<AiConversationException>(() => LocalInferenceJsonCodec.ReadResponse(Completion(payload), Envelope())).Code,
                Is.EqualTo("invalid_model_response"));
        }

        /// <summary>Refuses truncated generations and unexpected reasoning even when their content resembles valid JSON.</summary>
        [TestCase("length", "")]
        [TestCase("stop", "PRIVATE reasoning")]
        public void Json_UnfinishedOrReasoning_IsRejected(string finish, string reasoning)
        {
            Assert.Throws<AiConversationException>(() => LocalInferenceJsonCodec.ReadResponse(
                Completion("{\"dialogue\":\"hi\",\"emotion\":\"neutral\",\"gesture\":\"none\",\"matchedTriggerIds\":[]}", finish, reasoning), Envelope()));
        }

        /// <summary>Gives current state its own trusted message after stale dialogue without changing user roles or history.</summary>
        [Test]
        public void Prompt_UsesCurrentGroundingAndValidActionFreeSchema()
        {
            var envelope = Envelope();
            envelope.triggers = Array.Empty<AiNpcTriggerDto>();
            var request = new LocalInferenceRequest(envelope, new[] { new LocalConversationTurn("past", "answer") });
            var messages = LocalInferenceJsonCodec.CreateMessages(request, 0);
            Assert.That(messages.Select(m => m.role), Is.EqualTo(new[] { "system", "user", "assistant", "system", "user" }));
            Assert.That(messages[3].content, Does.Contain("CURRENT TURN"));
            Assert.That(messages[3].content, Does.Contain("Grounding snapshot:"));
            Assert.That(messages[3].content, Does.Contain("\"statements\":[\"past\"]"));
            Assert.That(LocalInferenceJsonCodec.CreateMessages(request, 1)[1].content, Does.Not.Contain("\"past\""));
            Assert.That(messages[2].content, Is.EqualTo("answer"));
            Assert.That(messages[4].content, Is.EqualTo(envelope.userText));
            Assert.That(LocalInferenceJsonCodec.CreateMessages(request, 1).Length, Is.EqualTo(3));
            var json = LocalInferenceJsonCodec.SerializeChat(messages, envelope.triggers);
            Assert.That(json, Does.Contain("\"maxItems\":0"));
            Assert.That(json, Does.Not.Contain("\"enum\":[]"));
            Assert.That(json, Does.Contain("\"enable_thinking\":false"));
        }

        /// <summary>Retains the exact serialized V4 ceiling before model transport.</summary>
        [Test]
        public void Request_OverV4Budget_IsRejected()
        {
            var envelope = Envelope();
            envelope.character.personality = new string('x', AiNpcContractV4.MaxRequestBodyUtf8Bytes);
            Assert.That(Assert.Throws<AiConversationException>(() => LocalInferenceJsonCodec.ValidateRequest(
                new LocalInferenceRequest(envelope, Array.Empty<LocalConversationTurn>()))).Code, Is.EqualTo("invalid_request"));
        }

        /// <summary>Preserves Windows paths with spaces, Korean characters, quotes and a trailing backslash.</summary>
        [Test]
        public void ProcessArguments_AreQuotedWithoutShellExecution()
        {
            Assert.That(WindowsLocalInferenceProcess.JoinArguments(new[] { "-m", "E:\\한글 폴더\\model.gguf", "last\\" }),
                Is.EqualTo("\"-m\" \"E:\\한글 폴더\\model.gguf\" \"last\\\\\""));
        }

        /// <summary>Remembers the actual action receipt rather than the model's contradictory proposal.</summary>
        [Test]
        public async Task Finalization_CommitsOnlyFinalReplyOnce()
        {
            var calls = 0;
            var runner = new FakeRunner { Generate = (r, c) => Task.FromResult(new AiNpcResponse(
                "문을 열 수 없습니다.", NpcEmotion.Sad, NpcGesture.None, new[] { "greet_guard" })) };
            using (var client = new LocalInferenceConversationClient(runner, "guard", Triggers(), finalizeResponse: (r, response, token) =>
            {
                calls++;
                Assert.That(response.Dialogue, Is.EqualTo("문을 열 수 없습니다."));
                return Task.FromResult(new AiNpcResponse("문을 열었습니다.", NpcEmotion.Neutral, NpcGesture.None, response.MatchedTriggerIds));
            }))
            {
                Assert.That((await client.SendAsync(Request(), CancellationToken.None)).Dialogue, Is.EqualTo("문을 열었습니다."));
                await client.SendAsync(Request(), CancellationToken.None);
                Assert.That(runner.Requests[1].History.Single().AssistantText, Is.EqualTo("문을 열었습니다."));
                Assert.That(calls, Is.EqualTo(2));
                await client.ResetAsync(CancellationToken.None);
                Assert.That(calls, Is.EqualTo(2));
            }
        }

        /// <summary>Untrusted or late cancelled model output must never reach a consumer action callback.</summary>
        [TestCase(false)]
        [TestCase(true)]
        public async Task Finalization_RejectsBeforeSideEffects(bool cancelled)
        {
            var pending = new TaskCompletionSource<AiNpcResponse>();
            var runner = new FakeRunner { Generate = (r, c) => pending.Task };
            var calls = 0;
            using (var token = new CancellationTokenSource())
            using (var client = new LocalInferenceConversationClient(runner, "guard", Triggers(), finalizeResponse: (r, response, ct) =>
                { calls++; return Task.FromResult(response); }))
            {
                var send = client.SendAsync(Request(), token.Token);
                if (cancelled) token.Cancel();
                pending.SetResult(cancelled ? Reply() : new AiNpcResponse("Bad", NpcEmotion.Neutral, NpcGesture.None, new[] { "unknown" }));
                if (cancelled) await ExpectFailureAsync<OperationCanceledException>(send);
                else await ExpectFailureAsync<AiConversationException>(send);
                Assert.That(calls, Is.Zero);
                runner.Generate = null;
                await client.SendAsync(Request(), CancellationToken.None);
                Assert.That(runner.Requests[1].History, Is.Empty);
            }
        }

        /// <summary>A failed formatter cannot save proposed text, leak a raw exception, retry generation or poison the client.</summary>
        [TestCase("throw")]
        [TestCase("null")]
        [TestCase("unknown")]
        [TestCase("oversized")]
        public async Task Finalization_FailureLeavesMemoryEmpty(string failure)
        {
            var fail = true;
            var runner = new FakeRunner();
            using (var client = new LocalInferenceConversationClient(runner, "guard", Triggers(), finalizeResponse: (r, response, ct) =>
            {
                if (!fail) return Task.FromResult(response);
                if (failure == "throw") throw new InvalidOperationException("PRIVATE consumer exception");
                return Task.FromResult(failure == "null" ? null : new AiNpcResponse(
                    failure == "oversized" ? new string('x', 601) : "Bad", NpcEmotion.Neutral, NpcGesture.None, new[] { "unknown" }));
            }))
            {
                var error = await ExpectFailureAsync<AiConversationException>(client.SendAsync(Request(), CancellationToken.None));
                Assert.That(error.Message, Does.Not.Contain("PRIVATE"));
                Assert.That(runner.Requests.Count, Is.EqualTo(1));
                fail = false;
                await client.SendAsync(Request(), CancellationToken.None);
                Assert.That(runner.Requests[1].History, Is.Empty);
            }
        }

        /// <summary>Cancellation during finalization leaves no dialogue history and keeps other clients' runner alive.</summary>
        [Test]
        public async Task Finalization_CancellationAndBusyGuardPersistUntilCompletion()
        {
            var pending = new TaskCompletionSource<AiNpcResponse>();
            var runner = new FakeRunner();
            var hold = true;
            using (var cancellation = new CancellationTokenSource())
            using (var client = new LocalInferenceConversationClient(runner, "guard", Triggers(), finalizeResponse: (r, response, ct) =>
                hold ? pending.Task : Task.FromResult(response)))
            {
                var send = client.SendAsync(Request(), cancellation.Token);
                Assert.That(Assert.ThrowsAsync<AiConversationException>(() => client.ResetAsync(CancellationToken.None)).Code, Is.EqualTo("local_busy"));
                cancellation.Cancel();
                pending.SetResult(Reply());
                await ExpectFailureAsync<OperationCanceledException>(send);
                hold = false;
                await client.SendAsync(Request(), CancellationToken.None);
                Assert.That(runner.Requests[1].History, Is.Empty);
                Assert.That(runner.StopCalls, Is.Zero);
            }
        }

        /// <summary>Awaits failures without blocking Unity's synchronization context or the editor update loop.</summary>
        private static async Task<T> ExpectFailureAsync<T>(Task task) where T : Exception
        {
            try { await task; }
            catch (T error) { return error; }
            Assert.Fail("Expected " + typeof(T).Name);
            return null;
        }

        /// <summary>Provides one valid synthetic request for all client tests.</summary>
        private static AiNpcRequest Request(string text = "안녕하세요") => new AiNpcRequest(
            "guard", "경비병", "침착", "한국어 존댓말", "안녕하세요", NpcEmotion.Neutral, text);
        /// <summary>Provides the configured opaque trigger used by validation tests.</summary>
        private static NpcTriggerDefinition[] Triggers() => new[] { new NpcTriggerDefinition("greet_guard", "인사한다", "안녕", "greet", 1) };
        /// <summary>Creates one independent client using a fake runner.</summary>
        private static LocalInferenceConversationClient CreateClient(FakeRunner runner) => new LocalInferenceConversationClient(runner, "guard", Triggers());
        /// <summary>Creates a bounded successful model reply.</summary>
        private static AiNpcResponse Reply() => new AiNpcResponse("안녕하세요.", NpcEmotion.Neutral, NpcGesture.None);
        /// <summary>Builds a real V4 fixture instead of bypassing transport validation.</summary>
        private static AiNpcRequestEnvelopeDto Envelope() => AiNpcContractMapper.CreateRequest(Request(), "test-request", "test-session", Triggers());
        /// <summary>Wraps a synthetic payload in the model server's real response shape.</summary>
        private static string Completion(string content, string finish = "stop", string reasoning = "") => JsonUtility.ToJson(new LocalCompletionResponse
        {
            choices = new[] { new LocalCompletionChoice { finish_reason = finish,
                message = new LocalCompletionMessage { role = "assistant", content = content, reasoning_content = reasoning } } }
        });

        private sealed class FakeRunner : ILocalInferenceRunner
        {
            public LocalInferenceState State { get; set; } = LocalInferenceState.Ready;
            public List<LocalInferenceRequest> Requests { get; } = new List<LocalInferenceRequest>();
            public Func<LocalInferenceRequest, CancellationToken, Task<AiNpcResponse>> Generate;
            public int StopCalls;
            /// <summary>Simulates explicit recovery to readiness without starting any process.</summary>
            public Task PrepareAsync(CancellationToken cancellationToken) { State = LocalInferenceState.Ready; return Task.CompletedTask; }
            /// <summary>Captures synthetic inputs and injects a controlled result or failure.</summary>
            public Task<AiNpcResponse> GenerateAsync(LocalInferenceRequest request, CancellationToken cancellationToken)
            {
                Requests.Add(request);
                return Generate == null ? Task.FromResult(Reply()) : Generate(request, cancellationToken);
            }
            /// <summary>Records unwanted lifecycle coupling from an individual NPC client.</summary>
            public Task StopAsync() { StopCalls++; State = LocalInferenceState.Stopped; return Task.CompletedTask; }
        }
    }
}
