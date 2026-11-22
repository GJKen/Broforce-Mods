using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace EnemyAIEnhancement
{
    internal static partial class Controller
    {
        private static readonly Dictionary<PolymorphicAI, ActionObject> groundCooldowns = new Dictionary<PolymorphicAI, ActionObject>();
        private static readonly MethodInfo followNative = AccessTools.Method(typeof(PolymorphicAI), "FollowPath", null, null);
        private static readonly FieldInfo warlockFrames = AccessTools.Field(typeof(Warlock), "startSummonFrames");
        private static readonly FieldInfo warlockProjectile = AccessTools.Field(typeof(Warlock), "waitingForProjectile");
        private static readonly FieldInfo boomerDying = AccessTools.Field(typeof(MookHellBoomer), "dying");

        private static bool AggressiveGround(PolymorphicAI ai, Mook mook)
        {
            EnemyKind kind = Kind(ai, mook);
            return (mook.GetType() == typeof(MookTrooper) && kind == EnemyKind.Trooper) || kind == EnemyKind.Bazooka ||
                kind == EnemyKind.Warlock || kind == EnemyKind.Boomer || kind == EnemyKind.Executioner;
        }

        private static bool GroundNativePhase(PolymorphicAI ai, Mook mook)
        {
            if (!ai.enabled || (bool)faceFirst.GetValue(ai) || mook.PlayingSummoningAnimation() ||
                mook.IsIncapacitated() || mook.blindTime > 0f || mook.burnTime > 0f ||
                (bool)undeadDecapitated.GetValue(mook) || mook.tumbling || mook.frontSomersaulting ||
                mook.backSomersaulting || (bool)faceUsing.GetValue(mook) || Mathf.Abs(mook.xIBlast) > 20f ||
                HasAction(ai, EnemyActionType.UseSpecial) || HasAction(ai, EnemyActionType.UseSpecial2)) return true;
            Warlock warlock = mook as Warlock;
            if (warlock != null && (warlock.activePortal != null || (int)warlockFrames.GetValue(warlock) >= 0 ||
                (bool)warlockProjectile.GetValue(warlock))) return true;
            return mook.GetType() == typeof(MookHellBoomer) && (bool)boomerDying.GetValue(mook);
        }

        private static bool GroundCommitted(PolymorphicAI ai)
        {
            ActionObject cooldown;
            if (groundCooldowns.TryGetValue(ai, out cooldown))
            {
                if (ai.actionQueue.Contains(cooldown)) return true;
                groundCooldowns.Remove(ai);
            }
            int fire = ai.actionQueue.FindIndex(a => a.type == EnemyActionType.Fire);
            if (fire < 0) return false;
            for (int i = ai.actionQueue.Count - 1; i > fire; i--)
                if (ai.actionQueue[i].type == EnemyActionType.Wait) { groundCooldowns[ai] = ai.actionQueue[i]; break; }
            return true;
        }

        private static bool GroundTick(PolymorphicAI ai, Mook mook, ref int seenPlayer, bool force)
        {
            if (!ai.enabled || (bool)faceFirst.GetValue(ai) || mook.PlayingSummoningAnimation())
            { memories.Remove(ai); return false; }
            if (GroundNativePhase(ai, mook)) return false;
            if (GroundCommitted(ai)) return true;
            if (ai.mentalState != MentalState.Alerted || seenPlayer == -1) return false;
            Memory memory; memories.TryGetValue(ai, out memory);
            if (memory != null && memory.SharedPending && !IntelInterruptible(ai)) return false;
            // Unowned native greetings, waits, jumps and routes finish before a new decision.
            if ((memory == null || (memory.Plan == Plan.None && !memory.SharedPending)) && ai.actionQueue.Count > 0) return false;
            if (!mook.IsOnGround() && (memory == null || memory.PathGoal == null)) return false;
            float now = Time.time;
            if (!force && memory != null && now < memory.NextSense) return false;
            int target = seenPlayer;
            bool visible = HeroController.CanSeePlayer(mook.X, mook.Y, 0, ai.sightRangeX, ai.sightRangeY, ref target) &&
                target >= 0 && target < 4 && HeroController.PlayerIsAlive(target) && GameModeController.DoesPlayerNumDamage(mook.playerNum, target);
            Vector2 position = visible ? (Vector2)HeroController.GetPlayerPosition(target) : Vector2.zero;
            if (PreferNativeNPC(ai, mook, visible, position))
            { memories.Remove(ai); return false; }
            if (memory == null && !visible) return false;
            memory = GetMemory(ai, mook); memory.NextSense = now + SenseInterval;
            memory.SharedPending = false;
            if (visible)
            {
                seenPlayer = target; memory.Player = target; memory.LastSeen = position; memory.SeenAt = now;
            }
            else if (!Plugin.Settings.RememberTargets || memory.Player < 0 || !HeroController.PlayerIsAlive(memory.Player) || now - memory.SeenAt > MemorySeconds)
            {
                Begin(ai, memory, Plan.None, 0, 0); memory.Player = -1; seenPlayer = -10;
                ai.SetMentalState(MentalState.Suspicious); ai.AddAction(EnemyActionType.Wait, 0.3f); return true;
            }
            if (mook.IsOnGround() && Plugin.Settings.AvoidGrenades && TryRetreat(ai, mook, memory)) return true;
            if (!visible)
            {
                if (Plugin.Settings.PursueTargets && GroundPursue(ai, mook, memory)) return true;
                Search(ai, mook, memory, force); return true;
            }
            EnemyKind kind = Kind(ai, mook);
            float distance = Mathf.Abs(position.x - mook.X);
            if (kind == EnemyKind.Bazooka && distance < 72f)
            {
                if (!Plugin.Settings.PursueTargets || !TrySpace(ai, mook, memory, 88f)) Guard(ai, mook, memory, force);
                return true;
            }
            float preferred = kind == EnemyKind.Boomer ? 96f : kind == EnemyKind.Executioner ? 112f : kind == EnemyKind.Warlock ? 192f : 128f;
            if (Plugin.Settings.PursueTargets && distance > preferred && GroundPursue(ai, mook, memory)) return true;
            if (!mook.IsOnGround()) return true; // Finish the native jump/ladder nodes before firing.
            if (!GroundShot(kind, mook, new Vector2(mook.X, mook.Y), position))
            {
                if (Plugin.Settings.PursueTargets && (FindFiringPosition(ai, mook, memory, kind) || GroundPursue(ai, mook, memory))) return true;
                Guard(ai, mook, memory, force); return true;
            }
            int direction = position.x < mook.X ? -1 : 1;
            Begin(ai, memory, Plan.Attack, 0, direction);
            ai.AddAction(EnemyActionType.FacePoint, new GridPoint(mook.collumn + direction, mook.row), 0f);
            ai.AddAction(EnemyActionType.Wait, Mathf.Max(0.2f, ai.sightDelay));
            ai.AddAction(EnemyActionType.Fire, kind == EnemyKind.Warlock ? 0.1f : kind == EnemyKind.Bazooka ? 0.1f : ai.attackTime);
            ai.AddAction(EnemyActionType.Wait, Mathf.Max(kind == EnemyKind.Warlock || kind == EnemyKind.Executioner ? 2f : kind == EnemyKind.Bazooka ? 1.4f : 0.2f, ai.delayBetweenFiring));
            groundCooldowns[ai] = ai.actionQueue[ai.actionQueue.Count - 1];
            return true;
        }

        private static bool GroundShot(EnemyKind kind, Mook mook, Vector2 from, Vector2 target)
        {
            float distance = Mathf.Abs(target.x - from.x), vertical = Mathf.Abs(target.y - from.y);
            if (kind == EnemyKind.Bazooka && distance < 72f) return false;
            if (kind == EnemyKind.Boomer && (distance < 24f || distance > 128f || vertical > 32f)) return false;
            if (kind == EnemyKind.Executioner && (distance > 144f || vertical > 24f)) return false;
            if (kind == EnemyKind.Warlock && (distance > 224f || vertical > 64f)) return false;
            if ((kind == EnemyKind.Trooper || kind == EnemyKind.Bazooka) && vertical > 20f) return false;
            float height = kind == EnemyKind.Boomer ? 18f : 8f;
            Vector3 origin = new Vector3(from.x, from.y + height, 0f);
            Vector3 delta = kind == EnemyKind.Warlock ? new Vector3(target.x - from.x, target.y - from.y, 0f) : Vector3.right * (target.x < from.x ? -distance : distance);
            int layers = Map.groundLayer | Map.fragileLayer;
            // Boomer's native spray is an arc: check launch clearance, not a fictitious straight trajectory.
            float length = kind == EnemyKind.Boomer ? Mathf.Min(28f, distance) : Mathf.Max(0f, delta.magnitude - 8f);
            return !Physics.CheckSphere(origin, kind == EnemyKind.Bazooka ? 3f : 2f, layers) &&
                !Physics.SphereCast(new Ray(origin, delta.normalized), kind == EnemyKind.Bazooka ? 3f : 2f, length, layers);
        }

        private static bool GroundPursue(PolymorphicAI ai, Mook mook, Memory memory)
        {
            Vector2 point = CooperativePoint(ai, mook, memory);
            GridPoint goal = new GridPoint(Mathf.Clamp(Map.GetCollumn(point.x), Mathf.Max(1, memory.AnchorColumn - PursuitTiles), Mathf.Min(Map.Width - 2, memory.AnchorColumn + PursuitTiles)), Map.GetRow(point.y));
            if (KeepGroundPath(ai, mook, memory, goal)) return true;
            if (mook.IsOnGround() && PursuePoint(ai, mook, memory, point)) return true;
            return TryGroundPath(ai, mook, memory, goal);
        }

        private static bool GroundPoint(Mook mook, GridPoint point)
        {
            if (point.collumn < 1 || point.collumn >= Map.Width - 1 || point.row < 1 || point.row >= Map.Height - 3 || !Map.IsBlockSolid(point.collumn, point.row - 1)) return false;
            float height = mook is MookDog ? DogHeight((MookDog)mook) : Mathf.Max(mook.height, mook.headHeight);
            Vector3 center = new Vector3(point.collumn * 16f, point.row * 16f - 7f + height * 0.5f, 0f);
            return !Physics.CheckBox(center, new Vector3(mook.HalfWidth, height * 0.5f, 1f), Quaternion.identity, Map.groundLayer | Map.fragileLayer);
        }

        private static int PathTerrain(NavPath path, Mook mook)
        {
            int stamp = 17, head = Mathf.CeilToInt(Mathf.Max(mook.height, mook.headHeight) / 16f);
            unchecked {
                foreach (PathPoint node in path.points)
                    for (int x = node.collumn - 1; x <= node.collumn + 1; x++)
                        for (int y = node.row - 1; y <= node.row + head; y++)
                            stamp = stamp * 31 + (Map.IsBlockSolid(x, y) ? 1 : 0);
            }
            return stamp;
        }

        private static bool KeepGroundPath(PolymorphicAI ai, Mook mook, Memory memory, GridPoint goal)
        {
            if (memory.PathGoal == null || !HasAction(ai, EnemyActionType.FollowPath)) return false;
            PathAgent agent = ai.GetComponent<PathAgent>();
            bool valid = agent.CurrentPath != null && agent.CurrentPath.TargetPoint == memory.PathGoal && GroundPoint(mook, memory.PathGoal) && PathTerrain(agent.CurrentPath, mook) == memory.PathTerrain;
            if (valid && (Time.time < memory.NextPath || (System.Math.Abs(goal.collumn - memory.PathGoal.collumn) <= 2 && System.Math.Abs(goal.row - memory.PathGoal.row) <= 1))) return true;
            Begin(ai, memory, Plan.None, 0, 0); return false;
        }

        private static bool TryGroundPath(PolymorphicAI ai, Mook mook, Memory memory, GridPoint goal)
        {
            PathAgent agent = ai.GetComponent<PathAgent>();
            if (agent == null || agent.capabilities == null || Time.time < memory.NextPath ||
                System.Math.Abs(goal.collumn - memory.AnchorColumn) > PursuitTiles || System.Math.Abs(goal.row - mook.row) > PursuitTiles || !GroundPoint(mook, goal)) return false;
            memory.NextPath = Time.time + 1f;
            NavPath path = PathfindingController.FindPath(new GridPoint(mook.collumn, mook.row), goal, agent.capabilities, 2f);
            // Exhausted searches return partial paths too. Never label them as a route to the goal.
            if (path == null || path.TargetPoint != goal || path.points.Count < 2) return false;
            foreach (PathPoint node in path.points)
                if (System.Math.Abs(node.collumn - memory.AnchorColumn) > PursuitTiles || node.collumn < 1 || node.collumn >= Map.Width - 1 || node.row < 1 || node.row >= Map.Height - 2) return false;
            Begin(ai, memory, Plan.Pursue, goal.collumn, System.Math.Sign(goal.collumn - mook.collumn));
            followNative.Invoke(ai, new object[] { path });
            memory.PathGoal = goal; memory.PathTerrain = PathTerrain(path, mook); Searches++;
            return true;
        }

        private static bool FindFiringPosition(PolymorphicAI ai, Mook mook, Memory memory, EnemyKind kind)
        {
            if (KeepGroundPath(ai, mook, memory, memory.PathGoal ?? new GridPoint(mook.collumn, mook.row))) return true;
            if (Time.time < memory.NextPath) return false;
            int direction = memory.LastSeen.x < mook.X ? -1 : 1;
            int target = Map.GetCollumn(memory.LastSeen.x), row = Map.GetRow(memory.LastSeen.y);
            GridPoint[] candidates = { new GridPoint(mook.collumn + direction * 2, mook.row), new GridPoint(mook.collumn + direction * 2, mook.row + 2),
                new GridPoint(target - direction * 6, row), new GridPoint(mook.collumn - direction * 2, mook.row) };
            // Four firing candidates plus one known-position fallback, at most 10ms once per second.
            foreach (GridPoint candidate in candidates)
            {
                if (System.Math.Abs(candidate.collumn - memory.AnchorColumn) > PursuitTiles || !GroundPoint(mook, candidate) ||
                    OccupiedByAlly(ai, mook, candidate) ||
                    !GroundShot(kind, mook, new Vector2(candidate.collumn * 16f, candidate.row * 16f - 8f), memory.LastSeen)) continue;
                memory.NextPath = 0f;
                if (TryGroundPath(ai, mook, memory, candidate)) return true;
            }
            // A firing-position miss must not consume the only slot for reaching a visible step/ladder.
            memory.NextPath = 0f;
            GridPoint known = new GridPoint(Mathf.Clamp(Map.GetCollumn(memory.LastSeen.x), Mathf.Max(1, memory.AnchorColumn - PursuitTiles), Mathf.Min(Map.Width - 2, memory.AnchorColumn + PursuitTiles)), Map.GetRow(memory.LastSeen.y));
            if (TryGroundPath(ai, mook, memory, known)) return true;
            memory.NextPath = Time.time + 1f; return false;
        }

        internal static void MaintainGroundPath(PolymorphicAI ai, Mook mook)
        {
            Memory memory;
            if (mook == null || !Supported(ai, mook) || GroundPathNativePhase(ai, mook) || !UsesGroundPath(ai, mook) ||
                !memories.TryGetValue(ai, out memory) || memory.PathGoal == null || !HasAction(ai, EnemyActionType.FollowPath)) return;
            // Native RunQueue would retry a vanished/stuck path every frame with a 10ms search.
            PathAgent agent = ai.GetComponent<PathAgent>();
            if (agent.CurrentPath == null) Begin(ai, memory, Plan.None, 0, 0);
        }

        internal static void SyncGroundPath(PolymorphicAI ai, Mook mook)
        {
            if (!Plugin.Enabled || mook == null || ai.IsMine || mook.IsMine || !UsesGroundPath(ai, mook) ||
                mook.playerNum >= 0 || mook.beingControlledByTriggerAction || mook.health <= 0 ||
                Map.isEditing || GameModeController.IsLevelFinished() || GroundPathNativePhase(ai, mook)) return;
            PathAgent agent = ai.GetComponent<PathAgent>();
            if (agent == null || agent.CurrentPath == null) return;
            ActionObject follow = ai.actionQueue.Find(a => a.type == EnemyActionType.FollowPath);
            if (follow == null || follow.gridPoint != agent.CurrentPath.TargetPoint) agent.CurrentPath = null;
        }
    }

    [HarmonyPatch(typeof(PolymorphicAI), "RunQueue")]
    internal static class MaintainNativePursuit
    {
        private static void Prefix(PolymorphicAI __instance, Mook ___mook) { Controller.MaintainGroundPath(__instance, ___mook); }
    }
}
