using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Reflection;
using Networking;
using UnityEngine;

namespace BroforceOnlineDiagnostics
{
    internal static partial class HarmonyDiagnostics
    {
        private const float WorkshopPickupRetryDelaySeconds = 0.5f;

        private static bool _workshopCrateDeterminismLogged;
        private static bool _workshopRemotePickupScanLogged;
        private static bool _workshopDuplicatePickupCollectLogged;
        private static bool _workshopAmmoFullRpcSuppressionLogged;
        private static readonly FieldInfo PickupableListField =
            typeof(PickupableController).GetField(
                "pickupables",
                BindingFlags.NonPublic | BindingFlags.Static);
        private static readonly Dictionary<TestVanDammeAnim, HashSet<Pickupable>>
            WorkshopAmmoFullContacts =
                new Dictionary<TestVanDammeAnim, HashSet<Pickupable>>();
        private static readonly Dictionary<TestVanDammeAnim, HashSet<Pickupable>>
            CurrentWorkshopAmmoFullContacts =
                new Dictionary<TestVanDammeAnim, HashSet<Pickupable>>();

        private static void PatchWorkshopPickupSynchronization()
        {
            PatchWorkshopPickupMethod(
                typeof(CrateBlock),
                "CreatePickupable",
                "PrepareWorkshopCratePickupPrefix",
                null);
            PatchWorkshopPickupMethod(
                typeof(TestVanDammeAnim),
                "PickupPickupables",
                "AllowWorkshopPickupScanPrefix",
                null);
            PatchWorkshopPickupMethod(
                typeof(Pickupable),
                "Collect",
                "AllowWorkshopPickupCollectPrefix",
                "CompleteWorkshopPickupCollectPostfix");
            PatchWorkshopPickupMethod(
                typeof(PickupableController),
                "UsePickupables",
                "UseWorkshopPickupablesPrefix",
                null);
        }

        private static void PatchWorkshopPickupMethod(
            Type targetType,
            string methodName,
            string prefixName,
            string postfixName)
        {
            try
            {
                var target = targetType.GetMethod(
                    methodName,
                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static);
                if (target == null)
                {
                    DiagnosticLog.Warning(
                        "Workshop pickup synchronization target not found: " +
                        targetType.Name + "." + methodName + ".");
                    return;
                }

                var prefix = string.IsNullOrEmpty(prefixName)
                    ? null
                    : new HarmonyMethod(typeof(HarmonyDiagnostics).GetMethod(
                        prefixName,
                        BindingFlags.NonPublic | BindingFlags.Static));
                var postfix = string.IsNullOrEmpty(postfixName)
                    ? null
                    : new HarmonyMethod(typeof(HarmonyDiagnostics).GetMethod(
                        postfixName,
                        BindingFlags.NonPublic | BindingFlags.Static));
                _harmony.Patch(target, prefix, postfix, null, null);
                DiagnosticLog.Info(
                    "Workshop pickup synchronization enabled for " +
                    targetType.Name + "." + methodName + ".");
            }
            catch (Exception exception)
            {
                DiagnosticLog.Warning(
                    "Workshop pickup synchronization patch failed for " +
                    targetType.Name + "." + methodName + ": " + exception);
            }
        }

        private static void PrepareWorkshopCratePickupPrefix(CrateBlock __instance)
        {
            if (!IsWorkshopOnlineSession() || __instance == null ||
                __instance.ammoType != PockettedSpecialAmmoType.Standard ||
                __instance.RegisterPickUpOnStartBecauseItTurnsOutBlocksAreDoodadsNow)
            {
                return;
            }

            // The native method turns Standard crates into a locally random unlocked
            // pickup. Keeping this flag set skips that choice on every reset/spawn,
            // so peers construct the same prefab and network-object sequence.
            __instance.RegisterPickUpOnStartBecauseItTurnsOutBlocksAreDoodadsNow = true;
            if (!_workshopCrateDeterminismLogged)
            {
                _workshopCrateDeterminismLogged = true;
                DiagnosticLog.Info(
                    "Workshop online standard ammo crates now keep deterministic standard contents.");
            }
        }

        private static bool AllowWorkshopPickupScanPrefix(TestVanDammeAnim __instance)
        {
            if (!IsWorkshopOnlineSession() || __instance == null || __instance.IsMine)
            {
                return true;
            }

            // Every peer updates remote hero mirrors. Letting those mirrors scan
            // local pickups makes a client send collection RPCs for another peer.
            if (!_workshopRemotePickupScanLogged)
            {
                _workshopRemotePickupScanLogged = true;
                DiagnosticLog.Info(
                    "Suppressed Workshop pickup scanning for remote hero mirrors.");
            }
            return false;
        }

        private static bool AllowWorkshopPickupCollectPrefix(Pickupable __instance)
        {
            if (!IsWorkshopOnlineSession() || __instance == null ||
                (!__instance.collected && __instance.gameObject.activeInHierarchy))
            {
                return true;
            }

            // Reliable RPCs queued before the first collection can still target the
            // disabled object. The native method has no idempotency guard and would
            // replay its sound/effect on every invocation.
            if (!_workshopDuplicatePickupCollectLogged)
            {
                _workshopDuplicatePickupCollectLogged = true;
                DiagnosticLog.Info(
                    "Suppressed a duplicate Workshop pickup collection RPC.");
            }
            return false;
        }

        private static bool UseWorkshopPickupablesPrefix(
            TestVanDammeAnim self,
            float range,
            float x,
            float y,
            bool onlyAmmo)
        {
            if (!IsWorkshopOnlineSession())
            {
                return true;
            }

            if (self == null || !self.IsMine)
            {
                return false;
            }

            var pickupables = PickupableListField == null
                ? null
                : PickupableListField.GetValue(null) as List<Pickupable>;
            if (pickupables == null)
            {
                DiagnosticLog.Warning(
                    "Workshop pickup synchronization could not read PickupableController.pickupables; native scanning remains active.");
                return true;
            }

            HashSet<Pickupable> previousAmmoFullContacts;
            HashSet<Pickupable> currentAmmoFullContacts;
            if (WorkshopAmmoFullContacts.TryGetValue(self, out previousAmmoFullContacts))
            {
                currentAmmoFullContacts = CurrentWorkshopAmmoFullContacts[self];
                currentAmmoFullContacts.Clear();
            }
            else
            {
                currentAmmoFullContacts = null;
            }

            for (var index = pickupables.Count - 1; index >= 0; index--)
            {
                var pickupable = pickupables[index];
                if (pickupable == null ||
                    !pickupable.gameObject.activeInHierarchy ||
                    (pickupable.pickupType != PickupType.Ammo && onlyAmmo))
                {
                    continue;
                }

                var deltaX = pickupable.X - x;
                var deltaY = pickupable.Y + pickupable.yOffset - y;
                if (Mathf.Abs(deltaX) - range >= pickupable.collectionRadius ||
                    Mathf.Abs(deltaY) - range >= pickupable.collectionRadius ||
                    pickupable.collected)
                {
                    continue;
                }

                if (pickupable.pickupType == PickupType.Ammo &&
                    GameModeController.GameMode != GameMode.ExplosionRun &&
                    self.IsAmmoFull())
                {
                    // Preserve one native full-ammo feedback per continuous contact
                    // without sending a TargetAll RPC that cannot consume the pickup.
                    if (previousAmmoFullContacts == null)
                    {
                        previousAmmoFullContacts = new HashSet<Pickupable>();
                        currentAmmoFullContacts = new HashSet<Pickupable>();
                        WorkshopAmmoFullContacts.Add(self, previousAmmoFullContacts);
                        CurrentWorkshopAmmoFullContacts.Add(self, currentAmmoFullContacts);
                    }
                    currentAmmoFullContacts.Add(pickupable);
                    if (!previousAmmoFullContacts.Contains(pickupable))
                    {
                        if (pickupable.pickupDelay <= 0f)
                        {
                            previousAmmoFullContacts.Add(pickupable);
                            pickupable.Collect(self);
                        }
                        if (!_workshopAmmoFullRpcSuppressionLogged)
                        {
                            _workshopAmmoFullRpcSuppressionLogged = true;
                            DiagnosticLog.Info(
                                "Suppressed Workshop ammo-full pickup RPC retries; local feedback remains active.");
                        }
                    }
                    continue;
                }

                if (pickupable.pickupDelay > 0f)
                {
                    continue;
                }

                global::Networking.Networking.RPC<TestVanDammeAnim>(
                    PID.TargetAll,
                    new RpcSignature<TestVanDammeAnim>(pickupable.Collect),
                    self,
                    false);
            }

            if (previousAmmoFullContacts != null)
            {
                previousAmmoFullContacts.IntersectWith(currentAmmoFullContacts);
                if (previousAmmoFullContacts.Count == 0)
                {
                    WorkshopAmmoFullContacts.Remove(self);
                    CurrentWorkshopAmmoFullContacts.Remove(self);
                }
            }

            return false;
        }

        private static void CompleteWorkshopPickupCollectPostfix(Pickupable __instance)
        {
            if (!IsWorkshopOnlineSession() || __instance == null ||
                __instance.collected || !__instance.gameObject.activeInHierarchy)
            {
                return;
            }

            // Ammo-full pickups intentionally remain available. Native code retries
            // every frame; a short delay preserves later collection without flooding
            // TargetAll while the hero remains on top of the pickup.
            if (__instance.pickupDelay < WorkshopPickupRetryDelaySeconds)
            {
                __instance.pickupDelay = WorkshopPickupRetryDelaySeconds;
            }
        }

        private static void ClearWorkshopPickupSynchronizationState()
        {
            _workshopCrateDeterminismLogged = false;
            _workshopRemotePickupScanLogged = false;
            _workshopDuplicatePickupCollectLogged = false;
            _workshopAmmoFullRpcSuppressionLogged = false;
            WorkshopAmmoFullContacts.Clear();
            CurrentWorkshopAmmoFullContacts.Clear();
        }
    }
}
