namespace AiCharacterKit.Unity
{
    /// <summary>
    /// Selects deterministic, backend, or optional local grounded conversation composition.
    /// </summary>
    public enum NpcConversationMode
    {
        Mock = 0,
        Backend = 1,
        BackendSession = 2,
        BackendActions = 3,
        BackendContext = 4,
        LocalInference = 5
    }
}
