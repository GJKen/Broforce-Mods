using System;
using HarmonyLib;
using UnityModManagerNet;
using UnityEngine;

namespace TestMod
{
    public static partial class Plugin
    {
        private static TestModBehaviour _behaviour;
        private static GameSpeedController _speedController;
        private static Harmony _harmony;
        private static int _bindingTarget;
        private static int _ignoreHotkeysThroughFrame;

        internal static bool CapturingHotkey
        {
            get { return _bindingTarget != 0 || Time.frameCount <= _ignoreHotkeysThroughFrame; }
        }

        public static bool IsGameSpeedEnabled
        {
            get { return _speedController != null && Settings.EnableGameSpeed; }
        }

        public static float GameSpeed
        {
            get { return Settings == null ? 1f : Settings.GameSpeed; }
        }

        public static bool IsGamePaused
        {
            get { return _speedController != null && _speedController.IsPaused; }
        }

        internal static bool IsGameFrameFrozen
        {
            get { return _speedController != null && _speedController.IsFrozen; }
        }

        public static int SteppedFrames
        {
            get { return _speedController == null ? 0 : _speedController.SteppedFrames; }
        }

        public static void SetGameSpeedEnabled(bool enabled)
        {
            if (Settings == null) return;
            Settings.EnableGameSpeed = enabled;
            if (_speedController != null) _speedController.ApplySettings();
            SaveSettings(ModEntry);
        }

        public static void SetGameSpeed(float speed)
        {
            if (float.IsNaN(speed) || float.IsInfinity(speed) || speed < 0f || speed > 4f)
                throw new ArgumentOutOfRangeException("speed", "Use a speed between 0 and 4.");
            if (Settings == null) return;
            Settings.EnableGameSpeed = true;
            if (speed > 0f) Settings.GameSpeed = Mathf.Max(0.01f, speed);
            if (_speedController != null) _speedController.SetPaused(speed == 0f);
            SaveSettings(ModEntry);
        }

        public static void SetGamePaused(bool paused)
        {
            if (Settings == null || _speedController == null) return;
            Settings.EnableGameSpeed = true;
            _speedController.SetPaused(paused);
            SaveSettings(ModEntry);
        }

        public static bool StepOneFrame()
        {
            if (_speedController == null || PauseController.pauseStatus != PauseStatus.UnPaused)
                return false;
            Settings.EnableGameSpeed = true;
            _speedController.ApplySettings();
            var requested = _speedController.RequestFrameStep();
            SaveSettings(ModEntry);
            return requested;
        }

        internal static UnityModManager.ModEntry ModEntry { get; private set; }
        internal static TestModSettings Settings { get; private set; }
        internal static bool AutoKillEnabled
        {
            get { return Settings != null && Settings.EnableAutoKill; }
        }

        public static bool IsAutoKillEnabled
        {
            get { return AutoKillEnabled; }
        }

        public static int LastKillCount
        {
            get { return _behaviour == null ? 0 : _behaviour.LastKillCount; }
        }

        public static void SetAutoKillEnabled(bool enabled)
        {
            if (Settings == null)
            {
                return;
            }

            Settings.EnableAutoKill = enabled;
            if (_behaviour != null)
            {
                _behaviour.OnAutoKillSettingChanged();
            }

            SaveSettings(ModEntry);
        }

        public static int KillVisibleEnemiesNow()
        {
            if (!AutoKillEnabled || _behaviour == null)
            {
                return 0;
            }

            return _behaviour.KillVisibleEnemies();
        }

        public static bool Load(UnityModManager.ModEntry modEntry)
        {
            ModEntry = modEntry;
            modEntry.OnToggle = OnToggle;
            modEntry.OnUnload = OnUnload;
            modEntry.OnGUI = OnGUI;
            modEntry.OnSaveGUI = OnSaveGUI;

            Settings = UnityModManager.ModSettings.Load<TestModSettings>(modEntry);
            if (Settings == null)
            {
                Settings = new TestModSettings();
            }

            NormalizeSettings();
            Log("Test Mod loaded.");
            return true;
        }

        private static bool OnToggle(UnityModManager.ModEntry modEntry, bool enabled)
        {
            if (enabled)
            {
                Start();
            }
            else
            {
                Stop();
            }

            SaveSettings(modEntry);
            return true;
        }

        private static bool OnUnload(UnityModManager.ModEntry modEntry)
        {
            SaveSettings(modEntry);
            Stop();
            ReleaseSettingsUI();
            Settings = null;
            ModEntry = null;
            return true;
        }

        private static void OnSaveGUI(UnityModManager.ModEntry modEntry)
        {
            SaveSettings(modEntry);
        }

        private static void OnGUI(UnityModManager.ModEntry modEntry)
        {
            if (Settings == null)
            {
                return;
            }

            DrawSettingsUI(modEntry);
        }

        private static void Start()
        {
            if (_behaviour == null)
            {
                _behaviour = TestModBehaviour.Create();
                _behaviour.gameObject.tag = "Unpausable";
                _harmony = new Harmony("GJKen.TestMod.GameSpeed");
                _harmony.PatchAll(typeof(Plugin).Assembly);
                _speedController = _behaviour.gameObject.AddComponent<GameSpeedController>();
                _speedController.ApplySettings();
            }
        }

        private static void Stop()
        {
            _bindingTarget = 0;
            if (_speedController != null)
            {
                _speedController.ReleaseTime();
                _speedController.enabled = false;
                _speedController = null;
            }
            if (_harmony != null)
            {
                _harmony.UnpatchAll(_harmony.Id);
                _harmony = null;
            }
            if (_behaviour != null)
            {
                _behaviour.Stop();
                _behaviour = null;
            }
        }

        private static void NormalizeSettings()
        {
            if (Settings.SettingsPanel != "speed" && Settings.SettingsPanel != "enemies" &&
                Settings.SettingsPanel != "language") Settings.SettingsPanel = "speed";
            if (float.IsNaN(Settings.GameSpeed) || float.IsInfinity(Settings.GameSpeed))
                Settings.GameSpeed = 1f;
            Settings.GameSpeed = Mathf.Clamp(Settings.GameSpeed, 0.01f, 4f);
            Settings.FrameStepRepeatRate = Mathf.Clamp(Settings.FrameStepRepeatRate, 1, 60);
            if (!Enum.IsDefined(typeof(KeyCode), Settings.PauseKey)) Settings.PauseKey = KeyCode.None;
            if (!Enum.IsDefined(typeof(KeyCode), Settings.FrameStepKey) ||
                Settings.FrameStepKey == Settings.PauseKey) Settings.FrameStepKey = KeyCode.None;
            if (!string.Equals(Settings.Language, "en", StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(Settings.Language, "zh", StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(Settings.Language, "system", StringComparison.OrdinalIgnoreCase))
            {
                Settings.Language = "system";
            }
        }

        private static int GetLanguageIndex(string language)
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

        private static bool IsChinese()
        {
            if (string.Equals(Settings.Language, "zh", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            if (string.Equals(Settings.Language, "en", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            return Application.systemLanguage.ToString().StartsWith(
                "Chinese", StringComparison.OrdinalIgnoreCase);
        }

        private static void SaveSettings(UnityModManager.ModEntry modEntry)
        {
            if (Settings != null && modEntry != null)
            {
                UnityModManager.ModSettings.Save(Settings, modEntry);
            }
        }

        internal static void Log(string message)
        {
            if (ModEntry != null)
            {
                ModEntry.Logger.Log(message);
            }
        }
    }
}
