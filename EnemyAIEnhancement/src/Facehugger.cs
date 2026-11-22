using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace EnemyAIEnhancement
{
    internal static partial class Controller
    {
        private static readonly FieldInfo faceFirst = AccessTools.Field(typeof(PolymorphicAI), "firstFrame");
        private static readonly FieldInfo faceRoof = AccessTools.Field(typeof(FacehuggerAI), "isOnRoof");
        private static readonly FieldInfo faceRoofTime = AccessTools.Field(typeof(FacehuggerAI), "tryInitialStickToRoofTime");
        private static readonly FieldInfo faceDrop = AccessTools.Field(typeof(FacehuggerAI), "dropDownHoldFire");
        private static readonly FieldInfo faceCharge = AccessTools.Field(typeof(AlienFaceHugger), "chargingSpecial");
        private static readonly FieldInfo faceConnected = AccessTools.Field(typeof(AlienFaceHugger), "connectedToFace");
        private static readonly FieldInfo faceUsing = AccessTools.Field(typeof(TestVanDammeAnim), "usingSpecial");
        private static readonly FieldInfo faceTransition = AccessTools.Field(typeof(AlienClimber), "wallTransitionToAttachDirection");
        private static readonly FieldInfo faceFeet = AccessTools.Field(typeof(TestVanDammeAnim), "feetWidth");
        private static readonly FieldInfo faceLastAttack = AccessTools.Field(typeof(XenomorphAI), "lastForcedAttackTime");
        private static readonly FieldInfo faceHoldFire = AccessTools.Field(typeof(DogAI), "holdFireTime");
        private static readonly FieldInfo[] faceClimbing = new[] {
            AccessTools.Field(typeof(XenomorphAI), "forcedClimbingTime"), AccessTools.Field(typeof(XenomorphAI), "forcedLeftClimbingTime"),
            AccessTools.Field(typeof(XenomorphAI), "forcedRightClimbingTime"), AccessTools.Field(typeof(XenomorphAI), "forcedUpClimbingTime"),
            AccessTools.Field(typeof(XenomorphAI), "forceGoOverLedgeTime") };
        // Separate from target memory so toggling cannot discard an in-progress cooldown.
        private static readonly Dictionary<PolymorphicAI, ActionObject> faceCooldowns = new Dictionary<PolymorphicAI, ActionObject>();

        private static bool FacehuggerEnvironment(FacehuggerAI ai, AlienFaceHugger mook)
        {
            if ((bool)faceFirst.GetValue(ai) || (bool)faceRoof.GetValue(ai) || (float)faceRoofTime.GetValue(ai) > 0f ||
                (bool)faceDrop.GetValue(ai) || mook.wallAttachDirection != DirectionEnum.None ||
                (DirectionEnum)faceTransition.GetValue(mook) != DirectionEnum.None || !mook.IsOnGround() ||
                mook.actionState == ActionState.Hanging || mook.IsIncapacitated() || mook.blindTime > 0f || mook.burnTime > 0f) return true;
            for (int i = 0; i < faceClimbing.Length; i++) if ((float)faceClimbing[i].GetValue(ai) > 0f) return true;
            return false;
        }

        private static bool FacehuggerNativePhase(FacehuggerAI ai, AlienFaceHugger mook)
        {
            return FacehuggerEnvironment(ai, mook) || (bool)faceCharge.GetValue(mook) ||
                (bool)faceUsing.GetValue(mook) || (bool)faceConnected.GetValue(mook);
        }

        internal static bool FacehuggerCommitted(PolymorphicAI ai, Mook mook)
        {
            ActionObject cooldown;
            if (faceCooldowns.TryGetValue(ai, out cooldown))
            {
                if (ai.actionQueue.Contains(cooldown)) return true;
                faceCooldowns.Remove(ai);
            }
            for (int i = 0; i < ai.actionQueue.Count; i++)
                if (ai.actionQueue[i].type == EnemyActionType.UseSpecial || ai.actionQueue[i].type == EnemyActionType.Fire)
                {
                    for (int j = i + 1; j < ai.actionQueue.Count; j++)
                        if (ai.actionQueue[j].type == EnemyActionType.Wait) { faceCooldowns[ai] = ai.actionQueue[j]; break; }
                    return true;
                }
            return (bool)faceCharge.GetValue(mook) || (bool)faceUsing.GetValue(mook) ||
                (bool)faceConnected.GetValue(mook) || (float)faceHoldFire.GetValue(ai) > 0f ||
                Time.time - (float)faceLastAttack.GetValue(ai) < 1.5f;
        }

        // Full body sweep and every supporting cell across the feet, including intermediate tiles.
        private static bool FacehuggerRoute(Mook mook, float x)
        {
            float width = mook.HalfWidth;
            if (Mathf.Min(x, mook.X) - width < 0f || Mathf.Max(x, mook.X) + width >= Map.Width * 16f) return false;
            float feet = (float)faceFeet.GetValue(mook);
            int first = Map.GetCollumn(Mathf.Min(x, mook.X) - feet);
            int last = Map.GetCollumn(Mathf.Max(x, mook.X) + feet);
            for (int c = first; c <= last; c++) if (!Map.IsBlockSolid(c, mook.row - 1)) return false;
            Vector3 center = new Vector3(mook.X, mook.Y + 7f, 0f);
            Vector3 extents = new Vector3(width, 5f, 1f);
            Vector3 delta = new Vector3(x - mook.X, 0f, 0f);
            int layers = Map.groundLayer | Map.fragileLayer;
            RaycastHit floor;
            return Physics.Raycast(new Vector3(x, mook.Y + 4f, 0f), Vector3.down, out floor, 8f, layers) &&
                Mathf.Abs(floor.point.y - mook.Y) < 2f && !Physics.CheckBox(center, extents, Quaternion.identity, layers) &&
                !Physics.CheckBox(center + delta, extents, Quaternion.identity, layers) &&
                (delta.sqrMagnitude < 0.01f || !Physics.BoxCast(center, extents, delta.normalized, Quaternion.identity, delta.magnitude, layers));
        }

        private static bool FacehuggerCombat(FacehuggerAI ai, AlienFaceHugger mook, Memory memory)
        {
            float distance = Mathf.Abs(memory.LastSeen.x - mook.X);
            if (Mathf.Abs(memory.LastSeen.y - mook.Y) > 16f) return false; // Native path/climbing can handle other levels.
            // Conservative flat-ground reach from each prefab's native leap velocities and gravity.
            float reach = Mathf.Min(90f, mook.attackJumpXSpeed * 2f * mook.attackJumpYSpeed / 726f * 0.8f);
            if (distance > reach) return Plugin.Settings.PursueTargets && TryPursue(ai, mook, memory);
            if (!FacehuggerRoute(mook, memory.LastSeen.x)) return false;
            bool bite = distance <= 18f && !ai.alwaysWindup;
            float rise = mook.attackJumpYSpeed * mook.attackJumpYSpeed / 1452f;
            if (!bite && Physics.CheckBox(new Vector3((mook.X + memory.LastSeen.x) * 0.5f, mook.Y + 7f + rise * 0.5f, 0f),
                new Vector3(distance * 0.5f + mook.HalfWidth, 5f + rise * 0.5f, 1f), Quaternion.identity, Map.groundLayer | Map.fragileLayer)) return false;
            int direction = memory.LastSeen.x < mook.X ? -1 : 1;
            Begin(ai, memory, Plan.Attack, 0, direction);
            ai.AddAction(EnemyActionType.FacePoint, new GridPoint(mook.collumn + direction, mook.row), 0f);
            ai.AddAction(bite ? EnemyActionType.Fire : EnemyActionType.UseSpecial, bite ? ai.attackTime : 0.05f);
            ai.AddAction(EnemyActionType.Wait, 1.5f);
            faceCooldowns[ai] = ai.actionQueue[ai.actionQueue.Count - 1];
            faceLastAttack.SetValue(ai, Time.time);
            return true;
        }

        internal static bool FacehuggerOwnsGround(PolymorphicAI ai)
        {
            if (ai.GetType() != typeof(FacehuggerAI)) return false;
            Mook mook = ai.GetComponent<Mook>();
            if (Kind(ai, mook) != EnemyKind.Facehugger || !Supported(ai, mook) ||
                ai.mentalState != MentalState.Alerted || FacehuggerEnvironment((FacehuggerAI)ai, (AlienFaceHugger)mook)) return false;
            return FacehuggerCommitted(ai, mook) || (ai.GetSeenPlayerNum() >= 0 && ai.GetSeenPlayerNum() < 4);
        }
    }

    [HarmonyPatch(typeof(XenomorphAI), "DoAlertedThink")]
    internal static class PlanFacehugger
    {
        private static bool Prefix(XenomorphAI __instance, Mook ___mook, ref int ___seenEnemyNum)
        { return (__instance.GetType() != typeof(FacehuggerAI) && __instance.GetType() != typeof(XenomorphAI)) || !Controller.Tick(__instance, ___mook, ref ___seenEnemyNum, true); }
    }

    // XenomorphAI.Update also forces attacks outside DoAlertedThink. Suppress that one branch
    // while this exact owner/ground strategy is responsible, including its committed cooldown.
    [HarmonyPatch(typeof(DogAI), "IsEnemyNearbyInAttackRange")]
    internal static class PreserveFacehuggerAttack
    {
        private static void Postfix(DogAI __instance, ref bool __result)
        { if (Controller.FacehuggerOwnsGround(__instance) || Controller.XenomorphOwnsGround(__instance)) __result = false; }
    }

    [HarmonyPatch(typeof(FacehuggerAI), "PerformMediumRangeAttack")]
    internal static class FacehuggerVisibleOpportunity
    {
        private static bool Prefix(FacehuggerAI __instance, ref bool __result)
        { if (!Controller.FacehuggerOwnsGround(__instance)) return true; __result = false; return false; }
    }

    [HarmonyPatch(typeof(XenomorphAI), "Land")]
    internal static class PreserveFacehuggerLanding
    {
        private static bool Prefix(XenomorphAI __instance, Mook ___mook)
        { return !Controller.PreserveMeleeLanding(__instance, ___mook); }
    }
}
