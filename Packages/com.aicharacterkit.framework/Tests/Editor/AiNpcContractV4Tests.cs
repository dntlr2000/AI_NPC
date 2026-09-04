using System.IO;
using AiCharacterKit.Core;
using AiCharacterKit.Unity.Transport;
using NUnit.Framework;
using UnityEditor;
using V4 = AiCharacterKit.Transport.V4;

namespace AiCharacterKit.Core.Tests
{
    /// <summary>
    /// Verifies V4 mapping, validation, and JsonUtility behavior against golden fixtures.
    /// </summary>
    public sealed class AiNpcContractV4Tests
    {
        private static string FixtureRoot => AiCharacterKitTestPaths.Resolve(
            "Tests/Editor/Fixtures/Transport/V4") + "/";

        /// <summary>
        /// Reads the golden V4 request and preserves all grounding meanings.
        /// </summary>
        [Test]
        public void TryDeserializeRequest_ValidFixture_PreservesGrounding()
        {
            var succeeded = AiNpcJsonCodecV4.TryDeserializeRequest(
                LoadFixture("valid-request.json"),
                out var envelope,
                out var error);

            Assert.That(succeeded, Is.True, error);
            Assert.That(envelope.schemaVersion, Is.EqualTo(4));
            Assert.That(envelope.triggers, Is.Empty);
            var request = V4.AiNpcContractMapper.ReadRequest(envelope);
            Assert.That(request.Grounding.Facts, Has.Count.EqualTo(3));
            Assert.That(request.Grounding.Facts[0].FactId, Is.EqualTo("gate_status"));
            Assert.That(request.Grounding.Revision, Is.EqualTo(
                "ctx-0fbb1fef8071da13b9476369537500347025c3762df5df65449f89b5275022bc"));
        }

        /// <summary>
        /// Serializes the same request deterministically and round-trips its immutable snapshot.
        /// </summary>
        [Test]
        public void TrySerializeRequest_KnownSnapshot_IsDeterministic()
        {
            Assert.That(
                AiNpcJsonCodecV4.TryDeserializeRequest(
                    LoadFixture("valid-request.json"),
                    out var fixture,
                    out var fixtureError),
                Is.True,
                fixtureError);
            var request = V4.AiNpcContractMapper.ReadRequest(fixture);
            var mapped = V4.AiNpcContractMapper.CreateRequest(
                request,
                "req-v4-stable",
                "session-v4-stable",
                System.Array.Empty<NpcTriggerDefinition>());

            Assert.That(
                AiNpcJsonCodecV4.TrySerializeRequest(
                    mapped,
                    out var first,
                    out var firstError),
                Is.True,
                firstError);
            Assert.That(
                AiNpcJsonCodecV4.TrySerializeRequest(
                    mapped,
                    out var second,
                    out var secondError),
                Is.True,
                secondError);
            Assert.That(second, Is.EqualTo(first));
            Assert.That(first, Does.Contain("\"grounding\""));
            Assert.That(first, Does.Contain("\"triggers\":[]"));
        }

        /// <summary>
        /// Reads exclusive success and error branches without materializing inactive fields.
        /// </summary>
        [Test]
        public void TryDeserializeResponse_GoldenBranches_PreserveMeaning()
        {
            Assert.That(AiNpcJsonCodecV4.TryDeserializeResponse(
                LoadFixture("valid-success-response.json"),
                out var success,
                out var successError), Is.True, successError);
            Assert.That(AiNpcJsonCodecV4.TryDeserializeResponse(
                LoadFixture("valid-error-response.json"),
                out var failure,
                out var failureError), Is.True, failureError);

            Assert.That(success.error, Is.Null);
            Assert.That(success.result.matchedTriggerIds, Is.Empty);
            Assert.That(failure.result, Is.Null);
            Assert.That(failure.error.code, Is.EqualTo("session_busy"));
        }

        /// <summary>
        /// Rejects missing grounding, stale revisions, unknown tokens, and malformed JSON safely.
        /// </summary>
        [Test]
        public void TryDeserialize_InvalidV4Inputs_ReturnFalseWithoutThrowing()
        {
            var valid = LoadFixture("valid-request.json");
            Assert.DoesNotThrow(() =>
            {
                Assert.That(AiNpcJsonCodecV4.TryDeserializeRequest(
                    LoadFixture("missing-grounding-request.json"),
                    out _,
                    out _), Is.False);
                Assert.That(AiNpcJsonCodecV4.TryDeserializeRequest(
                    valid.Replace("The western gate is closed.", "The gate is open."),
                    out _,
                    out _), Is.False);
                Assert.That(AiNpcJsonCodecV4.TryDeserializeRequest(
                    valid.Replace("\"observation\"", "\"rumor\""),
                    out _,
                    out _), Is.False);
                Assert.That(AiNpcJsonCodecV4.TryDeserializeRequest(
                    LoadFixture("malformed.json"),
                    out _,
                    out _), Is.False);
            });
        }

        /// <summary>
        /// Accepts same-version additions while rejecting unknown versions, enums, and response branches.
        /// </summary>
        [Test]
        public void Validation_V4CompatibilityAndBranches_EnforcesContract()
        {
            var valid = LoadFixture("valid-request.json");
            var withAddition = valid.Insert(
                valid.IndexOf('{') + 1,
                "\n  \"futureField\": true,");
            Assert.That(AiNpcJsonCodecV4.TryDeserializeRequest(
                withAddition,
                out _,
                out var additionError), Is.True, additionError);
            Assert.That(AiNpcJsonCodecV4.TryDeserializeRequest(
                valid.Replace("\"schemaVersion\": 4", "\"schemaVersion\": 9"),
                out _,
                out _), Is.False);
            Assert.That(AiNpcJsonCodecV4.TryDeserializeRequest(
                valid.Replace(
                    "\"defaultEmotion\": \"neutral\"",
                    "\"defaultEmotion\": \"excited\""),
                out _,
                out _), Is.False);

            var bothBranches = new V4.AiNpcResponseEnvelopeDto
            {
                schemaVersion = V4.AiNpcContractV4.SchemaVersion,
                requestId = "req-v4-branches",
                status = V4.AiNpcContractV4.SuccessStatus,
                result = new V4.AiNpcResponsePayloadDto
                {
                    dialogue = "Hello.",
                    emotion = "neutral",
                    gesture = "none",
                    matchedTriggerIds = System.Array.Empty<string>()
                },
                error = new V4.AiNpcErrorDto
                {
                    code = "invalid_request",
                    message = "Invalid.",
                    retryable = false
                }
            };
            Assert.That(V4.AiNpcContractValidator.TryValidateResponse(
                bothBranches,
                out _), Is.False);
            bothBranches.error = null;
            bothBranches.status = "unknown";
            Assert.That(V4.AiNpcContractValidator.TryValidateResponse(
                bothBranches,
                out _), Is.False);
        }

        /// <summary>
        /// Round-trips the V4 reset request and success without serializing an inactive branch.
        /// </summary>
        [Test]
        public void ResetCodec_ValidRequestAndSuccess_RoundTripsExclusiveBranches()
        {
            var request = V4.AiNpcContractMapper.CreateResetRequest(
                "req-v4-reset",
                "session-v4-reset",
                "sample-guard");
            Assert.That(AiNpcJsonCodecV4.TrySerializeResetRequest(
                request,
                out var requestJson,
                out var requestError), Is.True, requestError);
            Assert.That(AiNpcJsonCodecV4.TryDeserializeResetRequest(
                requestJson,
                out var decodedRequest,
                out var decodedRequestError), Is.True, decodedRequestError);
            Assert.That(decodedRequest.sessionId, Is.EqualTo(request.sessionId));

            var response = V4.AiNpcContractMapper.CreateResetSuccessResponse(
                request.requestId);
            Assert.That(AiNpcJsonCodecV4.TrySerializeResetResponse(
                response,
                out var responseJson,
                out var responseError), Is.True, responseError);
            Assert.That(responseJson, Does.Not.Contain("\"error\""));
            Assert.That(AiNpcJsonCodecV4.TryDeserializeResetResponse(
                responseJson,
                out var decodedResponse,
                out var decodedResponseError), Is.True, decodedResponseError);
            Assert.That(decodedResponse.result.reset, Is.True);
        }

        /// <summary>
        /// Loads one fixture text through Unity's package-aware asset path.
        /// </summary>
        private static string LoadFixture(string fileName)
        {
            var asset = AssetDatabase.LoadAssetAtPath<UnityEngine.TextAsset>(
                FixtureRoot + fileName);
            Assert.That(asset, Is.Not.Null, Path.Combine(FixtureRoot, fileName));
            return asset.text;
        }
    }
}
