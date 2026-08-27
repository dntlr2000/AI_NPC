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
    }
}
