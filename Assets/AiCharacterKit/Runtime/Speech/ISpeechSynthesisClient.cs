using System.Threading;
using System.Threading.Tasks;

namespace AiCharacterKit.Speech
{
    /// <summary>
    /// Generates normalized speech without exposing a concrete model or transport.
    /// </summary>
    public interface ISpeechSynthesisClient
    {
        /// <summary>
        /// Synthesizes one request into fixed-format PCM audio.
        /// </summary>
        Task<SpeechAudioData> SynthesizeAsync(
            SpeechSynthesisRequest request,
            CancellationToken cancellationToken);
    }
}
