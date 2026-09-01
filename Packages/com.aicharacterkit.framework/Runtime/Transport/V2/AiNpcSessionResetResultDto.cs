using System;

namespace AiCharacterKit.Transport.V2
{
    /// <summary>
    /// Confirms that a reset request completed successfully.
    /// </summary>
    [Serializable]
    public sealed class AiNpcSessionResetResultDto
    {
        public bool reset;
    }
}
