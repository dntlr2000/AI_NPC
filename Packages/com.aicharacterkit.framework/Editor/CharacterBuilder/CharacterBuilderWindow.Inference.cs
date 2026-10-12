using AiCharacterKit.Core;
using AiCharacterKit.Unity;
using UnityEditor;
using UnityEngine;

namespace AiCharacterKit.Editor
{
    internal sealed partial class CharacterBuilderWindow
    {
        /// <summary>Offers only validated compositions while preserving a loaded unsupported selection for explicit correction.</summary>
        private void DrawInferenceStrategy()
        {
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.PrefixLabel("Inference Strategy");
            if (GUILayout.Button(configuration.InferenceStrategy.ToString(), EditorStyles.popup))
            {
                var menu = new GenericMenu();
                menu.AddItem(new GUIContent("SinglePass"), configuration.InferenceStrategy == NpcInferenceStrategy.SinglePass,
                    () => configuration.InferenceStrategy = NpcInferenceStrategy.SinglePass);
                var label = new GUIContent("ClassifyThenRespond");
                if (NpcInferenceAvailability.TryValidate(configuration.ConversationMode, NpcInferenceStrategy.ClassifyThenRespond, out _))
                    menu.AddItem(label, configuration.InferenceStrategy == NpcInferenceStrategy.ClassifyThenRespond,
                        () => configuration.InferenceStrategy = NpcInferenceStrategy.ClassifyThenRespond);
                else menu.AddDisabledItem(label);
                menu.ShowAsContext();
            }
            EditorGUILayout.EndHorizontal();
            if (configuration.InferenceStrategy == NpcInferenceStrategy.ClassifyThenRespond)
                EditorGUILayout.HelpBox("Two calls classify intent, then generate dialogue. Basic/Advanced rules still execute once. Only documented model configurations are validated.", MessageType.Info);
        }
    }
}
