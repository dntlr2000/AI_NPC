using System;
using System.Collections.Generic;

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

        public IReadOnlyList<string> MatchedTriggerIds { get; }

        /// <summary>
        /// Creates a complete structured response.
        /// </summary>
        public AiNpcResponse(string dialogue, NpcEmotion emotion, NpcGesture gesture)
            : this(dialogue, emotion, gesture, null)
        {
        }

        /// <summary>
        /// Creates a structured response with an immutable snapshot of matched trigger IDs.
        /// </summary>
        public AiNpcResponse(
            string dialogue,
            NpcEmotion emotion,
            NpcGesture gesture,
            IEnumerable<string> matchedTriggerIds)
        {
            Dialogue = dialogue ?? string.Empty;
            Emotion = emotion;
            Gesture = gesture;
            MatchedTriggerIds = CopyTriggerIds(matchedTriggerIds);
        }

        /// <summary>
        /// Copies optional trigger IDs so callers cannot mutate a completed response.
        /// </summary>
        private static IReadOnlyList<string> CopyTriggerIds(
            IEnumerable<string> matchedTriggerIds)
        {
            if (matchedTriggerIds == null)
            {
                return Array.Empty<string>();
            }

            var copiedIds = new List<string>();
            foreach (var triggerId in matchedTriggerIds)
            {
                copiedIds.Add(triggerId ?? string.Empty);
            }

            return copiedIds.AsReadOnly();
        }
    }
}
