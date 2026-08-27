namespace AiCharacterKit.Core
{
    /// <summary>
    /// Carries a character profile snapshot and one user message into the conversation layer.
    /// </summary>
    public sealed class AiNpcRequest
    {
        public string CharacterId { get; }

        public string DisplayName { get; }

        public string Personality { get; }

        public string SpeechStyle { get; }

        public string ExampleDialogue { get; }

        public NpcEmotion DefaultEmotion { get; }

        public string UserText { get; }

        /// <summary>
        /// Creates an immutable request without retaining a Unity object reference.
        /// </summary>
        public AiNpcRequest(
            string characterId,
            string displayName,
            string personality,
            string speechStyle,
            string exampleDialogue,
            NpcEmotion defaultEmotion,
            string userText)
        {
            CharacterId = characterId ?? string.Empty;
            DisplayName = displayName ?? string.Empty;
            Personality = personality ?? string.Empty;
            SpeechStyle = speechStyle ?? string.Empty;
            ExampleDialogue = exampleDialogue ?? string.Empty;
            DefaultEmotion = defaultEmotion;
            UserText = userText ?? string.Empty;
        }
    }
}
