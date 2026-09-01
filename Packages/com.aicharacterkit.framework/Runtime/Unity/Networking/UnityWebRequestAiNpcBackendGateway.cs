using System;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using AiCharacterKit.Core;
using AiCharacterKit.Transport.V1;
using AiCharacterKit.Unity.Transport;
using UnityEngine;
using UnityEngine.Networking;

namespace AiCharacterKit.Unity.Networking
{
    /// <summary>
    /// Sends contract V1 JSON to a loopback backend with UnityWebRequest.
    /// </summary>
    public sealed class UnityWebRequestAiNpcBackendGateway : IAiNpcBackendGateway
    {
        private readonly Uri endpoint;
        private readonly int timeoutSeconds;

        /// <summary>
        /// Creates a local-only HTTP gateway with a positive request timeout.
        /// </summary>
        public UnityWebRequestAiNpcBackendGateway(
            string endpoint,
            int timeoutSeconds)
        {
            this.endpoint = ParseLoopbackEndpoint(endpoint);
            if (timeoutSeconds <= 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(timeoutSeconds),
                    "Backend timeout must be greater than zero.");
            }

            this.timeoutSeconds = timeoutSeconds;
        }

        /// <summary>
        /// Posts one request and accepts only a matching V1 success or error HTTP branch.
        /// </summary>
        public async Task<AiNpcResponseEnvelopeDto> SendAsync(
            AiNpcRequestEnvelopeDto requestEnvelope,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!AiNpcJsonCodec.TrySerializeRequest(
                    requestEnvelope,
                    out var requestJson,
                    out _))
            {
                throw CreateProtocolException();
            }

            using (var request = CreateRequest(requestJson))
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

                cancellationToken.ThrowIfCancellationRequested();
                var responseJson = request.downloadHandler?.text ?? string.Empty;
                if (AiNpcJsonCodec.TryDeserializeResponse(
                        responseJson,
                        out var responseEnvelope,
                        out _))
                {
                    ValidateHttpBranch(request.responseCode, responseEnvelope);
                    return responseEnvelope;
                }

                if (request.result == UnityWebRequest.Result.ConnectionError)
                {
                    if (IsTimeout(request.error))
                    {
                        throw new AiConversationException(
                            AiNpcBackendErrorCodes.BackendTimeout,
                            "NPC 백엔드 응답 시간이 초과되었습니다.",
                            true);
                    }

                    throw CreateUnreachableException();
                }

                throw CreateProtocolException();
            }
        }

        /// <summary>
        /// Creates one JSON POST without credentials or application-global state.
        /// </summary>
        private UnityWebRequest CreateRequest(string json)
        {
            var request = new UnityWebRequest(endpoint, UnityWebRequest.kHttpVerbPOST)
            {
                uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(json)),
                downloadHandler = new DownloadHandlerBuffer(),
                timeout = timeoutSeconds
            };
            request.SetRequestHeader("Content-Type", "application/json");
            request.SetRequestHeader("Accept", "application/json");
            return request;
        }

        /// <summary>
        /// Converts Unity's callback operation into a cancellable Task on the Unity thread.
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
        /// Restricts this prototype client to explicit HTTP(S) loopback endpoints.
        /// </summary>
        private static Uri ParseLoopbackEndpoint(string value)
        {
            if (!Uri.TryCreate(value, UriKind.Absolute, out var uri)
                || (uri.Scheme != Uri.UriSchemeHttp
                    && uri.Scheme != Uri.UriSchemeHttps)
                || !uri.IsLoopback)
            {
                throw new ArgumentException(
                    "Backend endpoint must be an absolute HTTP(S) loopback URL.",
                    nameof(value));
            }

            return uri;
        }

        /// <summary>
        /// Enforces success-on-2xx and error-on-non-2xx response semantics.
        /// </summary>
        private static void ValidateHttpBranch(
            long responseCode,
            AiNpcResponseEnvelopeDto response)
        {
            var isSuccessStatusCode = responseCode >= 200 && responseCode < 300;
            if ((isSuccessStatusCode
                    && response.status != AiNpcContractV1.SuccessStatus)
                || (!isSuccessStatusCode
                    && response.status != AiNpcContractV1.ErrorStatus))
            {
                throw CreateProtocolException();
            }
        }

        /// <summary>
        /// Detects Unity's platform-dependent timeout error text without exposing it.
        /// </summary>
        private static bool IsTimeout(string error)
        {
            return !string.IsNullOrWhiteSpace(error)
                && (error.IndexOf(
                        "timed out",
                        StringComparison.OrdinalIgnoreCase) >= 0
                    || error.IndexOf(
                        "timeout",
                        StringComparison.OrdinalIgnoreCase) >= 0);
        }

        /// <summary>
        /// Creates a safe local connection failure without leaking platform diagnostics.
        /// </summary>
        private static AiConversationException CreateUnreachableException(
            Exception innerException = null)
        {
            return new AiConversationException(
                AiNpcBackendErrorCodes.BackendUnreachable,
                "로컬 NPC 백엔드에 연결할 수 없습니다.",
                true,
                innerException);
        }

        /// <summary>
        /// Creates a safe response-contract failure without leaking response content.
        /// </summary>
        private static AiConversationException CreateProtocolException()
        {
            return new AiConversationException(
                AiNpcBackendErrorCodes.BackendProtocolError,
                "백엔드 응답 계약이 올바르지 않습니다.",
                false);
        }
    }
}
