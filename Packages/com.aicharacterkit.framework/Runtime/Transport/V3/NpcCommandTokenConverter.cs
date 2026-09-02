using AiCharacterKit.Core;

namespace AiCharacterKit.Transport.V3
{
    /// <summary>
    /// Converts domain presentation commands to and from exact lowercase V3 tokens.
    /// </summary>
    internal static class NpcCommandTokenConverter
    {
        /// <summary>
        /// Converts one supported domain emotion into its V3 token.
        /// </summary>
        public static bool TryToToken(NpcEmotion emotion, out string token)
        {
            switch (emotion)
            {
                case NpcEmotion.Neutral: token = AiNpcContractV3.NeutralEmotion; return true;
                case NpcEmotion.Happy: token = AiNpcContractV3.HappyEmotion; return true;
                case NpcEmotion.Sad: token = AiNpcContractV3.SadEmotion; return true;
                case NpcEmotion.Angry: token = AiNpcContractV3.AngryEmotion; return true;
                case NpcEmotion.Concerned: token = AiNpcContractV3.ConcernedEmotion; return true;
                default: token = string.Empty; return false;
            }
        }

        /// <summary>
        /// Converts one exact V3 emotion token into its domain command.
        /// </summary>
        public static bool TryParseEmotion(string token, out NpcEmotion emotion)
        {
            switch (token)
            {
                case AiNpcContractV3.NeutralEmotion: emotion = NpcEmotion.Neutral; return true;
                case AiNpcContractV3.HappyEmotion: emotion = NpcEmotion.Happy; return true;
                case AiNpcContractV3.SadEmotion: emotion = NpcEmotion.Sad; return true;
                case AiNpcContractV3.AngryEmotion: emotion = NpcEmotion.Angry; return true;
                case AiNpcContractV3.ConcernedEmotion: emotion = NpcEmotion.Concerned; return true;
                default: emotion = NpcEmotion.Neutral; return false;
            }
        }

        /// <summary>
        /// Converts one supported domain gesture into its V3 token.
        /// </summary>
        public static bool TryToToken(NpcGesture gesture, out string token)
        {
            switch (gesture)
            {
                case NpcGesture.None: token = AiNpcContractV3.NoneGesture; return true;
                case NpcGesture.Nod: token = AiNpcContractV3.NodGesture; return true;
                case NpcGesture.Wave: token = AiNpcContractV3.WaveGesture; return true;
                default: token = string.Empty; return false;
            }
        }

        /// <summary>
        /// Converts one exact V3 gesture token into its domain command.
        /// </summary>
        public static bool TryParseGesture(string token, out NpcGesture gesture)
        {
            switch (token)
            {
                case AiNpcContractV3.NoneGesture: gesture = NpcGesture.None; return true;
                case AiNpcContractV3.NodGesture: gesture = NpcGesture.Nod; return true;
                case AiNpcContractV3.WaveGesture: gesture = NpcGesture.Wave; return true;
                default: gesture = NpcGesture.None; return false;
            }
        }
    }
}
