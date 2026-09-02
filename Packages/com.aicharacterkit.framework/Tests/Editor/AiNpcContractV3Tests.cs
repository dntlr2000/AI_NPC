using System.IO;
using AiCharacterKit.Core;
using AiCharacterKit.Unity.Transport;
using NUnit.Framework;
using UnityEditor;
using V3 = AiCharacterKit.Transport.V3;

namespace AiCharacterKit.Core.Tests
{
    /// <summary>
    /// Verifies V3 mapping, validation, and JsonUtility behavior against golden fixtures.
    /// </summary>
    public sealed class AiNpcContractV3Tests
    {
        private static string FixtureRoot => AiCharacterKitTestPaths.Resolve(
            "Tests/Editor/Fixtures/Transport/V3") + "/";

        /// <summary>
        /// Maps a golden request while keeping action IDs and examples outside the wire snapshot.
        /// </summary>
        [Test]
        public void TryDeserializeRequest_ValidFixture_PreservesTriggerBoundary()
        {
            var succeeded = AiNpcJsonCodecV3.TryDeserializeRequest(
                LoadFixture("valid-request.json"),
                out var envelope,
                out var error);

            Assert.That(succeeded, Is.True, error);
            Assert.That(envelope.schemaVersion, Is.EqualTo(3));
            Assert.That(envelope.sessionId, Is.EqualTo("session-v3-001"));
            Assert.That(envelope.triggers, Has.Length.EqualTo(1));
            Assert.That(envelope.triggers[0].triggerId, Is.EqualTo("open_gate"));
            Assert.That(
                JsonUtilityContainsForbiddenActionFields(LoadFixture("valid-request.json")),
                Is.False);
            var request = V3.AiNpcContractMapper.ReadRequest(envelope);
            Assert.That(request.CharacterId, Is.EqualTo("sample-guide"));
        }

        /// <summary>
        /// Maps golden success and error branches including matched trigger IDs.
        /// </summary>
        [Test]
        public void TryDeserializeResponse_GoldenBranches_PreserveMeaning()
        {
            Assert.That(
                AiNpcJsonCodecV3.TryDeserializeResponse(
                    LoadFixture("valid-success-response.json"),
                    out var success,
                    out var successError),
                Is.True,
                successError);
            Assert.That(
                AiNpcJsonCodecV3.TryDeserializeResponse(
                    LoadFixture("valid-error-response.json"),
                    out var failure,
                    out var failureError),
                Is.True,
                failureError);

            var response = V3.AiNpcContractMapper.ReadSuccessResponse(success);
            Assert.That(response.MatchedTriggerIds, Is.EqualTo(new[] { "open_gate" }));
            Assert.That(response.Emotion, Is.EqualTo(NpcEmotion.Happy));
            Assert.That(success.error, Is.Null);
            Assert.That(failure.result, Is.Null);
            Assert.That(failure.error.code, Is.EqualTo("session_busy"));
        }

        /// <summary>
        /// Produces deterministic request JSON and round-trips every domain response value.
        /// </summary>
        [Test]
        public void TrySerializeRequestAndResponse_RoundTripDeterministically()
        {
            var definitions = new[]
            {
                new NpcTriggerDefinition(
                    "open_gate",
                    "The player asks to open the gate.",
                    "open",
                    "open_gate_action",
                    5)
            };
            var request = V3.AiNpcContractMapper.CreateRequest(
                CreateRequest("open"),
                "req-stable",
                "session-stable",
                definitions);
            Assert.That(
                AiNpcJsonCodecV3.TrySerializeRequest(
                    request,
                    out var firstJson,
                    out var firstError),
                Is.True,
                firstError);
            Assert.That(
                AiNpcJsonCodecV3.TrySerializeRequest(
                    request,
                    out var secondJson,
                    out var secondError),
                Is.True,
                secondError);
            Assert.That(secondJson, Is.EqualTo(firstJson));
            Assert.That(firstJson, Does.Not.Contain("open_gate_action"));
            Assert.That(firstJson, Does.Not.Contain("exampleUserText"));

            var success = V3.AiNpcContractMapper.CreateSuccessResponse(
                new AiNpcResponse(
                    "Done",
                    NpcEmotion.Happy,
                    NpcGesture.Nod,
                    new[] { "open_gate" }),
                "req-stable");
            Assert.That(
                AiNpcJsonCodecV3.TrySerializeResponse(
                    success,
                    out var responseJson,
                    out var responseError),
                Is.True,
                responseError);
            Assert.That(responseJson, Does.Contain("\"matchedTriggerIds\":[\"open_gate\"]"));
        }

        /// <summary>
        /// Rejects missing snapshots, duplicate IDs, missing matched IDs, and wrong versions safely.
        /// </summary>
        [Test]
        public void TryDeserialize_InvalidV3Inputs_ReturnFalseWithoutThrowing()
        {
            Assert.DoesNotThrow(() =>
            {
                Assert.That(
                    AiNpcJsonCodecV3.TryDeserializeRequest(
                        string.Empty,
                        out _,
                        out _),
                    Is.False);
                Assert.That(
                    AiNpcJsonCodecV3.TryDeserializeRequest(
                        LoadFixture("missing-triggers-request.json"),
                        out _,
                        out _),
                    Is.False);
                Assert.That(
                    AiNpcJsonCodecV3.TryDeserializeRequest(
                        LoadFixture("valid-request.json").Replace(
                            "\"schemaVersion\": 3",
                            "\"schemaVersion\": 4"),
                        out _,
                        out _),
                    Is.False);
                Assert.That(
                    AiNpcJsonCodecV3.TryDeserializeResponse(
                        LoadFixture("valid-success-response.json").Replace(
                            "\"matchedTriggerIds\": [\"open_gate\"]",
                            "\"matchedTriggerIds\": [\"open_gate\", \"open_gate\"]"),
                        out _,
                        out _),
                    Is.False);
                Assert.That(
                    AiNpcJsonCodecV3.TryDeserializeResponse(
                        LoadFixture("valid-success-response.json")
                            .Replace("\r\n", "\n")
                            .Replace(
                                ",\n    \"matchedTriggerIds\": [\"open_gate\"]",
                                string.Empty),
                        out _,
                        out _),
                    Is.False);
                Assert.That(
                    AiNpcJsonCodecV3.TryDeserializeResponse(
                        LoadFixture("unknown-emotion-response.json"),
                        out _,
                        out _),
                    Is.False);
                Assert.That(
                    AiNpcJsonCodecV3.TryDeserializeResponse(
                        LoadFixture("invalid-branch-response.json"),
                        out _,
                        out _),
                    Is.False);
                Assert.That(
                    AiNpcJsonCodecV3.TryDeserializeRequest(
                        LoadFixture("malformed.json"),
                        out _,
                        out _),
                    Is.False);
            });
        }

        /// <summary>
        /// Confirms same-version unknown fields remain forward-compatible in V3.
        /// </summary>
        [Test]
        public void TryDeserializeRequest_UnknownAdditionalField_IsIgnored()
        {
            var json = LoadFixture("valid-request.json").Replace(
                "\n}",
                ",\n  \"futureField\": \"ignored\"\n}");

            Assert.That(
                AiNpcJsonCodecV3.TryDeserializeRequest(
                    json,
                    out var request,
                    out var error),
                Is.True,
                error);
            Assert.That(request.requestId, Is.EqualTo("req-v3-001"));
        }

        /// <summary>
        /// Confirms the standalone contract accepts a valid-shaped ID for client subset checking.
        /// </summary>
        [Test]
        public void TryDeserializeResponse_UnknownButWellFormedId_DefersSubsetCheck()
        {
            Assert.That(
                AiNpcJsonCodecV3.TryDeserializeResponse(
                    LoadFixture("unknown-trigger-response.json"),
                    out var response,
                    out var error),
                Is.True,
                error);
            Assert.That(response.result.matchedTriggerIds[0], Is.EqualTo("invented_trigger"));
        }

        /// <summary>
        /// Loads one fixture text through Unity's package-aware asset path.
        /// </summary>
        private static string LoadFixture(string fileName)
        {
            var asset = AssetDatabase.LoadAssetAtPath<UnityEngine.TextAsset>(
                FixtureRoot + fileName);
            Assert.That(asset, Is.Not.Null, FixtureRoot + fileName);
            return asset.text;
        }

        /// <summary>
        /// Detects fields that must remain local to Unity action binding data.
        /// </summary>
        private static bool JsonUtilityContainsForbiddenActionFields(string json)
        {
            return json.Contains("actionId") || json.Contains("exampleUserText");
        }

        /// <summary>
        /// Creates one valid domain request for V3 mapping tests.
        /// </summary>
        private static AiNpcRequest CreateRequest(string text)
        {
            return new AiNpcRequest(
                "sample-guide",
                "Guide",
                "Helpful",
                "Brief",
                "Hello.",
                NpcEmotion.Neutral,
                text);
        }
    }
}
