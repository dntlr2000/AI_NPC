namespace AiCharacterKit.Transcription
{
    /// <summary>
    /// Describes the externally visible state of one push-to-talk operation.
    /// </summary>
    public enum VoiceInputState
    {
        Idle = 0,
        Recording = 1,
        Transcribing = 2,
        Failed = 3
    }
}
