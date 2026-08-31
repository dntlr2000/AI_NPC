using System;

namespace AiCharacterKit.Transcription
{
    /// <summary>
    /// Represents one safe capture or transcription failure without provider details.
    /// </summary>
    public sealed class TranscriptionException : Exception
    {
        public string Code { get; }

        public bool Retryable { get; }

        /// <summary>
        /// Creates one contract-ready failure with an optional private cause.
        /// </summary>
        public TranscriptionException(
            string code,
            string message,
            bool retryable,
            Exception innerException = null)
            : base(message, innerException)
        {
            Code = code ?? string.Empty;
            Retryable = retryable;
        }
    }
}
