using AiCharacterKit.Core;
using AiCharacterKit.Transport.V1;
using AiCharacterKit.Unity.Transport;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace AiCharacterKit.Core.Tests
{
    /// <summary>
    /// Verifies JsonUtility encoding against stable V1 golden fixtures.
    /// </summary>
    public sealed class AiNpcJsonCodecTests
    {
        private static string FixtureRoot => AiCharacterKitTestPaths.Resolve(
            "Tests/Editor/Fixtures/Transport/V1") + "/";

        /// <summary>
        /// Confirms that the golden request decodes and maps every character value.
        /// </summary>
        [Test]
        public void TryDeserializeRequest_ValidGoldenFixture_MapsAllValues()
        {
            var json = LoadFixture("valid-request.json");

            var succeeded = AiNpcJsonCodec.TryDeserializeRequest(
                json,
                out var envelope,
                out var error);

            Assert.That(succeeded, Is.True, error);
            var request = AiNpcContractMapper.ReadRequest(envelope);
            Assert.That(envelope.requestId, Is.EqualTo("req-001"));
            Assert.That(request.CharacterId, Is.EqualTo("sample-luna"));
            Assert.That(request.DisplayName, Is.EqualTo("Luna"));
            Assert.That(request.Personality, Does.Contain("Playful"));
            Assert.That(request.SpeechStyle, Does.Contain("Warm"));
            Assert.That(request.ExampleDialogue, Does.Contain("모험"));
            Assert.That(request.DefaultEmotion, Is.EqualTo(NpcEmotion.Happy));
            Assert.That(request.UserText, Is.EqualTo("무엇을 좋아해?"));
        }

        /// <summary>
        /// Confirms that the golden success response decodes into domain presentation commands.
        /// </summary>
        [Test]
        public void TryDeserializeResponse_ValidSuccessFixture_MapsAllValues()
        {
            var json = LoadFixture("valid-success-response.json");

            var succeeded = AiNpcJsonCodec.TryDeserializeResponse(
                json,
                out var envelope,
                out var error);

            Assert.That(succeeded, Is.True, error);
            var response = AiNpcContractMapper.ReadSuccessResponse(envelope);
            Assert.That(envelope.requestId, Is.EqualTo("req-001"));
            Assert.That(response.Dialogue, Does.Contain("Luna"));
            Assert.That(response.Emotion, Is.EqualTo(NpcEmotion.Happy));
            Assert.That(response.Gesture, Is.EqualTo(NpcGesture.Nod));
        }

        /// <summary>
        /// Confirms that the golden error response preserves its open error code and retry hint.
        /// </summary>
        [Test]
        public void TryDeserializeResponse_ValidErrorFixture_PreservesError()
        {
            var json = LoadFixture("valid-error-response.json");

            var succeeded = AiNpcJsonCodec.TryDeserializeResponse(
                json,
                out var envelope,
                out var error);

            Assert.That(succeeded, Is.True, error);
            Assert.That(envelope.status, Is.EqualTo(AiNpcContractV1.ErrorStatus));
            Assert.That(envelope.result, Is.Null);
            Assert.That(envelope.error.code, Is.EqualTo("invalid_request"));
            Assert.That(envelope.error.message, Is.Not.Empty);
            Assert.That(envelope.error.retryable, Is.False);
        }

        /// <summary>
        /// Confirms that identical request DTOs serialize deterministically and round-trip.
        /// </summary>
        [Test]
        public void TrySerializeRequest_SameEnvelope_ReturnsDeterministicRoundTrip()
        {
            var request = new AiNpcRequest(
                "sample-guard",
                "Guard",
                "Disciplined, vigilant, and duty-bound.",
                "Formal, concise, respectful sentences.",
                "성문 주변에서는 질서를 지켜 주십시오.",
                NpcEmotion.Concerned,
                "누구세요?");
            var envelope = AiNpcContractMapper.CreateRequest(request, "req-stable");

            var firstSucceeded = AiNpcJsonCodec.TrySerializeRequest(
                envelope,
                out var firstJson,
                out var firstError);
            var secondSucceeded = AiNpcJsonCodec.TrySerializeRequest(
                envelope,
                out var secondJson,
                out var secondError);
            var decodeSucceeded = AiNpcJsonCodec.TryDeserializeRequest(
                firstJson,
                out var restored,
                out var decodeError);

            Assert.That(firstSucceeded, Is.True, firstError);
            Assert.That(secondSucceeded, Is.True, secondError);
            Assert.That(secondJson, Is.EqualTo(firstJson));
            Assert.That(decodeSucceeded, Is.True, decodeError);
            Assert.That(restored.requestId, Is.EqualTo("req-stable"));
            Assert.That(restored.character.defaultEmotion, Is.EqualTo("concerned"));
        }

        /// <summary>
        /// Confirms that both valid response branches serialize and deserialize successfully.
        /// </summary>
        [Test]
        public void TrySerializeResponse_SuccessAndError_RoundTrip()
        {
            var success = AiNpcContractMapper.CreateSuccessResponse(
                new AiNpcResponse("완료", NpcEmotion.Neutral, NpcGesture.None),
                "req-success-round-trip");
            var failure = AiNpcContractMapper.CreateErrorResponse(
                "req-error-round-trip",
                AiNpcContractV1.InternalErrorCode,
                "처리하지 못했습니다.",
                true);

            Assert.That(
                AiNpcJsonCodec.TrySerializeResponse(
                    success,
                    out var successJson,
                    out var successSerializeError),
                Is.True,
                successSerializeError);
            Assert.That(successJson, Does.Not.Contain("\"error\""));
            Assert.That(
                AiNpcJsonCodec.TryDeserializeResponse(
                    successJson,
                    out var restoredSuccess,
                    out var successDeserializeError),
                Is.True,
                successDeserializeError);
            Assert.That(restoredSuccess.result.dialogue, Is.EqualTo("완료"));

            Assert.That(
                AiNpcJsonCodec.TrySerializeResponse(
                    failure,
                    out var errorJson,
                    out var errorSerializeError),
                Is.True,
                errorSerializeError);
            Assert.That(errorJson, Does.Not.Contain("\"result\""));
            Assert.That(
                AiNpcJsonCodec.TryDeserializeResponse(
                    errorJson,
                    out var restoredError,
                    out var errorDeserializeError),
                Is.True,
                errorDeserializeError);
            Assert.That(restoredError.error.code, Is.EqualTo("internal_error"));
            Assert.That(restoredError.error.retryable, Is.True);
        }

        /// <summary>
        /// Confirms that a missing required character snapshot is rejected after parsing.
        /// </summary>
        [Test]
        public void TryDeserializeRequest_MissingCharacterFixture_ReturnsFalse()
        {
            var succeeded = AiNpcJsonCodec.TryDeserializeRequest(
                LoadFixture("missing-character-request.json"),
                out var envelope,
                out var error);

            Assert.That(succeeded, Is.False);
            Assert.That(envelope, Is.Null);
            Assert.That(error, Does.Contain("character"));
        }

        /// <summary>
        /// Confirms that an unknown response emotion token is rejected after parsing.
        /// </summary>
        [Test]
        public void TryDeserializeResponse_UnknownEmotionFixture_ReturnsFalse()
        {
            var succeeded = AiNpcJsonCodec.TryDeserializeResponse(
                LoadFixture("unknown-emotion-response.json"),
                out var envelope,
                out var error);

            Assert.That(succeeded, Is.False);
            Assert.That(envelope, Is.Null);
            Assert.That(error, Does.Contain("emotion"));
        }

        /// <summary>
        /// Confirms that versions other than integer V1 are rejected.
        /// </summary>
        [Test]
        public void TryDeserializeRequest_UnsupportedVersionFixture_ReturnsFalse()
        {
            var succeeded = AiNpcJsonCodec.TryDeserializeRequest(
                LoadFixture("unsupported-version-request.json"),
                out var envelope,
                out var error);

            Assert.That(succeeded, Is.False);
            Assert.That(envelope, Is.Null);
            Assert.That(error, Does.Contain("schemaVersion"));
        }

        /// <summary>
        /// Confirms that malformed JSON is reported without throwing to the caller.
        /// </summary>
        [Test]
        public void TryDeserializeRequest_MalformedFixture_ReturnsFalse()
        {
            Assert.DoesNotThrow(() =>
            {
                var succeeded = AiNpcJsonCodec.TryDeserializeRequest(
                    LoadFixture("malformed.json"),
                    out var envelope,
                    out var error);

                Assert.That(succeeded, Is.False);
                Assert.That(envelope, Is.Null);
                Assert.That(error, Is.Not.Empty);
            });
        }

        /// <summary>
        /// Confirms that empty JSON is rejected before calling the serializer.
        /// </summary>
        [TestCase("")]
        [TestCase("   ")]
        [TestCase(null)]
        public void TryDeserializeRequest_EmptyJson_ReturnsFalse(string json)
        {
            var succeeded = AiNpcJsonCodec.TryDeserializeRequest(
                json,
                out var envelope,
                out var error);

            Assert.That(succeeded, Is.False);
            Assert.That(envelope, Is.Null);
            Assert.That(error, Does.Contain("must not be empty"));
        }

        /// <summary>
        /// Confirms that additive fields do not break readers within schema V1.
        /// </summary>
        [Test]
        public void TryDeserializeRequest_UnknownAdditionalField_ReturnsTrue()
        {
            var succeeded = AiNpcJsonCodec.TryDeserializeRequest(
                LoadFixture("request-with-extra-field.json"),
                out var envelope,
                out var error);

            Assert.That(succeeded, Is.True, error);
            Assert.That(envelope.requestId, Is.EqualTo("req-extra"));
        }

        /// <summary>
        /// Confirms that status tokens are case-sensitive and closed in V1.
        /// </summary>
        [Test]
        public void TryDeserializeResponse_UnknownStatus_ReturnsFalse()
        {
            const string json =
                "{\"schemaVersion\":1,\"requestId\":\"req-status\",\"status\":\"Success\"}";

            var succeeded = AiNpcJsonCodec.TryDeserializeResponse(
                json,
                out var envelope,
                out var error);

            Assert.That(succeeded, Is.False);
            Assert.That(envelope, Is.Null);
            Assert.That(error, Does.Contain("status"));
        }

        /// <summary>
        /// Confirms that an explicitly present empty inactive branch is not normalized away.
        /// </summary>
        [Test]
        public void TryDeserializeResponse_ExplicitEmptyInactiveBranch_ReturnsFalse()
        {
            const string json =
                "{\"schemaVersion\":1,\"requestId\":\"req-empty\","
                + "\"status\":\"success\",\"result\":{\"dialogue\":\"완료\","
                + "\"emotion\":\"neutral\",\"gesture\":\"none\"},\"error\":{}}";

            var succeeded = AiNpcJsonCodec.TryDeserializeResponse(
                json,
                out var envelope,
                out var error);

            Assert.That(succeeded, Is.False);
            Assert.That(envelope, Is.Null);
            Assert.That(error, Does.Contain("must not contain error"));
        }

        /// <summary>
        /// Confirms that either inactive branch may be explicitly represented by JSON null.
        /// </summary>
        [Test]
        public void TryDeserializeResponse_ExplicitNullInactiveBranches_ReturnTrue()
        {
            const string successJson =
                "{\"schemaVersion\":1,\"requestId\":\"req-null-success\","
                + "\"status\":\"success\",\"result\":{\"dialogue\":\"완료\","
                + "\"emotion\":\"neutral\",\"gesture\":\"none\"},\"error\":null}";
            const string errorJson =
                "{\"schemaVersion\":1,\"requestId\":\"req-null-error\","
                + "\"status\":\"error\",\"result\":null,\"error\":{"
                + "\"code\":\"internal_error\",\"message\":\"실패\","
                + "\"retryable\":false}}";

            Assert.That(
                AiNpcJsonCodec.TryDeserializeResponse(
                    successJson,
                    out var success,
                    out var successError),
                Is.True,
                successError);
            Assert.That(success.error, Is.Null);

            Assert.That(
                AiNpcJsonCodec.TryDeserializeResponse(
                    errorJson,
                    out var failure,
                    out var failureError),
                Is.True,
                failureError);
            Assert.That(failure.result, Is.Null);
        }

        /// <summary>
        /// Confirms that invalid DTOs cannot be serialized into wire content.
        /// </summary>
        [Test]
        public void TrySerializeRequest_InvalidEnvelope_ReturnsFalse()
        {
            var envelope = new AiNpcRequestEnvelopeDto
            {
                schemaVersion = AiNpcContractV1.SchemaVersion,
                requestId = "req-invalid"
            };

            var succeeded = AiNpcJsonCodec.TrySerializeRequest(
                envelope,
                out var json,
                out var error);

            Assert.That(succeeded, Is.False);
            Assert.That(json, Is.Empty);
            Assert.That(error, Does.Contain("character"));
        }

        /// <summary>
        /// Loads one tracked JSON fixture through Unity's asset database.
        /// </summary>
        private static string LoadFixture(string fileName)
        {
            var asset = AssetDatabase.LoadAssetAtPath<TextAsset>(FixtureRoot + fileName);
            Assert.That(asset, Is.Not.Null, fileName);
            return asset.text;
        }
    }
}
