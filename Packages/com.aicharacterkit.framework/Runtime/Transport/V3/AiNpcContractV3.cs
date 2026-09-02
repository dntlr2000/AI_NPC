namespace AiCharacterKit.Transport.V3
{
    /// <summary>
    /// Defines the stable version, bounds, and tokens for action-aware contract V3.
    /// </summary>
    public static class AiNpcContractV3
    {
        public const int SchemaVersion = 3;
        public const int MaxSessionIdLength = 128;
        public const int MaxUserTextUtf8Bytes = 8 * 1024;
        public const int MaxTriggerCount = 16;
        public const int MaxTriggerIdLength = 64;
        public const int MaxConditionUtf8Bytes = 512;
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
        public const string InvalidModelResponseErrorCode =
            "invalid_model_response";
    }
}
