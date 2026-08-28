using System;

namespace AiCharacterKit.Transport.V1
{
    /// <summary>
    /// Carries a stable machine code and safe message for an unsuccessful response.
    /// </summary>
    [Serializable]
    public sealed class AiNpcErrorDto
    {
        public string code = string.Empty;

        public string message = string.Empty;

        public bool retryable;
    }
}
