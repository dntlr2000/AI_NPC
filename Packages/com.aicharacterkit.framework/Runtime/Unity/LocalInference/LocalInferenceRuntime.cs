using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using AiCharacterKit.Core;
using AiCharacterKit.LocalInference;
using AiCharacterKit.Unity.Networking;
using UnityEngine;

namespace AiCharacterKit.Unity.LocalInference
{
    /// <summary>Explicit scene-owned process host shared by child NPCs; contains no static model state.</summary>
    public sealed class LocalInferenceRuntime : MonoBehaviour
    {
        [SerializeField] private LocalInferenceProfile profile;
        [SerializeField] private bool prepareOnEnable = true;
        private UnityWebRequestLocalInferenceRunner runner;
        private Task stopping = Task.CompletedTask;
        private bool destroyed;
        public LocalInferenceProfile Profile => profile;
        public LocalInferenceState State => runner?.State ?? LocalInferenceState.Stopped;
        public string LastErrorCode { get; private set; } = string.Empty;
        public string LastErrorMessage { get; private set; } = string.Empty;
        public int ProcessId => runner?.ProcessId ?? 0;
        public double PreparationSeconds => runner?.PreparationSeconds ?? 0;
        public double LastGenerationSeconds => runner?.LastGenerationSeconds ?? 0;
        public int LastInputTokens => runner?.LastInputTokens ?? 0;

        /// <summary>Starts background preparation only during an enabled runtime lifetime.</summary>
        private async void OnEnable()
        {
            if (prepareOnEnable) await PrepareAsync();
        }

        /// <summary>Stops owned work immediately while allowing the same host and clients to be enabled again.</summary>
        private void OnDisable()
        {
            if (runner != null) stopping = runner.StopAsync();
        }

        /// <summary>Closes the native job even if Unity cannot finish pending async continuations.</summary>
        private void OnDestroy()
        {
            destroyed = true;
            runner?.Dispose();
        }

        /// <summary>Closes the owned process during player exit, independently of component destruction order.</summary>
        private void OnApplicationQuit() { runner?.Dispose(); }

        /// <summary>Validates only references and path syntax without touching providers or starting a process.</summary>
        public bool TryValidateConfiguration(out string error)
        {
            if (profile == null) { error = "LocalInferenceRuntime requires a LocalInferenceProfile."; return false; }
            return profile.TryValidateConfiguration(out error);
        }

        /// <summary>Creates independent memory with optional response finalization, borrowing the shared process.</summary>
        public LocalInferenceConversationClient CreateClient(string characterId, IReadOnlyList<NpcTriggerDefinition> triggers,
            Func<AiNpcRequest, AiNpcResponse, CancellationToken, Task<AiNpcResponse>> finalizeResponse = null)
        {
            EnsureRunner();
            return new LocalInferenceConversationClient(runner, characterId, triggers, finalizeResponse: finalizeResponse);
        }

        /// <summary>Supports initial startup and explicit recovery without retrying any dialogue request.</summary>
        public async Task<bool> PrepareAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                await stopping;
                if (destroyed || !isActiveAndEnabled) return false;
                EnsureRunner();
                LastErrorCode = LastErrorMessage = string.Empty;
                await runner.PrepareAsync(cancellationToken);
                return true;
            }
            catch (OperationCanceledException) { return false; }
            catch (AiConversationException exception)
            {
                LastErrorCode = exception.Code;
                LastErrorMessage = exception.Message;
                return false;
            }
            catch
            {
                LastErrorCode = "local_runtime_unavailable";
                LastErrorMessage = "로컬 모델을 사용할 수 없습니다. 모델을 다시 준비해 주세요.";
                return false;
            }
        }

        /// <summary>Exposes an awaitable stop for scene transitions and verification.</summary>
        public Task StopAsync()
        {
            stopping = runner?.StopAsync() ?? Task.CompletedTask;
            return stopping;
        }

        /// <summary>Constructs the lazy adapter once; this does not read model files or spawn a process.</summary>
        private void EnsureRunner()
        {
            if (destroyed || !TryValidateConfiguration(out _)) throw LocalInferenceErrors.Configuration();
            if (runner == null)
                runner = new UnityWebRequestLocalInferenceRunner(profile.ResolveManifestPath(Environment.GetCommandLineArgs()));
        }
    }
}
