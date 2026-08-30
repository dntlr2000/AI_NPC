using System;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using AiCharacterKit.Speech;
using AiCharacterKit.Transport.Speech.V1;
using AiCharacterKit.Unity.Transport;
using UnityEngine;
using UnityEngine.Networking;

namespace AiCharacterKit.Unity.Networking
{
    /// <summary>
    /// Posts Speech V1 JSON to an explicit loopback endpoint and returns bounded PCM.
    /// </summary>
    public sealed class UnityWebRequestAiSpeechBackendGateway
        : IAiSpeechBackendGateway
    {
        private readonly Uri endpoint;
        private readonly int timeoutSeconds;

        /// <summary>
        /// Creates a credential-free local speech gateway with a positive timeout.
        /// </summary>
        public UnityWebRequestAiSpeechBackendGateway(
            string endpoint,
            int timeoutSeconds)
        {
            this.endpoint = ParseLoopbackEndpoint(endpoint);
            if (timeoutSeconds <= 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(timeoutSeconds),
                    "Speech backend timeout must be greater than zero.");
            }

            this.timeoutSeconds = timeoutSeconds;
        }

        /// <summary>
        /// Sends one request and accepts only correlated fixed-format PCM or valid JSON error.
        /// </summary>
        public async Task<SpeechBackendAudioResponse> SendAsync(
            SpeechSynthesisRequestDto requestDto,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!SpeechJsonCodecV1.TrySerializeRequest(
                    requestDto,
                    out var requestJson,
                    out _))
            {
                throw CreateProtocolException();
            }

            using (var request = CreateRequest(requestJson))
            {
                await SendSafelyAsync(request, cancellationToken);
                cancellationToken.ThrowIfCancellationRequested();

                if (request.result == UnityWebRequest.Result.ConnectionError)
                {
                    ThrowConnectionFailure(request.error);
                }

                if (request.responseCode >= 200 && request.responseCode < 300)
                {
                    return ReadSuccessResponse(request);
                }

                ThrowErrorResponse(request, requestDto.requestId);
                return null;
            }
        }

        /// <summary>
        /// Creates one JSON POST that buffers the complete bounded response in memory.
        /// </summary>
        private UnityWebRequest CreateRequest(string json)
        {
            var request = new UnityWebRequest(endpoint, UnityWebRequest.kHttpVerbPOST)
            {
                uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(json)),
                downloadHandler = new DownloadHandlerBuffer(),
                timeout = timeoutSeconds,
                redirectLimit = 0
            };
            request.SetRequestHeader("Content-Type", "application/json");
            request.SetRequestHeader(
                "Accept",
                SpeechContractV1.ContentType + ", application/json");
            return request;
        }

        /// <summary>
        /// Sends a Unity request while preserving cancellation and safe connection failures.
        /// </summary>
        private static async Task SendSafelyAsync(
            UnityWebRequest request,
            CancellationToken cancellationToken)
        {
            try
            {
                await SendRequestAsync(request, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception exception)
            {
                throw CreateUnreachableException(exception);
            }
        }

        /// <summary>
        /// Converts Unity's completion callback into a cancellable Task on the Unity thread.
        /// </summary>
        private static async Task SendRequestAsync(
            UnityWebRequest request,
            CancellationToken cancellationToken)
        {
            var completion = new TaskCompletionSource<bool>();
            var operation = request.SendWebRequest();

            /// <summary>
            /// Completes the awaiting Task when Unity reports the HTTP operation finished.
            /// </summary>
            void OnCompleted(AsyncOperation _)
            {
                completion.TrySetResult(true);
            }

            operation.completed += OnCompleted;
            if (operation.isDone)
            {
                completion.TrySetResult(true);
            }

            using (cancellationToken.Register(
                       () =>
                       {
                           request.Abort();
                           completion.TrySetCanceled();
                       }))
            {
                try
                {
                    await completion.Task;
                }
                catch (TaskCanceledException)
                {
                    throw new OperationCanceledException(cancellationToken);
                }
                finally
                {
                    operation.completed -= OnCompleted;
                }
            }
        }

        /// <summary>
        /// Reads and validates correlation headers and the complete PCM payload.
        /// </summary>
        private static SpeechBackendAudioResponse ReadSuccessResponse(
            UnityWebRequest request)
        {
            if (!HeaderEquals(
                    request,
                    "Content-Type",
                    SpeechContractV1.ContentType)
                || !HeaderEquals(
                    request,
                    SpeechContractV1.VersionHeader,
                    SpeechContractV1.SchemaVersion.ToString())
                || !HeaderEquals(
                    request,
                    SpeechContractV1.AudioFormatHeader,
                    SpeechContractV1.AudioFormat)
                || !HeaderEquals(
                    request,
                    SpeechContractV1.SampleRateHeader,
                    SpeechContractV1.SampleRate)
                || !HeaderEquals(
                    request,
                    SpeechContractV1.ChannelsHeader,
                    SpeechContractV1.Channels))
            {
                throw CreateProtocolException();
            }

            var responseRequestId = request.GetResponseHeader(
                SpeechContractV1.RequestIdHeader);
            var bytes = request.downloadHandler?.data;
            if (string.IsNullOrWhiteSpace(responseRequestId)
                || bytes == null
                || bytes.Length == 0
                || bytes.Length > SpeechAudioData.MaximumByteCount
                || bytes.Length % 2 != 0)
            {
                throw CreateProtocolException();
            }

            return new SpeechBackendAudioResponse(responseRequestId, bytes);
        }

        /// <summary>
        /// Parses one correlated non-success JSON branch into a safe speech exception.
        /// </summary>
        private static void ThrowErrorResponse(
            UnityWebRequest request,
            string expectedRequestId)
        {
            var responseJson = request.downloadHandler?.text ?? string.Empty;
            if (!SpeechJsonCodecV1.TryDeserializeErrorResponse(
                    responseJson,
                    out var response,
                    out _)
                || !string.Equals(
                    response.requestId,
                    expectedRequestId,
                    StringComparison.Ordinal))
            {
                throw CreateProtocolException();
            }

            throw new SpeechSynthesisException(
                response.error.code,
                response.error.message,
                response.error.retryable);
        }

        /// <summary>
        /// Compares a single-value response header without culture-sensitive rules.
        /// </summary>
        private static bool HeaderEquals(
            UnityWebRequest request,
            string name,
            string expectedValue)
        {
            return string.Equals(
                request.GetResponseHeader(name),
                expectedValue,
                StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Restricts speech traffic to explicit HTTP(S) loopback addresses.
        /// </summary>
        private static Uri ParseLoopbackEndpoint(string value)
        {
            if (!Uri.TryCreate(value, UriKind.Absolute, out var uri)
                || (uri.Scheme != Uri.UriSchemeHttp
                    && uri.Scheme != Uri.UriSchemeHttps)
                || !uri.IsLoopback)
            {
                throw new ArgumentException(
                    "Speech backend endpoint must be an absolute HTTP(S) loopback URL.",
                    nameof(value));
            }

            return uri;
        }

        /// <summary>
        /// Converts one completed connection failure into timeout or reachability state.
        /// </summary>
        private static void ThrowConnectionFailure(string error)
        {
            if (!string.IsNullOrWhiteSpace(error)
                && (error.IndexOf("timed out", StringComparison.OrdinalIgnoreCase) >= 0
                    || error.IndexOf("timeout", StringComparison.OrdinalIgnoreCase) >= 0))
            {
                throw new SpeechSynthesisException(
                    SpeechBackendErrorCodes.BackendTimeout,
                    "음성 백엔드 응답 시간이 초과되었습니다.",
                    true);
            }

            throw CreateUnreachableException();
        }

        /// <summary>
        /// Creates a safe local connection failure without platform diagnostics.
        /// </summary>
        private static SpeechSynthesisException CreateUnreachableException(
            Exception innerException = null)
        {
            return new SpeechSynthesisException(
                SpeechBackendErrorCodes.BackendUnreachable,
                "로컬 음성 백엔드에 연결할 수 없습니다.",
                true,
                innerException);
        }

        /// <summary>
        /// Creates a safe response-contract failure without response content.
        /// </summary>
        private static SpeechSynthesisException CreateProtocolException()
        {
            return new SpeechSynthesisException(
                SpeechBackendErrorCodes.BackendProtocolError,
                "음성 백엔드 응답 계약이 올바르지 않습니다.",
                false);
        }
    }
}
