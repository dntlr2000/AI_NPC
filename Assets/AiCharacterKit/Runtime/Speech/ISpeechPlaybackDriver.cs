namespace AiCharacterKit.Speech
{
    /// <summary>
    /// Plays normalized speech through a replaceable engine-specific output.
    /// </summary>
    public interface ISpeechPlaybackDriver
    {
        /// <summary>
        /// Replaces the current clip with one complete speech result.
        /// </summary>
        void Play(SpeechAudioData audioData);

        /// <summary>
        /// Stops and releases the current clip when possible.
        /// </summary>
        void Stop();
    }
}
