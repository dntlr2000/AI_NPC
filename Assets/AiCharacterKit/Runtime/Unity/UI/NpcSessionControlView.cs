using System;
using UnityEngine;
using UnityEngine.UI;

namespace AiCharacterKit.Unity
{
    /// <summary>
    /// Connects one optional reset button and visible memory status to an NPC session.
    /// </summary>
    public sealed class NpcSessionControlView : MonoBehaviour
    {
        [SerializeField]
        private Button resetButton;

        [SerializeField]
        private Text memoryStatusText;

        [SerializeField]
        private NpcConversationBehaviour conversationBehaviour;

        /// <summary>
        /// Registers the reset button listener for the enabled lifetime.
        /// </summary>
        private void OnEnable()
        {
            if (resetButton != null)
            {
                resetButton.onClick.AddListener(HandleResetButtonClicked);
            }
        }

        /// <summary>
        /// Reflects whether the configured conversation supports session reset.
        /// </summary>
        private void Start()
        {
            var supported = conversationBehaviour != null
                && conversationBehaviour.SupportsConversationReset;
            if (resetButton != null)
            {
                resetButton.interactable = supported;
            }

            if (memoryStatusText != null)
            {
                memoryStatusText.text = supported
                    ? "단기 기억: 활성"
                    : "단기 기억: 지원 안 함";
            }
        }

        /// <summary>
        /// Removes the reset listener to avoid duplicate subscriptions.
        /// </summary>
        private void OnDisable()
        {
            if (resetButton != null)
            {
                resetButton.onClick.RemoveListener(HandleResetButtonClicked);
            }
        }

        /// <summary>
        /// Handles the Unity button event without leaking asynchronous failures.
        /// </summary>
        private async void HandleResetButtonClicked()
        {
            if (resetButton == null || conversationBehaviour == null)
            {
                Debug.LogError("NpcSessionControlView is missing required references.", this);
                return;
            }

            resetButton.interactable = false;
            try
            {
                var succeeded = await conversationBehaviour.ResetConversationAsync();
                if (memoryStatusText != null)
                {
                    memoryStatusText.text = succeeded
                        ? "단기 기억: 초기화됨"
                        : "단기 기억: 초기화 실패";
                }
            }
            catch (Exception exception)
            {
                Debug.LogException(exception, this);
                if (memoryStatusText != null)
                {
                    memoryStatusText.text = "단기 기억: 초기화 실패";
                }
            }
            finally
            {
                resetButton.interactable =
                    conversationBehaviour.SupportsConversationReset
                    && !conversationBehaviour.IsRequestInProgress;
            }
        }
    }
}
