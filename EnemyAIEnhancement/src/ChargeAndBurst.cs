using System.Reflection;
using UnityEngine;

namespace EnemyAIEnhancement
{
    internal static partial class Controller
    {
        private static readonly FieldInfo litFuse = typeof(SuicideAI).GetField("hasLitFuse",
            BindingFlags.Instance | BindingFlags.NonPublic);
        private static readonly FieldInfo liftingOff = typeof(MookArmouredGuy).GetField("liftingOff", BindingFlags.Instance | BindingFlags.NonPublic);
        private static readonly FieldInfo landing = typeof(MookArmouredGuy).GetField("landing", BindingFlags.Instance | BindingFlags.NonPublic);
        private static readonly FieldInfo deathCount = typeof(MookArmouredGuy).GetField("deathCount", BindingFlags.Instance | BindingFlags.NonPublic);
        private static readonly FieldInfo feetWidth = typeof(TestVanDammeAnim).GetField("feetWidth", BindingFlags.Instance | BindingFlags.NonPublic);

        private static bool ArmouredNativePhase(Mook mook)
        {
            MookArmouredGuy mech = mook as MookArmouredGuy;
            return mech != null && (mech.playerNum >= 0 || (mech.pilotUnit != null && mech.pilotUnit.playerNum >= 0) ||
                (bool)liftingOff.GetValue(mech) || (bool)landing.GetValue(mech) || mech.up || mech.buttonJump ||
                (int)deathCount.GetValue(mech) > 2 || !mech.IsOnGround() || mech.IsIncapacitated() || mech.blindTime > 0f);
        }

        private static bool ArmouredStep(Mook mook, int column)
        {
            float destination = column * 16f;
            float left = Mathf.Min(mook.X, destination), right = Mathf.Max(mook.X, destination);
            float feet = (float)feetWidth.GetValue(mook);
            for (int c = Map.GetCollumn(left - feet); c <= Map.GetCollumn(right + feet); c++)
                if (!Map.IsBlockSolid(c, mook.row - 1)) return false;
            for (int c = Map.GetCollumn(left - mook.HalfWidth); c <= Map.GetCollumn(right + mook.HalfWidth); c++)
                for (int r = Map.GetRow(mook.Y + 1f); r <= Map.GetRow(mook.Y + mook.height); r++)
                    if (Map.IsBlockSolid(c, r)) return false;
            // Sweep the full body as doors/fragile colliders need not be present in Map.blocks.
            Vector3 center = new Vector3(mook.X, mook.Y + (mook.height + 1f) * 0.5f, 0f);
            Vector3 extents = new Vector3(mook.HalfWidth, (mook.height - 1f) * 0.5f, 1f);
            int layers = Map.groundLayer | Map.fragileLayer;
            return !Physics.CheckBox(center, extents, Quaternion.identity, layers) &&
                !Physics.BoxCast(center, extents, Vector3.right * Mathf.Sign(destination - mook.X),
                    Quaternion.identity, Mathf.Abs(destination - mook.X), layers);
        }

        private static bool SuicideCommitted(PolymorphicAI ai)
        {
            return ai.GetType() == typeof(SuicideAI) && (bool)litFuse.GetValue(ai);
        }

        private static bool HasAction(PolymorphicAI ai, EnemyActionType type)
        {
            for (int i = 0; i < ai.actionQueue.Count; i++)
                if (ai.actionQueue[i].type == type) return true;
            return false;
        }

        private static bool SuicideCombat(PolymorphicAI ai, Mook mook, Memory memory, bool force)
        {
            if (TryPursue(ai, mook, memory)) return true;
            // A point-blank target still needs native movement input to ignite the fuse.
            int direction = memory.LastSeen.x < mook.X ? -1 : 1;
            int destination = mook.collumn + direction;
            if (Mathf.Abs(memory.LastSeen.y - mook.Y) <= 20f &&
                Mathf.Abs(memory.LastSeen.x - mook.X) < 18f &&
                System.Math.Abs(destination - memory.AnchorColumn) <= PursuitTiles && SafeStep(mook, destination))
            {
                Begin(ai, memory, Plan.Pursue, destination, direction);
                ai.AddAction(EnemyActionType.Move, new GridPoint(destination, mook.row));
            }
            else Guard(ai, mook, memory, force);
            return true;
        }

        private static bool BruiserCombat(PolymorphicAI ai, Mook mook, Memory memory, bool force)
        {
            if (Plugin.Settings.PursueTargets && Mathf.Abs(memory.LastSeen.x - mook.X) > 128f &&
                TryPursue(ai, mook, memory)) return true;
            int direction = memory.LastSeen.x < mook.X ? -1 : 1;
            Begin(ai, memory, Plan.Attack, 0, direction);
            ai.AddAction(EnemyActionType.FacePoint, new GridPoint(mook.collumn + direction, mook.row), 0f);
            ai.AddAction(EnemyActionType.Wait, Mathf.Max(0.2f, ai.sightDelay));
            // Awake already applies native Hard-mode modifiers to both timings.
            ai.AddAction(EnemyActionType.Fire, ai.attackTime);
            ai.AddAction(EnemyActionType.Wait, 0.4f);
            ai.AddAction(EnemyActionType.Wait, ai.delayBetweenFiring);
            return true;
        }
    }
}
