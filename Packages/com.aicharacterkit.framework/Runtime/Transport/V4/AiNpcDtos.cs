using System;

namespace AiCharacterKit.Transport.V4
{
    /// <summary>
    /// Carries the stable character fields shared with earlier dialogue contracts.
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
    /// Carries one bounded character or world fact for context-grounded generation.
    /// </summary>
    [Serializable]
    public sealed class NpcContextFactDto
    {
        public string factId = string.Empty;
        public string kind = string.Empty;
        public string statement = string.Empty;
        public int priority;
    }

    /// <summary>
    /// Carries normalized character canon and the relevant current fact snapshot.
    /// </summary>
    [Serializable]
    public sealed class NpcGroundingSnapshotDto
    {
        public string revision = string.Empty;
        public string background = string.Empty;
        public string goalsAndValues = string.Empty;
        public string[] behavioralRules;
        public string[] dialogueExamples;
        public NpcContextFactDto[] facts;
    }

    /// <summary>
    /// Carries one bounded semantic trigger without its Unity action binding.
    /// </summary>
    [Serializable]
    public sealed class AiNpcTriggerDto
    {
        public string triggerId = string.Empty;
        public string conditionDescription = string.Empty;
    }

    /// <summary>
    /// Defines one session-aware context-grounded conversation request.
    /// </summary>
    [Serializable]
    public sealed class AiNpcRequestEnvelopeDto
    {
        public int schemaVersion;
        public string requestId = string.Empty;
        public string sessionId = string.Empty;
        public CharacterSnapshotDto character;
        public NpcGroundingSnapshotDto grounding;
        public string userText = string.Empty;
        public AiNpcTriggerDto[] triggers;
    }

    /// <summary>
    /// Carries dialogue presentation and only matched IDs from the request snapshot.
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
    /// Carries one stable machine-readable V4 failure without provider details.
    /// </summary>
    [Serializable]
    public sealed class AiNpcErrorDto
    {
        public string code = string.Empty;
        public string message = string.Empty;
        public bool retryable;
    }

    /// <summary>
    /// Defines one exclusive success or error response for a grounded turn.
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
    /// Defines one correlated request to reset a V4 character-bound session.
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
    /// Confirms that a V4 reset request completed successfully.
    /// </summary>
    [Serializable]
    public sealed class AiNpcSessionResetResultDto
    {
        public bool reset;
    }

    /// <summary>
    /// Defines one exclusive success or error response for a V4 session reset.
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
