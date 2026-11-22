using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace EnemyAIEnhancement
{
    internal static partial class Controller
    {
        private static readonly FieldInfo jetSpiral = typeof(MookJetpack).GetField("spiralling", BindingFlags.Instance | BindingFlags.NonPublic);
        private static readonly FieldInfo jetWarning = typeof(MookJetpack).GetField("spiralDelay", BindingFlags.Instance | BindingFlags.NonPublic);
        private static readonly FieldInfo jetDeath = typeof(MookJetpack).GetField("deathCounter", BindingFlags.Instance | BindingFlags.NonPublic);
        private static readonly FieldInfo jetDelay = typeof(MookJetpack).GetField("jetpacksDelay", BindingFlags.Instance | BindingFlags.NonPublic);
        private static readonly FieldInfo jetDecapitated = typeof(Mook).GetField("decapitated", BindingFlags.Instance | BindingFlags.NonPublic);
        private static readonly Dictionary<MookJetpack, float> jetpackHeights = new Dictionary<MookJetpack, float>();

        private static bool IsJetpack(EnemyKind kind) { return kind == EnemyKind.Jetpack || kind == EnemyKind.JetpackRocket; }

        private static bool JetpackNativePhase(MookJetpack mook)
        {
            return !mook.UsingJetpack() || (float)jetDelay.GetValue(mook) > 0f || mook.IsIncapacitated() ||
                mook.blindTime > 0f || (bool)jetDecapitated.GetValue(mook) || (bool)jetSpiral.GetValue(mook) ||
                (float)jetWarning.GetValue(mook) > 0f || (float)jetDeath.GetValue(mook) > 0f;
        }

        // Sweep actual body space. Air routes deliberately require no supporting floor.
        private static bool JetpackSpace(MookJetpack mook, float x, float y)
        {
            if (x - mook.HalfWidth < 0f || x + mook.HalfWidth >= Map.Width * 16f ||
                y < 0f || y + mook.height >= Map.Height * 16f) return false;
            Vector3 center = new Vector3(mook.X, mook.Y + (mook.height + 1f) * 0.5f, 0f);
            Vector3 extents = new Vector3(mook.HalfWidth + 1f, (mook.height - 1f) * 0.5f, 1f);
            Vector3 delta = new Vector3(x - mook.X, y - mook.Y, 0f);
            int layers = Map.groundLayer | Map.fragileLayer;
            return !Physics.CheckBox(center, extents, Quaternion.identity, layers) &&
                !Physics.CheckBox(center + delta, extents, Quaternion.identity, layers) &&
                (delta.sqrMagnitude < 0.01f || !Physics.BoxCast(center, extents, delta.normalized, Quaternion.identity, delta.magnitude, layers));
        }

        private static void JetpackAimHeight(MookJetpack mook, Memory memory)
        {
            float original;
            if (!jetpackHeights.TryGetValue(mook, out original)) original = mook.jetPackHeight;
            float desired = Mathf.Clamp(memory.LastSeen.y + 12f, original - 48f, original + 48f);
            desired = Mathf.Max(mook.groundHeight + 12f, desired);
            // Leave a margin for the native hover model's small oscillation.
            if (!JetpackSpace(mook, mook.X, desired - 6f) || !JetpackSpace(mook, mook.X, desired + 12f)) return;
            if (!jetpackHeights.ContainsKey(mook)) jetpackHeights.Add(mook, original);
            mook.jetPackHeight = desired;
        }

        private static bool JetpackMove(PolymorphicAI ai, MookJetpack mook, Memory memory, int direction, Plan plan)
        {
            int destination = mook.collumn;
            for (int step = 1; step <= 2; step++)
            {
                int column = mook.collumn + direction * step;
                if (System.Math.Abs(column - memory.AnchorColumn) > PursuitTiles || !JetpackSpace(mook, column * 16f, mook.Y)) break;
                destination = column;
            }
            if (destination == mook.collumn) return false;
            if (memory.Plan == plan && memory.Direction == direction && ai.actionQueue.Count > 0 &&
                ai.actionQueue[0].type == EnemyActionType.Move && JetpackSpace(mook, memory.Destination * 16f, mook.Y)) return true;
            Begin(ai, memory, plan, destination, direction);
            ai.AddAction(EnemyActionType.Move, new GridPoint(destination, mook.row));
            ai.AddAction(EnemyActionType.Wait, 0.15f);
            Searches++;
            return true;
        }

        private static bool JetpackShot(MookJetpack mook, Memory memory)
        {
            int direction = memory.LastSeen.x < mook.X ? -1 : 1;
            Vector3 muzzle = new Vector3(mook.X, mook.Y + 8f, 0f);
            float distance = Mathf.Abs(memory.LastSeen.x - mook.X);
            int layers = Map.groundLayer | Map.fragileLayer;
            if (Physics.SphereCast(new Ray(muzzle, Vector3.right * direction), 3f, 24f, layers)) return false;
            if (mook.GetType() == typeof(MookJetpackBazooka))
                return memory.Player >= 0 && HeroController.PlayerIsAlive(memory.Player) &&
                    Vector2.Distance(memory.LastSeen, new Vector2(mook.X, mook.Y)) >= 80f;
            return Mathf.Abs(memory.LastSeen.y - mook.Y) <= 20f && distance >= 24f &&
                !Physics.SphereCast(new Ray(muzzle, Vector3.right * direction), 2f, Mathf.Max(0f, distance - 8f), layers);
        }

        private static bool JetpackCombat(PolymorphicAI ai, MookJetpack mook, Memory memory, bool force)
        {
            if (memory.FlightCooldown != null && ai.actionQueue.Contains(memory.FlightCooldown)) return true;
            if (memory.Plan == Plan.None && ai.actionQueue.Count > 0 && ai.actionQueue[0].type == EnemyActionType.Wait) return true;
            // Keep an already submitted native shot and its following cooldown, including after a toggle.
            if (HasAction(ai, EnemyActionType.Fire))
            {
                int fire = ai.actionQueue.FindIndex(a => a.type == EnemyActionType.Fire);
                if (fire + 1 < ai.actionQueue.Count && ai.actionQueue[fire + 1].type == EnemyActionType.Wait)
                    memory.FlightCooldown = ai.actionQueue[fire + 1];
                return true;
            }
            bool rocket = mook.GetType() == typeof(MookJetpackBazooka);
            if (!rocket) JetpackAimHeight(mook, memory);
            float distance = Mathf.Abs(memory.LastSeen.x - mook.X);
            int direction = memory.LastSeen.x < mook.X ? -1 : 1;
            if (rocket && Vector2.Distance(memory.LastSeen, new Vector2(mook.X, mook.Y)) < 80f)
            {
                if (!Plugin.Settings.PursueTargets || !JetpackMove(ai, mook, memory, -direction, Plan.Space)) Guard(ai, mook, memory, force);
                return true;
            }
            if (Plugin.Settings.PursueTargets && distance > (rocket ? 192f : 128f) &&
                JetpackMove(ai, mook, memory, direction, Plan.Pursue)) return true;
            if (!JetpackShot(mook, memory)) { Guard(ai, mook, memory, force); return true; }
            Begin(ai, memory, Plan.Attack, 0, direction);
            ai.AddAction(EnemyActionType.FacePoint, new GridPoint(mook.collumn + direction, mook.row), 0f);
            ai.AddAction(EnemyActionType.Wait, Mathf.Max(0.2f, ai.sightDelay));
            ai.AddAction(EnemyActionType.Fire, ai.attackTime);
            ai.AddAction(EnemyActionType.Wait, Mathf.Max(rocket ? 1.4f : 0.2f, ai.delayBetweenFiring));
            memory.FlightCooldown = ai.actionQueue[ai.actionQueue.Count - 1];
            return true;
        }

        private static bool JetpackSearch(PolymorphicAI ai, MookJetpack mook, Memory memory, bool force)
        {
            // Lost targets never receive a new seeking Target or speculative Fire.
            if (memory.FlightCooldown != null && ai.actionQueue.Contains(memory.FlightCooldown))
            {
                ActionObject cooldown = memory.FlightCooldown;
                ai.ClearActionQueue(true);
                ai.actionQueue.Add(cooldown);
                return true;
            }
            if (Plugin.Settings.PursueTargets && Mathf.Abs(memory.LastSeen.x - mook.X) > 24f &&
                JetpackMove(ai, mook, memory, memory.LastSeen.x < mook.X ? -1 : 1, Plan.Pursue)) return true;
            Search(ai, mook, memory, force);
            return true;
        }

        private static void RestoreJetpackHeight(MookJetpack mook)
        {
            if (mook == null) return;
            float original;
            if (!jetpackHeights.TryGetValue(mook, out original)) return;
            if (mook.IsMine) mook.jetPackHeight = original;
            jetpackHeights.Remove(mook);
        }

        private static void RestoreJetpackHeights()
        {
            foreach (KeyValuePair<MookJetpack, float> pair in jetpackHeights)
                if (pair.Key != null && pair.Key.IsMine) pair.Key.jetPackHeight = pair.Value;
            jetpackHeights.Clear();
        }
    }
}
