using System;
using System.Collections.Generic;
using System.Text;
using AiCharacterKit.Core;

namespace AiCharacterKit.Transport.V4
{
    /// <summary>
    /// Validates V4 grounded conversation and reset envelopes without Unity dependencies.
    /// </summary>
    public static class AiNpcContractValidator
    {
        /// <summary>
        /// Verifies the complete bounded V4 request and deterministic grounding revision.
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
                || !TryValidateGrounding(request.grounding, out error)
                || !TryRequireText(request.userText, "userText", out error))
            {
                return false;
            }

            if (Encoding.UTF8.GetByteCount(request.userText)
                > AiNpcContractV4.MaxUserTextUtf8Bytes)
            {
                error = $"userText must not exceed {AiNpcContractV4.MaxUserTextUtf8Bytes} UTF-8 bytes.";
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

            if (response.status == AiNpcContractV4.SuccessStatus)
            {
                if (response.error != null)
                {
                    error = "A success response must not contain error content.";
                    return false;
                }

                return TryValidateSuccess(response.result, out error);
            }

            if (response.status == AiNpcContractV4.ErrorStatus)
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
        /// Verifies all identifiers required to reset one V4 session.
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
        /// Verifies one correlated V4 reset acknowledgement or safe error.
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

            if (response.status == AiNpcContractV4.SuccessStatus)
            {
                if (response.error != null || response.result == null || !response.result.reset)
                {
                    error = "A reset success requires only result.reset=true.";
                    return false;
                }

                error = string.Empty;
                return true;
            }

            if (response.status == AiNpcContractV4.ErrorStatus)
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
        /// Verifies normalized canon fields, bounded facts, and their content-derived revision.
        /// </summary>
        private static bool TryValidateGrounding(
            NpcGroundingSnapshotDto grounding,
            out string error)
        {
            if (grounding == null)
            {
                error = "grounding must not be null.";
                return false;
            }

            if (!IsValidRevision(grounding.revision))
            {
                error = "grounding.revision must be a canonical ctx SHA-256 value.";
                return false;
            }

            if (!TryValidateOptionalText(
                    grounding.background,
                    NpcGroundingSnapshot.MaxBackgroundUtf8Bytes,
                    "grounding.background",
                    out error)
                || !TryValidateOptionalText(
                    grounding.goalsAndValues,
                    NpcGroundingSnapshot.MaxGoalsAndValuesUtf8Bytes,
                    "grounding.goalsAndValues",
                    out error)
                || !TryValidateTextCollection(
                    grounding.behavioralRules,
                    NpcGroundingSnapshot.MaxBehavioralRuleCount,
                    NpcGroundingSnapshot.MaxBehavioralRuleUtf8Bytes,
                    "grounding.behavioralRules",
                    out error)
                || !TryValidateTextCollection(
                    grounding.dialogueExamples,
                    NpcGroundingSnapshot.MaxDialogueExampleCount,
                    NpcGroundingSnapshot.MaxDialogueExampleUtf8Bytes,
                    "grounding.dialogueExamples",
                    out error))
            {
                return false;
            }

            if (grounding.facts == null
                || grounding.facts.Length > NpcGroundingSnapshot.MaxFactCount)
            {
                error = $"grounding.facts must contain at most {NpcGroundingSnapshot.MaxFactCount} entries.";
                return false;
            }

            var facts = new List<NpcContextFact>(grounding.facts.Length);
            var knownIds = new HashSet<string>(StringComparer.Ordinal);
            var totalBytes = 0;
            for (var index = 0; index < grounding.facts.Length; index++)
            {
                var fact = grounding.facts[index];
                if (fact == null
                    || !NpcTriggerDefinition.IsValidIdentifier(fact.factId)
                    || !knownIds.Add(fact.factId))
                {
                    error = $"grounding.facts[{index}].factId must be a unique valid identifier.";
                    return false;
                }

                if (!NpcTokenConverter.TryParseFactKind(fact.kind, out var kind))
                {
                    error = $"Unsupported grounding.facts[{index}].kind '{fact.kind}'.";
                    return false;
                }

                if (!TryRequireText(
                        fact.statement,
                        $"grounding.facts[{index}].statement",
                        out error))
                {
                    return false;
                }


                if (!string.Equals(
                        fact.statement,
                        NormalizeText(fact.statement),
                        StringComparison.Ordinal))
                {
                    error = $"grounding.facts[{index}].statement must use canonical whitespace and line endings.";
                    return false;
                }

                var statementBytes = Encoding.UTF8.GetByteCount(fact.statement);
                totalBytes += statementBytes;
                if (statementBytes > NpcContextFact.MaxStatementUtf8Bytes
                    || totalBytes > NpcGroundingSnapshot.MaxTotalFactUtf8Bytes)
                {
                    error = "grounding facts exceed their UTF-8 byte budget.";
                    return false;
                }

                if (fact.priority < NpcContextFact.MinPriority
                    || fact.priority > NpcContextFact.MaxPriority)
                {
                    error = $"grounding.facts[{index}].priority is out of range.";
                    return false;
                }

                try
                {
                    facts.Add(new NpcContextFact(
                        fact.factId,
                        kind,
                        fact.statement,
                        fact.priority));
                }
                catch (ArgumentException)
                {
                    error = $"grounding.facts[{index}] is invalid.";
                    return false;
                }
            }

            try
            {
                var snapshot = new NpcGroundingSnapshot(
                    grounding.background,
                    grounding.goalsAndValues,
                    grounding.behavioralRules,
                    grounding.dialogueExamples,
                    facts);
                if (!string.Equals(
                        snapshot.Revision,
                        grounding.revision,
                        StringComparison.Ordinal))
                {
                    error = "grounding.revision does not match the normalized content.";
                    return false;
                }
            }
            catch (ArgumentException)
            {
                error = "grounding contains invalid bounded content.";
                return false;
            }

            error = string.Empty;
            return true;
        }

        /// <summary>
        /// Verifies an optional text field against one UTF-8 limit.
        /// </summary>
        private static bool TryValidateOptionalText(
            string value,
            int maxUtf8Bytes,
            string fieldName,
            out string error)
        {
            if (value == null)
            {
                error = $"{fieldName} must not be null.";
                return false;
            }

            if (Encoding.UTF8.GetByteCount(value) > maxUtf8Bytes)
            {
                error = $"{fieldName} exceeds its UTF-8 byte limit.";
                return false;
            }

            if (!string.Equals(value, NormalizeText(value), StringComparison.Ordinal))
            {
                error = $"{fieldName} must use canonical whitespace and line endings.";
                return false;
            }

            error = string.Empty;
            return true;
        }

        /// <summary>
        /// Verifies one required array of non-empty bounded text values.
        /// </summary>
        private static bool TryValidateTextCollection(
            string[] values,
            int maxCount,
            int maxUtf8Bytes,
            string fieldName,
            out string error)
        {
            if (values == null || values.Length > maxCount)
            {
                error = $"{fieldName} must contain at most {maxCount} entries.";
                return false;
            }

            for (var index = 0; index < values.Length; index++)
            {
                if (!TryRequireText(values[index], $"{fieldName}[{index}]", out error)
                    || Encoding.UTF8.GetByteCount(values[index]) > maxUtf8Bytes)
                {
                    if (string.IsNullOrEmpty(error))
                    {
                        error = $"{fieldName}[{index}] exceeds its UTF-8 byte limit.";
                    }

                    return false;
                }

                if (!string.Equals(
                        values[index],
                        NormalizeText(values[index]),
                        StringComparison.Ordinal))
                {
                    error = $"{fieldName}[{index}] must use canonical whitespace and line endings.";
                    return false;
                }
            }

            error = string.Empty;
            return true;
        }

        /// <summary>
        /// Verifies an optional bounded unique trigger snapshot.
        /// </summary>
        private static bool TryValidateTriggers(
            AiNpcTriggerDto[] triggers,
            out string error)
        {
            if (triggers == null || triggers.Length > AiNpcContractV4.MaxTriggerCount)
            {
                error = $"triggers must contain 0 to {AiNpcContractV4.MaxTriggerCount} entries.";
                return false;
            }

            var ids = new HashSet<string>(StringComparer.Ordinal);
            for (var index = 0; index < triggers.Length; index++)
            {
                var trigger = triggers[index];
                if (trigger == null
                    || !NpcTriggerDefinition.IsValidIdentifier(trigger.triggerId)
                    || !ids.Add(trigger.triggerId))
                {
                    error = $"triggers[{index}].triggerId must be a unique valid identifier.";
                    return false;
                }

                if (!TryRequireText(
                        trigger.conditionDescription,
                        $"triggers[{index}].conditionDescription",
                        out error)
                    || Encoding.UTF8.GetByteCount(trigger.conditionDescription)
                        > AiNpcContractV4.MaxConditionUtf8Bytes)
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

            if (!NpcTokenConverter.TryParseEmotion(result.emotion, out _))
            {
                error = $"Unsupported result.emotion '{result.emotion}'.";
                return false;
            }

            if (!NpcTokenConverter.TryParseGesture(result.gesture, out _))
            {
                error = $"Unsupported result.gesture '{result.gesture}'.";
                return false;
            }

            if (result.matchedTriggerIds == null
                || result.matchedTriggerIds.Length > AiNpcContractV4.MaxTriggerCount)
            {
                error = $"result.matchedTriggerIds must contain 0 to {AiNpcContractV4.MaxTriggerCount} entries.";
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
            if (schemaVersion != AiNpcContractV4.SchemaVersion)
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

            if (sessionId.Length > AiNpcContractV4.MaxSessionIdLength)
            {
                error = $"sessionId must not exceed {AiNpcContractV4.MaxSessionIdLength} characters.";
                return false;
            }

            error = string.Empty;
            return true;
        }

        /// <summary>
        /// Verifies the complete character snapshot embedded in a V4 request.
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

            if (!NpcTokenConverter.TryParseEmotion(character.defaultEmotion, out _))
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
        /// Verifies the exact lowercase content-derived revision format.
        /// </summary>
        private static bool IsValidRevision(string value)
        {
            if (string.IsNullOrEmpty(value)
                || value.Length != 68
                || !value.StartsWith("ctx-", StringComparison.Ordinal))
            {
                return false;
            }

            for (var index = 4; index < value.Length; index++)
            {
                var character = value[index];
                if ((character < '0' || character > '9')
                    && (character < 'a' || character > 'f'))
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// Applies the canonical V4 line-ending and surrounding-whitespace rules.
        /// </summary>
        private static string NormalizeText(string value)
        {
            return (value ?? string.Empty)
                .Replace("\r\n", "\n")
                .Replace('\r', '\n')
                .Trim();
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
