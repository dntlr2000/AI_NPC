namespace AiCharacterKit.Transport.V4
{
    /// <summary>
    /// Defines stable bounds and tokens for context-grounded contract V4.
    /// </summary>
    public static class AiNpcContractV4
    {
        public const int SchemaVersion = 4;
        public const int MaxSessionIdLength = 128;
        public const int MaxUserTextUtf8Bytes = 8 * 1024;
        public const int MaxTriggerCount = 16;
        public const int MaxTriggerIdLength = 64;
        public const int MaxConditionUtf8Bytes = 512;
        public const int MaxRevisionLength = 128;
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
        public const string LoreFactKind = "lore";
        public const string BeliefFactKind = "belief";
        public const string ObservationFactKind = "observation";
        public const string InvalidRequestErrorCode = "invalid_request";
        public const string UnsupportedSchemaVersionErrorCode =
            "unsupported_schema_version";
        public const string InvalidModelResponseErrorCode =
            "invalid_model_response";
    }
}
