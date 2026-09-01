using System;
using System.Text;

namespace AiCharacterKit.Transcription
{
    /// <summary>
    /// Stores one bounded provider-neutral transcription result.
    /// </summary>
    public sealed class TranscriptionResult
    {
        public const int MaximumTextLength = 4096;

        public const int MaximumTextUtf8Bytes = 8 * 1024;

        public string Text { get; }

        /// <summary>
        /// Creates one non-empty bounded transcript while preserving provider text.
        /// </summary>
        public TranscriptionResult(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                throw new ArgumentException(
                    "Transcription text must not be empty.",
                    nameof(text));
            }

            if (text.Length > MaximumTextLength
                || Encoding.UTF8.GetByteCount(text) > MaximumTextUtf8Bytes)
            {
                throw new ArgumentException(
                    "Transcription text exceeds the supported size.",
                    nameof(text));
            }

            Text = text;
        }
    }
}
