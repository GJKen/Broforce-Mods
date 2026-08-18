using UnityModManagerNet;
using System;
using UnityEngine;

namespace BroforceOnlineDiagnostics
{
    public static class Plugin
    {
        private const int CurrentDiagnosticSettingsVersion = 2;
        private static UnityModManager.ModEntry _modEntry;
        private static DiagnosticsBehaviour _behaviour;

        internal static DiagnosticSettings Settings { get; private set; }

        internal static bool ShouldSkipLateHeroResponse(int playerNum)
        {
            return _behaviour != null && _behaviour.ShouldSkipLateHeroResponse(playerNum);
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
                DiagnosticLog.Info("Plugin loaded. Observation-only mode is active.");
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

            Settings.EnableOnlineWorkshopInjection = GUILayout.Toggle(
                Settings.EnableOnlineWorkshopInjection,
                "Inject configured workshop map into online level switching");
            GUILayout.Label("Workshop ID");
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
            GUILayout.Label("Diagnostic session ID (use the same value on both clients; optional)");
            Settings.DiagnosticSessionId = GUILayout.TextField(
                Settings.DiagnosticSessionId ?? string.Empty,
                GUILayout.Width(260f));
            GUILayout.Label("Diagnostic label (optional; only used in log names)");
            Settings.DiagnosticRole = GUILayout.TextField(
                Settings.DiagnosticRole ?? string.Empty,
                GUILayout.Width(260f));
            GUILayout.Label("Changes are saved when UMM settings are saved, the Mod is toggled, or the game exits normally.");
        }

        private static void OnSaveGUI(UnityModManager.ModEntry modEntry)
        {
            SaveSettings(modEntry);
        }

        private static void SaveSettings(UnityModManager.ModEntry modEntry)
        {
            if (Settings == null)
            {
                return;
            }

            try
            {
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

            settings.DiagnosticSettingsVersion = CurrentDiagnosticSettingsVersion;
        }

        private static void StopDiagnostics()
        {
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
