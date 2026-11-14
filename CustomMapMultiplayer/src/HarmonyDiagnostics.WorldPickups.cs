using System.Reflection;
using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

namespace CustomMapMultiplayer
{
    internal static partial class HarmonyDiagnostics
    {
        private static readonly FieldInfo WorldCratePickup = typeof(CrateBlock).GetField("pickup", WorldFields);

        private static IEnumerable<CodeInstruction> WorldPickupVisibilityTranspiler(IEnumerable<CodeInstruction> instructions)
        {
            foreach (var instruction in instructions)
            {
                var method = instruction.operand as MethodInfo;
                if (method != null && method.DeclaringType == typeof(SortOfFollow) && method.Name == "IsItSortOfVisible")
                    instruction.operand = typeof(HarmonyDiagnostics).GetMethod("IsWorldPickupVisible", BindingFlags.Static | BindingFlags.NonPublic);
                yield return instruction;
            }
        }

        private static bool IsWorldPickupVisible(float x, float y, float rangeX, float rangeY)
        {
            // An owner's camera can be elsewhere while a joining player opens a
            // crate. Native camera culling must not freeze that player's loot.
            return _worldEpoch != 0 && IsWorldSession() || SortOfFollow.IsItSortOfVisible(x, y, rangeX, rangeY);
        }

        private static void CaptureWorldPickupState(Pickupable pickup, WorldSnapshot state)
        {
            state.Flags |= WorldFlags.Pickup;
            if (pickup.gameObject.activeSelf) state.Flags |= WorldFlags.PickupActive;
            if (pickup.collected) state.Flags |= WorldFlags.PickupCollected;
            state.PickupType = (int)pickup.pickupType;
            state.PickupDelay = Mathf.Max(0f, pickup.pickupDelay);
            state.LifeTime = pickup.life;
        }

        private static void ApplyWorldPickupState(Pickupable pickup, WorldSnapshot state)
        {
            var active = (state.Flags & WorldFlags.PickupActive) != 0;
            var collected = (state.Flags & WorldFlags.PickupCollected) != 0;
            // A preceding owner snapshot must not undo the native local Collect RPC.
            if (pickup.collected && !collected) return;
            pickup.pickupType = (PickupType)state.PickupType;
            if (active || collected)
            {
                var parent = pickup.transform.parent;
                var crate = parent == null ? null : parent.GetComponent<CrateBlock>();
                if (crate != null && (WorldCratePickup.GetValue(crate) as Pickupable) == pickup)
                    WorldCratePickup.SetValue(crate, null);
                pickup.transform.parent = null;
            }
            if (active && !pickup.gameObject.activeSelf)
            {
                // Launch enrolls the existing, registered child in the native pickup list.
                // A destroyed-crate snapshot skips CrateBlock.MakeEffects and its Launch.
                pickup.Launch(state.X - pickup.xExtraOffset, state.Y,
                    state.VX, state.VY - pickup.extraLaunchForce);
            }
            else if (!active && pickup.gameObject.activeSelf)
            {
                PickupableController.RemovePickupable(pickup);
                pickup.gameObject.SetActive(false);
            }
            // Only native CollectAndDisappear acknowledges delivery to the hero.
            // A state snapshot can arrive before Collect, even after hiding the item.
            pickup.pickupDelay = state.PickupDelay;
            pickup.life = state.LifeTime;
            pickup.xI = state.VX; pickup.yI = state.VY;
        }

        private static bool WorldPickupUpdatePrefix(Pickupable __instance)
        {
            if (!IsWorldReplica(__instance)) return true;
            // The owner advances physics/lifetime. Present its samples every frame
            // instead of leaving the replica frozen between 12 Hz snapshots.
            WorldEntry entry;
            if (WorldEntries.TryGetValue(__instance.Nid, out entry) && entry.Presentation != null &&
                entry.Presentation.Crate != null)
            {
                // A newly revealed, existing drop moves immediately until its first
                // authoritative active sample replaces this short presentation.
                __instance.pickupDelay = Mathf.Max(__instance.pickupDelay, 1f);
                return true;
            }
            if (WorldEntries.TryGetValue(__instance.Nid, out entry) && entry.Last != null &&
                (entry.Last.Flags & WorldFlags.PickupActive) != 0)
            {
                var state = entry.Last;
                var xy = Vector2.Lerp(entry.LerpFrom, new Vector2(state.X, state.Y),
                    Mathf.Clamp01((Time.unscaledTime - entry.ReceivedAt) / WorldSendInterval));
                __instance.SetPosition(new Vector3(xy.x, xy.y, __instance.transform.position.z));
            }
            if (Time.timeScale > 0f)
                __instance.pickupDelay -= Mathf.Clamp(Time.deltaTime, 0f, 0.0334f) / Time.timeScale;
            return false;
        }
    }
}
