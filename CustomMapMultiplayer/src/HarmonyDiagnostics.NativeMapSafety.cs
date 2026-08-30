using HarmonyLib;
using System;
using System.Reflection;
using UnityEngine;

namespace CustomMapMultiplayer
{
    // Guards native map objects whose serialized references or teardown order are outside this mod's control.
    internal static partial class HarmonyDiagnostics
    {
        private static readonly FieldInfo DoodadCrateContainsPresentField =
            typeof(DoodadCrate).GetField(
                "containsPresent",
                BindingFlags.NonPublic | BindingFlags.Instance);
        private static readonly FieldInfo DoodadCrateContainsMuscleTemplePrizeField =
            typeof(DoodadCrate).GetField(
                "containsMuscleTemplePrize",
                BindingFlags.NonPublic | BindingFlags.Instance);
        private static readonly FieldInfo DoodadCratePickupField =
            typeof(DoodadCrate).GetField(
                "pickup",
                BindingFlags.NonPublic | BindingFlags.Instance);
        private static readonly FieldInfo PickupableControllerInstanceField =
            typeof(PickupableController).GetField(
                "instance",
                BindingFlags.NonPublic | BindingFlags.Static);

        private static bool _projectileTeardownGuardLogged;
        private static bool _torturedVillagerFallbackLogged;
        private static bool _doodadCrateSetupDelayLogged;
        private static bool _doodadCrateMissingPickupLogged;

        private static void PatchNativeMapObjectSafety()
        {
            var patchedCount = 0;
            patchedCount += PatchNativeMapSafetyMethod(
                typeof(Map).GetMethod(
                    "RemoveProjectile",
                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static,
                    null,
                    new[] { typeof(Projectile) },
                    null),
                "MapRemoveProjectileSafetyPrefix");
            patchedCount += PatchNativeMapSafetyMethod(
                typeof(TorturedVillager).GetMethod(
                    "Awake",
                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance,
                    null,
                    Type.EmptyTypes,
                    null),
                "TorturedVillagerAwakeSafetyPrefix");
            if (PickupableControllerInstanceField == null)
            {
                DiagnosticLog.Warning(
                    "Native map safety could not resolve PickupableController.instance.");
            }
            else
            {
                patchedCount += PatchNativeMapSafetyMethod(
                    typeof(DoodadCrate).GetMethod(
                        "SetupBlockAtStart",
                        BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance,
                        null,
                        Type.EmptyTypes,
                        null),
                    "DoodadCrateSetupBlockAtStartSafetyPrefix");
            }

            if (DoodadCrateContainsPresentField == null ||
                DoodadCrateContainsMuscleTemplePrizeField == null ||
                DoodadCratePickupField == null)
            {
                DiagnosticLog.Warning(
                    "Native map safety could not resolve DoodadCrate reward fields.");
            }
            else
            {
                patchedCount += PatchNativeMapSafetyMethod(
                    typeof(DoodadCrate).GetMethod(
                        "DestroyBlockInternal",
                        BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance,
                        null,
                        new[] { typeof(bool) },
                        null),
                    "DoodadCrateDestroyBlockInternalSafetyPrefix");
            }

            DiagnosticLog.Info(
                "Native map object safety enabled; patched methods=" + patchedCount + ".");
        }

        private static int PatchNativeMapSafetyMethod(MethodInfo target, string prefixName)
        {
            var prefix = typeof(HarmonyDiagnostics).GetMethod(
                prefixName,
                BindingFlags.NonPublic | BindingFlags.Static);
            if (target == null || prefix == null)
            {
                DiagnosticLog.Warning(
                    "Native map safety target could not be resolved: " + prefixName + ".");
                return 0;
            }

            try
            {
                _harmony.Patch(target, new HarmonyMethod(prefix), null, null, null);
                return 1;
            }
            catch (Exception exception)
            {
                DiagnosticLog.Warning(
                    "Native map safety patch failed for " + DescribeMethod(target) + ": " +
                    exception);
                return 0;
            }
        }

        private static bool MapRemoveProjectileSafetyPrefix(Projectile projectile)
        {
            if (Map.projectiles != null && Map.damageableProjectiles != null)
            {
                return true;
            }

            if (Map.projectiles != null)
            {
                Map.projectiles.Remove(projectile);
            }
            if (Map.damageableProjectiles != null)
            {
                Map.damageableProjectiles.Remove(projectile);
            }

            if (!_projectileTeardownGuardLogged)
            {
                _projectileTeardownGuardLogged = true;
                DiagnosticLog.Warning(
                    "NATIVE_MAP guarded projectile deregistration during Map teardown.");
            }
            return false;
        }

        private static void TorturedVillagerAwakeSafetyPrefix(TorturedVillager __instance)
        {
            if (!IsWorkshopOnlineSession() || __instance == null)
            {
                return;
            }

            var maleMissing = __instance.maleVillagerPrefab == null;
            var femaleMissing = __instance.femaleVillagerPrefab == null;
            if (maleMissing == femaleMissing)
            {
                return;
            }

            if (maleMissing)
            {
                __instance.maleVillagerPrefab = __instance.femaleVillagerPrefab;
            }
            else
            {
                __instance.femaleVillagerPrefab = __instance.maleVillagerPrefab;
            }

            if (!_torturedVillagerFallbackLogged)
            {
                _torturedVillagerFallbackLogged = true;
                DiagnosticLog.Warning(
                    "WORKSHOP_OBJECT repaired captured-villager prefab fallback; missing=" +
                    (maleMissing ? "male" : "female") + ".");
            }
        }

        private static bool DoodadCrateSetupBlockAtStartSafetyPrefix(DoodadCrate __instance)
        {
            if (!IsWorkshopOnlineSession() || __instance == null)
            {
                return true;
            }

            var currentMap = Map.Instance;
            var pickupController =
                PickupableControllerInstanceField.GetValue(null) as PickupableController;
            var belongsToCurrentMap = currentMap != null &&
                __instance.transform != null &&
                __instance.transform.IsChildOf(currentMap.transform);
            if (Map.MapData != null && pickupController != null && belongsToCurrentMap)
            {
                return true;
            }

            // FallingBlock clears this flag before invoking SetupBlockAtStart; restore it for a ready frame.
            __instance.setupBlockAtStart = true;
            if (!_doodadCrateSetupDelayLogged)
            {
                _doodadCrateSetupDelayLogged = true;
                DiagnosticLog.Warning(
                    "WORKSHOP_OBJECT delayed DoodadCrate setup during Map transition.");
            }
            return false;
        }

        private static void DoodadCrateDestroyBlockInternalSafetyPrefix(DoodadCrate __instance)
        {
            if (!IsWorkshopOnlineSession() || __instance == null)
            {
                return;
            }

            var containsPresent = (bool)DoodadCrateContainsPresentField.GetValue(__instance);
            var containsMuscleTemplePrize =
                (bool)DoodadCrateContainsMuscleTemplePrizeField.GetValue(__instance);
            if (!containsPresent && !containsMuscleTemplePrize)
            {
                return;
            }

            var pickup = DoodadCratePickupField.GetValue(__instance) as Pickupable;
            if (pickup != null)
            {
                return;
            }

            DoodadCrateContainsPresentField.SetValue(__instance, false);
            DoodadCrateContainsMuscleTemplePrizeField.SetValue(__instance, false);
            if (!_doodadCrateMissingPickupLogged)
            {
                _doodadCrateMissingPickupLogged = true;
                DiagnosticLog.Warning(
                    "WORKSHOP_OBJECT cleared an invalid DoodadCrate reward before collapse.");
            }
        }
    }
}
