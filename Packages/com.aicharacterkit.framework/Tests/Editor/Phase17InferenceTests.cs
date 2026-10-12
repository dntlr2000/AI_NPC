using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AiCharacterKit.Core;
using AiCharacterKit.LocalInference;
using AiCharacterKit.Transport.LocalInference;
using AiCharacterKit.Transport.V4;
using AiCharacterKit.Unity.Networking;
using NUnit.Framework;
using UnityEngine;

namespace AiCharacterKit.Core.Tests
{
    /// <summary>Runs the real staged pipeline against controlled HTTP without starting a process.</summary>
    public sealed class Phase17InferenceTests
    {
        private const string Dialogue = "{\"dialogue\":\"Checking.\",\"emotion\":\"neutral\",\"gesture\":\"none\"}";

        /// <summary>Does not offer a real pinned model composition that failed the independent semantic adoption gate.</summary>
        [Test] public void ProductionCapabilityRespectsModelQualificationWithoutIo()
        {
            using (var runner = new UnityWebRequestLocalInferenceRunner("E:/synthetic-only/manifest.json"))
            {
                Assert.That(runner.SupportsStrategy(NpcInferenceStrategy.SinglePass), Is.True);
                Assert.That(runner.SupportsStrategy(NpcInferenceStrategy.ClassifyThenRespond),
                    Is.EqualTo(AiCharacterKit.Unity.NpcInferenceAvailability.WindowsE4BTwoStage));
                Assert.That(runner.ProcessId, Is.Zero);
                Assert.That(runner.State, Is.EqualTo(LocalInferenceState.Stopped));
            }
        }
        /// <summary>Rejects malformed, extra, duplicated and truncated classifications before a second call.</summary>
        [TestCase("{}")][TestCase("{\"decisions\":[]}")][TestCase("{\"decisions\":[\"bad\"]}")]
        [TestCase("{\"decisions\":[\"match\",\"match\"]}")][TestCase("{\"decisions\":null}")]
        [TestCase("{\"decisions\":[\"match\"],\"reason\":\"hidden\"}")]
        [TestCase("{\"decisions\":[\"match\"],\"decisions\":[\"no_match\"]}")]
        [TestCase("{\"decisions\":[\"match\"]")]
        public void RejectsInvalidClassification(string json)
        { Assert.Throws<AiConversationException>(() => LocalInferenceJsonCodec.ReadClassificationPayload(json, 1)); }

        /// <summary>Only match positions can acquire a declared trigger ID.</summary>
        [TestCase("match", 1)][TestCase("no_match", 0)][TestCase("uncertain", 0)]
        public void MapsValidatedAuthority(string decision, int count)
        { Assert.That(LocalInferenceJsonCodec.ReadDialoguePayload(Dialogue, Request().Envelope, new[] { decision }).MatchedTriggerIds.Count, Is.EqualTo(count)); }

        /// <summary>Rejects attempts by presentation to replace classification.</summary>
        [Test]
        public void RejectsDialogueAuthority()
        { Assert.Throws<AiConversationException>(() => LocalInferenceJsonCodec.ReadDialoguePayload(Dialogue.Substring(0, Dialogue.Length - 1) + ",\"matchedTriggerIds\":[\"open\"]}", Request().Envelope, new[] { "no_match" })); }

        /// <summary>Freezes source DTOs and guards the snapshot from mutations through its public getter.</summary>
        [Test]
        public void SnapshotOwnsEveryMutableArray()
        {
            var original = Request().Envelope;
            var local = new LocalInferenceRequest(original, Array.Empty<LocalConversationTurn>());
            original.character.displayName = "Changed";
            original.triggers[0].triggerId = "wrong";
            original.grounding.behavioralRules = new[] { "changed" };
            var exposed = local.Envelope;
            exposed.character.personality = "changed";
            exposed.triggers[0].conditionDescription = "changed";
            Assert.That(local.Envelope.character.displayName, Is.EqualTo("NPC"));
            Assert.That(local.Envelope.character.personality, Is.EqualTo("PRIVATE_PERSONA"));
            Assert.That(local.Envelope.triggers[0].triggerId, Is.EqualTo("open"));
            Assert.That(local.Envelope.triggers[0].conditionDescription, Is.EqualTo("A current open request."));
            Assert.That(local.Strategy, Is.EqualTo(NpcInferenceStrategy.SinglePass));
        }

        /// <summary>Accepts exactly input plus reserved output while rejecting the next token before generation.</summary>
        [TestCase(3840, false)][TestCase(3840, true)][TestCase(3841, false)][TestCase(3841, true)]
        public async Task ExactTokenBoundary(int tokens, bool two)
        {
            var transport = new Transport { Tokens = tokens };
            using (var runner = new UnityWebRequestLocalInferenceRunner(transport.Send))
            {
                if (tokens == 3841)
                    Assert.That(Assert.ThrowsAsync<AiConversationException>(() => runner.GenerateAsync(Request(two), CancellationToken.None)).Code, Is.EqualTo("local_context_limit"));
                else await runner.GenerateAsync(Request(two), CancellationToken.None);
                Assert.That(transport.Calls, Is.EqualTo(tokens == 3841 ? 0 : two ? 2 : 1));
                Assert.That(transport.TokenBodies.All(t => t.add_special && t.parse_special), Is.True);
            }
        }

        /// <summary>No-trigger requests skip classification, while the diagnostics observer cannot break completion.</summary>
        [Test]
        public async Task NoTriggersUsesOneCallAndSafeDiagnostics()
        {
            var transport = new Transport();
            using (var runner = new UnityWebRequestLocalInferenceRunner(transport.Send))
            {
                LocalInferenceTurnDiagnostics observed = null;
                runner.TurnCompleted += _ => throw new InvalidOperationException("observer");
                runner.TurnCompleted += value => observed = value;
                var envelope = Request().Envelope; envelope.triggers = Array.Empty<AiNpcTriggerDto>();
                await runner.GenerateAsync(new LocalInferenceRequest(envelope, null, NpcInferenceStrategy.ClassifyThenRespond), CancellationToken.None);
                Assert.That(transport.Calls, Is.EqualTo(1));
                Assert.That(observed.ModelCalls, Is.EqualTo(1));
                Assert.That(observed.RequestId, Is.EqualTo(envelope.requestId));
                Assert.That(observed.Stage, Is.EqualTo("complete"));
            }
        }

        /// <summary>A successful classification followed by invalid dialogue cannot finalize an action or commit memory.</summary>
        [Test]
        public async Task DialogueFailureHasNoEffectsAndRecovers()
        {
            var transport = new Transport { InvalidDialogue = true };
            using (var runner = new UnityWebRequestLocalInferenceRunner(transport.Send))
            {
                var effects = 0;
                using (var client = new LocalInferenceConversationClient(runner, "test", Triggers(),
                    NpcInferenceStrategy.ClassifyThenRespond, finalizeResponse: (q, r, t) => { effects++; return Task.FromResult(r); }))
                {
                    Assert.ThrowsAsync<AiConversationException>(() => client.SendAsync(UserRequest(), CancellationToken.None));
                    Assert.That(effects, Is.Zero);
                    Assert.That(client.LastDiagnostics.Stage, Is.EqualTo("dialogue"));
                    Assert.That(client.LastDiagnostics.FailureCode, Is.EqualTo("invalid_model_response"));
                    transport.InvalidDialogue = false;
                    await client.SendAsync(UserRequest("Next valid turn"), CancellationToken.None);
                    Assert.That(effects, Is.EqualTo(1));
                    Assert.That(transport.ChatBodies.Last().messages.Any(m => m.content == "Do that."), Is.False);
                }
            }
        }

        /// <summary>Actual decision text is re-counted without deleting the history used by classification.</summary>
        [Test]
        public void RealDialogueOverflowStopsBeforeSecondGeneration()
        {
            var transport = new Transport { OverflowAfterClassification = true };
            using (var runner = new UnityWebRequestLocalInferenceRunner(transport.Send))
            {
                Assert.That(Assert.ThrowsAsync<AiConversationException>(() => runner.GenerateAsync(Request(true), CancellationToken.None)).Code,
                    Is.EqualTo("local_context_limit"));
                Assert.That(transport.Calls, Is.EqualTo(1));
            }
        }

        /// <summary>Cancellation after classification suppresses dialogue and the same queue accepts the next request.</summary>
        [Test]
        public async Task CancellationAtBoundaryRecovers()
        {
            using (var cancellation = new CancellationTokenSource())
            {
                var transport = new Transport { AfterClassification = cancellation.Cancel };
                using (var runner = new UnityWebRequestLocalInferenceRunner(transport.Send))
                {
                    Assert.That(async () => await runner.GenerateAsync(Request(true), cancellation.Token), Throws.InstanceOf<OperationCanceledException>());
                    Assert.That(transport.Calls, Is.EqualTo(1));
                    transport.AfterClassification = null;
                    await runner.GenerateAsync(Request(true), CancellationToken.None);
                    Assert.That(transport.Calls, Is.EqualTo(3));
                }
            }
        }

        /// <summary>Separate clients ignore each other's diagnostics and never report an old completed request as current.</summary>
        [Test]
        public async Task SharedRunnerDiagnosticsStayWithClient()
        {
            var transport = new Transport();
            using (var runner = new UnityWebRequestLocalInferenceRunner(transport.Send))
            using (var first = new LocalInferenceConversationClient(runner, "test", Triggers(), NpcInferenceStrategy.ClassifyThenRespond))
            using (var second = new LocalInferenceConversationClient(runner, "test", Triggers(), NpcInferenceStrategy.SinglePass))
            {
                await first.SendAsync(UserRequest(), CancellationToken.None);
                var saved = first.LastDiagnostics;
                await second.SendAsync(UserRequest(), CancellationToken.None);
                Assert.That(first.LastDiagnostics, Is.SameAs(saved));
                Assert.That(second.LastDiagnostics.RequestId, Is.Not.EqualTo(saved.RequestId));
                Assert.That(first.LastDiagnostics.ModelCalls, Is.EqualTo(2));
                Assert.That(second.LastDiagnostics.ModelCalls, Is.EqualTo(1));
            }
        }

        /// <summary>Legacy runners retain SinglePass, while an unsupported two-stage client fails before any generation.</summary>
        [Test]
        public async Task CustomRunnerCapabilityIsExplicit()
        {
            var legacy = new LegacyRunner();
            Assert.That(Assert.Throws<AiConversationException>(() =>
                new LocalInferenceConversationClient(legacy, "test", Triggers(), NpcInferenceStrategy.ClassifyThenRespond)).Code,
                Is.EqualTo("unsupported_inference_strategy"));
            using (var client = new LocalInferenceConversationClient(legacy, "test", Triggers()))
                await client.SendAsync(UserRequest(), CancellationToken.None);
            Assert.That(legacy.Calls, Is.EqualTo(1));
        }

        /// <summary>Distinguishes missing token telemetry from an actual zero-token completion.</summary>
        [Test] public async Task MissingUsageIsReportedAsUnavailable()
        {
            var transport = new Transport();
            using (var runner = new UnityWebRequestLocalInferenceRunner(async (route, body, token) =>
            {
                var raw = await transport.Send(route, body, token);
                var start = raw.IndexOf(",\"usage\":", StringComparison.Ordinal);
                return start < 0 ? raw : raw.Substring(0, start) + "}";
            }))
            using (var client = new LocalInferenceConversationClient(runner, "test", Triggers(), NpcInferenceStrategy.ClassifyThenRespond))
            {
                await client.SendAsync(UserRequest(), CancellationToken.None);
                Assert.That(client.LastDiagnostics.ClassificationOutputTokens, Is.EqualTo(-1));
                Assert.That(client.LastDiagnostics.DialogueOutputTokens, Is.EqualTo(-1));
            }
        }

        /// <summary>Keeps both generation stages in the same FIFO lease even when a second NPC is waiting.</summary>
        [Test] public async Task TwoStageOwnsOneFifoSlot()
        {
            var transport = new Transport(); var release = new TaskCompletionSource<bool>();
            var entered = new TaskCompletionSource<bool>(); var blocked = false;
            using (var runner = new UnityWebRequestLocalInferenceRunner(async (route, body, token) =>
            {
                if (route == "/v1/chat/completions" && !blocked)
                { blocked = true; entered.SetResult(true); await release.Task; }
                return await transport.Send(route, body, token);
            }))
            {
                var first = runner.GenerateAsync(Request(true), CancellationToken.None); await entered.Task;
                var second = runner.GenerateAsync(Request(true), CancellationToken.None);
                Assert.That(second.IsCompleted, Is.False); Assert.That(transport.Calls, Is.Zero);
                release.SetResult(true); await first; await second;
                Assert.That(transport.Calls, Is.EqualTo(4));
                Assert.That(transport.ChatBodies[0].messages[0].content, Does.StartWith("Classify"));
                Assert.That(transport.ChatBodies[1].messages[0].content, Does.StartWith("Role-play"));
                Assert.That(transport.ChatBodies[2].messages[0].content, Does.StartWith("Classify"));
            }
        }

        /// <summary>Does not report shutdown while cancelled transport work can still produce a late response.</summary>
        [Test] public async Task StopWaitsForNativeCompletionAndDiscardsLateReply()
        {
            var transport = new Transport(); var entered = new TaskCompletionSource<bool>();
            var release = new TaskCompletionSource<bool>();
            using (var runner = new UnityWebRequestLocalInferenceRunner(async (route, body, token) =>
            {
                if (route == "/v1/chat/completions") { entered.TrySetResult(true); await release.Task; }
                return await transport.Send(route, body, CancellationToken.None);
            }))
            {
                var active = runner.GenerateAsync(Request(true), CancellationToken.None); await entered.Task;
                var stopped = runner.StopAsync();
                Assert.That(stopped.IsCompleted, Is.False);
                Assert.That(runner.State, Is.EqualTo(LocalInferenceState.Stopping));
                Assert.That(runner.StopAsync(), Is.SameAs(stopped));
                release.SetResult(true);
                Assert.That(async () => await active, Throws.TypeOf<AiConversationException>());
                await stopped;
                Assert.That(runner.State, Is.EqualTo(LocalInferenceState.Stopped));
                Assert.That(transport.Calls, Is.EqualTo(1));
            }
        }

        /// <summary>Uses the actual 35-second budget across both stages instead of granting another budget to dialogue.</summary>
        [Test, Timeout(45000)] public async Task BothStagesShareOneGenerationDeadline()
        {
            var transport = new Transport(); var calls = 0;
            using (var runner = new UnityWebRequestLocalInferenceRunner(async (route, body, token) =>
            {
                if (route == "/v1/chat/completions") { calls++; await Task.Delay(TimeSpan.FromSeconds(20), token); }
                return await transport.Send(route, body, token);
            }))
            {
                LocalInferenceTurnDiagnostics diagnostic = null; runner.TurnCompleted += value => diagnostic = value;
                var watch = System.Diagnostics.Stopwatch.StartNew();
                AiConversationException failure = null;
                try { await runner.GenerateAsync(Request(true), CancellationToken.None); }
                catch (AiConversationException error) { failure = error; }
                Assert.That(failure?.Code, Is.EqualTo(LocalInferenceErrors.Timeout().Code));
                Assert.That(calls, Is.EqualTo(2)); Assert.That(diagnostic.Stage, Is.EqualTo("dialogue"));
                Assert.That(watch.Elapsed.TotalSeconds, Is.InRange(34d, 43d));
            }
        }

        /// <summary>Builds a bounded canonical request shared by real-pipeline tests.</summary>
        private static LocalInferenceRequest Request(bool two = false) => new LocalInferenceRequest(
            AiNpcContractMapper.CreateRequest(UserRequest(), "req", "session", Triggers()),
            new[] { new LocalConversationTurn("PAST_USER", "PAST_ASSISTANT") },
            two ? NpcInferenceStrategy.ClassifyThenRespond : NpcInferenceStrategy.SinglePass);
        /// <summary>Creates a synthetic persona with no live providers.</summary>
        private static AiNpcRequest UserRequest(string text = "Do that.") =>
            new AiNpcRequest("test", "NPC", "PRIVATE_PERSONA", "Brief.", "Hello", NpcEmotion.Neutral, text);
        /// <summary>Declares one semantic signal without executing a game action.</summary>
        private static NpcTriggerDefinition[] Triggers() => new[] { NpcTriggerDefinition.CreateSignal("open", "A current open request.", "Open.") };

        private sealed class Transport
        {
            public int Tokens = 128, Calls;
            public bool InvalidDialogue, OverflowAfterClassification;
            public Action AfterClassification;
            public readonly List<LocalTokenizeRequest> TokenBodies = new List<LocalTokenizeRequest>();
            public readonly List<LocalChatRequest> ChatBodies = new List<LocalChatRequest>();
            /// <summary>Returns exact tokenizer counts and controlled completions for production HTTP routes.</summary>
            public Task<string> Send(string route, string body, CancellationToken token)
            {
                token.ThrowIfCancellationRequested();
                if (route == "/apply-template") return Task.FromResult("{\"prompt\":\"SYNTHETIC_PROMPT\"}");
                if (route == "/tokenize")
                {
                    TokenBodies.Add(JsonUtility.FromJson<LocalTokenizeRequest>(body));
                    var count = OverflowAfterClassification && Calls > 0 ? 3841 : Tokens;
                    return Task.FromResult("{\"tokens\":[" + string.Join(",", Enumerable.Repeat("1", count)) + "]}");
                }
                Calls++;
                ChatBodies.Add(JsonUtility.FromJson<LocalChatRequest>(body));
                var classification = body.Contains("\"decisions\"");
                var isSingle = body.Contains("\"npc_reply\"");
                var content = classification ? "{\"decisions\":[\"match\"]}" : InvalidDialogue ? "{\"bad\":true}" :
                    isSingle ? Dialogue.Substring(0, Dialogue.Length - 1) + ",\"matchedTriggerIds\":[]}" : Dialogue;
                var message = JsonUtility.ToJson(new LocalChatMessage { role = "assistant", content = content });
                var result = "{\"choices\":[{\"finish_reason\":\"stop\",\"message\":" + message
                    + "}],\"usage\":{\"prompt_tokens\":" + Tokens + ",\"completion_tokens\":12}}";
                if (classification) AfterClassification?.Invoke();
                return Task.FromResult(result);
            }
        }
        private sealed class LegacyRunner : ILocalInferenceRunner
        {
            public int Calls;
            public LocalInferenceState State => LocalInferenceState.Ready;
            /// <summary>Provides a legacy runner without the optional strategy interface.</summary>
            public Task PrepareAsync(CancellationToken token) => Task.CompletedTask;
            /// <summary>Counts legacy calls and returns a validated neutral reply.</summary>
            public Task<AiNpcResponse> GenerateAsync(LocalInferenceRequest request, CancellationToken token)
            { Calls++; return Task.FromResult(new AiNpcResponse("Hello", NpcEmotion.Neutral, NpcGesture.None)); }
            /// <summary>Completes shutdown without external resources.</summary>
            public Task StopAsync() => Task.CompletedTask;
        }
    }
}

