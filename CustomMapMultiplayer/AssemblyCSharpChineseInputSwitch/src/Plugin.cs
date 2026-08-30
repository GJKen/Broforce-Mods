using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Security.Cryptography;
using UnityEngine;
using UnityModManagerNet;

namespace AssemblyCSharpChineseInputSwitch
{
    public static class Plugin
    {
        private const string CandidateRelativePath = "payload\\Assembly-CSharp-ChineseInput.candidate.dll";
        private const string HelperRelativePath = "tools\\AssemblyCSharpChineseInputSwitch.Helper.exe";
        private static ModPaths _paths;
        private static UnityModManager.ModEntry _modEntry;
        private static bool _scheduled;

        public static bool Load(UnityModManager.ModEntry modEntry)
        {
            try
            {
                _modEntry = modEntry;
                modEntry.OnToggle = OnToggle;
                modEntry.OnUnload = OnUnload;
                _paths = ModPaths.Create(modEntry.Path, Application.dataPath);
                modEntry.Logger.Log(
                    "Loaded. The Assembly-CSharp switch is scheduled only when this Mod is enabled; " +
                    "replacement is applied after the current game process exits.");
                return true;
            }
            catch (Exception exception)
            {
                modEntry.Logger.LogException("Assembly-CSharp switcher Load failed", exception);
                return false;
            }
        }

        private static bool OnToggle(UnityModManager.ModEntry modEntry, bool enabled)
        {
            if (!enabled)
            {
                modEntry.Logger.Log(
                    "Disabled. Any already-running switch helper will finish its guarded restore or activation action.");
                return true;
            }

            if (_scheduled)
            {
                return true;
            }

            try
            {
                ScheduleSwitch();
                _scheduled = true;
                return true;
            }
            catch (Exception exception)
            {
                modEntry.Logger.LogException("Assembly-CSharp switch scheduling failed", exception);
                return false;
            }
        }

        private static bool OnUnload(UnityModManager.ModEntry modEntry)
        {
            modEntry.Logger.Log(
                "Unloaded. The helper process, if already started, remains responsible for its guarded file action.");
            _modEntry = null;
            _paths = null;
            return true;
        }

        private static void ScheduleSwitch()
        {
            if (_paths == null)
            {
                throw new InvalidOperationException("Mod paths are not initialized.");
            }

            EnsureFile(_paths.LiveAssemblyPath, "live Assembly-CSharp.dll");
            EnsureFile(_paths.CandidateAssemblyPath, "Chinese-input candidate DLL");
            EnsureFile(_paths.HelperPath, "switch helper");
            ValidateAssembly(_paths.LiveAssemblyPath, "live Assembly-CSharp.dll");
            ValidateAssembly(_paths.CandidateAssemblyPath, "candidate Assembly-CSharp.dll");

            var candidateHash = HashFile(_paths.CandidateAssemblyPath);
            var liveHash = HashFile(_paths.LiveAssemblyPath);
            Directory.CreateDirectory(_paths.StateDirectory);

            if (string.Equals(liveHash, candidateHash, StringComparison.OrdinalIgnoreCase))
            {
                EnsureFile(_paths.OriginalBackupPath, "original Assembly-CSharp backup");
                var originalHash = HashFile(_paths.OriginalBackupPath);
                StartHelper(
                    "restore-after-exit",
                    candidateHash,
                    originalHash);
                Log(
                    "Chinese-input Assembly-CSharp is active for this session; " +
                    "the original DLL will be restored after the game exits.");
                return;
            }

            var originalHashForAction = PrepareOriginalBackup(liveHash);
            StartHelper(
                "activate-after-exit",
                candidateHash,
                originalHashForAction);
            Log(
                "The current session already loaded the original Assembly-CSharp.dll. " +
                "The Chinese-input DLL is queued for the next launch; exit the game and start it again.");
        }

        private static string PrepareOriginalBackup(string liveHash)
        {
            if (File.Exists(_paths.OriginalBackupPath))
            {
                var existingHash = HashFile(_paths.OriginalBackupPath);
                if (!string.Equals(existingHash, liveHash, StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidOperationException(
                        "The live Assembly-CSharp.dll does not match the saved original backup; refusing to overwrite either file.");
                }

                return existingHash;
            }

            File.Copy(_paths.LiveAssemblyPath, _paths.OriginalBackupPath, false);
            return HashFile(_paths.OriginalBackupPath);
        }

        private static void StartHelper(string mode, string candidateHash, string originalHash)
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = _paths.HelperPath,
                WorkingDirectory = Path.GetDirectoryName(_paths.HelperPath),
                UseShellExecute = false,
                CreateNoWindow = true,
                WindowStyle = ProcessWindowStyle.Hidden,
                Arguments =
                    "--mode " + Quote(mode) +
                    " --parent-pid " + Process.GetCurrentProcess().Id +
                    " --live " + Quote(_paths.LiveAssemblyPath) +
                    " --candidate " + Quote(_paths.CandidateAssemblyPath) +
                    " --backup " + Quote(_paths.OriginalBackupPath) +
                    " --marker " + Quote(_paths.ActiveMarkerPath) +
                    " --candidate-hash " + Quote(candidateHash) +
                    " --original-hash " + Quote(originalHash)
            };

            var helper = Process.Start(startInfo);
            if (helper == null)
            {
                throw new InvalidOperationException("The switch helper process could not be started.");
            }

            helper.Dispose();
        }

        private static void ValidateAssembly(string path, string description)
        {
            var assemblyName = AssemblyName.GetAssemblyName(path);
            if (!string.Equals(assemblyName.Name, "Assembly-CSharp", StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    description + " has assembly name '" + assemblyName.Name + "', expected 'Assembly-CSharp'.");
            }
        }

        private static string HashFile(string path)
        {
            using (var algorithm = SHA256.Create())
            using (var stream = File.OpenRead(path))
            {
                var hash = algorithm.ComputeHash(stream);
                return BitConverter.ToString(hash).Replace("-", string.Empty).ToUpperInvariant();
            }
        }

        private static void EnsureFile(string path, string description)
        {
            if (!File.Exists(path))
            {
                throw new FileNotFoundException("Missing " + description + ".", path);
            }
        }

        private static string Quote(string value)
        {
            return "\"" + value.Replace("\"", "\\\"") + "\"";
        }

        private static void Log(string message)
        {
            if (_modEntry != null)
            {
                _modEntry.Logger.Log(message);
            }
        }

        private sealed class ModPaths
        {
            internal string StateDirectory;
            internal string LiveAssemblyPath;
            internal string CandidateAssemblyPath;
            internal string OriginalBackupPath;
            internal string ActiveMarkerPath;
            internal string HelperPath;

            internal static ModPaths Create(string modPath, string dataPath)
            {
                if (string.IsNullOrEmpty(modPath) || string.IsNullOrEmpty(dataPath))
                {
                    throw new InvalidOperationException("UMM mod path or Unity data path is empty.");
                }

                var stateDirectory = Path.Combine(modPath, "state");
                var managedDirectory = Path.Combine(dataPath, "Managed");
                return new ModPaths
                {
                    StateDirectory = stateDirectory,
                    LiveAssemblyPath = Path.Combine(managedDirectory, "Assembly-CSharp.dll"),
                    CandidateAssemblyPath = Path.Combine(modPath, CandidateRelativePath),
                    OriginalBackupPath = Path.Combine(stateDirectory, "Assembly-CSharp.original.bak"),
                    ActiveMarkerPath = Path.Combine(stateDirectory, "active.marker"),
                    HelperPath = Path.Combine(modPath, HelperRelativePath)
                };
            }
        }
    }
}
