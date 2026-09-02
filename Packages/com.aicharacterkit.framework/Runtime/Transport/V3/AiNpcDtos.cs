using System;

namespace AiCharacterKit.Transport.V3
{
    /// <summary>
    /// Carries the complete character snapshot for one action-aware request.
    /// </summary>
    [Serializable]
    public sealed class CharacterSnapshotDto
    {
        public string characterId = string.Empty;
        public string displayName = string.Empty;
        public string personality = string.Empty;
        public string speechStyle = string.Empty;
        public string exampleDialogue = string.Empty;
        public string defaultEmotion = string.Empty;
    }

    /// <summary>
    /// Carries one bounded semantic trigger without exposing its Unity action binding.
    /// </summary>
    [Serializable]
    public sealed class AiNpcTriggerDto
    {
        public string triggerId = string.Empty;
        public string conditionDescription = string.Empty;
    }

    /// <summary>
    /// Defines one correlated session-aware action classification request.
    /// </summary>
    [Serializable]
    public sealed class AiNpcRequestEnvelopeDto
    {
        public int schemaVersion;
        public string requestId = string.Empty;
        public string sessionId = string.Empty;
        public CharacterSnapshotDto character;
        public string userText = string.Empty;
        public AiNpcTriggerDto[] triggers;
    }

    /// <summary>
    /// Carries dialogue presentation and only matched IDs from the request trigger snapshot.
    /// </summary>
    [Serializable]
    public sealed class AiNpcResponsePayloadDto
    {
        public string dialogue = string.Empty;
        public string emotion = string.Empty;
        public string gesture = string.Empty;
        public string[] matchedTriggerIds;
    }

    /// <summary>
    /// Carries a stable machine-readable failure without provider details.
    /// </summary>
    [Serializable]
    public sealed class AiNpcErrorDto
    {
        public string code = string.Empty;
        public string message = string.Empty;
        public bool retryable;
    }

    /// <summary>
    /// Defines one exclusive success or error response for an action-aware turn.
    /// </summary>
    [Serializable]
    public sealed class AiNpcResponseEnvelopeDto
    {
        public int schemaVersion;
        public string requestId = string.Empty;
        public string status = string.Empty;
        public AiNpcResponsePayloadDto result;
        public AiNpcErrorDto error;
    }

    /// <summary>
    /// Defines one correlated request to reset a V3 character-bound session.
    /// </summary>
    [Serializable]
    public sealed class AiNpcSessionResetRequestDto
    {
        public int schemaVersion;
        public string requestId = string.Empty;
        public string sessionId = string.Empty;
        public string characterId = string.Empty;
    }

    /// <summary>
    /// Confirms that a V3 reset request completed successfully.
    /// </summary>
    [Serializable]
    public sealed class AiNpcSessionResetResultDto
    {
        public bool reset;
    }

    /// <summary>
    /// Defines one exclusive success or error response for a V3 session reset.
    /// </summary>
    [Serializable]
    public sealed class AiNpcSessionResetResponseDto
    {
        public int schemaVersion;
        public string requestId = string.Empty;
        public string status = string.Empty;
        public AiNpcSessionResetResultDto result;
        public AiNpcErrorDto error;
    }
}
