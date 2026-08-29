using System;

namespace AiCharacterKit.Transport.V2
{
    /// <summary>
    /// Defines one correlated session-aware conversation response.
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
