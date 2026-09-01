namespace AiCharacterKit.Unity.Networking
{
    /// <summary>
    /// Defines stable local failures produced before a valid transcription response exists.
    /// </summary>
    public static class TranscriptionBackendErrorCodes
    {
        public const string BackendUnreachable = "backend_unreachable";

        public const string BackendTimeout = "backend_timeout";

        public const string BackendProtocolError = "backend_protocol_error";
    }
}
