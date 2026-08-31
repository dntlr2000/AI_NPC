using System;
using UnityEditor;

namespace AiCharacterKit.Editor
{
    /// <summary>
    /// Resolves the movable Assets-relative installation root used by editor tools and tests.
    /// </summary>
    public static class AiCharacterKitAssetPaths
    {
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
                if (!candidateRoot.StartsWith(
                        "Assets/",
                        StringComparison.Ordinal)
                    && !string.Equals(
                        candidateRoot,
                        "Assets",
                        StringComparison.Ordinal))
                {
                    continue;
                }

                if (rootFolder.Length > 0
                    && !string.Equals(
                        rootFolder,
                        candidateRoot,
                        StringComparison.Ordinal))
                {
                    rootFolder = string.Empty;
                    error =
                        "Multiple AI Character Kit installations were found under Assets. "
                        + "Remove the duplicate before using editor automation.";
                    return false;
                }

                rootFolder = candidateRoot;
            }

            if (rootFolder.Length > 0)
            {
                return true;
            }

            error =
                "AI Character Kit could not locate Runtime/Core/AiCharacterKit.Core.asmdef under Assets.";
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
    }
}
