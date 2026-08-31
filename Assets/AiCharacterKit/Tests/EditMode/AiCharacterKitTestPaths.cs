using AiCharacterKit.Editor;

namespace AiCharacterKit.Core.Tests
{
    /// <summary>
    /// Resolves test assets relative to the movable AI Character Kit installation root.
    /// </summary>
    internal static class AiCharacterKitTestPaths
    {
        /// <summary>
        /// Returns one kit-relative path for fixture and sample configuration tests.
        /// </summary>
        public static string Resolve(string relativePath)
        {
            return AiCharacterKitAssetPaths.Resolve(relativePath);
        }
    }
}
