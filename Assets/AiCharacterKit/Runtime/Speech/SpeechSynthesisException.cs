using System;

namespace AiCharacterKit.Speech
{
    /// <summary>
    /// Represents one safe speech failure without exposing transport or provider details.
    /// </summary>
    public sealed class SpeechSynthesisException : Exception
    {
        public string Code { get; }

        public bool Retryable { get; }

        /// <summary>
        /// Creates one contract-ready failure with an optional private cause.
        /// </summary>
        public SpeechSynthesisException(
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
