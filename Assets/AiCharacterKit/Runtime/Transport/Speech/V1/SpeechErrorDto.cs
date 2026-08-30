using System;

namespace AiCharacterKit.Transport.Speech.V1
{
    /// <summary>
    /// Carries one safe extensible speech error over the wire.
    /// </summary>
    [Serializable]
    public sealed class SpeechErrorDto
    {
        public string code = string.Empty;

        public string message = string.Empty;

        public bool retryable;
    }
}
