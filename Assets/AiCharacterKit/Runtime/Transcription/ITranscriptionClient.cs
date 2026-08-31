using System.Threading;
using System.Threading.Tasks;

namespace AiCharacterKit.Transcription
{
    /// <summary>
    /// Converts one complete captured clip into provider-neutral text.
    /// </summary>
    public interface ITranscriptionClient
    {
        /// <summary>
        /// Transcribes one bounded WAV payload while honoring caller cancellation.
        /// </summary>
        Task<TranscriptionResult> TranscribeAsync(
            CapturedAudioData audioData,
            CancellationToken cancellationToken);
    }
}
