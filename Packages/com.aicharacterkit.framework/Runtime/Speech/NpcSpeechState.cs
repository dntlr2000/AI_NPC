namespace AiCharacterKit.Speech
{
    /// <summary>
    /// Describes the visible lifecycle of one optional NPC speech output.
    /// </summary>
    public enum NpcSpeechState
    {
        Disabled = 0,
        Idle = 1,
        Synthesizing = 2,
        Playing = 3,
        Failed = 4
    }
}
