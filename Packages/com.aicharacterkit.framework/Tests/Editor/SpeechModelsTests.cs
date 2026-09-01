using System;
using AiCharacterKit.Speech;
using AiCharacterKit.Unity.Speech;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace AiCharacterKit.Core.Tests
{
    /// <summary>
    /// Verifies provider-neutral speech data and Unity voice profile validation.
    /// </summary>
    public sealed class SpeechModelsTests
    {
        /// <summary>
        /// Accepts complete PCM16 samples and preserves the caller-owned byte sequence.
        /// </summary>
        [Test]
        public void SpeechAudioData_ValidPcm_PreservesFixedFormatData()
        {
            var bytes = new byte[] { 0, 0, 255, 127 };
            var audio = new SpeechAudioData(bytes);

            Assert.That(audio.PcmBytes, Is.SameAs(bytes));
            Assert.That(SpeechAudioData.SampleRate, Is.EqualTo(24000));
            Assert.That(SpeechAudioData.Channels, Is.EqualTo(1));
            Assert.That(SpeechAudioData.BitsPerSample, Is.EqualTo(16));
        }

        /// <summary>
        /// Rejects absent, empty, partial, and over-limit PCM buffers.
        /// </summary>
        [Test]
        public void SpeechAudioData_InvalidPcm_ThrowsArgumentErrors()
        {
            Assert.Throws<ArgumentNullException>(() => new SpeechAudioData(null));
            Assert.Throws<ArgumentException>(() => new SpeechAudioData(Array.Empty<byte>()));
            Assert.Throws<ArgumentException>(() => new SpeechAudioData(new byte[3]));
            Assert.Throws<ArgumentException>(
                () => new SpeechAudioData(
                    new byte[SpeechAudioData.MaximumByteCount + 2]));
        }

        /// <summary>
        /// Validates reusable preset IDs stored in a ScriptableObject without provider options.
        /// </summary>
        [Test]
        public void NpcVoiceProfile_PresetToken_ValidatesExpectedGrammar()
        {
            var profile = ScriptableObject.CreateInstance<NpcVoiceProfile>();
            try
            {
                var serializedProfile = new SerializedObject(profile);
                serializedProfile.FindProperty("voicePresetId").stringValue =
                    "warm-friendly";
                serializedProfile.ApplyModifiedPropertiesWithoutUndo();

                Assert.That(profile.TryValidate(out var validError), Is.True, validError);

                serializedProfile.FindProperty("voicePresetId").stringValue =
                    "OpenAI-Marin";
                serializedProfile.ApplyModifiedPropertiesWithoutUndo();
                Assert.That(profile.TryValidate(out _), Is.False);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(profile);
            }
        }
    }
}
