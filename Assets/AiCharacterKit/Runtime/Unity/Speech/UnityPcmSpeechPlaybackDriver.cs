using System;
using AiCharacterKit.Speech;
using UnityEngine;

namespace AiCharacterKit.Unity.Speech
{
    /// <summary>
    /// Converts fixed PCM16LE speech into a transient Unity AudioClip.
    /// </summary>
    [RequireComponent(typeof(AudioSource))]
    public sealed class UnityPcmSpeechPlaybackDriver
        : MonoBehaviour, ISpeechPlaybackDriver
    {
        [SerializeField]
        private AudioSource audioSource;

        private AudioClip currentClip;

        public bool IsPlaying => audioSource != null && audioSource.isPlaying;

        /// <summary>
        /// Resolves the local AudioSource when one was not explicitly assigned.
        /// </summary>
        private void Awake()
        {
            ResolveAudioSource();
        }

        /// <summary>
        /// Replaces current output with one complete normalized PCM speech clip.
        /// </summary>
        public void Play(SpeechAudioData audioData)
        {
            if (audioData == null)
            {
                throw new ArgumentNullException(nameof(audioData));
            }

            ResolveAudioSource();
            if (audioSource == null)
            {
                throw new InvalidOperationException(
                    "Unity PCM playback requires an AudioSource.");
            }

            Stop();
            var samples = ConvertPcm16LittleEndian(audioData.PcmBytes);
            var clip = AudioClip.Create(
                "AI NPC Speech",
                samples.Length,
                SpeechAudioData.Channels,
                SpeechAudioData.SampleRate,
                false);
            if (clip == null || !clip.SetData(samples, 0))
            {
                DestroyClip(clip);
                throw new InvalidOperationException(
                    "Unity could not create the speech AudioClip.");
            }

            currentClip = clip;
            audioSource.clip = currentClip;
            audioSource.Play();
        }

        /// <summary>
        /// Stops playback and releases the transient clip owned by this driver.
        /// </summary>
        public void Stop()
        {
            if (audioSource != null)
            {
                audioSource.Stop();
                if (audioSource.clip == currentClip)
                {
                    audioSource.clip = null;
                }
            }

            DestroyClip(currentClip);
            currentClip = null;
        }

        /// <summary>
        /// Releases transient audio when the component leaves the scene permanently.
        /// </summary>
        private void OnDestroy()
        {
            Stop();
        }

        /// <summary>
        /// Converts signed 16-bit little-endian samples into Unity's normalized float range.
        /// </summary>
        private static float[] ConvertPcm16LittleEndian(byte[] bytes)
        {
            var samples = new float[bytes.Length / 2];
            for (var index = 0; index < samples.Length; index++)
            {
                var byteIndex = index * 2;
                var sample = (short)(bytes[byteIndex]
                    | bytes[byteIndex + 1] << 8);
                samples[index] = sample / 32768f;
            }

            return samples;
        }

        /// <summary>
        /// Resolves the required local source for both runtime and EditMode callers.
        /// </summary>
        private void ResolveAudioSource()
        {
            if (audioSource == null)
            {
                audioSource = GetComponent<AudioSource>();
            }
        }

        /// <summary>
        /// Destroys one transient clip using the correct runtime or edit-time API.
        /// </summary>
        private static void DestroyClip(AudioClip clip)
        {
            if (clip == null)
            {
                return;
            }

            if (Application.isPlaying)
            {
                Destroy(clip);
            }
            else
            {
                DestroyImmediate(clip);
            }
        }
    }
}
