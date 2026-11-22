using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace EnemyAIEnhancement
{
    internal static partial class Controller
    {
        private static readonly FieldInfo xenoGrowth = typeof(AlienXenomorph).GetField("growingFrames", BindingFlags.Instance | BindingFlags.NonPublic);
        private static readonly Dictionary<PolymorphicAI, ActionObject> xenoCooldowns = new Dictionary<PolymorphicAI, ActionObject>();

        private static bool XenomorphNativePhase(XenomorphAI ai, AlienXenomorph mook)
        {
            if ((bool)faceFirst.GetValue(ai) || (int)xenoGrowth.GetValue(mook) > 0 ||
                mook.wallAttachDirection != DirectionEnum.None || (DirectionEnum)faceTransition.GetValue(mook) != DirectionEnum.None ||
                !mook.IsOnGround() || (mook.actionState != ActionState.Idle && mook.actionState != ActionState.Running) ||
                mook.IsIncapacitated() || mook.blindTime > 0f || mook.burnTime > 0f || ai.mentalState != MentalState.Alerted) return true;
            // Only forcedClimbingTime uses >= 0 in GetInput; the other timers act strictly above zero.
            for (int i = 0; i < faceClimbing.Length; i++)
                if (i == 0 ? (float)faceClimbing[i].GetValue(ai) >= 0f : (float)faceClimbing[i].GetValue(ai) > 0f) return true;
            return false;
        }

        private static bool XenomorphCommitted(PolymorphicAI ai)
        {
            ActionObject cooldown;
            if (xenoCooldowns.TryGetValue(ai, out cooldown))
            {
                if (ai.actionQueue.Contains(cooldown)) return true;
                xenoCooldowns.Remove(ai);
            }
            for (int i = 0; i < ai.actionQueue.Count; i++)
                if (ai.actionQueue[i].type == EnemyActionType.Fire)
                {
                    for (int j = i + 1; j < ai.actionQueue.Count; j++)
                        if (ai.actionQueue[j].type == EnemyActionType.Wait) xenoCooldowns[ai] = ai.actionQueue[j];
                    return true;
                }
            return (float)faceHoldFire.GetValue(ai) > 0f;
        }

        // Land is also called on replicas and controlled actors. Phase protection never grants authority.
        internal static bool PreserveMeleeLanding(PolymorphicAI ai, Mook mook)
        {
            if (!Supported(ai, mook)) return false;
            EnemyKind kind = Kind(ai, mook);
            return (kind == EnemyKind.Facehugger && FacehuggerCommitted(ai, mook)) ||
                (kind == EnemyKind.Xenomorph && XenomorphCommitted(ai));
        }

        private static bool XenomorphRoute(AlienXenomorph mook, float x)
        {
            float width = mook.HalfWidth, height = Mathf.Max(mook.height, mook.headHeight);
            if (Mathf.Min(x, mook.X) - width < 0f || Mathf.Max(x, mook.X) + width >= Map.Width * 16f ||
                mook.Y < 0f || mook.Y + height >= Map.Height * 16f) return false;
            float feet = (float)faceFeet.GetValue(mook);
            for (int c = Map.GetCollumn(Mathf.Min(x, mook.X) - feet); c <= Map.GetCollumn(Mathf.Max(x, mook.X) + feet); c++)
                if (!Map.IsBlockSolid(c, mook.row - 1)) return false;
            Vector3 center = new Vector3(mook.X, mook.Y + 1f + height * 0.5f, 0f);
            Vector3 extents = new Vector3(width, height * 0.5f, 1f);
            Vector3 delta = new Vector3(x - mook.X, 0f, 0f);
            int layers = Map.groundLayer | Map.fragileLayer;
            RaycastHit floor;
            return Physics.Raycast(new Vector3(x, mook.Y + 4f, 0f), Vector3.down, out floor, 8f, layers) &&
                Mathf.Abs(floor.point.y - mook.Y) < 2f && !Physics.CheckBox(center, extents, Quaternion.identity, layers) &&
                !Physics.CheckBox(center + delta, extents, Quaternion.identity, layers) &&
                (delta.sqrMagnitude < 0.01f || !Physics.BoxCast(center, extents, delta.normalized, Quaternion.identity, delta.magnitude, layers));
        }

        private static bool XenomorphTick(XenomorphAI ai, AlienXenomorph mook, ref int seenPlayer, bool force)
        {
            Memory memory;
            memories.TryGetValue(ai, out memory);
            if (memory != null && (memory.XenoFaction != mook.playerNum || memory.XenoBrainBox != mook.hasBrainBox))
            {
                memories.Remove(ai);
                return false; // DropBrainBox's blind/growth/wait and native target selection run first.
            }
            if (XenomorphNativePhase(ai, mook)) return false;
            if (XenomorphCommitted(ai)) return true;
            if (seenPlayer == -1) { memories.Remove(ai); return false; }
            if ((memory == null || memory.PathGoal == null) && (HasAction(ai, EnemyActionType.FollowPath) || ai.GetComponent<PathAgent>().CurrentPath != null) ||
                (ai.actionQueue.Count > 0 && (ai.actionQueue[0].type == EnemyActionType.Wait ||
                 HasAction(ai, EnemyActionType.Jump) || memory == null))) return false;
            float now = Time.time;
            if (!force && memory != null && now < memory.NextSense) return false;
            int target = seenPlayer;
            bool visible = HeroController.CanSeePlayer(mook.X, mook.Y, 0, ai.sightRangeX, ai.sightRangeY, ref target) &&
                target >= 0 && target < 4 && HeroController.PlayerIsAlive(target) && GameModeController.DoesPlayerNumDamage(mook.playerNum, target);
            Vector2 hero = visible ? (Vector2)HeroController.GetPlayerPosition(target) : Vector2.zero;
            int npc = int.MinValue; float npcX = 0f, npcY = 0f;
            if (Map.CanSeeNPCEnemyUnit(mook.X, mook.Y, ai.FacingDirection, ai.sightRangeX, 16, mook.playerNum, ref npc, ref npcX, ref npcY) &&
                (!visible || Vector2.Distance(new Vector2(mook.X, mook.Y), new Vector2(npcX, npcY)) < Vector2.Distance(new Vector2(mook.X, mook.Y), hero)))
            { memories.Remove(ai); return false; }
            if (memory == null && !visible) return false;
            memory = GetMemory(ai, mook); memory.NextSense = now + SenseInterval;
            if (Plugin.Settings.AvoidGrenades && TryRetreat(ai, mook, memory)) return true;
            if (visible)
            {
                seenPlayer = target; SampleVisible(memory, target, hero, now);
                if (Mathf.Abs(hero.y - mook.Y) > 16f || Mathf.Abs(hero.x - mook.X) > 24f)
                    return Plugin.Settings.PursueTargets && InterceptPursue(ai, mook, memory, true);
                int direction = hero.x < mook.X ? -1 : 1;
                float travel = Mathf.Max(24f, mook.speed * (ai.attackHoldFireDuration + ai.attackHoldFireDelay));
                if (!XenomorphRoute(mook, mook.X + direction * travel)) return false;
                float jump = ai.jumpWhileAttacking ? (mook.useAttackJumpForceForAttack ? mook.attackJumpForce : mook.jumpForce) : 0f;
                float rise = jump * jump / 1452f;
                float height = Mathf.Max(mook.height, mook.headHeight);
                if (Physics.CheckBox(new Vector3(mook.X + direction * travel * 0.5f, mook.Y + 1f + (height + rise) * 0.5f, 0f),
                    new Vector3(travel * 0.5f + mook.HalfWidth, (height + rise) * 0.5f, 1f), Quaternion.identity, Map.groundLayer | Map.fragileLayer)) return false;
                Begin(ai, memory, Plan.Attack, 0, direction);
                ai.AddAction(EnemyActionType.FacePoint, new GridPoint(mook.collumn + direction, mook.row), 0f);
                ai.AddAction(EnemyActionType.Fire, ai.attackTime);
                ai.AddAction(EnemyActionType.Wait, Mathf.Max(ai.delayBetweenFiring, ai.attackHoldFireDelay + ai.attackHoldFireDuration));
                xenoCooldowns[ai] = ai.actionQueue[ai.actionQueue.Count - 1];
                faceLastAttack.SetValue(ai, now);
                return true;
            }
            if (Plugin.Settings.RememberTargets && memory.Player >= 0 && HeroController.PlayerIsAlive(memory.Player) && now - memory.SeenAt <= MemorySeconds)
            {
                if (Plugin.Settings.PursueTargets && InterceptPursue(ai, mook, memory, false)) return true;
                Search(ai, mook, memory, force); return true;
            }
            memory.Player = -1; memory.Plan = Plan.None; seenPlayer = -10;
            ai.SetMentalState(MentalState.Suspicious); ai.ClearActionQueue(true); ai.AddAction(EnemyActionType.Wait, 0.3f);
            return true;
        }

        internal static bool XenomorphOwnsGround(PolymorphicAI ai)
        {
            if (ai.GetType() != typeof(XenomorphAI)) return false;
            Mook mook = ai.GetComponent<Mook>(); Memory memory;
            return Supported(ai, mook) && Kind(ai, mook) == EnemyKind.Xenomorph &&
                !XenomorphNativePhase((XenomorphAI)ai, (AlienXenomorph)mook) &&
                (XenomorphCommitted(ai) || (ai.GetSeenPlayerNum() >= 0 && ai.GetSeenPlayerNum() < 4 &&
                 memories.TryGetValue(ai, out memory) && memory.Plan != Plan.None && memory.XenoFaction == mook.playerNum &&
                 memory.XenoBrainBox == ((AlienXenomorph)mook).hasBrainBox &&
                 !HasAction(ai, EnemyActionType.FollowPath) && ai.GetComponent<PathAgent>().CurrentPath == null));
        }
    }
}
