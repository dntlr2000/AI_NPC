using System.Text;
using AiCharacterKit.Transcription;

namespace AiCharacterKit.Transport.Transcription.V1
{
    /// <summary>
    /// Validates Transcription V1 response branches without Unity or a JSON package.
    /// </summary>
    public static class TranscriptionContractValidator
    {
        /// <summary>
        /// Verifies version, correlation, status, and the active response branch.
        /// </summary>
        public static bool TryValidateResponse(
            TranscriptionResponseEnvelopeDto response,
            out string error)
        {
            if (response == null)
            {
                error = "Transcription response must not be null.";
                return false;
            }

            if (response.schemaVersion != TranscriptionContractV1.SchemaVersion)
            {
                error = $"Unsupported schemaVersion '{response.schemaVersion}'.";
                return false;
            }

            if (!TryRequireBoundedText(
                    response.requestId,
                    "requestId",
                    TranscriptionContractV1.MaximumRequestIdLength,
                    out error))
            {
                return false;
            }

            if (response.status == TranscriptionContractV1.SuccessStatus)
            {
                if (response.result == null || response.error != null)
                {
                    error = "A success response requires only result content.";
                    return false;
                }

                if (!TryRequireBoundedText(
                        response.result.text,
                        "result.text",
                        TranscriptionResult.MaximumTextLength,
                        out error))
                {
                    return false;
                }

                if (Encoding.UTF8.GetByteCount(response.result.text)
                    > TranscriptionResult.MaximumTextUtf8Bytes)
                {
                    error = "result.text exceeds the UTF-8 byte limit.";
                    return false;
                }

                error = string.Empty;
                return true;
            }

            if (response.status == TranscriptionContractV1.ErrorStatus)
            {
                if (response.error == null || response.result != null)
                {
                    error = "An error response requires only error content.";
                    return false;
                }

                if (!IsSnakeCase(response.error.code))
                {
                    error = "error.code must be a non-empty snake_case token.";
                    return false;
                }

                return TryRequireBoundedText(
                    response.error.message,
                    "error.message",
                    TranscriptionResult.MaximumTextLength,
                    out error);
            }

            error = $"Unsupported response status '{response.status}'.";
            return false;
        }

        /// <summary>
        /// Checks a lowercase extensible snake_case error token.
        /// </summary>
        private static bool IsSnakeCase(string value)
        {
            if (string.IsNullOrWhiteSpace(value)
                || value[0] < 'a'
                || value[0] > 'z'
                || value[value.Length - 1] == '_')
            {
                return false;
            }

            var previousWasUnderscore = false;
            for (var index = 1; index < value.Length; index++)
            {
                var character = value[index];
                var isLetter = character >= 'a' && character <= 'z';
                var isDigit = character >= '0' && character <= '9';
                var isUnderscore = character == '_';
                if ((!isLetter && !isDigit && !isUnderscore)
                    || (isUnderscore && previousWasUnderscore))
                {
                    return false;
                }

                previousWasUnderscore = isUnderscore;
            }

            return true;
        }

        /// <summary>
        /// Verifies one visible string against its wire character limit.
        /// </summary>
        private static bool TryRequireBoundedText(
            string value,
            string fieldName,
            int maximumLength,
            out string error)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                error = $"{fieldName} must not be empty.";
                return false;
            }

            if (value.Length > maximumLength)
            {
                error = $"{fieldName} exceeds its character limit.";
                return false;
            }

            error = string.Empty;
            return true;
        }
    }
}
