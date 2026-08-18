using System;
using System.IO;
using System.Text;
using UnityEngine;
using UnityModManagerNet;

namespace BroforceOnlineDiagnostics
{
    internal static class DiagnosticLog
    {
        private const int FlushIntervalMilliseconds = 750;

        private static UnityModManager.ModEntry _modEntry;
        private static StreamWriter _eventWriter;
        private static StreamWriter _traceWriter;
        private static readonly object Sync = new object();
        private static string _directory;
        private static DateTime _sessionStartedUtc;
        private static DateTime _nextFlushAtUtc;
        private static bool _sessionActive;

        public static string FilePath { get; private set; }
        public static string TraceFilePath { get; private set; }
        public static string SessionId { get; private set; }
        public static string Role { get; private set; }

        public static void Initialize(UnityModManager.ModEntry modEntry)
        {
            _modEntry = modEntry;
            try
            {
                _directory = Path.Combine(Application.persistentDataPath, "BroforceOnlineDiagnostics");
                Directory.CreateDirectory(_directory);
                lock (Sync)
                {
                    OpenSessionLocked("startup", "startup");
                    WriteLineLocked("INFO", "SESSION_BEGIN trigger=plugin-load", false);
                }
            }
            catch (Exception exception)
            {
                Debug.LogError(
                    "[BroforceOnlineDiagnostics] Cannot open diagnostic file: " +
                    SanitizeUtf16(exception.ToString()));
            }
        }

        public static void BeginSession(string trigger, string inferredRole)
        {
            lock (Sync)
            {
                if (_sessionActive)
                {
                    return;
                }

                try
                {
                    OpenSessionLocked(trigger, inferredRole);
                    WriteLineLocked(
                        "INFO",
                        "SESSION_BEGIN trigger=" + SanitizeToken(trigger) +
                        "; sessionId=" + SanitizeToken(SessionId) +
                        "; role=" + SanitizeToken(Role) +
                        "; networkRole=" + SanitizeToken(inferredRole),
                        false);
                }
                catch (Exception exception)
                {
                    Debug.LogError(
                        "[BroforceOnlineDiagnostics] Cannot start diagnostic session: " +
                        SanitizeUtf16(exception.ToString()));
                }
            }
        }

        public static void EndSession(string reason)
        {
            lock (Sync)
            {
                if (!_sessionActive)
                {
                    return;
                }

                try
                {
                    WriteLineLocked("INFO", "SESSION_END reason=" + SanitizeToken(reason), false);
                    FlushLocked();
                }
                catch (Exception exception)
                {
                    Debug.LogError(
                        "[BroforceOnlineDiagnostics] Cannot finish diagnostic session: " +
                        SanitizeUtf16(exception.ToString()));
                }
                finally
                {
                    CloseWritersLocked();
                    _sessionActive = false;
                }
            }
        }

        public static void Info(string message)
        {
            Write("INFO", message, false, true);
        }

        public static void Warning(string message)
        {
            Write("WARN", message, false, true);
        }

        public static void Error(string message)
        {
            Write("ERROR", message, false, true);
        }

        public static void Trace(string message)
        {
            Write("TRACE", message, true, false);
        }

        public static void Close()
        {
            lock (Sync)
            {
                if (_eventWriter == null && _traceWriter == null)
                {
                    return;
                }

                try
                {
                    if (_eventWriter != null)
                    {
                        WriteLineLocked("INFO", "SESSION_END reason=logger closed", false);
                    }

                    FlushLocked();
                }
                catch (Exception exception)
                {
                    Debug.LogError(
                        "[BroforceOnlineDiagnostics] Cannot close diagnostic file: " +
                        SanitizeUtf16(exception.ToString()));
                }
                finally
                {
                    CloseWritersLocked();
                    _sessionActive = false;
                    _modEntry = null;
                }
            }
        }

        private static void Write(string level, string message, bool trace, bool writeToUnity)
        {
            var safeMessage = SanitizeUtf16(message);
            var line = string.Empty;
            try
            {
                lock (Sync)
                {
                    line = FormatLine(level, safeMessage);
                    WriteLineLocked(level, safeMessage, trace);
                }

                if (writeToUnity)
                {
                    Debug.Log("[BroforceOnlineDiagnostics] " + line);
                }
            }
            catch (Exception exception)
            {
                Debug.LogError(
                    "[BroforceOnlineDiagnostics] Cannot write diagnostic file: " +
                    SanitizeUtf16(exception.ToString()));
            }
        }

        private static void OpenSessionLocked(string trigger, string inferredRole)
        {
            if (_eventWriter != null || _traceWriter != null)
            {
                WriteLineLocked("INFO", "SESSION_END reason=new session", false);
                FlushLocked();
            }

            CloseWritersLocked();

            var now = DateTime.UtcNow;
            var configuredId = GetConfiguredValue("session");
            var sessionId = string.IsNullOrEmpty(configuredId)
                ? "auto-" + now.ToString("yyyyMMdd-HHmmss-fff") + "-" + Guid.NewGuid().ToString("N").Substring(0, 8)
                : configuredId;
            var configuredRole = GetConfiguredValue("role").ToLowerInvariant();
            var role = configuredRole == "host" || configuredRole == "client"
                ? configuredRole
                : (string.IsNullOrEmpty(inferredRole) ? "unknown" : inferredRole.ToLowerInvariant());
            var stamp = now.ToString("yyyyMMdd-HHmmss-fff");
            var stem = "diagnostics-" + SanitizeToken(role) + "-" + SanitizeToken(sessionId) + "-" + stamp;
            var eventPath = Path.Combine(_directory, stem + ".log");
            var tracePath = Path.Combine(_directory, stem + ".trace.log");

            _eventWriter = new StreamWriter(eventPath, false, new UTF8Encoding(false));
            _traceWriter = new StreamWriter(tracePath, false, new UTF8Encoding(false));
            _eventWriter.AutoFlush = false;
            _traceWriter.AutoFlush = false;
            FilePath = eventPath;
            TraceFilePath = tracePath;
            SessionId = sessionId;
            Role = role;
            _sessionStartedUtc = now;
            _nextFlushAtUtc = now.AddMilliseconds(FlushIntervalMilliseconds);
            _sessionActive = !string.Equals(trigger, "startup", StringComparison.Ordinal);
        }

        private static string GetConfiguredValue(string kind)
        {
            var settings = Plugin.Settings;
            if (settings == null)
            {
                return string.Empty;
            }

            var value = kind == "session" ? settings.DiagnosticSessionId : settings.DiagnosticRole;
            return value == null ? string.Empty : value.Trim();
        }

        private static void WriteLineLocked(string level, string message, bool trace)
        {
            var writer = trace ? _traceWriter : _eventWriter;
            if (writer == null)
            {
                return;
            }

            writer.WriteLine(FormatLine(level, message));
            var now = DateTime.UtcNow;
            if (level == "WARN" || level == "ERROR" || now >= _nextFlushAtUtc)
            {
                FlushLocked();
            }
        }

        private static string FormatLine(string level, string message)
        {
            var elapsed = DateTime.UtcNow - _sessionStartedUtc;
            return string.Format(
                "[{0:O}] [+{1:0.000}s] [session={2}] [role={3}] [{4}] {5}",
                DateTime.UtcNow,
                elapsed.TotalSeconds < 0d ? 0d : elapsed.TotalSeconds,
                SanitizeToken(SessionId),
                SanitizeToken(Role),
                level,
                message ?? string.Empty);
        }

        private static void FlushLocked()
        {
            if (_eventWriter != null)
            {
                _eventWriter.Flush();
            }

            if (_traceWriter != null)
            {
                _traceWriter.Flush();
            }

            _nextFlushAtUtc = DateTime.UtcNow.AddMilliseconds(FlushIntervalMilliseconds);
        }

        private static void CloseWritersLocked()
        {
            if (_eventWriter != null)
            {
                _eventWriter.Dispose();
                _eventWriter = null;
            }

            if (_traceWriter != null)
            {
                _traceWriter.Dispose();
                _traceWriter = null;
            }
        }

        private static string SanitizeToken(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return "unknown";
            }

            var builder = new StringBuilder();
            for (var index = 0; index < value.Length; index++)
            {
                var current = value[index];
                if ((current >= 'a' && current <= 'z') ||
                    (current >= 'A' && current <= 'Z') ||
                    (current >= '0' && current <= '9') ||
                    current == '-' || current == '_')
                {
                    builder.Append(current);
                }
                else
                {
                    builder.Append('_');
                }
            }

            return builder.Length == 0 ? "unknown" : builder.ToString();
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
