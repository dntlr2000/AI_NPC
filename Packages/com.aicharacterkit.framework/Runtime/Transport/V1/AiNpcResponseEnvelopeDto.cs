using System;

namespace AiCharacterKit.Transport.V1
{
    /// <summary>
    /// Defines one correlated success or error response from a future backend.
    /// </summary>
    [Serializable]
    public sealed class AiNpcResponseEnvelopeDto
    {
        public int schemaVersion;

        public string requestId = string.Empty;

        public string status = string.Empty;

        public AiNpcResponsePayloadDto result;

        public AiNpcErrorDto error;
    }
}
