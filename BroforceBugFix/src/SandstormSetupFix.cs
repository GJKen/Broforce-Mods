using HarmonyLib;

namespace BroforceBugFix
{
    internal static class SandstormSetupFix
    {
        private const string HarmonyId = "GJKen.BroforceBugFix.SandstormSetup";
        private static Harmony _harmony;

        internal static void Apply()
        {
            if (_harmony != null) return;
            var harmony = new Harmony(HarmonyId);
            harmony.Patch(AccessTools.Method(typeof(Effects.Sandstorm), "Update", null, null),
                new HarmonyMethod(typeof(SandstormSetupFix), "Prefix", null),
                null, null, null);
            _harmony = harmony;
            Plugin.Log("Sandstorm setup fix enabled.");
        }

        internal static void Remove()
        {
            if (_harmony == null) return;
            _harmony.UnpatchAll(HarmonyId);
            _harmony = null;
            Plugin.Log("Sandstorm setup fix disabled.");
        }

        private static bool Prefix(bool ____hasSetup)
        {
            // Broforce clears MapData before the old scene's last Update. Native setup
            // marks _hasSetup before reading MapData, so defer instead of disabling it.
            // Remove when upstream waits for MapData before committing setup.
            return ____hasSetup || Map.isEditing || Map.MapData != null;
        }
    }
}
