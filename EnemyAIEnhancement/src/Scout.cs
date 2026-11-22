using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace EnemyAIEnhancement
{
    internal static partial class Controller
    {
        private static readonly FieldInfo scoutPoint = typeof(ScoutAI).GetField("mookInterestPoint", BindingFlags.Instance | BindingFlags.NonPublic);
        private static readonly FieldInfo scoutStart = typeof(ScoutAI).GetField("startPoint", BindingFlags.Instance | BindingFlags.NonPublic);
        private static readonly FieldInfo scoutDirection = typeof(ScoutAI).GetField("runDirection", BindingFlags.Instance | BindingFlags.NonPublic);
        private static readonly FieldInfo queueChanged = typeof(PolymorphicAI).GetField("hasActionQueueChanged", BindingFlags.Instance | BindingFlags.NonPublic);

        private static bool ScoutCanDecide(ScoutAI ai, Mook mook)
        {
            return mook != null && Kind(ai, mook) == EnemyKind.Scout && Supported(ai, mook) &&
                !mook.IsIncapacitated() && mook.blindTime <= 0f;
        }

        internal static bool PrepareScoutAlert(ScoutAI ai, Mook mook)
        {
            if (!ScoutCanDecide(ai, mook) || ai.mentalState == MentalState.Alerted ||
                ai.mentalState == MentalState.Panicking || ai.mentalState == MentalState.Attracted) return false;
            // Let the original FullyAlert build its complete warning/escape/door sequence.
            scoutPoint.SetValue(ai, null);
            scoutDirection.SetValue(ai, DirectionEnum.None);
            memories.Remove(ai);
            return true;
        }

        private static bool ValidScoutPoint(ScoutMookInterestPoint point)
        {
            return point != null && point.isActiveAndEnabled && Map.mookInterestPoints.Contains(point) &&
                point.collumn > 0 && point.collumn < Map.Width && point.row > 0 && point.row < Map.Height &&
                !Map.IsBlockSolid(point.collumn, point.row) &&
                (point.GetMookDoor() == null || !point.GetMookDoor().isDestroyed);
        }

        private static bool ScoutCanReach(PathAgent agent, GridPoint from, GridPoint goal)
        {
            NavPath path = PathfindingController.FindPath(from, goal, agent.capabilities, 2f);
            // Native FindPath also returns partial paths after exhaustion/time limits.
            return path != null && path.TargetPoint == goal;
        }

        internal static ScoutMookInterestPoint SelectScoutPoint(int column, int row, ScoutAI ai)
        {
            Mook mook = ai.GetComponent<Mook>();
            if (!ScoutCanDecide(ai, mook)) return Map.GetNearestMookInterestPoint(column, row);
            PathAgent agent = ai.GetComponent<PathAgent>();
            if (agent == null) return null;
            GridPoint from = Map.GetGridPoint(mook.transform.position);
            List<ScoutMookInterestPoint> candidates = Map.mookInterestPoints.FindAll(ValidScoutPoint);
            candidates.Sort(delegate(ScoutMookInterestPoint a, ScoutMookInterestPoint b) {
                return (System.Math.Abs(a.collumn - from.collumn) + System.Math.Abs(a.row - from.row)).CompareTo(
                    System.Math.Abs(b.collumn - from.collumn) + System.Math.Abs(b.row - from.row));
            });
            // At alert/invalidation only: at most eight native searches, two milliseconds each.
            for (int i = 0; i < candidates.Count && i < 8; i++)
                if (ScoutCanReach(agent, from, new GridPoint(candidates[i].collumn, candidates[i].row))) return candidates[i];
            return null;
        }

        internal static void TrackScout(ScoutAI ai, Mook mook)
        {
            if (!ScoutCanDecide(ai, mook)) return;
            foreach (ActionObject action in ai.actionQueue)
                if (action.type == EnemyActionType.FollowPath)
                {
                    Memory memory = GetMemory(ai, mook);
                    memory.Plan = Plan.Escape;
                    memory.ScoutPoint = (ScoutMookInterestPoint)scoutPoint.GetValue(ai);
                    memory.ScoutGoal = new GridPoint(action.gridPoint.collumn, action.gridPoint.row);
                    memory.NextSense = 0f;
                    Plans++;
                    return;
                }
        }

        internal static void RefreshScout(ScoutAI ai, Mook mook)
        {
            if (!ScoutCanDecide(ai, mook) || !ai.IsAlerted()) { memories.Remove(ai); return; }
            ActionObject follow = ai.actionQueue.Find(delegate(ActionObject a) { return a.type == EnemyActionType.FollowPath; });
            // Never interrupt the committed UseSpecial2 / ReachDoor / post-door actions.
            if (follow == null) { memories.Remove(ai); return; }
            Memory memory;
            if (!memories.TryGetValue(ai, out memory)) { TrackScout(ai, mook); memory = memories[ai]; }
            if (ReferenceEquals(memory.ScoutPoint, null) || Time.time < memory.NextSense) return;
            memory.NextSense = Time.time + 1f;
            PathAgent agent = ai.GetComponent<PathAgent>();
            ScoutMookInterestPoint point = memory.ScoutPoint;
            if (ValidScoutPoint(point) && memory.ScoutGoal.collumn == point.collumn && memory.ScoutGoal.row == point.row &&
                ScoutCanReach(agent, Map.GetGridPoint(mook.transform.position), memory.ScoutGoal)) return;
            point = SelectScoutPoint(mook.collumn, mook.row, ai);
            GridPoint goal;
            if (point != null) goal = new GridPoint(point.collumn, point.row);
            else
            {
                GridPoint start = (GridPoint)scoutStart.GetValue(ai);
                DirectionEnum direction = (DirectionEnum)scoutDirection.GetValue(ai);
                if (direction == DirectionEnum.None) direction = mook.X < start.collumn * 16f ? DirectionEnum.Left : DirectionEnum.Right;
                scoutDirection.SetValue(ai, direction);
                goal = new GridPoint(Mathf.Clamp(mook.collumn + (direction == DirectionEnum.Left ? -32 : 32), 1, Map.Width - 1), mook.row);
            }
            bool afterFollow = false;
            foreach (ActionObject action in ai.actionQueue)
            {
                if (action == follow) { action.gridPoint = goal; afterFollow = true; }
                else if (afterFollow && action.type == EnemyActionType.Move && action.gridPoint != null)
                    action.gridPoint = new GridPoint(goal.collumn + action.gridPoint.collumn - memory.ScoutGoal.collumn,
                        goal.row + action.gridPoint.row - memory.ScoutGoal.row);
            }
            scoutPoint.SetValue(ai, point);
            memory.ScoutPoint = point; memory.ScoutGoal = goal;
            agent.CurrentPath = null;
            queueChanged.SetValue(ai, true);
            Plans++;
        }

        internal static void SyncScout(PolymorphicAI ai, Mook mook)
        {
            if (!Plugin.Enabled || mook == null || Kind(ai, mook) != EnemyKind.Scout || ai.IsMine) return;
            PathAgent agent = ai.GetComponent<PathAgent>();
            ActionObject follow = ai.actionQueue.Find(delegate(ActionObject a) { return a.type == EnemyActionType.FollowPath; });
            if (agent != null && agent.CurrentPath != null && follow != null && agent.CurrentPath.TargetPoint != follow.gridPoint)
                agent.CurrentPath = null;
        }
    }
}
