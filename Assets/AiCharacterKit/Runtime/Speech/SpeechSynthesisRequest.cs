namespace AiCharacterKit.Speech
{
    /// <summary>
    /// Carries provider-neutral text and voice preset data for one speech operation.
    /// </summary>
    public sealed class SpeechSynthesisRequest
    {
        public string VoicePresetId { get; }

        public string Text { get; }

        /// <summary>
        /// Creates one immutable synthesis request without assigning provider-specific options.
        /// </summary>
        public SpeechSynthesisRequest(string voicePresetId, string text)
        {
            VoicePresetId = voicePresetId ?? string.Empty;
            Text = text ?? string.Empty;
        }
    }
}
