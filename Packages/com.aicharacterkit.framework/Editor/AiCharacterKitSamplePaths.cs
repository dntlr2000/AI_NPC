using System;
using System.Collections.Generic;
using UnityEditor;
using PackageManagerPackageInfo = UnityEditor.PackageManager.PackageInfo;

namespace AiCharacterKit.Editor
{
    /// <summary>
    /// Resolves imported or generated sample assets under a writable Assets location.
    /// </summary>
    public static class AiCharacterKitSamplePaths
    {
        public const string DefaultGeneratedRootFolder =
            "Assets/AI Character Kit/Samples";

        private const string PrototypeSceneSuffix =
            "/MockNpc/Scenes/MockNpcPrototype.unity";

        private const string ImportedSamplePrefix =
            "Assets/Samples/AI Character Kit/";

        private const string ImportedSampleSuffix =
            "/AI NPC Prototypes";

        public static string RootFolder
        {
            get
            {
                if (TryGetExistingRootFolder(out var rootFolder, out var error))
                {
                    return rootFolder;
                }

                if (!string.IsNullOrEmpty(error))
                {
                    throw new InvalidOperationException(error);
                }

                return DefaultGeneratedRootFolder;
            }
        }

        /// <summary>
        /// Finds one already imported or generated sample root from its prototype scene.
        /// </summary>
        public static bool TryGetExistingRootFolder(
            out string rootFolder,
            out string error)
        {
            rootFolder = string.Empty;
            error = string.Empty;
            var candidates = new List<string>();
            var matches = AssetDatabase.FindAssets(
                "MockNpcPrototype t:Scene",
                new[] { "Assets" });
            foreach (var guid in matches)
            {
                var assetPath = AssetDatabase.GUIDToAssetPath(guid)
                    .Replace('\\', '/');
                if (!assetPath.EndsWith(
                        PrototypeSceneSuffix,
                        StringComparison.Ordinal))
                {
                    continue;
                }

                var candidateRoot = assetPath.Substring(
                    0,
                    assetPath.Length - PrototypeSceneSuffix.Length);
                AddUniqueCandidate(candidates, candidateRoot);
            }

            if (candidates.Count == 1)
            {
                rootFolder = candidates[0];
                return true;
            }

            if (candidates.Count > 1
                && TryGetCurrentPackageSampleRoot(out var currentSampleRoot))
            {
                foreach (var candidate in candidates)
                {
                    if (string.Equals(
                            candidate,
                            currentSampleRoot,
                            StringComparison.Ordinal))
                    {
                        rootFolder = candidate;
                        return true;
                    }
                }
            }

            if (candidates.Count > 1)
            {
                error =
                    "Multiple AI Character Kit sample installations were found under Assets, "
                    + "and none uniquely matches the installed package version. "
                    + "Keep one sample root or import the current package sample before using automation.";
            }

            return false;
        }

        /// <summary>
        /// Combines the writable sample root with one safe sample-relative asset path.
        /// </summary>
        public static string Resolve(string relativePath)
        {
            if (relativePath == null)
            {
                throw new ArgumentNullException(nameof(relativePath));
            }

            var normalized = relativePath.Replace('\\', '/').Trim('/');
            if (normalized.Length == 0)
            {
                return RootFolder;
            }

            if (normalized == ".."
                || normalized.StartsWith("../", StringComparison.Ordinal)
                || normalized.Contains("/../"))
            {
                throw new ArgumentException(
                    "AI Character Kit sample paths cannot traverse outside the sample root.",
                    nameof(relativePath));
            }

            return RootFolder + "/" + normalized;
        }

        /// <summary>
        /// Builds the standard writable import root for the currently installed UPM package version.
        /// </summary>
        private static bool TryGetCurrentPackageSampleRoot(
            out string sampleRoot)
        {
            sampleRoot = string.Empty;
            var packageInfo = PackageManagerPackageInfo.FindForAssembly(
                typeof(AiCharacterKitSamplePaths).Assembly);
            if (packageInfo == null
                || !string.Equals(
                    packageInfo.name,
                    AiCharacterKitAssetPaths.PackageName,
                    StringComparison.Ordinal)
                || string.IsNullOrWhiteSpace(packageInfo.version))
            {
                return false;
            }

            sampleRoot = ImportedSamplePrefix
                + packageInfo.version
                + ImportedSampleSuffix;
            return true;
        }

        /// <summary>
        /// Adds one normalized sample candidate without duplicating the same asset path.
        /// </summary>
        private static void AddUniqueCandidate(
            ICollection<string> candidates,
            string candidate)
        {
            foreach (var existing in candidates)
            {
                if (string.Equals(existing, candidate, StringComparison.Ordinal))
                {
                    return;
                }
            }

            candidates.Add(candidate);
        }
    }
}
