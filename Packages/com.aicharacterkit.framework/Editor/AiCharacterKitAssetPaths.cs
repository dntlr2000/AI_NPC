using System;
using System.Collections.Generic;
using UnityEditor;
using PackageManagerPackageInfo = UnityEditor.PackageManager.PackageInfo;

namespace AiCharacterKit.Editor
{
    /// <summary>
    /// Resolves the movable raw Assets or UPM package root used by editor tools and tests.
    /// </summary>
    public static class AiCharacterKitAssetPaths
    {
        public const string PackageName = "com.aicharacterkit.framework";

        private const string CoreAssemblyMarker =
            "/Runtime/Core/AiCharacterKit.Core.asmdef";

        public static string RootFolder
        {
            get
            {
                if (TryGetRootFolder(out var rootFolder, out var error))
                {
                    return rootFolder;
                }

                throw new InvalidOperationException(error);
            }
        }

        /// <summary>
        /// Finds the unique kit root from its Core assembly definition without assuming an import folder.
        /// </summary>
        public static bool TryGetRootFolder(
            out string rootFolder,
            out string error)
        {
            rootFolder = string.Empty;
            error = string.Empty;
            var candidates = new List<string>();

            var packageInfo = PackageManagerPackageInfo.FindForAssembly(
                typeof(AiCharacterKitAssetPaths).Assembly);
            if (packageInfo != null
                && !string.IsNullOrWhiteSpace(packageInfo.assetPath))
            {
                AddUniqueCandidate(
                    candidates,
                    packageInfo.assetPath.Replace('\\', '/').TrimEnd('/'));
            }

            var matches = AssetDatabase.FindAssets("AiCharacterKit.Core");
            foreach (var guid in matches)
            {
                var assetPath = AssetDatabase.GUIDToAssetPath(guid)
                    .Replace('\\', '/');
                if (!assetPath.EndsWith(
                        CoreAssemblyMarker,
                        StringComparison.Ordinal))
                {
                    continue;
                }

                var candidateRoot = assetPath.Substring(
                    0,
                    assetPath.Length - CoreAssemblyMarker.Length);
                if (!candidateRoot.StartsWith("Assets/", StringComparison.Ordinal)
                    && !string.Equals(candidateRoot, "Assets", StringComparison.Ordinal))
                {
                    continue;
                }

                AddUniqueCandidate(candidates, candidateRoot);
            }

            if (candidates.Count == 1)
            {
                rootFolder = candidates[0];
                return true;
            }

            if (candidates.Count > 1)
            {
                error =
                    "Multiple AI Character Kit installations were found. "
                    + "Remove the duplicate raw Assets or UPM package installation before using editor automation.";
                return false;
            }

            error =
                "AI Character Kit could not locate Runtime/Core/AiCharacterKit.Core.asmdef in Assets or an installed package.";
            return false;
        }

        /// <summary>
        /// Combines the discovered installation root with one safe kit-relative asset path.
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
                    "AI Character Kit relative paths cannot traverse outside the installation root.",
                    nameof(relativePath));
            }

            return RootFolder + "/" + normalized;
        }

        /// <summary>
        /// Adds one normalized installation candidate without duplicating the same asset path.
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
