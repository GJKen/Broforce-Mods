using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace EnemyAIEnhancement
{
    internal static partial class Controller
    {
        private static readonly FieldInfo resurrectionFrame = AccessTools.Field(typeof(UndeadTrooper), "resurrectionFrame");
        private static readonly FieldInfo undeadFuse = AccessTools.Field(typeof(UndeadSuicideAI), "hasLitFuse");
        private static readonly FieldInfo undeadDecapitated = AccessTools.Field(typeof(Mook), "decapitated");
        private static readonly Dictionary<PolymorphicAI, ActionObject> undeadCooldowns = new Dictionary<PolymorphicAI, ActionObject>();

        private static bool IsUndead(EnemyKind kind)
        { return kind == EnemyKind.UndeadTrooper || kind == EnemyKind.UndeadSuicide; }

        private static bool UndeadDormant(PolymorphicAI ai, Mook mook)
        {
            return !ai.enabled || (bool)faceFirst.GetValue(ai) || mook.PlayingSummoningAnimation() ||
                (mook.GetType() == typeof(UndeadTrooper) && (int)resurrectionFrame.GetValue(mook) >= 0);
        }

        private static bool UndeadNativePhase(PolymorphicAI ai, Mook mook)
        {
            return UndeadDormant(ai, mook) || mook.IsIncapacitated() || mook.blindTime > 0f || mook.burnTime > 0f ||
                (bool)undeadDecapitated.GetValue(mook) || mook.tumbling || mook.frontSomersaulting || mook.backSomersaulting ||
                (bool)faceUsing.GetValue(mook) || Mathf.Abs(mook.xIBlast) > 20f || !mook.IsOnGround() ||
                (mook.actionState != ActionState.Idle && mook.actionState != ActionState.Running) ||
                HasAction(ai, EnemyActionType.PlaySpecialSound) || HasAction(ai, EnemyActionType.UseSpecial) || HasAction(ai, EnemyActionType.UseSpecial2);
        }

        private static bool UndeadCommitted(PolymorphicAI ai)
        {
            if (ai.GetType() == typeof(UndeadSuicideAI)) return (bool)undeadFuse.GetValue(ai);
            ActionObject cooldown;
            if (!undeadCooldowns.TryGetValue(ai, out cooldown)) return false;
            if (ai.actionQueue.Contains(cooldown)) return true;
            undeadCooldowns.Remove(ai); return false;
        }

        private static bool UndeadRoute(Mook mook, float x)
        {
            float height = Mathf.Max(mook.height, Mathf.Max(mook.headHeight, (float)dogStanding.GetValue(mook)));
            if (Mathf.Min(x, mook.X) - mook.HalfWidth < 0f || Mathf.Max(x, mook.X) + mook.HalfWidth >= Map.Width * 16f ||
                mook.Y < 0f || mook.Y + height >= Map.Height * 16f) return false;
            float feet = (float)feetWidth.GetValue(mook);
            for (int c = Map.GetCollumn(Mathf.Min(x, mook.X) - feet); c <= Map.GetCollumn(Mathf.Max(x, mook.X) + feet); c++)
                if (!Map.IsBlockSolid(c, mook.row - 1)) return false;
            Vector3 center = new Vector3(mook.X, mook.Y + 1f + height * 0.5f, 0f), delta = new Vector3(x - mook.X, 0f, 0f);
            Vector3 extents = new Vector3(mook.HalfWidth, height * 0.5f, 1f);
            int layers = Map.groundLayer | Map.fragileLayer; RaycastHit floor;
            return Physics.Raycast(new Vector3(x, mook.Y + 4f, 0f), Vector3.down, out floor, 8f, layers) &&
                Mathf.Abs(floor.point.y - mook.Y) < 2f && !Physics.CheckBox(center, extents, Quaternion.identity, layers) &&
                (delta.sqrMagnitude < 0.01f || !Physics.BoxCast(center, extents, delta.normalized, Quaternion.identity, delta.magnitude, layers));
        }

        private static bool UndeadTick(PolymorphicAI ai, Mook mook, ref int seenPlayer, bool force)
        {
            // Summoning and positive-health resurrection must discard knowledge without touching native actions.
            if (UndeadDormant(ai, mook)) { memories.Remove(ai); undeadCooldowns.Remove(ai); return false; }
            bool suicide = ai.GetType() == typeof(UndeadSuicideAI);
            // Returning false leaves native charging in full control; a stale Plan is not ongoing intervention.
            if (UndeadCommitted(ai)) return !suicide;
            if (UndeadNativePhase(ai, mook) || ai.mentalState != MentalState.Alerted) return false;
            Memory memory; memories.TryGetValue(ai, out memory);
            if (seenPlayer == -1) { memories.Remove(ai); return false; }
            PathAgent agent = ai.GetComponent<PathAgent>();
            if (HasAction(ai, EnemyActionType.FollowPath) || (agent != null && agent.CurrentPath != null) ||
                HasAction(ai, EnemyActionType.Jump) || HasAction(ai, EnemyActionType.Fire) ||
                (ai.actionQueue.Count > 0 && (memory == null || ai.actionQueue[0].type == EnemyActionType.Wait))) return false;
            float now = Time.time;
            if (!force && memory != null && now < memory.NextSense) return false;
            int target = seenPlayer;
            bool visible = HeroController.CanSeePlayer(mook.X, mook.Y, 0, ai.sightRangeX, ai.sightRangeY, ref target) &&
                target >= 0 && target < 4 && HeroController.PlayerIsAlive(target) && GameModeController.DoesPlayerNumDamage(mook.playerNum, target);
            Vector2 hero = visible ? (Vector2)HeroController.GetPlayerPosition(target) : Vector2.zero;
            int npc = int.MinValue; float npcX = 0f, npcY = 0f;
            if (Map.CanSeeNPCEnemyUnit(mook.X, mook.Y, ai.FacingDirection, ai.sightRangeX, ai.sightRangeY, mook.playerNum, ref npc, ref npcX, ref npcY) &&
                (!visible || Vector2.Distance(new Vector2(mook.X, mook.Y), new Vector2(npcX, npcY)) < Vector2.Distance(new Vector2(mook.X, mook.Y), hero)))
            { memories.Remove(ai); return false; }
            if (memory == null && !visible) return false;
            memory = GetMemory(ai, mook); memory.NextSense = now + SenseInterval;
            if (!suicide && Plugin.Settings.AvoidGrenades && TryRetreat(ai, mook, memory)) return true;
            if (visible)
            {
                seenPlayer = target; memory.Player = target; memory.LastSeen = hero; memory.SeenAt = now;
                if (Mathf.Abs(hero.y - mook.Y) > 20f) return false;
                if (suicide) return Plugin.Settings.PursueTargets && SuicideCombat(ai, mook, memory, force);
                if (Plugin.Settings.PursueTargets && Mathf.Abs(hero.x - mook.X) > 128f && TryPursue(ai, mook, memory)) return true;
                Attack(ai, mook, memory, force);
                undeadCooldowns[ai] = ai.actionQueue[ai.actionQueue.Count - 1];
                return true;
            }
            if (Plugin.Settings.RememberTargets && memory.Player >= 0 && HeroController.PlayerIsAlive(memory.Player) && now - memory.SeenAt <= MemorySeconds)
            {
                if (Plugin.Settings.PursueTargets && TryPursue(ai, mook, memory)) return true;
                Search(ai, mook, memory, force); return true;
            }
            memory.Player = -1; memory.Plan = Plan.None; seenPlayer = -10;
            ai.SetMentalState(MentalState.Suspicious); ai.ClearActionQueue(true); ai.AddAction(EnemyActionType.Wait, 0.3f);
            return true;
        }
    }

    [HarmonyPatch(typeof(UndeadSuicideAI), "DoAlertedThink")]
    internal static class PlanUndeadCharge
    {
        private static bool Prefix(UndeadSuicideAI __instance, Mook ___mook, ref int ___seenEnemyNum)
        { return ___mook == null || Controller.Kind(__instance, ___mook) != EnemyKind.UndeadSuicide || !Controller.Tick(__instance, ___mook, ref ___seenEnemyNum, true); }
    }
}
