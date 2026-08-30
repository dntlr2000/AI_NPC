using AiCharacterKit.Speech;
using AiCharacterKit.Unity.Speech;
using NUnit.Framework;
using UnityEngine;

namespace AiCharacterKit.Core.Tests
{
    /// <summary>
    /// Verifies normalized PCM conversion and transient Unity clip ownership.
    /// </summary>
    public sealed class UnityPcmSpeechPlaybackDriverTests
    {
        /// <summary>
        /// Creates a 24 kHz mono clip with expected normalized signed sample values.
        /// </summary>
        [Test]
        public void Play_ValidPcm_CreatesExpectedAudioClip()
        {
            var gameObject = new GameObject("PCM Playback Test");
            try
            {
                var source = gameObject.AddComponent<AudioSource>();
                var driver = gameObject.AddComponent<UnityPcmSpeechPlaybackDriver>();
                driver.Play(
                    new SpeechAudioData(
                        new byte[] { 0, 128, 0, 0, 255, 127 }));

                Assert.That(source.clip, Is.Not.Null);
                Assert.That(source.clip.frequency, Is.EqualTo(24000));
                Assert.That(source.clip.channels, Is.EqualTo(1));
                Assert.That(source.clip.samples, Is.EqualTo(3));
                var samples = new float[3];
                Assert.That(source.clip.GetData(samples, 0), Is.True);
                Assert.That(samples[0], Is.EqualTo(-1f).Within(0.0001f));
                Assert.That(samples[1], Is.EqualTo(0f).Within(0.0001f));
                Assert.That(samples[2], Is.EqualTo(32767f / 32768f).Within(0.0001f));

                driver.Stop();
                Assert.That(source.clip, Is.Null);
            }
            finally
            {
                Object.DestroyImmediate(gameObject);
            }
        }
    }
}
