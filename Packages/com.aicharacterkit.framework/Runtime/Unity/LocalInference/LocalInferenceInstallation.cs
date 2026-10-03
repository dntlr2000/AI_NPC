using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using AiCharacterKit.LocalInference;
using AiCharacterKit.Transport.LocalInference;
using UnityEngine;

namespace AiCharacterKit.Unity.LocalInference
{
    /// <summary>Verifies consumer-owned files; never downloads, installs, or edits model weights.</summary>
    public static class LocalInferenceInstallation
    {
        /// <summary>Reads a bounded manifest and verifies every declared file away from the main thread.</summary>
        public static async Task<LocalInstallManifest> LoadAsync(string manifestPath, CancellationToken cancellationToken)
        {
            try
            {
                manifestPath = RequireLocalPath(manifestPath, Environment.CurrentDirectory);
                var json = await Task.Run(() =>
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var info = new FileInfo(manifestPath);
                    if (!info.Exists || info.Length > 128 * 1024) throw LocalInferenceErrors.Configuration();
                    return File.ReadAllText(manifestPath);
                }, cancellationToken);
                var manifest = JsonUtility.FromJson<LocalInstallManifest>(json);
                await Task.Run(() => Verify(manifest, Path.GetDirectoryName(manifestPath), cancellationToken), cancellationToken);
                return manifest;
            }
            catch (OperationCanceledException) { throw; }
            catch { throw LocalInferenceErrors.Configuration(); }
        }

        /// <summary>Creates a digest record for the explicit installation setup tool.</summary>
        public static LocalInstallFile InspectFile(string path, CancellationToken cancellationToken)
        {
            path = RequireLocalPath(path, Environment.CurrentDirectory);
            var info = new FileInfo(path);
            if (!info.Exists || (info.Attributes & FileAttributes.ReparsePoint) != 0) throw LocalInferenceErrors.Configuration();
            using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                var digest = WindowsFileDigest.Compute(stream, cancellationToken);
                return new LocalInstallFile
                {
                    path = path, sizeBytes = info.Length,
                    sha256 = BitConverter.ToString(digest).Replace("-", string.Empty).ToLowerInvariant()
                };
            }
        }

        /// <summary>Resolves local paths relative to the installation manifest, excluding network locations.</summary>
        public static string RequireLocalPath(string path, string baseDirectory)
        {
            if (string.IsNullOrWhiteSpace(path) || path.IndexOf('"') >= 0) throw LocalInferenceErrors.Configuration();
            var fullPath = Path.GetFullPath(Path.IsPathRooted(path) ? path : Path.Combine(baseDirectory, path));
            if (fullPath.StartsWith("\\\\", StringComparison.Ordinal)) throw LocalInferenceErrors.Configuration();
            return fullPath;
        }

        /// <summary>Checks the measured model/runtime/template and all runtime DLL identities.</summary>
        private static void Verify(LocalInstallManifest manifest, string directory, CancellationToken cancellationToken)
        {
            if (manifest == null || manifest.schemaVersion != 1
                || manifest.runtimeVersion != LocalInferenceDefaults.RuntimeVersion
                || manifest.modelVersion != LocalInferenceDefaults.ModelVersion
                || manifest.templateVersion != LocalInferenceDefaults.TemplateVersion
                || manifest.runtimeFiles == null || manifest.runtimeFiles.Length == 0 || manifest.runtimeFiles.Length > 128)
                throw LocalInferenceErrors.Configuration();
            VerifyFile(manifest.model, directory, LocalInferenceDefaults.ModelSha256, cancellationToken);
            VerifyFile(manifest.executable, directory, LocalInferenceDefaults.ExecutableSha256, cancellationToken);
            VerifyFile(manifest.chatTemplate, directory, LocalInferenceDefaults.TemplateSha256, cancellationToken);
            var expectedDirectory = Path.GetDirectoryName(manifest.executable.path);
            var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var file in manifest.runtimeFiles)
            {
                VerifyFile(file, directory, null, cancellationToken);
                if (!string.Equals(Path.GetDirectoryName(file.path), expectedDirectory, StringComparison.OrdinalIgnoreCase)
                    || !names.Add(Path.GetFileName(file.path))) throw LocalInferenceErrors.Configuration();
            }
            foreach (var name in new[] { "llama-server-impl.dll", "llama.dll", "llama-common.dll", "ggml.dll", "ggml-base.dll", "ggml-vulkan.dll" })
                if (!names.Contains(name)) throw LocalInferenceErrors.Configuration();
        }

        /// <summary>Validates a file record and replaces its path with its resolved absolute location.</summary>
        private static void VerifyFile(LocalInstallFile file, string directory, string expectedHash, CancellationToken cancellationToken)
        {
            if (file == null || file.sizeBytes <= 0 || file.sha256 == null || file.sha256.Length != 64
                || (expectedHash != null && !string.Equals(file.sha256, expectedHash, StringComparison.OrdinalIgnoreCase)))
                throw LocalInferenceErrors.Configuration();
            file.path = RequireLocalPath(file.path, directory);
            if (!File.Exists(file.path) || new FileInfo(file.path).Length != file.sizeBytes) throw LocalInferenceErrors.Configuration();
            var actual = InspectFile(file.path, cancellationToken);
            if (!string.Equals(actual.sha256, file.sha256, StringComparison.OrdinalIgnoreCase)) throw LocalInferenceErrors.Configuration();
        }
    }
}
