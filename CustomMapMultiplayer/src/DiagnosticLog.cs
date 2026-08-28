using System;
using System.IO;
using System.Text;
using UnityEngine;
using UnityModManagerNet;

namespace CustomMapMultiplayer
{
    [Flags]
    internal enum DiagnosticLogCategory
    {
        LobbyAndNetwork = 1,
        Workshop = 2,
        PlayerLifecycle = 4,
        Afk = 8,
        LevelOutcome = 16,
        WorkshopObjects = 32,
        FrpDirect = 64,
        OptionalMod = 128,
        HarmonyTrace = 256
    }

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

        internal static DiagnosticLogCategory AllCategories
        {
            get
            {
                return DiagnosticLogCategory.LobbyAndNetwork |
                       DiagnosticLogCategory.Workshop |
                       DiagnosticLogCategory.PlayerLifecycle |
                       DiagnosticLogCategory.Afk |
                       DiagnosticLogCategory.LevelOutcome |
                       DiagnosticLogCategory.WorkshopObjects |
                       DiagnosticLogCategory.FrpDirect |
                       DiagnosticLogCategory.OptionalMod |
                       DiagnosticLogCategory.HarmonyTrace;
            }
        }

        public static void Initialize(UnityModManager.ModEntry modEntry)
        {
            _modEntry = modEntry;
            EnsureCategoryDefaults();
            try
            {
                _directory = GetLogDirectory();
                Directory.CreateDirectory(_directory);
                lock (Sync)
                {
                    OpenSessionLocked("startup", "startup");
                    WriteLineLocked(
                        "INFO",
                        "SESSION_BEGIN trigger=plugin-load; buildHash=" + BuildMetadata.BuildHash,
                        false);
                }
            }
            catch (Exception exception)
            {
                Debug.LogError(
                    "[CustomMapMultiplayer] Cannot open diagnostic file: " +
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
                        "; networkRole=" + SanitizeToken(inferredRole) +
                        "; buildHash=" + BuildMetadata.BuildHash,
                        false);
                }
                catch (Exception exception)
                {
                    Debug.LogError(
                        "[CustomMapMultiplayer] Cannot start diagnostic session: " +
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
                        "[CustomMapMultiplayer] Cannot finish diagnostic session: " +
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

        internal static bool IsCategoryEnabled(DiagnosticLogCategory category)
        {
            var settings = Plugin.Settings;
            if (settings == null)
            {
                return true;
            }

            switch (category)
            {
                case DiagnosticLogCategory.LobbyAndNetwork:
                    return settings.LogLobbyAndNetwork;
                case DiagnosticLogCategory.Workshop:
                    return settings.LogWorkshop;
                case DiagnosticLogCategory.PlayerLifecycle:
                    return settings.LogPlayerLifecycle;
                case DiagnosticLogCategory.Afk:
                    return settings.LogAfk;
                case DiagnosticLogCategory.LevelOutcome:
                    return settings.LogLevelOutcome;
                case DiagnosticLogCategory.WorkshopObjects:
                    return settings.LogWorkshopObjects;
                case DiagnosticLogCategory.FrpDirect:
                    return settings.LogFrpDirect;
                case DiagnosticLogCategory.OptionalMod:
                    return settings.LogOptionalMod;
                case DiagnosticLogCategory.HarmonyTrace:
                    return settings.LogHarmonyTrace;
                default:
                    return true;
            }
        }

        internal static void SetCategoryEnabled(DiagnosticLogCategory category, bool enabled)
        {
            var settings = Plugin.Settings;
            if (settings == null)
            {
                return;
            }

            switch (category)
            {
                case DiagnosticLogCategory.LobbyAndNetwork:
                    settings.LogLobbyAndNetwork = enabled;
                    break;
                case DiagnosticLogCategory.Workshop:
                    settings.LogWorkshop = enabled;
                    break;
                case DiagnosticLogCategory.PlayerLifecycle:
                    settings.LogPlayerLifecycle = enabled;
                    break;
                case DiagnosticLogCategory.Afk:
                    settings.LogAfk = enabled;
                    break;
                case DiagnosticLogCategory.LevelOutcome:
                    settings.LogLevelOutcome = enabled;
                    break;
                case DiagnosticLogCategory.WorkshopObjects:
                    settings.LogWorkshopObjects = enabled;
                    break;
                case DiagnosticLogCategory.FrpDirect:
                    settings.LogFrpDirect = enabled;
                    break;
                case DiagnosticLogCategory.OptionalMod:
                    settings.LogOptionalMod = enabled;
                    break;
                case DiagnosticLogCategory.HarmonyTrace:
                    settings.LogHarmonyTrace = enabled;
                    break;
            }

            settings.DiagnosticLogPreset = "custom";
        }

        internal static void ApplyPreset(string preset)
        {
            var settings = Plugin.Settings;
            if (settings == null)
            {
                return;
            }

            var normalized = (preset ?? string.Empty).Trim().ToLowerInvariant();
            var categories = DiagnosticLogCategory.LobbyAndNetwork;
            switch (normalized)
            {
                case "basic":
                    categories = DiagnosticLogCategory.LobbyAndNetwork |
                                 DiagnosticLogCategory.PlayerLifecycle;
                    break;
                case "join":
                case "rejoin":
                    categories = DiagnosticLogCategory.LobbyAndNetwork |
                                 DiagnosticLogCategory.Workshop |
                                 DiagnosticLogCategory.PlayerLifecycle;
                    break;
                case "afk":
                    categories = DiagnosticLogCategory.LobbyAndNetwork |
                                 DiagnosticLogCategory.PlayerLifecycle |
                                 DiagnosticLogCategory.Afk |
                                 DiagnosticLogCategory.LevelOutcome;
                    break;
                case "workshop":
                    categories = DiagnosticLogCategory.LobbyAndNetwork |
                                 DiagnosticLogCategory.Workshop |
                                 DiagnosticLogCategory.WorkshopObjects |
                                 DiagnosticLogCategory.PlayerLifecycle;
                    break;
                case "full":
                case "complete":
                    categories = AllCategories;
                    normalized = "full";
                    break;
                default:
                    categories = AllCategories;
                    normalized = "full";
                    break;
            }

            SetCategories(settings, categories);
            settings.DiagnosticLogPreset = normalized;
        }

        internal static string GetEnabledCategoryList()
        {
            var enabled = new StringBuilder();
            var categories = new[]
            {
                DiagnosticLogCategory.LobbyAndNetwork,
                DiagnosticLogCategory.Workshop,
                DiagnosticLogCategory.PlayerLifecycle,
                DiagnosticLogCategory.Afk,
                DiagnosticLogCategory.LevelOutcome,
                DiagnosticLogCategory.WorkshopObjects,
                DiagnosticLogCategory.FrpDirect,
                DiagnosticLogCategory.OptionalMod,
                DiagnosticLogCategory.HarmonyTrace
            };
            foreach (var category in categories)
            {
                if (!IsCategoryEnabled(category))
                {
                    continue;
                }

                if (enabled.Length > 0)
                {
                    enabled.Append(",");
                }

                enabled.Append(GetCategoryKey(category));
            }

            return enabled.Length == 0 ? "none" : enabled.ToString();
        }

        internal static string GetCategoryKey(DiagnosticLogCategory category)
        {
            switch (category)
            {
                case DiagnosticLogCategory.LobbyAndNetwork:
                    return "lobby-network";
                case DiagnosticLogCategory.Workshop:
                    return "workshop";
                case DiagnosticLogCategory.PlayerLifecycle:
                    return "player-lifecycle";
                case DiagnosticLogCategory.Afk:
                    return "afk-dropout";
                case DiagnosticLogCategory.LevelOutcome:
                    return "level-outcome";
                case DiagnosticLogCategory.WorkshopObjects:
                    return "workshop-objects";
                case DiagnosticLogCategory.FrpDirect:
                    return "frp-direct";
                case DiagnosticLogCategory.OptionalMod:
                    return "optional-mod";
                case DiagnosticLogCategory.HarmonyTrace:
                    return "harmony-trace";
                default:
                    return "unknown";
            }
        }

        internal static bool TryOpenDirectory(out string error)
        {
            error = string.Empty;
            try
            {
                var directory = _directory;
                if (string.IsNullOrEmpty(directory))
                {
                    directory = GetLogDirectory();
                }

                Directory.CreateDirectory(directory);
                System.Diagnostics.Process.Start("explorer.exe", "\"" + directory + "\"");
                return true;
            }
            catch (Exception exception)
            {
                error = SanitizeUtf16(exception.Message);
                return false;
            }
        }

        internal static void DrawSettingsGui(
            SettingsUiText text,
            GUIStyle labelStyle,
            GUIStyle selectionStyle,
            GUIStyle toggleStyle,
            float contentWidth)
        {
            var settings = Plugin.Settings;
            if (settings == null || text == null)
            {
                return;
            }

            GUILayout.Label(text.DiagnosticLogPreset, labelStyle);
            var preset = (settings.DiagnosticLogPreset ?? string.Empty).ToLowerInvariant();
            var presetIndex = preset == "basic" ? 0 :
                preset == "join" || preset == "rejoin" ? 1 :
                preset == "afk" ? 2 : preset == "workshop" ? 3 :
                preset == "full" || preset == "complete" ? 4 : -1;
            var nextPresetIndex = GUILayout.SelectionGrid(
                presetIndex,
                text.DiagnosticPresets,
                3,
                selectionStyle,
                GUILayout.Width(contentWidth));
            if (nextPresetIndex != presetIndex)
            {
                ApplyPreset(nextPresetIndex == 0 ? "basic" :
                    nextPresetIndex == 1 ? "join" :
                    nextPresetIndex == 2 ? "afk" :
                    nextPresetIndex == 3 ? "workshop" : "full");
            }

            GUILayout.Space(7f);
            GUILayout.Label(text.DiagnosticCategories, labelStyle);
            DrawCategoryToggle(DiagnosticLogCategory.LobbyAndNetwork, text.DiagnosticCategoryLabels[0], toggleStyle);
            DrawCategoryToggle(DiagnosticLogCategory.Workshop, text.DiagnosticCategoryLabels[1], toggleStyle);
            DrawCategoryToggle(DiagnosticLogCategory.PlayerLifecycle, text.DiagnosticCategoryLabels[2], toggleStyle);
            DrawCategoryToggle(DiagnosticLogCategory.Afk, text.DiagnosticCategoryLabels[3], toggleStyle);
            DrawCategoryToggle(DiagnosticLogCategory.LevelOutcome, text.DiagnosticCategoryLabels[4], toggleStyle);
            DrawCategoryToggle(DiagnosticLogCategory.WorkshopObjects, text.DiagnosticCategoryLabels[5], toggleStyle);
            DrawCategoryToggle(DiagnosticLogCategory.FrpDirect, text.DiagnosticCategoryLabels[6], toggleStyle);
            DrawCategoryToggle(DiagnosticLogCategory.OptionalMod, text.DiagnosticCategoryLabels[7], toggleStyle);
            DrawCategoryToggle(DiagnosticLogCategory.HarmonyTrace, text.DiagnosticCategoryLabels[8], toggleStyle);
        }

        private static void DrawCategoryToggle(
            DiagnosticLogCategory category,
            string label,
            GUIStyle style)
        {
            var current = IsCategoryEnabled(category);
            var next = Plugin.DrawSettingsToggle(current, label, style);
            if (next != current)
            {
                SetCategoryEnabled(category, next);
            }
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
                        "[CustomMapMultiplayer] Cannot close diagnostic file: " +
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
            var written = false;
            try
            {
                lock (Sync)
                {
                    line = FormatLine(level, safeMessage);
                    if (!ShouldWrite(level, InferCategory(safeMessage, trace)))
                    {
                        return;
                    }

                    WriteLineLocked(level, safeMessage, trace);
                    written = true;
                }

                if (writeToUnity && written)
                {
                    Debug.Log("[CustomMapMultiplayer] " + line);
                }
            }
            catch (Exception exception)
            {
                Debug.LogError(
                    "[CustomMapMultiplayer] Cannot write diagnostic file: " +
                    SanitizeUtf16(exception.ToString()));
            }
        }

        private static bool ShouldWrite(string level, DiagnosticLogCategory category)
        {
            return string.Equals(level, "WARN", StringComparison.Ordinal) ||
                   string.Equals(level, "ERROR", StringComparison.Ordinal) ||
                   IsCategoryEnabled(category);
        }

        private static DiagnosticLogCategory InferCategory(string message, bool trace)
        {
            var value = (message ?? string.Empty).ToUpperInvariant();
            // HarmonyDiagnostics.TracePrefix marks method traces with TRACE #.
            // Check this before payload words such as PLAYER or JOIN so the
            // dedicated Harmony category can actually suppress those traces.
            if (trace && value.StartsWith("TRACE #", StringComparison.Ordinal))
            {
                return DiagnosticLogCategory.HarmonyTrace;
            }

            if (value.IndexOf("AFK_", StringComparison.Ordinal) >= 0 ||
                value.IndexOf("PLAYER_DROPOUT", StringComparison.Ordinal) >= 0 ||
                value.IndexOf("DROPOUT", StringComparison.Ordinal) >= 0)
            {
                return DiagnosticLogCategory.Afk;
            }

            if (value.IndexOf("LEVEL_OUTCOME", StringComparison.Ordinal) >= 0 ||
                value.IndexOf("LEVEL FINISH", StringComparison.Ordinal) >= 0 ||
                value.IndexOf("REMOVE LIFE", StringComparison.Ordinal) >= 0)
            {
                return DiagnosticLogCategory.LevelOutcome;
            }

            if (value.IndexOf("FRP_DIRECT", StringComparison.Ordinal) >= 0 ||
                value.IndexOf("FRP DIRECT", StringComparison.Ordinal) >= 0)
            {
                return DiagnosticLogCategory.FrpDirect;
            }

            if (value.IndexOf("OPTIONAL_BRO_MOD", StringComparison.Ordinal) >= 0 ||
                value.IndexOf("SWAP BROS", StringComparison.Ordinal) >= 0 ||
                value.IndexOf("OPTIONAL MOD", StringComparison.Ordinal) >= 0)
            {
                return DiagnosticLogCategory.OptionalMod;
            }

            if (value.IndexOf("PICKUP", StringComparison.Ordinal) >= 0 ||
                value.IndexOf("COLLECT", StringComparison.Ordinal) >= 0 ||
                value.IndexOf("AMMO", StringComparison.Ordinal) >= 0 ||
                value.IndexOf("DETERMINISTIC", StringComparison.Ordinal) >= 0 ||
                value.IndexOf("WORKSHOP OBJECT", StringComparison.Ordinal) >= 0 ||
                value.IndexOf("ITEM", StringComparison.Ordinal) >= 0 ||
                value.IndexOf("PROP", StringComparison.Ordinal) >= 0)
            {
                return DiagnosticLogCategory.WorkshopObjects;
            }

            if (value.IndexOf("PLAYER", StringComparison.Ordinal) >= 0 ||
                value.IndexOf("HERO", StringComparison.Ordinal) >= 0 ||
                value.IndexOf("JOIN", StringComparison.Ordinal) >= 0 ||
                value.IndexOf("PID", StringComparison.Ordinal) >= 0 ||
                value.IndexOf("SLOT", StringComparison.Ordinal) >= 0 ||
                value.IndexOf("CONTROLLER", StringComparison.Ordinal) >= 0 ||
                value.IndexOf("SPAWN", StringComparison.Ordinal) >= 0 ||
                value.IndexOf("REGISTER", StringComparison.Ordinal) >= 0)
            {
                return DiagnosticLogCategory.PlayerLifecycle;
            }

            if (value.IndexOf("WORKSHOP", StringComparison.Ordinal) >= 0 ||
                value.IndexOf("CAMPAIGN", StringComparison.Ordinal) >= 0 ||
                value.IndexOf("SCENE", StringComparison.Ordinal) >= 0 ||
                value.IndexOf("LEVEL LOAD", StringComparison.Ordinal) >= 0)
            {
                return DiagnosticLogCategory.Workshop;
            }

            if (trace || value.IndexOf("HARMONY", StringComparison.Ordinal) >= 0 ||
                value.IndexOf("TRACE", StringComparison.Ordinal) >= 0)
            {
                return DiagnosticLogCategory.HarmonyTrace;
            }

            return DiagnosticLogCategory.LobbyAndNetwork;
        }

        private static void SetCategories(DiagnosticSettings settings, DiagnosticLogCategory categories)
        {
            settings.LogLobbyAndNetwork = (categories & DiagnosticLogCategory.LobbyAndNetwork) != 0;
            settings.LogWorkshop = (categories & DiagnosticLogCategory.Workshop) != 0;
            settings.LogPlayerLifecycle = (categories & DiagnosticLogCategory.PlayerLifecycle) != 0;
            settings.LogAfk = (categories & DiagnosticLogCategory.Afk) != 0;
            settings.LogLevelOutcome = (categories & DiagnosticLogCategory.LevelOutcome) != 0;
            settings.LogWorkshopObjects = (categories & DiagnosticLogCategory.WorkshopObjects) != 0;
            settings.LogFrpDirect = (categories & DiagnosticLogCategory.FrpDirect) != 0;
            settings.LogOptionalMod = (categories & DiagnosticLogCategory.OptionalMod) != 0;
            settings.LogHarmonyTrace = (categories & DiagnosticLogCategory.HarmonyTrace) != 0;
        }

        private static void EnsureCategoryDefaults()
        {
            var settings = Plugin.Settings;
            if (settings == null)
            {
                return;
            }

            // Old UMM XML does not contain new bool fields. Detect that shape and retain the old all-on behavior.
            if (string.IsNullOrEmpty(settings.DiagnosticLogPreset))
            {
                var anyEnabled = settings.LogLobbyAndNetwork || settings.LogWorkshop ||
                    settings.LogPlayerLifecycle || settings.LogAfk || settings.LogLevelOutcome ||
                    settings.LogWorkshopObjects || settings.LogFrpDirect || settings.LogOptionalMod ||
                    settings.LogHarmonyTrace;
                if (!anyEnabled)
                {
                    SetCategories(settings, AllCategories);
                }

                settings.DiagnosticLogPreset = "full";
            }
        }

        private static string GetLogDirectory()
        {
            var localApplicationData = Environment.GetFolderPath(
                Environment.SpecialFolder.LocalApplicationData);
            var appDataDirectory = string.IsNullOrEmpty(localApplicationData)
                ? null
                : Directory.GetParent(localApplicationData);
            if (appDataDirectory != null)
            {
                return Path.Combine(
                    Path.Combine(
                        Path.Combine(
                            Path.Combine(appDataDirectory.FullName, "LocalLow"),
                            "Free Lives"),
                        "Broforce"),
                    "CustomMapMultiplayer");
            }

            return Path.Combine(Application.persistentDataPath, "CustomMapMultiplayer");
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

            var buildInfo =
                "BUILD_INFO algorithm=SHA-256; buildHash=" + BuildMetadata.BuildHash +
                "; scope=source-and-build-inputs";
            WriteLineLocked("INFO", buildInfo, false);
            WriteLineLocked("TRACE", buildInfo, true);
            var categoryInfo = "DIAGNOSTIC_CATEGORIES enabled=" + GetEnabledCategoryList();
            WriteLineLocked("INFO", categoryInfo, false);
            WriteLineLocked("TRACE", categoryInfo, true);
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
