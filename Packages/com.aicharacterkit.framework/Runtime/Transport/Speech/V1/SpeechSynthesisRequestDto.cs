using System;

namespace AiCharacterKit.Transport.Speech.V1
{
    /// <summary>
    /// Carries one provider-neutral speech request over the versioned wire contract.
    /// </summary>
    [Serializable]
    public sealed class SpeechSynthesisRequestDto
    {
        public int schemaVersion;

        public string requestId = string.Empty;

        public string voicePresetId = string.Empty;

        public string text = string.Empty;
    }
}
