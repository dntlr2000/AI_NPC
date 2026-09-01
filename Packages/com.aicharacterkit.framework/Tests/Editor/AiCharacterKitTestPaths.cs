using AiCharacterKit.Editor;

namespace AiCharacterKit.Core.Tests
{
    /// <summary>
    /// Resolves test assets relative to the movable AI Character Kit installation root.
    /// </summary>
    internal static class AiCharacterKitTestPaths
    {
        /// <summary>
        /// Returns one package-relative path for fixture and metadata tests.
        /// </summary>
        public static string Resolve(string relativePath)
        {
            return AiCharacterKitAssetPaths.Resolve(relativePath);
        }

        /// <summary>
        /// Returns one path relative to the writable imported or generated sample root.
        /// </summary>
        public static string ResolveSample(string relativePath)
        {
            return AiCharacterKitSamplePaths.Resolve(relativePath);
        }
    }
}
