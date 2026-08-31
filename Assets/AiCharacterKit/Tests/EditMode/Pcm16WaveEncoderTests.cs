using System;
using AiCharacterKit.Transcription;
using NUnit.Framework;

namespace AiCharacterKit.Core.Tests
{
    /// <summary>
    /// Verifies canonical WAV headers, actual lengths, downmixing, and float clamping.
    /// </summary>
    public sealed class Pcm16WaveEncoderTests
    {
        /// <summary>
        /// Writes exact PCM16 mono metadata and only the requested sample frames.
        /// </summary>
        [Test]
        public void Encode_ActualFrames_WritesCanonicalHeader()
        {
            var audio = Pcm16WaveEncoder.Encode(
                new[] { 0f, 0.25f, 0.5f, 0.75f },
                1,
                16000,
                3);

            Assert.That(audio.WaveBytes.Length, Is.EqualTo(44 + 6));
            Assert.That(ReadAscii(audio.WaveBytes, 0, 4), Is.EqualTo("RIFF"));
            Assert.That(ReadAscii(audio.WaveBytes, 8, 4), Is.EqualTo("WAVE"));
            Assert.That(ReadInt32(audio.WaveBytes, 24), Is.EqualTo(16000));
            Assert.That(ReadInt16(audio.WaveBytes, 22), Is.EqualTo(1));
            Assert.That(ReadInt16(audio.WaveBytes, 34), Is.EqualTo(16));
            Assert.That(ReadInt32(audio.WaveBytes, 40), Is.EqualTo(6));
            Assert.That(audio.SampleFrames, Is.EqualTo(3));
        }

        /// <summary>
        /// Downmixes stereo frames and clamps samples to valid signed PCM16 range.
        /// </summary>
        [Test]
        public void Encode_StereoAndOutOfRange_DownmixesAndClamps()
        {
            var audio = Pcm16WaveEncoder.Encode(
                new[] { 1f, -1f, 2f, 2f, -2f, -2f },
                2,
                16000,
                3);

            Assert.That(ReadInt16(audio.WaveBytes, 44), Is.EqualTo(0));
            Assert.That(ReadInt16(audio.WaveBytes, 46), Is.EqualTo(short.MaxValue));
            Assert.That(ReadInt16(audio.WaveBytes, 48), Is.EqualTo(short.MinValue));
        }

        /// <summary>
        /// Rejects empty, inconsistent, unsupported-rate, and over-duration input.
        /// </summary>
        [Test]
        public void Encode_InvalidInput_ThrowsBeforeAllocation()
        {
            Assert.Throws<ArgumentNullException>(
                () => Pcm16WaveEncoder.Encode(null, 1, 16000, 1));
            Assert.Throws<ArgumentOutOfRangeException>(
                () => Pcm16WaveEncoder.Encode(new[] { 0f }, 0, 16000, 1));
            Assert.Throws<ArgumentOutOfRangeException>(
                () => Pcm16WaveEncoder.Encode(new[] { 0f }, 1, 4000, 1));
            Assert.Throws<ArgumentOutOfRangeException>(
                () => Pcm16WaveEncoder.Encode(new[] { 0f }, 1, 16000, 2));
        }

        /// <summary>
        /// Reads one ASCII marker without a platform encoding dependency.
        /// </summary>
        private static string ReadAscii(byte[] bytes, int offset, int count)
        {
            return System.Text.Encoding.ASCII.GetString(bytes, offset, count);
        }

        /// <summary>
        /// Reads one little-endian signed 16-bit integer.
        /// </summary>
        private static short ReadInt16(byte[] bytes, int offset)
        {
            return BitConverter.ToInt16(bytes, offset);
        }

        /// <summary>
        /// Reads one little-endian signed 32-bit integer.
        /// </summary>
        private static int ReadInt32(byte[] bytes, int offset)
        {
            return BitConverter.ToInt32(bytes, offset);
        }
    }
}
