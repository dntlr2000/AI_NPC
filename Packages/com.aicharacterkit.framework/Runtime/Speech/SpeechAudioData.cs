using System;

namespace AiCharacterKit.Speech
{
    /// <summary>
    /// Stores one normalized PCM16LE mono clip returned by any speech provider.
    /// </summary>
    public sealed class SpeechAudioData
    {
        public const int SampleRate = 24000;

        public const int Channels = 1;

        public const int BitsPerSample = 16;

        public const int MaximumByteCount = 8 * 1024 * 1024;

        public byte[] PcmBytes { get; }

        /// <summary>
        /// Creates validated fixed-format audio without copying the provider-owned byte buffer.
        /// </summary>
        public SpeechAudioData(byte[] pcmBytes)
        {
            if (pcmBytes == null)
            {
                throw new ArgumentNullException(nameof(pcmBytes));
            }

            if (pcmBytes.Length == 0
                || pcmBytes.Length % (BitsPerSample / 8 * Channels) != 0)
            {
                throw new ArgumentException(
                    "PCM audio must contain complete non-empty 16-bit mono samples.",
                    nameof(pcmBytes));
            }

            if (pcmBytes.Length > MaximumByteCount)
            {
                throw new ArgumentException(
                    "PCM audio exceeds the supported in-memory size.",
                    nameof(pcmBytes));
            }

            PcmBytes = pcmBytes;
        }
    }
}
