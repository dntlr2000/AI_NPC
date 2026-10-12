using AiCharacterKit.Core;

namespace AiCharacterKit.Unity
{
    /// <summary>Contains immutable, source-version-specific adoption flags; no mutable global NPC state is stored here.</summary>
    public static class NpcInferenceAvailability
    {
        public const bool WindowsE4BTwoStage = false;
        public const bool DefaultApiTwoStage = true;
        public const string ValidatedApiModel = "gpt-5.6-luna";

        /// <summary>Validates mode and qualification without calling providers, handlers, a server or a model.</summary>
        public static bool TryValidate(NpcConversationMode mode, NpcInferenceStrategy strategy, out string error)
        {
            error = string.Empty;
            if (strategy == NpcInferenceStrategy.SinglePass) return true;
            if (strategy != NpcInferenceStrategy.ClassifyThenRespond)
            { error = "unsupported_inference_strategy: Unknown inference strategy."; return false; }
            if (mode != NpcConversationMode.LocalInference && mode != NpcConversationMode.BackendContext)
            { error = "unsupported_inference_strategy: Two-stage inference requires LocalInference or BackendContext/V4."; return false; }
            if ((mode == NpcConversationMode.LocalInference && WindowsE4BTwoStage)
                || (mode == NpcConversationMode.BackendContext && DefaultApiTwoStage)) return true;
            error = "unsupported_inference_strategy: This model configuration has not passed the Phase 17 adoption gate.";
            return false;
        }
    }
}
