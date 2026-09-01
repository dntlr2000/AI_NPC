using System;
using UnityEngine;
using UnityEngine.UI;

namespace AiCharacterKit.Unity
{
    /// <summary>
    /// Sends uGUI text input to one NPC conversation bridge.
    /// </summary>
    public sealed class NpcTextInputView : MonoBehaviour
    {
        [SerializeField]
        private InputField inputField;

        [SerializeField]
        private Button sendButton;

        [SerializeField]
        private NpcConversationBehaviour conversationBehaviour;

        /// <summary>
        /// Replaces the editable text without submitting it and focuses the field for review.
        /// </summary>
        public void SetInputText(string value)
        {
            if (inputField == null)
            {
                return;
            }

            inputField.text = value ?? string.Empty;
            inputField.ActivateInputField();
            inputField.MoveTextEnd(false);
        }

        /// <summary>
        /// Registers the send button listener for the enabled lifetime.
        /// </summary>
        private void OnEnable()
        {
            if (sendButton != null)
            {
                sendButton.onClick.AddListener(HandleSendButtonClicked);
            }
        }

        /// <summary>
        /// Removes the send button listener to avoid duplicate subscriptions.
        /// </summary>
        private void OnDisable()
        {
            if (sendButton != null)
            {
                sendButton.onClick.RemoveListener(HandleSendButtonClicked);
            }
        }

        /// <summary>
        /// Handles the Unity button event and keeps successful input ready for the next message.
        /// </summary>
        private async void HandleSendButtonClicked()
        {
            if (inputField == null || conversationBehaviour == null)
            {
                Debug.LogError("NpcTextInputView is missing required references.", this);
                return;
            }

            try
            {
                var succeeded = await conversationBehaviour.SubmitAsync(inputField.text);
                if (succeeded)
                {
                    inputField.text = string.Empty;
                }

                inputField.ActivateInputField();
            }
            catch (Exception exception)
            {
                Debug.LogException(exception, this);
            }
        }
    }
}
