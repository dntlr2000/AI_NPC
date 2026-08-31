namespace AiCharacterKit.Transcription
{
    /// <summary>
    /// Captures one bounded audio clip without exposing an engine microphone API.
    /// </summary>
    public interface IAudioCaptureDriver
    {
        /// <summary>
        /// Starts one exclusive microphone capture operation.
        /// </summary>
        void StartCapture();

        /// <summary>
        /// Stops capture and returns one complete canonical WAV payload.
        /// </summary>
        CapturedAudioData StopCapture();

        /// <summary>
        /// Abandons active capture without returning recorded audio.
        /// </summary>
        void CancelCapture();
    }
}
