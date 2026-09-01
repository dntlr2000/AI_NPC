using System;
using AiCharacterKit.Transport.Transcription.V1;
using UnityEngine;

namespace AiCharacterKit.Unity.Transport
{
    /// <summary>
    /// Serializes Transcription V1 JSON responses only at the Unity boundary.
    /// </summary>
    public static class TranscriptionJsonCodecV1
    {
        private enum TopLevelPropertyValue
        {
            Absent = 0,
            Null = 1,
            Other = 2
        }

        /// <summary>
        /// Omits the inactive error branch from canonical success JSON.
        /// </summary>
        [Serializable]
        private sealed class SuccessResponseJsonDto
        {
            public int schemaVersion;

            public string requestId = string.Empty;

            public string status = string.Empty;

            public TranscriptionResultDto result;
        }

        /// <summary>
        /// Omits the inactive result branch from canonical error JSON.
        /// </summary>
        [Serializable]
        private sealed class ErrorResponseJsonDto
        {
            public int schemaVersion;

            public string requestId = string.Empty;

            public string status = string.Empty;

            public TranscriptionErrorDto error;
        }

        /// <summary>
        /// Serializes one validated response without leaking serializer exceptions.
        /// </summary>
        public static bool TrySerializeResponse(
            TranscriptionResponseEnvelopeDto response,
            out string json,
            out string error)
        {
            json = string.Empty;
            if (!TranscriptionContractValidator.TryValidateResponse(
                    response,
                    out error))
            {
                return false;
            }

            try
            {
                if (response.status == TranscriptionContractV1.SuccessStatus)
                {
                    json = JsonUtility.ToJson(new SuccessResponseJsonDto
                    {
                        schemaVersion = response.schemaVersion,
                        requestId = response.requestId,
                        status = response.status,
                        result = response.result
                    });
                }
                else
                {
                    json = JsonUtility.ToJson(new ErrorResponseJsonDto
                    {
                        schemaVersion = response.schemaVersion,
                        requestId = response.requestId,
                        status = response.status,
                        error = response.error
                    });
                }

                if (string.IsNullOrWhiteSpace(json))
                {
                    error = "JSON serialization returned no content.";
                    return false;
                }

                error = string.Empty;
                return true;
            }
            catch (Exception exception)
            {
                json = string.Empty;
                error = $"JSON serialization failed: {exception.Message}";
                return false;
            }
        }

        /// <summary>
        /// Deserializes one validated response without leaking serializer exceptions.
        /// </summary>
        public static bool TryDeserializeResponse(
            string json,
            out TranscriptionResponseEnvelopeDto response,
            out string error)
        {
            response = null;
            if (string.IsNullOrWhiteSpace(json))
            {
                error = "JSON content must not be empty.";
                return false;
            }

            try
            {
                response = new TranscriptionResponseEnvelopeDto();
                JsonUtility.FromJsonOverwrite(json, response);
                NormalizeJsonUtilityDefaults(response, json);
            }
            catch (Exception exception)
            {
                response = null;
                error = $"JSON deserialization failed: {exception.Message}";
                return false;
            }

            if (!TranscriptionContractValidator.TryValidateResponse(
                    response,
                    out error))
            {
                response = null;
                return false;
            }

            error = string.Empty;
            return true;
        }

        /// <summary>
        /// Restores omitted inactive branches that JsonUtility materializes as empty objects.
        /// </summary>
        private static void NormalizeJsonUtilityDefaults(
            TranscriptionResponseEnvelopeDto response,
            string json)
        {
            if (response.status == TranscriptionContractV1.SuccessStatus
                && GetTopLevelPropertyValue(json, "error")
                    != TopLevelPropertyValue.Other
                && IsEmptyError(response.error))
            {
                response.error = null;
            }
            else if (response.status == TranscriptionContractV1.ErrorStatus
                && GetTopLevelPropertyValue(json, "result")
                    != TopLevelPropertyValue.Other
                && IsEmptyResult(response.result))
            {
                response.result = null;
            }
        }

        /// <summary>
        /// Classifies one root property as absent, explicitly null, or another JSON value.
        /// </summary>
        private static TopLevelPropertyValue GetTopLevelPropertyValue(
            string json,
            string propertyName)
        {
            var depth = 0;
            var isInsideString = false;
            var isEscaped = false;
            var tokenStart = -1;

            for (var index = 0; index < json.Length; index++)
            {
                var character = json[index];
                if (isInsideString)
                {
                    if (isEscaped)
                    {
                        isEscaped = false;
                        continue;
                    }

                    if (character == '\\')
                    {
                        isEscaped = true;
                        continue;
                    }

                    if (character != '"')
                    {
                        continue;
                    }

                    isInsideString = false;
                    var tokenLength = index - tokenStart;
                    if (depth != 1
                        || tokenLength != propertyName.Length
                        || string.CompareOrdinal(
                            json,
                            tokenStart,
                            propertyName,
                            0,
                            propertyName.Length) != 0)
                    {
                        continue;
                    }

                    var nextIndex = index + 1;
                    while (nextIndex < json.Length
                        && char.IsWhiteSpace(json[nextIndex]))
                    {
                        nextIndex++;
                    }

                    if (nextIndex >= json.Length || json[nextIndex] != ':')
                    {
                        continue;
                    }

                    nextIndex++;
                    while (nextIndex < json.Length
                        && char.IsWhiteSpace(json[nextIndex]))
                    {
                        nextIndex++;
                    }

                    if (nextIndex + 4 <= json.Length
                        && string.CompareOrdinal(
                            json,
                            nextIndex,
                            "null",
                            0,
                            4) == 0
                        && IsJsonValueBoundary(json, nextIndex + 4))
                    {
                        return TopLevelPropertyValue.Null;
                    }

                    return TopLevelPropertyValue.Other;
                }

                if (character == '"')
                {
                    isInsideString = true;
                    tokenStart = index + 1;
                }
                else if (character == '{' || character == '[')
                {
                    depth++;
                }
                else if (character == '}' || character == ']')
                {
                    depth--;
                }
            }

            return TopLevelPropertyValue.Absent;
        }

        /// <summary>
        /// Verifies that a recognized JSON literal ends at a valid value boundary.
        /// </summary>
        private static bool IsJsonValueBoundary(string json, int index)
        {
            if (index >= json.Length)
            {
                return true;
            }

            var character = json[index];
            return char.IsWhiteSpace(character)
                || character == ','
                || character == '}'
                || character == ']';
        }

        /// <summary>
        /// Detects an inactive error object containing only JsonUtility defaults.
        /// </summary>
        private static bool IsEmptyError(TranscriptionErrorDto error)
        {
            return error != null
                && string.IsNullOrEmpty(error.code)
                && string.IsNullOrEmpty(error.message)
                && !error.retryable;
        }

        /// <summary>
        /// Detects an inactive result object containing only JsonUtility defaults.
        /// </summary>
        private static bool IsEmptyResult(TranscriptionResultDto result)
        {
            return result != null && string.IsNullOrEmpty(result.text);
        }
    }
}
