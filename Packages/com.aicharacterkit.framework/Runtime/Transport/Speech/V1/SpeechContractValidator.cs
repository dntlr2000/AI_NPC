using System.Text;

namespace AiCharacterKit.Transport.Speech.V1
{
    /// <summary>
    /// Validates Speech V1 JSON values without depending on Unity or a JSON package.
    /// </summary>
    public static class SpeechContractValidator
    {
        /// <summary>
        /// Verifies request correlation, preset token, and bounded synthesis text.
        /// </summary>
        public static bool TryValidateRequest(
            SpeechSynthesisRequestDto request,
            out string error)
        {
            if (request == null)
            {
                error = "Speech request must not be null.";
                return false;
            }

            if (request.schemaVersion != SpeechContractV1.SchemaVersion)
            {
                error = $"Unsupported schemaVersion '{request.schemaVersion}'.";
                return false;
            }

            if (!TryRequireBoundedText(
                    request.requestId,
                    "requestId",
                    SpeechContractV1.MaximumRequestIdLength,
                    out error))
            {
                return false;
            }

            if (!TryValidateVoicePresetId(request.voicePresetId, out error))
            {
                return false;
            }

            if (!TryRequireBoundedText(
                    request.text,
                    "text",
                    SpeechContractV1.MaximumTextLength,
                    out error))
            {
                return false;
            }

            if (Encoding.UTF8.GetByteCount(request.text)
                > SpeechContractV1.MaximumTextUtf8Bytes)
            {
                error = "text exceeds the UTF-8 byte limit.";
                return false;
            }

            error = string.Empty;
            return true;
        }

        /// <summary>
        /// Verifies one correlated JSON error returned instead of binary speech.
        /// </summary>
        public static bool TryValidateErrorResponse(
            SpeechErrorResponseDto response,
            out string error)
        {
            if (response == null)
            {
                error = "Speech error response must not be null.";
                return false;
            }

            if (response.schemaVersion != SpeechContractV1.SchemaVersion)
            {
                error = $"Unsupported schemaVersion '{response.schemaVersion}'.";
                return false;
            }

            if (!TryRequireBoundedText(
                    response.requestId,
                    "requestId",
                    SpeechContractV1.MaximumRequestIdLength,
                    out error))
            {
                return false;
            }

            if (response.status != SpeechContractV1.ErrorStatus)
            {
                error = $"Unsupported response status '{response.status}'.";
                return false;
            }

            if (response.error == null)
            {
                error = "An error response requires error content.";
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
                SpeechContractV1.MaximumTextLength,
                out error);
        }

        /// <summary>
        /// Checks the stable lowercase hyphenated preset identifier grammar.
        /// </summary>
        public static bool TryValidateVoicePresetId(
            string value,
            out string error)
        {
            if (string.IsNullOrWhiteSpace(value)
                || value.Length > SpeechContractV1.MaximumVoicePresetIdLength
                || value[0] == '-'
                || value[value.Length - 1] == '-')
            {
                error =
                    "voicePresetId must be a lowercase token containing letters, digits, or single hyphens.";
                return false;
            }

            var previousWasHyphen = false;
            for (var index = 0; index < value.Length; index++)
            {
                var character = value[index];
                var isLetter = character >= 'a' && character <= 'z';
                var isDigit = character >= '0' && character <= '9';
                var isHyphen = character == '-';
                if ((!isLetter && !isDigit && !isHyphen)
                    || (isHyphen && previousWasHyphen))
                {
                    error =
                        "voicePresetId must be a lowercase token containing letters, digits, or single hyphens.";
                    return false;
                }

                previousWasHyphen = isHyphen;
            }

            error = string.Empty;
            return true;
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
        /// Verifies one visible string against its wire length limit.
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
