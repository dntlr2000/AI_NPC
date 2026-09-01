using System;
using AiCharacterKit.Core;

namespace AiCharacterKit.Transport.V1
{
    /// <summary>
    /// Maps pure domain models to and from validated V1 transport envelopes.
    /// </summary>
    public static class AiNpcContractMapper
    {
        /// <summary>
        /// Creates a request envelope with caller-owned correlation metadata.
        /// </summary>
        public static AiNpcRequestEnvelopeDto CreateRequest(
            AiNpcRequest request,
            string requestId)
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
                schemaVersion = AiNpcContractV1.SchemaVersion,
                requestId = requestId ?? string.Empty,
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
        /// Reconstructs a domain request from one valid V1 request envelope.
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
                schemaVersion = AiNpcContractV1.SchemaVersion,
                requestId = requestId ?? string.Empty,
                status = AiNpcContractV1.SuccessStatus,
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
        /// Creates a correlated error envelope without assigning backend-specific handling.
        /// </summary>
        public static AiNpcResponseEnvelopeDto CreateErrorResponse(
            string requestId,
            string code,
            string message,
            bool retryable)
        {
            var envelope = new AiNpcResponseEnvelopeDto
            {
                schemaVersion = AiNpcContractV1.SchemaVersion,
                requestId = requestId ?? string.Empty,
                status = AiNpcContractV1.ErrorStatus,
                result = null,
                error = new AiNpcErrorDto
                {
                    code = code ?? string.Empty,
                    message = message ?? string.Empty,
                    retryable = retryable
                }
            };

            if (!AiNpcContractValidator.TryValidateResponse(envelope, out var error))
            {
                throw new ArgumentException(error, nameof(code));
            }

            return envelope;
        }

        /// <summary>
        /// Reconstructs a domain response from one valid V1 success envelope.
        /// </summary>
        public static AiNpcResponse ReadSuccessResponse(
            AiNpcResponseEnvelopeDto envelope)
        {
            if (!AiNpcContractValidator.TryValidateResponse(envelope, out var error))
            {
                throw new ArgumentException(error, nameof(envelope));
            }

            if (envelope.status != AiNpcContractV1.SuccessStatus)
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
    }
}
