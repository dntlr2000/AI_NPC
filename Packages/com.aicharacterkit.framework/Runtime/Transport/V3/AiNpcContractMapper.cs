using System;
using System.Collections.Generic;
using AiCharacterKit.Core;

namespace AiCharacterKit.Transport.V3
{
    /// <summary>
    /// Maps pure domain data to and from validated action-aware V3 envelopes.
    /// </summary>
    public static class AiNpcContractMapper
    {
        /// <summary>
        /// Creates one V3 request containing only semantic trigger IDs and descriptions.
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

            NpcCommandTokenConverter.TryToToken(
                request.DefaultEmotion,
                out var defaultEmotion);
            var triggers = new AiNpcTriggerDto[definitions.Count];
            for (var index = 0; index < definitions.Count; index++)
            {
                var definition = definitions[index]
                    ?? throw new ArgumentException("Trigger definitions must not contain null.", nameof(definitions));
                triggers[index] = new AiNpcTriggerDto
                {
                    triggerId = definition.TriggerId,
                    conditionDescription = definition.ConditionDescription
                };
            }

            var envelope = new AiNpcRequestEnvelopeDto
            {
                schemaVersion = AiNpcContractV3.SchemaVersion,
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
        /// Reconstructs the shared domain request from one valid V3 envelope.
        /// </summary>
        public static AiNpcRequest ReadRequest(AiNpcRequestEnvelopeDto envelope)
        {
            if (!AiNpcContractValidator.TryValidateRequest(envelope, out var error))
            {
                throw new ArgumentException(error, nameof(envelope));
            }

            NpcCommandTokenConverter.TryParseEmotion(
                envelope.character.defaultEmotion,
                out var defaultEmotion);
            return new AiNpcRequest(
                envelope.character.characterId,
                envelope.character.displayName,
                envelope.character.personality,
                envelope.character.speechStyle,
                envelope.character.exampleDialogue,
                defaultEmotion,
                envelope.userText);
        }

        /// <summary>
        /// Creates one correlated V3 success response with matched trigger IDs.
        /// </summary>
        public static AiNpcResponseEnvelopeDto CreateSuccessResponse(
            AiNpcResponse response,
            string requestId)
        {
            if (response == null)
            {
                throw new ArgumentNullException(nameof(response));
            }

            NpcCommandTokenConverter.TryToToken(response.Emotion, out var emotion);
            NpcCommandTokenConverter.TryToToken(response.Gesture, out var gesture);
            var matchedIds = new string[response.MatchedTriggerIds.Count];
            for (var index = 0; index < matchedIds.Length; index++)
            {
                matchedIds[index] = response.MatchedTriggerIds[index];
            }

            var envelope = new AiNpcResponseEnvelopeDto
            {
                schemaVersion = AiNpcContractV3.SchemaVersion,
                requestId = requestId ?? string.Empty,
                status = AiNpcContractV3.SuccessStatus,
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
        /// Creates one validated correlated V3 error response.
        /// </summary>
        public static AiNpcResponseEnvelopeDto CreateErrorResponse(
            string requestId,
            string code,
            string message,
            bool retryable)
        {
            var envelope = new AiNpcResponseEnvelopeDto
            {
                schemaVersion = AiNpcContractV3.SchemaVersion,
                requestId = requestId ?? string.Empty,
                status = AiNpcContractV3.ErrorStatus,
                error = CreateError(code, message, retryable)
            };
            if (!AiNpcContractValidator.TryValidateResponse(envelope, out var error))
            {
                throw new ArgumentException(error, nameof(code));
            }

            return envelope;
        }

        /// <summary>
        /// Reconstructs one action-aware domain response from a V3 success envelope.
        /// </summary>
        public static AiNpcResponse ReadSuccessResponse(
            AiNpcResponseEnvelopeDto envelope)
        {
            if (!AiNpcContractValidator.TryValidateResponse(envelope, out var error))
            {
                throw new ArgumentException(error, nameof(envelope));
            }

            if (envelope.status != AiNpcContractV3.SuccessStatus)
            {
                throw new InvalidOperationException("Only success envelopes map to AiNpcResponse.");
            }

            NpcCommandTokenConverter.TryParseEmotion(envelope.result.emotion, out var emotion);
            NpcCommandTokenConverter.TryParseGesture(envelope.result.gesture, out var gesture);
            return new AiNpcResponse(
                envelope.result.dialogue,
                emotion,
                gesture,
                envelope.result.matchedTriggerIds);
        }

        /// <summary>
        /// Creates one validated V3 session reset request.
        /// </summary>
        public static AiNpcSessionResetRequestDto CreateResetRequest(
            string requestId,
            string sessionId,
            string characterId)
        {
            var request = new AiNpcSessionResetRequestDto
            {
                schemaVersion = AiNpcContractV3.SchemaVersion,
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
        /// Creates one canonical V3 reset success response.
        /// </summary>
        public static AiNpcSessionResetResponseDto CreateResetSuccessResponse(string requestId)
        {
            var response = new AiNpcSessionResetResponseDto
            {
                schemaVersion = AiNpcContractV3.SchemaVersion,
                requestId = requestId ?? string.Empty,
                status = AiNpcContractV3.SuccessStatus,
                result = new AiNpcSessionResetResultDto { reset = true }
            };
            if (!AiNpcContractValidator.TryValidateResetResponse(response, out var error))
            {
                throw new ArgumentException(error, nameof(requestId));
            }

            return response;
        }

        /// <summary>
        /// Creates one canonical V3 reset error response.
        /// </summary>
        public static AiNpcSessionResetResponseDto CreateResetErrorResponse(
            string requestId,
            string code,
            string message,
            bool retryable)
        {
            var response = new AiNpcSessionResetResponseDto
            {
                schemaVersion = AiNpcContractV3.SchemaVersion,
                requestId = requestId ?? string.Empty,
                status = AiNpcContractV3.ErrorStatus,
                error = CreateError(code, message, retryable)
            };
            if (!AiNpcContractValidator.TryValidateResetResponse(response, out var error))
            {
                throw new ArgumentException(error, nameof(code));
            }

            return response;
        }

        /// <summary>
        /// Builds the shared V3 error branch.
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
