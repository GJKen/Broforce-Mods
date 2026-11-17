using HarmonyLib;

namespace BroforceBugFix
{
    internal static class AlienEggCleanupFix
    {
        private const string HarmonyId = "GJKen.BroforceBugFix.AlienEggCleanup";
        private static Harmony _harmony;

        internal static void Apply()
        {
            if (_harmony != null) return;
            var harmony = new Harmony(HarmonyId);
            harmony.Patch(AccessTools.Method(typeof(Map), "RemoveAlienEggExplosive", null, null),
                new HarmonyMethod(AccessTools.Method(typeof(AlienEggCleanupFix), "Prefix", null, null)),
                null, null, null);
            harmony.Patch(AccessTools.Method(typeof(Map), "RemoveDisturbedWildLife", null, null),
                new HarmonyMethod(AccessTools.Method(typeof(AlienEggCleanupFix), "ListenerPrefix", null, null)),
                null, null, null);
            _harmony = harmony;
            Plugin.Log("Alien egg cleanup fix enabled.");
        }

        internal static void Remove()
        {
            if (_harmony == null) return;
            _harmony.UnpatchAll(HarmonyId);
            _harmony = null;
            Plugin.Log("Alien egg cleanup fix disabled.");
        }

        private static bool Prefix()
        {
            // Broforce's Map.OnDestroy clears this list; later egg destruction would throw.
            // Skip only the unavailable-list removal so AlienEggExplosive.OnDestroy
            // can continue into base cleanup. Remove when the upstream method handles null.
            return Map.alienEggs != null;
        }

        private static bool ListenerPrefix(WildLife wildLifeObject)
        {
            // AlienEggListener can outlive Map's disturbedWildLife list too.
            // Keep addedToMap cleanup; remove this guard when upstream handles the null list.
            return !(wildLifeObject is AlienEggListener) || Map.disturbedWildLife != null;
        }
    }
}
