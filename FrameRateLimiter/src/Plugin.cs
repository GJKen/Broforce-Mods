using System;
using System.Globalization;
using System.Text;
using UnityModManagerNet;
using UnityEngine;

namespace FrameRateLimiter
{
    public static class Plugin
    {
        private const int MinimumFrameRate = 1;
        private const int MaximumFrameRate = 1000;

        private static bool _modEnabled;
        private static bool _originalSettingsCaptured;
        private static int _originalTargetFrameRate;
        private static int _originalVSyncCount;
        private static string _targetFrameRateText;

        internal static UnityModManager.ModEntry ModEntry { get; private set; }
        internal static FrameRateLimiterSettings Settings { get; private set; }

        public static bool Load(UnityModManager.ModEntry modEntry)
        {
            if (modEntry == null)
            {
                return false;
            }

            ModEntry = modEntry;
            TooltipOverlay.Create();
            modEntry.OnToggle = OnToggle;
            modEntry.OnUnload = OnUnload;
            modEntry.OnGUI = OnGUI;
            modEntry.OnSaveGUI = OnSaveGUI;

            try
            {
                Settings = UnityModManager.ModSettings.Load<FrameRateLimiterSettings>(modEntry);
            }
            catch (Exception exception)
            {
                Settings = new FrameRateLimiterSettings();
                modEntry.Logger.LogException("Frame-rate limiter settings load failed; defaults are active", exception);
            }

            if (Settings == null)
            {
                Settings = new FrameRateLimiterSettings();
            }

            Settings.Normalize();
            _targetFrameRateText = FormatInputFrameRate(Settings.TargetFrameRate);
            modEntry.Logger.Log("Frame Rate Limiter loaded.");
            return true;
        }

        private static bool OnToggle(UnityModManager.ModEntry modEntry, bool enabled)
        {
            try
            {
                if (enabled)
                {
                    CaptureOriginalSettings();
                }

                _modEnabled = enabled;
                ApplyConfiguredSettings();
                SaveSettings(modEntry);
                Log(enabled ? "Frame-rate limit enabled." : "Frame-rate limit disabled; original display settings restored.");
                return true;
            }
            catch (Exception exception)
            {
                _modEnabled = false;
                RestoreOriginalSettings();
                modEntry.Logger.LogException(
                    enabled ? "Frame-rate limiter activation failed" : "Frame-rate limiter deactivation failed",
                    exception);
                return false;
            }
        }

        private static bool OnUnload(UnityModManager.ModEntry modEntry)
        {
            try
            {
                _modEnabled = false;
                RestoreOriginalSettings();
                TooltipOverlay.Stop();
                SaveSettings(modEntry);
                ModEntry = null;
                Settings = null;
                return true;
            }
            catch (Exception exception)
            {
                modEntry.Logger.LogException("Frame-rate limiter unload failed", exception);
                return false;
            }
        }

        private static void OnGUI(UnityModManager.ModEntry modEntry)
        {
            if (Settings == null)
            {
                return;
            }

            DrawLanguageButton(modEntry);

            var previousTargetFrameRate = Settings.TargetFrameRate;
            var previousDisableVSync = Settings.DisableVSync;

            GUILayout.Label(Text("Target frame rate", "目标帧率"));
            GUILayout.BeginHorizontal();
            var editedText = GUILayout.TextField(_targetFrameRateText, GUILayout.Width(90f));
            if (!string.Equals(editedText, _targetFrameRateText, StringComparison.Ordinal))
            {
                _targetFrameRateText = SanitizeFrameRateText(editedText);
                int parsedFrameRate;
                if (!string.IsNullOrEmpty(_targetFrameRateText))
                {
                    parsedFrameRate = _targetFrameRateText.Length > 4 ||
                        !int.TryParse(
                            _targetFrameRateText,
                            NumberStyles.Integer,
                            CultureInfo.InvariantCulture,
                            out parsedFrameRate)
                        ? MaximumFrameRate
                        : parsedFrameRate;
                    Settings.TargetFrameRate = NormalizeFrameRate(parsedFrameRate);
                    _targetFrameRateText = FormatInputFrameRate(Settings.TargetFrameRate);
                }
            }

            GUILayout.Label(Text("FPS (1-1000)", "FPS（1-1000）"));
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            DrawPresetButton(30);
            DrawPresetButton(60);
            DrawPresetButton(120);
            DrawPresetButton(144);
            DrawPresetButton(240);
            if (GUILayout.Button(Text("Unlimited", "不限制"), GUILayout.Width(80f)))
            {
                Settings.TargetFrameRate = -1;
                if (string.IsNullOrEmpty(_targetFrameRateText))
                {
                    _targetFrameRateText = FormatInputFrameRate(FrameRateLimiterSettings.DefaultFrameRate);
                }
            }
            GUILayout.EndHorizontal();

            var vsyncTooltip = Text(
                "VSync synchronizes frames with your monitor refresh rate. It can reduce screen tearing, but may add input latency and can override the custom FPS limit.",
                "垂直同步会让游戏帧率与显示器刷新率同步，可以减少画面撕裂，但可能增加输入延迟，并可能覆盖自定义帧率限制。");
            var vsyncLabelText = Settings.DisableVSync
                ? Text(
                    "Disable VSync while this Mod is enabled",
                    "启用此 Mod 时关闭 VSync")
                : Text(
                    "Use the original VSync setting while this Mod is enabled",
                    "启用此 Mod 时使用原始 VSync 设置");
            var vsyncLabel = new GUIContent(
                vsyncLabelText,
                vsyncTooltip);
            Settings.DisableVSync = GUILayout.Toggle(Settings.DisableVSync, vsyncLabel);
            var vsyncRect = GUILayoutUtility.GetLastRect();
            var vsyncScreenPosition = GUIUtility.GUIToScreenPoint(vsyncRect.position);
            var vsyncScreenRect = new Rect(vsyncScreenPosition, vsyncRect.size);

            if (previousTargetFrameRate != Settings.TargetFrameRate ||
                previousDisableVSync != Settings.DisableVSync)
            {
                try
                {
                    ApplyConfiguredSettings();
                }
                catch (Exception exception)
                {
                    modEntry.Logger.LogException("Applying frame-rate settings failed", exception);
                }
            }

            if (_modEnabled)
            {
                GUILayout.Label(
                    Text("Active: target=", "已启用：目标=") +
                    FormatFrameRate(Application.targetFrameRate) +
                    Text(", VSync=", "，VSync=") +
                    QualitySettings.vSyncCount);
            }
            else
            {
                GUILayout.Label(Text("Inactive", "未启用"));
            }

            if (Event.current != null && Event.current.type == EventType.Repaint)
            {
                var mousePosition = new Vector2(
                    Input.mousePosition.x,
                    Screen.height - Input.mousePosition.y);
                if (vsyncScreenRect.Contains(mousePosition))
                {
                    TooltipOverlay.DrawInGui(vsyncTooltip, vsyncRect);
                }
            }
        }

        private static void DrawLanguageButton(UnityModManager.ModEntry modEntry)
        {
            GUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            if (GUILayout.Button(
                Settings.UseChinese ? "English" : "中文",
                GUILayout.Width(90f)))
            {
                Settings.UseChinese = !Settings.UseChinese;
                SaveSettings(modEntry);
            }
            GUILayout.EndHorizontal();
        }

        private static string Text(string english, string chinese)
        {
            return Settings != null && Settings.UseChinese ? chinese : english;
        }

        private static void DrawPresetButton(int frameRate)
        {
            if (GUILayout.Button(frameRate.ToString(CultureInfo.InvariantCulture), GUILayout.Width(48f)))
            {
                Settings.TargetFrameRate = frameRate;
                _targetFrameRateText = FormatFrameRate(frameRate);
            }
        }

        private static void ApplyConfiguredSettings()
        {
            if (!_modEnabled || Settings == null)
            {
                RestoreOriginalSettings();
                return;
            }

            CaptureOriginalSettings();
            Settings.Normalize();
            Application.targetFrameRate = Settings.TargetFrameRate;
            QualitySettings.vSyncCount = Settings.DisableVSync
                ? 0
                : _originalVSyncCount;
        }

        private static void CaptureOriginalSettings()
        {
            if (_originalSettingsCaptured)
            {
                return;
            }

            _originalTargetFrameRate = Application.targetFrameRate;
            _originalVSyncCount = QualitySettings.vSyncCount;
            _originalSettingsCaptured = true;
        }

        private static void RestoreOriginalSettings()
        {
            if (!_originalSettingsCaptured)
            {
                return;
            }

            Application.targetFrameRate = _originalTargetFrameRate;
            QualitySettings.vSyncCount = _originalVSyncCount;
            _originalSettingsCaptured = false;
        }

        private static int NormalizeFrameRate(int frameRate)
        {
            if (frameRate == -1)
            {
                return -1;
            }

            if (frameRate < MinimumFrameRate)
            {
                return MinimumFrameRate;
            }

            return Math.Max(MinimumFrameRate, Math.Min(MaximumFrameRate, frameRate));
        }

        private static string SanitizeFrameRateText(string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return string.Empty;
            }

            var builder = new StringBuilder(text.Length);
            for (var index = 0; index < text.Length; index++)
            {
                var character = text[index];
                if (character >= '0' && character <= '9')
                {
                    builder.Append(character);
                }
            }

            return builder.ToString();
        }

        private static string FormatInputFrameRate(int frameRate)
        {
            return (frameRate < MinimumFrameRate
                    ? FrameRateLimiterSettings.DefaultFrameRate
                    : frameRate)
                .ToString(CultureInfo.InvariantCulture);
        }

        private static string FormatFrameRate(int frameRate)
        {
            return frameRate.ToString(CultureInfo.InvariantCulture);
        }

        private static void OnSaveGUI(UnityModManager.ModEntry modEntry)
        {
            SaveSettings(modEntry);
        }

        private static void SaveSettings(UnityModManager.ModEntry modEntry)
        {
            if (modEntry == null || Settings == null)
            {
                return;
            }

            try
            {
                Settings.Normalize();
                UnityModManager.ModSettings.Save(Settings, modEntry);
            }
            catch (Exception exception)
            {
                modEntry.Logger.LogException("Frame-rate limiter settings save failed", exception);
            }
        }

        private static void Log(string message)
        {
            if (ModEntry != null)
            {
                ModEntry.Logger.Log(message);
            }
        }
    }
}
