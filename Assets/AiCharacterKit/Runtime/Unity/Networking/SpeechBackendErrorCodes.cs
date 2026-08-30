namespace AiCharacterKit.Unity.Networking
{
    /// <summary>
    /// Defines safe Unity-side failures for the optional speech backend adapter.
    /// </summary>
    public static class SpeechBackendErrorCodes
    {
        public const string BackendUnreachable = "speech_backend_unreachable";

        public const string BackendTimeout = "speech_backend_timeout";

        public const string BackendProtocolError = "speech_backend_protocol_error";
    }
}
