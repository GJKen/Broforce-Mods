using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace CustomMapMultiplayer
{
    internal static partial class HarmonyDiagnostics
    {
        private static readonly FieldInfo WorldUnitBurnDamage = AccessTools.Field(typeof(Unit), "burnDamage");
        private static readonly FieldInfo WorldBarrelExplosion = AccessTools.Field(typeof(BarrelBlock), "explosionCounter");

        private static void CaptureWorldBurnState(BroforceObject obj, WorldSnapshot state)
        {
            var block = obj as Block;
            var doodad = obj as DoodadDestroyable;
            var unit = obj as Unit;
            if (block != null)
            {
                state.BurnTime = block.destroyed ? 0f : Mathf.Max(0f, block.burnTime);
                state.BurnDamage = block.burnDamage;
                state.Oily = block.isOily;
            }
            else if (doodad != null)
            {
                state.BurnTime = doodad.isDead ? 0f : Mathf.Max(0f, doodad.burnTime);
                state.BurnDamage = doodad.burnDamage;
            }
            else if (unit != null)
            {
                state.BurnTime = Mathf.Max(0f, unit.burnTime);
                state.BurnDamage = (int)WorldUnitBurnDamage.GetValue(unit);
            }
            if (obj is BarrelBlock && !obj.destroyed)
                state.BurnExplosion = Mathf.Max(0f, (float)WorldBarrelExplosion.GetValue(obj));
        }

        private static void ApplyWorldBurnState(BroforceObject obj, WorldSnapshot state)
        {
            var block = obj as Block;
            var doodad = obj as DoodadDestroyable;
            var unit = obj as Unit;
            if (block != null)
            {
                block.burnTime = state.BurnTime;
                block.burnDamage = state.BurnDamage;
                block.isOily = state.Oily;
                // Static blocks may already be disabled before the ignition snapshot.
                if (state.BurnTime > 0f) block.enabled = true;
            }
            else if (doodad != null)
            {
                doodad.burnTime = state.BurnTime;
                doodad.burnDamage = state.BurnDamage;
            }
            else if (unit != null)
            {
                unit.burnTime = state.BurnTime;
                WorldUnitBurnDamage.SetValue(unit, state.BurnDamage);
            }
            if (obj is BarrelBlock) WorldBarrelExplosion.SetValue(obj, state.BurnExplosion);
        }

        private static void WorldBurnLogicPrefix(BroforceObject __instance, out WorldDamageContext __state)
        {
            __state = new WorldDamageContext { Previous = _worldDamageSource };
            if (!IsWorldReplica(__instance)) return;
            // Keep native flames and ammunition-crate flashes. Map effects and
            // mutation prefixes suppress replica damage, ignition and collapse.
            __state.VisualReplay = true;
            _worldApplying++;
        }

        private static bool WorldBurnEffectPrefix(BroforceObject __instance)
        {
            // BurnOthers also runs outside RunBurnLogic, in the native mook Update.
            // Only the owner spreads fire and settles the final StopBurning hit.
            return _worldApplying == 0 && !IsWorldReplica(__instance);
        }

        private static bool WorldBarrelExplodingPrefix(BarrelBlock __instance, out WorldDamageContext __state)
        {
            WorldBurnLogicPrefix(__instance, out __state);
            if (!__state.VisualReplay) return true;
            if ((float)WorldBarrelExplosion.GetValue(__instance) > Mathf.Clamp(Time.deltaTime, 0f, 0.033334f)) return true;
            // Native expiry writes health directly before Collapse. Wait for the
            // owner's terminal state while retaining its warning/fire animation.
            WorldBarrelExplosion.SetValue(__instance, 0f);
            return false;
        }
    }
}
