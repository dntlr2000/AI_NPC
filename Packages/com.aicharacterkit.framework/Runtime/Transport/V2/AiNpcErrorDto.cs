using System;

namespace AiCharacterKit.Transport.V2
{
    /// <summary>
    /// Carries a stable machine code and safe message for a V2 failure.
    /// </summary>
    [Serializable]
    public sealed class AiNpcErrorDto
    {
        public string code = string.Empty;

        public string message = string.Empty;

        public bool retryable;
    }
}
