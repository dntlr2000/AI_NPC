using AiCharacterKit.Core;
using UnityEngine;

namespace AiCharacterKit.Unity.Speech
{
    /// <summary>
    /// Decorates any visual presentation driver with optional generated speech output.
    /// </summary>
    public sealed class SpeechAugmentedPresentationDriver
        : MonoBehaviour, INpcPresentationDriver
    {
        [SerializeField]
        private MonoBehaviour visualDriverSource;

        [SerializeField]
        private NpcSpeechOutput speechOutput;

        private INpcPresentationDriver visualDriver;
        private bool isConversationBusy;

        /// <summary>
        /// Resolves the reusable visual driver while leaving speech independently optional.
        /// </summary>
        private void Awake()
        {
            visualDriver = visualDriverSource as INpcPresentationDriver;
            if (visualDriver == null || ReferenceEquals(visualDriver, this))
            {
                Debug.LogError(
                    "Speech presentation requires a separate visual presentation driver.",
                    this);
                enabled = false;
            }
        }

        /// <summary>
        /// Forwards busy state and replaces old audio when a new conversation operation starts.
        /// </summary>
        public void SetBusy(bool isBusy)
        {
            if (isBusy)
            {
                speechOutput?.StopSpeech();
            }

            isConversationBusy = isBusy;
            visualDriver?.SetBusy(isBusy);
        }

        /// <summary>
        /// Presents text first and speaks only dialogue produced during an active request.
        /// </summary>
        public void PresentDialogue(string dialogue)
        {
            visualDriver?.PresentDialogue(dialogue);
            if (isConversationBusy)
            {
                speechOutput?.RequestSpeech(dialogue);
            }
        }

        /// <summary>
        /// Forwards the structured emotion command unchanged.
        /// </summary>
        public void PresentEmotion(NpcEmotion emotion)
        {
            visualDriver?.PresentEmotion(emotion);
        }

        /// <summary>
        /// Forwards the structured gesture command unchanged.
        /// </summary>
        public void PresentGesture(NpcGesture gesture)
        {
            visualDriver?.PresentGesture(gesture);
        }

        /// <summary>
        /// Stops optional speech while preserving the existing visual error fallback.
        /// </summary>
        public void PresentError(string message)
        {
            speechOutput?.StopSpeech();
            visualDriver?.PresentError(message);
        }

        /// <summary>
        /// Stops optional speech while preserving the existing cancellation presentation.
        /// </summary>
        public void PresentCancellation()
        {
            speechOutput?.StopSpeech();
            visualDriver?.PresentCancellation();
        }
    }
}
