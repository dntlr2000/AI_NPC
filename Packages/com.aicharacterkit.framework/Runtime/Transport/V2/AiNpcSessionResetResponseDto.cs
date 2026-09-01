using System;

namespace AiCharacterKit.Transport.V2
{
    /// <summary>
    /// Defines one correlated success or error response for a session reset.
    /// </summary>
    [Serializable]
    public sealed class AiNpcSessionResetResponseDto
    {
        public int schemaVersion;

        public string requestId = string.Empty;

        public string status = string.Empty;

        public AiNpcSessionResetResultDto result;

        public AiNpcErrorDto error;
    }
}
