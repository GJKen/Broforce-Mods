using UnityModManagerNet;
using System;
using UnityEngine;

namespace CustomMapMultiplayer
{
    public static class Plugin
    {
        private const int CurrentDiagnosticSettingsVersion = 7;
        private const float FrpSettingsApplyDelaySeconds = 0.75f;
        private const float SectionHeaderWidth = 540f;
        private const float SectionHeaderHeight = 30f;
        private static UnityModManager.ModEntry _modEntry;
        private static DiagnosticsBehaviour _behaviour;
        private static Texture2D _sectionCollapsedIcon;
        private static Texture2D _sectionExpandedIcon;
        private static GUIStyle _workshopSectionStyle;
        private static GUIStyle _frpSectionStyle;
        private static GUIStyle _diagnosticSectionStyle;
        private static float _frpSettingsApplyAt = -1f;

        internal static DiagnosticSettings Settings { get; private set; }

        internal static bool ShouldSkipLateHeroResponse(int playerNum)
        {
            return _behaviour != null && _behaviour.ShouldSkipLateHeroResponse(playerNum);
        }

        internal static void ShowWorkshopNotice(string message)
        {
            if (_behaviour != null)
            {
                _behaviour.ShowWorkshopNotice(message);
            }
        }

        internal static void ClearWorkshopNotice()
        {
            if (_behaviour != null)
            {
                _behaviour.ClearWorkshopNotice();
            }
        }

        internal static void ShowFrpDirectNotice(string message)
        {
            if (_behaviour != null)
            {
                _behaviour.ShowFrpDirectNotice(message);
            }
        }

        internal static void ClearFrpDirectNotice()
        {
            if (_behaviour != null)
            {
                _behaviour.ClearFrpDirectNotice();
            }
        }

        public static bool Load(UnityModManager.ModEntry modEntry)
        {
            try
            {
                _modEntry = modEntry;
                modEntry.OnToggle = OnToggle;
                modEntry.OnUnload = OnUnload;
                modEntry.OnGUI = OnGUI;
                modEntry.OnSaveGUI = OnSaveGUI;
                try
                {
                    Settings = UnityModManager.ModSettings.Load<DiagnosticSettings>(modEntry);
                }
                catch (Exception settingsException)
                {
                    Settings = new DiagnosticSettings();
                    modEntry.Logger.LogException("Diagnostic settings load failed; empty settings are active", settingsException);
                }

                if (Settings == null)
                {
                    Settings = new DiagnosticSettings();
                }

                MigrateDiagnosticSettings(Settings);

                DiagnosticLog.Initialize(modEntry);
                DiagnosticLog.Info(
                    "Plugin loaded. Steam diagnostics are active; optional injections and FRP prototype follow saved settings; buildHash=" +
                    BuildMetadata.BuildHash + ".");
                return true;
            }
            catch (Exception exception)
            {
                modEntry.Logger.LogException("Diagnostic plugin Load failed", exception);
                return false;
            }
        }

        private static bool OnToggle(UnityModManager.ModEntry modEntry, bool enabled)
        {
            if (enabled)
            {
                _behaviour = DiagnosticsBehaviour.Create();
                HarmonyDiagnostics.Start();
                DiagnosticLog.Info("Diagnostics enabled.");
            }
            else
            {
                StopDiagnostics();
            }

            SaveSettings(modEntry);

            return true;
        }

        private static bool OnUnload(UnityModManager.ModEntry modEntry)
        {
            DiagnosticLog.Info("Plugin unloading.");
            SaveSettings(modEntry);
            StopDiagnostics();
            DiagnosticLog.Close();
            ReleaseSectionUiResources();
            _modEntry = null;
            Settings = null;
            return true;
        }

        private static void OnGUI(UnityModManager.ModEntry modEntry)
        {
            if (Settings == null)
            {
                return;
            }

            if (DrawSectionHeader(
                    "Workshop online",
                    Settings.WorkshopSettingsExpanded,
                    ref _workshopSectionStyle,
                    new Color(0.40f, 0.85f, 0.56f)))
            {
                Settings.WorkshopSettingsExpanded = !Settings.WorkshopSettingsExpanded;
            }
            if (Settings.WorkshopSettingsExpanded)
            {
                var workshopInjectionEnabled = GUILayout.Toggle(
                    Settings.EnableOnlineWorkshopInjection,
                    "Inject configured workshop map into online level switching");
                if (workshopInjectionEnabled != Settings.EnableOnlineWorkshopInjection)
                {
                    var wasEnabled = Settings.EnableOnlineWorkshopInjection;
                    Settings.EnableOnlineWorkshopInjection = workshopInjectionEnabled;
                    SaveSettings(modEntry);
                    if (wasEnabled && !workshopInjectionEnabled)
                    {
                        HarmonyDiagnostics.DisableOnlineWorkshopInjection(
                            "UMM Workshop injection setting disabled");
                    }
                }
                Settings.DisableOnlineAfkSpectatorMode = GUILayout.Toggle(
                    Settings.DisableOnlineAfkSpectatorMode,
                    "Disable automatic AFK spectator mode in online games");
                GUILayout.Label("Workshop ID (host only; clients ignore saved values and detect the room map)");
                Settings.WorkshopId = GUILayout.TextField(
                    Settings.WorkshopId ?? string.Empty,
                    GUILayout.Width(260f));
                GUILayout.Label("Workshop campaign name (optional)");
                Settings.WorkshopCampaignName = GUILayout.TextField(
                    Settings.WorkshopCampaignName ?? string.Empty,
                    GUILayout.Width(260f));
                GUILayout.Label("Custom level scene");
                Settings.WorkshopSceneName = GUILayout.TextField(
                    Settings.WorkshopSceneName ?? string.Empty,
                    GUILayout.Width(260f));
            }

            if (DrawSectionHeader(
                    "FRP Direct",
                    Settings.FrpDirectSettingsExpanded,
                    ref _frpSectionStyle,
                    new Color(0.95f, 0.70f, 0.32f)))
            {
                Settings.FrpDirectSettingsExpanded = !Settings.FrpDirectSettingsExpanded;
            }
            if (Settings.FrpDirectSettingsExpanded)
            {
                var frpEnabled = GUILayout.Toggle(
                    Settings.EnableFrpDirect,
                    "Enable FRP Direct networking");
                if (frpEnabled != Settings.EnableFrpDirect)
                {
                    SetFrpDirectEnabled(frpEnabled);
                    ApplyFrpDirectSettingsImmediately(modEntry);
                }
                GUILayout.Label("FRP Direct role");
                var previousRoleIndex = string.Equals(
                    Settings.FrpDirectRole,
                    "client",
                    StringComparison.OrdinalIgnoreCase) ? 1 : 0;
                var roleIndex = previousRoleIndex;
                roleIndex = GUILayout.Toolbar(roleIndex, new[] { "Host", "Client" }, GUILayout.Width(260f));
                if (roleIndex != previousRoleIndex)
                {
                    Settings.FrpDirectRole = roleIndex == 1 ? "client" : "host";
                    ApplyFrpDirectSettingsImmediately(modEntry);
                }
                if (roleIndex == 0)
                {
                    GUILayout.Label("Local UDP listen port");
                    var localPort = DrawPortField(Settings.FrpDirectLocalPort);
                    if (localPort != Settings.FrpDirectLocalPort)
                    {
                        Settings.FrpDirectLocalPort = localPort;
                        ScheduleFrpDirectSettingsApply();
                    }
                    GUILayout.Label("FRP room player limit (applies immediately)");
                    var currentPlayerLimit = global::System.Math.Max(
                        1,
                        global::System.Math.Min(4, Settings.FrpDirectPlayerLimit));
                    var playerLimitIndex = currentPlayerLimit - 1;
                    playerLimitIndex = GUILayout.Toolbar(
                        playerLimitIndex,
                        new[] { "1", "2", "3", "4" },
                        GUILayout.Width(260f));
                    var selectedPlayerLimit = playerLimitIndex + 1;
                    if (selectedPlayerLimit != currentPlayerLimit)
                    {
                        Settings.FrpDirectPlayerLimit = selectedPlayerLimit;
                        SaveSettings(modEntry);
                        ApplyFrpDirectPlayerLimit(selectedPlayerLimit);
                    }
                }
                else
                {
                    GUILayout.Label("FRP server endpoint (host:port)");
                    var serverEndpoint = GUILayout.TextField(
                        Settings.FrpDirectServerEndpoint ?? string.Empty,
                        GUILayout.Width(260f));
                    if (!string.Equals(
                            serverEndpoint,
                            Settings.FrpDirectServerEndpoint ?? string.Empty,
                            StringComparison.Ordinal))
                    {
                        Settings.FrpDirectServerEndpoint = serverEndpoint;
                        ScheduleFrpDirectSettingsApply();
                    }
                }
                GUILayout.Label("FRP room password (optional)");
                var roomPassword = GUILayout.PasswordField(
                    Settings.FrpDirectRoomPassword ?? string.Empty,
                    '*',
                    GUILayout.Width(260f));
                if (!string.Equals(
                        roomPassword,
                        Settings.FrpDirectRoomPassword ?? string.Empty,
                        StringComparison.Ordinal))
                {
                    Settings.FrpDirectRoomPassword = roomPassword;
                    ScheduleFrpDirectSettingsApply();
                }
                GUILayout.Label("FRP Direct status: " + GetFrpDirectStatus());
            }

            if (DrawSectionHeader(
                    "Diagnostic logs",
                    Settings.DiagnosticSettingsExpanded,
                    ref _diagnosticSectionStyle,
                    new Color(0.38f, 0.76f, 0.96f)))
            {
                Settings.DiagnosticSettingsExpanded = !Settings.DiagnosticSettingsExpanded;
            }
            if (Settings.DiagnosticSettingsExpanded)
            {
                GUILayout.Label("Diagnostic session ID (use the same value on both clients; optional)");
                Settings.DiagnosticSessionId = GUILayout.TextField(
                    Settings.DiagnosticSessionId ?? string.Empty,
                    GUILayout.Width(260f));
                GUILayout.Label("Diagnostic label (optional; only used in log names)");
                Settings.DiagnosticRole = GUILayout.TextField(
                    Settings.DiagnosticRole ?? string.Empty,
                    GUILayout.Width(260f));
                DiagnosticLog.DrawSettingsGui();
            }
            ApplyPendingFrpDirectSettings(modEntry);
            GUILayout.Label("Changes are saved when UMM settings are saved, the Mod is toggled, or the game exits normally.");
        }

        private static bool DrawSectionHeader(
            string title,
            bool expanded,
            ref GUIStyle style,
            Color textColor)
        {
            EnsureSectionUiResources();
            if (style == null)
            {
                style = new GUIStyle(GUI.skin.button);
                style.alignment = TextAnchor.MiddleLeft;
                style.imagePosition = ImagePosition.ImageLeft;
                style.padding = new RectOffset(12, 8, 4, 4);
                style.normal.textColor = textColor;
                style.hover.textColor = textColor;
                style.active.textColor = textColor;
                style.focused.textColor = textColor;
            }

            return GUILayout.Button(
                new GUIContent(
                    title,
                    expanded ? _sectionExpandedIcon : _sectionCollapsedIcon),
                style,
                GUILayout.Width(SectionHeaderWidth),
                GUILayout.Height(SectionHeaderHeight));
        }

        private static void EnsureSectionUiResources()
        {
            if (_sectionCollapsedIcon == null)
            {
                _sectionCollapsedIcon = CreateSectionTriangle(false);
            }
            if (_sectionExpandedIcon == null)
            {
                _sectionExpandedIcon = CreateSectionTriangle(true);
            }
        }

        private static Texture2D CreateSectionTriangle(bool expanded)
        {
            const int triangleSize = 12;
            const int rightGutter = 4;
            const int textureWidth = triangleSize + rightGutter;
            var texture = new Texture2D(
                textureWidth,
                triangleSize,
                TextureFormat.ARGB32,
                false);
            texture.name = expanded
                ? "BroforceOnlineSectionExpanded"
                : "BroforceOnlineSectionCollapsed";
            texture.hideFlags = HideFlags.HideAndDontSave;
            texture.filterMode = FilterMode.Point;
            var pixels = new Color[textureWidth * triangleSize];
            var color = new Color(0.92f, 0.92f, 0.92f, 1f);

            for (var row = 0; row < 6; row++)
            {
                for (var offset = -row; offset <= row; offset++)
                {
                    var x = expanded ? 6 + offset : 8 - row;
                    var y = expanded ? 8 - row : 6 + offset;
                    if (x >= 0 && x < triangleSize && y >= 0 && y < triangleSize)
                    {
                        pixels[y * textureWidth + x] = color;
                    }
                }
            }

            texture.SetPixels(pixels);
            texture.Apply(false, true);
            return texture;
        }

        private static void ReleaseSectionUiResources()
        {
            if (_sectionCollapsedIcon != null)
            {
                UnityEngine.Object.Destroy(_sectionCollapsedIcon);
                _sectionCollapsedIcon = null;
            }
            if (_sectionExpandedIcon != null)
            {
                UnityEngine.Object.Destroy(_sectionExpandedIcon);
                _sectionExpandedIcon = null;
            }

            _workshopSectionStyle = null;
            _frpSectionStyle = null;
            _diagnosticSectionStyle = null;
        }

        private static void OnSaveGUI(UnityModManager.ModEntry modEntry)
        {
            _frpSettingsApplyAt = -1f;
            SaveSettings(modEntry);
            ApplyFrpDirectSettings(false);
        }

        private static void SetFrpDirectEnabled(bool enabled)
        {
            Settings.EnableFrpDirect = enabled;
            Settings.EnableFrpDirectPrototype = enabled;
            Settings.EnableFrpDirectGameLayer = enabled;
        }

        private static void ApplyFrpDirectSettingsImmediately(UnityModManager.ModEntry modEntry)
        {
            _frpSettingsApplyAt = -1f;
            SaveSettings(modEntry);
            ApplyFrpDirectSettings(false);
        }

        private static void ScheduleFrpDirectSettingsApply()
        {
            _frpSettingsApplyAt = Time.realtimeSinceStartup + FrpSettingsApplyDelaySeconds;
        }

        private static void ApplyPendingFrpDirectSettings(UnityModManager.ModEntry modEntry)
        {
            if (_frpSettingsApplyAt < 0f || Time.realtimeSinceStartup < _frpSettingsApplyAt)
            {
                return;
            }

            ApplyFrpDirectSettingsImmediately(modEntry);
        }

        private static int DrawPortField(int value)
        {
            int parsed;
            var text = GUILayout.TextField(value.ToString(), GUILayout.Width(260f));
            return int.TryParse(text, out parsed) && parsed >= 1 && parsed <= 65535 ? parsed : value;
        }

        private static string GetFrpDirectStatus()
        {
            return _behaviour == null ? "Mod disabled" : _behaviour.GetFrpDirectStatus();
        }

        private static void ApplyFrpDirectSettings(bool forceRestart)
        {
            if (_behaviour != null)
            {
                _behaviour.ApplyFrpDirectSettings(forceRestart);
            }
        }

        private static void ApplyFrpDirectPlayerLimit(int playerLimit)
        {
            if (_behaviour != null)
            {
                _behaviour.ApplyFrpDirectPlayerLimit(playerLimit);
            }
        }

        internal static bool ShouldUseFrpDirectGameLayer
        {
            get
            {
                return Settings != null &&
                       Settings.EnableFrpDirect;
            }
        }

        internal static FrpDirectTransport GetFrpDirectTransport()
        {
            return _behaviour == null ? null : _behaviour.GetFrpDirectTransport();
        }

        private static void SaveSettings(UnityModManager.ModEntry modEntry)
        {
            if (Settings == null)
            {
                return;
            }

            try
            {
                Settings.EnableFrpDirectPrototype = Settings.EnableFrpDirect;
                Settings.EnableFrpDirectGameLayer = Settings.EnableFrpDirect;
                UnityModManager.ModSettings.Save(Settings, modEntry);
            }
            catch (Exception exception)
            {
                modEntry.Logger.LogException("Diagnostic settings save failed", exception);
            }
        }

        private static void MigrateDiagnosticSettings(DiagnosticSettings settings)
        {
            if (settings == null || settings.DiagnosticSettingsVersion >= CurrentDiagnosticSettingsVersion)
            {
                return;
            }

            if (settings.DiagnosticSettingsVersion < 7)
            {
                settings.EnableFrpDirect = settings.EnableFrpDirectPrototype &&
                                           settings.EnableFrpDirectGameLayer;
            }
            settings.EnableFrpDirectPrototype = settings.EnableFrpDirect;
            settings.EnableFrpDirectGameLayer = settings.EnableFrpDirect;

            if (string.Equals((settings.WorkshopId ?? string.Empty).Trim(), "456121589", StringComparison.Ordinal))
            {
                settings.WorkshopId = string.Empty;
            }

            if (string.Equals(
                    (settings.WorkshopCampaignName ?? string.Empty).Trim(),
                    "the sweet taste of freedom 3",
                    StringComparison.OrdinalIgnoreCase))
            {
                settings.WorkshopCampaignName = string.Empty;
            }

            if (string.Equals((settings.DiagnosticRole ?? string.Empty).Trim(), "auto", StringComparison.OrdinalIgnoreCase))
            {
                settings.DiagnosticRole = string.Empty;
            }

            if (string.IsNullOrEmpty((settings.WorkshopSceneName ?? string.Empty).Trim()))
            {
                settings.WorkshopSceneName = DiagnosticSettings.DefaultWorkshopSceneName;
            }

            if (!string.Equals(settings.FrpDirectRole, "client", StringComparison.OrdinalIgnoreCase))
            {
                settings.FrpDirectRole = "host";
            }

            if (settings.FrpDirectLocalPort < 1 || settings.FrpDirectLocalPort > 65535)
            {
                settings.FrpDirectLocalPort = 27045;
            }

            if (settings.FrpDirectPlayerLimit < 1 || settings.FrpDirectPlayerLimit > 4)
            {
                settings.FrpDirectPlayerLimit = 4;
            }

            // Legacy settings did not persist section state. Keep the Workshop
            // controls visible after migration while leaving diagnostics and FRP collapsed.
            if (settings.DiagnosticSettingsVersion < 5)
            {
                settings.WorkshopSettingsExpanded = true;
                settings.DiagnosticSettingsExpanded = false;
                settings.FrpDirectSettingsExpanded = false;
            }

            if (settings.FrpDirectServerPort < 1 || settings.FrpDirectServerPort > 65535)
            {
                settings.FrpDirectServerPort = 27045;
            }

            settings.FrpDirectServerAddress = settings.FrpDirectServerAddress ?? string.Empty;
            settings.FrpDirectServerEndpoint = settings.FrpDirectServerEndpoint ?? string.Empty;
            if (string.IsNullOrEmpty(settings.FrpDirectServerEndpoint.Trim()) &&
                !string.IsNullOrEmpty(settings.FrpDirectServerAddress.Trim()))
            {
                settings.FrpDirectServerEndpoint = FormatServerEndpoint(
                    settings.FrpDirectServerAddress.Trim(),
                    settings.FrpDirectServerPort);
            }
            settings.FrpDirectRoomPassword = settings.FrpDirectRoomPassword ?? string.Empty;

            settings.DiagnosticSettingsVersion = CurrentDiagnosticSettingsVersion;
        }

        private static string FormatServerEndpoint(string address, int port)
        {
            if (string.IsNullOrEmpty(address))
            {
                return string.Empty;
            }

            var formattedAddress = address.IndexOf(':') >= 0 &&
                                   !address.StartsWith("[", StringComparison.Ordinal)
                ? "[" + address + "]"
                : address;
            return formattedAddress + ":" + port;
        }

        private static void StopDiagnostics()
        {
            HarmonyDiagnostics.DisableOnlineWorkshopInjection(
                "UMM diagnostics mod disabled or unloaded");
            HarmonyDiagnostics.Stop();

            if (_behaviour != null)
            {
                _behaviour.Stop();
                _behaviour = null;
            }

            DiagnosticLog.EndSession("diagnostics disabled");
        }
    }
}
