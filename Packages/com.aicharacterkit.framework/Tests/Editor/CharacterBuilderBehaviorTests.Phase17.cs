using System;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using AiCharacterKit.Editor;
using AiCharacterKit.Unity;
using AiCharacterKit.Unity.Actions;
using NUnit.Framework;
using UnityEngine;

namespace AiCharacterKit.Core.Tests
{
    public sealed partial class CharacterBuilderBehaviorTests
    {
        /// <summary>Never displays a previous successful action as the result of a failed Basic or Advanced submission.</summary>
        [TestCase(false)][TestCase(true)]
        public async Task CurrentOutcome_FailureAndBusyDoNotReusePreviousTurn(bool advanced)
        {
            var target = new GameObject("Outcome NPC");
            var adapter = target.AddComponent<BehaviorTestAdapter>(); adapter.ValuesReady = true;
            var config = Configuration(target, adapter);
            if (!advanced)
            {
                var draft = new ActionProfileDraft(); draft.Bindings.Clear();
                draft.Bindings.Add(new ActionBindingDraft { TriggerId = "praise", ActionId = "open_gate" });
                Assert.That(CharacterBuilderAssetService.TryCreateActionProfile(draft, Folder, out var basic, out _), Is.True);
                config.BehaviorMode = NpcBehaviorMode.Basic; config.ActionProfile = basic;
            }
            Assert.That(CharacterBuilderService.TryApply(config, out var conversation, out _), Is.True);
            Initialize(conversation);
            var actions = target.GetComponent<NpcActionCoordinator>();
            var client = ReplaceOutcomeClient(conversation, adapter, actions);
            var first = conversation.SubmitAsync("Synthetic current praise.");
            client.Complete(); Assert.That(await first, Is.True);
            var previous = actions.LastBehaviorTurnOutcome;
            Assert.That(previous.WasEvaluated, Is.True);
            Assert.That(previous.ReturnedTriggerIds, Is.EqualTo(new[] { "praise" }));
            if (advanced)
            {
                Assert.That(previous.StateBefore["affinity"].Number, Is.EqualTo(40));
                Assert.That(previous.StateAfter["affinity"].Number, Is.EqualTo(50));
            }
            var second = conversation.SubmitAsync("Synthetic rejected generation.");
            var pending = actions.LastBehaviorTurnOutcome;
            Assert.That(pending.IsPending, Is.True);
            Assert.That(pending.ActionResult, Is.Null);
            Assert.That(await conversation.SubmitAsync("Duplicate"), Is.False);
            Assert.That(await conversation.ResetConversationAsync(), Is.False);
            Assert.That(actions.LastBehaviorTurnOutcome, Is.SameAs(pending));
            Assert.That(client.Calls, Is.EqualTo(2)); Assert.That(client.Resets, Is.Zero);
            client.Fail(); Assert.That(await second, Is.False);
            var failure = actions.LastBehaviorTurnOutcome;
            Assert.That(failure.TurnNumber, Is.GreaterThan(previous.TurnNumber));
            Assert.That(failure.IsPending || failure.WasEvaluated || failure.ConversationSucceeded, Is.False);
            Assert.That(failure.EvaluationCode, Is.EqualTo("generation_failed"));
            Assert.That(failure.ReturnedTriggerIds, Is.Empty); Assert.That(failure.ActionResult, Is.Null);
            if (advanced)
            {
                Assert.That(actions.TrySetNumber("affinity", 80, out _), Is.True);
                Assert.That(previous.StateAfter["affinity"].Number, Is.EqualTo(50));
                Assert.That(await conversation.ResetConversationAsync(), Is.True);
                Assert.That(actions.LastBehaviorTurnOutcome, Is.SameAs(failure));
                Assert.That(actions.TryResetBehaviorState(out _), Is.True);
                Assert.That(actions.LastBehaviorTurnOutcome, Is.Null);
            }
            Invoke(conversation, "OnDisable"); Invoke(conversation, "OnDestroy");
        }

        /// <summary>Reports consumer capture failure for this turn and succeeds on the same NPC when the provider becomes ready.</summary>
        [Test] public async Task CurrentOutcome_ProviderFailureAndRecoveryAreDistinct()
        {
            var target = new GameObject("Provider outcome"); var adapter = target.AddComponent<BehaviorTestAdapter>();
            Assert.That(CharacterBuilderService.TryApply(Configuration(target, adapter), out var conversation, out _), Is.True);
            Initialize(conversation); var actions = target.GetComponent<NpcActionCoordinator>();
            var client = ReplaceOutcomeClient(conversation, adapter, actions);
            var first = conversation.SubmitAsync("Synthetic praise"); client.Complete(); Assert.That(await first, Is.True);
            var failed = actions.LastBehaviorTurnOutcome;
            Assert.That(failed.WasEvaluated, Is.True); Assert.That(failed.ActionResult.Status, Is.EqualTo(NpcActionStatus.Failed));
            Assert.That(failed.StateBefore["affinity"].Number, Is.EqualTo(failed.StateAfter["affinity"].Number));
            adapter.ValuesReady = true;
            var second = conversation.SubmitAsync("Synthetic praise"); client.Complete(); Assert.That(await second, Is.True);
            Assert.That(actions.LastBehaviorTurnOutcome.TurnNumber, Is.GreaterThan(failed.TurnNumber));
            Assert.That(actions.LastBehaviorTurnOutcome.StateAfter["affinity"].Number, Is.EqualTo(50));
            Invoke(conversation, "OnDisable"); Invoke(conversation, "OnDestroy");
        }

        /// <summary>Cancels generation without committing state and allows the same enabled instance to submit again.</summary>
        [Test] public async Task CurrentOutcome_CancellationDoesNotPersistPendingOrMutateState()
        {
            var target = new GameObject("Cancelled outcome"); var adapter = target.AddComponent<BehaviorTestAdapter>(); adapter.ValuesReady = true;
            Assert.That(CharacterBuilderService.TryApply(Configuration(target, adapter), out var conversation, out _), Is.True);
            Initialize(conversation); var actions = target.GetComponent<NpcActionCoordinator>();
            var client = ReplaceOutcomeClient(conversation, adapter, actions);
            var pending = conversation.SubmitAsync("Synthetic praise"); Invoke(conversation, "OnDisable");
            Assert.That(await pending, Is.False);
            Assert.That(actions.LastBehaviorTurnOutcome.IsPending, Is.False);
            Assert.That(actions.LastBehaviorTurnOutcome.EvaluationCode, Is.EqualTo("cancelled"));
            Assert.That(actions.BehaviorState["affinity"].Number, Is.EqualTo(40));
            Invoke(conversation, "OnEnable"); var next = conversation.SubmitAsync("Synthetic praise"); client.Complete();
            Assert.That(await next, Is.True); Assert.That(actions.BehaviorState["affinity"].Number, Is.EqualTo(50));
            Invoke(conversation, "OnDisable"); Invoke(conversation, "OnDestroy");
        }

        /// <summary>Replaces only the conversation source; actual controller, provider and action dispatch remain under test.</summary>
        private static OutcomeClient ReplaceOutcomeClient(NpcConversationBehaviour npc, BehaviorTestAdapter adapter, NpcActionCoordinator actions)
        {
            var field = typeof(NpcConversationBehaviour).GetField("controller", BindingFlags.Instance | BindingFlags.NonPublic);
            ((NpcAIController)field.GetValue(npc)).Dispose();
            var client = new OutcomeClient(); field.SetValue(npc, new NpcAIController(client, adapter, actions)); return client;
        }

        private sealed class OutcomeClient : IResettableAiConversationClient
        {
            private TaskCompletionSource<AiNpcResponse> pending;
            public int Calls, Resets;
            /// <summary>Holds a synthetic generation until the test supplies success, failure or cancellation.</summary>
            public async Task<AiNpcResponse> SendAsync(AiNpcRequest request, CancellationToken token)
            {
                Calls++; var completion = new TaskCompletionSource<AiNpcResponse>(); pending = completion;
                using (token.Register(() => completion.TrySetCanceled())) return await completion.Task;
            }
            /// <summary>Supplies a valid signal without bypassing the real turn observer.</summary>
            public void Complete() => pending.SetResult(new AiNpcResponse("Synthetic reply.", NpcEmotion.Neutral, NpcGesture.None, new[] { "praise" }));
            /// <summary>Fails before behavior processing with a safe synthetic exception.</summary>
            public void Fail() => pending.SetException(new InvalidOperationException("synthetic"));
            /// <summary>Counts independent memory resets without touching consumer-owned state.</summary>
            public Task ResetAsync(CancellationToken token) { Resets++; return Task.CompletedTask; }
        }
    }
}
