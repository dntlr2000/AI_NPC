namespace AiCharacterKit.Unity
{
    /// <summary>
    /// Selects deterministic, stateless backend, or session-aware backend composition.
    /// </summary>
    public enum NpcConversationMode
    {
        Mock = 0,
        Backend = 1,
        BackendSession = 2
    }
}
