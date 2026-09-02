using System.IO;
using System.Linq;
using AiCharacterKit.Unity;
using AiCharacterKit.Unity.Actions;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace AiCharacterKit.Core.Tests
{
    /// <summary>
    /// Verifies the imported conversation-action sample and its consumer handlers.
    /// </summary>
    public sealed class ActionSceneConfigurationTests
    {
        private static string ActionProfilePath => AiCharacterKitTestPaths.ResolveSample(
            "ActionNpc/Profiles/ActionGuideActions.asset");

        private static string ScenePath => AiCharacterKitTestPaths.ResolveSample(
            "ActionNpc/Scenes/ActionNpcPrototype.unity");

        /// <summary>
        /// Reloads the action sample and confirms its profile, handlers, and Mock wiring.
        /// </summary>
        [Test]
        public void ActionScene_AfterReload_HasRequiredConfiguration()
        {
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            Assert.That(scene.IsValid(), Is.True);
            Assert.That(scene.isLoaded, Is.True);

            var actionProfile = AssetDatabase.LoadAssetAtPath<NpcActionProfile>(
                ActionProfilePath);
            Assert.That(actionProfile, Is.Not.Null);
            Assert.That(actionProfile.TryValidate(out var error), Is.True, error);
            Assert.That(actionProfile.CreateDefinitions(), Has.Count.EqualTo(2));

            var npc = GameObject.Find("Action Guide NPC");
            Assert.That(npc, Is.Not.Null);
            var conversation = npc.GetComponent<NpcConversationBehaviour>();
            var coordinator = npc.GetComponent<NpcActionCoordinator>();
            Assert.That(conversation, Is.Not.Null);
            Assert.That(coordinator, Is.Not.Null);
            Assert.That(
                GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(npc),
                Is.Zero,
                "The generated action NPC must survive a Scene reload without missing scripts.");
            Assert.That(coordinator.TryValidateConfiguration(out error), Is.True, error);

            var conversationState = new SerializedObject(conversation);
            Assert.That(
                conversationState.FindProperty("conversationMode").enumValueIndex,
                Is.EqualTo((int)NpcConversationMode.Mock));
            Assert.That(
                conversationState.FindProperty("actionCoordinator").objectReferenceValue,
                Is.EqualTo(coordinator));

            var handlers = npc.GetComponents<MonoBehaviour>()
                .OfType<INpcActionHandler>()
                .ToArray();
            foreach (var handler in handlers)
            {
                var behaviour = (MonoBehaviour)handler;
                var script = MonoScript.FromMonoBehaviour(behaviour);
                Assert.That(
                    script,
                    Is.Not.Null,
                    $"{behaviour.GetType().FullName} must resolve to a persistent MonoScript asset.");
                Assert.That(
                    Path.GetFileNameWithoutExtension(AssetDatabase.GetAssetPath(script)),
                    Is.EqualTo(behaviour.GetType().Name),
                    "Sample MonoBehaviour source files must match their class names for Scene persistence.");
            }

            var actionIds = handlers
                .Select(handler => handler.ActionId)
                .ToArray();
            Assert.That(actionIds, Is.EquivalentTo(new[]
            {
                "wave_to_player",
                "open_gate",
            }));
            Assert.That(Object.FindObjectsByType<InputField>(
                FindObjectsSortMode.None), Is.Not.Empty);
            Assert.That(Object.FindObjectsByType<Button>(
                FindObjectsSortMode.None), Is.Not.Empty);
        }
    }
}
