using UnityModManagerNet;
using System;
using UnityEngine;

namespace CustomMapMultiplayer
{
    public static class Plugin
    {
        private const int CurrentDiagnosticSettingsVersion = 9;
        private const float FrpSettingsApplyDelaySeconds = 0.75f;
        private const float SettingsNavigationWidth = 190f;
        private const float SettingsContentWidth = 590f;
        private const float SettingsTextFieldWidth = 220f;
        private const float SettingsTextFieldHeight = 24f;
        private static readonly Color SettingsToggleOnColor = new Color(0.72f, 1.00f, 1.00f, 1f);
        private static readonly Color SettingsToggleOffColor = new Color(0.68f, 0.71f, 0.74f, 1f);
        private static UnityModManager.ModEntry _modEntry;
        private static DiagnosticsBehaviour _behaviour;
        private static GUIStyle _settingsNavigationPanelStyle;
        private static GUIStyle _settingsContentPanelStyle;
        private static GUIStyle _settingsNavigationStyle;
        private static GUIStyle _settingsSelectedNavigationStyle;
        private static Texture2D _settingsSelectedNavigationBackground;
        private static Texture2D _settingsSelectedNavigationHoverBackground;
        private static Texture2D _settingsSelectedNavigationActiveBackground;
        private static GUIStyle _settingsTitleStyle;
        private static GUIStyle _settingsLabelStyle;
        private static GUIStyle _settingsHelpStyle;
        private static GUIStyle _settingsIndentedHelpStyle;
        private static GUIStyle _settingsToggleStyle;
        private static GUIStyle _settingsButtonStyle;
        private static GUIStyle _settingsTextFieldStyle;
        private static GUIStyle _settingsToolbarStyle;
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

        internal static bool DrawSettingsToggle(bool value, string label, GUIStyle style)
        {
            var previousColor = GUI.color;
            GUI.color = value ? SettingsToggleOnColor : SettingsToggleOffColor;
            try
            {
                return GUILayout.Toggle(value, label, style);
            }
            finally
            {
                GUI.color = previousColor;
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
            ClearSettingsUiStyles();
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

            var text = SettingsUiLocalization.Get(Settings.SettingsLanguage);
            GUILayout.BeginHorizontal();
            GUILayout.BeginVertical(
                GetSettingsNavigationPanelStyle(),
                GUILayout.Width(SettingsNavigationWidth),
                GUILayout.MinHeight(500f));
            DrawSettingsNavigation(text);
            GUILayout.EndVertical();
            GUILayout.Space(8f);
            GUILayout.BeginVertical(
                GetSettingsContentPanelStyle(),
                GUILayout.Width(SettingsContentWidth),
                GUILayout.MinHeight(500f));
            switch (Settings.SettingsPanel)
            {
                case "frp":
                    DrawFrpSettings(modEntry, text);
                    break;
                case "language":
                    DrawLanguageSettings(text);
                    break;
                case "logs":
                    DrawDiagnosticSettings(modEntry, text);
                    break;
                default:
                    DrawMultiplayerSettings(modEntry, text);
                    break;
            }
            GUILayout.EndVertical();
            GUILayout.EndHorizontal();
            ApplyPendingFrpDirectSettings(modEntry);
        }

        private static void DrawSettingsNavigation(SettingsUiText text)
        {
            DrawSettingsNavigationButton(text.MultiplayerOptions, "multiplayer");
            DrawSettingsNavigationButton(text.FrpDirect, "frp");
            DrawSettingsNavigationButton(text.Language, "language");
            DrawSettingsNavigationButton(text.DiagnosticLogs, "logs");
        }

        private static void DrawSettingsNavigationButton(string label, string panel)
        {
            var selected = string.Equals(Settings.SettingsPanel, panel, StringComparison.OrdinalIgnoreCase);
            if (GUILayout.Button(
                    label,
                    selected ? GetSettingsSelectedNavigationStyle() : GetSettingsNavigationStyle(),
                    GUILayout.ExpandWidth(true)))
            {
                Settings.SettingsPanel = panel;
            }
            GUILayout.Space(3f);
        }

        private static void DrawMultiplayerSettings(
            UnityModManager.ModEntry modEntry,
            SettingsUiText text)
        {
            DrawViewHeading(text.MultiplayerOptions, text.WorkshopIntro);

            var workshopInjectionEnabled = DrawSettingsToggle(
                Settings.EnableOnlineWorkshopInjection,
                Settings.EnableOnlineWorkshopInjection ? text.WorkshopEnabled : text.WorkshopDisabled,
                GetSettingsToggleStyle());
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

            DrawIndentedHelp(
                Settings.EnableOnlineWorkshopInjection ? text.WorkshopEnabledHelp : text.WorkshopDisabledHelp);

            var disableAfkSpectator = DrawSettingsToggle(
                Settings.DisableOnlineAfkSpectatorMode,
                Settings.DisableOnlineAfkSpectatorMode
                    ? text.AfkDisabled
                    : text.AfkEnabled,
                GetSettingsToggleStyle());
            if (disableAfkSpectator != Settings.DisableOnlineAfkSpectatorMode)
            {
                Settings.DisableOnlineAfkSpectatorMode = disableAfkSpectator;
                SaveSettings(modEntry);
            }

            GUILayout.Space(7f);
            GUILayout.Label(text.WorkshopNotice, GetSettingsHelpStyle(), GUILayout.ExpandWidth(true));
            Settings.WorkshopId = DrawTextField(text.WorkshopId, Settings.WorkshopId);
            Settings.WorkshopCampaignName = DrawTextField(text.WorkshopCampaignName, Settings.WorkshopCampaignName);
            Settings.WorkshopSceneName = DrawTextField(text.WorkshopScene, Settings.WorkshopSceneName);
        }

        private static void DrawFrpSettings(
            UnityModManager.ModEntry modEntry,
            SettingsUiText text)
        {
            DrawViewHeading(text.FrpDirect, text.FrpIntro);

            var frpEnabled = DrawSettingsToggle(
                Settings.EnableFrpDirect,
                Settings.EnableFrpDirect ? text.FrpEnabled : text.FrpDisabled,
                GetSettingsToggleStyle());
            if (frpEnabled != Settings.EnableFrpDirect)
            {
                SetFrpDirectEnabled(frpEnabled);
                ApplyFrpDirectSettingsImmediately(modEntry);
            }
            DrawIndentedHelp(Settings.EnableFrpDirect ? text.FrpEnabledHelp : text.FrpDisabledHelp);

            GUILayout.Space(7f);
            GUILayout.Label(text.FrpRole, GetSettingsLabelStyle());
            DrawIndentedHelp(text.FrpRoleHelp);
            var previousRoleIndex = string.Equals(
                Settings.FrpDirectRole,
                "client",
                StringComparison.OrdinalIgnoreCase) ? 1 : 0;
            var roleIndex = GUILayout.Toolbar(
                previousRoleIndex,
                new[] { text.Host, text.Client },
                GetSettingsToolbarStyle(),
                GUILayout.Width(260f));
            if (roleIndex != previousRoleIndex)
            {
                Settings.FrpDirectRole = roleIndex == 1 ? "client" : "host";
                ApplyFrpDirectSettingsImmediately(modEntry);
            }

            if (roleIndex == 0)
            {
                GUILayout.Space(6f);
                GUILayout.Label(text.LocalUdpPort, GetSettingsLabelStyle());
                var localPort = DrawPortField(Settings.FrpDirectLocalPort);
                if (localPort != Settings.FrpDirectLocalPort)
                {
                    Settings.FrpDirectLocalPort = localPort;
                    ScheduleFrpDirectSettingsApply();
                }

                GUILayout.Space(6f);
                GUILayout.Label(text.FrpPlayerLimit, GetSettingsLabelStyle());
                var currentPlayerLimit = global::System.Math.Max(
                    1,
                    global::System.Math.Min(4, Settings.FrpDirectPlayerLimit));
                var selectedPlayerLimit = GUILayout.Toolbar(
                    currentPlayerLimit - 1,
                    new[] { "1", "2", "3", "4" },
                    GetSettingsToolbarStyle(),
                    GUILayout.Width(260f)) + 1;
                if (selectedPlayerLimit != currentPlayerLimit)
                {
                    Settings.FrpDirectPlayerLimit = selectedPlayerLimit;
                    SaveSettings(modEntry);
                    ApplyFrpDirectPlayerLimit(selectedPlayerLimit);
                }
            }
            else
            {
                GUILayout.Space(6f);
                GUILayout.Label(text.FrpServerEndpoint, GetSettingsLabelStyle());
                var serverEndpoint = GUILayout.TextField(
                    Settings.FrpDirectServerEndpoint ?? string.Empty,
                    GetSettingsTextFieldStyle(),
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

            GUILayout.Space(6f);
            GUILayout.Label(text.FrpRoomPassword, GetSettingsLabelStyle());
            var roomPassword = GUILayout.PasswordField(
                Settings.FrpDirectRoomPassword ?? string.Empty,
                '*',
                GetSettingsTextFieldStyle(),
                GUILayout.Width(260f));
            if (!string.Equals(
                    roomPassword,
                    Settings.FrpDirectRoomPassword ?? string.Empty,
                    StringComparison.Ordinal))
            {
                Settings.FrpDirectRoomPassword = roomPassword;
                ScheduleFrpDirectSettingsApply();
            }
            GUILayout.Space(7f);
            GUILayout.Label(text.FrpStatus + GetFrpDirectStatus(), GetSettingsHelpStyle(), GUILayout.ExpandWidth(true));
        }

        private static void DrawLanguageSettings(SettingsUiText text)
        {
            DrawViewHeading(text.Language, text.LanguageIntro);
            var currentLanguage = GetSettingsLanguageIndex(Settings.SettingsLanguage);
            var nextLanguage = GUILayout.SelectionGrid(
                currentLanguage,
                text.LanguageChoices,
                3,
                GetSettingsToolbarStyle(),
                GUILayout.Width(390f));
            if (nextLanguage != currentLanguage)
            {
                Settings.SettingsLanguage = nextLanguage == 1 ? "en" :
                    nextLanguage == 2 ? "zh" : "system";
            }
        }

        private static int GetSettingsLanguageIndex(string language)
        {
            if (string.Equals(language, "en", StringComparison.OrdinalIgnoreCase))
            {
                return 1;
            }
            if (string.Equals(language, "zh", StringComparison.OrdinalIgnoreCase))
            {
                return 2;
            }
            return 0;
        }

        private static void DrawDiagnosticSettings(
            UnityModManager.ModEntry modEntry,
            SettingsUiText text)
        {
            DrawViewHeading(text.DiagnosticLogs, text.DiagnosticsIntro);
            Settings.DiagnosticSessionId = DrawTextField(
                text.DiagnosticSessionId,
                Settings.DiagnosticSessionId);
            Settings.DiagnosticRole = DrawTextField(text.DiagnosticLabel, Settings.DiagnosticRole);
            if (GUILayout.Button(text.OpenLogDirectory, GetSettingsButtonStyle(), GUILayout.Width(280f)))
            {
                string error;
                DiagnosticLog.TryOpenDirectory(out error);
            }
            GUILayout.Space(7f);
            var performanceTelemetryEnabled = DrawSettingsToggle(
                Settings.EnablePerformanceTelemetry,
                Settings.EnablePerformanceTelemetry
                    ? text.PerformanceTelemetryEnabled
                    : text.PerformanceTelemetryDisabled,
                GetSettingsToggleStyle());
            if (performanceTelemetryEnabled != Settings.EnablePerformanceTelemetry)
            {
                Settings.EnablePerformanceTelemetry = performanceTelemetryEnabled;
                SaveSettings(modEntry);
            }
            DrawIndentedHelp(text.PerformanceTelemetryHelp);
            GUILayout.Space(7f);
            DiagnosticLog.DrawSettingsGui(
                text,
                GetSettingsLabelStyle(),
                GetSettingsToolbarStyle(),
                GetSettingsToggleStyle(),
                SettingsContentWidth - 30f);
        }

        private static string DrawTextField(string label, string value)
        {
            GUILayout.Space(6f);
            GUILayout.Label(label, GetSettingsLabelStyle());
            return GUILayout.TextField(
                value ?? string.Empty,
                GetSettingsTextFieldStyle(),
                GUILayout.Width(SettingsTextFieldWidth));
        }

        private static void DrawIndentedHelp(string value)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Space(18f);
            GUILayout.Label(value, GetSettingsIndentedHelpStyle(), GUILayout.ExpandWidth(true));
            GUILayout.EndHorizontal();
        }

        private static void DrawViewHeading(string title, string intro)
        {
            GUILayout.Label(title, GetSettingsTitleStyle());
            GUILayout.Label(intro, GetSettingsHelpStyle(), GUILayout.ExpandWidth(true));
            GUILayout.Space(10f);
        }

        private static GUIStyle GetSettingsHelpStyle()
        {
            if (_settingsHelpStyle == null)
            {
                _settingsHelpStyle = new GUIStyle(GUI.skin.label);
                _settingsHelpStyle.fontSize = 13;
                _settingsHelpStyle.wordWrap = true;
            }
            return _settingsHelpStyle;
        }

        private static GUIStyle GetSettingsIndentedHelpStyle()
        {
            if (_settingsIndentedHelpStyle == null)
            {
                _settingsIndentedHelpStyle = new GUIStyle(GetSettingsHelpStyle());
                _settingsIndentedHelpStyle.fontSize = 12;
                _settingsIndentedHelpStyle.margin = new RectOffset(0, 0, 0, 0);
                _settingsIndentedHelpStyle.padding = new RectOffset(0, 0, 0, 0);
                _settingsIndentedHelpStyle.fixedHeight = 18f;
            }
            return _settingsIndentedHelpStyle;
        }

        private static GUIStyle GetSettingsNavigationPanelStyle()
        {
            if (_settingsNavigationPanelStyle == null)
            {
                _settingsNavigationPanelStyle = new GUIStyle(GUI.skin.box);
                _settingsNavigationPanelStyle.padding = new RectOffset(8, 8, 10, 10);
            }
            return _settingsNavigationPanelStyle;
        }

        private static GUIStyle GetSettingsContentPanelStyle()
        {
            if (_settingsContentPanelStyle == null)
            {
                _settingsContentPanelStyle = new GUIStyle(GUI.skin.box);
                _settingsContentPanelStyle.padding = new RectOffset(14, 14, 12, 14);
            }
            return _settingsContentPanelStyle;
        }

        private static GUIStyle GetSettingsNavigationStyle()
        {
            if (_settingsNavigationStyle == null)
            {
                _settingsNavigationStyle = new GUIStyle(GUI.skin.button);
                _settingsNavigationStyle.alignment = TextAnchor.MiddleLeft;
                _settingsNavigationStyle.fontSize = 14;
                _settingsNavigationStyle.padding = new RectOffset(10, 8, 5, 5);
                _settingsNavigationStyle.fixedHeight = 38f;
            }
            return _settingsNavigationStyle;
        }

        private static GUIStyle GetSettingsSelectedNavigationStyle()
        {
            if (_settingsSelectedNavigationStyle == null)
            {
                _settingsSelectedNavigationStyle = new GUIStyle(GetSettingsNavigationStyle());
                _settingsSelectedNavigationStyle.fontStyle = FontStyle.Bold;
                _settingsSelectedNavigationStyle.border = new RectOffset(5, 5, 5, 5);
                _settingsSelectedNavigationStyle.normal.background =
                    GetSettingsSelectedNavigationBackground();
                _settingsSelectedNavigationStyle.hover.background =
                    GetSettingsSelectedNavigationHoverBackground();
                _settingsSelectedNavigationStyle.active.background =
                    GetSettingsSelectedNavigationActiveBackground();
                _settingsSelectedNavigationStyle.focused.background =
                    GetSettingsSelectedNavigationHoverBackground();
                _settingsSelectedNavigationStyle.normal.textColor = Color.white;
                _settingsSelectedNavigationStyle.hover.textColor = Color.white;
                _settingsSelectedNavigationStyle.active.textColor = Color.white;
                _settingsSelectedNavigationStyle.focused.textColor = Color.white;
            }
            return _settingsSelectedNavigationStyle;
        }

        private static Texture2D GetSettingsSelectedNavigationBackground()
        {
            if (_settingsSelectedNavigationBackground == null)
            {
                _settingsSelectedNavigationBackground = CreateSettingsNavigationBackground(
                    new Color(0.18f, 0.50f, 0.62f, 1f),
                    new Color(0.26f, 0.62f, 0.72f, 1f));
            }
            return _settingsSelectedNavigationBackground;
        }

        private static Texture2D GetSettingsSelectedNavigationHoverBackground()
        {
            if (_settingsSelectedNavigationHoverBackground == null)
            {
                _settingsSelectedNavigationHoverBackground = CreateSettingsNavigationBackground(
                    new Color(0.25f, 0.65f, 0.76f, 1f),
                    new Color(0.39f, 0.80f, 0.88f, 1f));
            }
            return _settingsSelectedNavigationHoverBackground;
        }

        private static Texture2D GetSettingsSelectedNavigationActiveBackground()
        {
            if (_settingsSelectedNavigationActiveBackground == null)
            {
                _settingsSelectedNavigationActiveBackground = CreateSettingsNavigationBackground(
                    new Color(0.30f, 0.72f, 0.82f, 1f),
                    new Color(0.48f, 0.88f, 0.94f, 1f));
            }
            return _settingsSelectedNavigationActiveBackground;
        }

        private static Texture2D CreateSettingsNavigationBackground(
            Color fillColor,
            Color highlightColor)
        {
            const int textureSize = 18;
            const float cornerRadius = 5f;
            var texture = new Texture2D(textureSize, textureSize, TextureFormat.RGBA32, false);
            for (var y = 0; y < textureSize; y++)
            {
                for (var x = 0; x < textureSize; x++)
                {
                    var centerX = x + 0.5f;
                    var centerY = y + 0.5f;
                    var distanceX = centerX < cornerRadius
                        ? cornerRadius - centerX
                        : centerX > textureSize - cornerRadius
                            ? centerX - (textureSize - cornerRadius)
                            : 0f;
                    var distanceY = centerY < cornerRadius
                        ? cornerRadius - centerY
                        : centerY > textureSize - cornerRadius
                            ? centerY - (textureSize - cornerRadius)
                            : 0f;
                    var distanceSquared = distanceX * distanceX + distanceY * distanceY;
                    if (distanceX > 0f && distanceY > 0f &&
                        distanceSquared > cornerRadius * cornerRadius)
                    {
                        texture.SetPixel(x, y, Color.clear);
                        continue;
                    }

                    var highlightAmount = y >= textureSize - 4
                        ? (y - (textureSize - 4) + 1) / 4f
                        : 0f;
                    texture.SetPixel(x, y, Color.Lerp(fillColor, highlightColor, highlightAmount));
                }
            }
            texture.Apply();
            texture.wrapMode = TextureWrapMode.Clamp;
            texture.filterMode = FilterMode.Bilinear;
            texture.hideFlags = HideFlags.HideAndDontSave;
            return texture;
        }

        private static GUIStyle GetSettingsTitleStyle()
        {
            if (_settingsTitleStyle == null)
            {
                _settingsTitleStyle = new GUIStyle(GUI.skin.label);
                _settingsTitleStyle.fontSize = 18;
                _settingsTitleStyle.fontStyle = FontStyle.Bold;
            }
            return _settingsTitleStyle;
        }

        private static GUIStyle GetSettingsLabelStyle()
        {
            if (_settingsLabelStyle == null)
            {
                _settingsLabelStyle = new GUIStyle(GUI.skin.label);
                _settingsLabelStyle.fontSize = 14;
            }
            return _settingsLabelStyle;
        }

        private static GUIStyle GetSettingsToggleStyle()
        {
            if (_settingsToggleStyle == null)
            {
                _settingsToggleStyle = new GUIStyle(GUI.skin.toggle);
                _settingsToggleStyle.fontSize = 14;
                _settingsToggleStyle.fixedHeight = 30f;
            }
            return _settingsToggleStyle;
        }

        private static GUIStyle GetSettingsButtonStyle()
        {
            if (_settingsButtonStyle == null)
            {
                _settingsButtonStyle = new GUIStyle(GUI.skin.button);
                _settingsButtonStyle.fontSize = 14;
                _settingsButtonStyle.fixedHeight = 30f;
            }
            return _settingsButtonStyle;
        }

        private static GUIStyle GetSettingsTextFieldStyle()
        {
            if (_settingsTextFieldStyle == null)
            {
                _settingsTextFieldStyle = new GUIStyle(GUI.skin.textField);
                _settingsTextFieldStyle.fontSize = 13;
                _settingsTextFieldStyle.fixedHeight = SettingsTextFieldHeight;
            }
            return _settingsTextFieldStyle;
        }

        private static GUIStyle GetSettingsToolbarStyle()
        {
            if (_settingsToolbarStyle == null)
            {
                _settingsToolbarStyle = new GUIStyle(GUI.skin.button);
                _settingsToolbarStyle.fontSize = 14;
                _settingsToolbarStyle.fixedHeight = 30f;
            }
            return _settingsToolbarStyle;
        }

        private static void ClearSettingsUiStyles()
        {
            _settingsNavigationPanelStyle = null;
            _settingsContentPanelStyle = null;
            _settingsNavigationStyle = null;
            _settingsSelectedNavigationStyle = null;
            if (_settingsSelectedNavigationBackground != null)
            {
                UnityEngine.Object.Destroy(_settingsSelectedNavigationBackground);
                _settingsSelectedNavigationBackground = null;
            }
            if (_settingsSelectedNavigationHoverBackground != null)
            {
                UnityEngine.Object.Destroy(_settingsSelectedNavigationHoverBackground);
                _settingsSelectedNavigationHoverBackground = null;
            }
            if (_settingsSelectedNavigationActiveBackground != null)
            {
                UnityEngine.Object.Destroy(_settingsSelectedNavigationActiveBackground);
                _settingsSelectedNavigationActiveBackground = null;
            }
            _settingsTitleStyle = null;
            _settingsLabelStyle = null;
            _settingsHelpStyle = null;
            _settingsIndentedHelpStyle = null;
            _settingsToggleStyle = null;
            _settingsButtonStyle = null;
            _settingsTextFieldStyle = null;
            _settingsToolbarStyle = null;
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
            var text = GUILayout.TextField(
                value.ToString(),
                GetSettingsTextFieldStyle(),
                GUILayout.Width(SettingsTextFieldWidth));
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
            if (settings == null)
            {
                return;
            }

            if (settings.DiagnosticSettingsVersion >= CurrentDiagnosticSettingsVersion)
            {
                NormalizeSettingsPresentation(settings);
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

            NormalizeSettingsPresentation(settings);

            settings.DiagnosticSettingsVersion = CurrentDiagnosticSettingsVersion;
        }

        private static void NormalizeSettingsPresentation(DiagnosticSettings settings)
        {
            if (!string.Equals(settings.SettingsPanel, "frp", StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(settings.SettingsPanel, "language", StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(settings.SettingsPanel, "logs", StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(settings.SettingsPanel, "multiplayer", StringComparison.OrdinalIgnoreCase))
            {
                settings.SettingsPanel = "multiplayer";
            }

            if (!string.Equals(settings.SettingsLanguage, "en", StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(settings.SettingsLanguage, "zh", StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(settings.SettingsLanguage, "system", StringComparison.OrdinalIgnoreCase))
            {
                settings.SettingsLanguage = "system";
            }
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
