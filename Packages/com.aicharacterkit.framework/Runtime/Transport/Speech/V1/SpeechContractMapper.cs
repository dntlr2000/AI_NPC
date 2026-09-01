using System;
using AiCharacterKit.Speech;

namespace AiCharacterKit.Transport.Speech.V1
{
    /// <summary>
    /// Maps provider-neutral speech requests to the versioned transport DTO.
    /// </summary>
    public static class SpeechContractMapper
    {
        /// <summary>
        /// Creates one validated request DTO with caller-owned correlation metadata.
        /// </summary>
        public static SpeechSynthesisRequestDto CreateRequest(
            SpeechSynthesisRequest request,
            string requestId)
        {
            if (request == null)
            {
                throw new ArgumentNullException(nameof(request));
            }

            var dto = new SpeechSynthesisRequestDto
            {
                schemaVersion = SpeechContractV1.SchemaVersion,
                requestId = requestId ?? string.Empty,
                voicePresetId = request.VoicePresetId,
                text = request.Text
            };

            if (!SpeechContractValidator.TryValidateRequest(dto, out var error))
            {
                throw new ArgumentException(error, nameof(request));
            }

            return dto;
        }

        /// <summary>
        /// Reconstructs a provider-neutral request from one valid DTO.
        /// </summary>
        public static SpeechSynthesisRequest ReadRequest(
            SpeechSynthesisRequestDto dto)
        {
            if (!SpeechContractValidator.TryValidateRequest(dto, out var error))
            {
                throw new ArgumentException(error, nameof(dto));
            }

            return new SpeechSynthesisRequest(dto.voicePresetId, dto.text);
        }
    }
}
