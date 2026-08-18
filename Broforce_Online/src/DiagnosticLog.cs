using System;
using System.IO;
using System.Text;
using UnityEngine;
using UnityModManagerNet;

namespace BroforceOnlineDiagnostics
{
    internal static class DiagnosticLog
    {
        private static UnityModManager.ModEntry _modEntry;
        private static StreamWriter _writer;
        private static readonly object Sync = new object();

        public static string FilePath { get; private set; }

        public static void Initialize(UnityModManager.ModEntry modEntry)
        {
            _modEntry = modEntry;
            try
            {
                var directory = Path.Combine(Application.persistentDataPath, "BroforceOnlineDiagnostics");
                Directory.CreateDirectory(directory);
                FilePath = Path.Combine(directory, "diagnostics.log");
                _writer = new StreamWriter(FilePath, true);
                _writer.AutoFlush = true;
            }
            catch (Exception exception)
            {
                Debug.LogError(
                    "[BroforceOnlineDiagnostics] Cannot open diagnostic file: " +
                    SanitizeUtf16(exception.ToString()));
            }
        }

        public static void Info(string message)
        {
            Write("INFO", message);
        }

        public static void Warning(string message)
        {
            Write("WARN", message);
        }

        public static void Error(string message)
        {
            Write("ERROR", message);
        }

        public static void Close()
        {
            lock (Sync)
            {
                if (_writer == null)
                {
                    return;
                }

                _writer.Flush();
                _writer.Dispose();
                _writer = null;
            }
        }

        private static void Write(string level, string message)
        {
            var line = string.Format("[{0:O}] [{1}] {2}", DateTime.UtcNow, level, SanitizeUtf16(message));
            Debug.Log("[BroforceOnlineDiagnostics] " + line);
            try
            {
                lock (Sync)
                {
                    if (_writer != null)
                    {
                        _writer.WriteLine(line);
                    }
                }
            }
            catch (Exception exception)
            {
                Debug.LogError(
                    "[BroforceOnlineDiagnostics] Cannot write diagnostic file: " +
                    SanitizeUtf16(exception.ToString()));
            }
        }

        // Unity's native logging path rejects strings containing unpaired UTF-16 surrogates.
        private static string SanitizeUtf16(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return value ?? string.Empty;
            }

            var builder = new StringBuilder(value.Length);
            for (var index = 0; index < value.Length; index++)
            {
                var current = value[index];
                if (char.IsHighSurrogate(current))
                {
                    if (index + 1 < value.Length && char.IsLowSurrogate(value[index + 1]))
                    {
                        builder.Append(current);
                        builder.Append(value[++index]);
                    }
                    else
                    {
                        builder.Append("\\u");
                        builder.Append(((int)current).ToString("X4"));
                    }
                }
                else if (char.IsLowSurrogate(current))
                {
                    builder.Append("\\u");
                    builder.Append(((int)current).ToString("X4"));
                }
                else
                {
                    builder.Append(current);
                }
            }

            return builder.ToString();
        }
    }
}
