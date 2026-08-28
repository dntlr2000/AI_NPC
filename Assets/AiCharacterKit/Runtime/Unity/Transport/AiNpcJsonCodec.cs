using System;
using AiCharacterKit.Transport.V1;
using UnityEngine;

namespace AiCharacterKit.Unity.Transport
{
    /// <summary>
    /// Serializes validated V1 envelopes with Unity's built-in JSON implementation.
    /// </summary>
    public static class AiNpcJsonCodec
    {
        private delegate bool ContractValidator<T>(T value, out string error);

        private enum TopLevelPropertyValue
        {
            Absent,
            Null,
            Other
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

            public AiNpcResponsePayloadDto result;
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

            public AiNpcErrorDto error;
        }

        /// <summary>
        /// Serializes one valid request without allowing validation or JSON exceptions to escape.
        /// </summary>
        public static bool TrySerializeRequest(
            AiNpcRequestEnvelopeDto request,
            out string json,
            out string error)
        {
            return TrySerialize(
                request,
                AiNpcContractValidator.TryValidateRequest,
                out json,
                out error);
        }

        /// <summary>
        /// Deserializes and validates one request without allowing malformed JSON to escape.
        /// </summary>
        public static bool TryDeserializeRequest(
            string json,
            out AiNpcRequestEnvelopeDto request,
            out string error)
        {
            return TryDeserialize(
                json,
                AiNpcContractValidator.TryValidateRequest,
                out request,
                out error);
        }

        /// <summary>
        /// Serializes one valid response without allowing validation or JSON exceptions to escape.
        /// </summary>
        public static bool TrySerializeResponse(
            AiNpcResponseEnvelopeDto response,
            out string json,
            out string error)
        {
            json = string.Empty;
            if (!AiNpcContractValidator.TryValidateResponse(response, out error))
            {
                return false;
            }

            try
            {
                if (response.status == AiNpcContractV1.SuccessStatus)
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
        /// Deserializes and validates one response without allowing malformed JSON to escape.
        /// </summary>
        public static bool TryDeserializeResponse(
            string json,
            out AiNpcResponseEnvelopeDto response,
            out string error)
        {
            return TryDeserialize(
                json,
                AiNpcContractValidator.TryValidateResponse,
                out response,
                out error);
        }

        /// <summary>
        /// Validates and serializes one supported reference DTO through JsonUtility.
        /// </summary>
        private static bool TrySerialize<T>(
            T value,
            ContractValidator<T> validator,
            out string json,
            out string error)
            where T : class
        {
            json = string.Empty;
            if (!validator(value, out error))
            {
                return false;
            }

            try
            {
                json = JsonUtility.ToJson(value);
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
        /// Deserializes and validates one supported reference DTO through JsonUtility.
        /// </summary>
        private static bool TryDeserialize<T>(
            string json,
            ContractValidator<T> validator,
            out T value,
            out string error)
            where T : class, new()
        {
            value = null;
            if (string.IsNullOrWhiteSpace(json))
            {
                error = "JSON content must not be empty.";
                return false;
            }

            try
            {
                value = new T();
                JsonUtility.FromJsonOverwrite(json, value);
                NormalizeJsonUtilityDefaults(value, json);
            }
            catch (Exception exception)
            {
                value = null;
                error = $"JSON deserialization failed: {exception.Message}";
                return false;
            }

            if (!validator(value, out error))
            {
                value = null;
                return false;
            }

            error = string.Empty;
            return true;
        }

        /// <summary>
        /// Restores absent response branches that JsonUtility materializes as empty objects.
        /// </summary>
        private static void NormalizeJsonUtilityDefaults<T>(T value, string json)
            where T : class
        {
            if (!(value is AiNpcResponseEnvelopeDto response))
            {
                return;
            }

            if (response.status == AiNpcContractV1.SuccessStatus
                && GetTopLevelPropertyValue(json, "error")
                    != TopLevelPropertyValue.Other
                && IsEmptyError(response.error))
            {
                response.error = null;
            }
            else if (response.status == AiNpcContractV1.ErrorStatus
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
        /// Verifies that a recognized JSON literal ends before a delimiter or document end.
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
        private static bool IsEmptyError(AiNpcErrorDto error)
        {
            return error != null
                && string.IsNullOrEmpty(error.code)
                && string.IsNullOrEmpty(error.message)
                && !error.retryable;
        }

        /// <summary>
        /// Detects an inactive result object containing only JsonUtility defaults.
        /// </summary>
        private static bool IsEmptyResult(AiNpcResponsePayloadDto result)
        {
            return result != null
                && string.IsNullOrEmpty(result.dialogue)
                && string.IsNullOrEmpty(result.emotion)
                && string.IsNullOrEmpty(result.gesture);
        }
    }
}
