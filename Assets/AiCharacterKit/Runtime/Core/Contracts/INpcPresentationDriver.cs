namespace AiCharacterKit.Core
{
    /// <summary>
    /// Defines presentation operations without depending on a particular Unity UI or animation system.
    /// </summary>
    public interface INpcPresentationDriver
    {
        /// <summary>
        /// Updates whether the NPC is currently processing a request.
        /// </summary>
        void SetBusy(bool isBusy);

        /// <summary>
        /// Displays the generated dialogue.
        /// </summary>
        void PresentDialogue(string dialogue);

        /// <summary>
        /// Applies the generated emotion command.
        /// </summary>
        void PresentEmotion(NpcEmotion emotion);

        /// <summary>
        /// Applies the generated gesture command.
        /// </summary>
        void PresentGesture(NpcGesture gesture);

        /// <summary>
        /// Displays a recoverable request error.
        /// </summary>
        void PresentError(string message);

        /// <summary>
        /// Displays that the active request was cancelled.
        /// </summary>
        void PresentCancellation();
    }
}
