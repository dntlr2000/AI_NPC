using System;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using AiCharacterKit.Core;
using AiCharacterKit.Transport.V2;
using AiCharacterKit.Unity.Transport;
using UnityEngine;
using UnityEngine.Networking;

namespace AiCharacterKit.Unity.Networking
{
    /// <summary>
    /// Sends V2 conversation and reset JSON to explicit loopback endpoints.
    /// </summary>
    public sealed class UnityWebRequestAiNpcSessionBackendGateway
        : IAiNpcSessionBackendGateway
    {
        private readonly Uri respondEndpoint;
        private readonly Uri resetEndpoint;
        private readonly int timeoutSeconds;

        /// <summary>
        /// Creates a local-only V2 gateway with a positive timeout.
        /// </summary>
        public UnityWebRequestAiNpcSessionBackendGateway(
            string respondEndpoint,
            string resetEndpoint,
            int timeoutSeconds)
        {
            this.respondEndpoint = ParseLoopbackEndpoint(
                respondEndpoint,
                nameof(respondEndpoint));
            this.resetEndpoint = ParseLoopbackEndpoint(
                resetEndpoint,
                nameof(resetEndpoint));
            if (timeoutSeconds <= 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(timeoutSeconds),
                    "Backend timeout must be greater than zero.");
            }

            this.timeoutSeconds = timeoutSeconds;
        }

        /// <summary>
        /// Posts one V2 conversation request and validates its HTTP and contract branches.
        /// </summary>
        public async Task<AiNpcResponseEnvelopeDto> SendAsync(
            AiNpcRequestEnvelopeDto requestEnvelope,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!AiNpcJsonCodecV2.TrySerializeRequest(
                    requestEnvelope,
                    out var requestJson,
                    out _))
            {
                throw CreateProtocolException();
            }

            using (var request = CreateRequest(respondEndpoint, requestJson))
            {
                await SendSafelyAsync(request, cancellationToken);
                cancellationToken.ThrowIfCancellationRequested();
                var responseJson = request.downloadHandler?.text ?? string.Empty;
                if (AiNpcJsonCodecV2.TryDeserializeResponse(
                        responseJson,
                        out var responseEnvelope,
                        out _))
                {
                    ValidateHttpBranch(request.responseCode, responseEnvelope.status);
                    return responseEnvelope;
                }

                ThrowTransportOrProtocolFailure(request);
                return null;
            }
        }

        /// <summary>
        /// Posts one V2 reset request and validates its HTTP and acknowledgement branches.
        /// </summary>
        public async Task<AiNpcSessionResetResponseDto> ResetAsync(
            AiNpcSessionResetRequestDto requestEnvelope,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!AiNpcJsonCodecV2.TrySerializeResetRequest(
                    requestEnvelope,
                    out var requestJson,
                    out _))
            {
                throw CreateProtocolException();
            }

            using (var request = CreateRequest(resetEndpoint, requestJson))
            {
                await SendSafelyAsync(request, cancellationToken);
                cancellationToken.ThrowIfCancellationRequested();
                var responseJson = request.downloadHandler?.text ?? string.Empty;
                if (AiNpcJsonCodecV2.TryDeserializeResetResponse(
                        responseJson,
                        out var responseEnvelope,
                        out _))
                {
                    ValidateHttpBranch(request.responseCode, responseEnvelope.status);
                    return responseEnvelope;
                }

                ThrowTransportOrProtocolFailure(request);
                return null;
            }
        }

        /// <summary>
        /// Creates one credential-free JSON POST to the selected loopback endpoint.
        /// </summary>
        private UnityWebRequest CreateRequest(Uri endpoint, string json)
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
        /// Sends a Unity request while preserving cancellation and safe connection errors.
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
        /// Restricts a configured V2 endpoint to explicit HTTP(S) loopback addresses.
        /// </summary>
        private static Uri ParseLoopbackEndpoint(string value, string parameterName)
        {
            if (!Uri.TryCreate(value, UriKind.Absolute, out var uri)
                || (uri.Scheme != Uri.UriSchemeHttp
                    && uri.Scheme != Uri.UriSchemeHttps)
                || !uri.IsLoopback)
            {
                throw new ArgumentException(
                    "Backend endpoint must be an absolute HTTP(S) loopback URL.",
                    parameterName);
            }

            return uri;
        }

        /// <summary>
        /// Enforces success-on-2xx and error-on-non-2xx response semantics.
        /// </summary>
        private static void ValidateHttpBranch(long responseCode, string status)
        {
            var isSuccessStatusCode = responseCode >= 200 && responseCode < 300;
            if ((isSuccessStatusCode && status != AiNpcContractV2.SuccessStatus)
                || (!isSuccessStatusCode && status != AiNpcContractV2.ErrorStatus))
            {
                throw CreateProtocolException();
            }
        }

        /// <summary>
        /// Converts an unparseable transport result into timeout, reachability, or protocol failure.
        /// </summary>
        private static void ThrowTransportOrProtocolFailure(UnityWebRequest request)
        {
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

        /// <summary>
        /// Detects Unity's platform-dependent timeout text without exposing it.
        /// </summary>
        private static bool IsTimeout(string error)
        {
            return !string.IsNullOrWhiteSpace(error)
                && (error.IndexOf("timed out", StringComparison.OrdinalIgnoreCase) >= 0
                    || error.IndexOf("timeout", StringComparison.OrdinalIgnoreCase) >= 0);
        }

        /// <summary>
        /// Creates a safe local connection failure without platform diagnostics.
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
        /// Creates a safe response-contract failure without response content.
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
