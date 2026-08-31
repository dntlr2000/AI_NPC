using System;

namespace AiCharacterKit.Transport.Transcription.V1
{
    /// <summary>
    /// Carries one successful bounded transcription result.
    /// </summary>
    [Serializable]
    public sealed class TranscriptionResultDto
    {
        public string text;
    }
}
