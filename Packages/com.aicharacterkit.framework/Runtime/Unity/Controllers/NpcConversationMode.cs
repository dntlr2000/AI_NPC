namespace AiCharacterKit.Unity
{
    /// <summary>
    /// Selects deterministic, stateless, session-aware, or action-aware composition.
    /// </summary>
    public enum NpcConversationMode
    {
        Mock = 0,
        Backend = 1,
        BackendSession = 2,
        BackendActions = 3
    }
}
