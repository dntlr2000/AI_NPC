using System;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;

namespace AiCharacterKit.Editor
{
    /// <summary>
    /// Creates sample EventSystems without making the Input System package mandatory.
    /// </summary>
    public static class UiEventSystemFactory
    {
        private const string InputSystemModuleTypeName =
            "UnityEngine.InputSystem.UI.InputSystemUIInputModule, Unity.InputSystem";

        /// <summary>
        /// Creates or repairs the active scene's EventSystem for the configured Unity input backend.
        /// </summary>
        public static EventSystem EnsureCompatibleEventSystem()
        {
            var eventSystem = UnityEngine.Object.FindAnyObjectByType<EventSystem>(
                FindObjectsInactive.Include);
            var eventSystemObject = eventSystem == null
                ? new GameObject("EventSystem")
                : eventSystem.gameObject;
            eventSystemObject.SetActive(false);

            GameObjectUtility.RemoveMonoBehavioursWithMissingScript(eventSystemObject);
            eventSystem = eventSystemObject.GetComponent<EventSystem>();
            if (eventSystem == null)
            {
                eventSystem = eventSystemObject.AddComponent<EventSystem>();
            }

            EnsureCompatibleInputModule(eventSystemObject);
            eventSystemObject.SetActive(true);
            EditorUtility.SetDirty(eventSystemObject);
            return eventSystem;
        }

        /// <summary>
        /// Keeps one compatible input module and removes modules for inactive input backends.
        /// </summary>
        private static void EnsureCompatibleInputModule(GameObject eventSystemObject)
        {
            var preferredType = GetPreferredInputModuleType();
            BaseInputModule preferredModule = null;
            foreach (var module in eventSystemObject.GetComponents<BaseInputModule>())
            {
                if (module.GetType() == preferredType && preferredModule == null)
                {
                    preferredModule = module;
                    continue;
                }

                UnityEngine.Object.DestroyImmediate(module);
            }

            if (preferredModule == null)
            {
                preferredModule = (BaseInputModule)eventSystemObject.AddComponent(
                    preferredType);
            }

            EnsureInputSystemModuleActions(preferredModule);
        }

        /// <summary>
        /// Selects the package-backed or legacy uGUI input module enabled by Player Settings.
        /// </summary>
        private static Type GetPreferredInputModuleType()
        {
#if ENABLE_INPUT_SYSTEM
            var inputSystemType = Type.GetType(InputSystemModuleTypeName, false);
            if (inputSystemType != null
                && typeof(BaseInputModule).IsAssignableFrom(inputSystemType))
            {
                return inputSystemType;
            }
#endif

#if ENABLE_LEGACY_INPUT_MANAGER
            return typeof(StandaloneInputModule);
#else
            throw new InvalidOperationException(
                "No supported Unity UI input backend is enabled. Enable the Input System or legacy Input Manager.");
#endif
        }

        /// <summary>
        /// Assigns self-contained defaults when an Input System module has no valid action asset.
        /// </summary>
        private static void EnsureInputSystemModuleActions(BaseInputModule inputModule)
        {
            if (!string.Equals(
                    inputModule.GetType().FullName,
                    "UnityEngine.InputSystem.UI.InputSystemUIInputModule",
                    StringComparison.Ordinal))
            {
                return;
            }

            var actionsProperty = inputModule.GetType().GetProperty(
                "actionsAsset",
                BindingFlags.Instance | BindingFlags.Public);
            if (actionsProperty == null)
            {
                throw new InvalidOperationException(
                    "The installed Input System does not expose actionsAsset.");
            }

            if (actionsProperty.GetValue(inputModule) is UnityEngine.Object)
            {
                return;
            }

            var assignDefaults = inputModule.GetType().GetMethod(
                "AssignDefaultActions",
                BindingFlags.Instance | BindingFlags.Public);
            if (assignDefaults == null)
            {
                throw new InvalidOperationException(
                    "The installed Input System does not expose AssignDefaultActions().");
            }

            try
            {
                assignDefaults.Invoke(inputModule, null);
            }
            catch (TargetInvocationException exception)
            {
                throw new InvalidOperationException(
                    "The Input System could not assign default UI actions.",
                    exception.InnerException ?? exception);
            }
        }
    }
}
