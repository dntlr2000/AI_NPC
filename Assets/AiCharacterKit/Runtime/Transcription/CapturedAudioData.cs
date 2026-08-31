using System;

namespace AiCharacterKit.Transcription
{
    /// <summary>
    /// Stores one complete canonical PCM16 mono WAV captured for transcription.
    /// </summary>
    public sealed class CapturedAudioData
    {
        public const int MinimumSampleRate = 8000;

        public const int MaximumSampleRate = 48000;

        public const int MaximumDurationSeconds = 15;

        public const int MaximumWaveByteCount = 2 * 1024 * 1024;

        public byte[] WaveBytes { get; }

        public int SampleRate { get; }

        public int SampleFrames { get; }

        public double DurationSeconds => (double)SampleFrames / SampleRate;

        /// <summary>
        /// Creates validated bounded capture metadata without copying the owned byte buffer.
        /// </summary>
        public CapturedAudioData(
            byte[] waveBytes,
            int sampleRate,
            int sampleFrames)
        {
            if (waveBytes == null)
            {
                throw new ArgumentNullException(nameof(waveBytes));
            }

            if (waveBytes.Length < Pcm16WaveEncoder.HeaderByteCount
                || waveBytes.Length > MaximumWaveByteCount)
            {
                throw new ArgumentException(
                    "Captured WAV must fit the supported in-memory size.",
                    nameof(waveBytes));
            }

            if (sampleRate < MinimumSampleRate || sampleRate > MaximumSampleRate)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(sampleRate),
                    "Capture sample rate is outside the supported range.");
            }

            if (sampleFrames <= 0
                || sampleFrames > sampleRate * MaximumDurationSeconds)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(sampleFrames),
                    "Capture duration must be greater than zero and at most 15 seconds.");
            }

            if (waveBytes.Length != Pcm16WaveEncoder.HeaderByteCount
                + sampleFrames * 2)
            {
                throw new ArgumentException(
                    "Captured WAV byte count does not match its PCM16 mono frames.",
                    nameof(waveBytes));
            }

            WaveBytes = waveBytes;
            SampleRate = sampleRate;
            SampleFrames = sampleFrames;
        }
    }
}
