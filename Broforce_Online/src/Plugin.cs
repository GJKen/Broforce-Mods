using UnityModManagerNet;
using System;
using UnityEngine;

namespace BroforceOnlineDiagnostics
{
    public static class Plugin
    {
        private const int CurrentDiagnosticSettingsVersion = 4;
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
            Settings.DisableOnlineAfkSpectatorMode = GUILayout.Toggle(
                Settings.DisableOnlineAfkSpectatorMode,
                "Disable automatic AFK spectator mode in online games");
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
            GUILayout.Space(10f);
            Settings.EnableFrpDirectPrototype = GUILayout.Toggle(
                Settings.EnableFrpDirectPrototype,
                "Enable FRP Direct transport prototype");
            Settings.EnableFrpDirectGameLayer = GUILayout.Toggle(
                Settings.EnableFrpDirectGameLayer,
                "Route Broforce rooms and RPC through FRP Direct (experimental)");
            GUILayout.Label("FRP Direct role");
            var roleIndex = string.Equals(
                Settings.FrpDirectRole,
                "client",
                StringComparison.OrdinalIgnoreCase) ? 1 : 0;
            roleIndex = GUILayout.Toolbar(roleIndex, new[] { "Host", "Client" }, GUILayout.Width(260f));
            Settings.FrpDirectRole = roleIndex == 1 ? "client" : "host";
            if (roleIndex == 0)
            {
                GUILayout.Label("Local UDP listen port");
                Settings.FrpDirectLocalPort = DrawPortField(Settings.FrpDirectLocalPort);
            }
            else
            {
                GUILayout.Label("FRP server endpoint (host:port)");
                Settings.FrpDirectServerEndpoint = GUILayout.TextField(
                    Settings.FrpDirectServerEndpoint ?? string.Empty,
                    GUILayout.Width(260f));
            }
            GUILayout.Label("FRP room password (optional)");
            Settings.FrpDirectRoomPassword = GUILayout.PasswordField(
                Settings.FrpDirectRoomPassword ?? string.Empty,
                '*',
                GUILayout.Width(260f));
            GUILayout.Label("FRP Direct status: " + GetFrpDirectStatus());
            if (GUILayout.Button("Apply / restart FRP Direct", GUILayout.Width(260f)))
            {
                SaveSettings(modEntry);
                ApplyFrpDirectSettings(true);
            }
            GUILayout.Label("Changes are saved when UMM settings are saved, the Mod is toggled, or the game exits normally.");
        }

        private static void OnSaveGUI(UnityModManager.ModEntry modEntry)
        {
            SaveSettings(modEntry);
            ApplyFrpDirectSettings(false);
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

        internal static bool ShouldUseFrpDirectGameLayer
        {
            get
            {
                return Settings != null &&
                       Settings.EnableFrpDirectPrototype &&
                       Settings.EnableFrpDirectGameLayer;
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

            if (!string.Equals(settings.FrpDirectRole, "client", StringComparison.OrdinalIgnoreCase))
            {
                settings.FrpDirectRole = "host";
            }

            if (settings.FrpDirectLocalPort < 1 || settings.FrpDirectLocalPort > 65535)
            {
                settings.FrpDirectLocalPort = 27045;
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
