using System;

namespace AiCharacterKit.Transport.Transcription.V1
{
    /// <summary>
    /// Represents the mutually exclusive success or error Transcription V1 response.
    /// </summary>
    [Serializable]
    public sealed class TranscriptionResponseEnvelopeDto
    {
        public int schemaVersion;

        public string requestId;

        public string status;

        public TranscriptionResultDto result;

        public TranscriptionErrorDto error;
    }
}
