using System.Text;
using AiCharacterKit.Core;
using AiCharacterKit.Transport.V2;
using AiCharacterKit.Unity.Transport;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace AiCharacterKit.Core.Tests
{
    /// <summary>
    /// Verifies V2 mapping, validation, and JsonUtility behavior against shared fixtures.
    /// </summary>
    public sealed class AiNpcContractV2Tests
    {
        private static string FixtureRoot => AiCharacterKitTestPaths.Resolve(
            "Tests/Editor/Fixtures/Transport/V2") + "/";

        /// <summary>
        /// Maps every golden request value and preserves its caller-owned session ID.
        /// </summary>
        [Test]
        public void TryDeserializeRequest_ValidFixture_MapsAllValues()
        {
            var succeeded = AiNpcJsonCodecV2.TryDeserializeRequest(
                LoadFixture("valid-request.json"),
                out var envelope,
                out var error);

            Assert.That(succeeded, Is.True, error);
            var request = AiNpcContractMapper.ReadRequest(envelope);
            Assert.That(envelope.schemaVersion, Is.EqualTo(2));
            Assert.That(envelope.requestId, Is.EqualTo("req-v2-001"));
            Assert.That(envelope.sessionId, Is.EqualTo("session-001"));
            Assert.That(request.CharacterId, Is.EqualTo("sample-luna"));
            Assert.That(request.DefaultEmotion, Is.EqualTo(NpcEmotion.Happy));
            Assert.That(request.UserText, Does.Contain("파랑"));
        }

        /// <summary>
        /// Maps both golden conversation branches without materializing inactive content.
        /// </summary>
        [Test]
        public void TryDeserializeResponse_GoldenBranches_PreserveMeaning()
        {
            Assert.That(
                AiNpcJsonCodecV2.TryDeserializeResponse(
                    LoadFixture("valid-success-response.json"),
                    out var success,
                    out var successError),
                Is.True,
                successError);
            Assert.That(
                AiNpcJsonCodecV2.TryDeserializeResponse(
                    LoadFixture("valid-error-response.json"),
                    out var failure,
                    out var failureError),
                Is.True,
                failureError);

            var response = AiNpcContractMapper.ReadSuccessResponse(success);
            Assert.That(response.Emotion, Is.EqualTo(NpcEmotion.Happy));
            Assert.That(response.Gesture, Is.EqualTo(NpcGesture.Nod));
            Assert.That(success.error, Is.Null);
            Assert.That(failure.result, Is.Null);
            Assert.That(failure.error.code, Is.EqualTo("session_busy"));
            Assert.That(failure.error.retryable, Is.True);
        }

        /// <summary>
        /// Decodes golden reset request, success, and error branches.
        /// </summary>
        [Test]
        public void TryDeserializeReset_GoldenFixtures_PreserveMeaning()
        {
            Assert.That(
                AiNpcJsonCodecV2.TryDeserializeResetRequest(
                    LoadFixture("valid-reset-request.json"),
                    out var request,
                    out var requestError),
                Is.True,
                requestError);
            Assert.That(
                AiNpcJsonCodecV2.TryDeserializeResetResponse(
                    LoadFixture("valid-reset-success-response.json"),
                    out var success,
                    out var successError),
                Is.True,
                successError);
            Assert.That(
                AiNpcJsonCodecV2.TryDeserializeResetResponse(
                    LoadFixture("valid-reset-error-response.json"),
                    out var failure,
                    out var failureError),
                Is.True,
                failureError);

            Assert.That(request.sessionId, Is.EqualTo("session-001"));
            Assert.That(request.characterId, Is.EqualTo("sample-luna"));
            Assert.That(success.result.reset, Is.True);
            Assert.That(success.error, Is.Null);
            Assert.That(failure.result, Is.Null);
            Assert.That(
                failure.error.code,
                Is.EqualTo("session_character_mismatch"));
        }

        /// <summary>
        /// Produces deterministic canonical JSON for conversation and reset envelopes.
        /// </summary>
        [Test]
        public void TrySerialize_AllV2Branches_RoundTripCanonically()
        {
            var request = AiNpcContractMapper.CreateRequest(
                CreateRequest("안녕"),
                "req-stable",
                "session-stable");
            var reset = AiNpcContractMapper.CreateResetRequest(
                "req-reset",
                "session-stable",
                "sample-luna");
            var success = AiNpcContractMapper.CreateSuccessResponse(
                new AiNpcResponse("완료", NpcEmotion.Neutral, NpcGesture.None),
                "req-success");
            var resetSuccess = AiNpcContractMapper.CreateResetSuccessResponse(
                "req-reset-success");

            Assert.That(
                AiNpcJsonCodecV2.TrySerializeRequest(
                    request,
                    out var firstJson,
                    out var firstError),
                Is.True,
                firstError);
            Assert.That(
                AiNpcJsonCodecV2.TrySerializeRequest(
                    request,
                    out var secondJson,
                    out var secondError),
                Is.True,
                secondError);
            Assert.That(secondJson, Is.EqualTo(firstJson));
            Assert.That(firstJson, Does.Contain("\"sessionId\":\"session-stable\""));

            Assert.That(
                AiNpcJsonCodecV2.TrySerializeResetRequest(
                    reset,
                    out var resetJson,
                    out var resetError),
                Is.True,
                resetError);
            Assert.That(resetJson, Does.Contain("\"characterId\":\"sample-luna\""));

            Assert.That(
                AiNpcJsonCodecV2.TrySerializeResponse(
                    success,
                    out var successJson,
                    out var successError),
                Is.True,
                successError);
            Assert.That(successJson, Does.Not.Contain("\"error\""));

            Assert.That(
                AiNpcJsonCodecV2.TrySerializeResetResponse(
                    resetSuccess,
                    out var resetSuccessJson,
                    out var resetSuccessError),
                Is.True,
                resetSuccessError);
            Assert.That(resetSuccessJson, Does.Not.Contain("\"error\""));
        }

        /// <summary>
        /// Rejects malformed, incomplete, unsupported, and unknown-token fixtures safely.
        /// </summary>
        [Test]
        public void TryDeserialize_InvalidFixtures_ReturnFalseWithoutThrowing()
        {
            Assert.DoesNotThrow(() =>
            {
                Assert.That(
                    AiNpcJsonCodecV2.TryDeserializeRequest(
                        LoadFixture("malformed.json"),
                        out _,
                        out _),
                    Is.False);
            });
            Assert.That(
                AiNpcJsonCodecV2.TryDeserializeRequest(
                    LoadFixture("missing-session-request.json"),
                    out _,
                    out _),
                Is.False);
            Assert.That(
                AiNpcJsonCodecV2.TryDeserializeRequest(
                    LoadFixture("unsupported-version-request.json"),
                    out _,
                    out _),
                Is.False);
            Assert.That(
                AiNpcJsonCodecV2.TryDeserializeResponse(
                    LoadFixture("unknown-emotion-response.json"),
                    out _,
                    out _),
                Is.False);
            Assert.That(
                AiNpcJsonCodecV2.TryDeserializeResetResponse(
                    LoadFixture("invalid-reset-result.json"),
                    out _,
                    out _),
                Is.False);
        }

        /// <summary>
        /// Enforces opaque session and UTF-8 user text limits in the pure validator.
        /// </summary>
        [Test]
        public void TryValidateRequest_ValuesBeyondLimits_ReturnFalse()
        {
            var request = AiNpcContractMapper.CreateRequest(
                CreateRequest("안녕"),
                "req-limits",
                "session-limits");
            request.sessionId = new string('s', 129);
            Assert.That(
                AiNpcContractValidator.TryValidateRequest(request, out var sessionError),
                Is.False);
            Assert.That(sessionError, Does.Contain("sessionId"));

            request.sessionId = "session-limits";
            request.userText = new string('한', 2731);
            Assert.That(
                Encoding.UTF8.GetByteCount(request.userText),
                Is.GreaterThan(AiNpcContractV2.MaxUserTextUtf8Bytes));
            Assert.That(
                AiNpcContractValidator.TryValidateRequest(request, out var textError),
                Is.False);
            Assert.That(textError, Does.Contain("UTF-8"));
        }

        /// <summary>
        /// Accepts additive fields from the shared fixture within schema V2.
        /// </summary>
        [Test]
        public void TryDeserializeRequest_AdditionalFields_ReturnsTrue()
        {
            Assert.That(
                AiNpcJsonCodecV2.TryDeserializeRequest(
                    LoadFixture("request-with-extra-field.json"),
                    out var request,
                    out var error),
                Is.True,
                error);
            Assert.That(request.requestId, Is.EqualTo("req-v2-extra"));
        }

        /// <summary>
        /// Creates one complete domain request used by V2 mapper tests.
        /// </summary>
        private static AiNpcRequest CreateRequest(string userText)
        {
            return new AiNpcRequest(
                "sample-luna",
                "Luna",
                "Playful",
                "Warm",
                "안녕!",
                NpcEmotion.Happy,
                userText);
        }

        /// <summary>
        /// Loads one tracked V2 JSON fixture through Unity's asset database.
        /// </summary>
        private static string LoadFixture(string fileName)
        {
            var asset = AssetDatabase.LoadAssetAtPath<TextAsset>(FixtureRoot + fileName);
            Assert.That(asset, Is.Not.Null, fileName);
            return asset.text;
        }
    }
}
