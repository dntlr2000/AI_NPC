using System;

namespace AiCharacterKit.Core
{
    /// <summary>
    /// Carries one safe conversation failure and its stable handling metadata.
    /// </summary>
    public sealed class AiConversationException : Exception
    {
        public string Code { get; }

        public bool Retryable { get; }

        /// <summary>
        /// Creates a public-safe failure without coupling Core to a transport mechanism.
        /// </summary>
        public AiConversationException(
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
