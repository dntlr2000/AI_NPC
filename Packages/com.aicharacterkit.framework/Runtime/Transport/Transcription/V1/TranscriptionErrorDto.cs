using System;

namespace AiCharacterKit.Transport.Transcription.V1
{
    /// <summary>
    /// Carries one safe extensible transcription failure.
    /// </summary>
    [Serializable]
    public sealed class TranscriptionErrorDto
    {
        public string code;

        public string message;

        public bool retryable;
    }
}
