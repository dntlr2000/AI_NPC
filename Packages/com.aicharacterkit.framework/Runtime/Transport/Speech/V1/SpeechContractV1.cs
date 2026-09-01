namespace AiCharacterKit.Transport.Speech.V1
{
    /// <summary>
    /// Defines stable Speech V1 tokens, limits, and success response headers.
    /// </summary>
    public static class SpeechContractV1
    {
        public const int SchemaVersion = 1;

        public const string ErrorStatus = "error";

        public const int MaximumRequestIdLength = 128;

        public const int MaximumVoicePresetIdLength = 64;

        public const int MaximumTextLength = 4096;

        public const int MaximumTextUtf8Bytes = 8 * 1024;

        public const string ContentType = "application/octet-stream";

        public const string VersionHeader =
            "X-Ai-Character-Kit-Speech-Version";

        public const string RequestIdHeader =
            "X-Ai-Character-Kit-Request-Id";

        public const string AudioFormatHeader =
            "X-Ai-Character-Kit-Audio-Format";

        public const string SampleRateHeader =
            "X-Ai-Character-Kit-Sample-Rate";

        public const string ChannelsHeader =
            "X-Ai-Character-Kit-Channels";

        public const string AudioFormat = "pcm_s16le";

        public const string SampleRate = "24000";

        public const string Channels = "1";

        public const string InvalidRequestErrorCode = "invalid_request";

        public const string UnsupportedSchemaVersionErrorCode =
            "unsupported_schema_version";

        public const string VoicePresetNotFoundErrorCode =
            "voice_preset_not_found";

        public const string RateLimitedErrorCode = "rate_limited";

        public const string UpstreamTimeoutErrorCode = "upstream_timeout";

        public const string UpstreamUnavailableErrorCode = "upstream_unavailable";

        public const string UpstreamInvalidResponseErrorCode =
            "upstream_invalid_response";

        public const string InternalErrorCode = "internal_error";
    }
}
