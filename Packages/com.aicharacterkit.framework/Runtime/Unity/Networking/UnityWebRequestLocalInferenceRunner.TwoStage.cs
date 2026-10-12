using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using AiCharacterKit.Core;
using AiCharacterKit.LocalInference;
using AiCharacterKit.Transport.LocalInference;
using UnityEngine;

namespace AiCharacterKit.Unity.Networking
{
    public sealed partial class UnityWebRequestLocalInferenceRunner
    {
        public event Action<LocalInferenceTurnDiagnostics> TurnCompleted;

        /// <summary>Exposes qualified production compositions; the internal fake transport can exercise an unadopted candidate.</summary>
        public bool SupportsStrategy(NpcInferenceStrategy strategy) =>
            strategy == NpcInferenceStrategy.SinglePass ||
            (strategy == NpcInferenceStrategy.ClassifyThenRespond &&
             (testTransport != null || NpcInferenceAvailability.WindowsE4BTwoStage));

        /// <summary>Runs classification and dialogue under one queue lease and one deadline.</summary>
        private async Task<AiNpcResponse> GenerateTwoStageAsync(LocalInferenceRequest request,
            CancellationToken token, TurnMeasurements diagnostic)
        {
            var count = request.Envelope.triggers.Length;
            var provisional = new string[count];
            for (var i = 0; i < count; i++) provisional[i] = "no_match";
            var first = 0;
            LocalChatMessage[] classify = null;
            for (; first <= request.History.Count; first++)
            {
                token.ThrowIfCancellationRequested();
                diagnostic.Stage = "preflight";
                classify = LocalInferenceJsonCodec.CreateClassificationMessages(request, first);
                var classificationTokens = count == 0 ? 0 : await CountPromptTokensAsync(classify, token);
                var dialogueTokens = await CountPromptTokensAsync(
                    LocalInferenceJsonCodec.CreateDialogueMessages(request, first, provisional), token);
                if (FitsContext(classificationTokens) && FitsContext(dialogueTokens))
                {
                    diagnostic.ClassificationInputTokens = classificationTokens;
                    break;
                }
            }
            if (first > request.History.Count) throw LocalInferenceErrors.ContextLimit();
            var decisions = provisional;
            if (count != 0)
            {
                diagnostic.Stage = "classification";
                var watch = Stopwatch.StartNew();
                diagnostic.ModelCalls++;
                try
                {
                    var raw = await SendAsync("/v1/chat/completions", LocalInferenceJsonCodec.SerializeStageChat(
                        classify, LocalInferenceJsonCodec.ClassificationSchema(count)), token);
                    token.ThrowIfCancellationRequested();
                    diagnostic.ClassificationOutputTokens = ReadOutputTokens(raw);
                    decisions = LocalInferenceJsonCodec.ReadClassificationPayload(
                        LocalInferenceJsonCodec.ReadStageHttpPayload(raw), count);
                }
                finally { diagnostic.ClassificationSeconds = watch.Elapsed.TotalSeconds; }
            }
            token.ThrowIfCancellationRequested();
            diagnostic.Stage = "dialogue_preflight";
            var dialogue = LocalInferenceJsonCodec.CreateDialogueMessages(request, first, decisions);
            diagnostic.DialogueInputTokens = await CountPromptTokensAsync(dialogue, token);
            if (!FitsContext(diagnostic.DialogueInputTokens)) throw LocalInferenceErrors.ContextLimit();
            diagnostic.Stage = "dialogue";
            var timer = Stopwatch.StartNew();
            diagnostic.ModelCalls++;
            try
            {
                var raw = await SendAsync("/v1/chat/completions", LocalInferenceJsonCodec.SerializeStageChat(
                    dialogue, LocalInferenceJsonCodec.DialogueSchema()), token);
                token.ThrowIfCancellationRequested();
                diagnostic.DialogueOutputTokens = ReadOutputTokens(raw);
                var response = LocalInferenceJsonCodec.ReadDialoguePayload(
                    LocalInferenceJsonCodec.ReadStageHttpPayload(raw), request.Envelope, decisions);
                LastInputTokens = diagnostic.DialogueInputTokens;
                return response;
            }
            finally { diagnostic.DialogueSeconds = timer.Elapsed.TotalSeconds; }
        }

        /// <summary>Counts the actual template with the pinned tokenizer's required BOS handling.</summary>
        private async Task<int> CountPromptTokensAsync(LocalChatMessage[] messages, CancellationToken token)
        {
            RequireRunning();
            var template = ReadJson<LocalTemplateResponse>(await SendAsync("/apply-template",
                JsonUtility.ToJson(new LocalTemplateRequest { messages = messages }), token));
            if (string.IsNullOrEmpty(template.prompt)) throw LocalInferenceErrors.InvalidResponse();
            var counted = ReadJson<LocalTokenizeResponse>(await SendAsync("/tokenize", JsonUtility.ToJson(
                new LocalTokenizeRequest { content = template.prompt, add_special = true }), token));
            if (counted.tokens == null || counted.tokens.Length == 0) throw LocalInferenceErrors.InvalidResponse();
            return counted.tokens.Length;
        }

        /// <summary>Reserves the full output budget, accepting the exact context boundary.</summary>
        private static bool FitsContext(int inputTokens) => inputTokens >= 0 &&
            inputTokens <= LocalInferenceDefaults.ContextTokens - LocalInferenceDefaults.OutputTokens;

        /// <summary>Reports absent usage explicitly rather than inventing a zero-token response.</summary>
        private static int ReadOutputTokens(string raw)
        {
            // JsonUtility can synthesize an empty usage object for an absent property.
            var match = System.Text.RegularExpressions.Regex.Match(raw ?? string.Empty,
                "\"completion_tokens\"\\s*:\\s*(?<count>[0-9]+)");
            return match.Success && int.TryParse(match.Groups["count"].Value, out var count) ? count : -1;
        }

        /// <summary>Delivers immutable per-request diagnostics without allowing observer exceptions to affect generation.</summary>
        private void PublishDiagnostics(LocalInferenceRequest request, TurnMeasurements value, double totalSeconds)
        {
            var handlers = TurnCompleted;
            if (handlers == null) return;
            var snapshot = new LocalInferenceTurnDiagnostics(request?.RequestId, request?.Strategy ?? NpcInferenceStrategy.SinglePass,
                value.Stage, value.FailureCode, value.ModelCalls, value.ClassificationInputTokens, value.DialogueInputTokens,
                value.ClassificationOutputTokens, value.DialogueOutputTokens, value.QueueSeconds,
                value.ClassificationSeconds, value.DialogueSeconds, totalSeconds);
            foreach (Action<LocalInferenceTurnDiagnostics> handler in handlers.GetInvocationList())
                try { handler(snapshot); } catch { /* Diagnostics never change the completed turn. */ }
        }

        /// <summary>Keeps writable measurements local to one request, including time spent waiting for admission.</summary>
        private sealed class TurnMeasurements
        {
            public string Stage = "validation";
            public string FailureCode = string.Empty;
            public int ModelCalls;
            public int ClassificationInputTokens = -1;
            public int DialogueInputTokens = -1;
            public int ClassificationOutputTokens = -1;
            public int DialogueOutputTokens = -1;
            public double QueueSeconds;
            public double ClassificationSeconds;
            public double DialogueSeconds;
        }
    }
}
