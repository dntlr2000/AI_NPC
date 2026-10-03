#if UNITY_EDITOR_WIN
using System;
using System.Diagnostics;
using System.IO;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using AiCharacterKit.Core;
using AiCharacterKit.LocalInference;
using AiCharacterKit.Transport.LocalInference;
using AiCharacterKit.Unity.LocalInference;
using AiCharacterKit.Unity.Networking;
using NUnit.Framework;
using UnityEngine;

namespace AiCharacterKit.Core.Tests
{
    /// <summary>Exercises external installation failures and actual Windows Job Object ownership without loading a model.</summary>
    public sealed class LocalInferenceInstallationTests
    {
        private string root;
        /// <summary>Creates one test-owned folder under the externally configured temporary root.</summary>
        [SetUp]
        public void SetUp()
        {
            root = Path.Combine(Path.GetTempPath(), "local-inference-한글 " + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
        }
        /// <summary>Deletes only the unique folder created by this test after all owned children have stopped.</summary>
        [TearDown]
        public void TearDown()
        {
            if (root != null && Directory.Exists(root)) Directory.Delete(root, true);
        }
        /// <summary>Confirms the native streaming digest matches SHA-256 across multiple file chunks and Unicode paths.</summary>
        [Test]
        public void Digest_MatchesReferenceAndSupportsCancellation()
        {
            var bytes = new byte[300001];
            new System.Random(42).NextBytes(bytes);
            var path = Path.Combine(root, "model file.bin");
            File.WriteAllBytes(path, bytes);
            var actual = LocalInferenceInstallation.InspectFile(path, CancellationToken.None);
            using (var reference = SHA256.Create())
                Assert.That(actual.sha256, Is.EqualTo(BitConverter.ToString(reference.ComputeHash(bytes)).Replace("-", "").ToLowerInvariant()));
            Assert.That(actual.sizeBytes, Is.EqualTo(bytes.Length));
            Assert.Throws<OperationCanceledException>(() => LocalInferenceInstallation.InspectFile(path, new CancellationToken(true)));
        }
        /// <summary>Rejects missing and malformed installations using the same safe public error.</summary>
        [TestCase(false)]
        [TestCase(true)]
        public async Task InstallationFailure_IsSafe(bool malformed)
        {
            var path = Path.Combine(root, "local.json");
            if (malformed) File.WriteAllText(path, JsonUtility.ToJson(new LocalInstallManifest { runtimeVersion = "WRONG PRIVATE" }));
            try { await LocalInferenceInstallation.LoadAsync(path, CancellationToken.None); Assert.Fail("Expected configuration rejection"); }
            catch (AiConversationException error)
            {
                Assert.That(error.Code, Is.EqualTo("local_configuration_error"));
                Assert.That(error.Message, Does.Not.Contain("PRIVATE"));
            }
        }
        /// <summary>Rejects a model with the expected declared digest but different bytes or size before starting a process.</summary>
        [TestCase(false)]
        [TestCase(true)]
        public async Task ModelIntegrityMismatch_IsRejected(bool wrongSize)
        {
            var file = Path.Combine(root, "synthetic.gguf");
            File.WriteAllBytes(file, new byte[] { 1, 2, 3 });
            var manifest = new LocalInstallManifest {
                runtimeVersion = LocalInferenceDefaults.RuntimeVersion, modelVersion = LocalInferenceDefaults.ModelVersion,
                templateVersion = LocalInferenceDefaults.TemplateVersion,
                model = new LocalInstallFile { path = "synthetic.gguf", sizeBytes = wrongSize ? 4 : 3, sha256 = LocalInferenceDefaults.ModelSha256 },
                runtimeFiles = new[] { new LocalInstallFile() } };
            var path = Path.Combine(root, "local.json");
            File.WriteAllText(path, JsonUtility.ToJson(manifest));
            try { await LocalInferenceInstallation.LoadAsync(path, CancellationToken.None); Assert.Fail("Expected integrity rejection"); }
            catch (AiConversationException error) { Assert.That(error.Code, Is.EqualTo("local_configuration_error")); }
        }
        /// <summary>Resolves the player override without file access and rejects ambiguous command lines.</summary>
        [Test]
        public void Profile_OverrideIsExplicitAndValidated()
        {
            var profile = ScriptableObject.CreateInstance<LocalInferenceProfile>();
            try
            {
                var file = Path.Combine(root, "settings.json");
                Assert.That(profile.ResolveManifestPath(new[] { "game", "-aiNpcLocalManifest", file }), Is.EqualTo(file));
                Assert.Throws<AiConversationException>(() => profile.ResolveManifestPath(new[] { "game", "-aiNpcLocalManifest" }));
                Assert.Throws<AiConversationException>(() => profile.ResolveManifestPath(new[] { "game", "-aiNpcLocalManifest", file, "-aiNpcLocalManifest", file }));
            }
            finally { UnityEngine.Object.DestroyImmediate(profile); }
        }
        /// <summary>Closing one private job terminates only its own process while another runner remains alive.</summary>
        [Test]
        public async Task JobObject_OwnershipIsIsolated()
        {
            var executable = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "WindowsPowerShell/v1.0/powershell.exe");
            using (var first = new WindowsLocalInferenceProcess())
            using (var second = new WindowsLocalInferenceProcess())
            {
                var arguments = new[] { "-NoProfile", "-NonInteractive", "-Command", "Start-Sleep -Seconds 30" };
                first.Start(executable, arguments, Path.Combine(root, "first"), "synthetic");
                second.Start(executable, arguments, Path.Combine(root, "second"), "synthetic");
                var firstId = first.ProcessId;
                first.Dispose();
                await Task.Delay(250);
                Assert.That(Alive(firstId), Is.False);
                Assert.That(second.HasExited, Is.False);
                var secondId = second.ProcessId;
                second.Dispose();
                await Task.Delay(250);
                Assert.That(Alive(secondId), Is.False);
            }
        }
        /// <summary>Stopping during deferred old-process cleanup cancels re-preparation before it can inspect or spawn a replacement.</summary>
        [Test]
        public async Task StopDuringCleanup_CancelsPreparationBeforeInstallationAccess()
        {
            var pendingExit = new TaskCompletionSource<bool>();
            var work = Path.Combine(root, "old-process-work");
            Directory.CreateDirectory(work);
            using (var runner = new UnityWebRequestLocalInferenceRunner(Path.Combine(root, "not-installed.json")))
            {
                var flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
                typeof(UnityWebRequestLocalInferenceRunner).GetField("processExit", flags).SetValue(runner, pendingExit.Task);
                typeof(UnityWebRequestLocalInferenceRunner).GetField("workDirectory", flags).SetValue(runner, work);
                var preparing = runner.PrepareAsync(CancellationToken.None);
                var stopping = runner.StopAsync();
                pendingExit.SetResult(true);
                try { await preparing; Assert.Fail("Expected preparation shutdown"); }
                catch (AiConversationException error) { Assert.That(error.Code, Is.EqualTo("local_runtime_unavailable")); }
                await stopping;
                Assert.That(runner.State, Is.EqualTo(LocalInferenceState.Stopped));
                Assert.That(runner.ProcessId, Is.Zero);
                Assert.That(Directory.Exists(work), Is.False);
            }
        }
        /// <summary>Checks only the explicit PID returned by a process created in this test.</summary>
        private static bool Alive(int pid)
        {
            try { using (var process = Process.GetProcessById(pid)) return !process.HasExited; }
            catch (ArgumentException) { return false; }
        }
    }
}
#endif
