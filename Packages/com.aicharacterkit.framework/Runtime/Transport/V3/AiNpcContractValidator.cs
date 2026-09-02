using System;
using System.Collections.Generic;
using System.Text;
using AiCharacterKit.Core;

namespace AiCharacterKit.Transport.V3
{
    /// <summary>
    /// Validates V3 action-aware conversation and reset envelopes without Unity.
    /// </summary>
    public static class AiNpcContractValidator
    {
        /// <summary>
        /// Verifies required session, character, user text, and bounded trigger fields.
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

            if (!TryValidateHeader(request.schemaVersion, request.requestId, out error)
                || !TryValidateSessionId(request.sessionId, out error)
                || !TryValidateCharacter(request.character, out error)
                || !TryRequireText(request.userText, "userText", out error))
            {
                return false;
            }

            if (Encoding.UTF8.GetByteCount(request.userText)
                > AiNpcContractV3.MaxUserTextUtf8Bytes)
            {
                error = $"userText must not exceed {AiNpcContractV3.MaxUserTextUtf8Bytes} UTF-8 bytes.";
                return false;
            }

            return TryValidateTriggers(request.triggers, out error);
        }

        /// <summary>
        /// Verifies response correlation and its exclusive success or error branch.
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

            if (!TryValidateHeader(response.schemaVersion, response.requestId, out error))
            {
                return false;
            }

            if (response.status == AiNpcContractV3.SuccessStatus)
            {
                if (response.error != null)
                {
                    error = "A success response must not contain error content.";
                    return false;
                }

                return TryValidateSuccess(response.result, out error);
            }

            if (response.status == AiNpcContractV3.ErrorStatus)
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
        /// Verifies all identifiers required to reset one V3 session.
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

            return TryValidateHeader(request.schemaVersion, request.requestId, out error)
                && TryValidateSessionId(request.sessionId, out error)
                && TryRequireText(request.characterId, "characterId", out error);
        }

        /// <summary>
        /// Verifies one correlated V3 reset acknowledgement or safe error.
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

            if (!TryValidateHeader(response.schemaVersion, response.requestId, out error))
            {
                return false;
            }

            if (response.status == AiNpcContractV3.SuccessStatus)
            {
                if (response.error != null || response.result == null || !response.result.reset)
                {
                    error = "A reset success requires only result.reset=true.";
                    return false;
                }

                error = string.Empty;
                return true;
            }

            if (response.status == AiNpcContractV3.ErrorStatus)
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
        /// Verifies a bounded non-empty unique trigger snapshot.
        /// </summary>
        private static bool TryValidateTriggers(
            AiNpcTriggerDto[] triggers,
            out string error)
        {
            if (triggers == null || triggers.Length == 0
                || triggers.Length > AiNpcContractV3.MaxTriggerCount)
            {
                error = $"triggers must contain 1 to {AiNpcContractV3.MaxTriggerCount} entries.";
                return false;
            }

            var ids = new HashSet<string>(StringComparer.Ordinal);
            for (var index = 0; index < triggers.Length; index++)
            {
                var trigger = triggers[index];
                if (trigger == null
                    || !NpcTriggerDefinition.IsValidIdentifier(trigger.triggerId))
                {
                    error = $"triggers[{index}].triggerId is invalid.";
                    return false;
                }

                if (!ids.Add(trigger.triggerId))
                {
                    error = $"Duplicate triggerId '{trigger.triggerId}'.";
                    return false;
                }

                if (!TryRequireText(
                        trigger.conditionDescription,
                        $"triggers[{index}].conditionDescription",
                        out error)
                    || Encoding.UTF8.GetByteCount(trigger.conditionDescription)
                        > AiNpcContractV3.MaxConditionUtf8Bytes)
                {
                    if (string.IsNullOrEmpty(error))
                    {
                        error = $"triggers[{index}].conditionDescription exceeds its UTF-8 limit.";
                    }

                    return false;
                }
            }

            error = string.Empty;
            return true;
        }

        /// <summary>
        /// Verifies dialogue commands and unique bounded matched trigger IDs.
        /// </summary>
        private static bool TryValidateSuccess(
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

            if (result.matchedTriggerIds == null
                || result.matchedTriggerIds.Length > AiNpcContractV3.MaxTriggerCount)
            {
                error = "result.matchedTriggerIds is required and exceeds no trigger bound.";
                return false;
            }

            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (var triggerId in result.matchedTriggerIds)
            {
                if (!NpcTriggerDefinition.IsValidIdentifier(triggerId)
                    || !ids.Add(triggerId))
                {
                    error = "result.matchedTriggerIds must contain unique valid trigger IDs.";
                    return false;
                }
            }

            error = string.Empty;
            return true;
        }

        /// <summary>
        /// Verifies the shared schema version and request correlation ID.
        /// </summary>
        private static bool TryValidateHeader(
            int schemaVersion,
            string requestId,
            out string error)
        {
            if (schemaVersion != AiNpcContractV3.SchemaVersion)
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

            if (sessionId.Length > AiNpcContractV3.MaxSessionIdLength)
            {
                error = $"sessionId must not exceed {AiNpcContractV3.MaxSessionIdLength} characters.";
                return false;
            }

            error = string.Empty;
            return true;
        }

        /// <summary>
        /// Verifies the complete character snapshot embedded in a V3 request.
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
                || !TryRequireText(character.exampleDialogue, "character.exampleDialogue", out error))
            {
                return false;
            }

            if (!NpcCommandTokenConverter.TryParseEmotion(
                    character.defaultEmotion,
                    out _))
            {
                error = $"Unsupported character.defaultEmotion '{character.defaultEmotion}'.";
                return false;
            }

            error = string.Empty;
            return true;
        }

        /// <summary>
        /// Verifies an extensible snake_case error code and safe message.
        /// </summary>
        private static bool TryValidateError(AiNpcErrorDto value, out string error)
        {
            if (value == null || !NpcTriggerDefinition.IsValidIdentifier(value.code))
            {
                error = "error.code must be a non-empty snake_case token.";
                return false;
            }

            return TryRequireText(value.message, "error.message", out error);
        }

        /// <summary>
        /// Verifies that one required wire field contains visible text.
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
    }
}
