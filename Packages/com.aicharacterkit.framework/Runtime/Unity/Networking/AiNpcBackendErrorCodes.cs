namespace AiCharacterKit.Unity.Networking
{
    /// <summary>
    /// Defines failures created locally by the Unity backend adapter.
    /// </summary>
    public static class AiNpcBackendErrorCodes
    {
        public const string BackendUnreachable = "backend_unreachable";

        public const string BackendTimeout = "backend_timeout";

        public const string BackendProtocolError = "backend_protocol_error";
    }
}
