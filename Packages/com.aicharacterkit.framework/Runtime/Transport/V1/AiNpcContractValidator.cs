namespace AiCharacterKit.Transport.V1
{
    /// <summary>
    /// Validates V1 envelopes without depending on a JSON library or Unity.
    /// </summary>
    public static class AiNpcContractValidator
    {
        /// <summary>
        /// Verifies every required request field and supported command token.
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

            if (request.schemaVersion != AiNpcContractV1.SchemaVersion)
            {
                error = $"Unsupported schemaVersion '{request.schemaVersion}'.";
                return false;
            }

            if (!TryRequireText(request.requestId, "requestId", out error))
            {
                return false;
            }

            if (!TryValidateCharacter(request.character, out error))
            {
                return false;
            }

            return TryRequireText(request.userText, "userText", out error);
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

            if (response.schemaVersion != AiNpcContractV1.SchemaVersion)
            {
                error = $"Unsupported schemaVersion '{response.schemaVersion}'.";
                return false;
            }

            if (!TryRequireText(response.requestId, "requestId", out error))
            {
                return false;
            }

            if (response.status == AiNpcContractV1.SuccessStatus)
            {
                if (response.error != null)
                {
                    error = "A success response must not contain error content.";
                    return false;
                }

                return TryValidateSuccessPayload(response.result, out error);
            }

            if (response.status == AiNpcContractV1.ErrorStatus)
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
        /// Checks a lowercase ASCII snake_case token without using regular expressions.
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
