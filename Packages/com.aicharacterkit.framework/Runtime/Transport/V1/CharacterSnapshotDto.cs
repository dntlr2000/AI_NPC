using System;

namespace AiCharacterKit.Transport.V1
{
    /// <summary>
    /// Carries the complete character data required to process one independent request.
    /// </summary>
    [Serializable]
    public sealed class CharacterSnapshotDto
    {
        public string characterId = string.Empty;

        public string displayName = string.Empty;

        public string personality = string.Empty;

        public string speechStyle = string.Empty;

        public string exampleDialogue = string.Empty;

        public string defaultEmotion = string.Empty;
    }
}
