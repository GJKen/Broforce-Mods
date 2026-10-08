using System;
using UnityModManagerNet;
using UnityEngine;

namespace TestMod
{
    public static class Plugin
    {
        private static TestModBehaviour _behaviour;

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

            var chinese = IsChinese();
            GUILayout.Label(chinese ? "测试 Mod" : "Test Mod");
            GUILayout.Label(chinese ? "测试用：清除当前屏幕内属于本机的敌人。" :
                "Test tool: kills enemies currently visible to this game instance.");

            GUILayout.Label(chinese ? "语言 / Language" : "Language / 语言");
            var languageIndex = GetLanguageIndex(Settings.Language);
            var nextLanguageIndex = GUILayout.SelectionGrid(
                languageIndex,
                new[] { chinese ? "跟随系统" : "Follow System", "English", "中文" },
                3);
            if (nextLanguageIndex != languageIndex)
            {
                Settings.Language = nextLanguageIndex == 1 ? "en" :
                    nextLanguageIndex == 2 ? "zh" : "system";
                SaveSettings(modEntry);
                chinese = IsChinese();
            }

            var nextEnabled = GUILayout.Toggle(
                Settings.EnableAutoKill,
                chinese ? "启用自动击杀" : "Enable automatic killing");
            if (nextEnabled != Settings.EnableAutoKill)
            {
                Settings.EnableAutoKill = nextEnabled;
                if (_behaviour != null)
                {
                    _behaviour.OnAutoKillSettingChanged();
                }

                SaveSettings(modEntry);
            }

            GUILayout.Label(Settings.EnableAutoKill
                ? (chinese ? "状态：自动击杀已启用" : "Status: automatic killing enabled")
                : (chinese ? "状态：自动击杀已禁用" : "Status: automatic killing disabled"));

            GUI.enabled = Settings.EnableAutoKill && _behaviour != null;
            if (GUILayout.Button(chinese ? "立即清除当前屏幕敌人" : "Kill visible enemies now"))
            {
                KillVisibleEnemiesNow();
            }

            GUI.enabled = true;
            if (_behaviour != null)
            {
                GUILayout.Label((chinese ? "上次清除数量：" : "Last kill count: ") +
                    _behaviour.LastKillCount);
            }
        }

        private static void Start()
        {
            if (_behaviour == null)
            {
                _behaviour = TestModBehaviour.Create();
            }
        }

        private static void Stop()
        {
            if (_behaviour != null)
            {
                _behaviour.Stop();
                _behaviour = null;
            }
        }

        private static void NormalizeSettings()
        {
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
