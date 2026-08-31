using AiCharacterKit.Speech;
using AiCharacterKit.Transport.Speech.V1;
using AiCharacterKit.Unity.Transport;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace AiCharacterKit.Core.Tests
{
    /// <summary>
    /// Verifies Speech V1 mapping, validation, and JsonUtility golden fixtures.
    /// </summary>
    public sealed class SpeechContractTests
    {
        private static string FixtureRoot => AiCharacterKitTestPaths.Resolve(
            "Tests/EditMode/Fixtures/Transport/Speech/V1") + "/";

        /// <summary>
        /// Maps all provider-neutral values through a valid golden request.
        /// </summary>
        [Test]
        public void GoldenRequest_DeserializeAndMap_PreservesAllValues()
        {
            var json = LoadFixture("valid-request.json");
            var succeeded = SpeechJsonCodecV1.TryDeserializeRequest(
                json,
                out var dto,
                out var error);

            Assert.That(succeeded, Is.True, error);
            var request = SpeechContractMapper.ReadRequest(dto);
            Assert.That(dto.requestId, Is.EqualTo("speech-001"));
            Assert.That(request.VoicePresetId, Is.EqualTo("warm-friendly"));
            Assert.That(request.Text, Does.Contain("Luna"));
        }

        /// <summary>
        /// Serializes identical requests deterministically and accepts unknown V1 fields.
        /// </summary>
        [Test]
        public void RequestCodec_RoundTripAndExtraField_IsDeterministic()
        {
            var dto = SpeechContractMapper.CreateRequest(
                new SpeechSynthesisRequest("calm-formal", "경계를 서고 있습니다."),
                "speech-stable");

            Assert.That(
                SpeechJsonCodecV1.TrySerializeRequest(
                    dto,
                    out var firstJson,
                    out var firstError),
                Is.True,
                firstError);
            Assert.That(
                SpeechJsonCodecV1.TrySerializeRequest(
                    dto,
                    out var secondJson,
                    out var secondError),
                Is.True,
                secondError);
            Assert.That(secondJson, Is.EqualTo(firstJson));

            var withExtra = firstJson.TrimEnd('}') + ",\"future\":true}";
            Assert.That(
                SpeechJsonCodecV1.TryDeserializeRequest(
                    withExtra,
                    out var restored,
                    out var decodeError),
                Is.True,
                decodeError);
            Assert.That(restored.voicePresetId, Is.EqualTo("calm-formal"));
        }

        /// <summary>
        /// Parses a correlated safe JSON error and preserves its retry metadata.
        /// </summary>
        [Test]
        public void GoldenError_Deserialize_PreservesSafeMetadata()
        {
            var succeeded = SpeechJsonCodecV1.TryDeserializeErrorResponse(
                LoadFixture("valid-error-response.json"),
                out var response,
                out var error);

            Assert.That(succeeded, Is.True, error);
            Assert.That(response.requestId, Is.EqualTo("speech-001"));
            Assert.That(response.error.code, Is.EqualTo("voice_preset_not_found"));
            Assert.That(response.error.retryable, Is.False);
        }

        /// <summary>
        /// Rejects malformed JSON, unsupported versions, bad preset tokens, and empty text.
        /// </summary>
        [Test]
        public void RequestCodec_InvalidInputs_ReturnFalseWithoutExceptions()
        {
            Assert.That(
                SpeechJsonCodecV1.TryDeserializeRequest(
                    LoadFixture("malformed.json"),
                    out _,
                    out _),
                Is.False);
            Assert.That(
                SpeechJsonCodecV1.TryDeserializeRequest(
                    "{\"schemaVersion\":2,\"requestId\":\"x\",\"voicePresetId\":\"warm-friendly\",\"text\":\"hello\"}",
                    out _,
                    out _),
                Is.False);
            Assert.That(
                SpeechJsonCodecV1.TryDeserializeRequest(
                    "{\"schemaVersion\":1,\"requestId\":\"x\",\"voicePresetId\":\"Warm Friendly\",\"text\":\"hello\"}",
                    out _,
                    out _),
                Is.False);
            Assert.That(
                SpeechJsonCodecV1.TryDeserializeRequest(
                    "{\"schemaVersion\":1,\"requestId\":\"x\",\"voicePresetId\":\"warm-friendly\",\"text\":\"\"}",
                    out _,
                    out _),
                Is.False);
        }

        /// <summary>
        /// Loads one tracked golden fixture through Unity's asset database.
        /// </summary>
        private static string LoadFixture(string fileName)
        {
            var asset = AssetDatabase.LoadAssetAtPath<TextAsset>(FixtureRoot + fileName);
            Assert.That(asset, Is.Not.Null, fileName);
            return asset.text;
        }
    }
}
