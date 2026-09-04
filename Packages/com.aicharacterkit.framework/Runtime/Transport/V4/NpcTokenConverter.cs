using AiCharacterKit.Core;

namespace AiCharacterKit.Transport.V4
{
    /// <summary>
    /// Converts domain commands and fact kinds to exact lowercase V4 tokens.
    /// </summary>
    internal static class NpcTokenConverter
    {
        /// <summary>
        /// Converts one supported domain emotion into its V4 token.
        /// </summary>
        public static bool TryToToken(NpcEmotion emotion, out string token)
        {
            switch (emotion)
            {
                case NpcEmotion.Neutral: token = AiNpcContractV4.NeutralEmotion; return true;
                case NpcEmotion.Happy: token = AiNpcContractV4.HappyEmotion; return true;
                case NpcEmotion.Sad: token = AiNpcContractV4.SadEmotion; return true;
                case NpcEmotion.Angry: token = AiNpcContractV4.AngryEmotion; return true;
                case NpcEmotion.Concerned: token = AiNpcContractV4.ConcernedEmotion; return true;
                default: token = string.Empty; return false;
            }
        }

        /// <summary>
        /// Converts one exact V4 emotion token into its domain command.
        /// </summary>
        public static bool TryParseEmotion(string token, out NpcEmotion emotion)
        {
            switch (token)
            {
                case AiNpcContractV4.NeutralEmotion: emotion = NpcEmotion.Neutral; return true;
                case AiNpcContractV4.HappyEmotion: emotion = NpcEmotion.Happy; return true;
                case AiNpcContractV4.SadEmotion: emotion = NpcEmotion.Sad; return true;
                case AiNpcContractV4.AngryEmotion: emotion = NpcEmotion.Angry; return true;
                case AiNpcContractV4.ConcernedEmotion: emotion = NpcEmotion.Concerned; return true;
                default: emotion = NpcEmotion.Neutral; return false;
            }
        }

        /// <summary>
        /// Converts one supported domain gesture into its V4 token.
        /// </summary>
        public static bool TryToToken(NpcGesture gesture, out string token)
        {
            switch (gesture)
            {
                case NpcGesture.None: token = AiNpcContractV4.NoneGesture; return true;
                case NpcGesture.Nod: token = AiNpcContractV4.NodGesture; return true;
                case NpcGesture.Wave: token = AiNpcContractV4.WaveGesture; return true;
                default: token = string.Empty; return false;
            }
        }

        /// <summary>
        /// Converts one exact V4 gesture token into its domain command.
        /// </summary>
        public static bool TryParseGesture(string token, out NpcGesture gesture)
        {
            switch (token)
            {
                case AiNpcContractV4.NoneGesture: gesture = NpcGesture.None; return true;
                case AiNpcContractV4.NodGesture: gesture = NpcGesture.Nod; return true;
                case AiNpcContractV4.WaveGesture: gesture = NpcGesture.Wave; return true;
                default: gesture = NpcGesture.None; return false;
            }
        }

        /// <summary>
        /// Converts one supported fact kind into its V4 token.
        /// </summary>
        public static bool TryToToken(NpcContextFactKind kind, out string token)
        {
            switch (kind)
            {
                case NpcContextFactKind.Lore: token = AiNpcContractV4.LoreFactKind; return true;
                case NpcContextFactKind.Belief: token = AiNpcContractV4.BeliefFactKind; return true;
                case NpcContextFactKind.Observation: token = AiNpcContractV4.ObservationFactKind; return true;
                default: token = string.Empty; return false;
            }
        }

        /// <summary>
        /// Converts one exact V4 fact-kind token into its domain value.
        /// </summary>
        public static bool TryParseFactKind(string token, out NpcContextFactKind kind)
        {
            switch (token)
            {
                case AiNpcContractV4.LoreFactKind: kind = NpcContextFactKind.Lore; return true;
                case AiNpcContractV4.BeliefFactKind: kind = NpcContextFactKind.Belief; return true;
                case AiNpcContractV4.ObservationFactKind: kind = NpcContextFactKind.Observation; return true;
                default: kind = NpcContextFactKind.Lore; return false;
            }
        }
    }
}
