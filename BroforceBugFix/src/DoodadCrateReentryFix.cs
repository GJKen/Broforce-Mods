using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace BroforceBugFix
{
    internal static class DoodadCrateReentryFix
    {
        private const string HarmonyId = "GJKen.BroforceBugFix.DoodadCrateReentry";
        private static readonly object Sync = new object();
        private static readonly HashSet<DoodadCrate> CollapsingCrates =
            new HashSet<DoodadCrate>(ReferenceComparer<DoodadCrate>.Instance);

        private static Harmony _harmony;
        private static int _suppressedReentryCount;

        internal static void Apply()
        {
            lock (Sync)
            {
                if (_harmony != null)
                {
                    return;
                }

                var target = typeof(DoodadCrate).GetMethod(
                    "ActuallyCollapse",
                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance,
                    null,
                    new[] { typeof(float), typeof(float), typeof(bool) },
                    null);
                var prefix = typeof(DoodadCrateReentryFix).GetMethod(
                    "ActuallyCollapsePrefix",
                    BindingFlags.NonPublic | BindingFlags.Static);
                var finalizer = typeof(DoodadCrateReentryFix).GetMethod(
                    "ActuallyCollapseFinalizer",
                    BindingFlags.NonPublic | BindingFlags.Static);

                if (target == null || prefix == null || finalizer == null)
                {
                    throw new MissingMethodException(
                        "Could not resolve DoodadCrate.ActuallyCollapse or its patch methods.");
                }

                var harmony = new Harmony(HarmonyId);
                harmony.Patch(
                    target,
                    new HarmonyMethod(prefix) { priority = Priority.First },
                    null,
                    null,
                    new HarmonyMethod(finalizer) { priority = Priority.Last });
                _suppressedReentryCount = 0;
                _harmony = harmony;
            }

            Plugin.Log(
                "DoodadCrate collapse reentry guard enabled for " +
                "DoodadCrate.ActuallyCollapse.");
        }

        internal static void Remove()
        {
            Harmony harmony;
            lock (Sync)
            {
                harmony = _harmony;
            }

            if (harmony != null)
            {
                harmony.UnpatchAll(HarmonyId);
                lock (Sync)
                {
                    if (ReferenceEquals(_harmony, harmony))
                    {
                        _harmony = null;
                        CollapsingCrates.Clear();
                    }
                }
                Plugin.Log(
                    "DoodadCrate collapse reentry guard disabled; suppressedReentries=" +
                    _suppressedReentryCount + ".");
            }
        }

        private static bool ActuallyCollapsePrefix(DoodadCrate __instance, ref bool __state)
        {
            __state = false;
            if (ReferenceEquals(__instance, null))
            {
                return true;
            }

            lock (Sync)
            {
                if (CollapsingCrates.Contains(__instance))
                {
                    _suppressedReentryCount++;
                    Plugin.Warning(
                        "Suppressed recursive DoodadCrate.ActuallyCollapse call; " +
                        "referenceId=" + RuntimeHelpers.GetHashCode(__instance) +
                        "; suppressedReentries=" + _suppressedReentryCount + ".");
                    return false;
                }

                CollapsingCrates.Add(__instance);
                __state = true;
                return true;
            }
        }

        private static Exception ActuallyCollapseFinalizer(
            Exception __exception,
            DoodadCrate __instance,
            bool __state)
        {
            if (__state && !ReferenceEquals(__instance, null))
            {
                lock (Sync)
                {
                    CollapsingCrates.Remove(__instance);
                }
            }

            return __exception;
        }

        private sealed class ReferenceComparer<T> : IEqualityComparer<T>
            where T : class
        {
            internal static readonly ReferenceComparer<T> Instance =
                new ReferenceComparer<T>();

            public bool Equals(T x, T y)
            {
                return ReferenceEquals(x, y);
            }

            public int GetHashCode(T value)
            {
                return RuntimeHelpers.GetHashCode(value);
            }
        }
    }
}
