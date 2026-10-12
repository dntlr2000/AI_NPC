using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using AiCharacterKit.Core;
using AiCharacterKit.Transport.V4;

namespace AiCharacterKit.LocalInference
{
    /// <summary>Describes the externally owned model process without exposing platform APIs.</summary>
    public enum LocalInferenceState { Stopped, Preparing, Warming, Ready, Faulted, Stopping }

    /// <summary>Separates model lifetime and bounded generation from conversation memory.</summary>
    public interface ILocalInferenceRunner
    {
        LocalInferenceState State { get; }

        /// <summary>Validates the installation and prepares the model without a game turn.</summary>
        Task PrepareAsync(CancellationToken cancellationToken);

        /// <summary>Generates one validated response from explicit request-time context.</summary>
        Task<AiNpcResponse> GenerateAsync(LocalInferenceRequest request, CancellationToken cancellationToken);

        /// <summary>Stops only resources owned by this runner and cancels pending work.</summary>
        Task StopAsync();
    }

    /// <summary>Optionally declares supported strategies without preparing a model.</summary>
    public interface ILocalInferenceStrategyCapabilities
    {
        /// <summary>Reports whether this runner can execute the explicit requested composition.</summary>
        bool SupportsStrategy(NpcInferenceStrategy strategy);
    }

    /// <summary>Stores only a successful dialogue pair; grounding and revision are never retained.</summary>
    public sealed class LocalConversationTurn
    {
        public string UserText { get; }
        public string AssistantText { get; }

        /// <summary>Copies the immutable dialogue strings of a committed turn.</summary>
        public LocalConversationTurn(string userText, string assistantText)
        {
            UserText = userText ?? string.Empty;
            AssistantText = assistantText ?? string.Empty;
        }
    }

    /// <summary>Carries one fresh V4 envelope and a bounded snapshot of successful history.</summary>
    public sealed class LocalInferenceRequest
    {
        private readonly AiNpcRequestEnvelopeDto envelope;
        public AiNpcRequestEnvelopeDto Envelope => LocalInferenceSnapshots.Copy(envelope);
        public NpcInferenceStrategy Strategy { get; }
        public string RequestId => envelope.requestId;
        public IReadOnlyList<LocalConversationTurn> History { get; }

        /// <summary>Copies the history collection so later client activity cannot change the request.</summary>
        public LocalInferenceRequest(AiNpcRequestEnvelopeDto envelope, IEnumerable<LocalConversationTurn> history)
            : this(envelope, history, NpcInferenceStrategy.SinglePass) { }

        /// <summary>Freezes input DTOs and selects a strategy without changing existing callers.</summary>
        public LocalInferenceRequest(AiNpcRequestEnvelopeDto envelope, IEnumerable<LocalConversationTurn> history, NpcInferenceStrategy strategy)
        {
            if (strategy != NpcInferenceStrategy.SinglePass && strategy != NpcInferenceStrategy.ClassifyThenRespond)
                throw LocalInferenceErrors.UnsupportedStrategy();
            Strategy = strategy;
            this.envelope = LocalInferenceSnapshots.Copy(envelope ?? throw new ArgumentNullException(nameof(envelope)));
            History = new List<LocalConversationTurn>(history ?? Array.Empty<LocalConversationTurn>()).AsReadOnly();
        }
    }

    /// <summary>Defines the first measured Windows configuration and stable resource bounds.</summary>
    public static class LocalInferenceDefaults
    {
        public const string RuntimeVersion = "llama.cpp-b11146-vulkan-win-x64";
        public const string ModelVersion = "gemma-4-E4B-it-Q4_K_M@ef69cb97ef6642c95b3597b2af8aa55d9a27bf5e";
        public const string TemplateVersion = "lmstudio-gemma4-e4b@37d87b94f5d7ce4a3ba69fd625d63b2062502efd256c993b76a9764ae4b4ce53";
        public const string ModelSha256 = "3f72a20a06f626c78e6c475ae07a64c88b2663149c0f6197b56bf7cf1f37585c";
        public const string ExecutableSha256 = "7b886298b688509ced3e92b420edd57dd3d665da72c1fc207d7a537be5870352";
        public const string TemplateSha256 = "37d87b94f5d7ce4a3ba69fd625d63b2062502efd256c993b76a9764ae4b4ce53";
        public const int ContextTokens = 4096;
        public const int OutputTokens = 256;
        public const int PreparationSeconds = 180;
        public const int RequestSeconds = 35;
        public const int QueueSeconds = 30;
        public const int MaxPendingRequests = 8;
        public const int MaxHistoryTurns = 8;
        public const int MaxHistoryBytes = 16 * 1024;
        public const int IdleMinutes = 30;
    }

    /// <summary>Creates fixed public messages without paths, prompts, or upstream diagnostics.</summary>
    public static class LocalInferenceErrors
    {
        /// <summary>Rejects unsupported composition explicitly without falling back or contacting a model.</summary>
        public static AiConversationException UnsupportedStrategy() => new AiConversationException(
            "unsupported_inference_strategy", "이 대화 모드 또는 실행기는 선택한 추론 방식을 지원하지 않습니다.", false);
        /// <summary>Reports a configuration or installation that cannot be used.</summary>
        public static AiConversationException Configuration() => new AiConversationException(
            "local_configuration_error", "로컬 모델 설치 설정을 확인해 주세요.", false);
        /// <summary>Reports temporary model preparation without disabling the NPC.</summary>
        public static AiConversationException NotReady() => new AiConversationException(
            "local_not_ready", "NPC 대화 모델을 준비 중입니다. 준비 후 다시 시도해 주세요.", true);
        /// <summary>Reports an unavailable model and allows explicit preparation again.</summary>
        public static AiConversationException Unavailable() => new AiConversationException(
            "local_runtime_unavailable", "로컬 모델을 사용할 수 없습니다. 모델을 다시 준비해 주세요.", true);
        /// <summary>Reports the bounded generation deadline.</summary>
        public static AiConversationException Timeout() => new AiConversationException(
            "local_timeout", "로컬 모델 응답 시간이 초과되었습니다.", true);
        /// <summary>Reports full or expired admission without submitting another request.</summary>
        public static AiConversationException Busy() => new AiConversationException(
            "local_busy", "다른 NPC 대화를 처리 중입니다. 잠시 후 다시 시도해 주세요.", true);
        /// <summary>Rejects current required context that cannot fit the model token window.</summary>
        public static AiConversationException ContextLimit() => new AiConversationException(
            "local_context_limit", "현재 NPC 설정과 대화 입력이 로컬 모델의 처리 한도를 초과했습니다.", false);
        /// <summary>Rejects invalid input without revealing profile or validation details.</summary>
        public static AiConversationException InvalidRequest() => new AiConversationException(
            "invalid_request", "NPC 대화 요청이 올바르지 않거나 허용 크기를 초과했습니다.", false);
        /// <summary>Rejects malformed, truncated, or unauthorized model output.</summary>
        public static AiConversationException InvalidResponse() => new AiConversationException(
            "invalid_model_response", "로컬 모델 응답 형식이 올바르지 않습니다.", false);
    }
}
