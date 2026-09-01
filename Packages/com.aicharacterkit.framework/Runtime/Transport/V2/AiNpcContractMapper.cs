using System;
using AiCharacterKit.Core;

namespace AiCharacterKit.Transport.V2
{
    /// <summary>
    /// Maps pure domain models to and from validated V2 transport envelopes.
    /// </summary>
    public static class AiNpcContractMapper
    {
        /// <summary>
        /// Creates a session-aware request with caller-owned correlation metadata.
        /// </summary>
        public static AiNpcRequestEnvelopeDto CreateRequest(
            AiNpcRequest request,
            string requestId,
            string sessionId)
        {
            if (request == null)
            {
                throw new ArgumentNullException(nameof(request));
            }

            NpcCommandTokenConverter.TryToToken(
                request.DefaultEmotion,
                out var defaultEmotion);

            var envelope = new AiNpcRequestEnvelopeDto
            {
                schemaVersion = AiNpcContractV2.SchemaVersion,
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
                userText = request.UserText
            };

            if (!AiNpcContractValidator.TryValidateRequest(envelope, out var error))
            {
                throw new ArgumentException(error, nameof(request));
            }

            return envelope;
        }

        /// <summary>
        /// Reconstructs a domain request from one valid V2 request envelope.
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
        /// Creates a correlated success envelope from one domain response.
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

            var envelope = new AiNpcResponseEnvelopeDto
            {
                schemaVersion = AiNpcContractV2.SchemaVersion,
                requestId = requestId ?? string.Empty,
                status = AiNpcContractV2.SuccessStatus,
                result = new AiNpcResponsePayloadDto
                {
                    dialogue = response.Dialogue,
                    emotion = emotion,
                    gesture = gesture
                },
                error = null
            };

            if (!AiNpcContractValidator.TryValidateResponse(envelope, out var error))
            {
                throw new ArgumentException(error, nameof(response));
            }

            return envelope;
        }

        /// <summary>
        /// Creates a correlated V2 error envelope.
        /// </summary>
        public static AiNpcResponseEnvelopeDto CreateErrorResponse(
            string requestId,
            string code,
            string message,
            bool retryable)
        {
            var envelope = new AiNpcResponseEnvelopeDto
            {
                schemaVersion = AiNpcContractV2.SchemaVersion,
                requestId = requestId ?? string.Empty,
                status = AiNpcContractV2.ErrorStatus,
                result = null,
                error = CreateError(code, message, retryable)
            };

            if (!AiNpcContractValidator.TryValidateResponse(envelope, out var error))
            {
                throw new ArgumentException(error, nameof(code));
            }

            return envelope;
        }

        /// <summary>
        /// Reconstructs a domain response from one valid V2 success envelope.
        /// </summary>
        public static AiNpcResponse ReadSuccessResponse(
            AiNpcResponseEnvelopeDto envelope)
        {
            if (!AiNpcContractValidator.TryValidateResponse(envelope, out var error))
            {
                throw new ArgumentException(error, nameof(envelope));
            }

            if (envelope.status != AiNpcContractV2.SuccessStatus)
            {
                throw new InvalidOperationException(
                    "Only success envelopes can be mapped to AiNpcResponse.");
            }

            NpcCommandTokenConverter.TryParseEmotion(
                envelope.result.emotion,
                out var emotion);
            NpcCommandTokenConverter.TryParseGesture(
                envelope.result.gesture,
                out var gesture);

            return new AiNpcResponse(envelope.result.dialogue, emotion, gesture);
        }

        /// <summary>
        /// Creates a validated request that clears one character-bound session.
        /// </summary>
        public static AiNpcSessionResetRequestDto CreateResetRequest(
            string requestId,
            string sessionId,
            string characterId)
        {
            var request = new AiNpcSessionResetRequestDto
            {
                schemaVersion = AiNpcContractV2.SchemaVersion,
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
        /// Creates a canonical correlated acknowledgement for a completed reset.
        /// </summary>
        public static AiNpcSessionResetResponseDto CreateResetSuccessResponse(
            string requestId)
        {
            var response = new AiNpcSessionResetResponseDto
            {
                schemaVersion = AiNpcContractV2.SchemaVersion,
                requestId = requestId ?? string.Empty,
                status = AiNpcContractV2.SuccessStatus,
                result = new AiNpcSessionResetResultDto { reset = true },
                error = null
            };

            if (!AiNpcContractValidator.TryValidateResetResponse(response, out var error))
            {
                throw new ArgumentException(error, nameof(requestId));
            }

            return response;
        }

        /// <summary>
        /// Creates a canonical correlated error for a rejected reset.
        /// </summary>
        public static AiNpcSessionResetResponseDto CreateResetErrorResponse(
            string requestId,
            string code,
            string message,
            bool retryable)
        {
            var response = new AiNpcSessionResetResponseDto
            {
                schemaVersion = AiNpcContractV2.SchemaVersion,
                requestId = requestId ?? string.Empty,
                status = AiNpcContractV2.ErrorStatus,
                result = null,
                error = CreateError(code, message, retryable)
            };

            if (!AiNpcContractValidator.TryValidateResetResponse(response, out var error))
            {
                throw new ArgumentException(error, nameof(code));
            }

            return response;
        }

        /// <summary>
        /// Builds the shared V2 error branch without assigning transport behavior.
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
