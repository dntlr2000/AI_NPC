using System;

namespace AiCharacterKit.Transport.V1
{
    /// <summary>
    /// Carries structured presentation commands for a successful response.
    /// </summary>
    [Serializable]
    public sealed class AiNpcResponsePayloadDto
    {
        public string dialogue = string.Empty;

        public string emotion = string.Empty;

        public string gesture = string.Empty;
    }
}
