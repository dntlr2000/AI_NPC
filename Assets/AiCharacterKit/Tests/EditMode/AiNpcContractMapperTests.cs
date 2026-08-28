using System;
using AiCharacterKit.Core;
using AiCharacterKit.Transport.V1;
using NUnit.Framework;

namespace AiCharacterKit.Core.Tests
{
    /// <summary>
    /// Verifies pure mappings and validation rules for the V1 wire contract.
    /// </summary>
    public sealed class AiNpcContractMapperTests
    {
        /// <summary>
        /// Confirms that a complete request survives domain-to-contract round-trip mapping.
        /// </summary>
        [Test]
        public void CreateAndReadRequest_CompleteRequest_PreservesDomainValues()
        {
            var source = CreateRequest(NpcEmotion.Happy);

            var envelope = AiNpcContractMapper.CreateRequest(source, "req-001");
            var restored = AiNpcContractMapper.ReadRequest(envelope);

            Assert.That(envelope.schemaVersion, Is.EqualTo(AiNpcContractV1.SchemaVersion));
            Assert.That(envelope.requestId, Is.EqualTo("req-001"));
            Assert.That(restored.CharacterId, Is.EqualTo(source.CharacterId));
            Assert.That(restored.DisplayName, Is.EqualTo(source.DisplayName));
            Assert.That(restored.Personality, Is.EqualTo(source.Personality));
            Assert.That(restored.SpeechStyle, Is.EqualTo(source.SpeechStyle));
            Assert.That(restored.ExampleDialogue, Is.EqualTo(source.ExampleDialogue));
            Assert.That(restored.DefaultEmotion, Is.EqualTo(source.DefaultEmotion));
            Assert.That(restored.UserText, Is.EqualTo(source.UserText));
        }

        /// <summary>
        /// Confirms that a structured success response survives contract round-trip mapping.
        /// </summary>
        [Test]
        public void CreateAndReadSuccessResponse_CompleteResponse_PreservesDomainValues()
        {
            var source = new AiNpcResponse(
                "Luna: 함께 모험을 떠나요.",
                NpcEmotion.Happy,
                NpcGesture.Wave);

            var envelope = AiNpcContractMapper.CreateSuccessResponse(
                source,
                "req-002");
            var restored = AiNpcContractMapper.ReadSuccessResponse(envelope);

            Assert.That(envelope.requestId, Is.EqualTo("req-002"));
            Assert.That(envelope.status, Is.EqualTo(AiNpcContractV1.SuccessStatus));
            Assert.That(envelope.error, Is.Null);
            Assert.That(restored.Dialogue, Is.EqualTo(source.Dialogue));
            Assert.That(restored.Emotion, Is.EqualTo(source.Emotion));
            Assert.That(restored.Gesture, Is.EqualTo(source.Gesture));
        }

        /// <summary>
        /// Confirms that an error factory creates only the validated error branch.
        /// </summary>
        [Test]
        public void CreateErrorResponse_ValidValues_CreatesExclusiveErrorBranch()
        {
            var envelope = AiNpcContractMapper.CreateErrorResponse(
                "req-003",
                AiNpcContractV1.InvalidRequestErrorCode,
                "요청을 확인해 주세요.",
                false);

            Assert.That(
                AiNpcContractValidator.TryValidateResponse(envelope, out var error),
                Is.True,
                error);
            Assert.That(envelope.status, Is.EqualTo(AiNpcContractV1.ErrorStatus));
            Assert.That(envelope.result, Is.Null);
            Assert.That(envelope.error.code, Is.EqualTo("invalid_request"));
            Assert.That(envelope.error.retryable, Is.False);
        }

        /// <summary>
        /// Confirms that undefined domain commands cannot enter the wire contract.
        /// </summary>
        [Test]
        public void CreateRequest_UnsupportedDomainEmotion_ThrowsArgumentException()
        {
            var source = CreateRequest((NpcEmotion)999);

            Assert.Throws<ArgumentException>(
                () => AiNpcContractMapper.CreateRequest(source, "req-invalid"));
        }

        /// <summary>
        /// Confirms that error codes outside the lowercase snake_case policy are rejected.
        /// </summary>
        [Test]
        public void CreateErrorResponse_InvalidErrorCode_ThrowsArgumentException()
        {
            Assert.Throws<ArgumentException>(() =>
                AiNpcContractMapper.CreateErrorResponse(
                    "req-invalid-code",
                    "Invalid-Request",
                    "Invalid code.",
                    false));
        }

        /// <summary>
        /// Confirms that success and error branches cannot coexist in one response.
        /// </summary>
        [Test]
        public void TryValidateResponse_BothBranchesPresent_ReturnsFalse()
        {
            var envelope = AiNpcContractMapper.CreateSuccessResponse(
                new AiNpcResponse("완료", NpcEmotion.Neutral, NpcGesture.None),
                "req-004");
            envelope.error = new AiNpcErrorDto
            {
                code = AiNpcContractV1.InternalErrorCode,
                message = "Unexpected error."
            };

            Assert.That(
                AiNpcContractValidator.TryValidateResponse(envelope, out var error),
                Is.False);
            Assert.That(error, Does.Contain("must not contain error"));
        }

        /// <summary>
        /// Confirms that a response cannot omit both its success and error branch.
        /// </summary>
        [Test]
        public void TryValidateResponse_ActiveBranchMissing_ReturnsFalse()
        {
            var envelope = new AiNpcResponseEnvelopeDto
            {
                schemaVersion = AiNpcContractV1.SchemaVersion,
                requestId = "req-005",
                status = AiNpcContractV1.SuccessStatus
            };

            Assert.That(
                AiNpcContractValidator.TryValidateResponse(envelope, out var error),
                Is.False);
            Assert.That(error, Does.Contain("requires result"));
        }

        /// <summary>
        /// Confirms that V1 accepts only its exact lowercase command tokens.
        /// </summary>
        [TestCase("Happy", AiNpcContractV1.NodGesture)]
        [TestCase(AiNpcContractV1.HappyEmotion, "dance")]
        public void TryValidateResponse_UnknownCommandToken_ReturnsFalse(
            string emotion,
            string gesture)
        {
            var envelope = new AiNpcResponseEnvelopeDto
            {
                schemaVersion = AiNpcContractV1.SchemaVersion,
                requestId = "req-006",
                status = AiNpcContractV1.SuccessStatus,
                result = new AiNpcResponsePayloadDto
                {
                    dialogue = "완료",
                    emotion = emotion,
                    gesture = gesture
                }
            };

            Assert.That(
                AiNpcContractValidator.TryValidateResponse(envelope, out _),
                Is.False);
        }

        /// <summary>
        /// Creates a reusable complete domain request for mapper tests.
        /// </summary>
        private static AiNpcRequest CreateRequest(NpcEmotion defaultEmotion)
        {
            return new AiNpcRequest(
                "sample-luna",
                "Luna",
                "Playful, curious, and friendly.",
                "Warm, casual, short sentences.",
                "새로운 모험 이야기를 들려줄래?",
                defaultEmotion,
                "무엇을 좋아해?");
        }
    }
}
