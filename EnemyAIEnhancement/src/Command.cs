using System;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace EnemyAIEnhancement
{
    internal static partial class Controller
    {
        private static readonly FieldInfo eliteCharging = AccessTools.Field(typeof(BigGuyEliteAI), "isCharging");
        private static readonly FieldInfo eliteIndex = AccessTools.Field(typeof(BigGuyEliteAI), "attackIndex");
        private static readonly FieldInfo eliteGetUp = AccessTools.Field(typeof(MookBigGuyElite), "getUpFrames");
        internal static int Reports, IntelReceived;

        private static bool CommandKind(EnemyKind kind)
        { return kind == EnemyKind.General || kind == EnemyKind.Alarmist; }

        private static bool Cooperative(PolymorphicAI ai, Mook mook)
        {
            EnemyKind kind = Kind(ai, mook);
            return CommandKind(kind) || kind == EnemyKind.Bazooka ||
                (kind == EnemyKind.Trooper && mook.GetType() == typeof(MookTrooper));
        }

        private static bool Allied(Mook a, Mook b)
        { return !GameModeController.DoesPlayerNumDamage(a.playerNum, b.playerNum) && !GameModeController.DoesPlayerNumDamage(b.playerNum, a.playerNum); }

        private static bool IntelReady(PolymorphicAI ai, Mook mook)
        {
            return Supported(ai, mook) && Cooperative(ai, mook) && !GroundNativePhase(ai, mook) &&
                (ai.mentalState == MentalState.Idle || ai.mentalState == MentalState.Suspicious || ai.mentalState == MentalState.Alerted);
        }

        private static void SharedPlayer(PolymorphicAI ai, Mook mook, ref int seenPlayer)
        {
            Memory memory;
            // Native unknown targets use -10; failed sight checks can also leave -1.
            if (seenPlayer < 0 && Cooperative(ai, mook) && memories.TryGetValue(ai, out memory) &&
                memory.SharedPending && memory.Player >= 0 && HeroController.PlayerIsAlive(memory.Player) &&
                Time.time - memory.SeenAt <= MemorySeconds && !PreferNativeNPC(ai, mook, false, Vector2.zero))
                seenPlayer = memory.Player;
        }

        private static bool IntelInterruptible(PolymorphicAI ai)
        {
            return !HasAction(ai, EnemyActionType.Jump) && !HasAction(ai, EnemyActionType.FollowPath) &&
                !HasAction(ai, EnemyActionType.GreetPlayer);
        }

        internal static bool PreferNativeNPC(PolymorphicAI ai, Unit mook, bool visible, Vector2 player)
        {
            int npc = int.MinValue; float x = 0f, y = 0f;
            if (!Map.CanSeeNPCEnemyUnit(mook.X, mook.Y, ai.FacingDirection, ai.sightRangeX, ai.sightRangeY, mook.playerNum, ref npc, ref x, ref y)) return false;
            // Resolve the actual native result: villagers can use faction 5 or a player's faction.
            for (int i = Map.units.Count - 1; i >= 0; i--)
            {
                Unit unit = Map.units[i];
                if (unit == null || unit.health <= 0 || unit.playerNum != npc || unit.X != x || unit.Y != y || unit.IsInStealthMode()) continue;
                if (npc >= 0 && npc < 4 && HeroController.players[npc] != null && HeroController.players[npc].character == unit) return false;
                return !visible || Vector2.Distance(new Vector2(mook.X, mook.Y), new Vector2(x, y)) < Vector2.Distance(new Vector2(mook.X, mook.Y), player);
            }
            return false;
        }

        internal static bool GeneralSpawnAllowed(MookGeneral mook)
        {
            PolymorphicAI ai = mook.enemyAI;
            if (!Plugin.Enabled || Kind(ai, mook) != EnemyKind.General || mook.playerNum >= 0 ||
                mook.beingControlledByTriggerAction || Map.isEditing) return true;
            return mook.IsMine && ai.IsMine;
        }

        internal static void LocalIntel(PolymorphicAI ai, Mook mook, ref int seenPlayer)
        {
            if (mook == null || !Cooperative(ai, mook)) return;
            Memory memory;
            memories.TryGetValue(ai, out memory);
            if (!IntelReady(ai, mook) || (memory != null && memory.IntelFaction != mook.playerNum))
            { memories.Remove(ai); return; }
            memory = GetMemory(ai, mook);
            float now = Time.time;
            if (now < memory.NextIntelSense) return;
            memory.NextIntelSense = now + SenseInterval;
            int target = seenPlayer;
            bool visible = HeroController.CanSeePlayer(mook.X, mook.Y, 0, ai.sightRangeX, ai.sightRangeY, ref target) &&
                target >= 0 && target < 4 && HeroController.PlayerIsAlive(target) && GameModeController.DoesPlayerNumDamage(mook.playerNum, target);
            Vector2 point = visible ? (Vector2)HeroController.GetPlayerPosition(target) : Vector2.zero;
            if (PreferNativeNPC(ai, mook, visible, point)) { memories.Remove(ai); return; }
            if (visible)
            {
                memory.Player = target; memory.LastSeen = point; memory.SeenAt = now;
                memory.DirectObservation = true; seenPlayer = target;
                if (!ai.IsAlerted()) { ai.SetMentalState(MentalState.Alerted); memory.SharedPending = true; }
            }
            if (!Plugin.Settings.RememberTargets || !memory.DirectObservation || memory.Player < 0 ||
                !HeroController.PlayerIsAlive(memory.Player) || now - memory.SeenAt > MemorySeconds || now < memory.NextReport) return;
            memory.NextReport = now + 1f;
            ShareObservation(ai, mook, memory);
        }

        private static void ShareObservation(PolymorphicAI senderAI, Mook sender, Memory report)
        {
            int accepted = 0;
            Reports++;
            for (int i = 0; i < Map.units.Count && accepted < 8; i++)
            {
                Mook receiver = Map.units[i] as Mook;
                if (receiver == null || receiver == sender || receiver.enemyAI == null) continue;
                PolymorphicAI ai = receiver.enemyAI;
                if (!IntelReady(ai, receiver) || !Allied(sender, receiver) ||
                    !GameModeController.DoesPlayerNumDamage(receiver.playerNum, report.Player) ||
                    Mathf.Abs(receiver.X - sender.X) > 128f || Mathf.Abs(receiver.Y - sender.Y) > 64f) continue;
                Vector3 origin = new Vector3(sender.X, sender.Y + 8f, 0f);
                Vector3 delta = new Vector3(receiver.X, receiver.Y + 8f, 0f) - origin;
                if (Physics.Raycast(origin, delta.normalized, delta.magnitude, Map.groundLayer | Map.fragileLayer)) continue;
                int ownTarget = ai.GetSeenPlayerNum();
                if (HeroController.CanSeePlayer(receiver.X, receiver.Y, 0, ai.sightRangeX, ai.sightRangeY, ref ownTarget)) continue;
                if (PreferNativeNPC(ai, receiver, false, Vector2.zero)) continue;
                Memory memory = GetMemory(ai, receiver);
                if (memory.IntelFaction != receiver.playerNum || memory.SeenAt >= report.SeenAt) continue;
                memory.Player = report.Player; memory.LastSeen = report.LastSeen; memory.SeenAt = report.SeenAt;
                memory.DirectObservation = false; memory.VisibleVelocity = Vector2.zero; memory.SharedPending = true;
                // SetMentalState preserves the current queue; FullyAlert would cancel committed actions.
                ai.SetMentalState(MentalState.Alerted);
                accepted++; IntelReceived++;
            }
        }

        private static bool OccupiedByAlly(PolymorphicAI ai, Mook mook, GridPoint goal)
        {
            if (!Cooperative(ai, mook)) return false;
            foreach (PolymorphicAI otherAI in memories.Keys)
            {
                if (otherAI == null || otherAI == ai) continue;
                Mook other = otherAI.GetComponent<Mook>();
                if (other == null || !Supported(otherAI, other) || !Cooperative(otherAI, other) || !Allied(mook, other)) continue;
                Memory otherMemory = memories[otherAI];
                GridPoint otherGoal = otherMemory.PathGoal;
                int column = otherGoal != null ? otherGoal.collumn : otherMemory.Plan == Plan.Pursue || otherMemory.Plan == Plan.Space ? otherMemory.Destination : other.collumn;
                int row = otherGoal != null ? otherGoal.row : other.row;
                if (System.Math.Abs(goal.collumn - column) <= 1 && System.Math.Abs(goal.row - row) <= 1) return true;
            }
            return false;
        }

        private static Vector2 CooperativePoint(PolymorphicAI ai, Mook mook, Memory memory)
        {
            if (!Cooperative(ai, mook)) return memory.LastSeen;
            int direction = memory.LastSeen.x < mook.X ? -1 : 1;
            EnemyKind kind = Kind(ai, mook);
            float offset = kind == EnemyKind.Bazooka ? 96f : CommandKind(kind) ? 80f : 32f;
            GridPoint goal = new GridPoint(Map.GetCollumn(memory.LastSeen.x - direction * offset), Map.GetRow(memory.LastSeen.y));
            for (int i = 0; i < 3; i++)
            {
                GridPoint candidate = new GridPoint(goal.collumn - direction * i * 2, goal.row);
                if (System.Math.Abs(candidate.collumn - memory.AnchorColumn) <= PursuitTiles && GroundPoint(mook, candidate) && !OccupiedByAlly(ai, mook, candidate))
                    return new Vector2(candidate.collumn * 16f, memory.LastSeen.y);
            }
            // No free local position: hold, rather than collapse the back row onto the target.
            return new Vector2(mook.X, mook.Y);
        }

        private static bool CommandTick(PolymorphicAI ai, Mook mook, ref int seenPlayer, bool force)
        {
            if (GroundNativePhase(ai, mook)) return false;
            if (GroundCommitted(ai)) return true;
            if (!IntelInterruptible(ai)) return false;
            if (!mook.IsOnGround() || !ai.IsAlerted() || seenPlayer == -1) return false;
            Memory memory = GetMemory(ai, mook);
            if (ai.actionQueue.Count > 0 && memory.Plan == Plan.None && !memory.SharedPending) return false;
            if (!force && Time.time < memory.NextSense) return false;
            memory.NextSense = Time.time + SenseInterval; memory.SharedPending = false;
            int target = seenPlayer;
            bool visible = HeroController.CanSeePlayer(mook.X, mook.Y, 0, ai.sightRangeX, ai.sightRangeY, ref target) &&
                target >= 0 && target < 4 && HeroController.PlayerIsAlive(target) && GameModeController.DoesPlayerNumDamage(mook.playerNum, target);
            if (PreferNativeNPC(ai, mook, visible, visible ? (Vector2)HeroController.GetPlayerPosition(target) : Vector2.zero)) { memories.Remove(ai); return false; }
            if (visible) { seenPlayer = target; memory.Player = target; memory.LastSeen = HeroController.GetPlayerPosition(target); memory.SeenAt = Time.time; memory.DirectObservation = true; }
            else if (!Plugin.Settings.RememberTargets || memory.Player < 0 || !HeroController.PlayerIsAlive(memory.Player) || Time.time - memory.SeenAt > MemorySeconds)
            { Begin(ai, memory, Plan.None, 0, 0); memory.Player = -1; seenPlayer = -10; ai.SetMentalState(MentalState.Suspicious); ai.AddAction(EnemyActionType.Wait, 0.3f); return true; }
            if (!visible) { if (Plugin.Settings.PursueTargets && GroundPursue(ai, mook, memory)) return true; Search(ai, mook, memory, force); return true; }
            float distance = Mathf.Abs(memory.LastSeen.x - mook.X);
            if (Plugin.Settings.PursueTargets && distance < 64f && TrySpace(ai, mook, memory, 88f)) return true;
            if (Plugin.Settings.PursueTargets && distance > 160f && GroundPursue(ai, mook, memory)) return true;
            if (Kind(ai, mook) == EnemyKind.Alarmist) { Guard(ai, mook, memory, force); return true; }
            Begin(ai, memory, Plan.Attack, 0, memory.LastSeen.x < mook.X ? -1 : 1);
            ai.AddAction(EnemyActionType.FacePoint, new GridPoint(Map.GetCollumn(memory.LastSeen.x), mook.row), 0f);
            // SatanAI converts this native Fire to Special; MookGeneral owns animation and the 3s spawn gate.
            ai.AddAction(EnemyActionType.Fire, ai.attackTime);
            ai.AddAction(EnemyActionType.Wait, ai.delayBetweenFiring);
            groundCooldowns[ai] = ai.actionQueue[ai.actionQueue.Count - 1]; return true;
        }

        private static bool ElitePhase(BigGuyEliteAI ai, MookBigGuyElite mook)
        { return GroundNativePhase(ai, mook) || !mook.IsOnGround() || (int)eliteGetUp.GetValue(mook) > 0 || (bool)eliteCharging.GetValue(ai); }

        private static bool EliteCommitted(PolymorphicAI ai)
        {
            Memory memory;
            if (HasAction(ai, EnemyActionType.Jump))
                for (int i = ai.actionQueue.Count - 1; i >= 0; i--)
                    if (ai.actionQueue[i].type == EnemyActionType.Wait) { groundCooldowns[ai] = ai.actionQueue[i]; break; }
            return GroundCommitted(ai) || HasAction(ai, EnemyActionType.Jump) ||
                (memories.TryGetValue(ai, out memory) && memory.Plan == Plan.Attack && ai.actionQueue.Count > 0);
        }

        private static bool EliteTick(BigGuyEliteAI ai, MookBigGuyElite mook, ref int seenPlayer, bool force)
        {
            if (ElitePhase(ai, mook)) return false;
            if (EliteCommitted(ai)) return true;
            if (!ai.IsAlerted() || seenPlayer == -1) return false;
            Memory memory = GetMemory(ai, mook);
            if (ai.actionQueue.Count > 0 && memory.Plan == Plan.None) return false;
            if (!force && Time.time < memory.NextSense) return false;
            memory.NextSense = Time.time + SenseInterval;
            int target = seenPlayer;
            bool visible = HeroController.CanSeePlayer(mook.X, mook.Y, 0, ai.sightRangeX, ai.sightRangeY, ref target) &&
                target >= 0 && target < 4 && HeroController.PlayerIsAlive(target) && GameModeController.DoesPlayerNumDamage(mook.playerNum, target);
            if (PreferNativeNPC(ai, mook, visible, visible ? (Vector2)HeroController.GetPlayerPosition(target) : Vector2.zero)) { memories.Remove(ai); return false; }
            if (visible) { seenPlayer = target; memory.Player = target; memory.LastSeen = HeroController.GetPlayerPosition(target); memory.SeenAt = Time.time; }
            else if (!Plugin.Settings.RememberTargets || memory.Player < 0 || !HeroController.PlayerIsAlive(memory.Player) || Time.time - memory.SeenAt > MemorySeconds)
            { Begin(ai, memory, Plan.None, 0, 0); memory.Player = -1; seenPlayer = -10; ai.SetMentalState(MentalState.Suspicious); ai.AddAction(EnemyActionType.Wait, 0.3f); return true; }
            float dx = Mathf.Abs(memory.LastSeen.x - mook.X), dy = memory.LastSeen.y - mook.Y;
            bool rockets = (int)eliteIndex.GetValue(ai) % 4 == 0;
            bool opportunity = rockets ? dx <= 192f && Mathf.Abs(dy) <= 32f &&
                !Physics.SphereCast(new Ray(new Vector3(mook.X, mook.Y + 19f, 0f), Vector3.right * Mathf.Sign(memory.LastSeen.x - mook.X)), 3f, Mathf.Max(0f, dx - 8f), Map.groundLayer | Map.fragileLayer) :
                dx <= 160f && dy <= 64f && ArmouredStep(mook, Map.GetCollumn(memory.LastSeen.x)) &&
                !Physics.BoxCast(new Vector3(mook.X, mook.Y + mook.height * 0.5f, 0f), new Vector3(mook.HalfWidth, mook.height * 0.5f - 1f, 1f), Vector3.up, Quaternion.identity, 48f, Map.groundLayer | Map.fragileLayer);
            if (visible && opportunity)
            {
                if (memory.Plan != Plan.Attack) Begin(ai, memory, Plan.Attack, 0, memory.LastSeen.x < mook.X ? -1 : 1);
                // Only the real native think advances attackIndex and commits the charge/jump/fire cycle.
                return false;
            }
            if (Plugin.Settings.PursueTargets && GroundPursue(ai, mook, memory)) return true;
            Guard(ai, mook, memory, force); return true;
        }
    }

    [HarmonyPatch(typeof(BigGuyEliteAI), "DoAlertedThink")]
    internal static class PlanEliteOpportunity
    {
        private static bool Prefix(BigGuyEliteAI __instance, Mook ___mook, ref int ___seenEnemyNum)
        { return !Controller.Tick(__instance, ___mook, ref ___seenEnemyNum, true); }
    }

    [HarmonyPatch(typeof(MookGeneral), "SpawnMook")]
    internal static class OwnGeneralReinforcements
    {
        private static bool Prefix(MookGeneral __instance)
        { return Controller.GeneralSpawnAllowed(__instance); }
    }
}
