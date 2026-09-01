using AiCharacterKit.Core;

namespace AiCharacterKit.Transport.V2
{
    /// <summary>
    /// Converts domain commands to and from the exact lowercase V2 wire tokens.
    /// </summary>
    internal static class NpcCommandTokenConverter
    {
        /// <summary>
        /// Converts one supported domain emotion into its stable V2 token.
        /// </summary>
        public static bool TryToToken(NpcEmotion emotion, out string token)
        {
            switch (emotion)
            {
                case NpcEmotion.Neutral:
                    token = AiNpcContractV2.NeutralEmotion;
                    return true;
                case NpcEmotion.Happy:
                    token = AiNpcContractV2.HappyEmotion;
                    return true;
                case NpcEmotion.Sad:
                    token = AiNpcContractV2.SadEmotion;
                    return true;
                case NpcEmotion.Angry:
                    token = AiNpcContractV2.AngryEmotion;
                    return true;
                case NpcEmotion.Concerned:
                    token = AiNpcContractV2.ConcernedEmotion;
                    return true;
                default:
                    token = string.Empty;
                    return false;
            }
        }

        /// <summary>
        /// Converts one exact V2 emotion token into its domain command.
        /// </summary>
        public static bool TryParseEmotion(string token, out NpcEmotion emotion)
        {
            switch (token)
            {
                case AiNpcContractV2.NeutralEmotion:
                    emotion = NpcEmotion.Neutral;
                    return true;
                case AiNpcContractV2.HappyEmotion:
                    emotion = NpcEmotion.Happy;
                    return true;
                case AiNpcContractV2.SadEmotion:
                    emotion = NpcEmotion.Sad;
                    return true;
                case AiNpcContractV2.AngryEmotion:
                    emotion = NpcEmotion.Angry;
                    return true;
                case AiNpcContractV2.ConcernedEmotion:
                    emotion = NpcEmotion.Concerned;
                    return true;
                default:
                    emotion = NpcEmotion.Neutral;
                    return false;
            }
        }

        /// <summary>
        /// Converts one supported domain gesture into its stable V2 token.
        /// </summary>
        public static bool TryToToken(NpcGesture gesture, out string token)
        {
            switch (gesture)
            {
                case NpcGesture.None:
                    token = AiNpcContractV2.NoneGesture;
                    return true;
                case NpcGesture.Nod:
                    token = AiNpcContractV2.NodGesture;
                    return true;
                case NpcGesture.Wave:
                    token = AiNpcContractV2.WaveGesture;
                    return true;
                default:
                    token = string.Empty;
                    return false;
            }
        }

        /// <summary>
        /// Converts one exact V2 gesture token into its domain command.
        /// </summary>
        public static bool TryParseGesture(string token, out NpcGesture gesture)
        {
            switch (token)
            {
                case AiNpcContractV2.NoneGesture:
                    gesture = NpcGesture.None;
                    return true;
                case AiNpcContractV2.NodGesture:
                    gesture = NpcGesture.Nod;
                    return true;
                case AiNpcContractV2.WaveGesture:
                    gesture = NpcGesture.Wave;
                    return true;
                default:
                    gesture = NpcGesture.None;
                    return false;
            }
        }
    }
}
