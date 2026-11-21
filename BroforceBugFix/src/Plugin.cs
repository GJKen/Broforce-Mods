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
            modEntry.OnSaveGUI = SaveSettings;
            try
            {
                Settings = UnityModManager.ModSettings.Load<BugFixSettings>(modEntry) ??
                           new BugFixSettings();
            }
            catch (Exception exception)
            {
                Settings = new BugFixSettings();
                modEntry.Logger.LogException(
                    "Bug-fix settings load failed; defaults are active",
                    exception);
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
                ApplyConfiguredFixes();
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

            var changed = DrawToggle(ref Settings.EnableAllFixes,
                "Enable all bug fixes");
            GUILayout.Space(6f);
            GUILayout.Label("Individual fixes");
            changed |= DrawToggle(ref Settings.EnableDoodadCrateReentryFix,
                "Prevent recursive explosive-ammo crate collapse");
            changed |= DrawToggle(ref Settings.EnableLobbyJoinStreamFix,
                "Clear a stalled previous room's network stream when joining");
            changed |= DrawToggle(ref Settings.EnableFaceHuggerDetachFix,
                "Fix snake/facehugger detach errors and repeated bleeding");
            changed |= DrawToggle(ref Settings.EnableAlienEggCleanupFix,
                "Fix alien egg cleanup after the map is unloaded");
            changed |= DrawToggle(ref Settings.EnableSandstormSetupFix,
                "Wait for map data before sandstorm setup");

            var doodadCrateActive = _modEnabled &&
                                    Settings.EnableAllFixes &&
                                    Settings.EnableDoodadCrateReentryFix;
            GUILayout.Label(
                "Explosive-ammo crate fix: " +
                (doodadCrateActive ? "active" : "inactive"));

            if (changed)
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

        private static bool DrawToggle(ref bool value, string label)
        {
            var previous = value;
            value = GUILayout.Toggle(value, label);
            return previous != value;
        }

        private static void ApplyConfiguredFixes()
        {
            var settings = Settings;
            var enabled = _modEnabled && settings != null && settings.EnableAllFixes;
            if (enabled && settings.EnableDoodadCrateReentryFix)
                DoodadCrateReentryFix.Apply();
            else
                DoodadCrateReentryFix.Remove();

            if (enabled && settings.EnableLobbyJoinStreamFix)
                LobbyJoinStreamFix.Apply();
            else
                LobbyJoinStreamFix.Remove();

            if (enabled && settings.EnableFaceHuggerDetachFix)
                FaceHuggerDetachFix.Apply();
            else
                FaceHuggerDetachFix.Remove();

            if (enabled && settings.EnableAlienEggCleanupFix)
                AlienEggCleanupFix.Apply();
            else
                AlienEggCleanupFix.Remove();

            if (enabled && settings.EnableSandstormSetupFix)
                SandstormSetupFix.Apply();
            else
                SandstormSetupFix.Remove();
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
