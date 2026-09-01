using System;
using AiCharacterKit.Transport.Speech.V1;
using UnityEngine;

namespace AiCharacterKit.Unity.Transport
{
    /// <summary>
    /// Serializes Speech V1 JSON request and error envelopes at the Unity boundary.
    /// </summary>
    public static class SpeechJsonCodecV1
    {
        /// <summary>
        /// Serializes one validated synthesis request without leaking serializer exceptions.
        /// </summary>
        public static bool TrySerializeRequest(
            SpeechSynthesisRequestDto request,
            out string json,
            out string error)
        {
            json = string.Empty;
            if (!SpeechContractValidator.TryValidateRequest(request, out error))
            {
                return false;
            }

            try
            {
                json = JsonUtility.ToJson(request);
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
        /// Deserializes one validated synthesis request without leaking serializer exceptions.
        /// </summary>
        public static bool TryDeserializeRequest(
            string json,
            out SpeechSynthesisRequestDto request,
            out string error)
        {
            request = null;
            if (string.IsNullOrWhiteSpace(json))
            {
                error = "JSON content must not be empty.";
                return false;
            }

            try
            {
                request = new SpeechSynthesisRequestDto();
                JsonUtility.FromJsonOverwrite(json, request);
            }
            catch (Exception exception)
            {
                request = null;
                error = $"JSON deserialization failed: {exception.Message}";
                return false;
            }

            if (!SpeechContractValidator.TryValidateRequest(request, out error))
            {
                request = null;
                return false;
            }

            error = string.Empty;
            return true;
        }

        /// <summary>
        /// Deserializes one validated JSON error without leaking serializer exceptions.
        /// </summary>
        public static bool TryDeserializeErrorResponse(
            string json,
            out SpeechErrorResponseDto response,
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
                response = new SpeechErrorResponseDto();
                JsonUtility.FromJsonOverwrite(json, response);
            }
            catch (Exception exception)
            {
                response = null;
                error = $"JSON deserialization failed: {exception.Message}";
                return false;
            }

            if (!SpeechContractValidator.TryValidateErrorResponse(response, out error))
            {
                response = null;
                return false;
            }

            error = string.Empty;
            return true;
        }
    }
}
