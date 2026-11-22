using HarmonyLib;
using System.Reflection;
using UnityEngine;

namespace EnemyAIEnhancement
{
    internal static partial class Controller
    {
        private static readonly FieldInfo bruteSkid = AccessTools.Field(typeof(AlienBrute), "skiddingToHalt");
        private static readonly FieldInfo bruteRecoil = AccessTools.Field(typeof(AlienBrute), "smashedRecoilFrames");
        private static readonly FieldInfo mosquitoForced = AccessTools.Field(typeof(AlienMosquito), "forcedFlyingInDirectionTime");

        private static bool IsNewMelee(EnemyKind kind)
        { return kind == EnemyKind.Skinless || kind == EnemyKind.Brute || kind == EnemyKind.Melter; }

        private static bool IsInterceptKind(EnemyKind kind)
        { return IsNewMelee(kind) || kind == EnemyKind.Dog || kind == EnemyKind.Xenomorph; }

        private static bool UsesGroundPath(PolymorphicAI ai, Mook mook)
        { return AggressiveGround(ai, mook) || IsInterceptKind(Kind(ai, mook)) || CommandKind(Kind(ai, mook)) || Kind(ai, mook) == EnemyKind.Elite; }

        private static bool SkinlessPhase(PolymorphicAI ai, SkinnedMook mook)
        {
            return GroundNativePhase(ai, mook) || Map.MapData == null || Map.MapData.theme != LevelTheme.Hell ||
                mook.hanging || mook.suspendFromRope || mook.strungUpBy != null || mook.onBackOf != null;
        }

        private static bool MelterPhase(AlienMelterAI ai, AlienMelter mook)
        {
            if (GroundNativePhase(ai, mook) || ai.hasLitFuse || (bool)faceRoof.GetValue(ai) ||
                (float)faceRoofTime.GetValue(ai) > 0f || (bool)faceDrop.GetValue(ai) ||
                mook.wallAttachDirection != DirectionEnum.None ||
                (DirectionEnum)faceTransition.GetValue(mook) != DirectionEnum.None) return true;
            for (int i = 0; i < faceClimbing.Length; i++)
                if (i == 0 ? (float)faceClimbing[i].GetValue(ai) >= 0f : (float)faceClimbing[i].GetValue(ai) > 0f) return true;
            return false;
        }

        private static bool BrutePhase(AlienBruteAI ai, AlienBrute mook)
        {
            return GroundNativePhase(ai, mook) || DogSpecialPhase(ai, mook) || mook.fire || mook.wasFire ||
                (bool)bruteSkid.GetValue(mook) || (int)bruteRecoil.GetValue(mook) > 0;
        }

        private static bool GroundPathNativePhase(PolymorphicAI ai, Mook mook)
        {
            EnemyKind kind = Kind(ai, mook);
            if (kind == EnemyKind.Elite) return ElitePhase((BigGuyEliteAI)ai, (MookBigGuyElite)mook);
            if (kind == EnemyKind.Skinless) return SkinlessPhase(ai, (SkinnedMook)mook);
            if (kind == EnemyKind.Melter) return MelterPhase((AlienMelterAI)ai, (AlienMelter)mook);
            if (kind == EnemyKind.Brute) return BrutePhase((AlienBruteAI)ai, (AlienBrute)mook);
            if (kind == EnemyKind.Dog) return GroundNativePhase(ai, mook) || DogSpecialPhase((DogAI)ai, (MookDog)mook);
            if (kind == EnemyKind.Xenomorph) return GroundNativePhase(ai, mook) || XenomorphNativePhase((XenomorphAI)ai, (AlienXenomorph)mook);
            return GroundNativePhase(ai, mook);
        }

        private static bool NewMeleePreserves(PolymorphicAI ai, Mook mook)
        { return GroundPathNativePhase(ai, mook) || (Kind(ai, mook) == EnemyKind.Brute && BruteCommitted(ai)); }

        private static bool BruteCommitted(PolymorphicAI ai)
        {
            ActionObject recovery;
            // Brute only consumes holdFireTime while Fire is active. Its leftover value after
            // the real skid/recovery is not a pending dog bite and must not stall the next plan.
            return HasAction(ai, EnemyActionType.Fire) ||
                (dogCooldowns.TryGetValue(ai, out recovery) && ai.actionQueue.Contains(recovery));
        }

        private static void SampleVisible(Memory memory, int player, Vector2 point, float now)
        {
            float elapsed = now - memory.SeenAt;
            if (memory.Player == player && elapsed < 0.05f) return;
            memory.VisibleVelocity = memory.Player == player && elapsed >= 0.05f && elapsed <= 0.6f
                ? Vector2.ClampMagnitude((point - memory.LastSeen) / elapsed, 120f) : Vector2.zero;
            memory.Player = player; memory.LastSeen = point; memory.SeenAt = now;
        }

        private static bool InterceptPursue(PolymorphicAI ai, Mook mook, Memory memory, bool visible)
        {
            Vector2 point = memory.LastSeen;
            if (visible)
            {
                float speed = mook.speed;
                MookDog dog = mook as MookDog;
                if (dog != null) speed = Mathf.Max(speed, dog.useNewFrames ? dog.awareMegaRunSpeed : dog.awareRunSpeed);
                float ahead = Mathf.Clamp(Vector2.Distance(point, new Vector2(mook.X, mook.Y)) / Mathf.Max(1f, speed), 0.05f, 0.2f);
                Vector2 intercept = point + Vector2.ClampMagnitude(memory.VisibleVelocity * ahead, 24f);
                GridPoint candidate = new GridPoint(Map.GetCollumn(intercept.x), Map.GetRow(intercept.y));
                // A projected point needs a real standing body and support, not just an empty target cell.
                if (System.Math.Abs(candidate.collumn - memory.AnchorColumn) <= PursuitTiles && GroundPoint(mook, candidate) &&
                    (candidate.row != mook.row || SafeRoute(mook, candidate.collumn))) point = intercept;
            }
            memory.PursuitPoint = point;
            GridPoint goal = new GridPoint(Mathf.Clamp(Map.GetCollumn(point.x), Mathf.Max(1, memory.AnchorColumn - PursuitTiles),
                Mathf.Min(Map.Width - 2, memory.AnchorColumn + PursuitTiles)), Map.GetRow(point.y));
            if (KeepGroundPath(ai, mook, memory, goal)) return true;
            if (PursuePoint(ai, mook, memory, point)) return true;
            bool canRepath = Time.time >= memory.NextPath;
            if (TryGroundPath(ai, mook, memory, goal)) return true;
            // An unreachable prediction must not prevent chasing the actual last observed position.
            if (point != memory.LastSeen)
            {
                memory.PursuitPoint = memory.LastSeen;
                if (TryPursue(ai, mook, memory)) return true;
                GridPoint known = new GridPoint(Mathf.Clamp(Map.GetCollumn(memory.LastSeen.x), Mathf.Max(1, memory.AnchorColumn - PursuitTiles),
                    Mathf.Min(Map.Width - 2, memory.AnchorColumn + PursuitTiles)), Map.GetRow(memory.LastSeen.y));
                if (canRepath) { memory.NextPath = 0f; return TryGroundPath(ai, mook, memory, known); }
            }
            return false;
        }

        private static bool NewMeleeTick(PolymorphicAI ai, Mook mook, ref int seenPlayer, bool force)
        {
            EnemyKind kind = Kind(ai, mook);
            if (GroundPathNativePhase(ai, mook)) return false;
            if (kind == EnemyKind.Brute && BruteCommitted(ai)) return true;
            if (ai.mentalState != MentalState.Alerted || seenPlayer == -1 || !mook.IsOnGround() ||
                (mook.actionState != ActionState.Idle && mook.actionState != ActionState.Running)) return false;
            Memory memory; memories.TryGetValue(ai, out memory);
            if ((memory == null || memory.Plan == Plan.None) && ai.actionQueue.Count > 0) return false;
            float now = Time.time;
            if (!force && memory != null && now < memory.NextSense) return false;
            int target = seenPlayer;
            bool visible = HeroController.CanSeePlayer(mook.X, mook.Y, 0, ai.sightRangeX, ai.sightRangeY, ref target) &&
                target >= 0 && target < 4 && HeroController.PlayerIsAlive(target) && GameModeController.DoesPlayerNumDamage(mook.playerNum, target);
            Vector2 point = visible ? (Vector2)HeroController.GetPlayerPosition(target) : Vector2.zero;
            int npc = int.MinValue; float npcX = 0f, npcY = 0f;
            if (Map.CanSeeNPCEnemyUnit(mook.X, mook.Y, ai.FacingDirection, ai.sightRangeX, ai.sightRangeY, mook.playerNum, ref npc, ref npcX, ref npcY) &&
                (!visible || Vector2.Distance(new Vector2(mook.X, mook.Y), new Vector2(npcX, npcY)) < Vector2.Distance(new Vector2(mook.X, mook.Y), point)))
            { memories.Remove(ai); return false; }
            if (memory == null && !visible) return false;
            memory = GetMemory(ai, mook); memory.NextSense = now + SenseInterval;
            if (visible) { seenPlayer = target; SampleVisible(memory, target, point, now); }
            else if (!Plugin.Settings.RememberTargets || memory.Player < 0 || !HeroController.PlayerIsAlive(memory.Player) || now - memory.SeenAt > MemorySeconds)
            {
                Begin(ai, memory, Plan.None, 0, 0); memory.Player = -1; seenPlayer = -10;
                ai.SetMentalState(MentalState.Suspicious); ai.AddAction(EnemyActionType.Wait, 0.3f); return true;
            }
            if (visible && kind == EnemyKind.Brute && Mathf.Abs(point.y - mook.Y) <= 24f &&
                Mathf.Abs(point.x - mook.X) <= Mathf.Min(144f, ((AlienBruteAI)ai).attackRangeX))
            {
                int direction = point.x < mook.X ? -1 : 1;
                float distance = Mathf.Abs(point.x - mook.X);
                if (DogRoute((AlienBrute)mook, point.x + direction * 24f))
                {
                    Begin(ai, memory, Plan.Attack, 0, direction);
                    ai.AddAction(EnemyActionType.FacePoint, new GridPoint(mook.collumn + direction, mook.row), 0f);
                    ai.AddAction(EnemyActionType.Fire, Mathf.Clamp((distance + 60f) / 160f, 0.3f, 1.5f));
                    DogAI rush = (DogAI)ai;
                    ai.AddAction(EnemyActionType.Wait, Mathf.Max(0.75f, Mathf.Max(ai.delayBetweenFiring, rush.attackHoldFireDelay + rush.attackHoldFireDuration)));
                    dogCooldowns[ai] = ai.actionQueue[ai.actionQueue.Count - 1]; return true;
                }
            }
            if (Plugin.Settings.PursueTargets && InterceptPursue(ai, mook, memory, visible)) return true;
            // Skinless needs actual contact (8px), and Melter needs native Move to light its fuse.
            if (visible && kind != EnemyKind.Brute && Mathf.Abs(point.y - mook.Y) <= 16f && Mathf.Abs(point.x - mook.X) < 18f)
            {
                int direction = point.x < mook.X ? -1 : 1, destination = mook.collumn + direction;
                if (System.Math.Abs(destination - memory.AnchorColumn) <= PursuitTiles && SafeStep(mook, destination))
                {
                    Begin(ai, memory, Plan.Pursue, destination, direction);
                    ai.AddAction(EnemyActionType.FacePoint, new GridPoint(destination, mook.row), 0f);
                    ai.AddAction(EnemyActionType.Move, new GridPoint(destination, mook.row)); return true;
                }
            }
            Search(ai, mook, memory, force); return true;
        }

        internal static bool MosquitoInput(AlienMosquitoAI ai, AlienMosquito mook, ref int seenPlayer,
            ref float counter, float takeOffDelay, ref bool left, ref bool right, ref bool up, ref bool down)
        {
            if (!Supported(ai, mook) || !ai.enabled || (bool)faceFirst.GetValue(ai) || ai.mentalState != MentalState.Alerted ||
                seenPlayer == -1 || mook.IsDiving() || mook.PlayingSummoningAnimation() ||
                (mook.spawnGoop != null && mook.spawnGoop.gameObject.activeSelf) || mook.IsIncapacitated() ||
                mook.blindTime > 0f || mook.burnTime > 0f || mook.tumbling || mook.frontSomersaulting || mook.backSomersaulting ||
                (bool)faceUsing.GetValue(mook) || (float)mosquitoForced.GetValue(mook) > 0f) return false;
            Memory memory = GetMemory(ai, mook); float now = Time.time; int target = seenPlayer;
            bool visible = HeroController.CanSeePlayer(mook.X, mook.Y, 0, ai.sightRangeX, ai.flyingSightYRange, ref target) &&
                target >= 0 && target < 4 && HeroController.PlayerIsAlive(target) && GameModeController.DoesPlayerNumDamage(mook.playerNum, target);
            if (visible)
            {
                seenPlayer = target;
                if (now >= memory.NextSense)
                { SampleVisible(memory, target, HeroController.GetPlayerPosition(target), now); memory.NextSense = now + SenseInterval; }
            }
            else if (!Plugin.Settings.RememberTargets || memory.Player < 0 || !HeroController.PlayerIsAlive(memory.Player) || now - memory.SeenAt > MemorySeconds)
            {
                memory.Player = -1; memory.Plan = Plan.None; seenPlayer = -10;
                ai.SetMentalState(MentalState.Suspicious); ai.ClearActionQueue(true); ai.AddAction(EnemyActionType.Wait, 0.3f); return true;
            }
            counter += Mathf.Clamp(Time.deltaTime, 0f, 0.033334f);
            Vector2 aim = memory.LastSeen + (visible ? Vector2.ClampMagnitude(memory.VisibleVelocity * 0.2f, 24f) : Vector2.zero);
            float drop = mook.Y - aim.y, dx = aim.x - mook.X;
            int direction = dx < 0f ? -1 : 1;
            // Native Baneling dives on a 45-degree line, unlike HellLostSoul's target homing.
            Vector3 diagonal = new Vector3(direction, -1f, 0f).normalized;
            bool clear = MosquitoDiveClear(mook, diagonal, Mathf.Max(0f, drop * 1.414214f - 12f));
            if (visible && takeOffDelay <= 0f && drop >= 32f && counter >= 0.5f &&
                Mathf.Abs(Mathf.Abs(dx) - drop) <= (counter >= ai.maxDiveDelay ? 32f : 20f) && clear)
            {
                memory.Plan = Plan.Attack; memory.PursuitPoint = aim;
                mook.StartDiving(new Vector3(aim.x, aim.y, 0f), seenPlayer, -1f); return true;
            }
            memory.Plan = Plan.Search;
            if (!Plugin.Settings.PursueTargets) return true;
            float wantedX = Mathf.Clamp(aim.x - direction * 64f, (memory.AnchorColumn - PursuitTiles) * 16f, (memory.AnchorColumn + PursuitTiles) * 16f);
            float wantedY = Mathf.Min(aim.y + 64f, mook.CalculateCeilingHeight() - 24f);
            float moveX = Mathf.Clamp(wantedX - mook.X, -16f, 16f), moveY = Mathf.Clamp(wantedY - mook.Y, -16f, 16f);
            if (!SoulAirSpace(mook, moveX, moveY))
            {
                // A low ceiling can reject the diagonal while a horizontal approach is clear.
                if (SoulAirSpace(mook, moveX, 0f)) moveY = 0f;
                else if (SoulAirSpace(mook, 0f, moveY)) moveX = 0f;
                else return false;
            }
            memory.PursuitPoint = new Vector2(wantedX, wantedY); memory.Plan = Plan.Pursue;
            left = moveX < -3f; right = moveX > 3f; up = moveY > 3f; down = moveY < -3f; return true;
        }

        private static bool MosquitoDiveClear(AlienMosquito mook, Vector3 direction, float distance)
        {
            Collider support = mook.flyingInvisibleParachute.GetComponent<Collider>();
            foreach (RaycastHit hit in Physics.SphereCastAll(new Ray(new Vector3(mook.X, mook.Y + 8f, 0f), direction),
                Mathf.Max(2f, mook.HalfWidth), distance, Map.groundLayer | Map.fragileLayer))
                if (hit.collider != support) return false;
            return true;
        }
    }

    [HarmonyPatch(typeof(SkinlessMookAI), "DoAlertedThink")]
    internal static class PlanSkinlessContact
    {
        private static bool Prefix(SkinlessMookAI __instance, Mook ___mook, ref int ___seenEnemyNum)
        { return !Controller.Tick(__instance, ___mook, ref ___seenEnemyNum, true); }
    }

    [HarmonyPatch(typeof(AlienBruteAI), "DoAlertedThink")]
    internal static class PlanBruteRush
    {
        private static bool Prefix(AlienBruteAI __instance, Mook ___mook, ref int ___seenEnemyNum)
        { return !Controller.Tick(__instance, ___mook, ref ___seenEnemyNum, true); }
    }

    [HarmonyPatch(typeof(AlienMelterAI), "DoAlertedThink")]
    internal static class PlanMelterApproach
    {
        private static bool Prefix(AlienMelterAI __instance, Mook ___mook, ref int ___seenEnemyNum)
        { return !Controller.Tick(__instance, ___mook, ref ___seenEnemyNum, true); }
    }

    [HarmonyPatch(typeof(AlienMosquitoAI), "PrepareToDive")]
    internal static class PlanBanelingDive
    {
        private static bool Prefix(AlienMosquitoAI __instance, AlienMosquito ___mosquito, ref int ___seenEnemyNum,
            ref float ___divePositionCounter, float ___takeOffDelay, ref bool left, ref bool right, ref bool up, ref bool down)
        {
            return Controller.Kind(__instance, ___mosquito) != EnemyKind.Mosquito ||
                !Controller.MosquitoInput(__instance, ___mosquito, ref ___seenEnemyNum, ref ___divePositionCounter,
                    ___takeOffDelay, ref left, ref right, ref up, ref down);
        }
    }
}
