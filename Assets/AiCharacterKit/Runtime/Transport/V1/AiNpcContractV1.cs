namespace AiCharacterKit.Transport.V1
{
    /// <summary>
    /// Defines the stable version, status, command, and baseline error tokens for contract V1.
    /// </summary>
    public static class AiNpcContractV1
    {
        public const int SchemaVersion = 1;

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

        public const string InternalErrorCode = "internal_error";
    }
}
