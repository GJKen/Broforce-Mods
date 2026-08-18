using UnityModManagerNet;
using System;
using UnityEngine;

namespace BroforceOnlineDiagnostics
{
    public static class Plugin
    {
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
                    modEntry.Logger.LogException("Diagnostic settings load failed; defaults are active", settingsException);
                }

                if (Settings == null)
                {
                    Settings = new DiagnosticSettings();
                }

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

            return true;
        }

        private static bool OnUnload(UnityModManager.ModEntry modEntry)
        {
            StopDiagnostics();
            DiagnosticLog.Info("Plugin unloaded.");
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

            GUILayout.Label("Broforce Online Diagnostics 0.3.0");
            Settings.EnableOnlineWorkshopInjection = GUILayout.Toggle(
                Settings.EnableOnlineWorkshopInjection,
                "Inject configured workshop map into online level switching");
            GUILayout.Label("Workshop ID (test default: 456121589)");
            Settings.WorkshopId = GUILayout.TextField(
                Settings.WorkshopId ?? string.Empty,
                GUILayout.Width(260f));
            GUILayout.Label("Workshop campaign name (optional)");
            Settings.WorkshopCampaignName = GUILayout.TextField(
                Settings.WorkshopCampaignName ?? string.Empty,
                GUILayout.Width(260f));
            GUILayout.Label("Custom level scene (default: Test Evan2)");
            Settings.WorkshopSceneName = GUILayout.TextField(
                Settings.WorkshopSceneName ?? string.Empty,
                GUILayout.Width(260f));
            GUILayout.Label("Changes are saved when the UMM settings panel is saved.");
        }

        private static void OnSaveGUI(UnityModManager.ModEntry modEntry)
        {
            if (Settings != null)
            {
                UnityModManager.ModSettings.Save(Settings, modEntry);
            }
        }

        private static void StopDiagnostics()
        {
            HarmonyDiagnostics.Stop();

            if (_behaviour == null)
            {
                return;
            }

            _behaviour.Stop();
            _behaviour = null;
        }
    }
}
