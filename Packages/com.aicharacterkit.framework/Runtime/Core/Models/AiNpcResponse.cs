namespace AiCharacterKit.Core
{
    /// <summary>
    /// Represents a structured NPC reply for dialogue, emotion, and gesture presentation.
    /// </summary>
    public sealed class AiNpcResponse
    {
        public string Dialogue { get; }

        public NpcEmotion Emotion { get; }

        public NpcGesture Gesture { get; }

        /// <summary>
        /// Creates a complete structured response.
        /// </summary>
        public AiNpcResponse(string dialogue, NpcEmotion emotion, NpcGesture gesture)
        {
            Dialogue = dialogue ?? string.Empty;
            Emotion = emotion;
            Gesture = gesture;
        }
    }
}
