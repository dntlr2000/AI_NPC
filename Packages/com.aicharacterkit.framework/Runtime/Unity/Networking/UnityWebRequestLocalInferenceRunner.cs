using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using AiCharacterKit.Core;
using AiCharacterKit.LocalInference;
using AiCharacterKit.Transport.LocalInference;
using AiCharacterKit.Transport.V4;
using AiCharacterKit.Unity.LocalInference;
using UnityEngine;
using UnityEngine.Networking;

namespace AiCharacterKit.Unity.Networking
{
    /// <summary>Manages one pinned local runner and sends bounded, non-streaming requests over loopback.</summary>
    public sealed partial class UnityWebRequestLocalInferenceRunner : ILocalInferenceRunner, ILocalInferenceStrategyCapabilities, ILocalInferenceDiagnostics, IDisposable
    {
        private readonly string manifestPath;
        private readonly SynchronizationContext unityContext;
        private WindowsLocalInferenceProcess process;
        private CancellationTokenSource lifetime;
        private LocalInferenceQueue queue;
        private Task preparation;
        private Task stopping;
        private readonly object requestSync = new object();
        private readonly HashSet<TaskCompletionSource<bool>> inFlight = new HashSet<TaskCompletionSource<bool>>();
        private Task processExit = Task.CompletedTask;
        private string endpoint;
        private string credential;
        private string workDirectory;
        private LocalInferenceState state;
        private bool disposed;
        private readonly Func<string, string, CancellationToken, Task<string>> testTransport;

        public LocalInferenceState State => testTransport == null && state == LocalInferenceState.Ready && (process == null || process.HasExited)
            ? LocalInferenceState.Faulted : state;
        public int ProcessId => process?.ProcessId ?? 0;
        public int LastInputTokens { get; private set; }
        public double PreparationSeconds { get; private set; }
        public double LastGenerationSeconds { get; private set; }
        public string RetainedWorkDirectory { get; private set; }

        /// <summary>Captures an explicit installation and the Unity synchronization context for cancellation.</summary>
        public UnityWebRequestLocalInferenceRunner(string manifestPath)
        {
            this.manifestPath = LocalInferenceInstallation.RequireLocalPath(manifestPath, Environment.CurrentDirectory);
            unityContext = SynchronizationContext.Current;
        }

        /// <summary>Exercises the production queue, token checks and stage pipeline through a test-only transport.</summary>
        internal UnityWebRequestLocalInferenceRunner(Func<string, string, CancellationToken, Task<string>> testTransport)
        {
            this.testTransport = testTransport ?? throw new ArgumentNullException(nameof(testTransport));
            unityContext = SynchronizationContext.Current;
            lifetime = new CancellationTokenSource();
            queue = new LocalInferenceQueue();
            state = LocalInferenceState.Ready;
        }

        /// <summary>Shares an in-flight preparation task and never starts a second process for the same host.</summary>
        public Task PrepareAsync(CancellationToken cancellationToken)
        {
            if (disposed) throw LocalInferenceErrors.Unavailable();
            if (stopping != null && !stopping.IsCompleted) return PrepareAfterStopAsync(cancellationToken);
            if (preparation != null && !preparation.IsCompleted) return preparation;
            if (State == LocalInferenceState.Ready) return Task.CompletedTask;
            preparation = PrepareCoreAsync(cancellationToken);
            return preparation;
        }

        /// <summary>Waits for old HTTP work and process cleanup before admitting an explicitly requested preparation.</summary>
        private async Task PrepareAfterStopAsync(CancellationToken cancellationToken)
        {
            await stopping;
            cancellationToken.ThrowIfCancellationRequested();
            await PrepareAsync(cancellationToken);
        }

        /// <summary>Verifies files, starts the private process, and completes synthetic schema warmup before readiness.</summary>
        private async Task PrepareCoreAsync(CancellationToken cancellationToken)
        {
            ReleaseResources();
            state = LocalInferenceState.Preparing;
            lifetime = new CancellationTokenSource();
            var preparationLifetime = lifetime.Token;
            var stopwatch = Stopwatch.StartNew();
            using (var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, preparationLifetime))
            {
                deadline.CancelAfter(TimeSpan.FromSeconds(LocalInferenceDefaults.PreparationSeconds));
                try
                {
                    // Establish cancellation before awaiting old-process cleanup so shutdown cannot start a replacement child.
                    await CleanupWorkAsync();
                    await DrainRequestsAsync();
                    deadline.Token.ThrowIfCancellationRequested();
                    var manifest = await LocalInferenceInstallation.LoadAsync(manifestPath, deadline.Token);
                    deadline.Token.ThrowIfCancellationRequested();
                    workDirectory = Path.Combine(Path.GetDirectoryName(manifestPath), "work", "run-" + Guid.NewGuid().ToString("N"));
                    EnsureNoDirectoryLinks(Path.GetDirectoryName(workDirectory));
                    var port = ReservePort();
                    endpoint = "http://127.0.0.1:" + port;
                    credential = Guid.NewGuid().ToString("N") + Guid.NewGuid().ToString("N");
                    var arguments = new[]
                    {
                        "-m", manifest.model.path, "--host", "127.0.0.1", "--port", port.ToString(),
                        "--device", "Vulkan0", "-ngl", "all", "-c", "4096", "-np", "1",
                        "--cache-ram", "0", "--no-cache-prompt", "--no-mmproj", "--offline", "--reasoning", "off",
                        "--no-context-shift", "--no-webui", "--no-slots", "--log-disable",
                        "--alias", "ai-character-kit-local", "--chat-template-file", manifest.chatTemplate.path,
                        "--chat-template-kwargs", "{\"enable_thinking\":false}"
                    };
                    process = new WindowsLocalInferenceProcess();
                    process.Start(manifest.executable.path, arguments, workDirectory, credential);
                    await WaitUntilHealthyAsync(deadline.Token);
                    state = LocalInferenceState.Warming;
                    var warmup = AiNpcContractMapper.CreateRequest(
                        new AiNpcRequest("local-warmup", "NPC", "Friendly.", "Reply briefly.", "Hello.", NpcEmotion.Neutral,
                            "Reply with one short greeting. Ignore this synthetic padding: " + new string('가', 700)),
                        "local-warmup", "local-warmup", Array.Empty<NpcTriggerDefinition>());
                    await GenerateCoreAsync(new LocalInferenceRequest(warmup, Array.Empty<LocalConversationTurn>()), deadline.Token);
                    deadline.Token.ThrowIfCancellationRequested();
                    queue = new LocalInferenceQueue();
                    PreparationSeconds = stopwatch.Elapsed.TotalSeconds;
                    state = LocalInferenceState.Ready;
                }
                catch (OperationCanceledException)
                {
                    var stopped = preparationLifetime.IsCancellationRequested;
                    ReleaseResources();
                    state = LocalInferenceState.Faulted;
                    await CleanupWorkAsync();
                    cancellationToken.ThrowIfCancellationRequested();
                    if (stopped) throw LocalInferenceErrors.Unavailable();
                    throw LocalInferenceErrors.Timeout();
                }
                catch (AiConversationException)
                {
                    ReleaseResources(); state = LocalInferenceState.Faulted;
                    await CleanupWorkAsync();
                    throw;
                }
                catch
                {
                    ReleaseResources(); state = LocalInferenceState.Faulted;
                    await CleanupWorkAsync();
                    throw LocalInferenceErrors.Unavailable();
                }
            }
        }

        /// <summary>Serializes callers through a bounded queue while keeping cancellation local to each request.</summary>
        public async Task<AiNpcResponse> GenerateAsync(LocalInferenceRequest request, CancellationToken cancellationToken)
        {
            var total = Stopwatch.StartNew();
            var diagnostic = new TurnMeasurements();
            var settled = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            lock (requestSync) inFlight.Add(settled);
            try
            {
                cancellationToken.ThrowIfCancellationRequested();
                LocalInferenceJsonCodec.ValidateRequest(request);
                if (!SupportsStrategy(request.Strategy)) throw LocalInferenceErrors.UnsupportedStrategy();
                if (State != LocalInferenceState.Ready || queue == null) throw LocalInferenceErrors.NotReady();
                var generationLifetime = lifetime.Token;
                diagnostic.Stage = "queue";
                var admissionWatch = Stopwatch.StartNew();
                using (var admission = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, generationLifetime))
                using (await queue.EnterAsync(admission.Token))
                using (var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, generationLifetime))
                {
                    diagnostic.QueueSeconds = admissionWatch.Elapsed.TotalSeconds;
                    deadline.CancelAfter(TimeSpan.FromSeconds(LocalInferenceDefaults.RequestSeconds));
                    var watch = Stopwatch.StartNew();
                    try
                    {
                        RequireRunning();
                        var response = request.Strategy == NpcInferenceStrategy.ClassifyThenRespond
                            ? await GenerateTwoStageAsync(request, deadline.Token, diagnostic)
                            : await GenerateCoreAsync(request, deadline.Token, diagnostic);
                        deadline.Token.ThrowIfCancellationRequested();
                        LastGenerationSeconds = watch.Elapsed.TotalSeconds;
                        diagnostic.Stage = "complete";
                        return response;
                    }
                    catch (OperationCanceledException)
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        if (generationLifetime.IsCancellationRequested) throw LocalInferenceErrors.Unavailable();
                        throw LocalInferenceErrors.Timeout();
                    }
                }
            }
            catch (OperationCanceledException) { diagnostic.FailureCode = "cancelled"; throw; }
            catch (AiConversationException error) { diagnostic.FailureCode = error.Code; throw; }
            catch { diagnostic.FailureCode = "local_runtime_unavailable"; throw LocalInferenceErrors.Unavailable(); }
            finally
            {
                try { PublishDiagnostics(request, diagnostic, total.Elapsed.TotalSeconds); }
                finally
                {
                    lock (requestSync) inFlight.Remove(settled);
                    settled.TrySetResult(true);
                }
            }
        }

        /// <summary>Counts the actual formatted prompt, drops oldest history pairs, and validates structured output.</summary>
        private async Task<AiNpcResponse> GenerateCoreAsync(LocalInferenceRequest request, CancellationToken token, TurnMeasurements diagnostic = null)
        {
            LocalInferenceJsonCodec.ValidateRequest(request);
            LocalChatMessage[] messages = null;
            for (var skipped = 0; skipped <= request.History.Count; skipped++)
            {
                RequireRunning();
                messages = LocalInferenceJsonCodec.CreateMessages(request, skipped);
                if (diagnostic != null) diagnostic.Stage = "preflight";
                LastInputTokens = await CountPromptTokensAsync(messages, token);
                if (diagnostic != null) diagnostic.DialogueInputTokens = LastInputTokens;
                if (FitsContext(LastInputTokens)) break;
                if (skipped == request.History.Count) throw LocalInferenceErrors.ContextLimit();
            }
            var json = LocalInferenceJsonCodec.SerializeChat(messages, request.Envelope.triggers);
            if (diagnostic != null) { diagnostic.Stage = "dialogue"; diagnostic.ModelCalls++; }
            var watch = Stopwatch.StartNew();
            try
            {
                var response = await SendAsync("/v1/chat/completions", json, token);
                token.ThrowIfCancellationRequested();
                if (diagnostic != null) diagnostic.DialogueOutputTokens = ReadOutputTokens(response);
                return LocalInferenceJsonCodec.ReadResponse(response, request.Envelope);
            }
            finally { if (diagnostic != null) diagnostic.DialogueSeconds = watch.Elapsed.TotalSeconds; }
        }

        /// <summary>Waits only for the owned process, treating startup loading as a readiness state.</summary>
        private async Task WaitUntilHealthyAsync(CancellationToken token)
        {
            while (true)
            {
                token.ThrowIfCancellationRequested();
                RequireRunning();
                using (var request = UnityWebRequest.Get(endpoint + "/health"))
                {
                    request.redirectLimit = 0;
                    request.timeout = 2;
                    await AwaitRequestAsync(request, token);
                    if (request.responseCode == 200) return;
                }
                await Task.Delay(150, token);
            }
        }

        /// <summary>Posts only to the private loopback instance and never follows HTTP redirects.</summary>
        private async Task<string> SendAsync(string route, string json, CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            if (testTransport != null) return await testTransport(route, json, token);
            using (var download = new BoundedDownload(4 * 1024 * 1024))
            using (var request = new UnityWebRequest(endpoint + route, UnityWebRequest.kHttpVerbPOST))
            {
                request.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(json));
                request.downloadHandler = download;
                request.disposeDownloadHandlerOnDispose = false;
                request.redirectLimit = 0;
                request.SetRequestHeader("Content-Type", "application/json");
                request.SetRequestHeader("Authorization", "Bearer " + credential);
                await AwaitRequestAsync(request, token);
                token.ThrowIfCancellationRequested();
                if (download.Exceeded) throw LocalInferenceErrors.InvalidResponse();
                if (request.responseCode < 200 || request.responseCode >= 300
                    || request.result != UnityWebRequest.Result.Success) throw LocalInferenceErrors.Unavailable();
                return download.ReadText();
            }
        }

        /// <summary>Aborts on Unity's main context and never exposes raw native or HTTP errors.</summary>
        private async Task AwaitRequestAsync(UnityWebRequest request, CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            var completed = new TaskCompletionSource<bool>();
            var operation = request.SendWebRequest();
            // Unity invokes completion on its main thread.
            void OnCompleted(AsyncOperation _) { completed.TrySetResult(true); }
            operation.completed += OnCompleted;
            if (operation.isDone) completed.TrySetResult(true);
            using (token.Register(() =>
            {
                if (unityContext != null) unityContext.Post(_ => AbortPending(request, completed), null);
                else AbortPending(request, completed);
            }))
            {
                try { await completed.Task; token.ThrowIfCancellationRequested(); }
                finally { operation.completed -= OnCompleted; }
            }
        }

        /// <summary>Ignores a late posted cancellation after completion rather than touching a disposed request.</summary>
        private static void AbortPending(UnityWebRequest request, TaskCompletionSource<bool> completed)
        {
            if (completed.Task.IsCompleted) return;
            request.Abort();
            // Wait for native completion before disposing the request and releasing its queue lease.
            if (request.isDone) completed.TrySetResult(true);
        }

        /// <summary>Converts unexpected JSON decoding details into a stable protocol error.</summary>
        private static T ReadJson<T>(string json) where T : class
        {
            try { return JsonUtility.FromJson<T>(json) ?? throw LocalInferenceErrors.InvalidResponse(); }
            catch { throw LocalInferenceErrors.InvalidResponse(); }
        }

        /// <summary>Fails immediately if the process this instance started has exited.</summary>
        private void RequireRunning()
        {
            if (testTransport != null) return;
            if (process == null || process.HasExited) throw LocalInferenceErrors.Unavailable();
        }

        /// <summary>Finds a loopback port; authentication prevents a bind race from accepting another server.</summary>
        private static int ReservePort()
        {
            var listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            try { return ((IPEndPoint)listener.LocalEndpoint).Port; }
            finally { listener.Stop(); }
        }

        /// <summary>Cancels current work and waits for preparation and owned-directory cleanup to finish.</summary>
        public Task StopAsync()
        {
            if (stopping != null && !stopping.IsCompleted) return stopping;
            stopping = StopCoreAsync();
            return stopping;
        }

        /// <summary>Settles admitted HTTP work and preparation before reporting that the shared runner has stopped.</summary>
        private async Task StopCoreAsync()
        {
            state = LocalInferenceState.Stopping;
            ReleaseResources();
            var pending = preparation;
            if (pending != null)
            {
                try { await pending; } catch { }
            }
            await DrainRequestsAsync();
            await CleanupWorkAsync();
            state = LocalInferenceState.Stopped;
        }

        /// <summary>Waits for the requests that were already admitted, including native cancellation completion.</summary>
        private Task DrainRequestsAsync()
        {
            Task[] pending;
            lock (requestSync)
            {
                pending = new Task[inFlight.Count];
                var index = 0;
                foreach (var request in inFlight) pending[index++] = request.Task;
            }
            return Task.WhenAll(pending);
        }

        /// <summary>Synchronously terminates the owned child during Unity destruction or assembly reload.</summary>
        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            state = LocalInferenceState.Stopped;
            ReleaseResources();
            // Directory cleanup has no Unity dependency and remains safe after the child handle closes.
            _ = CleanupWorkAsync();
        }

        /// <summary>Releases process and queue ownership without inspecting or killing unrelated processes.</summary>
        private void ReleaseResources()
        {
            lifetime?.Cancel();
            lifetime?.Dispose();
            lifetime = null;
            queue?.Dispose();
            queue = null;
            process?.Dispose();
            if (process != null) processExit = process.Termination;
            process = null;
            credential = null;
        }

        /// <summary>Deletes only the unique work directory created by this instance and retains uncertain paths.</summary>
        private async Task CleanupWorkAsync()
        {
            var owned = workDirectory;
            workDirectory = null;
            if (owned == null) return;
            await processExit;
            var removed = await Task.Run(() =>
            {
                try
                {
                    if (!Directory.Exists(owned)) return true;
                    EnsureNoDirectoryLinks(owned);
                    var pending = new Queue<string>(); pending.Enqueue(owned);
                    while (pending.Count > 0)
                    {
                        foreach (var item in Directory.EnumerateFileSystemEntries(pending.Dequeue()))
                        {
                            var attributes = File.GetAttributes(item);
                            if ((attributes & FileAttributes.ReparsePoint) != 0) return false;
                            if ((attributes & FileAttributes.Directory) != 0) pending.Enqueue(item);
                        }
                    }
                    Directory.Delete(owned, true);
                    return true;
                }
                catch { return false; }
            });
            if (!removed) RetainedWorkDirectory = owned;
        }

        /// <summary>Rejects directory junctions before creating or deleting private runtime files.</summary>
        private static void EnsureNoDirectoryLinks(string path)
        {
            for (var directory = new DirectoryInfo(path); directory != null; directory = directory.Parent)
                if (directory.Exists && (directory.Attributes & FileAttributes.ReparsePoint) != 0)
                    throw LocalInferenceErrors.Configuration();
        }

        private sealed class BoundedDownload : DownloadHandlerScript
        {
            private readonly MemoryStream data = new MemoryStream();
            private readonly int maximum;
            public bool Exceeded { get; private set; }
            /// <summary>Allocates a bounded receive buffer for local JSON and tokenization responses.</summary>
            public BoundedDownload(int maximum) : base(new byte[16384]) { this.maximum = maximum; }
            /// <summary>Stops oversized upstream bodies before retaining unbounded response data.</summary>
            protected override bool ReceiveData(byte[] bytes, int count)
            {
                if (data.Length + count > maximum) { Exceeded = true; return false; }
                if (bytes != null && count > 0) data.Write(bytes, 0, count);
                return true;
            }
            /// <summary>Decodes retained JSON without logging its contents.</summary>
            public string ReadText() { return Encoding.UTF8.GetString(data.ToArray()); }
        }
    }
}
