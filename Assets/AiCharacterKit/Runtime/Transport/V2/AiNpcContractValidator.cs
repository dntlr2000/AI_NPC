using System.Text;

namespace AiCharacterKit.Transport.V2
{
    /// <summary>
    /// Validates V2 conversation and reset envelopes without Unity or a JSON library.
    /// </summary>
    public static class AiNpcContractValidator
    {
        /// <summary>
        /// Verifies every required session-aware request field and supported command token.
        /// </summary>
        public static bool TryValidateRequest(
            AiNpcRequestEnvelopeDto request,
            out string error)
        {
            if (request == null)
            {
                error = "Request envelope must not be null.";
                return false;
            }

            if (!TryValidateEnvelopeHeader(
                    request.schemaVersion,
                    request.requestId,
                    out error)
                || !TryValidateSessionId(request.sessionId, out error)
                || !TryValidateCharacter(request.character, out error)
                || !TryRequireText(request.userText, "userText", out error))
            {
                return false;
            }

            if (Encoding.UTF8.GetByteCount(request.userText)
                > AiNpcContractV2.MaxUserTextUtf8Bytes)
            {
                error =
                    $"userText must not exceed {AiNpcContractV2.MaxUserTextUtf8Bytes} UTF-8 bytes.";
                return false;
            }

            error = string.Empty;
            return true;
        }

        /// <summary>
        /// Verifies response correlation, status, and exclusive success or error content.
        /// </summary>
        public static bool TryValidateResponse(
            AiNpcResponseEnvelopeDto response,
            out string error)
        {
            if (response == null)
            {
                error = "Response envelope must not be null.";
                return false;
            }

            if (!TryValidateEnvelopeHeader(
                    response.schemaVersion,
                    response.requestId,
                    out error))
            {
                return false;
            }

            if (response.status == AiNpcContractV2.SuccessStatus)
            {
                if (response.error != null)
                {
                    error = "A success response must not contain error content.";
                    return false;
                }

                return TryValidateSuccessPayload(response.result, out error);
            }

            if (response.status == AiNpcContractV2.ErrorStatus)
            {
                if (response.result != null)
                {
                    error = "An error response must not contain result content.";
                    return false;
                }

                return TryValidateError(response.error, out error);
            }

            error = $"Unsupported response status '{response.status}'.";
            return false;
        }

        /// <summary>
        /// Verifies the identifiers required to reset one character-bound session.
        /// </summary>
        public static bool TryValidateResetRequest(
            AiNpcSessionResetRequestDto request,
            out string error)
        {
            if (request == null)
            {
                error = "Reset request must not be null.";
                return false;
            }

            return TryValidateEnvelopeHeader(
                    request.schemaVersion,
                    request.requestId,
                    out error)
                && TryValidateSessionId(request.sessionId, out error)
                && TryRequireText(request.characterId, "characterId", out error);
        }

        /// <summary>
        /// Verifies a correlated reset acknowledgement or exclusive safe error branch.
        /// </summary>
        public static bool TryValidateResetResponse(
            AiNpcSessionResetResponseDto response,
            out string error)
        {
            if (response == null)
            {
                error = "Reset response must not be null.";
                return false;
            }

            if (!TryValidateEnvelopeHeader(
                    response.schemaVersion,
                    response.requestId,
                    out error))
            {
                return false;
            }

            if (response.status == AiNpcContractV2.SuccessStatus)
            {
                if (response.error != null)
                {
                    error = "A reset success must not contain error content.";
                    return false;
                }

                if (response.result == null || !response.result.reset)
                {
                    error = "A reset success requires result.reset to be true.";
                    return false;
                }

                error = string.Empty;
                return true;
            }

            if (response.status == AiNpcContractV2.ErrorStatus)
            {
                if (response.result != null)
                {
                    error = "A reset error must not contain result content.";
                    return false;
                }

                return TryValidateError(response.error, out error);
            }

            error = $"Unsupported reset response status '{response.status}'.";
            return false;
        }

        /// <summary>
        /// Verifies the shared schema version and correlation identifier.
        /// </summary>
        private static bool TryValidateEnvelopeHeader(
            int schemaVersion,
            string requestId,
            out string error)
        {
            if (schemaVersion != AiNpcContractV2.SchemaVersion)
            {
                error = $"Unsupported schemaVersion '{schemaVersion}'.";
                return false;
            }

            return TryRequireText(requestId, "requestId", out error);
        }

        /// <summary>
        /// Verifies one bounded opaque session identifier.
        /// </summary>
        private static bool TryValidateSessionId(string sessionId, out string error)
        {
            if (!TryRequireText(sessionId, "sessionId", out error))
            {
                return false;
            }

            if (sessionId.Length > AiNpcContractV2.MaxSessionIdLength)
            {
                error =
                    $"sessionId must not exceed {AiNpcContractV2.MaxSessionIdLength} characters.";
                return false;
            }

            error = string.Empty;
            return true;
        }

        /// <summary>
        /// Verifies the complete character snapshot embedded in a request.
        /// </summary>
        private static bool TryValidateCharacter(
            CharacterSnapshotDto character,
            out string error)
        {
            if (character == null)
            {
                error = "character must not be null.";
                return false;
            }

            if (!TryRequireText(character.characterId, "character.characterId", out error)
                || !TryRequireText(character.displayName, "character.displayName", out error)
                || !TryRequireText(character.personality, "character.personality", out error)
                || !TryRequireText(character.speechStyle, "character.speechStyle", out error)
                || !TryRequireText(
                    character.exampleDialogue,
                    "character.exampleDialogue",
                    out error))
            {
                return false;
            }

            if (!NpcCommandTokenConverter.TryParseEmotion(
                    character.defaultEmotion,
                    out _))
            {
                error =
                    $"Unsupported character.defaultEmotion '{character.defaultEmotion}'.";
                return false;
            }

            error = string.Empty;
            return true;
        }

        /// <summary>
        /// Verifies the presentation data carried by a successful response.
        /// </summary>
        private static bool TryValidateSuccessPayload(
            AiNpcResponsePayloadDto result,
            out string error)
        {
            if (result == null)
            {
                error = "A success response requires result content.";
                return false;
            }

            if (!TryRequireText(result.dialogue, "result.dialogue", out error))
            {
                return false;
            }

            if (!NpcCommandTokenConverter.TryParseEmotion(result.emotion, out _))
            {
                error = $"Unsupported result.emotion '{result.emotion}'.";
                return false;
            }

            if (!NpcCommandTokenConverter.TryParseGesture(result.gesture, out _))
            {
                error = $"Unsupported result.gesture '{result.gesture}'.";
                return false;
            }

            error = string.Empty;
            return true;
        }

        /// <summary>
        /// Verifies one extensible snake_case error code and its safe message.
        /// </summary>
        private static bool TryValidateError(AiNpcErrorDto errorContent, out string error)
        {
            if (errorContent == null)
            {
                error = "An error response requires error content.";
                return false;
            }

            if (!IsSnakeCase(errorContent.code))
            {
                error = "error.code must be a non-empty snake_case token.";
                return false;
            }

            return TryRequireText(errorContent.message, "error.message", out error);
        }

        /// <summary>
        /// Verifies that a required wire string contains visible content.
        /// </summary>
        private static bool TryRequireText(
            string value,
            string fieldName,
            out string error)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                error = $"{fieldName} must not be empty.";
                return false;
            }

            error = string.Empty;
            return true;
        }

        /// <summary>
        /// Checks a lowercase ASCII snake_case token without regular expressions.
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
                var isLowercaseLetter = character >= 'a' && character <= 'z';
                var isDigit = character >= '0' && character <= '9';
                var isUnderscore = character == '_';

                if ((!isLowercaseLetter && !isDigit && !isUnderscore)
                    || (isUnderscore && previousWasUnderscore))
                {
                    return false;
                }

                previousWasUnderscore = isUnderscore;
            }

            return true;
        }
    }
}
