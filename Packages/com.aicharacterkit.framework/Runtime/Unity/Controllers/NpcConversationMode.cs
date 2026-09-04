namespace AiCharacterKit.Unity
{
    /// <summary>
    /// Selects deterministic, stateless, session, action, or grounded composition.
    /// </summary>
    public enum NpcConversationMode
    {
        Mock = 0,
        Backend = 1,
        BackendSession = 2,
        BackendActions = 3,
        BackendContext = 4
    }
}
