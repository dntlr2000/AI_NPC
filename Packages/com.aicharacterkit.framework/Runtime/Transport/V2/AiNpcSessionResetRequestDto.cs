using System;

namespace AiCharacterKit.Transport.V2
{
    /// <summary>
    /// Defines one correlated request to clear a character-bound session.
    /// </summary>
    [Serializable]
    public sealed class AiNpcSessionResetRequestDto
    {
        public int schemaVersion;

        public string requestId = string.Empty;

        public string sessionId = string.Empty;

        public string characterId = string.Empty;
    }
}
