using AiCharacterKit.Core;

namespace AiCharacterKit.Transport.V1
{
    /// <summary>
    /// Converts domain commands to and from the exact lowercase V1 wire tokens.
    /// </summary>
    internal static class NpcCommandTokenConverter
    {
        /// <summary>
        /// Converts one supported domain emotion into its stable V1 token.
        /// </summary>
        public static bool TryToToken(NpcEmotion emotion, out string token)
        {
            switch (emotion)
            {
                case NpcEmotion.Neutral:
                    token = AiNpcContractV1.NeutralEmotion;
                    return true;
                case NpcEmotion.Happy:
                    token = AiNpcContractV1.HappyEmotion;
                    return true;
                case NpcEmotion.Sad:
                    token = AiNpcContractV1.SadEmotion;
                    return true;
                case NpcEmotion.Angry:
                    token = AiNpcContractV1.AngryEmotion;
                    return true;
                case NpcEmotion.Concerned:
                    token = AiNpcContractV1.ConcernedEmotion;
                    return true;
                default:
                    token = string.Empty;
                    return false;
            }
        }

        /// <summary>
        /// Converts one exact V1 emotion token into its domain command.
        /// </summary>
        public static bool TryParseEmotion(string token, out NpcEmotion emotion)
        {
            switch (token)
            {
                case AiNpcContractV1.NeutralEmotion:
                    emotion = NpcEmotion.Neutral;
                    return true;
                case AiNpcContractV1.HappyEmotion:
                    emotion = NpcEmotion.Happy;
                    return true;
                case AiNpcContractV1.SadEmotion:
                    emotion = NpcEmotion.Sad;
                    return true;
                case AiNpcContractV1.AngryEmotion:
                    emotion = NpcEmotion.Angry;
                    return true;
                case AiNpcContractV1.ConcernedEmotion:
                    emotion = NpcEmotion.Concerned;
                    return true;
                default:
                    emotion = NpcEmotion.Neutral;
                    return false;
            }
        }

        /// <summary>
        /// Converts one supported domain gesture into its stable V1 token.
        /// </summary>
        public static bool TryToToken(NpcGesture gesture, out string token)
        {
            switch (gesture)
            {
                case NpcGesture.None:
                    token = AiNpcContractV1.NoneGesture;
                    return true;
                case NpcGesture.Nod:
                    token = AiNpcContractV1.NodGesture;
                    return true;
                case NpcGesture.Wave:
                    token = AiNpcContractV1.WaveGesture;
                    return true;
                default:
                    token = string.Empty;
                    return false;
            }
        }

        /// <summary>
        /// Converts one exact V1 gesture token into its domain command.
        /// </summary>
        public static bool TryParseGesture(string token, out NpcGesture gesture)
        {
            switch (token)
            {
                case AiNpcContractV1.NoneGesture:
                    gesture = NpcGesture.None;
                    return true;
                case AiNpcContractV1.NodGesture:
                    gesture = NpcGesture.Nod;
                    return true;
                case AiNpcContractV1.WaveGesture:
                    gesture = NpcGesture.Wave;
                    return true;
                default:
                    gesture = NpcGesture.None;
                    return false;
            }
        }
    }
}
