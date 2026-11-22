using System;
using System.Reflection;
using HarmonyLib;
using UnityEngine;
using UnityModManagerNet;

[assembly: AssemblyTitle("EnemyAIEnhancement")]
[assembly: AssemblyVersion("0.19.4.0")]

namespace EnemyAIEnhancement
{
    public sealed class Settings : UnityModManager.ModSettings
    {
        public bool RememberTargets = true;
        public bool PursueTargets = true;
        public bool AvoidGrenades = true;

        public override void Save(UnityModManager.ModEntry entry) { Save(this, entry); }
    }

    public static class Plugin
    {
        internal const string HarmonyId = "GJKen.EnemyAIEnhancement";
        internal static Settings Settings;
        internal static bool Enabled;
        private static Harmony harmony;

        public static bool Load(UnityModManager.ModEntry entry)
        {
            try
            {
                Settings = UnityModManager.ModSettings.Load<Settings>(entry);
                harmony = new Harmony(HarmonyId);
                harmony.PatchAll(Assembly.GetExecutingAssembly());
                entry.OnToggle = Toggle;
                entry.OnUnload = Unload;
                entry.OnGUI = Draw;
                entry.OnSaveGUI = delegate(UnityModManager.ModEntry mod) { Settings.Save(mod); };
                Enabled = true;
                entry.Logger.Log("Enemy AI Enhancement 0.19.4 loaded: exact native boss queries and action-specific movement checks.");
                return true;
            }
            catch (Exception error)
            {
                if (harmony != null) harmony.UnpatchAll(HarmonyId);
                entry.Logger.LogException("Enemy AI initialization failed", error);
                return false;
            }
        }

        private static bool Toggle(UnityModManager.ModEntry entry, bool enabled)
        {
            Enabled = enabled;
            Controller.Reset(true);
            return true;
        }

        private static bool Unload(UnityModManager.ModEntry entry)
        {
            Enabled = false;
            Controller.Reset(true);
            harmony.UnpatchAll(HarmonyId);
            return true;
        }

        private static void Draw(UnityModManager.ModEntry entry)
        {
            bool memory = Settings.RememberTargets;
            bool pursuit = Settings.PursueTargets;
            bool grenades = Settings.AvoidGrenades;
            Settings.RememberTargets = GUILayout.Toggle(memory, "Remember lost targets (6 seconds)");
            Settings.PursueTargets = GUILayout.Toggle(pursuit, "Pursue targets (16 tiles, native routes where supported)");
            Settings.AvoidGrenades = GUILayout.Toggle(grenades, "Retreat from explosive grenades");
            GUILayout.Label("Only the enemy owner makes decisions; replicas use native synchronized actions.");
            if (memory != Settings.RememberTargets || pursuit != Settings.PursueTargets || grenades != Settings.AvoidGrenades)
                Controller.Reset(true);
        }

        // Small counters for runtime verification; no per-frame logging or retained game objects.
        public static string GetStatus()
        {
            return "enabled=" + Enabled + "; tracked=" + Controller.TrackedCount +
                "; plans=" + Controller.Plans + "; searches=" + Controller.Searches +
                "; retreats=" + Controller.Retreats + "; received=" + Controller.ReceivedQueues +
                "; worms=" + WormController.Observations.Count + "; wormSelections=" + WormController.Selections + "; wormReceived=" + WormController.Received +
                "; vehicles=" + VehicleController.Observations.Count + "; vehicleSelections=" + VehicleController.Selections + "; vehicleReceived=" + VehicleController.Received +
                "; transports=" + TransportController.Observations.Count + "; transportSelections=" + TransportController.Selections + "; transportPlans=" + TransportController.Plans + "; transportReceived=" + TransportController.Received +
                "; bosses=" + BossController.Observations.Count + "; bossSelections=" + BossController.Selections + "; bossCycles=" + BossController.Cycles + "; bossReceived=" + BossController.Received;
        }
    }
}
