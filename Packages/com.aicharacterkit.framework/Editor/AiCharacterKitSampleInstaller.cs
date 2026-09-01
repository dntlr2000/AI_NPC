using System;
using UnityEditor;
using UnityEditor.PackageManager.UI;
using UnityEditor.SceneManagement;
using PackageManagerPackageInfo = UnityEditor.PackageManager.PackageInfo;

namespace AiCharacterKit.Editor
{
    /// <summary>
    /// Imports the consolidated UPM sample into writable Assets before running scene repair.
    /// </summary>
    public static class AiCharacterKitSampleInstaller
    {
        private const string SampleDisplayName = "AI NPC Prototypes";

        /// <summary>
        /// Imports the sample through UPM and repairs it after protecting unsaved user scenes.
        /// </summary>
        [MenuItem("Tools/AI Character Kit/Import or Repair AI NPC Prototypes")]
        public static void ImportOrRepairSample()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                return;
            }

            if (!TryImportSample(false, out var sampleRoot, out var error))
            {
                EditorUtility.DisplayDialog(
                    "AI Character Kit Samples",
                    error,
                    "OK");
                return;
            }

            PrototypeSceneBuilder.RepairAllSampleScenesBatch();
            EditorUtility.DisplayDialog(
                "AI Character Kit Samples",
                "The AI NPC Prototypes sample is ready at:\n" + sampleRoot,
                "OK");
        }

        /// <summary>
        /// Reimports and repairs the sample without prompts for isolated validation projects.
        /// </summary>
        public static void ImportAndRepairSampleBatch()
        {
            if (!TryImportSample(true, out _, out var error))
            {
                throw new InvalidOperationException(error);
            }

            PrototypeSceneBuilder.RepairAllSampleScenesBatch();
        }

        /// <summary>
        /// Finds the installed package sample and imports it into its versioned Assets location.
        /// </summary>
        public static bool TryImportSample(
            bool overwritePreviousImport,
            out string sampleRoot,
            out string error)
        {
            sampleRoot = string.Empty;
            error = string.Empty;

            try
            {
                var packageInfo = PackageManagerPackageInfo.FindForAssembly(
                    typeof(AiCharacterKitSampleInstaller).Assembly);
                if (packageInfo == null
                    || !string.Equals(
                        packageInfo.name,
                        AiCharacterKitAssetPaths.PackageName,
                        StringComparison.Ordinal))
                {
                    error =
                        "AI NPC Prototypes can be imported only from the installed AI Character Kit UPM package.";
                    return false;
                }

                if (!TryFindPrototypeSample(
                        packageInfo.name,
                        packageInfo.version,
                        out var selectedSample))
                {
                    error =
                        "The installed AI Character Kit package does not declare the AI NPC Prototypes sample.";
                    return false;
                }

                if (!selectedSample.isImported || overwritePreviousImport)
                {
                    var options = Sample.ImportOptions.HideImportWindow;
                    if (overwritePreviousImport)
                    {
                        options |= Sample.ImportOptions.OverridePreviousImports;
                    }

                    if (!selectedSample.Import(options))
                    {
                        error = "Unity could not import the AI NPC Prototypes sample.";
                        return false;
                    }

                    AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
                }

                sampleRoot = selectedSample.importPath.Replace('\\', '/');
                return true;
            }
            catch (Exception exception)
            {
                error =
                    "Unity could not import the AI NPC Prototypes sample: "
                    + exception.Message;
                return false;
            }
        }

        /// <summary>
        /// Selects the one consolidated prototype sample declared by the installed package version.
        /// </summary>
        private static bool TryFindPrototypeSample(
            string packageName,
            string packageVersion,
            out Sample selectedSample)
        {
            foreach (var sample in Sample.FindByPackage(
                         packageName,
                         packageVersion))
            {
                if (string.Equals(
                        sample.displayName,
                        SampleDisplayName,
                        StringComparison.Ordinal))
                {
                    selectedSample = sample;
                    return true;
                }
            }

            selectedSample = default;
            return false;
        }
    }
}
