using System;
using AiCharacterKit.Transcription;
using UnityEngine;

namespace AiCharacterKit.Unity.Transcription
{
    /// <summary>
    /// Captures one bounded Unity microphone clip and encodes its actual samples as mono WAV.
    /// </summary>
    public sealed class UnityMicrophoneCaptureDriver
        : MonoBehaviour, IAudioCaptureDriver
    {
        [SerializeField]
        [Range(CapturedAudioData.MinimumSampleRate, CapturedAudioData.MaximumSampleRate)]
        private int sampleRate = 16000;

        [SerializeField]
        [Range(1, CapturedAudioData.MaximumDurationSeconds)]
        private int maximumDurationSeconds = CapturedAudioData.MaximumDurationSeconds;

        private AudioClip activeClip;
        private string activeDeviceName;
        private float captureStartedAt;

        public bool IsCapturing => activeClip != null;

        /// <summary>
        /// Starts one non-looping capture on the system default microphone.
        /// </summary>
        public void StartCapture()
        {
            if (activeClip != null)
            {
                throw new TranscriptionException(
                    "microphone_busy",
                    "마이크가 이미 녹음 중입니다.",
                    false);
            }

            if (!Application.HasUserAuthorization(UserAuthorization.Microphone))
            {
                throw new TranscriptionException(
                    "microphone_permission_denied",
                    "마이크 권한이 필요합니다.",
                    false);
            }

            if (Microphone.devices == null || Microphone.devices.Length == 0)
            {
                throw new TranscriptionException(
                    "microphone_unavailable",
                    "사용 가능한 마이크를 찾을 수 없습니다.",
                    false);
            }

            if (sampleRate < CapturedAudioData.MinimumSampleRate
                || sampleRate > CapturedAudioData.MaximumSampleRate
                || maximumDurationSeconds <= 0
                || maximumDurationSeconds > CapturedAudioData.MaximumDurationSeconds)
            {
                throw new TranscriptionException(
                    "microphone_configuration_invalid",
                    "마이크 캡처 설정이 올바르지 않습니다.",
                    false);
            }

            activeDeviceName = Microphone.devices[0];
            activeClip = Microphone.Start(
                activeDeviceName,
                false,
                maximumDurationSeconds,
                sampleRate);
            if (activeClip == null)
            {
                activeDeviceName = null;
                throw new TranscriptionException(
                    "microphone_start_failed",
                    "마이크 녹음을 시작하지 못했습니다.",
                    true);
            }

            captureStartedAt = Time.realtimeSinceStartup;
        }

        /// <summary>
        /// Stops capture, reads only recorded frames, and returns canonical PCM16 mono WAV.
        /// </summary>
        public CapturedAudioData StopCapture()
        {
            if (activeClip == null)
            {
                throw new TranscriptionException(
                    "microphone_not_recording",
                    "진행 중인 마이크 녹음이 없습니다.",
                    false);
            }

            var clip = activeClip;
            var deviceName = activeDeviceName;
            var sampleFrames = Microphone.GetPosition(deviceName);
            var elapsedSeconds = Time.realtimeSinceStartup - captureStartedAt;
            if (sampleFrames <= 0
                && elapsedSeconds >= maximumDurationSeconds - 0.1f)
            {
                sampleFrames = clip.samples;
            }

            Microphone.End(deviceName);
            activeClip = null;
            activeDeviceName = null;

            try
            {
                sampleFrames = Math.Min(sampleFrames, clip.samples);
                if (sampleFrames <= 0)
                {
                    throw new TranscriptionException(
                        "empty_recording",
                        "녹음된 음성이 없습니다.",
                        false);
                }

                var samples = new float[sampleFrames * clip.channels];
                if (!clip.GetData(samples, 0))
                {
                    throw new TranscriptionException(
                        "microphone_read_failed",
                        "녹음된 음성을 읽지 못했습니다.",
                        true);
                }

                return Pcm16WaveEncoder.Encode(
                    samples,
                    clip.channels,
                    clip.frequency,
                    sampleFrames);
            }
            finally
            {
                Destroy(clip);
            }
        }

        /// <summary>
        /// Ends and releases active Unity microphone resources without returning data.
        /// </summary>
        public void CancelCapture()
        {
            if (activeClip == null)
            {
                return;
            }

            var clip = activeClip;
            var deviceName = activeDeviceName;
            activeClip = null;
            activeDeviceName = null;
            Microphone.End(deviceName);
            Destroy(clip);
        }

        /// <summary>
        /// Releases microphone resources if the component leaves its active lifetime.
        /// </summary>
        private void OnDisable()
        {
            CancelCapture();
        }
    }
}
