using System;
using System.Collections.Generic;
using UnityEngine;

namespace EnemyAIEnhancement
{
    internal enum Plan { None, Attack, Pursue, Search, Retreat, Space, Guard, Escape }

    internal sealed class Memory
    {
        internal int Player = -1;
        internal Vector2 LastSeen;
        internal float SeenAt = float.NegativeInfinity;
        internal float NextSense;
        internal int AnchorColumn;
        internal Plan Plan;
        internal int Destination;
        internal int Direction;
        internal ScoutMookInterestPoint ScoutPoint;
        internal GridPoint ScoutGoal;
        internal ActionObject FlightCooldown;
        internal int XenoFaction;
        internal bool XenoBrainBox;
        internal int DogFaction;
        internal GridPoint PathGoal;
        internal int PathTerrain;
        internal float NextPath;
        internal Vector2 VisibleVelocity;
        internal Vector2 PursuitPoint;
        internal int IntelFaction;
        internal bool DirectObservation;
        internal bool SharedPending;
        internal float NextIntelSense;
        internal float NextReport;
    }

    internal static partial class Controller
    {
        private const float SenseInterval = 0.2f;
        private const float MemorySeconds = 6f;
        private const int PursuitTiles = 16;
        private static readonly Dictionary<PolymorphicAI, Memory> memories = new Dictionary<PolymorphicAI, Memory>();
        private static readonly List<Grenade> threats = new List<Grenade>();
        private static object mapUnits;
        internal static int Plans, Searches, Retreats, ReceivedQueues;
        internal static int TrackedCount { get { return memories.Count; } }

        private static bool Supported(PolymorphicAI ai, Mook mook)
        {
            return Plugin.Enabled && mook != null && Kind(ai, mook) != EnemyKind.Unsupported && mook.playerNum < 0 &&
                ai.IsMine && mook.IsMine && mook.health > 0 && !mook.beingControlledByTriggerAction &&
                !Map.isEditing && Map.units != null && !GameModeController.IsLevelFinished() && !ArmouredNativePhase(mook);
        }

        private static Memory GetMemory(PolymorphicAI ai, Mook mook)
        {
            if (!ReferenceEquals(mapUnits, Map.units))
            {
                RestoreJetpackHeights();
                if (mapUnits != null) { faceCooldowns.Clear(); xenoCooldowns.Clear(); dogCooldowns.Clear(); undeadCooldowns.Clear(); groundCooldowns.Clear(); }
                memories.Clear();
                mapUnits = Map.units;
            }
            Memory memory;
            if (!memories.TryGetValue(ai, out memory))
            {
                memory = new Memory();
                // FullyAlert can run before the freshly spawned unit's first grid update.
                memory.AnchorColumn = Map.GetCollumn(mook.X);
                memory.IntelFaction = mook.playerNum;
                AlienXenomorph xeno = mook as AlienXenomorph;
                if (xeno != null) { memory.XenoFaction = mook.playerNum; memory.XenoBrainBox = xeno.hasBrainBox; }
                if (Kind(ai, mook) == EnemyKind.Dog) memory.DogFaction = mook.playerNum;
                memories.Add(ai, memory);
            }
            return memory;
        }

        internal static void RememberAlert(PolymorphicAI ai, Mook mook, float x, float y, int player)
        {
            if (!Supported(ai, mook)) return;
            EnemyKind kind = Kind(ai, mook);
            if (kind == EnemyKind.Scout || kind == EnemyKind.Xenomorph ||
                kind == EnemyKind.Dog || IsUndead(kind) || AggressiveGround(ai, mook) ||
                kind == EnemyKind.LostSoul || NinjaNativePhase(ai, mook) ||
                IsInterceptKind(kind) || kind == EnemyKind.Mosquito ||
                CommandKind(kind) || kind == EnemyKind.Elite ||
                !ai.IsAlerted() || player < 0 || player >= 4) return;
            Memory memory = GetMemory(ai, mook);
            memory.Player = player;
            memory.LastSeen = new Vector2(x, y);
            memory.SeenAt = Time.time;
        }

        internal static bool Tick(PolymorphicAI ai, Mook mook, ref int seenPlayer, bool force)
        {
            if (NinjaNativePhase(ai, mook)) return false;
            EnemyKind kind = mook != null ? Kind(ai, mook) : EnemyKind.Unsupported;
            if (kind == EnemyKind.Scout)
            {
                RefreshScout((ScoutAI)ai, mook);
                return false;
            }
            if (!Supported(ai, mook))
            {
                RestoreJetpackHeight(mook as MookJetpack);
                memories.Remove(ai);
                return false;
            }
            bool flight = IsJetpack(kind);
            SharedPlayer(ai, mook, ref seenPlayer);
            if (kind == EnemyKind.Elite) return EliteTick((BigGuyEliteAI)ai, (MookBigGuyElite)mook, ref seenPlayer, force);
            if (CommandKind(kind)) return CommandTick(ai, mook, ref seenPlayer, force);
            if (AggressiveGround(ai, mook)) return GroundTick(ai, mook, ref seenPlayer, force);
            if (IsNewMelee(kind)) return NewMeleeTick(ai, mook, ref seenPlayer, force);
            if (kind == EnemyKind.Mosquito) return false;
            if (kind == EnemyKind.LostSoul) return false; // Native flying input has its own opportunity hook.
            if (IsUndead(kind)) return UndeadTick(ai, mook, ref seenPlayer, force);
            if (kind == EnemyKind.Dog) return DogTick((DogAI)ai, (MookDog)mook, ref seenPlayer, force);
            if (kind == EnemyKind.Xenomorph) return XenomorphTick((XenomorphAI)ai, (AlienXenomorph)mook, ref seenPlayer, force);
            bool facehugger = kind == EnemyKind.Facehugger;
            if (facehugger && FacehuggerNativePhase((FacehuggerAI)ai, (AlienFaceHugger)mook)) return false;
            if (flight && JetpackNativePhase((MookJetpack)mook))
            {
                RestoreJetpackHeight((MookJetpack)mook);
                return false;
            }
            if (mook is MookRiotShield && !((MookRiotShield)mook).hasShield)
            {
                memories.Remove(ai);
                return false;
            }
            if (mook.IsIncapacitated() || mook.blindTime > 0f || (!flight && !mook.IsOnGround()) ||
                (ai.mentalState != MentalState.Alerted && ai.mentalState != MentalState.Idle &&
                 ai.mentalState != MentalState.Suspicious)) return false;
            if (ai.mentalState == MentalState.Idle &&
                !SortOfFollow.IsItSortOfVisible(mook.X, mook.Y, 128f, 128f)) return false;

            Memory memory = GetMemory(ai, mook);
            if (facehugger && (FacehuggerCommitted(ai, mook) ||
                (ai.actionQueue.Count > 0 && ai.actionQueue[0].type == EnemyActionType.Wait))) return true;
            // Native SuicideAI owns every frame after ignition, including its next charge actions.
            if (SuicideCommitted(ai)) return false;
            if (kind == EnemyKind.Suicide && (!Plugin.Settings.PursueTargets ||
                HasAction(ai, EnemyActionType.PlaySpecialSound))) return false;
            // Finish the whole burst and its cooldown before reacquiring or avoiding a grenade.
            if (kind == EnemyKind.Bruiser)
            {
                if (HasAction(ai, EnemyActionType.Fire)) memory.Plan = Plan.Attack;
                // Also covers a cooldown kept across a toggle, when local memory was cleared.
                if (ai.actionQueue.Count > 0) return true;
            }
            // A committed throw must reach its release frame without changing aim or restarting Fire.
            if (mook is MookGrenadier && (bool)throwing.GetValue(mook)) return true;
            float now = Time.time;
            if (!force && now < memory.NextSense) return false;
            memory.NextSense = now + SenseInterval;

            if (!flight && kind != EnemyKind.Suicide && Plugin.Settings.AvoidGrenades && TryRetreat(ai, mook, memory)) return true;
            if (memory.Plan == Plan.Retreat)
            {
                ai.ClearActionQueue(true);
                memory.Plan = Plan.None;
            }
            if (!ai.IsAlerted())
            {
                if (flight) RestoreJetpackHeight((MookJetpack)mook);
                return false;
            }

            int target = seenPlayer;
            bool visible = HeroController.CanSeePlayer(mook.X, mook.Y, 0,
                (float)ai.sightRangeX, (float)ai.sightRangeY, ref target);
            if (visible && target >= 0 && target < 4 && HeroController.PlayerIsAlive(target))
            {
                seenPlayer = target;
                memory.Player = target;
                memory.LastSeen = HeroController.GetPlayerPosition(target);
                memory.SeenAt = now;
                if (flight) return JetpackCombat(ai, (MookJetpack)mook, memory, force);
                if (facehugger) return FacehuggerCombat((FacehuggerAI)ai, (AlienFaceHugger)mook, memory);
                if (kind == EnemyKind.Suicide) return SuicideCombat(ai, mook, memory, force);
                if (kind == EnemyKind.Bruiser) return BruiserCombat(ai, mook, memory, force);
                if (kind != EnemyKind.Trooper)
                    return SpecialistCombat(ai, mook, memory, force);
                if (Plugin.Settings.PursueTargets && Mathf.Abs(memory.LastSeen.x - mook.X) > 128f &&
                    TryPursue(ai, mook, memory)) return true;
                Attack(ai, mook, memory, force);
                return true;
            }

            if (Plugin.Settings.RememberTargets && memory.Player >= 0 &&
                HeroController.PlayerIsAlive(memory.Player) && now - memory.SeenAt <= MemorySeconds)
            {
                if (flight) return JetpackSearch(ai, (MookJetpack)mook, memory, force);
                if (Plugin.Settings.PursueTargets && TryPursue(ai, mook, memory)) return true;
                Search(ai, mook, memory, force);
                return true;
            }

            // Expire knowledge explicitly, including the queued fire/movement, and replicate the transition.
            if (flight) RestoreJetpackHeight((MookJetpack)mook);
            memory.Player = -1;
            memory.Plan = Plan.None;
            seenPlayer = -10;
            ai.SetMentalState(MentalState.Suspicious);
            ai.ClearActionQueue(true);
            ai.AddAction(EnemyActionType.Wait, 0.3f);
            return true;
        }

        private static void Attack(PolymorphicAI ai, Mook mook, Memory memory, bool force)
        {
            int direction = memory.LastSeen.x < mook.X ? -1 : 1;
            if (!force && memory.Plan == Plan.Attack && memory.Direction == direction && ai.actionQueue.Count > 0)
                return;
            bool turning = ai.FacingDirection != direction;
            Begin(ai, memory, Plan.Attack, 0, direction);
            if (turning)
            {
                ai.AddAction(EnemyActionType.FacePoint, new GridPoint(mook.collumn + direction, mook.row), 0f);
                ai.AddAction(EnemyActionType.Wait, Mathf.Max(0.2f, ai.sightDelay));
            }
            ai.AddAction(EnemyActionType.Fire, ai.attackTime);
            ai.AddAction(EnemyActionType.Wait, Mathf.Max(0.2f, ai.delayBetweenFiring));
        }

        private static bool TryPursue(PolymorphicAI ai, Mook mook, Memory memory)
        { return PursuePoint(ai, mook, memory, memory.LastSeen); }

        private static bool PursuePoint(PolymorphicAI ai, Mook mook, Memory memory, Vector2 point)
        {
            if (Mathf.Abs(point.y - mook.Y) > 20f || Mathf.Abs(point.x - mook.X) < 18f)
                return false;
            int targetColumn = Mathf.Clamp(Map.GetCollumn(point.x),
                memory.AnchorColumn - PursuitTiles, memory.AnchorColumn + PursuitTiles);
            int direction = System.Math.Sign(targetColumn - mook.collumn);
            if (direction == 0) return false;
            int destination = mook.collumn;
            int steps = System.Math.Min(2, System.Math.Abs(targetColumn - mook.collumn));
            for (int step = 1; step <= steps; step++)
            {
                int column = mook.collumn + direction * step;
                if (!SafeStep(mook, column)) break;
                destination = column;
            }
            if (destination == mook.collumn) return false;
            if (memory.Plan == Plan.Pursue && ai.actionQueue.Count > 0)
            {
                ActionObject current = ai.actionQueue[0];
                if (current.type == EnemyActionType.Move && current.gridPoint != null &&
                    current.gridPoint.row == mook.row && System.Math.Sign(current.gridPoint.collumn - mook.collumn) == direction &&
                    SafeRoute(mook, current.gridPoint.collumn)) return true;
            }
            Begin(ai, memory, Plan.Pursue, destination, direction);
            ai.AddAction(EnemyActionType.Move, new GridPoint(destination, mook.row));
            ai.AddAction(EnemyActionType.Wait, 0.15f);
            Searches++;
            return true;
        }

        private static void Search(PolymorphicAI ai, Mook mook, Memory memory, bool force)
        {
            if (!force && memory.Plan == Plan.Search && ai.actionQueue.Count > 0) return;
            int direction = -ai.FacingDirection;
            Begin(ai, memory, Plan.Search, 0, direction);
            ai.AddAction(EnemyActionType.FacePoint, new GridPoint(mook.collumn + direction, mook.row), 0f);
            ai.AddAction(EnemyActionType.Wait, 0.45f);
            Searches++;
        }

        private static bool SafeStep(Mook mook, int column)
        {
            EnemyKind kind = Kind(mook.enemyAI, mook);
            if (kind == EnemyKind.Skinless) return UndeadRoute(mook, column * 16f);
            if (kind == EnemyKind.Brute || kind == EnemyKind.Melter) return DogRoute((MookDog)mook, column * 16f);
            if (kind == EnemyKind.Elite) return ArmouredStep(mook, column);
            if (CommandKind(kind)) return UndeadRoute(mook, column * 16f);
            if (AggressiveGround(mook.enemyAI, mook)) return UndeadRoute(mook, column * 16f);
            if (mook.GetType() == typeof(UndeadTrooper) || mook.GetType() == typeof(MookSuicideUndead)) return UndeadRoute(mook, column * 16f);
            if (mook.GetType() == typeof(MookDog) || mook.GetType() == typeof(HellDog)) return DogRoute((MookDog)mook, column * 16f);
            if (mook.GetType() == typeof(AlienXenomorph)) return XenomorphRoute((AlienXenomorph)mook, column * 16f);
            if (mook.GetType() == typeof(AlienFaceHugger)) return FacehuggerRoute(mook, column * 16f);
            if (mook is MookArmouredGuy) return ArmouredStep(mook, column);
            if (!Map.IsBlockSolid(column, mook.row - 1) || Map.IsBlockSolid(column, mook.row) ||
                Map.IsBlockSolid(column, mook.row + 1)) return false;
            Vector3 start = new Vector3(mook.X, mook.Y + 7f, 0f);
            Vector3 delta = new Vector3((float)(column * 16) - mook.X, 0f, 0f);
            return !Physics.Raycast(start, delta, delta.magnitude, Map.groundLayer | Map.fragileLayer);
        }

        private static bool SafeRoute(Mook mook, int destination)
        {
            int distance = System.Math.Abs(destination - mook.collumn);
            if (distance > PursuitTiles) return false;
            int direction = System.Math.Sign(destination - mook.collumn);
            for (int i = 1; i <= distance; i++)
                if (!SafeStep(mook, mook.collumn + direction * i)) return false;
            return true;
        }

        private static bool IsExplosive(Grenade grenade)
        {
            // Smoke, holograms and character-specific devices have different effects and fuse rules.
            Type type = grenade.GetType();
            return grenade.damage > 0 && (type == typeof(Grenade) || type == typeof(GrenadeSmall) ||
                type == typeof(GrenadeCluster));
        }

        private static float DangerRadius(Grenade grenade, Mook mook)
        {
            return Mathf.Max(28f, grenade.range * 1.5f + mook.HalfWidth + 8f);
        }

        private static float Clearance(float x, float y, Mook mook)
        {
            float result = float.PositiveInfinity;
            for (int i = 0; i < threats.Count; i++)
            {
                Grenade grenade = threats[i];
                float dx = grenade.X - x, dy = grenade.Y - y;
                result = Mathf.Min(result, Mathf.Sqrt(dx * dx + dy * dy) - DangerRadius(grenade, mook));
            }
            return result;
        }

        private static bool TryRetreat(PolymorphicAI ai, Mook mook, Memory memory)
        {
            threats.Clear();
            if (Map.grenades == null) return false;
            for (int i = 0; i < Map.grenades.Count; i++)
            {
                Grenade grenade = Map.grenades[i];
                if (grenade == null || !grenade.gameObject.activeInHierarchy || grenade.life <= 0f ||
                    grenade.life > 1.5f || !IsExplosive(grenade)) continue;
                float radius = DangerRadius(grenade, mook);
                if (Mathf.Abs(grenade.X - mook.X) > radius + 96f || Mathf.Abs(grenade.Y - mook.Y) > radius)
                    continue;
                Vector3 start = new Vector3(mook.X, mook.Y + 8f, 0f);
                Vector3 delta = new Vector3(grenade.X, grenade.Y + 3f, 0f) - start;
                if (!Physics.Raycast(start, delta, Mathf.Max(0f, delta.magnitude - 4f), Map.groundLayer | Map.fragileLayer))
                    threats.Add(grenade);
            }
            if (threats.Count == 0) return false;
            float current = Clearance(mook.X, mook.Y, mook);
            if (current >= 0f) return false;
            int destination = mook.collumn;
            float bestScore = current;
            for (int direction = -1; direction <= 1; direction += 2)
            {
                for (int step = 1; step <= 6; step++)
                {
                    int column = mook.collumn + direction * step;
                    if (!SafeStep(mook, column)) break;
                    float score = Mathf.Min(8f, Clearance((float)(column * 16), mook.Y, mook)) - step * 0.25f;
                    if (score > bestScore + 1f) { bestScore = score; destination = column; }
                }
            }
            if (destination == mook.collumn) return false;
            if (memory.Plan == Plan.Retreat && ai.actionQueue.Count > 0 &&
                memory.Destination == destination && SafeRoute(mook, destination)) return true;
            Begin(ai, memory, Plan.Retreat, destination, System.Math.Sign(destination - mook.collumn));
            ai.AddAction(EnemyActionType.Move, new GridPoint(destination, mook.row));
            ai.AddAction(EnemyActionType.Wait, 0.1f);
            Retreats++;
            return true;
        }

        private static void Begin(PolymorphicAI ai, Memory memory, Plan plan, int destination, int direction)
        {
            if (memory.PathGoal != null) { ai.GetComponent<PathAgent>().CurrentPath = null; memory.PathGoal = null; }
            if (ai.GetType() == typeof(FacehuggerAI)) ai.GetComponent<PathAgent>().CurrentPath = null;
            ai.ClearActionQueue(true);
            memory.Plan = plan;
            memory.Destination = destination;
            memory.Direction = direction;
            Plans++;
        }

        internal static void Forget(PolymorphicAI ai)
        {
            RestoreJetpackHeight(ai.GetComponent<MookJetpack>());
            memories.Remove(ai);
            faceCooldowns.Remove(ai);
            xenoCooldowns.Remove(ai);
            dogCooldowns.Remove(ai);
            undeadCooldowns.Remove(ai);
            groundCooldowns.Remove(ai);
            MookNinjaAI ninja = ai as MookNinjaAI;
            if (ninja != null) { ninjaWarnings.Remove(ninja); ninjaRevealsSent.Remove(ninja); }
        }

        internal static void Reset(bool cancelOwnedPlans)
        {
            WormController.Reset();
            VehicleController.Reset();
            TransportController.Reset();
            BossController.Reset();
            if (cancelOwnedPlans)
                foreach (KeyValuePair<PolymorphicAI, Memory> pair in memories)
                {
                    if (pair.Key == null || !pair.Key.IsMine || pair.Value.Plan == Plan.None) continue;
                    Mook mook = pair.Key.GetComponent<Mook>();
                    if (mook == null || mook.playerNum >= 0 || mook.beingControlledByTriggerAction || !mook.IsMine) continue;
                    EnemyKind kind = Kind(pair.Key, mook);
                    if (!(kind == EnemyKind.Xenomorph && XenomorphCommitted(pair.Key)) &&
                        !(IsNewMelee(kind) && NewMeleePreserves(pair.Key, mook)) &&
                        !(AggressiveGround(pair.Key, mook) && (GroundCommitted(pair.Key) || GroundNativePhase(pair.Key, mook))) &&
                        !(CommandKind(kind) && (GroundCommitted(pair.Key) || GroundNativePhase(pair.Key, mook))) &&
                        !(kind == EnemyKind.Elite && (ElitePhase((BigGuyEliteAI)pair.Key, (MookBigGuyElite)mook) || EliteCommitted(pair.Key))) &&
                        !(IsUndead(kind) && (UndeadCommitted(pair.Key) || UndeadNativePhase(pair.Key, mook))) &&
                        !(kind == EnemyKind.Dog && (DogCommitted(pair.Key) || DogSpecialPhase((DogAI)pair.Key, (MookDog)mook))) &&
                        kind != EnemyKind.Scout && !NinjaNativePhase(pair.Key, mook) &&
                        !(kind == EnemyKind.Facehugger && FacehuggerCommitted(pair.Key, mook)) &&
                        !(IsJetpack(kind) && pair.Value.FlightCooldown != null && pair.Key.actionQueue.Contains(pair.Value.FlightCooldown)) &&
                        !ArmouredNativePhase(mook) && !SuicideCommitted(pair.Key) && !(kind == EnemyKind.Bruiser &&
                        pair.Value.Plan == Plan.Attack && pair.Key.actionQueue.Count > 0))
                    {
                        if (pair.Value.PathGoal != null) pair.Key.GetComponent<PathAgent>().CurrentPath = null;
                        pair.Key.ClearActionQueue(true);
                    }
                }
            memories.Clear();
            RestoreJetpackHeights();
            threats.Clear();
            ninjaRevealsSent.Clear();
            mapUnits = null;
        }
    }
}
