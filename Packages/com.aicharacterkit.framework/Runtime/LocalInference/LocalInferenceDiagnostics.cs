using System;
using AiCharacterKit.Core;

namespace AiCharacterKit.LocalInference
{
    /// <summary>Optionally publishes completed, request-correlated measurements without conversation content.</summary>
    public interface ILocalInferenceDiagnostics
    {
        event Action<LocalInferenceTurnDiagnostics> TurnCompleted;
    }

    /// <summary>Contains immutable measurements from one submission, including failed or cancelled stages.</summary>
    public sealed class LocalInferenceTurnDiagnostics
    {
        public string RequestId { get; }
        public NpcInferenceStrategy Strategy { get; }
        public string Stage { get; }
        public string FailureCode { get; }
        public int ModelCalls { get; }
        public int ClassificationInputTokens { get; }
        public int DialogueInputTokens { get; }
        public int ClassificationOutputTokens { get; }
        public int DialogueOutputTokens { get; }
        public double QueueSeconds { get; }
        public double ClassificationSeconds { get; }
        public double DialogueSeconds { get; }
        public double TotalSeconds { get; }

        /// <summary>Freezes measurements at completion; missing token usage remains -1 instead of a fabricated zero.</summary>
        public LocalInferenceTurnDiagnostics(string requestId, NpcInferenceStrategy strategy, string stage, string failureCode,
            int calls, int classificationInputTokens, int dialogueInputTokens, int classificationOutputTokens,
            int dialogueOutputTokens, double queueSeconds, double classificationSeconds, double dialogueSeconds, double totalSeconds)
        {
            RequestId = requestId; Strategy = strategy; Stage = stage; FailureCode = failureCode;
            ModelCalls = calls; ClassificationInputTokens = classificationInputTokens; DialogueInputTokens = dialogueInputTokens;
            ClassificationOutputTokens = classificationOutputTokens; DialogueOutputTokens = dialogueOutputTokens;
            QueueSeconds = queueSeconds; ClassificationSeconds = classificationSeconds; DialogueSeconds = dialogueSeconds;
            TotalSeconds = totalSeconds;
        }
    }
}
