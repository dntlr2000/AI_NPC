using System;
using System.Threading;
using System.Threading.Tasks;
using AiCharacterKit.Transcription;
using AiCharacterKit.Transport.Transcription.V1;
using AiCharacterKit.Unity.Transport;
using UnityEngine;
using UnityEngine.Networking;

namespace AiCharacterKit.Unity.Networking
{
    /// <summary>
    /// Posts one bounded WAV to an explicit loopback transcription endpoint.
    /// </summary>
    public sealed class UnityWebRequestAiTranscriptionBackendGateway
        : IAiTranscriptionBackendGateway
    {
        private readonly Uri endpoint;
        private readonly int timeoutSeconds;

        /// <summary>
        /// Creates a credential-free local gateway with a positive timeout.
        /// </summary>
        public UnityWebRequestAiTranscriptionBackendGateway(
            string endpoint,
            int timeoutSeconds)
        {
            this.endpoint = ParseLoopbackEndpoint(endpoint);
            if (timeoutSeconds <= 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(timeoutSeconds),
                    "Transcription backend timeout must be greater than zero.");
            }

            this.timeoutSeconds = timeoutSeconds;
        }

        /// <summary>
        /// Sends one complete WAV and accepts only a valid correlated JSON response.
        /// </summary>
        public async Task<TranscriptionResponseEnvelopeDto> SendAsync(
            byte[] waveBytes,
            string requestId,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (waveBytes == null
                || waveBytes.Length < Pcm16WaveEncoder.HeaderByteCount
                || waveBytes.Length > CapturedAudioData.MaximumWaveByteCount
                || string.IsNullOrWhiteSpace(requestId)
                || requestId.Length > TranscriptionContractV1.MaximumRequestIdLength)
            {
                throw CreateProtocolException();
            }

            using (var request = CreateRequest(waveBytes, requestId))
            {
                await SendSafelyAsync(request, cancellationToken);
                cancellationToken.ThrowIfCancellationRequested();

                if (request.result == UnityWebRequest.Result.ConnectionError)
                {
                    ThrowConnectionFailure(request.error);
                }

                var responseJson = request.downloadHandler?.text ?? string.Empty;
                if (!TranscriptionJsonCodecV1.TryDeserializeResponse(
                        responseJson,
                        out var response,
                        out _)
                    || !IsJsonContentType(
                        request.GetResponseHeader("Content-Type")))
                {
                    throw CreateProtocolException();
                }

                var isHttpSuccess = request.responseCode >= 200
                    && request.responseCode < 300;
                var isContractSuccess = response.status
                    == TranscriptionContractV1.SuccessStatus;
                if (isHttpSuccess != isContractSuccess)
                {
                    throw CreateProtocolException();
                }

                return response;
            }
        }

        /// <summary>
        /// Creates one raw WAV POST with explicit contract and correlation headers.
        /// </summary>
        private UnityWebRequest CreateRequest(byte[] waveBytes, string requestId)
        {
            var request = new UnityWebRequest(endpoint, UnityWebRequest.kHttpVerbPOST)
            {
                uploadHandler = new UploadHandlerRaw(waveBytes),
                downloadHandler = new DownloadHandlerBuffer(),
                timeout = timeoutSeconds,
                redirectLimit = 0
            };
            request.SetRequestHeader(
                "Content-Type",
                TranscriptionContractV1.ContentType);
            request.SetRequestHeader("Accept", "application/json");
            request.SetRequestHeader(
                TranscriptionContractV1.VersionHeader,
                TranscriptionContractV1.SchemaVersion.ToString());
            request.SetRequestHeader(
                TranscriptionContractV1.RequestIdHeader,
                requestId);
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
        /// Restricts transcription traffic to explicit HTTP(S) loopback addresses.
        /// </summary>
        private static Uri ParseLoopbackEndpoint(string value)
        {
            if (!Uri.TryCreate(value, UriKind.Absolute, out var uri)
                || (uri.Scheme != Uri.UriSchemeHttp
                    && uri.Scheme != Uri.UriSchemeHttps)
                || !uri.IsLoopback)
            {
                throw new ArgumentException(
                    "Transcription endpoint must be an absolute HTTP(S) loopback URL.",
                    nameof(value));
            }

            return uri;
        }

        /// <summary>
        /// Accepts JSON media types with an optional charset parameter.
        /// </summary>
        private static bool IsJsonContentType(string value)
        {
            return !string.IsNullOrWhiteSpace(value)
                && value.StartsWith(
                    "application/json",
                    StringComparison.OrdinalIgnoreCase);
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
                throw new TranscriptionException(
                    TranscriptionBackendErrorCodes.BackendTimeout,
                    "음성 전사 백엔드 응답 시간이 초과되었습니다.",
                    true);
            }

            throw CreateUnreachableException();
        }

        /// <summary>
        /// Creates a safe local connection failure without platform diagnostics.
        /// </summary>
        private static TranscriptionException CreateUnreachableException(
            Exception innerException = null)
        {
            return new TranscriptionException(
                TranscriptionBackendErrorCodes.BackendUnreachable,
                "로컬 음성 전사 백엔드에 연결할 수 없습니다.",
                true,
                innerException);
        }

        /// <summary>
        /// Creates a safe response-contract failure without response content.
        /// </summary>
        private static TranscriptionException CreateProtocolException()
        {
            return new TranscriptionException(
                TranscriptionBackendErrorCodes.BackendProtocolError,
                "음성 전사 백엔드 응답 계약이 올바르지 않습니다.",
                false);
        }
    }
}
