using System;
using System.Collections.Generic;
using AiCharacterKit.Core;

namespace AiCharacterKit.Transport.V4
{
    /// <summary>
    /// Maps pure domain data to and from validated context-grounded V4 envelopes.
    /// </summary>
    public static class AiNpcContractMapper
    {
        /// <summary>
        /// Creates one V4 request containing a grounding snapshot and optional semantic triggers.
        /// </summary>
        public static AiNpcRequestEnvelopeDto CreateRequest(
            AiNpcRequest request,
            string requestId,
            string sessionId,
            IReadOnlyList<NpcTriggerDefinition> definitions)
        {
            if (request == null)
            {
                throw new ArgumentNullException(nameof(request));
            }

            if (definitions == null)
            {
                throw new ArgumentNullException(nameof(definitions));
            }

            NpcTokenConverter.TryToToken(request.DefaultEmotion, out var defaultEmotion);
            var triggers = new AiNpcTriggerDto[definitions.Count];
            for (var index = 0; index < definitions.Count; index++)
            {
                var definition = definitions[index]
                    ?? throw new ArgumentException(
                        "Trigger definitions must not contain null.",
                        nameof(definitions));
                triggers[index] = new AiNpcTriggerDto
                {
                    triggerId = definition.TriggerId,
                    conditionDescription = definition.ConditionDescription
                };
            }

            var envelope = new AiNpcRequestEnvelopeDto
            {
                schemaVersion = AiNpcContractV4.SchemaVersion,
                requestId = requestId ?? string.Empty,
                sessionId = sessionId ?? string.Empty,
                character = new CharacterSnapshotDto
                {
                    characterId = request.CharacterId,
                    displayName = request.DisplayName,
                    personality = request.Personality,
                    speechStyle = request.SpeechStyle,
                    exampleDialogue = request.ExampleDialogue,
                    defaultEmotion = defaultEmotion
                },
                grounding = CreateGrounding(request.Grounding),
                userText = request.UserText,
                triggers = triggers
            };

            if (!AiNpcContractValidator.TryValidateRequest(envelope, out var error))
            {
                throw new ArgumentException(error, nameof(request));
            }

            return envelope;
        }

        /// <summary>
        /// Reconstructs the shared domain request and grounding snapshot from one valid V4 envelope.
        /// </summary>
        public static AiNpcRequest ReadRequest(AiNpcRequestEnvelopeDto envelope)
        {
            if (!AiNpcContractValidator.TryValidateRequest(envelope, out var error))
            {
                throw new ArgumentException(error, nameof(envelope));
            }

            NpcTokenConverter.TryParseEmotion(
                envelope.character.defaultEmotion,
                out var defaultEmotion);
            return new AiNpcRequest(
                envelope.character.characterId,
                envelope.character.displayName,
                envelope.character.personality,
                envelope.character.speechStyle,
                envelope.character.exampleDialogue,
                defaultEmotion,
                envelope.userText,
                ReadGrounding(envelope.grounding));
        }

        /// <summary>
        /// Creates one correlated V4 success response with zero or more matched trigger IDs.
        /// </summary>
        public static AiNpcResponseEnvelopeDto CreateSuccessResponse(
            AiNpcResponse response,
            string requestId)
        {
            if (response == null)
            {
                throw new ArgumentNullException(nameof(response));
            }

            NpcTokenConverter.TryToToken(response.Emotion, out var emotion);
            NpcTokenConverter.TryToToken(response.Gesture, out var gesture);
            var matchedIds = new string[response.MatchedTriggerIds.Count];
            for (var index = 0; index < matchedIds.Length; index++)
            {
                matchedIds[index] = response.MatchedTriggerIds[index];
            }

            var envelope = new AiNpcResponseEnvelopeDto
            {
                schemaVersion = AiNpcContractV4.SchemaVersion,
                requestId = requestId ?? string.Empty,
                status = AiNpcContractV4.SuccessStatus,
                result = new AiNpcResponsePayloadDto
                {
                    dialogue = response.Dialogue,
                    emotion = emotion,
                    gesture = gesture,
                    matchedTriggerIds = matchedIds
                }
            };

            if (!AiNpcContractValidator.TryValidateResponse(envelope, out var error))
            {
                throw new ArgumentException(error, nameof(response));
            }

            return envelope;
        }

        /// <summary>
        /// Creates one validated correlated V4 error response.
        /// </summary>
        public static AiNpcResponseEnvelopeDto CreateErrorResponse(
            string requestId,
            string code,
            string message,
            bool retryable)
        {
            var envelope = new AiNpcResponseEnvelopeDto
            {
                schemaVersion = AiNpcContractV4.SchemaVersion,
                requestId = requestId ?? string.Empty,
                status = AiNpcContractV4.ErrorStatus,
                error = CreateError(code, message, retryable)
            };
            if (!AiNpcContractValidator.TryValidateResponse(envelope, out var error))
            {
                throw new ArgumentException(error, nameof(code));
            }

            return envelope;
        }

        /// <summary>
        /// Reconstructs one action-aware domain response from a V4 success envelope.
        /// </summary>
        public static AiNpcResponse ReadSuccessResponse(
            AiNpcResponseEnvelopeDto envelope)
        {
            if (!AiNpcContractValidator.TryValidateResponse(envelope, out var error))
            {
                throw new ArgumentException(error, nameof(envelope));
            }

            if (envelope.status != AiNpcContractV4.SuccessStatus)
            {
                throw new InvalidOperationException(
                    "Only success envelopes map to AiNpcResponse.");
            }

            NpcTokenConverter.TryParseEmotion(envelope.result.emotion, out var emotion);
            NpcTokenConverter.TryParseGesture(envelope.result.gesture, out var gesture);
            return new AiNpcResponse(
                envelope.result.dialogue,
                emotion,
                gesture,
                envelope.result.matchedTriggerIds);
        }

        /// <summary>
        /// Creates one validated V4 session reset request.
        /// </summary>
        public static AiNpcSessionResetRequestDto CreateResetRequest(
            string requestId,
            string sessionId,
            string characterId)
        {
            var request = new AiNpcSessionResetRequestDto
            {
                schemaVersion = AiNpcContractV4.SchemaVersion,
                requestId = requestId ?? string.Empty,
                sessionId = sessionId ?? string.Empty,
                characterId = characterId ?? string.Empty
            };
            if (!AiNpcContractValidator.TryValidateResetRequest(request, out var error))
            {
                throw new ArgumentException(error, nameof(sessionId));
            }

            return request;
        }

        /// <summary>
        /// Creates one canonical V4 reset success response.
        /// </summary>
        public static AiNpcSessionResetResponseDto CreateResetSuccessResponse(string requestId)
        {
            var response = new AiNpcSessionResetResponseDto
            {
                schemaVersion = AiNpcContractV4.SchemaVersion,
                requestId = requestId ?? string.Empty,
                status = AiNpcContractV4.SuccessStatus,
                result = new AiNpcSessionResetResultDto { reset = true }
            };
            if (!AiNpcContractValidator.TryValidateResetResponse(response, out var error))
            {
                throw new ArgumentException(error, nameof(requestId));
            }

            return response;
        }

        /// <summary>
        /// Creates one canonical V4 reset error response.
        /// </summary>
        public static AiNpcSessionResetResponseDto CreateResetErrorResponse(
            string requestId,
            string code,
            string message,
            bool retryable)
        {
            var response = new AiNpcSessionResetResponseDto
            {
                schemaVersion = AiNpcContractV4.SchemaVersion,
                requestId = requestId ?? string.Empty,
                status = AiNpcContractV4.ErrorStatus,
                error = CreateError(code, message, retryable)
            };
            if (!AiNpcContractValidator.TryValidateResetResponse(response, out var error))
            {
                throw new ArgumentException(error, nameof(code));
            }

            return response;
        }

        /// <summary>
        /// Maps an immutable grounding snapshot to its serializable V4 representation.
        /// </summary>
        private static NpcGroundingSnapshotDto CreateGrounding(
            NpcGroundingSnapshot snapshot)
        {
            var safeSnapshot = snapshot ?? NpcGroundingSnapshot.Empty;
            var rules = CopyStrings(safeSnapshot.BehavioralRules);
            var examples = CopyStrings(safeSnapshot.DialogueExamples);
            var facts = new NpcContextFactDto[safeSnapshot.Facts.Count];
            for (var index = 0; index < facts.Length; index++)
            {
                var source = safeSnapshot.Facts[index];
                NpcTokenConverter.TryToToken(source.Kind, out var kind);
                facts[index] = new NpcContextFactDto
                {
                    factId = source.FactId,
                    kind = kind,
                    statement = source.Statement,
                    priority = source.Priority
                };
            }

            return new NpcGroundingSnapshotDto
            {
                revision = safeSnapshot.Revision,
                background = safeSnapshot.Background,
                goalsAndValues = safeSnapshot.GoalsAndValues,
                behavioralRules = rules,
                dialogueExamples = examples,
                facts = facts
            };
        }

        /// <summary>
        /// Reconstructs one immutable grounding snapshot from validated V4 data.
        /// </summary>
        private static NpcGroundingSnapshot ReadGrounding(
            NpcGroundingSnapshotDto grounding)
        {
            var facts = new NpcContextFact[grounding.facts.Length];
            for (var index = 0; index < facts.Length; index++)
            {
                var source = grounding.facts[index];
                NpcTokenConverter.TryParseFactKind(source.kind, out var kind);
                facts[index] = new NpcContextFact(
                    source.factId,
                    kind,
                    source.statement,
                    source.priority);
            }

            return new NpcGroundingSnapshot(
                grounding.background,
                grounding.goalsAndValues,
                grounding.behavioralRules,
                grounding.dialogueExamples,
                facts);
        }

        /// <summary>
        /// Copies one immutable text list to a wire-friendly array.
        /// </summary>
        private static string[] CopyStrings(IReadOnlyList<string> values)
        {
            var result = new string[values.Count];
            for (var index = 0; index < result.Length; index++)
            {
                result[index] = values[index];
            }

            return result;
        }

        /// <summary>
        /// Builds the shared V4 error branch.
        /// </summary>
        private static AiNpcErrorDto CreateError(
            string code,
            string message,
            bool retryable)
        {
            return new AiNpcErrorDto
            {
                code = code ?? string.Empty,
                message = message ?? string.Empty,
                retryable = retryable
            };
        }
    }
}
