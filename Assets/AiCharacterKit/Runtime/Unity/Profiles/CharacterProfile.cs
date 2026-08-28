using System;
using AiCharacterKit.Core;
using UnityEngine;

namespace AiCharacterKit.Unity
{
    /// <summary>
    /// Stores reusable character identity and speaking data outside MonoBehaviour code.
    /// </summary>
    [CreateAssetMenu(
        fileName = "CharacterProfile",
        menuName = "AI Character Kit/Character Profile")]
    public sealed class CharacterProfile : ScriptableObject
    {
        [SerializeField]
        private string characterId = "prototype-npc";

        [SerializeField]
        private string displayName = "Prototype NPC";

        [SerializeField]
        [TextArea(2, 5)]
        private string personality = "Friendly and curious.";

        [SerializeField]
        [TextArea(2, 4)]
        private string speechStyle = "Short and polite.";

        [SerializeField]
        [TextArea(2, 5)]
        private string exampleDialogue = "무엇을 도와드릴까요?";

        [SerializeField]
        private NpcEmotion defaultEmotion = NpcEmotion.Neutral;

        public string CharacterId => characterId;

        public string DisplayName => displayName;

        public string Personality => personality;

        public string SpeechStyle => speechStyle;

        public string ExampleDialogue => exampleDialogue;

        public NpcEmotion DefaultEmotion => defaultEmotion;

        /// <summary>
        /// Verifies that the profile contains every value required to create a conversation request.
        /// </summary>
        public bool TryValidate(out string error)
        {
            if (string.IsNullOrWhiteSpace(characterId))
            {
                error = "Character ID must not be empty.";
                return false;
            }

            if (string.IsNullOrWhiteSpace(displayName))
            {
                error = "Display name must not be empty.";
                return false;
            }

            if (string.IsNullOrWhiteSpace(personality))
            {
                error = "Personality must not be empty.";
                return false;
            }

            if (string.IsNullOrWhiteSpace(speechStyle))
            {
                error = "Speech style must not be empty.";
                return false;
            }

            if (string.IsNullOrWhiteSpace(exampleDialogue))
            {
                error = "Example dialogue must not be empty.";
                return false;
            }

            if (!Enum.IsDefined(typeof(NpcEmotion), defaultEmotion))
            {
                error = "Default emotion is not supported.";
                return false;
            }

            error = string.Empty;
            return true;
        }
    }
}
