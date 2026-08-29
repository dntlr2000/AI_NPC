using System;

namespace AiCharacterKit.Transport.V2
{
    /// <summary>
    /// Defines one correlated session-aware conversation request.
    /// </summary>
    [Serializable]
    public sealed class AiNpcRequestEnvelopeDto
    {
        public int schemaVersion;

        public string requestId = string.Empty;

        public string sessionId = string.Empty;

        public CharacterSnapshotDto character;

        public string userText = string.Empty;
    }
}
