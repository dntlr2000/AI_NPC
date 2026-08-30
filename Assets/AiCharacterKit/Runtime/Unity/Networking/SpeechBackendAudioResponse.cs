using System;

namespace AiCharacterKit.Unity.Networking
{
    /// <summary>
    /// Carries binary speech and its echoed correlation value across the gateway boundary.
    /// </summary>
    public sealed class SpeechBackendAudioResponse
    {
        public string RequestId { get; }

        public byte[] PcmBytes { get; }

        /// <summary>
        /// Creates one immutable gateway result without interpreting its PCM payload.
        /// </summary>
        public SpeechBackendAudioResponse(string requestId, byte[] pcmBytes)
        {
            RequestId = requestId ?? string.Empty;
            PcmBytes = pcmBytes ?? throw new ArgumentNullException(nameof(pcmBytes));
        }
    }
}
