using System;
using AiCharacterKit.Transport.V2;
using UnityEngine;

namespace AiCharacterKit.Unity.Transport
{
    /// <summary>
    /// Serializes validated V2 conversation and reset envelopes with JsonUtility.
    /// </summary>
    public static class AiNpcJsonCodecV2
    {
        private delegate bool ContractValidator<T>(T value, out string error);

        private enum TopLevelPropertyValue
        {
            Absent,
            Null,
            Other
        }

        [Serializable]
        private sealed class SuccessResponseJsonDto
        {
            public int schemaVersion;
            public string requestId = string.Empty;
            public string status = string.Empty;
            public AiNpcResponsePayloadDto result;
        }

        [Serializable]
        private sealed class ErrorResponseJsonDto
        {
            public int schemaVersion;
            public string requestId = string.Empty;
            public string status = string.Empty;
            public AiNpcErrorDto error;
        }

        [Serializable]
        private sealed class ResetSuccessResponseJsonDto
        {
            public int schemaVersion;
            public string requestId = string.Empty;
            public string status = string.Empty;
            public AiNpcSessionResetResultDto result;
        }

        /// <summary>
        /// Serializes one valid session-aware conversation request.
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
        /// Deserializes and validates one session-aware conversation request.
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
        /// Serializes one valid conversation response with only its active branch.
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
                json = response.status == AiNpcContractV2.SuccessStatus
                    ? JsonUtility.ToJson(new SuccessResponseJsonDto
                    {
                        schemaVersion = response.schemaVersion,
                        requestId = response.requestId,
                        status = response.status,
                        result = response.result
                    })
                    : JsonUtility.ToJson(new ErrorResponseJsonDto
                    {
                        schemaVersion = response.schemaVersion,
                        requestId = response.requestId,
                        status = response.status,
                        error = response.error
                    });

                return TryAcceptSerializedJson(json, out error);
            }
            catch (Exception exception)
            {
                json = string.Empty;
                error = $"JSON serialization failed: {exception.Message}";
                return false;
            }
        }

        /// <summary>
        /// Deserializes and validates one session-aware conversation response.
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
        /// Serializes one valid request to clear a session.
        /// </summary>
        public static bool TrySerializeResetRequest(
            AiNpcSessionResetRequestDto request,
            out string json,
            out string error)
        {
            return TrySerialize(
                request,
                AiNpcContractValidator.TryValidateResetRequest,
                out json,
                out error);
        }

        /// <summary>
        /// Deserializes and validates one request to clear a session.
        /// </summary>
        public static bool TryDeserializeResetRequest(
            string json,
            out AiNpcSessionResetRequestDto request,
            out string error)
        {
            return TryDeserialize(
                json,
                AiNpcContractValidator.TryValidateResetRequest,
                out request,
                out error);
        }

        /// <summary>
        /// Serializes one reset acknowledgement or error with only its active branch.
        /// </summary>
        public static bool TrySerializeResetResponse(
            AiNpcSessionResetResponseDto response,
            out string json,
            out string error)
        {
            json = string.Empty;
            if (!AiNpcContractValidator.TryValidateResetResponse(response, out error))
            {
                return false;
            }

            try
            {
                json = response.status == AiNpcContractV2.SuccessStatus
                    ? JsonUtility.ToJson(new ResetSuccessResponseJsonDto
                    {
                        schemaVersion = response.schemaVersion,
                        requestId = response.requestId,
                        status = response.status,
                        result = response.result
                    })
                    : JsonUtility.ToJson(new ErrorResponseJsonDto
                    {
                        schemaVersion = response.schemaVersion,
                        requestId = response.requestId,
                        status = response.status,
                        error = response.error
                    });

                return TryAcceptSerializedJson(json, out error);
            }
            catch (Exception exception)
            {
                json = string.Empty;
                error = $"JSON serialization failed: {exception.Message}";
                return false;
            }
        }

        /// <summary>
        /// Deserializes and validates one reset acknowledgement or safe error.
        /// </summary>
        public static bool TryDeserializeResetResponse(
            string json,
            out AiNpcSessionResetResponseDto response,
            out string error)
        {
            return TryDeserialize(
                json,
                AiNpcContractValidator.TryValidateResetResponse,
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
                return TryAcceptSerializedJson(json, out error);
            }
            catch (Exception exception)
            {
                json = string.Empty;
                error = $"JSON serialization failed: {exception.Message}";
                return false;
            }
        }

        /// <summary>
        /// Rejects empty serializer output and clears the success error message.
        /// </summary>
        private static bool TryAcceptSerializedJson(string json, out string error)
        {
            if (string.IsNullOrWhiteSpace(json))
            {
                error = "JSON serialization returned no content.";
                return false;
            }

            error = string.Empty;
            return true;
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
        /// Restores absent inactive branches that JsonUtility materializes as empty objects.
        /// </summary>
        private static void NormalizeJsonUtilityDefaults<T>(T value, string json)
            where T : class
        {
            if (value is AiNpcResponseEnvelopeDto response)
            {
                NormalizeConversationResponse(response, json);
            }
            else if (value is AiNpcSessionResetResponseDto resetResponse)
            {
                NormalizeResetResponse(resetResponse, json);
            }
        }

        /// <summary>
        /// Restores omitted inactive branches on a conversation response.
        /// </summary>
        private static void NormalizeConversationResponse(
            AiNpcResponseEnvelopeDto response,
            string json)
        {
            if (response.status == AiNpcContractV2.SuccessStatus
                && GetTopLevelPropertyValue(json, "error")
                    != TopLevelPropertyValue.Other
                && IsEmptyError(response.error))
            {
                response.error = null;
            }
            else if (response.status == AiNpcContractV2.ErrorStatus
                && GetTopLevelPropertyValue(json, "result")
                    != TopLevelPropertyValue.Other
                && IsEmptyResult(response.result))
            {
                response.result = null;
            }
        }

        /// <summary>
        /// Restores omitted inactive branches on a reset response.
        /// </summary>
        private static void NormalizeResetResponse(
            AiNpcSessionResetResponseDto response,
            string json)
        {
            if (response.status == AiNpcContractV2.SuccessStatus
                && GetTopLevelPropertyValue(json, "error")
                    != TopLevelPropertyValue.Other
                && IsEmptyError(response.error))
            {
                response.error = null;
            }
            else if (response.status == AiNpcContractV2.ErrorStatus
                && GetTopLevelPropertyValue(json, "result")
                    != TopLevelPropertyValue.Other
                && IsEmptyResetResult(response.result))
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
                    while (nextIndex < json.Length && char.IsWhiteSpace(json[nextIndex]))
                    {
                        nextIndex++;
                    }

                    if (nextIndex >= json.Length || json[nextIndex] != ':')
                    {
                        continue;
                    }

                    nextIndex++;
                    while (nextIndex < json.Length && char.IsWhiteSpace(json[nextIndex]))
                    {
                        nextIndex++;
                    }

                    if (nextIndex + 4 <= json.Length
                        && string.CompareOrdinal(json, nextIndex, "null", 0, 4) == 0
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
        /// Detects an inactive conversation result containing only defaults.
        /// </summary>
        private static bool IsEmptyResult(AiNpcResponsePayloadDto result)
        {
            return result != null
                && string.IsNullOrEmpty(result.dialogue)
                && string.IsNullOrEmpty(result.emotion)
                && string.IsNullOrEmpty(result.gesture);
        }

        /// <summary>
        /// Detects an inactive reset result containing only its default flag.
        /// </summary>
        private static bool IsEmptyResetResult(AiNpcSessionResetResultDto result)
        {
            return result != null && !result.reset;
        }
    }
}
