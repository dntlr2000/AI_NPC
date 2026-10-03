using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using AiCharacterKit.LocalInference;
using AiCharacterKit.Transport.LocalInference;
using AiCharacterKit.Unity.LocalInference;
using UnityEditor;
using UnityEngine;

namespace AiCharacterKit.Editor
{
    /// <summary>Creates a manifest from existing external files without downloading or copying model weights.</summary>
    public sealed class LocalInferenceSetupWindow : EditorWindow
    {
        [SerializeField] private string modelPath = string.Empty;
        [SerializeField] private string executablePath = string.Empty;
        [SerializeField] private string templateSource = string.Empty;
        private bool preparing;
        private string status = string.Empty;

        /// <summary>Opens the explicit local installation setup tool.</summary>
        [MenuItem("Tools/AI Character Kit/Local Inference/Prepare Installation")]
        public static void Open() { GetWindow<LocalInferenceSetupWindow>("Local NPC Setup"); }

        /// <summary>Draws only file selection and manifest creation, never a model download control.</summary>
        private void OnGUI()
        {
            EditorGUILayout.HelpBox("Select the measured Gemma Q4_K_M file, llama.cpp b11146 Vulkan executable, and its LM Studio model.yaml or extracted .jinja template. Model weights remain in place.", MessageType.Info);
            using (new EditorGUI.DisabledScope(preparing))
            {
                modelPath = DrawFile("Gemma GGUF", modelPath, "gguf");
                executablePath = DrawFile("llama-server.exe", executablePath, "exe");
                templateSource = DrawFile("Template / model.yaml", templateSource, string.Empty);
                if (GUILayout.Button("Verify files and create manifest")) _ = CreateFromSelectionAsync();
            }
            EditorGUILayout.HelpBox(status.Length == 0 ? "No installation has been prepared." : status, MessageType.None);
        }

        /// <summary>Lets the author choose an existing file without modifying it.</summary>
        private static string DrawFile(string label, string value, string extension)
        {
            EditorGUILayout.BeginHorizontal();
            value = EditorGUILayout.TextField(label, value);
            if (GUILayout.Button("Browse", GUILayout.Width(65)))
            {
                var selected = EditorUtility.OpenFilePanel(label, string.Empty, extension);
                if (selected.Length > 0) value = selected;
            }
            EditorGUILayout.EndHorizontal();
            return value;
        }

        /// <summary>Runs explicit setup asynchronously and reports only fixed diagnostic categories.</summary>
        private async Task CreateFromSelectionAsync()
        {
            var destination = EditorUtility.SaveFilePanel("Create external local inference manifest", string.Empty, "local-inference", "json");
            if (destination.Length == 0) return;
            preparing = true;
            status = "Checking file hashes…";
            try
            {
                await CreateManifestAsync(modelPath, executablePath, templateSource, destination, CancellationToken.None);
                status = "Installation verified. Assign this manifest to a LocalInferenceProfile.";
            }
            catch { status = "Installation could not be verified. Check the pinned versions, template and file permissions."; }
            finally { preparing = false; Repaint(); }
        }

        /// <summary>Inspects the approved files and writes only a derived template and explicit installation manifest.</summary>
        public static async Task<LocalInstallManifest> CreateManifestAsync(string model, string executable,
            string template, string destination, CancellationToken cancellationToken)
        {
            destination = LocalInferenceInstallation.RequireLocalPath(destination, Environment.CurrentDirectory);
            if (!string.Equals(Path.GetExtension(destination), ".json", StringComparison.OrdinalIgnoreCase))
                throw LocalInferenceErrors.Configuration();
            var directory = Path.GetDirectoryName(destination);
            var manifest = await Task.Run(() =>
            {
                var modelRecord = LocalInferenceInstallation.InspectFile(model, cancellationToken);
                var executableRecord = LocalInferenceInstallation.InspectFile(executable, cancellationToken);
                if (modelRecord.sha256 != LocalInferenceDefaults.ModelSha256
                    || executableRecord.sha256 != LocalInferenceDefaults.ExecutableSha256) throw LocalInferenceErrors.Configuration();
                var templateText = ExtractTemplate(template);
                using (var hash = SHA256.Create())
                {
                    var digest = BitConverter.ToString(hash.ComputeHash(Encoding.UTF8.GetBytes(templateText)))
                        .Replace("-", string.Empty).ToLowerInvariant();
                    if (digest != LocalInferenceDefaults.TemplateSha256) throw LocalInferenceErrors.Configuration();
                }
                Directory.CreateDirectory(directory);
                var templateDestination = Path.Combine(directory, "gemma-chat-template.jinja");
                if (File.Exists(templateDestination))
                {
                    if (LocalInferenceInstallation.InspectFile(templateDestination, cancellationToken).sha256
                        != LocalInferenceDefaults.TemplateSha256) throw LocalInferenceErrors.Configuration();
                }
                else File.WriteAllText(templateDestination, templateText, new UTF8Encoding(false));
                var files = new List<LocalInstallFile>();
                foreach (var dll in Directory.GetFiles(Path.GetDirectoryName(executableRecord.path), "*.dll"))
                    files.Add(LocalInferenceInstallation.InspectFile(dll, cancellationToken));
                files.Sort((a, b) => string.CompareOrdinal(a.path, b.path));
                var created = new LocalInstallManifest
                {
                    runtimeVersion = LocalInferenceDefaults.RuntimeVersion,
                    modelVersion = LocalInferenceDefaults.ModelVersion,
                    templateVersion = LocalInferenceDefaults.TemplateVersion,
                    model = modelRecord, executable = executableRecord,
                    chatTemplate = LocalInferenceInstallation.InspectFile(templateDestination, cancellationToken),
                    runtimeFiles = files.ToArray()
                };
                MakeRelative(created.model, directory);
                MakeRelative(created.executable, directory);
                MakeRelative(created.chatTemplate, directory);
                foreach (var file in created.runtimeFiles) MakeRelative(file, directory);
                return created;
            }, cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            var pending = Path.Combine(directory, ".local-install-" + Guid.NewGuid().ToString("N") + ".json");
            try
            {
                File.WriteAllText(pending, JsonUtility.ToJson(manifest, true), new UTF8Encoding(false));
                await LocalInferenceInstallation.LoadAsync(pending, cancellationToken);
                cancellationToken.ThrowIfCancellationRequested();
                if (File.Exists(destination)) File.Replace(pending, destination, null);
                else File.Move(pending, destination);
            }
            finally { if (File.Exists(pending)) File.Delete(pending); }
            return manifest;
        }

        /// <summary>Extracts the measured LM Studio YAML template or reads an already extracted Jinja file.</summary>
        private static string ExtractTemplate(string path)
        {
            if (!string.Equals(Path.GetExtension(path), ".yaml", StringComparison.OrdinalIgnoreCase)
                && !string.Equals(Path.GetExtension(path), ".yml", StringComparison.OrdinalIgnoreCase))
                return File.ReadAllText(path);
            var lines = new List<string>();
            var collecting = false;
            foreach (var line in File.ReadAllLines(path))
            {
                if (line == "            template: |") { collecting = true; continue; }
                if (!collecting) continue;
                if (line.Length > 0 && !line.StartsWith("              ", StringComparison.Ordinal)) break;
                lines.Add(line.Length == 0 ? string.Empty : line.Substring(14));
            }
            if (lines.Count < 10) throw LocalInferenceErrors.Configuration();
            return string.Join("\n", lines);
        }

        /// <summary>Keeps files beside the manifest portable while leaving externally owned model paths explicit.</summary>
        private static void MakeRelative(LocalInstallFile file, string directory)
        {
            if (string.Equals(Path.GetDirectoryName(file.path), directory, StringComparison.OrdinalIgnoreCase))
                file.path = Path.GetFileName(file.path);
        }
    }
}
