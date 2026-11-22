using HarmonyLib;
using System.Collections.Generic;
using System.Reflection.Emit;

namespace EnemyAIEnhancement
{
    [HarmonyPatch(typeof(PolymorphicAI), "LateUpdate")]
    internal static class ShareNinjaState
    {
        private static void Postfix(PolymorphicAI __instance, Mook ___mook)
        { Controller.ShareNinjaReveal(__instance, ___mook); }
    }

    [HarmonyPatch(typeof(MookNinjaAI), "FullyAlert")]
    internal static class PreserveNinjaAmbush
    {
        private static void Prefix(MookNinjaAI __instance, out bool __state)
        { __state = !__instance.IsRevealed(); }

        private static void Postfix(MookNinjaAI __instance, Mook ___mook, bool __state)
        { Controller.TrackNinjaWarning(__instance, ___mook, __state); }
    }

    [HarmonyPatch(typeof(ScoutAI), "FullyAlert")]
    internal static class ChooseScoutEscape
    {
        private static void Prefix(ScoutAI __instance, Mook ___mook, out bool __state)
        { __state = Controller.PrepareScoutAlert(__instance, ___mook); }

        private static void Postfix(ScoutAI __instance, Mook ___mook, bool __state)
        { if (__state) Controller.TrackScout(__instance, ___mook); }

        private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var nearest = AccessTools.Method(typeof(Map), "GetNearestMookInterestPoint", null, null);
            var select = AccessTools.Method(typeof(Controller), "SelectScoutPoint", null, null);
            foreach (CodeInstruction instruction in instructions)
            {
                if (instruction.opcode == OpCodes.Call && Equals(instruction.operand, nearest))
                {
                    yield return new CodeInstruction(OpCodes.Ldarg_0, null);
                    instruction.operand = select;
                }
                yield return instruction;
            }
        }
    }

    [HarmonyPatch(typeof(PolymorphicAI), "Update")]
    internal static class ObserveEnemy
    {
        private static void Postfix(PolymorphicAI __instance, Mook ___mook, ref int ___seenEnemyNum)
        {
            Controller.LocalIntel(__instance, ___mook, ref ___seenEnemyNum);
            Controller.Tick(__instance, ___mook, ref ___seenEnemyNum, false);
        }
    }

    [HarmonyPatch(typeof(MookTrooperAI), "DoAlertedThink")]
    internal static class PlanCombat
    {
        private static bool Prefix(MookTrooperAI __instance, Mook ___mook, ref int ___seenEnemyNum)
        {
            return !Controller.Tick(__instance, ___mook, ref ___seenEnemyNum, true);
        }
    }

    [HarmonyPatch(typeof(MookBazookaAi), "DoAlertedThink")]
    internal static class PlanExplosives
    {
        private static bool Prefix(PolymorphicAI __instance, Mook ___mook, ref int ___seenEnemyNum)
        {
            return !Controller.Tick(__instance, ___mook, ref ___seenEnemyNum, true);
        }
    }

    [HarmonyPatch(typeof(PolymorphicAI), "DoAlertedThink")]
    internal static class PlanShield
    {
        private static bool Prefix(PolymorphicAI __instance, Mook ___mook, ref int ___seenEnemyNum)
        {
            return !Controller.Tick(__instance, ___mook, ref ___seenEnemyNum, true);
        }
    }

    [HarmonyPatch(typeof(SuicideAI), "DoAlertedThink")]
    internal static class PlanCharge
    {
        private static bool Prefix(PolymorphicAI __instance, Mook ___mook, ref int ___seenEnemyNum)
        {
            return !Controller.Tick(__instance, ___mook, ref ___seenEnemyNum, true);
        }
    }

    [HarmonyPatch(typeof(BigGuyAI), "DoAlertedThink")]
    internal static class PlanBurst
    {
        private static bool Prefix(PolymorphicAI __instance, Mook ___mook, ref int ___seenEnemyNum)
        {
            return !Controller.Tick(__instance, ___mook, ref ___seenEnemyNum, true);
        }
    }

    [HarmonyPatch(typeof(PolymorphicAI), "GetPlayerRange")]
    internal static class KeepThrowAim
    {
        private static bool Prefix(PolymorphicAI __instance, Mook ___mook,
            ref float xRange, ref float yRange, ref bool __result)
        {
            if (!Controller.ThrowRange(__instance, ___mook, ref xRange, ref yRange)) return true;
            __result = true;
            return false;
        }
    }

    [HarmonyPatch(typeof(MookRiotShield), "DisarmShield")]
    internal static class ReleaseUnarmedShield
    {
        private static void Prefix(MookRiotShield __instance) { Controller.ReleaseShield(__instance); }
    }

    [HarmonyPatch(typeof(PolymorphicAI), "FullyAlert")]
    internal static class RememberAlert
    {
        private static void Postfix(PolymorphicAI __instance, Mook ___mook, float x, float y, int playerNum)
        {
            Controller.RememberAlert(__instance, ___mook, x, y, playerNum);
        }
    }

    [HarmonyPatch(typeof(PolymorphicAI), "Sync")]
    internal static class ObserveReplica
    {
        private static void Postfix(PolymorphicAI __instance)
        {
            Mook mook = __instance.GetComponent<Mook>();
            Controller.SyncScout(__instance, mook);
            Controller.SyncGroundPath(__instance, mook);
            if (Plugin.Enabled && !__instance.IsMine && mook != null && Controller.Kind(__instance, mook) != EnemyKind.Unsupported)
                Controller.ReceivedQueues++;
        }
    }

    [HarmonyPatch(typeof(NetworkObject), "OnDestroy")]
    internal static class ForgetDestroyedEnemy
    {
        private static void Prefix(NetworkObject __instance)
        {
            WormController.Forget(__instance);
            VehicleController.Forget(__instance);
            TransportController.Forget(__instance);
            BossController.Forget(__instance);
            PolymorphicAI ai = __instance as PolymorphicAI;
            if (ai != null) Controller.Forget(ai);
        }
    }
}
