using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using AiCharacterKit.Core;
using AiCharacterKit.Transport.V4;

namespace AiCharacterKit.LocalInference
{
    /// <summary>Owns one NPC's process-local dialogue memory while borrowing a shared runner.</summary>
    public sealed class LocalInferenceConversationClient : IResettableAiConversationClient, IDisposable
    {
        private readonly ILocalInferenceRunner runner;
        private readonly string characterId;
        private readonly NpcInferenceStrategy strategy;
        private readonly ILocalInferenceDiagnostics diagnosticSource;
        private string activeRequestId;
        public LocalInferenceTurnDiagnostics LastDiagnostics { get; private set; }
        private readonly string sessionId = "local-" + Guid.NewGuid().ToString("N");
        private readonly IReadOnlyList<NpcTriggerDefinition> triggers;
        private readonly HashSet<string> knownIds = new HashSet<string>(StringComparer.Ordinal);
        private readonly List<LocalConversationTurn> turns = new List<LocalConversationTurn>();
        private readonly Func<DateTime> utcNow;
        private readonly Func<AiNpcRequest, AiNpcResponse, CancellationToken, Task<AiNpcResponse>> finalizeResponse;
        private readonly object sync = new object();
        private readonly CancellationTokenSource lifetime = new CancellationTokenSource();
        private DateTime lastCommit;
        private bool busy;
        private bool disposed;

        /// <summary>Creates independent memory, optionally finalizing an authorized action reply before committing it.</summary>
        public LocalInferenceConversationClient(ILocalInferenceRunner runner, string characterId,
            IReadOnlyList<NpcTriggerDefinition> triggers, Func<DateTime> utcNow = null,
            Func<AiNpcRequest, AiNpcResponse, CancellationToken, Task<AiNpcResponse>> finalizeResponse = null)
            : this(runner, characterId, triggers, NpcInferenceStrategy.SinglePass, utcNow, finalizeResponse) { }

        /// <summary>Selects an explicit composition while preserving independent success-only memory.</summary>
        public LocalInferenceConversationClient(ILocalInferenceRunner runner, string characterId,
            IReadOnlyList<NpcTriggerDefinition> triggers, NpcInferenceStrategy strategy, Func<DateTime> utcNow = null,
            Func<AiNpcRequest, AiNpcResponse, CancellationToken, Task<AiNpcResponse>> finalizeResponse = null)
        {
            this.runner = runner ?? throw new ArgumentNullException(nameof(runner));
            if (string.IsNullOrWhiteSpace(characterId) || triggers == null
                || triggers.Count > AiNpcContractV4.MaxTriggerCount) throw LocalInferenceErrors.Configuration();
            var copy = new List<NpcTriggerDefinition>();
            foreach (var trigger in triggers)
            {
                if (trigger == null || !knownIds.Add(trigger.TriggerId)) throw LocalInferenceErrors.Configuration();
                copy.Add(trigger);
            }
            this.characterId = characterId;
            this.triggers = copy.AsReadOnly();
            this.utcNow = utcNow ?? (() => DateTime.UtcNow);
            this.finalizeResponse = finalizeResponse;
            if (strategy != NpcInferenceStrategy.SinglePass &&
                (strategy != NpcInferenceStrategy.ClassifyThenRespond || !(runner is ILocalInferenceStrategyCapabilities supported) || !supported.SupportsStrategy(strategy)))
                throw LocalInferenceErrors.UnsupportedStrategy();
            this.strategy = strategy;
            diagnosticSource = runner as ILocalInferenceDiagnostics;
            if (diagnosticSource != null) diagnosticSource.TurnCompleted += RecordDiagnostics;
        }

        /// <summary>Validates generation before side effects and remembers only the finalized, uncancelled reply.</summary>
        public async Task<AiNpcResponse> SendAsync(AiNpcRequest request, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            CancellationTokenSource operation;
            LocalInferenceRequest localRequest;
            lock (sync)
            {
                if (disposed) throw LocalInferenceErrors.Unavailable();
                if (busy) throw LocalInferenceErrors.Busy();
                if (request == null || request.CharacterId != characterId) throw LocalInferenceErrors.InvalidRequest();
                if (runner.State == LocalInferenceState.Faulted) throw LocalInferenceErrors.Unavailable();
                if (runner.State != LocalInferenceState.Ready) throw LocalInferenceErrors.NotReady();
                AiNpcRequestEnvelopeDto envelope;
                try { envelope = AiNpcContractMapper.CreateRequest(request, "req-" + Guid.NewGuid().ToString("N"), sessionId, triggers); }
                catch { throw LocalInferenceErrors.InvalidRequest(); }
                if (utcNow() - lastCommit >= TimeSpan.FromMinutes(LocalInferenceDefaults.IdleMinutes)) turns.Clear();
                localRequest = new LocalInferenceRequest(envelope, turns, strategy);
                activeRequestId = localRequest.RequestId;
                LastDiagnostics = null;
                operation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, lifetime.Token);
                busy = true;
            }
            using (operation)
            {
                try
                {
                    var response = await runner.GenerateAsync(localRequest, operation.Token);
                    ValidateResponse(response, localRequest.Envelope.requestId);
                    operation.Token.ThrowIfCancellationRequested();
                    if (finalizeResponse != null)
                    {
                        response = await finalizeResponse(request, response, operation.Token);
                        ValidateResponse(response, localRequest.Envelope.requestId);
                    }
                    lock (sync)
                    {
                        operation.Token.ThrowIfCancellationRequested();
                        if (disposed) throw LocalInferenceErrors.Unavailable();
                        turns.Add(new LocalConversationTurn(request.UserText, response.Dialogue));
                        TrimHistory();
                        lastCommit = utcNow();
                    }
                    return response;
                }
                catch (OperationCanceledException) { throw; }
                catch (AiConversationException) { throw; }
                catch { throw LocalInferenceErrors.Unavailable(); }
                finally { lock (sync) { busy = false; activeRequestId = null; } }
            }
        }

        /// <summary>Clears only this NPC's dialogue history without stopping the shared model.</summary>
        public Task ResetAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            lock (sync)
            {
                if (disposed) throw LocalInferenceErrors.Unavailable();
                if (busy) throw LocalInferenceErrors.Busy();
                turns.Clear();
                lastCommit = default;
            }
            return Task.CompletedTask;
        }

        /// <summary>Cancels this client's work and releases its memory without owning the runner lifetime.</summary>
        public void Dispose()
        {
            lock (sync)
            {
                if (disposed) return;
                disposed = true;
                turns.Clear();
            }
            if (diagnosticSource != null) diagnosticSource.TurnCompleted -= RecordDiagnostics;
            lifetime.Cancel();
            lifetime.Dispose();
        }

        /// <summary>Accepts only this client's active request, rejecting other NPCs and stale completions.</summary>
        private void RecordDiagnostics(LocalInferenceTurnDiagnostics value)
        {
            lock (sync) if (!disposed && value != null && value.RequestId == activeRequestId) LastDiagnostics = value;
        }

        /// <summary>Validates commands and configured IDs even when using a custom runner implementation.</summary>
        private void ValidateResponse(AiNpcResponse response, string requestId)
        {
            try
            {
                if (response == null || response.Dialogue.Length > 600) throw LocalInferenceErrors.InvalidResponse();
                AiNpcContractMapper.CreateSuccessResponse(response, requestId);
                foreach (var id in response.MatchedTriggerIds)
                    if (!knownIds.Contains(id)) throw LocalInferenceErrors.InvalidResponse();
            }
            catch { throw LocalInferenceErrors.InvalidResponse(); }
        }

        /// <summary>Evicts oldest complete pairs to preserve the existing count and UTF-8 memory limits.</summary>
        private void TrimHistory()
        {
            var bytes = 0;
            foreach (var turn in turns)
                bytes += Encoding.UTF8.GetByteCount(turn.UserText) + Encoding.UTF8.GetByteCount(turn.AssistantText);
            while (turns.Count > LocalInferenceDefaults.MaxHistoryTurns || bytes > LocalInferenceDefaults.MaxHistoryBytes)
            {
                bytes -= Encoding.UTF8.GetByteCount(turns[0].UserText) + Encoding.UTF8.GetByteCount(turns[0].AssistantText);
                turns.RemoveAt(0);
            }
        }
    }
}
