using System;
using System.IO;
using AiCharacterKit.Editor;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;

namespace AiCharacterKit.Core.Tests
{
    /// <summary>
    /// Guards the movable install root and UPM boundaries proven in Phases 8 and 9.
    /// </summary>
    public sealed class PortabilityBoundaryTests
    {
        private static readonly string[] SampleScenePaths =
        {
            "MockNpc/Scenes/MockNpcPrototype.unity",
            "MockNpc/Scenes/MultiCharacterMock.unity",
            "BackendNpc/Scenes/BackendNpcPrototype.unity",
            "MemoryNpc/Scenes/MemoryNpcPrototype.unity",
            "SpeechNpc/Scenes/SpeechNpcPrototype.unity",
            "VoiceInputNpc/Scenes/VoiceInputNpcPrototype.unity",
            "ActionNpc/Scenes/ActionNpcPrototype.unity"
        };

        /// <summary>
        /// Confirms root discovery resolves the Core assembly at its current import location.
        /// </summary>
        [Test]
        public void AssetPaths_AtCurrentLocation_ResolveCoreAssembly()
        {
            var markerPath = AiCharacterKitAssetPaths.Resolve(
                "Runtime/Core/AiCharacterKit.Core.asmdef");

            Assert.That(
                AiCharacterKitAssetPaths.RootFolder.StartsWith(
                    "Assets/",
                    StringComparison.Ordinal)
                || AiCharacterKitAssetPaths.RootFolder.StartsWith(
                    "Packages/",
                    StringComparison.Ordinal),
                Is.True);
            Assert.That(AssetDatabase.LoadMainAssetAtPath(markerPath), Is.Not.Null);
        }

        /// <summary>
        /// Confirms path resolution refuses to escape the discovered installation root.
        /// </summary>
        [Test]
        public void AssetPaths_WithParentTraversal_ThrowsArgumentException()
        {
            Assert.Throws<ArgumentException>(
                () => AiCharacterKitAssetPaths.Resolve("../Outside.asset"));
        }

        /// <summary>
        /// Confirms the resolved installation exposes the expected package identity and version.
        /// </summary>
        [Test]
        public void PackageMetadata_AtResolvedRoot_DeclaresVersionedSample()
        {
            var manifestPath = AiCharacterKitAssetPaths.Resolve("package.json");
            var contents = File.ReadAllText(manifestPath);

            Assert.That(
                AiCharacterKitAssetPaths.RootFolder,
                Does.StartWith("Packages/"));
            Assert.That(
                contents,
                Does.Contain("\"name\": \"com.aicharacterkit.framework\""));
            Assert.That(contents, Does.Contain("\"version\": \"0.3.0\""));
            Assert.That(
                contents,
                Does.Contain("\"path\": \"Samples~/AI NPC Prototypes\""));
        }

        /// <summary>
        /// Confirms sample output always resolves to a writable Assets location.
        /// </summary>
        [Test]
        public void SamplePaths_ResolveWritableAssetsLocation()
        {
            Assert.That(
                AiCharacterKitSamplePaths.RootFolder,
                Does.StartWith("Assets/"));
            Assert.Throws<ArgumentException>(
                () => AiCharacterKitSamplePaths.Resolve("../Outside.asset"));
        }

        /// <summary>
        /// Confirms editor and test assemblies do not make the optional Input System mandatory.
        /// </summary>
        [Test]
        public void AssemblyDefinitions_DoNotReferenceOptionalInputSystem()
        {
            AssertAssemblyDoesNotReferenceInputSystem(
                "Editor/AiCharacterKit.Editor.asmdef");
            AssertAssemblyDoesNotReferenceInputSystem(
                "Tests/Editor/AiCharacterKit.Core.Tests.EditMode.asmdef");
        }

        /// <summary>
        /// Confirms the factory creates a usable module for the project's active input backend.
        /// </summary>
        [Test]
        public void EventSystemFactory_ForActiveBackend_CreatesUsableInputModule()
        {
            EditorSceneManager.NewScene(
                NewSceneSetup.EmptyScene,
                NewSceneMode.Single);
            var eventSystem = UiEventSystemFactory.EnsureCompatibleEventSystem();

            Assert.That(eventSystem, Is.Not.Null);
            var inputModule = eventSystem.GetComponent<BaseInputModule>();
            Assert.That(inputModule, Is.Not.Null);
            AssertInputModuleIsUsable(inputModule);

            if (string.Equals(
                    inputModule.GetType().FullName,
                    "UnityEngine.InputSystem.UI.InputSystemUIInputModule",
                    StringComparison.Ordinal))
            {
                var actionsProperty = inputModule.GetType().GetProperty("actionsAsset");
                Assert.That(actionsProperty, Is.Not.Null);
                Assert.That(
                    actionsProperty.GetValue(inputModule) as UnityEngine.Object,
                    Is.Not.Null);

                actionsProperty.SetValue(inputModule, null);
                eventSystem = UiEventSystemFactory.EnsureCompatibleEventSystem();
                inputModule = eventSystem.GetComponent<BaseInputModule>();
                AssertInputModuleIsUsable(inputModule);
            }
        }

        /// <summary>
        /// Confirms every repaired sample scene has a usable module for the active input backend.
        /// </summary>
        [Test]
        public void SampleEventSystems_ForActiveBackend_AreUsable()
        {
            foreach (var relativePath in SampleScenePaths)
            {
                var scenePath = AiCharacterKitSamplePaths.Resolve(relativePath);
                EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
                var eventSystem = UnityEngine.Object.FindAnyObjectByType<EventSystem>();

                Assert.That(eventSystem, Is.Not.Null, scenePath);
                AssertInputModuleIsUsable(
                    eventSystem.GetComponent<BaseInputModule>(),
                    scenePath);
            }
        }

        /// <summary>
        /// Verifies one uGUI module and its optional Input System action asset are ready for input.
        /// </summary>
        private static void AssertInputModuleIsUsable(
            BaseInputModule inputModule,
            string context = null)
        {
            Assert.That(inputModule, Is.Not.Null, context);
            if (!string.Equals(
                    inputModule.GetType().FullName,
                    "UnityEngine.InputSystem.UI.InputSystemUIInputModule",
                    StringComparison.Ordinal))
            {
                return;
            }

            var actionsProperty = inputModule.GetType().GetProperty("actionsAsset");
            Assert.That(actionsProperty, Is.Not.Null, context);
            Assert.That(
                actionsProperty.GetValue(inputModule) as UnityEngine.Object,
                Is.Not.Null,
                context);
        }

        /// <summary>
        /// Reads one assembly definition and rejects a direct Input System reference.
        /// </summary>
        private static void AssertAssemblyDoesNotReferenceInputSystem(
            string relativePath)
        {
            var assetPath = AiCharacterKitAssetPaths.Resolve(relativePath);
            var contents = File.ReadAllText(assetPath);

            Assert.That(contents, Does.Not.Contain("\"Unity.InputSystem\""));
        }
    }
}
