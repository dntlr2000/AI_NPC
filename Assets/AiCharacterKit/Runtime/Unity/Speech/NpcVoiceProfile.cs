using AiCharacterKit.Transport.Speech.V1;
using UnityEngine;

namespace AiCharacterKit.Unity.Speech
{
    /// <summary>
    /// Stores one reusable backend-owned voice preset selection as Unity data.
    /// </summary>
    [CreateAssetMenu(
        fileName = "NpcVoiceProfile",
        menuName = "AI Character Kit/NPC Voice Profile")]
    public sealed class NpcVoiceProfile : ScriptableObject
    {
        [SerializeField]
        private string voicePresetId = string.Empty;

        public string VoicePresetId => voicePresetId;

        /// <summary>
        /// Verifies that the opaque preset identifier matches the Speech V1 token grammar.
        /// </summary>
        public bool TryValidate(out string error)
        {
            return SpeechContractValidator.TryValidateVoicePresetId(
                voicePresetId,
                out error);
        }
    }
}
