using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace AssemblyCSharpChineseInputSwitch
{
    public static class Helper
    {
        public static int Main(string[] args)
        {
            Dictionary<string, string> options = null;
            try
            {
                options = ParseOptions(args);
                var mode = Require(options, "mode");
                var parentPid = ParseInt(Require(options, "parent-pid"), "parent-pid");
                WaitForParent(parentPid);

                var live = Require(options, "live");
                var candidate = Require(options, "candidate");
                var backup = Require(options, "backup");
                var marker = Require(options, "marker");
                var candidateHash = Require(options, "candidate-hash").ToUpperInvariant();
                var originalHash = Require(options, "original-hash").ToUpperInvariant();

                if (string.Equals(mode, "activate-after-exit", StringComparison.OrdinalIgnoreCase))
                {
                    Activate(live, candidate, backup, marker, candidateHash, originalHash);
                }
                else if (string.Equals(mode, "restore-after-exit", StringComparison.OrdinalIgnoreCase))
                {
                    Restore(live, backup, marker, candidateHash);
                }
                else
                {
                    throw new InvalidOperationException("Unknown switch mode: " + mode);
                }

                WriteLog(marker, "completed mode=" + mode + ".");
                return 0;
            }
            catch (Exception exception)
            {
                var marker = options == null || !options.ContainsKey("marker")
                    ? null
                    : options["marker"];
                WriteLog(marker, "failed: " + exception);
                return 1;
            }
        }

        private static Dictionary<string, string> ParseOptions(string[] args)
        {
            var options = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            for (var index = 0; index < args.Length; index++)
            {
                var key = args[index];
                if (string.IsNullOrEmpty(key) || !key.StartsWith("--", StringComparison.Ordinal) ||
                    index + 1 >= args.Length)
                {
                    throw new ArgumentException("Invalid helper argument list.");
                }

                options[key.Substring(2)] = args[++index];
            }

            return options;
        }

        private static string Require(Dictionary<string, string> options, string key)
        {
            string value;
            if (!options.TryGetValue(key, out value) || string.IsNullOrEmpty(value))
            {
                throw new ArgumentException("Missing helper option --" + key + ".");
            }

            return value;
        }

        private static int ParseInt(string value, string name)
        {
            int result;
            if (!int.TryParse(value, out result) || result <= 0)
            {
                throw new ArgumentException("Invalid " + name + ".");
            }

            return result;
        }

        private static void WaitForParent(int parentPid)
        {
            try
            {
                using (var parent = Process.GetProcessById(parentPid))
                {
                    parent.WaitForExit();
                }
            }
            catch (ArgumentException)
            {
                // The game may have exited before the helper opened its process handle.
            }
        }

        private static void Activate(
            string live,
            string candidate,
            string backup,
            string marker,
            string candidateHash,
            string originalHash)
        {
            EnsureFile(live, "live Assembly-CSharp.dll");
            EnsureFile(candidate, "candidate Assembly-CSharp.dll");
            var actualCandidateHash = HashFile(candidate);
            if (!string.Equals(actualCandidateHash, candidateHash, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("The candidate DLL hash changed while the game was running.");
            }

            var currentHash = HashFile(live);
            if (string.Equals(currentHash, candidateHash, StringComparison.OrdinalIgnoreCase))
            {
                WriteMarker(marker, candidateHash, originalHash);
                return;
            }

            if (!File.Exists(backup))
            {
                throw new FileNotFoundException("The original DLL backup is missing.", backup);
            }

            if (!string.Equals(HashFile(backup), originalHash, StringComparison.OrdinalIgnoreCase) ||
                !string.Equals(currentHash, originalHash, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(
                    "The live DLL or original backup hash changed; refusing to replace the game assembly.");
            }

            ReplaceFile(candidate, live);
            WriteMarker(marker, candidateHash, originalHash);
        }

        private static void Restore(
            string live,
            string backup,
            string marker,
            string candidateHash)
        {
            EnsureFile(live, "live Assembly-CSharp.dll");
            EnsureFile(backup, "original Assembly-CSharp backup");
            var currentHash = HashFile(live);
            if (!string.Equals(currentHash, candidateHash, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(
                    "The live DLL is no longer the expected Chinese-input candidate; refusing to overwrite it.");
            }

            ReplaceFile(backup, live);
            if (File.Exists(marker))
            {
                File.Delete(marker);
            }
        }

        private static void ReplaceFile(string source, string destination)
        {
            var temporaryPath = destination + ".switch-" + Process.GetCurrentProcess().Id + ".tmp";
            var temporaryBackupPath = destination + ".switch-" + Process.GetCurrentProcess().Id + ".bak";
            if (File.Exists(temporaryPath))
            {
                File.Delete(temporaryPath);
            }
            if (File.Exists(temporaryBackupPath))
            {
                File.Delete(temporaryBackupPath);
            }

            try
            {
                File.Copy(source, temporaryPath, true);
                File.Replace(temporaryPath, destination, temporaryBackupPath, true);
            }
            finally
            {
                if (File.Exists(temporaryPath))
                {
                    File.Delete(temporaryPath);
                }
                if (File.Exists(temporaryBackupPath))
                {
                    File.Delete(temporaryBackupPath);
                }
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

        private static void WriteMarker(string marker, string candidateHash, string originalHash)
        {
            var directory = Path.GetDirectoryName(marker);
            Directory.CreateDirectory(directory);
            File.WriteAllText(
                marker,
                "candidateHash=" + candidateHash + Environment.NewLine +
                "originalHash=" + originalHash + Environment.NewLine,
                Encoding.UTF8);
        }

        private static void WriteLog(string marker, string message)
        {
            try
            {
                if (string.IsNullOrEmpty(marker))
                {
                    return;
                }

                var directory = Path.GetDirectoryName(marker);
                Directory.CreateDirectory(directory);
                File.AppendAllText(
                    Path.Combine(directory, "switch-helper.log"),
                    DateTime.UtcNow.ToString("o") + " " + message + Environment.NewLine,
                    Encoding.UTF8);
            }
            catch
            {
                // Logging must not change the file-safety decision.
            }
        }
    }
}
