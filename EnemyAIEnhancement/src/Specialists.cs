using System;
using System.Reflection;
using UnityEngine;

namespace EnemyAIEnhancement
{
    internal enum EnemyKind { Unsupported, Trooper, Bazooka, Grenadier, Shield, Suicide, Bruiser, Scout, Jetpack, JetpackRocket, Facehugger, Xenomorph, Dog, UndeadTrooper, UndeadSuicide, Warlock, Boomer, Executioner, LostSoul, Skinless, Brute, Melter, Mosquito, Elite, General, Alarmist }

    internal static partial class Controller
    {
        private static readonly FieldInfo throwing = typeof(MookGrenadier).GetField("usingGrenade",
            BindingFlags.Instance | BindingFlags.NonPublic);

        // Each additional species needs its own reviewed unit/AI pair and combat behavior.
        // Shared ownership, memory, terrain and queue replication remain in Controller.cs.
        internal static EnemyKind Kind(PolymorphicAI ai, Mook mook)
        {
            Type unitType = mook.GetType(), aiType = ai.GetType();
            if (unitType == typeof(MookBigGuyElite) && aiType == typeof(BigGuyEliteAI)) return EnemyKind.Elite;
            if (unitType == typeof(MookGeneral) && aiType == typeof(SatanAI)) return EnemyKind.General;
            if (unitType == typeof(MookGeneral) && aiType == typeof(MookTrooperAI)) return EnemyKind.Alarmist;
            if (unitType == typeof(SkinnedMook) && aiType == typeof(SkinlessMookAI)) return EnemyKind.Skinless;
            if (unitType == typeof(AlienBrute) && aiType == typeof(AlienBruteAI)) return EnemyKind.Brute;
            if (unitType == typeof(AlienMelter) && aiType == typeof(AlienMelterAI)) return EnemyKind.Melter;
            if (unitType == typeof(AlienMosquito) && aiType == typeof(AlienMosquitoAI)) return EnemyKind.Mosquito;
            if (unitType == typeof(Warlock) && aiType == typeof(MookWarlockAI)) return EnemyKind.Warlock;
            if (unitType == typeof(MookHellBoomer) && aiType == typeof(BoomerAI)) return EnemyKind.Boomer;
            if (unitType == typeof(MookHellBigGuy) && aiType == typeof(BigGuyAI)) return EnemyKind.Executioner;
            if (unitType == typeof(HellLostSoul) && aiType == typeof(LostSoulAI)) return EnemyKind.LostSoul;
            if (unitType == typeof(UndeadTrooper) && aiType == typeof(UndeadTrooperAI)) return EnemyKind.UndeadTrooper;
            if (unitType == typeof(MookSuicideUndead) && aiType == typeof(UndeadSuicideAI)) return EnemyKind.UndeadSuicide;
            if ((unitType == typeof(MookDog) && aiType == typeof(DogAI)) ||
                (unitType == typeof(HellDog) && aiType == typeof(HellDogAI))) return EnemyKind.Dog;
            if (unitType == typeof(AlienXenomorph) && aiType == typeof(XenomorphAI)) return EnemyKind.Xenomorph;
            if (unitType == typeof(AlienFaceHugger) && aiType == typeof(FacehuggerAI)) return EnemyKind.Facehugger;
            if (unitType == typeof(MookJetpack) && aiType == typeof(MookTrooperAI)) return EnemyKind.Jetpack;
            if (unitType == typeof(MookJetpackBazooka) && aiType == typeof(MookBazookaAi)) return EnemyKind.JetpackRocket;
            if (unitType == typeof(ScoutMook) && aiType == typeof(ScoutAI)) return EnemyKind.Scout;
            if (unitType == typeof(MookNinja) && aiType == typeof(MookNinjaAI)) return EnemyKind.Trooper;
            if (unitType == typeof(MookTrooper) && aiType == typeof(MookTrooperAI)) return EnemyKind.Trooper;
            if (unitType == typeof(MookBazooka) && aiType == typeof(MookBazookaAi)) return EnemyKind.Bazooka;
            if (unitType == typeof(MookGrenadier) && aiType == typeof(MookBazookaAi)) return EnemyKind.Grenadier;
            if (unitType == typeof(MookRiotShield) && aiType == typeof(PolymorphicAI)) return EnemyKind.Shield;
            if (unitType == typeof(MookSuicide) && aiType == typeof(SuicideAI)) return EnemyKind.Suicide;
            if ((unitType == typeof(MookBigGuy) || unitType == typeof(MookArmouredGuy)) &&
                aiType == typeof(BigGuyAI)) return EnemyKind.Bruiser;
            return EnemyKind.Unsupported;
        }

        private static bool SpecialistCombat(PolymorphicAI ai, Mook mook, Memory memory, bool force)
        {
            EnemyKind kind = Kind(ai, mook);
            float distance = Mathf.Abs(memory.LastSeen.x - mook.X);
            if (kind == EnemyKind.Shield)
            {
                if (Plugin.Settings.PursueTargets && distance > 32f && TryPursue(ai, mook, memory)) return true;
                Guard(ai, mook, memory, force);
                return true;
            }
            float minimum = kind == EnemyKind.Bazooka ? 64f : 80f;
            if (distance < minimum)
            {
                if (!TrySpace(ai, mook, memory, minimum)) Guard(ai, mook, memory, force);
                return true;
            }
            if (Plugin.Settings.PursueTargets && distance > 160f && TryPursue(ai, mook, memory)) return true;
            int direction = memory.LastSeen.x < mook.X ? -1 : 1;
            // Rockets travel horizontally. Grenades need room at the hand and over the thrower;
            // this clearance check deliberately does not pretend to predict the random native arc.
            Vector3 start = new Vector3(mook.X, mook.Y + (kind == EnemyKind.Bazooka ? 8f : 24f), 0f);
            bool blocked = kind == EnemyKind.Bazooka
                ? Mathf.Abs(memory.LastSeen.y - mook.Y) > 20f || Physics.SphereCast(new Ray(start, Vector3.right * direction),
                    3f, distance - 8f, Map.groundLayer | Map.fragileLayer)
                : Mathf.Abs(memory.LastSeen.y - mook.Y) > 48f ||
                    Physics.CheckSphere(start + Vector3.right * direction * 8f, 4f, Map.groundLayer | Map.fragileLayer) ||
                    Physics.SphereCast(new Ray(start, Vector3.right * direction), 4f, 24f, Map.groundLayer | Map.fragileLayer) ||
                    Physics.SphereCast(new Ray(start, Vector3.up), 4f, 16f, Map.groundLayer | Map.fragileLayer);
            if (blocked) { Guard(ai, mook, memory, force); return true; }
            if (!force && memory.Plan == Plan.Attack && memory.Direction == direction && ai.actionQueue.Count > 0)
                return true;
            Begin(ai, memory, Plan.Attack, 0, direction);
            ai.AddAction(EnemyActionType.FacePoint, new GridPoint(mook.collumn + direction, mook.row), 0f);
            ai.AddAction(EnemyActionType.Wait, Mathf.Max(0.2f, ai.sightDelay));
            ai.AddAction(EnemyActionType.Fire, 0.1f);
            ai.AddAction(EnemyActionType.Wait, Mathf.Max(kind == EnemyKind.Bazooka ? 1.4f : 1.6f, ai.delayBetweenFiring));
            return true;
        }

        private static bool TrySpace(PolymorphicAI ai, Mook mook, Memory memory, float minimum)
        {
            int direction = memory.LastSeen.x < mook.X ? 1 : -1;
            int destination = mook.collumn;
            for (int step = 1; step <= 2; step++)
            {
                int column = mook.collumn + direction * step;
                if (System.Math.Abs(column - memory.AnchorColumn) > PursuitTiles || !SafeStep(mook, column)) break;
                destination = column;
                if (Mathf.Abs(column * 16f - memory.LastSeen.x) >= minimum) break;
            }
            if (destination == mook.collumn) return false;
            if (memory.Plan == Plan.Space && ai.actionQueue.Count > 0 && memory.Direction == direction &&
                SafeRoute(mook, memory.Destination)) return true;
            Begin(ai, memory, Plan.Space, destination, direction);
            ai.AddAction(EnemyActionType.Move, new GridPoint(destination, mook.row));
            ai.AddAction(EnemyActionType.Wait, 0.15f);
            return true;
        }

        private static void Guard(PolymorphicAI ai, Mook mook, Memory memory, bool force)
        {
            int direction = memory.LastSeen.x < mook.X ? -1 : 1;
            if (!force && memory.Plan == Plan.Guard && memory.Direction == direction && ai.actionQueue.Count > 0) return;
            Begin(ai, memory, Plan.Guard, 0, direction);
            ai.AddAction(EnemyActionType.FacePoint, new GridPoint(mook.collumn + direction, mook.row), 0f);
            ai.AddAction(EnemyActionType.Wait, 0.3f);
        }

        internal static bool ThrowRange(PolymorphicAI ai, Mook mook, ref float x, ref float y)
        {
            Memory memory;
            if (!Supported(ai, mook) || !(mook is MookGrenadier) || !memories.TryGetValue(ai, out memory) ||
                memory.Player < 0 || !(bool)throwing.GetValue(mook)) return false;
            x = Mathf.Abs(memory.LastSeen.x - mook.X);
            y = memory.LastSeen.y - mook.Y;
            return true;
        }

        internal static void ReleaseShield(MookRiotShield mook)
        {
            PolymorphicAI ai = mook.enemyAI;
            Memory memory;
            if (ai != null && ai.IsMine && mook.IsMine && memories.TryGetValue(ai, out memory))
            {
                if (memory.Plan != Plan.None) ai.ClearActionQueue(true);
                memories.Remove(ai);
            }
        }
    }
}
