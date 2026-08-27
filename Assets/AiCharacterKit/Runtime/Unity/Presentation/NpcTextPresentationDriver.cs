using AiCharacterKit.Core;
using UnityEngine;
using UnityEngine.UI;

namespace AiCharacterKit.Unity
{
    /// <summary>
    /// Presents Phase 1 responses through uGUI labels and simple transform/color feedback.
    /// </summary>
    public sealed class NpcTextPresentationDriver : MonoBehaviour, INpcPresentationDriver
    {
        [SerializeField]
        private Text dialogueText;

        [SerializeField]
        private Text emotionText;

        [SerializeField]
        private Text gestureText;

        [SerializeField]
        private Text statusText;

        [SerializeField]
        private Button sendButton;

        [SerializeField]
        private Renderer emotionRenderer;

        [SerializeField]
        private Transform gestureTarget;

        private MaterialPropertyBlock materialProperties;
        private Quaternion initialGestureRotation;
        private int baseColorPropertyId;
        private int colorPropertyId;

        /// <summary>
        /// Caches presentation state needed for non-destructive renderer and transform updates.
        /// </summary>
        private void Awake()
        {
            materialProperties = new MaterialPropertyBlock();
            initialGestureRotation =
                gestureTarget == null ? Quaternion.identity : gestureTarget.localRotation;
            baseColorPropertyId = Shader.PropertyToID("_BaseColor");
            colorPropertyId = Shader.PropertyToID("_Color");
        }

        /// <summary>
        /// Shows request progress and prevents repeated button submissions while busy.
        /// </summary>
        public void SetBusy(bool isBusy)
        {
            if (statusText != null)
            {
                if (isBusy)
                {
                    statusText.text = "상태: 응답 생성 중...";
                }
                else if (statusText.text == "상태: 응답 생성 중...")
                {
                    statusText.text = "상태: 준비";
                }
            }

            if (sendButton != null)
            {
                sendButton.interactable = !isBusy;
            }
        }

        /// <summary>
        /// Displays the latest generated dialogue.
        /// </summary>
        public void PresentDialogue(string dialogue)
        {
            if (dialogueText != null)
            {
                dialogueText.text = dialogue;
            }
        }

        /// <summary>
        /// Displays the emotion command and maps it to a visible NPC color.
        /// </summary>
        public void PresentEmotion(NpcEmotion emotion)
        {
            if (emotionText != null)
            {
                emotionText.text = $"감정: {emotion}";
            }

            ApplyEmotionColor(GetEmotionColor(emotion));
        }

        /// <summary>
        /// Displays the gesture command and maps it to a simple deterministic pose.
        /// </summary>
        public void PresentGesture(NpcGesture gesture)
        {
            if (gestureText != null)
            {
                gestureText.text = $"제스처: {gesture}";
            }

            if (gestureTarget != null)
            {
                gestureTarget.localRotation =
                    initialGestureRotation * GetGestureRotation(gesture);
            }
        }

        /// <summary>
        /// Displays a recoverable error without throwing through the Unity event loop.
        /// </summary>
        public void PresentError(string message)
        {
            if (statusText != null)
            {
                statusText.text = $"오류: {message}";
            }
        }

        /// <summary>
        /// Displays the cancellation outcome for the active request.
        /// </summary>
        public void PresentCancellation()
        {
            if (statusText != null)
            {
                statusText.text = "상태: 요청 취소됨";
            }
        }

        /// <summary>
        /// Applies a color through a property block without instantiating a material.
        /// </summary>
        private void ApplyEmotionColor(Color color)
        {
            if (emotionRenderer == null)
            {
                return;
            }

            emotionRenderer.GetPropertyBlock(materialProperties);
            materialProperties.SetColor(baseColorPropertyId, color);
            materialProperties.SetColor(colorPropertyId, color);
            emotionRenderer.SetPropertyBlock(materialProperties);
        }

        /// <summary>
        /// Maps each supported emotion to an immediately recognizable prototype color.
        /// </summary>
        private static Color GetEmotionColor(NpcEmotion emotion)
        {
            switch (emotion)
            {
                case NpcEmotion.Happy:
                    return new Color(0.3f, 0.9f, 0.45f);
                case NpcEmotion.Sad:
                    return new Color(0.3f, 0.55f, 0.95f);
                case NpcEmotion.Angry:
                    return new Color(0.95f, 0.3f, 0.25f);
                case NpcEmotion.Concerned:
                    return new Color(0.95f, 0.75f, 0.25f);
                default:
                    return new Color(0.75f, 0.75f, 0.75f);
            }
        }

        /// <summary>
        /// Maps each supported gesture to a small visible local rotation.
        /// </summary>
        private static Quaternion GetGestureRotation(NpcGesture gesture)
        {
            switch (gesture)
            {
                case NpcGesture.Nod:
                    return Quaternion.Euler(12f, 0f, 0f);
                case NpcGesture.Wave:
                    return Quaternion.Euler(0f, 0f, -12f);
                default:
                    return Quaternion.identity;
            }
        }
    }
}
