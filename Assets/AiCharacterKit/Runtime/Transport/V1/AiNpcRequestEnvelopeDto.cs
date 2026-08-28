using System;

namespace AiCharacterKit.Transport.V1
{
    /// <summary>
    /// Defines the versioned wire request sent to a future backend.
    /// </summary>
    [Serializable]
    public sealed class AiNpcRequestEnvelopeDto
    {
        public int schemaVersion;

        public string requestId = string.Empty;

        public CharacterSnapshotDto character;

        public string userText = string.Empty;
    }
}
