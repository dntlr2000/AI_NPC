using System;

namespace AiCharacterKit.Transport.Speech.V1
{
    /// <summary>
    /// Defines the only JSON response branch used by the binary Speech V1 endpoint.
    /// </summary>
    [Serializable]
    public sealed class SpeechErrorResponseDto
    {
        public int schemaVersion;

        public string requestId = string.Empty;

        public string status = string.Empty;

        public SpeechErrorDto error;
    }
}
