using System;

namespace AiCharacterKit.Transcription
{
    /// <summary>
    /// Encodes interleaved float samples into a canonical 44-byte-header PCM16 mono WAV.
    /// </summary>
    public static class Pcm16WaveEncoder
    {
        public const int HeaderByteCount = 44;

        /// <summary>
        /// Downmixes the requested complete frames and encodes their actual captured length.
        /// </summary>
        public static CapturedAudioData Encode(
            float[] interleavedSamples,
            int channels,
            int sampleRate,
            int sampleFrames)
        {
            if (interleavedSamples == null)
            {
                throw new ArgumentNullException(nameof(interleavedSamples));
            }

            if (channels <= 0 || channels > 16)
            {
                throw new ArgumentOutOfRangeException(nameof(channels));
            }

            if (sampleRate < CapturedAudioData.MinimumSampleRate
                || sampleRate > CapturedAudioData.MaximumSampleRate)
            {
                throw new ArgumentOutOfRangeException(nameof(sampleRate));
            }

            if (sampleFrames <= 0
                || sampleFrames > sampleRate * CapturedAudioData.MaximumDurationSeconds
                || sampleFrames > interleavedSamples.Length / channels)
            {
                throw new ArgumentOutOfRangeException(nameof(sampleFrames));
            }

            var dataByteCount = checked(sampleFrames * 2);
            var totalByteCount = checked(HeaderByteCount + dataByteCount);
            if (totalByteCount > CapturedAudioData.MaximumWaveByteCount)
            {
                throw new ArgumentException(
                    "Encoded WAV exceeds the supported in-memory size.",
                    nameof(interleavedSamples));
            }

            var waveBytes = new byte[totalByteCount];
            WriteAscii(waveBytes, 0, "RIFF");
            WriteInt32(waveBytes, 4, totalByteCount - 8);
            WriteAscii(waveBytes, 8, "WAVE");
            WriteAscii(waveBytes, 12, "fmt ");
            WriteInt32(waveBytes, 16, 16);
            WriteInt16(waveBytes, 20, 1);
            WriteInt16(waveBytes, 22, 1);
            WriteInt32(waveBytes, 24, sampleRate);
            WriteInt32(waveBytes, 28, sampleRate * 2);
            WriteInt16(waveBytes, 32, 2);
            WriteInt16(waveBytes, 34, 16);
            WriteAscii(waveBytes, 36, "data");
            WriteInt32(waveBytes, 40, dataByteCount);

            for (var frame = 0; frame < sampleFrames; frame++)
            {
                double mixed = 0d;
                var frameOffset = frame * channels;
                for (var channel = 0; channel < channels; channel++)
                {
                    mixed += interleavedSamples[frameOffset + channel];
                }

                var normalized = Math.Max(-1d, Math.Min(1d, mixed / channels));
                var scaled = normalized >= 0d
                    ? normalized * short.MaxValue
                    : normalized * -short.MinValue;
                var sample = (short)Math.Round(
                    scaled,
                    MidpointRounding.AwayFromZero);
                WriteInt16(waveBytes, HeaderByteCount + frame * 2, sample);
            }

            return new CapturedAudioData(waveBytes, sampleRate, sampleFrames);
        }

        /// <summary>
        /// Writes one four-character WAV marker at an exact byte offset.
        /// </summary>
        private static void WriteAscii(byte[] bytes, int offset, string value)
        {
            for (var index = 0; index < value.Length; index++)
            {
                bytes[offset + index] = (byte)value[index];
            }
        }

        /// <summary>
        /// Writes one signed 16-bit integer using WAV little-endian byte order.
        /// </summary>
        private static void WriteInt16(byte[] bytes, int offset, int value)
        {
            bytes[offset] = (byte)value;
            bytes[offset + 1] = (byte)(value >> 8);
        }

        /// <summary>
        /// Writes one signed 32-bit integer using WAV little-endian byte order.
        /// </summary>
        private static void WriteInt32(byte[] bytes, int offset, int value)
        {
            bytes[offset] = (byte)value;
            bytes[offset + 1] = (byte)(value >> 8);
            bytes[offset + 2] = (byte)(value >> 16);
            bytes[offset + 3] = (byte)(value >> 24);
        }
    }
}
