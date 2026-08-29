namespace AiCharacterKit.Transport.V2
{
    /// <summary>
    /// Defines the stable version, limits, status, command, and error tokens for contract V2.
    /// </summary>
    public static class AiNpcContractV2
    {
        public const int SchemaVersion = 2;

        public const int MaxSessionIdLength = 128;

        public const int MaxUserTextUtf8Bytes = 8 * 1024;

        public const string SuccessStatus = "success";

        public const string ErrorStatus = "error";

        public const string NeutralEmotion = "neutral";

        public const string HappyEmotion = "happy";

        public const string SadEmotion = "sad";

        public const string AngryEmotion = "angry";

        public const string ConcernedEmotion = "concerned";

        public const string NoneGesture = "none";

        public const string NodGesture = "nod";

        public const string WaveGesture = "wave";

        public const string InvalidRequestErrorCode = "invalid_request";

        public const string UnsupportedSchemaVersionErrorCode =
            "unsupported_schema_version";

        public const string SessionBusyErrorCode = "session_busy";

        public const string SessionCharacterMismatchErrorCode =
            "session_character_mismatch";

        public const string SessionCapacityReachedErrorCode =
            "session_capacity_reached";
    }
}
