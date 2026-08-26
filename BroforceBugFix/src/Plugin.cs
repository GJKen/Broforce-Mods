using System;
using UnityModManagerNet;
using UnityEngine;

namespace BroforceBugFix
{
    public static class Plugin
    {
        private static bool _modEnabled;

        internal static UnityModManager.ModEntry ModEntry { get; private set; }
        internal static BugFixSettings Settings { get; private set; }

        public static bool Load(UnityModManager.ModEntry modEntry)
        {
            if (modEntry == null)
            {
                return false;
            }

            ModEntry = modEntry;
            modEntry.OnToggle = OnToggle;
            modEntry.OnUnload = OnUnload;
            modEntry.OnGUI = OnGUI;
            modEntry.OnSaveGUI = OnSaveGUI;
            try
            {
                Settings = UnityModManager.ModSettings.Load<BugFixSettings>(modEntry);
            }
            catch (Exception exception)
            {
                Settings = new BugFixSettings();
                modEntry.Logger.LogException(
                    "Bug-fix settings load failed; defaults are active",
                    exception);
            }

            if (Settings == null)
            {
                Settings = new BugFixSettings();
            }

            Log("Broforce Bug Fix loaded.");
            return true;
        }

        private static bool OnToggle(UnityModManager.ModEntry modEntry, bool enabled)
        {
            try
            {
                _modEnabled = enabled;
                ApplyConfiguredFixes();
                SaveSettings(modEntry);
                return true;
            }
            catch (Exception exception)
            {
                modEntry.Logger.LogException(
                    enabled ? "Bug-fix activation failed" : "Bug-fix deactivation failed",
                    exception);
                return false;
            }
        }

        private static bool OnUnload(UnityModManager.ModEntry modEntry)
        {
            try
            {
                _modEnabled = false;
                RemoveAllFixes();
                SaveSettings(modEntry);
                ModEntry = null;
                Settings = null;
                return true;
            }
            catch (Exception exception)
            {
                modEntry.Logger.LogException("Bug-fix unload failed", exception);
                return false;
            }
        }

        private static void OnGUI(UnityModManager.ModEntry modEntry)
        {
            if (Settings == null)
            {
                return;
            }

            var previousMaster = Settings.EnableAllFixes;
            var previousDoodadCrate = Settings.EnableDoodadCrateReentryFix;

            Settings.EnableAllFixes = GUILayout.Toggle(
                Settings.EnableAllFixes,
                "Enable all bug fixes");
            GUILayout.Space(6f);
            GUILayout.Label("Individual fixes");
            Settings.EnableDoodadCrateReentryFix = GUILayout.Toggle(
                Settings.EnableDoodadCrateReentryFix,
                "Prevent recursive explosive-ammo crate collapse");

            var doodadCrateActive = _modEnabled &&
                                    Settings.EnableAllFixes &&
                                    Settings.EnableDoodadCrateReentryFix;
            GUILayout.Label(
                "Explosive-ammo crate fix: " +
                (doodadCrateActive ? "active" : "inactive"));

            if (previousMaster != Settings.EnableAllFixes ||
                previousDoodadCrate != Settings.EnableDoodadCrateReentryFix)
            {
                try
                {
                    ApplyConfiguredFixes();
                }
                catch (Exception exception)
                {
                    modEntry.Logger.LogException(
                        "Applying changed bug-fix settings failed",
                        exception);
                }
            }
        }

        private static void OnSaveGUI(UnityModManager.ModEntry modEntry)
        {
            SaveSettings(modEntry);
        }

        private static void ApplyConfiguredFixes()
        {
            var settings = Settings;
            var enableDoodadCrateFix = _modEnabled &&
                                       settings != null &&
                                       settings.EnableAllFixes &&
                                       settings.EnableDoodadCrateReentryFix;
            if (enableDoodadCrateFix)
            {
                DoodadCrateReentryFix.Apply();
            }
            else
            {
                DoodadCrateReentryFix.Remove();
            }
        }

        private static void RemoveAllFixes()
        {
            DoodadCrateReentryFix.Remove();
        }

        private static void SaveSettings(UnityModManager.ModEntry modEntry)
        {
            if (modEntry == null || Settings == null)
            {
                return;
            }

            try
            {
                UnityModManager.ModSettings.Save(Settings, modEntry);
            }
            catch (Exception exception)
            {
                modEntry.Logger.LogException("Bug-fix settings save failed", exception);
            }
        }

        internal static void Log(string message)
        {
            var modEntry = ModEntry;
            if (modEntry != null)
            {
                modEntry.Logger.Log(message);
            }
        }

        internal static void Warning(string message)
        {
            var modEntry = ModEntry;
            if (modEntry != null)
            {
                modEntry.Logger.Warning(message);
            }
        }
    }
}
