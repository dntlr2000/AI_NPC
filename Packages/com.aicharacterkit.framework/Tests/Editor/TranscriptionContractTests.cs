using AiCharacterKit.Transcription;
using AiCharacterKit.Transport.Transcription.V1;
using AiCharacterKit.Unity.Transport;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace AiCharacterKit.Core.Tests
{
    /// <summary>
    /// Verifies Transcription V1 mapping, branches, and JsonUtility golden fixtures.
    /// </summary>
    public sealed class TranscriptionContractTests
    {
        private static string FixtureRoot => AiCharacterKitTestPaths.Resolve(
            "Tests/Editor/Fixtures/Transport/Transcription/V1") + "/";

        /// <summary>
        /// Reads and maps a golden success while preserving exact transcript text.
        /// </summary>
        [Test]
        public void GoldenSuccess_DeserializeAndMap_PreservesText()
        {
            var succeeded = TranscriptionJsonCodecV1.TryDeserializeResponse(
                LoadFixture("valid-success-response.json"),
                out var response,
                out var error);

            Assert.That(succeeded, Is.True, error);
            Assert.That(response.requestId, Is.EqualTo("transcription-001"));
            Assert.That(
                TranscriptionContractMapper.ReadResult(response).Text,
                Is.EqualTo("안녕하세요, Luna."));
        }

        /// <summary>
        /// Reads a golden error and preserves its safe retry metadata.
        /// </summary>
        [Test]
        public void GoldenError_DeserializeAndMap_PreservesMetadata()
        {
            var succeeded = TranscriptionJsonCodecV1.TryDeserializeResponse(
                LoadFixture("valid-error-response.json"),
                out var response,
                out var error);

            Assert.That(succeeded, Is.True, error);
            var exception = TranscriptionContractMapper.ReadError(response);
            Assert.That(exception.Code, Is.EqualTo("rate_limited"));
            Assert.That(exception.Retryable, Is.True);
        }

        /// <summary>
        /// Serializes identical DTOs deterministically and ignores unknown V1 fields.
        /// </summary>
        [Test]
        public void ResponseCodec_RoundTripAndExtraField_IsDeterministic()
        {
            var response = TranscriptionContractMapper.CreateSuccessResponse(
                "transcription-stable",
                new TranscriptionResult("정확한 전사"));
            Assert.That(
                TranscriptionJsonCodecV1.TrySerializeResponse(
                    response,
                    out var first,
                    out var firstError),
                Is.True,
                firstError);
            Assert.That(
                TranscriptionJsonCodecV1.TrySerializeResponse(
                    response,
                    out var second,
                    out var secondError),
                Is.True,
                secondError);
            Assert.That(second, Is.EqualTo(first));
            Assert.That(first, Does.Not.Contain("\"error\""));

            var withExtra = first.Substring(0, first.Length - 1)
                + ",\"future\":true}";
            Assert.That(
                TranscriptionJsonCodecV1.TryDeserializeResponse(
                    withExtra,
                    out var restored,
                    out var decodeError),
                Is.True,
                decodeError + " JSON: " + withExtra);
            Assert.That(restored.result.text, Is.EqualTo("정확한 전사"));

            var failure = TranscriptionContractMapper.CreateErrorResponse(
                "transcription-error",
                new TranscriptionException(
                    "invalid_audio",
                    "Invalid audio.",
                    false));
            Assert.That(
                TranscriptionJsonCodecV1.TrySerializeResponse(
                    failure,
                    out var failureJson,
                    out var failureError),
                Is.True,
                failureError);
            Assert.That(failureJson, Does.Not.Contain("\"result\""));
            Assert.That(
                TranscriptionJsonCodecV1.TryDeserializeResponse(
                    failureJson,
                    out var restoredFailure,
                    out var restoredFailureError),
                Is.True,
                restoredFailureError);
            Assert.That(restoredFailure.error.code, Is.EqualTo("invalid_audio"));
        }

        /// <summary>
        /// Rejects malformed JSON, unknown status or version, and invalid branch overlap.
        /// </summary>
        [Test]
        public void ResponseCodec_InvalidInputs_ReturnFalseWithoutExceptions()
        {
            Assert.That(
                TranscriptionJsonCodecV1.TryDeserializeResponse(
                    LoadFixture("malformed.json"),
                    out _,
                    out _),
                Is.False);
            Assert.That(
                TranscriptionJsonCodecV1.TryDeserializeResponse(
                    "{\"schemaVersion\":2,\"requestId\":\"x\",\"status\":\"success\",\"result\":{\"text\":\"ok\"}}",
                    out _,
                    out _),
                Is.False);
            Assert.That(
                TranscriptionJsonCodecV1.TryDeserializeResponse(
                    "{\"schemaVersion\":1,\"requestId\":\"x\",\"status\":\"pending\"}",
                    out _,
                    out _),
                Is.False);
            Assert.That(
                TranscriptionJsonCodecV1.TryDeserializeResponse(
                    LoadFixture("invalid-branch-response.json"),
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
