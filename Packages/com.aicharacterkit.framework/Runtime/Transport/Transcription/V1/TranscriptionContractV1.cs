namespace AiCharacterKit.Transport.Transcription.V1
{
    /// <summary>
    /// Defines stable Transcription V1 wire tokens, headers, and size limits.
    /// </summary>
    public static class TranscriptionContractV1
    {
        public const int SchemaVersion = 1;

        public const string SuccessStatus = "success";

        public const string ErrorStatus = "error";

        public const string ContentType = "audio/wav";

        public const string VersionHeader =
            "X-Ai-Character-Kit-Transcription-Version";

        public const string RequestIdHeader =
            "X-Ai-Character-Kit-Request-Id";

        public const int MaximumRequestIdLength = 128;
    }
}
