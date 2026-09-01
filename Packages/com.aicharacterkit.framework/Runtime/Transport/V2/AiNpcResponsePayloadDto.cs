using System;

namespace AiCharacterKit.Transport.V2
{
    /// <summary>
    /// Carries structured presentation commands for a successful V2 response.
    /// </summary>
    [Serializable]
    public sealed class AiNpcResponsePayloadDto
    {
        public string dialogue = string.Empty;

        public string emotion = string.Empty;

        public string gesture = string.Empty;
    }
}
