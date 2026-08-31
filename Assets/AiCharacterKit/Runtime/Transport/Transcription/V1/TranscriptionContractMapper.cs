using System;
using AiCharacterKit.Transcription;

namespace AiCharacterKit.Transport.Transcription.V1
{
    /// <summary>
    /// Maps provider-neutral transcription results and failures to Transcription V1 DTOs.
    /// </summary>
    public static class TranscriptionContractMapper
    {
        /// <summary>
        /// Creates one canonical success response for deterministic serialization.
        /// </summary>
        public static TranscriptionResponseEnvelopeDto CreateSuccessResponse(
            string requestId,
            TranscriptionResult result)
        {
            if (result == null)
            {
                throw new ArgumentNullException(nameof(result));
            }

            var response = new TranscriptionResponseEnvelopeDto
            {
                schemaVersion = TranscriptionContractV1.SchemaVersion,
                requestId = requestId,
                status = TranscriptionContractV1.SuccessStatus,
                result = new TranscriptionResultDto { text = result.Text }
            };
            RequireValid(response);
            return response;
        }

        /// <summary>
        /// Creates one canonical safe error response for deterministic serialization.
        /// </summary>
        public static TranscriptionResponseEnvelopeDto CreateErrorResponse(
            string requestId,
            TranscriptionException exception)
        {
            if (exception == null)
            {
                throw new ArgumentNullException(nameof(exception));
            }

            var response = new TranscriptionResponseEnvelopeDto
            {
                schemaVersion = TranscriptionContractV1.SchemaVersion,
                requestId = requestId,
                status = TranscriptionContractV1.ErrorStatus,
                error = new TranscriptionErrorDto
                {
                    code = exception.Code,
                    message = exception.Message,
                    retryable = exception.Retryable
                }
            };
            RequireValid(response);
            return response;
        }

        /// <summary>
        /// Reads one validated success result into the provider-neutral model.
        /// </summary>
        public static TranscriptionResult ReadResult(
            TranscriptionResponseEnvelopeDto response)
        {
            RequireValid(response);
            if (response.status != TranscriptionContractV1.SuccessStatus)
            {
                throw new ArgumentException(
                    "Transcription response is not a success branch.",
                    nameof(response));
            }

            return new TranscriptionResult(response.result.text);
        }

        /// <summary>
        /// Reads one validated error branch into a safe provider-neutral exception.
        /// </summary>
        public static TranscriptionException ReadError(
            TranscriptionResponseEnvelopeDto response)
        {
            RequireValid(response);
            if (response.status != TranscriptionContractV1.ErrorStatus)
            {
                throw new ArgumentException(
                    "Transcription response is not an error branch.",
                    nameof(response));
            }

            return new TranscriptionException(
                response.error.code,
                response.error.message,
                response.error.retryable);
        }

        /// <summary>
        /// Converts validation failure into an argument error at the mapping boundary.
        /// </summary>
        private static void RequireValid(TranscriptionResponseEnvelopeDto response)
        {
            if (!TranscriptionContractValidator.TryValidateResponse(
                    response,
                    out var error))
            {
                throw new ArgumentException(error, nameof(response));
            }
        }
    }
}
