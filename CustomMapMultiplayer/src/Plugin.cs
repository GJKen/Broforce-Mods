using UnityModManagerNet;
using RocketLib.Menus.Core;
using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace CustomMapMultiplayer
{
    public static class Plugin
    {
        private const int CurrentDiagnosticSettingsVersion = 13;
        private const float FrpSettingsApplyDelaySeconds = 0.75f;
        private const float DefaultSettingsNavigationWidth = 190f;
        private const float DefaultSettingsContentWidth = 590f;
        private const float DefaultWorkshopInfoColumnWidth = 300f;
        private const float WorkshopColumnGap = 12f;
        private const float WorkshopMapGridGap = 8f;
        private const float WorkshopMapCardButtonHeight = 46f;
        private const float WorkshopMapTriggerButtonHeight = 46f;
        private const float WorkshopMapFavoriteColumnWidth = 40f;
        private const float WorkshopMapFilterGap = 4f;
        private static float SettingsNavigationWidth = DefaultSettingsNavigationWidth;
        private static float SettingsContentWidth = DefaultSettingsContentWidth;
        private static float WorkshopInfoColumnWidth = DefaultWorkshopInfoColumnWidth;
        private static float WorkshopMapColumnWidth =
            DefaultSettingsContentWidth - 28f - DefaultWorkshopInfoColumnWidth - WorkshopColumnGap;
        private const float SettingsTextFieldWidth = 220f;
        private const float SettingsTextFieldHeight = 24f;
        private static readonly Color SettingsToggleOnColor = new Color(0.72f, 1.00f, 1.00f, 1f);
        private static readonly Color SettingsToggleOffColor = new Color(0.68f, 0.71f, 0.74f, 1f);
        private static readonly Color SettingsNavigationPanelColor = new Color(0.05f, 0.07f, 0.12f, 1f);
        private static readonly Color SettingsContentPanelColor = new Color(0.08f, 0.11f, 0.17f, 1f);
        private static readonly Color SettingsSectionPanelColor = new Color(0.12f, 0.16f, 0.23f, 1f);
        private static readonly Color SettingsButtonColor = new Color(0.32f, 0.16f, 0.07f, 1f);
        private static readonly Color SettingsButtonHoverColor = new Color(0.50f, 0.25f, 0.08f, 1f);
        private static readonly Color SettingsButtonActiveColor = new Color(0.70f, 0.36f, 0.09f, 1f);
        private static readonly Color SettingsButtonSelectedColor = new Color(0.72f, 0.27f, 0.06f, 1f);
        private static readonly Color SettingsButtonSelectedHoverColor = new Color(0.90f, 0.42f, 0.08f, 1f);
        private static readonly Color SettingsButtonSelectedActiveColor = new Color(1.00f, 0.58f, 0.14f, 1f);
        private static readonly Color SettingsTextFieldColor = new Color(0.04f, 0.06f, 0.11f, 1f);
        private static readonly Color SettingsTextFieldFocusedColor = new Color(0.25f, 0.13f, 0.07f, 1f);
        private static readonly Color SettingsTooltipColor = new Color(0.16f, 0.11f, 0.09f, 1f);
        private static readonly Color SettingsAccentColor = new Color(1.00f, 0.68f, 0.24f, 1f);
        private static readonly Color SettingsPrimaryTextColor = new Color(1.00f, 0.97f, 0.92f, 1f);
        private static readonly Color SettingsSecondaryTextColor = new Color(0.96f, 0.78f, 0.57f, 1f);
        private static readonly Color SettingsMapIdTextColor = new Color(0.76f, 0.83f, 0.91f, 1f);
        private static UnityModManager.ModEntry _modEntry;
        private static DiagnosticsBehaviour _behaviour;
        private static GUIStyle _settingsNavigationPanelStyle;
        private static GUIStyle _settingsContentPanelStyle;
        private static GUIStyle _settingsNavigationStyle;
        private static GUIStyle _settingsSelectedNavigationStyle;
        private static Texture2D _settingsSelectedNavigationBackground;
        private static Texture2D _settingsSelectedNavigationHoverBackground;
        private static Texture2D _settingsSelectedNavigationActiveBackground;
        private static Texture2D _settingsNavigationPanelBackground;
        private static Texture2D _settingsContentPanelBackground;
        private static Texture2D _settingsSectionPanelBackground;
        private static Texture2D _settingsButtonBackground;
        private static Texture2D _settingsButtonHoverBackground;
        private static Texture2D _settingsButtonActiveBackground;
        private static Texture2D _settingsButtonSelectedBackground;
        private static Texture2D _settingsButtonSelectedHoverBackground;
        private static Texture2D _settingsButtonSelectedActiveBackground;
        private static Texture2D _settingsTextFieldBackground;
        private static Texture2D _settingsTextFieldFocusedBackground;
        private static Texture2D _settingsTooltipBackground;
        private static Texture2D _settingsRefreshButtonBackground;
        private static Texture2D _settingsRefreshButtonHoverBackground;
        private static Texture2D _settingsRefreshButtonActiveBackground;
        private static Texture2D _settingsAdvancedCollapsedIcon;
        private static Texture2D _settingsAdvancedExpandedIcon;
        private static GUIStyle _settingsTitleStyle;
        private static GUIStyle _settingsLabelStyle;
        private static GUIStyle _settingsHelpStyle;
        private static GUIStyle _settingsIndentedHelpStyle;
        private static GUIStyle _settingsToggleStyle;
        private static GUIStyle _settingsButtonStyle;
        private static GUIStyle _settingsTextFieldStyle;
        private static GUIStyle _settingsToolbarStyle;
        private static GUIStyle _settingsSectionPanelStyle;
        private static GUIStyle _settingsStatusStyle;
        private static GUIStyle _settingsStatusKickerStyle;
        private static GUIStyle _settingsIconButtonStyle;
        private static GUIStyle _settingsMapTriggerStyle;
        private static GUIStyle _settingsEmptyMapTriggerStyle;
        private static GUIStyle _settingsMapOptionStyle;
        private static GUIStyle _settingsMapSelectedOptionStyle;
        private static GUIStyle _settingsMapTitleStyle;
        private static GUIStyle _settingsMapIdStyle;
        private static GUIStyle _settingsAdvancedButtonStyle;
        private static GUIStyle _settingsTooltipStyle;
        private static float _frpSettingsApplyAt = -1f;
        private static bool _pauseMenuAfkActionRegistered;
        private static bool _workshopMapSelectionOpen;
        private static Vector2 _workshopMapScrollPosition;
        private static string _workshopMapSearchText = string.Empty;
        private static int _workshopMapFilter;
        private static bool _ummWindowSizeResolved;
        private static PropertyInfo _ummUiInstanceProperty;
        private static FieldInfo _ummWindowSizeField;
        private static bool _guiClipVisibleRectResolved;
        private static PropertyInfo _guiClipVisibleRectProperty;

        internal const string PauseMenuAfkActionDisplayText = "Enter AFK now";

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
                WorkshopMapDirectory.Clear();

                DiagnosticLog.Initialize(modEntry);
                RegisterPauseMenuAfkAction(modEntry);
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

        private static void RegisterPauseMenuAfkAction(UnityModManager.ModEntry modEntry)
        {
            if (_pauseMenuAfkActionRegistered)
            {
                return;
            }

            // RocketLib uses the registration text as its injection/deduplication key.
            // Keep this value fixed and localize only the instantiated UI below.
            _pauseMenuAfkActionRegistered = true;
            try
            {
                MenuRegistry.RegisterAction(
                    PauseMenuAfkActionDisplayText,
                    menu => HarmonyDiagnostics.RequestLocalAfk(),
                    TargetMenu.PauseMenu,
                    PositionMode.After,
                    "OPTIONS",
                    0,
                    menu => CanShowPauseMenuAfkAction());
                DiagnosticLog.Info(
                    "Registered RocketLib PauseMenu action: Enter AFK now after OPTIONS.");
            }
            catch (Exception exception)
            {
                modEntry.Logger.LogException(
                    "RocketLib PauseMenu AFK action registration failed",
                    exception);
            }
        }

        private static bool CanShowPauseMenuAfkAction()
        {
            return HarmonyDiagnostics.CanShowManualAfkMenuItem();
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
            WorkshopMapDirectory.Clear();
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
            if (string.Equals(Settings.SettingsPanel, "multiplayer", StringComparison.OrdinalIgnoreCase))
            {
                WorkshopMapDirectory.EnsureInitialRefresh();
            }
            else
            {
                _workshopMapSelectionOpen = false;
            }
            ConfigureSettingsLayout();
            GUILayout.BeginHorizontal(
                GUILayout.Width(SettingsNavigationWidth + 8f + SettingsContentWidth),
                GUILayout.ExpandWidth(false));
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
                GUILayout.ExpandWidth(false),
                GUILayout.MinHeight(500f));
            switch (Settings.SettingsPanel)
            {
                case "multiplayer":
                    DrawWorkshopSettings(modEntry, text);
                    break;
                case "behavior":
                    DrawBehaviorSettings(modEntry, text);
                    break;
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
                    DrawWorkshopSettings(modEntry, text);
                    break;
            }
            GUILayout.EndVertical();
            GUILayout.EndHorizontal();
            ApplyPendingFrpDirectSettings(modEntry);
            DrawSettingsTooltip();
        }

        private static void ConfigureSettingsLayout()
        {
            var availableWidth = GetSettingsViewportWidth();
            var defaultTotalWidth =
                DefaultSettingsNavigationWidth + 8f + DefaultSettingsContentWidth;
            if (availableWidth < 1f)
            {
                availableWidth = defaultTotalWidth;
            }

            SettingsNavigationWidth = Mathf.Clamp(
                availableWidth * 0.24f,
                150f,
                DefaultSettingsNavigationWidth);
            SettingsContentWidth = Mathf.Max(
                0f,
                availableWidth - SettingsNavigationWidth - 8f);
            var contentInnerWidth = Mathf.Max(
                0f,
                SettingsContentWidth - GetSettingsContentPanelStyle().padding.horizontal);
            WorkshopInfoColumnWidth = Mathf.Clamp(
                contentInnerWidth * 0.54f,
                0f,
                DefaultWorkshopInfoColumnWidth);
            WorkshopMapColumnWidth = Mathf.Max(
                0f,
                contentInnerWidth - WorkshopInfoColumnWidth - WorkshopColumnGap);
        }

        private static float GetSettingsViewportWidth()
        {
            var box = GUI.skin.box;
            var scrollView = GUI.skin.scrollView;
            // UMM DrawTab wraps OnGUI in two boxes inside its scroll view.
            var containerInset =
                Mathf.Max(scrollView.padding.left, box.margin.left) +
                Mathf.Max(scrollView.padding.right, box.margin.right) +
                Mathf.Max(box.padding.left, box.margin.left) +
                Mathf.Max(box.padding.right, box.margin.right) +
                box.padding.horizontal + 4f;

            if (!_ummWindowSizeResolved)
            {
                _ummWindowSizeResolved = true;
                try
                {
                    var uiType = typeof(UnityModManager).GetNestedType(
                        "UI",
                        BindingFlags.Public | BindingFlags.NonPublic);
                    _ummUiInstanceProperty = uiType == null
                        ? null
                        : uiType.GetProperty(
                            "Instance",
                            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
                    _ummWindowSizeField = uiType == null
                        ? null
                        : uiType.GetField(
                            "mWindowSize",
                            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                }
                catch (Exception exception)
                {
                    DiagnosticLog.Trace("UMM window width reflection setup failed: " + exception.Message);
                }
            }

            if (_ummUiInstanceProperty != null && _ummWindowSizeField != null)
            {
                try
                {
                    var uiInstance = _ummUiInstanceProperty.GetValue(null, null);
                    if (uiInstance != null)
                    {
                        var windowSize = (Vector2)_ummWindowSizeField.GetValue(uiInstance);
                        if (windowSize.x > 0f)
                        {
                            // mWindowSize.x is the scroll view's MinWidth; window padding is outside it.
                            var scrollbar = GUI.skin.verticalScrollbar;
                            return Mathf.Floor(
                                windowSize.x - scrollbar.fixedWidth - scrollbar.margin.left - containerInset);
                        }
                    }
                }
                catch (Exception exception)
                {
                    DiagnosticLog.Trace("UMM window width lookup failed: " + exception.Message);
                }
            }

            if (!_guiClipVisibleRectResolved)
            {
                _guiClipVisibleRectResolved = true;
                var guiClipType = typeof(GUILayout).Assembly.GetType("UnityEngine.GUIClip");
                _guiClipVisibleRectProperty = guiClipType == null
                    ? null
                    : guiClipType.GetProperty(
                        "visibleRect",
                        BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
            }

            if (_guiClipVisibleRectProperty != null)
            {
                try
                {
                    var visibleRect = (Rect)_guiClipVisibleRectProperty.GetValue(null, null);
                    if (visibleRect.width > 0f)
                    {
                        return Mathf.Floor(visibleRect.width - containerInset);
                    }
                }
                catch (Exception exception)
                {
                    DiagnosticLog.Trace("Settings viewport width lookup failed: " + exception.Message);
                }
            }

            return DefaultSettingsNavigationWidth + 8f + DefaultSettingsContentWidth;
        }

        private static void DrawSettingsTooltip()
        {
            if (Event.current == null || Event.current.type != EventType.Repaint ||
                string.IsNullOrEmpty(GUI.tooltip))
            {
                return;
            }

            var content = new GUIContent(GUI.tooltip);
            var style = GetSettingsTooltipStyle();
            const float minWidth = 240f;
            const float maxWidth = 360f;
            var width = Mathf.Min(maxWidth, Mathf.Max(minWidth, style.CalcSize(content).x));
            var height = style.CalcHeight(content, width);
            var mousePosition = Event.current.mousePosition;
            var rect = new Rect(mousePosition.x - width - 12f, mousePosition.y + 18f, width, height);
            if (rect.x < 6f)
            {
                rect.x = mousePosition.x + 12f;
            }
            if (rect.yMax > Screen.height - 6f)
            {
                rect.y = mousePosition.y - rect.height - 12f;
            }

            GUI.Label(rect, content, style);
        }

        private static void DrawSettingsNavigation(SettingsUiText text)
        {
            DrawSettingsNavigationButton(text.WorkshopMapSettings, "multiplayer");
            DrawSettingsNavigationButton(text.MultiplayerOptions, "behavior");
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

        private static void DrawWorkshopSettings(
            UnityModManager.ModEntry modEntry,
            SettingsUiText text)
        {
            if (_workshopMapSelectionOpen)
            {
                DrawWorkshopMapSelectionPage(text);
                return;
            }

            DrawViewHeading(text.WorkshopMapSettings, text.WorkshopIntro);

            GUILayout.BeginHorizontal();
            GUILayout.BeginVertical(
                GetSettingsSectionPanelStyle(),
                GUILayout.Width(WorkshopInfoColumnWidth));
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

            GUILayout.Space(8f);
            GUILayout.Label(
                text.WorkshopHostLabel,
                GetSettingsStatusKickerStyle(),
                GUILayout.ExpandWidth(true));
            GUILayout.Label(
                text.WorkshopHostHelp,
                GetSettingsHelpStyle(),
                GUILayout.ExpandWidth(true));
            GUILayout.Label(
                text.WorkshopJoinLabel,
                GetSettingsStatusKickerStyle(),
                GUILayout.ExpandWidth(true));
            GUILayout.Label(
                text.WorkshopJoinHelp,
                GetSettingsHelpStyle(),
                GUILayout.ExpandWidth(true));
            GUILayout.Space(8f);
            DrawWorkshopDirectoryStatus(text);
            GUILayout.Space(10f);
            if (GUILayout.Button(
                    new GUIContent(text.WorkshopAdvancedTitle),
                    GetSettingsAdvancedButtonStyle(),
                    GUILayout.ExpandWidth(true)))
            {
                Settings.WorkshopSettingsExpanded = !Settings.WorkshopSettingsExpanded;
            }
            DrawSettingsAdvancedIcon(
                GUILayoutUtility.GetLastRect(),
                Settings.WorkshopSettingsExpanded);
            if (Settings.WorkshopSettingsExpanded)
            {
                Settings.WorkshopCampaignName = DrawTextField(
                    text.WorkshopCampaignName,
                    Settings.WorkshopCampaignName,
                    text.WorkshopCampaignNameHelp);
                Settings.WorkshopSceneName = DrawTextField(
                    text.WorkshopScene,
                    Settings.WorkshopSceneName,
                    text.WorkshopSceneHelp);
            }
            GUILayout.EndVertical();

            GUILayout.Space(WorkshopColumnGap);
            GUILayout.BeginVertical(
                GetSettingsSectionPanelStyle(),
                GUILayout.Width(WorkshopMapColumnWidth));
            DrawWorkshopMapPicker(modEntry, text);
            GUILayout.EndVertical();
            GUILayout.EndHorizontal();
        }

        private static void DrawBehaviorSettings(
            UnityModManager.ModEntry modEntry,
            SettingsUiText text)
        {
            DrawViewHeading(text.MultiplayerOptions, text.MultiplayerOptionsIntro);

            var overlapMeleeEnabled = DrawSettingsToggle(
                Settings.DisablePlayerOverlapHighFive,
                Settings.DisablePlayerOverlapHighFive
                    ? text.OverlapMeleeEnabled
                    : text.OverlapMeleeDisabled,
                GetSettingsToggleStyle());
            if (overlapMeleeEnabled != Settings.DisablePlayerOverlapHighFive)
            {
                Settings.DisablePlayerOverlapHighFive = overlapMeleeEnabled;
                SaveSettings(modEntry);
            }
            DrawIndentedHelp(
                Settings.DisablePlayerOverlapHighFive
                    ? text.OverlapMeleeHelpEnabled
                    : text.OverlapMeleeHelpDisabled);
            GUILayout.Space(10f);

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
            DrawIndentedHelp(text.AfkHelp);
            GUILayout.Space(10f);
        }

        private static void DrawWorkshopDirectoryStatus(SettingsUiText text)
        {
            if (!string.IsNullOrEmpty(WorkshopMapDirectory.LastError))
            {
                GUILayout.Label(text.WorkshopStatusUnavailableKicker, GetSettingsStatusKickerStyle());
                GUILayout.Label(text.WorkshopStatusUnavailable, GetSettingsHelpStyle());
                GUILayout.Label(text.WorkshopStatusUnavailableDetail, GetSettingsHelpStyle());
            }
            else if (WorkshopMapDirectory.IsRefreshing)
            {
                GUILayout.Label(text.WorkshopStatusRefreshingKicker, GetSettingsStatusKickerStyle());
                GUILayout.Label(text.WorkshopStatusRefreshing, GetSettingsHelpStyle());
                GUILayout.Label(text.WorkshopStatusRefreshingDetail, GetSettingsHelpStyle());
            }
            else if (WorkshopMapDirectory.Items.Count == 0)
            {
                GUILayout.Label(text.WorkshopStatusEmptyKicker, GetSettingsStatusKickerStyle());
                GUILayout.Label(FormatWorkshopStatusText(text.WorkshopStatusEmpty), GetSettingsHelpStyle());
            }
            else if (WorkshopMapDirectory.TitleFailureCount > 0)
            {
                GUILayout.Label(text.WorkshopTitleReadFailed, GetSettingsHelpStyle());
            }
        }

        private static string FormatWorkshopStatusText(string value)
        {
            return (value ?? string.Empty)
                .Replace("{count}", WorkshopMapDirectory.Items.Count.ToString())
                .Replace("{subscribed}", WorkshopMapDirectory.SubscribedCount.ToString())
                .Replace("{unreadable}", WorkshopMapDirectory.UnreadableCount.ToString())
                .Replace("{readable}", WorkshopMapDirectory.ReadableCount.ToString());
        }

        private static void DrawWorkshopMapPicker(
            UnityModManager.ModEntry modEntry,
            SettingsUiText text)
        {
            var refreshing = WorkshopMapDirectory.IsRefreshing;
            GUILayout.Label(text.WorkshopId, GetSettingsLabelStyle());
            var manualWorkshopId = GUILayout.TextField(
                Settings.WorkshopId ?? string.Empty,
                GetSettingsTextFieldStyle(),
                GUILayout.ExpandWidth(true));
            if (!string.Equals(manualWorkshopId, Settings.WorkshopId ?? string.Empty, StringComparison.Ordinal))
            {
                Settings.WorkshopId = manualWorkshopId;
            }
            GUILayout.BeginVertical(
                GetSettingsStatusStyle(),
                GUILayout.ExpandWidth(true));
            GUILayout.Label(
                text.WorkshopNotice,
                GetSettingsLabelStyle(),
                GUILayout.ExpandWidth(true));
            GUILayout.EndVertical();
            GUILayout.Label(
                WorkshopMapDirectory.IsRefreshing
                    ? text.WorkshopManualIdHelpRefreshing
                    : WorkshopMapDirectory.Items.Count == 0
                        ? text.WorkshopManualIdHelpEmpty
                        : text.WorkshopManualIdHelpReady,
                GetSettingsHelpStyle(),
                GUILayout.ExpandWidth(true));

            GUILayout.Space(9f);
            GUILayout.Label(text.WorkshopMapSelectLabel, GetSettingsLabelStyle(), GUILayout.ExpandWidth(true));

            if (WorkshopMapDirectory.Items.Count == 0)
            {
                return;
            }

            var selectedItem = FindWorkshopMapItem(Settings.WorkshopId);
            var hasWorkshopId = !string.IsNullOrEmpty((Settings.WorkshopId ?? string.Empty).Trim());
            var triggerTitle = selectedItem == null
                ? !hasWorkshopId
                    ? text.WorkshopMapNoSelection
                    : text.WorkshopMapManualSelection
                : GetWorkshopMapTitle(text, selectedItem);
            var triggerId = selectedItem == null
                ? !hasWorkshopId
                    ? text.WorkshopMapEnterId
                    : text.WorkshopIdPrefix + " " + Settings.WorkshopId.Trim()
                : selectedItem.WorkshopId.ToString();
            var triggerTooltip = selectedItem == null
                ? string.Empty
                : GetWorkshopMapStatusText(text, selectedItem);
            var previousMenuEnabled = GUI.enabled;
            GUI.enabled = !refreshing && WorkshopMapDirectory.Items.Count > 0;
            var triggerStyle = !hasWorkshopId && selectedItem == null
                ? GetSettingsEmptyMapTriggerStyle()
                : GetSettingsMapTriggerStyle();
            var triggerRect = GUILayoutUtility.GetRect(
                GUIContent.none,
                triggerStyle,
                GUILayout.ExpandWidth(true));
            var triggerClicked = GUI.Button(
                triggerRect,
                new GUIContent(string.Empty, triggerTooltip),
                triggerStyle);
            DrawWorkshopMapButtonText(triggerRect, triggerTitle, triggerId);
            if (triggerClicked)
            {
                OpenWorkshopMapSelection();
            }
            GUI.enabled = previousMenuEnabled;
        }

        private static void OpenWorkshopMapSelection()
        {
            _workshopMapSearchText = string.Empty;
            _workshopMapFilter = 0;
            _workshopMapScrollPosition = Vector2.zero;
            _workshopMapSelectionOpen = true;
        }

        private static void DrawWorkshopMapSelectionPage(SettingsUiText text)
        {
            if (Event.current != null && Event.current.type == EventType.KeyDown &&
                Event.current.keyCode == KeyCode.Escape)
            {
                _workshopMapSelectionOpen = false;
                Event.current.Use();
                return;
            }

            GUILayout.BeginHorizontal();
            GUILayout.Label(
                text.WorkshopMapSelectLabel,
                GetSettingsTitleStyle(),
                GUILayout.Height(32f),
                GUILayout.ExpandWidth(true));
            var backClicked = GUILayout.Button(
                new GUIContent("×", text.WorkshopMapCloseTooltip),
                GetSettingsIconButtonStyle(),
                GUILayout.Width(32f),
                GUILayout.Height(32f));
            GUILayout.EndHorizontal();
            GUILayout.Label(
                text.WorkshopMapSelectionIntro,
                GetSettingsHelpStyle(),
                GUILayout.ExpandWidth(true));
            if (backClicked)
            {
                _workshopMapSelectionOpen = false;
                return;
            }
            GUILayout.Space(10f);

            GUILayout.BeginHorizontal();
            GUILayout.Label(text.WorkshopMapSearch, GetSettingsLabelStyle(), GUILayout.Width(48f));
            var searchText = GUILayout.TextField(
                _workshopMapSearchText,
                GetSettingsTextFieldStyle(),
                GUILayout.ExpandWidth(true));
            if (!string.Equals(searchText, _workshopMapSearchText, StringComparison.Ordinal))
            {
                _workshopMapSearchText = searchText;
                _workshopMapScrollPosition = Vector2.zero;
            }

            var previousEnabled = GUI.enabled;
            GUI.enabled = !string.IsNullOrEmpty(_workshopMapSearchText);
            if (GUILayout.Button(
                    new GUIContent("×", text.WorkshopMapSearchClearTooltip),
                    GetSettingsButtonStyle(),
                    GUILayout.Width(30f)))
            {
                _workshopMapSearchText = string.Empty;
                _workshopMapScrollPosition = Vector2.zero;
            }
            GUI.enabled = previousEnabled;
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            var favoriteCount = ParseWorkshopIdList(Settings.WorkshopFavoriteIds).Count;
            var showClearFavorites = _workshopMapFilter == 2 && favoriteCount > 0;
            var allLabel = FormatWorkshopMapFilterLabel(
                text,
                text.WorkshopMapFilterAll,
                GetWorkshopMapSelectionItems(text, 0).Count);
            var favoritesLabel = FormatWorkshopMapFilterLabel(
                text,
                text.WorkshopMapFilterFavorites,
                GetWorkshopMapSelectionItems(text, 2).Count);
            if (showClearFavorites)
            {
                var filterRowRect = GUILayoutUtility.GetRect(
                    GUIContent.none,
                    GUIStyle.none,
                    GUILayout.Height(30f),
                    GUILayout.ExpandWidth(true));
                var actionButtonWidth = 32f;
                var filterTabWidth = Mathf.Max(
                    0f,
                    (filterRowRect.width - actionButtonWidth - WorkshopMapFilterGap * 2f) / 3f);
                var favoriteTabWidth = Mathf.Max(
                    0f,
                    filterTabWidth - actionButtonWidth - WorkshopMapFilterGap * 2f);
                DrawWorkshopMapFilterTab(
                    new Rect(
                        filterRowRect.x,
                        filterRowRect.y,
                        filterTabWidth,
                        filterRowRect.height),
                    0,
                    allLabel);
                DrawWorkshopMapFilterTab(
                    new Rect(
                        filterRowRect.x + filterTabWidth + WorkshopMapFilterGap,
                        filterRowRect.y,
                        filterTabWidth,
                        filterRowRect.height),
                    1,
                    text.WorkshopMapFilterRecent);
                DrawWorkshopMapFilterTab(
                    new Rect(
                        filterRowRect.x + (filterTabWidth + WorkshopMapFilterGap) * 2f,
                        filterRowRect.y,
                        favoriteTabWidth,
                        filterRowRect.height),
                    2,
                    favoritesLabel);
                previousEnabled = GUI.enabled;
                GUI.enabled = !WorkshopMapDirectory.IsRefreshing;
                if (GUI.Button(
                    new Rect(
                            filterRowRect.x + (filterTabWidth + WorkshopMapFilterGap) * 2f +
                            favoriteTabWidth + WorkshopMapFilterGap,
                            filterRowRect.y,
                            actionButtonWidth,
                            filterRowRect.height),
                        new GUIContent("×", text.WorkshopMapClearFavoritesTooltip),
                        GetSettingsIconButtonStyle()))
                {
                    ClearWorkshopMapFavorites();
                }
                GUI.enabled = previousEnabled;

                previousEnabled = GUI.enabled;
                GUI.enabled = !WorkshopMapDirectory.IsRefreshing;
                if (GUI.Button(
                    new Rect(
                            filterRowRect.x + filterRowRect.width - actionButtonWidth,
                            filterRowRect.y,
                            actionButtonWidth,
                            filterRowRect.height),
                        new GUIContent("↻", text.WorkshopRefreshTooltip),
                        GetSettingsIconButtonStyle()))
                {
                    WorkshopMapDirectory.Refresh();
                }
                GUI.enabled = previousEnabled;
            }
            else
            {
                _workshopMapFilter = GUILayout.Toolbar(
                    Mathf.Clamp(_workshopMapFilter, 0, 2),
                    new[] { allLabel, text.WorkshopMapFilterRecent, favoritesLabel },
                    GetSettingsToolbarStyle(),
                    GUILayout.ExpandWidth(true));
                previousEnabled = GUI.enabled;
                GUI.enabled = !WorkshopMapDirectory.IsRefreshing;
                if (GUILayout.Button(
                        new GUIContent("↻", text.WorkshopRefreshTooltip),
                        GetSettingsIconButtonStyle(),
                        GUILayout.Width(32f)))
                {
                    WorkshopMapDirectory.Refresh();
                }
                GUI.enabled = previousEnabled;
            }
            GUILayout.EndHorizontal();

            DrawWorkshopDirectoryStatus(text);
            DrawWorkshopMapSelectionStatus(text);
            var items = GetWorkshopMapSelectionItems(text, _workshopMapFilter);
            _workshopMapScrollPosition = GUILayout.BeginScrollView(
                _workshopMapScrollPosition,
                false,
                false,
                GUIStyle.none,
                GUI.skin.verticalScrollbar,
                GUILayout.Height(320f),
                GUILayout.ExpandWidth(true));
            if (items.Count == 0)
            {
                GUILayout.Label(text.WorkshopMapSelectionEmpty, GetSettingsHelpStyle());
            }
            else
            {
                var verticalScrollbar = GUI.skin.verticalScrollbar;
                var gridWidth = Mathf.Max(
                    0f,
                    SettingsContentWidth - GetSettingsContentPanelStyle().padding.horizontal -
                    verticalScrollbar.fixedWidth - verticalScrollbar.margin.horizontal - 4f);
                var cardWidth = Mathf.Max(0f, (gridWidth - WorkshopMapGridGap) * 0.5f);
                for (var index = 0; index < items.Count; index += 2)
                {
                    GUILayout.BeginHorizontal();
                    DrawWorkshopMapGridItem(text, items[index], cardWidth);
                    GUILayout.Space(WorkshopMapGridGap);
                    if (index + 1 < items.Count)
                    {
                        DrawWorkshopMapGridItem(text, items[index + 1], cardWidth);
                    }
                    else
                    {
                        GUILayout.Space(cardWidth);
                    }
                    GUILayout.EndHorizontal();
                    GUILayout.Space(WorkshopMapGridGap);
                }
            }
            GUILayout.EndScrollView();
        }

        private static void DrawWorkshopMapFilterTab(Rect rect, int filter, string label)
        {
            if (GUI.Toggle(
                    rect,
                    _workshopMapFilter == filter,
                    label,
                    GetSettingsToolbarStyle()))
            {
                _workshopMapFilter = filter;
            }
        }

        private static void DrawWorkshopMapGridItem(
            SettingsUiText text,
            WorkshopMapItem item,
            float cardWidth)
        {
            GUILayout.BeginHorizontal(
                GUILayout.Width(cardWidth),
                GUILayout.Height(WorkshopMapCardButtonHeight));
            var mapButtonWidth = Mathf.Max(
                32f,
                cardWidth - WorkshopMapFavoriteColumnWidth);
            var mapStyle = IsWorkshopMapSelected(item)
                ? GetSettingsMapSelectedOptionStyle()
                : GetSettingsMapOptionStyle();
            var mapRect = GUILayoutUtility.GetRect(
                GUIContent.none,
                mapStyle,
                GUILayout.Width(mapButtonWidth),
                GUILayout.Height(WorkshopMapCardButtonHeight));
            var mapClicked = GUI.Button(
                mapRect,
                new GUIContent(string.Empty, GetWorkshopMapStatusText(text, item)),
                mapStyle);
            DrawWorkshopMapButtonText(
                mapRect,
                GetWorkshopMapTitle(text, item),
                item.WorkshopId.ToString());
            if (mapClicked)
            {
                SelectWorkshopMap(item);
            }

            var favorite = IsWorkshopMapFavorite(item.WorkshopId);
            GUILayout.BeginVertical(
                GUILayout.Width(WorkshopMapFavoriteColumnWidth),
                GUILayout.Height(WorkshopMapCardButtonHeight));
            GUILayout.FlexibleSpace();
            GUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            var favoriteClicked = GUILayout.Button(
                new GUIContent(
                    favorite ? "★" : "☆",
                    favorite
                        ? text.WorkshopMapUnfavoriteTooltip
                        : text.WorkshopMapFavoriteTooltip),
                GetSettingsIconButtonStyle(),
                GUILayout.Width(32f),
                GUILayout.Height(32f));
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();
            GUILayout.FlexibleSpace();
            GUILayout.EndVertical();
            if (favoriteClicked)
            {
                ToggleWorkshopMapFavorite(item.WorkshopId);
            }
            GUILayout.EndHorizontal();
        }

        private static void DrawWorkshopMapButtonText(
            Rect buttonRect,
            string title,
            string id)
        {
            var textRect = new Rect(
                buttonRect.x + 7f,
                buttonRect.y + 4f,
                Mathf.Max(0f, buttonRect.width - 14f),
                20f);
            if (string.IsNullOrEmpty(id))
            {
                textRect.y = buttonRect.y + (buttonRect.height - textRect.height) * 0.5f;
                GUI.Label(textRect, title, GetSettingsMapTitleStyle());
                return;
            }

            GUI.Label(textRect, title, GetSettingsMapTitleStyle());
            GUI.Label(
                new Rect(textRect.x, textRect.y + 20f, textRect.width, 18f),
                id,
                GetSettingsMapIdStyle());
        }

        private static bool IsWorkshopMapSelected(WorkshopMapItem item)
        {
            return item != null &&
                   string.Equals(
                       (Settings.WorkshopId ?? string.Empty).Trim(),
                       item.WorkshopId.ToString(),
                       StringComparison.Ordinal);
        }

        private static void DrawWorkshopMapSelectionStatus(SettingsUiText text)
        {
            var workshopId = (Settings.WorkshopId ?? string.Empty).Trim();
            var selectedItem = FindWorkshopMapItem(workshopId);
            if (selectedItem != null)
            {
                GUILayout.Label(
                    text.WorkshopMapSelectionCurrent
                        .Replace("{title}", GetWorkshopMapTitle(text, selectedItem))
                        .Replace("{id}", selectedItem.WorkshopId.ToString()),
                    GetSettingsStatusKickerStyle(),
                    GUILayout.ExpandWidth(true));
                return;
            }

            GUILayout.Label(
                string.IsNullOrEmpty(workshopId)
                    ? text.WorkshopMapSelectionNone
                    : text.WorkshopMapSelectionManual.Replace("{id}", workshopId),
                GetSettingsStatusKickerStyle(),
                GUILayout.ExpandWidth(true));
        }

        private static string FormatWorkshopMapFilterLabel(
            SettingsUiText text,
            string label,
            int count)
        {
            return label + " (" +
                text.WorkshopMapSelectionCount.Replace("{count}", count.ToString()) +
                ")";
        }

        private static List<WorkshopMapItem> GetWorkshopMapSelectionItems(
            SettingsUiText text,
            int filter)
        {
            var result = new List<WorkshopMapItem>();
            var search = (_workshopMapSearchText ?? string.Empty).Trim();
            var items = WorkshopMapDirectory.Items;
            if (filter == 1 || filter == 2)
            {
                var storedIds = filter == 1
                    ? ParseWorkshopIdList(Settings.WorkshopRecentIds)
                    : ParseWorkshopIdList(Settings.WorkshopFavoriteIds);
                for (var index = 0; index < storedIds.Count; index++)
                {
                    var item = FindWorkshopMapItem(storedIds[index].ToString());
                    if (item != null && MatchesWorkshopMapSearch(text, item, search))
                    {
                        result.Add(item);
                    }
                }
                return result;
            }

            for (var index = 0; index < items.Count; index++)
            {
                if (MatchesWorkshopMapSearch(text, items[index], search))
                {
                    result.Add(items[index]);
                }
            }
            return result;
        }

        private static bool MatchesWorkshopMapSearch(
            SettingsUiText text,
            WorkshopMapItem item,
            string search)
        {
            if (string.IsNullOrEmpty(search))
            {
                return true;
            }

            return GetWorkshopMapTitle(text, item).IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0 ||
                   item.WorkshopId.ToString().IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static void SelectWorkshopMap(WorkshopMapItem item)
        {
            Settings.WorkshopId = item.WorkshopId.ToString();
            SaveSettings(_modEntry);
        }

        internal static void RecordWorkshopMapUsage(ulong workshopId)
        {
            if (Settings == null || workshopId == 0)
            {
                return;
            }

            AddRecentWorkshopMap(workshopId);
            SaveSettings(_modEntry);
        }

        private static bool IsWorkshopMapFavorite(ulong workshopId)
        {
            return ParseWorkshopIdList(Settings.WorkshopFavoriteIds).Contains(workshopId);
        }

        private static void ToggleWorkshopMapFavorite(ulong workshopId)
        {
            var favoriteIds = ParseWorkshopIdList(Settings.WorkshopFavoriteIds);
            if (favoriteIds.Contains(workshopId))
            {
                favoriteIds.Remove(workshopId);
            }
            else
            {
                favoriteIds.Add(workshopId);
            }

            Settings.WorkshopFavoriteIds = SerializeWorkshopIdList(favoriteIds);
            SaveSettings(_modEntry);
        }

        private static void ClearWorkshopMapFavorites()
        {
            if (ParseWorkshopIdList(Settings.WorkshopFavoriteIds).Count == 0)
            {
                return;
            }

            Settings.WorkshopFavoriteIds = string.Empty;
            _workshopMapScrollPosition = Vector2.zero;
            SaveSettings(_modEntry);
        }

        private static void AddRecentWorkshopMap(ulong workshopId)
        {
            var recentIds = ParseWorkshopIdList(Settings.WorkshopRecentIds);
            recentIds.Remove(workshopId);
            recentIds.Insert(0, workshopId);

            Settings.WorkshopRecentIds = SerializeWorkshopIdList(recentIds);
        }

        private static List<ulong> ParseWorkshopIdList(string value)
        {
            var result = new List<ulong>();
            var parts = (value ?? string.Empty).Split(
                new[] { ',', ';' },
                StringSplitOptions.RemoveEmptyEntries);
            for (var index = 0; index < parts.Length; index++)
            {
                ulong workshopId;
                if (UInt64.TryParse(parts[index].Trim(), out workshopId) &&
                    workshopId != 0 && !result.Contains(workshopId))
                {
                    result.Add(workshopId);
                }
            }
            return result;
        }

        private static string SerializeWorkshopIdList(List<ulong> workshopIds)
        {
            var values = new string[workshopIds.Count];
            for (var index = 0; index < workshopIds.Count; index++)
            {
                values[index] = workshopIds[index].ToString();
            }
            return string.Join(",", values);
        }

        private static WorkshopMapItem FindWorkshopMapItem(string workshopId)
        {
            ulong numericWorkshopId;
            if (!UInt64.TryParse((workshopId ?? string.Empty).Trim(), out numericWorkshopId))
            {
                return null;
            }

            var items = WorkshopMapDirectory.Items;
            for (var index = 0; index < items.Count; index++)
            {
                if (items[index].WorkshopId == numericWorkshopId)
                {
                    return items[index];
                }
            }
            return null;
        }

        private static string GetWorkshopMapStatusText(SettingsUiText text, WorkshopMapItem item)
        {
            switch (item.LocalState)
            {
                case WorkshopMapLocalState.InstalledReadable:
                    return text.WorkshopMapStatusInstalledReadable;
                case WorkshopMapLocalState.CampaignUnreadable:
                    return text.WorkshopMapStatusCampaignUnreadable;
                default:
                    return text.WorkshopMapStatusNotInstalled;
            }
        }

        private static string GetWorkshopMapTitle(SettingsUiText text, WorkshopMapItem item)
        {
            if (item.TitleReadPending)
            {
                return text.WorkshopMapTitleLoading;
            }

            if (item.TitleReadFailed)
            {
                return text.WorkshopMapTitleUnavailable;
            }

            return item.Title;
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
            GUILayout.Label(text.FrpStatus + GetLocalizedFrpDirectStatus(text), GetSettingsHelpStyle(), GUILayout.ExpandWidth(true));
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
            return DrawTextField(label, value, string.Empty);
        }

        private static string DrawTextField(string label, string value, string tooltip)
        {
            GUILayout.Space(6f);
            GUILayout.Label(new GUIContent(label, tooltip), GetSettingsLabelStyle());
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
            GUILayout.Label(title, GetSettingsTitleStyle(), GUILayout.Height(24f));
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
                _settingsHelpStyle.normal.textColor = SettingsSecondaryTextColor;
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
                _settingsIndentedHelpStyle.fixedHeight = 0f;
            }
            return _settingsIndentedHelpStyle;
        }

        private static GUIStyle GetSettingsNavigationPanelStyle()
        {
            if (_settingsNavigationPanelStyle == null)
            {
                _settingsNavigationPanelStyle = new GUIStyle(GUI.skin.box);
                _settingsNavigationPanelStyle.normal.background = GetSettingsSolidBackground(
                    ref _settingsNavigationPanelBackground,
                    SettingsNavigationPanelColor);
                _settingsNavigationPanelStyle.border = new RectOffset(7, 7, 7, 7);
                _settingsNavigationPanelStyle.padding = new RectOffset(8, 8, 10, 10);
                _settingsNavigationPanelStyle.margin.left = 0;
                _settingsNavigationPanelStyle.margin.right = 0;
            }
            return _settingsNavigationPanelStyle;
        }

        private static GUIStyle GetSettingsContentPanelStyle()
        {
            if (_settingsContentPanelStyle == null)
            {
                _settingsContentPanelStyle = new GUIStyle(GUI.skin.box);
                _settingsContentPanelStyle.normal.background = GetSettingsSolidBackground(
                    ref _settingsContentPanelBackground,
                    SettingsContentPanelColor);
                _settingsContentPanelStyle.border = new RectOffset(7, 7, 7, 7);
                _settingsContentPanelStyle.padding = new RectOffset(14, 14, 12, 14);
                _settingsContentPanelStyle.margin.left = 0;
                _settingsContentPanelStyle.margin.right = 0;
            }
            return _settingsContentPanelStyle;
        }

        private static GUIStyle GetSettingsNavigationStyle()
        {
            if (_settingsNavigationStyle == null)
            {
                _settingsNavigationStyle = new GUIStyle(GetSettingsButtonStyle());
                _settingsNavigationStyle.alignment = TextAnchor.MiddleCenter;
                _settingsNavigationStyle.fontSize = 14;
                _settingsNavigationStyle.wordWrap = true;
                _settingsNavigationStyle.padding = new RectOffset(0, 0, 5, 5);
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
                _settingsSelectedNavigationStyle.border = new RectOffset(7, 7, 7, 7);
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
                    new Color(0.62f, 0.25f, 0.06f, 1f),
                    new Color(0.82f, 0.40f, 0.09f, 1f));
            }
            return _settingsSelectedNavigationBackground;
        }

        private static Texture2D GetSettingsSelectedNavigationHoverBackground()
        {
            if (_settingsSelectedNavigationHoverBackground == null)
            {
                _settingsSelectedNavigationHoverBackground = CreateSettingsNavigationBackground(
                    new Color(0.78f, 0.34f, 0.07f, 1f),
                    new Color(0.96f, 0.54f, 0.13f, 1f));
            }
            return _settingsSelectedNavigationHoverBackground;
        }

        private static Texture2D GetSettingsSelectedNavigationActiveBackground()
        {
            if (_settingsSelectedNavigationActiveBackground == null)
            {
                _settingsSelectedNavigationActiveBackground = CreateSettingsNavigationBackground(
                    new Color(0.88f, 0.44f, 0.10f, 1f),
                    new Color(1.00f, 0.66f, 0.20f, 1f));
            }
            return _settingsSelectedNavigationActiveBackground;
        }

        private static Texture2D CreateSettingsNavigationBackground(
            Color fillColor,
            Color highlightColor)
        {
            const int textureSize = 18;
            const float cornerRadius = 7f;
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

        private static void ApplySettingsButtonPalette(GUIStyle style)
        {
            style.normal.background = GetSettingsSolidBackground(
                ref _settingsButtonBackground,
                SettingsButtonColor);
            style.hover.background = GetSettingsSolidBackground(
                ref _settingsButtonHoverBackground,
                SettingsButtonHoverColor);
            style.active.background = GetSettingsSolidBackground(
                ref _settingsButtonActiveBackground,
                SettingsButtonActiveColor);
            style.focused.background = _settingsButtonHoverBackground;
            style.onNormal.background = GetSettingsSolidBackground(
                ref _settingsButtonSelectedBackground,
                SettingsButtonSelectedColor);
            style.onHover.background = GetSettingsSolidBackground(
                ref _settingsButtonSelectedHoverBackground,
                SettingsButtonSelectedHoverColor);
            style.onActive.background = GetSettingsSolidBackground(
                ref _settingsButtonSelectedActiveBackground,
                SettingsButtonSelectedActiveColor);
            style.onFocused.background = _settingsButtonSelectedHoverBackground;
            style.normal.textColor = SettingsPrimaryTextColor;
            style.hover.textColor = SettingsPrimaryTextColor;
            style.active.textColor = SettingsPrimaryTextColor;
            style.focused.textColor = SettingsPrimaryTextColor;
            style.onNormal.textColor = SettingsPrimaryTextColor;
            style.onHover.textColor = SettingsPrimaryTextColor;
            style.onActive.textColor = SettingsPrimaryTextColor;
            style.onFocused.textColor = SettingsPrimaryTextColor;
        }

        private static Texture2D GetSettingsSolidBackground(
            ref Texture2D background,
            Color color)
        {
            return GetSettingsSolidBackground(ref background, color, 24, 7f);
        }

        private static Texture2D GetSettingsSolidBackground(
            ref Texture2D background,
            Color color,
            int textureSize,
            float cornerRadius)
        {
            if (background == null)
            {
                background = CreateSettingsRoundedBackground(color, textureSize, cornerRadius);
            }
            return background;
        }

        private static Texture2D CreateSettingsRoundedBackground(
            Color color,
            int textureSize,
            float cornerRadius)
        {
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
                    texture.SetPixel(
                        x,
                        y,
                        distanceX > 0f && distanceY > 0f &&
                            distanceSquared > cornerRadius * cornerRadius
                            ? Color.clear
                            : color);
                }
            }
            texture.Apply();
            texture.wrapMode = TextureWrapMode.Clamp;
            texture.filterMode = FilterMode.Bilinear;
            texture.hideFlags = HideFlags.HideAndDontSave;
            return texture;
        }

        private static void DestroySettingsTexture(ref Texture2D texture)
        {
            if (texture != null)
            {
                UnityEngine.Object.Destroy(texture);
                texture = null;
            }
        }

        private static GUIStyle GetSettingsTitleStyle()
        {
            if (_settingsTitleStyle == null)
            {
                _settingsTitleStyle = new GUIStyle(GUI.skin.label);
                _settingsTitleStyle.fontSize = 18;
                _settingsTitleStyle.fontStyle = FontStyle.Bold;
                _settingsTitleStyle.normal.textColor = SettingsPrimaryTextColor;
            }
            return _settingsTitleStyle;
        }

        private static GUIStyle GetSettingsLabelStyle()
        {
            if (_settingsLabelStyle == null)
            {
                _settingsLabelStyle = new GUIStyle(GUI.skin.label);
                _settingsLabelStyle.fontSize = 14;
                _settingsLabelStyle.wordWrap = true;
                _settingsLabelStyle.normal.textColor = SettingsPrimaryTextColor;
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
                ApplySettingsButtonPalette(_settingsButtonStyle);
                _settingsButtonStyle.border = new RectOffset(7, 7, 7, 7);
                _settingsButtonStyle.fontSize = 14;
                _settingsButtonStyle.fixedHeight = 30f;
                _settingsButtonStyle.alignment = TextAnchor.MiddleCenter;
                _settingsButtonStyle.padding = new RectOffset(0, 0, 0, 0);
                _settingsButtonStyle.wordWrap = false;
                _settingsButtonStyle.contentOffset = Vector2.zero;
            }
            return _settingsButtonStyle;
        }

        private static GUIStyle GetSettingsTextFieldStyle()
        {
            if (_settingsTextFieldStyle == null)
            {
                _settingsTextFieldStyle = new GUIStyle(GUI.skin.textField);
                _settingsTextFieldStyle.normal.background = GetSettingsSolidBackground(
                    ref _settingsTextFieldBackground,
                    SettingsTextFieldColor);
                _settingsTextFieldStyle.hover.background = GetSettingsSolidBackground(
                    ref _settingsTextFieldFocusedBackground,
                    SettingsTextFieldFocusedColor);
                _settingsTextFieldStyle.focused.background = _settingsTextFieldFocusedBackground;
                _settingsTextFieldStyle.active.background = _settingsTextFieldFocusedBackground;
                _settingsTextFieldStyle.normal.textColor = SettingsPrimaryTextColor;
                _settingsTextFieldStyle.hover.textColor = SettingsPrimaryTextColor;
                _settingsTextFieldStyle.focused.textColor = SettingsPrimaryTextColor;
                _settingsTextFieldStyle.active.textColor = SettingsPrimaryTextColor;
                _settingsTextFieldStyle.border = new RectOffset(7, 7, 7, 7);
                _settingsTextFieldStyle.fontSize = 13;
                _settingsTextFieldStyle.fixedHeight = SettingsTextFieldHeight;
            }
            return _settingsTextFieldStyle;
        }

        private static GUIStyle GetSettingsToolbarStyle()
        {
            if (_settingsToolbarStyle == null)
            {
                _settingsToolbarStyle = new GUIStyle(GetSettingsButtonStyle());
                _settingsToolbarStyle.fontSize = 14;
                _settingsToolbarStyle.fixedHeight = 30f;
                _settingsToolbarStyle.alignment = TextAnchor.MiddleCenter;
                _settingsToolbarStyle.padding = new RectOffset(0, 0, 0, 0);
                _settingsToolbarStyle.wordWrap = false;
                _settingsToolbarStyle.contentOffset = Vector2.zero;
            }
            return _settingsToolbarStyle;
        }

        private static GUIStyle GetSettingsSectionPanelStyle()
        {
            if (_settingsSectionPanelStyle == null)
            {
                _settingsSectionPanelStyle = new GUIStyle(GUI.skin.box);
                _settingsSectionPanelStyle.normal.background = GetSettingsSolidBackground(
                    ref _settingsSectionPanelBackground,
                    SettingsSectionPanelColor);
                _settingsSectionPanelStyle.border = new RectOffset(7, 7, 7, 7);
                _settingsSectionPanelStyle.padding = new RectOffset(12, 12, 10, 12);
                _settingsSectionPanelStyle.margin.left = 0;
                _settingsSectionPanelStyle.margin.right = 0;
            }
            return _settingsSectionPanelStyle;
        }

        private static GUIStyle GetSettingsStatusStyle()
        {
            if (_settingsStatusStyle == null)
            {
                _settingsStatusStyle = new GUIStyle(GUI.skin.box);
                _settingsStatusStyle.normal.background = GetSettingsSolidBackground(
                    ref _settingsSectionPanelBackground,
                    SettingsSectionPanelColor);
                _settingsStatusStyle.border = new RectOffset(7, 7, 7, 7);
                _settingsStatusStyle.padding = new RectOffset(8, 8, 7, 7);
            }
            return _settingsStatusStyle;
        }

        private static GUIStyle GetSettingsStatusKickerStyle()
        {
            if (_settingsStatusKickerStyle == null)
            {
                _settingsStatusKickerStyle = new GUIStyle(GetSettingsLabelStyle());
                _settingsStatusKickerStyle.fontSize = 12;
                _settingsStatusKickerStyle.fontStyle = FontStyle.Bold;
            }
            return _settingsStatusKickerStyle;
        }

        private static GUIStyle GetSettingsIconButtonStyle()
        {
            if (_settingsIconButtonStyle == null)
            {
                _settingsIconButtonStyle = new GUIStyle(GetSettingsButtonStyle());
                _settingsIconButtonStyle.normal.background = GetSettingsSolidBackground(
                    ref _settingsRefreshButtonBackground,
                    SettingsButtonColor,
                    32,
                    16f);
                _settingsIconButtonStyle.hover.background = GetSettingsSolidBackground(
                    ref _settingsRefreshButtonHoverBackground,
                    SettingsButtonHoverColor,
                    32,
                    16f);
                _settingsIconButtonStyle.active.background = GetSettingsSolidBackground(
                    ref _settingsRefreshButtonActiveBackground,
                    SettingsButtonActiveColor,
                    32,
                    16f);
                _settingsIconButtonStyle.focused.background = _settingsRefreshButtonHoverBackground;
                _settingsIconButtonStyle.border = new RectOffset(16, 16, 16, 16);
                _settingsIconButtonStyle.fontSize = 18;
                _settingsIconButtonStyle.alignment = TextAnchor.MiddleCenter;
                _settingsIconButtonStyle.fixedHeight = 32f;
                _settingsIconButtonStyle.padding = new RectOffset(0, 0, 0, 0);
                _settingsIconButtonStyle.contentOffset = Vector2.zero;
                _settingsIconButtonStyle.normal.textColor = SettingsAccentColor;
                _settingsIconButtonStyle.hover.textColor = SettingsAccentColor;
                _settingsIconButtonStyle.active.textColor = SettingsPrimaryTextColor;
            }
            return _settingsIconButtonStyle;
        }

        private static GUIStyle GetSettingsMapTriggerStyle()
        {
            if (_settingsMapTriggerStyle == null)
            {
                _settingsMapTriggerStyle = new GUIStyle(GetSettingsButtonStyle());
                _settingsMapTriggerStyle.fontSize = 13;
                _settingsMapTriggerStyle.alignment = TextAnchor.MiddleCenter;
                _settingsMapTriggerStyle.wordWrap = true;
                _settingsMapTriggerStyle.fixedHeight = WorkshopMapTriggerButtonHeight;
                _settingsMapTriggerStyle.padding = new RectOffset(8, 8, 4, 4);
            }
            return _settingsMapTriggerStyle;
        }

        private static GUIStyle GetSettingsEmptyMapTriggerStyle()
        {
            if (_settingsEmptyMapTriggerStyle == null)
            {
                _settingsEmptyMapTriggerStyle = new GUIStyle(GetSettingsMapTriggerStyle());
                _settingsEmptyMapTriggerStyle.fixedHeight = 30f;
                _settingsEmptyMapTriggerStyle.padding = new RectOffset(8, 8, 3, 3);
            }
            return _settingsEmptyMapTriggerStyle;
        }

        private static GUIStyle GetSettingsMapOptionStyle()
        {
            if (_settingsMapOptionStyle == null)
            {
                _settingsMapOptionStyle = new GUIStyle(GetSettingsButtonStyle());
                _settingsMapOptionStyle.fontSize = 12;
                _settingsMapOptionStyle.alignment = TextAnchor.MiddleCenter;
                _settingsMapOptionStyle.wordWrap = false;
                _settingsMapOptionStyle.clipping = TextClipping.Clip;
                _settingsMapOptionStyle.fixedHeight = WorkshopMapCardButtonHeight;
                _settingsMapOptionStyle.padding = new RectOffset(7, 7, 4, 4);
            }
            return _settingsMapOptionStyle;
        }

        private static GUIStyle GetSettingsMapSelectedOptionStyle()
        {
            if (_settingsMapSelectedOptionStyle == null)
            {
                _settingsMapSelectedOptionStyle = new GUIStyle(GetSettingsMapOptionStyle());
                _settingsMapSelectedOptionStyle.fontStyle = FontStyle.Bold;
                _settingsMapSelectedOptionStyle.normal.background = GetSettingsSolidBackground(
                    ref _settingsButtonSelectedBackground,
                    SettingsButtonSelectedColor);
                _settingsMapSelectedOptionStyle.hover.background = GetSettingsSolidBackground(
                    ref _settingsButtonSelectedHoverBackground,
                    SettingsButtonSelectedHoverColor);
                _settingsMapSelectedOptionStyle.active.background = GetSettingsSolidBackground(
                    ref _settingsButtonSelectedActiveBackground,
                    SettingsButtonSelectedActiveColor);
                _settingsMapSelectedOptionStyle.focused.background = _settingsButtonSelectedHoverBackground;
            }
            return _settingsMapSelectedOptionStyle;
        }

        private static GUIStyle GetSettingsMapTitleStyle()
        {
            if (_settingsMapTitleStyle == null)
            {
                _settingsMapTitleStyle = new GUIStyle(GUI.skin.label);
                _settingsMapTitleStyle.fontSize = 12;
                _settingsMapTitleStyle.fontStyle = FontStyle.Bold;
                _settingsMapTitleStyle.alignment = TextAnchor.MiddleCenter;
                _settingsMapTitleStyle.wordWrap = false;
                _settingsMapTitleStyle.clipping = TextClipping.Clip;
                _settingsMapTitleStyle.padding = new RectOffset(0, 0, 0, 0);
                _settingsMapTitleStyle.margin = new RectOffset(0, 0, 0, 0);
                _settingsMapTitleStyle.normal.textColor = SettingsPrimaryTextColor;
            }
            return _settingsMapTitleStyle;
        }

        private static GUIStyle GetSettingsMapIdStyle()
        {
            if (_settingsMapIdStyle == null)
            {
                _settingsMapIdStyle = new GUIStyle(GUI.skin.label);
                _settingsMapIdStyle.fontSize = 10;
                _settingsMapIdStyle.alignment = TextAnchor.MiddleCenter;
                _settingsMapIdStyle.wordWrap = false;
                _settingsMapIdStyle.clipping = TextClipping.Clip;
                _settingsMapIdStyle.padding = new RectOffset(0, 0, 0, 0);
                _settingsMapIdStyle.margin = new RectOffset(0, 0, 0, 0);
                _settingsMapIdStyle.normal.textColor = SettingsMapIdTextColor;
            }
            return _settingsMapIdStyle;
        }

        private static GUIStyle GetSettingsAdvancedButtonStyle()
        {
            if (_settingsAdvancedButtonStyle == null)
            {
                _settingsAdvancedButtonStyle = new GUIStyle(GetSettingsButtonStyle());
                _settingsAdvancedButtonStyle.fontSize = 13;
                _settingsAdvancedButtonStyle.alignment = TextAnchor.MiddleCenter;
                _settingsAdvancedButtonStyle.fixedHeight = 30f;
                _settingsAdvancedButtonStyle.padding = new RectOffset(0, 0, 4, 4);
            }
            return _settingsAdvancedButtonStyle;
        }

        private static void DrawSettingsAdvancedIcon(Rect buttonRect, bool expanded)
        {
            if (Event.current == null || Event.current.type != EventType.Repaint)
            {
                return;
            }

            const float iconSize = 14f;
            var iconRect = new Rect(
                buttonRect.x + 8f,
                buttonRect.y + (buttonRect.height - iconSize) * 0.5f,
                iconSize,
                iconSize);
            GUI.DrawTexture(
                iconRect,
                GetSettingsAdvancedIcon(expanded),
                ScaleMode.ScaleToFit,
                true);
        }

        private static Texture2D GetSettingsAdvancedIcon(bool expanded)
        {
            if (expanded)
            {
                if (_settingsAdvancedExpandedIcon == null)
                {
                    _settingsAdvancedExpandedIcon = CreateSettingsDisclosureIcon(true);
                }
                return _settingsAdvancedExpandedIcon;
            }

            if (_settingsAdvancedCollapsedIcon == null)
            {
                _settingsAdvancedCollapsedIcon = CreateSettingsDisclosureIcon(false);
            }
            return _settingsAdvancedCollapsedIcon;
        }

        private static Texture2D CreateSettingsDisclosureIcon(bool expanded)
        {
            const int textureSize = 16;
            var texture = new Texture2D(textureSize, textureSize, TextureFormat.RGBA32, false);
            for (var y = 0; y < textureSize; y++)
            {
                for (var x = 0; x < textureSize; x++)
                {
                    var filled = false;
                    if (expanded && y >= 4 && y <= 12)
                    {
                        var halfWidth = (y - 4) * 6 / 8;
                        filled = x >= 8 - halfWidth && x <= 8 + halfWidth;
                    }
                    else if (!expanded && x >= 4 && x <= 12)
                    {
                        var halfHeight = 6 - (x - 4) * 6 / 8;
                        filled = y >= 8 - halfHeight && y <= 8 + halfHeight;
                    }

                    texture.SetPixel(x, y, filled ? SettingsAccentColor : Color.clear);
                }
            }
            texture.Apply();
            texture.wrapMode = TextureWrapMode.Clamp;
            texture.filterMode = FilterMode.Point;
            texture.hideFlags = HideFlags.HideAndDontSave;
            return texture;
        }

        private static GUIStyle GetSettingsTooltipStyle()
        {
            if (_settingsTooltipStyle == null)
            {
                _settingsTooltipStyle = new GUIStyle(GUI.skin.box);
                _settingsTooltipStyle.normal.background = GetSettingsSolidBackground(
                    ref _settingsTooltipBackground,
                    SettingsTooltipColor);
                _settingsTooltipStyle.fontSize = 12;
                _settingsTooltipStyle.wordWrap = true;
                _settingsTooltipStyle.alignment = TextAnchor.UpperLeft;
                _settingsTooltipStyle.padding = new RectOffset(8, 8, 6, 6);
                _settingsTooltipStyle.normal.textColor = Color.white;
            }
            return _settingsTooltipStyle;
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
            DestroySettingsTexture(ref _settingsNavigationPanelBackground);
            DestroySettingsTexture(ref _settingsContentPanelBackground);
            DestroySettingsTexture(ref _settingsSectionPanelBackground);
            DestroySettingsTexture(ref _settingsButtonBackground);
            DestroySettingsTexture(ref _settingsButtonHoverBackground);
            DestroySettingsTexture(ref _settingsButtonActiveBackground);
            DestroySettingsTexture(ref _settingsButtonSelectedBackground);
            DestroySettingsTexture(ref _settingsButtonSelectedHoverBackground);
            DestroySettingsTexture(ref _settingsButtonSelectedActiveBackground);
            DestroySettingsTexture(ref _settingsTextFieldBackground);
            DestroySettingsTexture(ref _settingsTextFieldFocusedBackground);
            DestroySettingsTexture(ref _settingsTooltipBackground);
            DestroySettingsTexture(ref _settingsRefreshButtonBackground);
            DestroySettingsTexture(ref _settingsRefreshButtonHoverBackground);
            DestroySettingsTexture(ref _settingsRefreshButtonActiveBackground);
            DestroySettingsTexture(ref _settingsAdvancedCollapsedIcon);
            DestroySettingsTexture(ref _settingsAdvancedExpandedIcon);
            _settingsTitleStyle = null;
            _settingsLabelStyle = null;
            _settingsHelpStyle = null;
            _settingsIndentedHelpStyle = null;
            _settingsToggleStyle = null;
            _settingsButtonStyle = null;
            _settingsTextFieldStyle = null;
            _settingsToolbarStyle = null;
            _settingsSectionPanelStyle = null;
            _settingsStatusStyle = null;
            _settingsStatusKickerStyle = null;
            _settingsIconButtonStyle = null;
            _settingsMapTriggerStyle = null;
            _settingsEmptyMapTriggerStyle = null;
            _settingsMapOptionStyle = null;
            _settingsMapSelectedOptionStyle = null;
            _settingsMapTitleStyle = null;
            _settingsMapIdStyle = null;
            _settingsAdvancedButtonStyle = null;
            _settingsTooltipStyle = null;
            _workshopMapSelectionOpen = false;
            _workshopMapScrollPosition = Vector2.zero;
            _workshopMapSearchText = string.Empty;
            _workshopMapFilter = 0;
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

        private static string GetLocalizedFrpDirectStatus(SettingsUiText text)
        {
            var status = _behaviour == null ? "Disabled" : _behaviour.GetFrpDirectStatus();
            if (text == null)
            {
                return _behaviour == null ? "Mod disabled" : status;
            }

            if (string.Equals(status, "Disabled", StringComparison.Ordinal))
            {
                return text.FrpStatusDisabled;
            }

            const string listeningPrefix = "Listening on UDP ";
            if (status.StartsWith(listeningPrefix, StringComparison.Ordinal))
            {
                return text.FrpStatusListening + status.Substring(listeningPrefix.Length);
            }

            if (string.Equals(status, "Waiting to connect", StringComparison.Ordinal))
            {
                return text.FrpStatusWaiting;
            }

            return status;
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

            if (settings.DiagnosticSettingsVersion < 10)
            {
                settings.DisablePlayerOverlapHighFive = true;
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

            if (settings.DiagnosticSettingsVersion < 13)
            {
                // Recent maps used to mean maps clicked in the picker. Rebuild this
                // list from completed Workshop loads under the new meaning.
                settings.WorkshopRecentIds = string.Empty;
            }

            NormalizeSettingsPresentation(settings);

            settings.DiagnosticSettingsVersion = CurrentDiagnosticSettingsVersion;
        }

        private static void NormalizeSettingsPresentation(DiagnosticSettings settings)
        {
            if (!string.Equals(settings.SettingsPanel, "frp", StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(settings.SettingsPanel, "behavior", StringComparison.OrdinalIgnoreCase) &&
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

            settings.WorkshopFavoriteIds = settings.WorkshopFavoriteIds ?? string.Empty;
            settings.WorkshopRecentIds = settings.WorkshopRecentIds ?? string.Empty;
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
