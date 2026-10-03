using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using AiCharacterKit.LocalInference;
using Microsoft.Win32.SafeHandles;

namespace AiCharacterKit.Unity.LocalInference
{
    /// <summary>Owns one hidden child and a kill-on-close Windows job; never searches for processes by name.</summary>
    public sealed class WindowsLocalInferenceProcess : IDisposable
    {
        private Process process;
        private SafeFileHandle job;
        public int ProcessId { get; private set; }
        public Task Termination { get; private set; } = Task.CompletedTask;
        public bool HasExited => process == null || process.HasExited;

        /// <summary>Starts an explicit executable with bounded arguments and isolated transient directories.</summary>
        public void Start(string executable, IReadOnlyList<string> arguments, string directory, string credential)
        {
#if UNITY_EDITOR_WIN || UNITY_STANDALONE_WIN
            if (process != null) throw LocalInferenceErrors.Configuration();
            Directory.CreateDirectory(directory);
            var start = new ProcessStartInfo
            {
                FileName = executable, Arguments = JoinArguments(arguments), WorkingDirectory = directory,
                UseShellExecute = false, CreateNoWindow = true,
                RedirectStandardOutput = true, RedirectStandardError = true
            };
            var inherited = new List<string>();
            foreach (string key in start.EnvironmentVariables.Keys)
                if (key.StartsWith("LLAMA_", StringComparison.OrdinalIgnoreCase)) inherited.Add(key);
            foreach (var key in inherited) start.EnvironmentVariables.Remove(key);
            foreach (var key in new[] { "TEMP", "TMP", "LOCALAPPDATA", "APPDATA", "LLAMA_CACHE" })
            {
                var path = Path.Combine(directory, key.ToLowerInvariant());
                Directory.CreateDirectory(path);
                start.EnvironmentVariables[key] = path;
            }
            start.EnvironmentVariables["LLAMA_API_KEY"] = credential;
            start.EnvironmentVariables["__GL_SHADER_DISK_CACHE"] = "0";
            start.EnvironmentVariables["__GL_SHADER_DISK_CACHE_PATH"] = directory;
            try
            {
                job = CreateJobObject(IntPtr.Zero, null);
                if (job == null || job.IsInvalid) throw LocalInferenceErrors.Unavailable();
                var limits = new ExtendedLimits { BasicLimitInformation = new BasicLimits { LimitFlags = 0x2000 } };
                var size = Marshal.SizeOf<ExtendedLimits>();
                var pointer = Marshal.AllocHGlobal(size);
                try
                {
                    Marshal.StructureToPtr(limits, pointer, false);
                    if (!SetInformationJobObject(job, 9, pointer, (uint)size)) throw LocalInferenceErrors.Unavailable();
                }
                finally { Marshal.FreeHGlobal(pointer); }
                process = new Process { StartInfo = start };
                // Drain streams without persisting raw model output or native error messages.
                process.OutputDataReceived += DiscardOutput;
                process.ErrorDataReceived += DiscardOutput;
                if (!process.Start()) throw LocalInferenceErrors.Unavailable();
                ProcessId = process.Id;
                if (!AssignProcessToJobObject(job, process.Handle)) throw LocalInferenceErrors.Unavailable();
                process.BeginOutputReadLine();
                process.BeginErrorReadLine();
            }
            catch { Dispose(); throw LocalInferenceErrors.Unavailable(); }
#else
            throw LocalInferenceErrors.Configuration();
#endif
        }

        /// <summary>Quotes arguments using Windows backslash rules, including spaces and trailing separators.</summary>
        public static string JoinArguments(IReadOnlyList<string> arguments)
        {
            var result = new System.Text.StringBuilder();
            foreach (var argument in arguments)
            {
                if (result.Length > 0) result.Append(' ');
                result.Append('"');
                var slashes = 0;
                foreach (var c in argument)
                {
                    if (c == '\\') { slashes++; continue; }
                    result.Append('\\', c == '"' ? slashes * 2 + 1 : slashes);
                    result.Append(c);
                    slashes = 0;
                }
                result.Append('\\', slashes * 2);
                result.Append('"');
            }
            return result.ToString();
        }

        /// <summary>Discards redirected output so it cannot enter Unity logs or block the native process.</summary>
        private static void DiscardOutput(object sender, DataReceivedEventArgs args) { }

        /// <summary>Closes only this instance's job and child handles, also covering failed startup.</summary>
        public void Dispose()
        {
            job?.Dispose();
            job = null;
            if (process != null)
            {
                var owned = process;
                try { if (!owned.HasExited) owned.Kill(); } catch (InvalidOperationException) { }
                catch (System.ComponentModel.Win32Exception) { }
                process = null;
                Termination = Task.Run(() =>
                {
                    try { owned.WaitForExit(5000); }
                    catch (InvalidOperationException) { }
                    finally { owned.Dispose(); }
                });
            }
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct BasicLimits
        {
            public long PerProcessUserTimeLimit, PerJobUserTimeLimit;
            public uint LimitFlags;
            public UIntPtr MinimumWorkingSetSize, MaximumWorkingSetSize;
            public uint ActiveProcessLimit;
            public UIntPtr Affinity;
            public uint PriorityClass, SchedulingClass;
        }
        [StructLayout(LayoutKind.Sequential)]
        private struct IoCounters
        {
            public ulong ReadOperationCount, WriteOperationCount, OtherOperationCount;
            public ulong ReadTransferCount, WriteTransferCount, OtherTransferCount;
        }
        [StructLayout(LayoutKind.Sequential)]
        private struct ExtendedLimits
        {
            public BasicLimits BasicLimitInformation;
            public IoCounters IoInfo;
            public UIntPtr ProcessMemoryLimit, JobMemoryLimit, PeakProcessMemoryUsed, PeakJobMemoryUsed;
        }
        /// <summary>Creates a non-inheritable job handle owned by this runner.</summary>
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern SafeFileHandle CreateJobObject(IntPtr attributes, string name);
        /// <summary>Enables process termination when the final owned job handle closes.</summary>
        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool SetInformationJobObject(SafeFileHandle job, int informationClass, IntPtr information, uint length);
        /// <summary>Associates only the newly created process with the private lifetime job.</summary>
        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool AssignProcessToJobObject(SafeFileHandle job, IntPtr process);
    }
}
