using HarmonyLib;
using UnityEngine;

namespace EnemyAIEnhancement
{
    internal static partial class Controller
    {
        internal static bool WarlockLook(MookWarlockAI ai, Mook mook)
        {
            if (!Supported(ai, mook) || GroundNativePhase(ai, mook)) return false;
            int player = -1;
            if (HeroController.CanSeePlayer(mook.X, mook.Y, ai.FacingDirection, ai.sightRangeX, ai.sightRangeY, ref player) &&
                player >= 0 && player < 4 && HeroController.PlayerIsAlive(player))
            {
                Vector2 point = HeroController.GetPlayerPosition(player);
                ai.FullyAlert(point.x, point.y, player);
            }
            return true;
        }

        internal static bool LostSoulInput(LostSoulAI ai, HellLostSoul mook, ref int seenPlayer,
            ref float counter, ref bool left, ref bool right, ref bool up, ref bool down)
        {
            if (!Supported(ai, mook) || !ai.enabled || (bool)faceFirst.GetValue(ai) || ai.mentalState != MentalState.Alerted ||
                mook.PlayingSummoningAnimation() || (mook.spawnGoop != null && mook.spawnGoop.gameObject.activeSelf) ||
                mook.IsDiving() || mook.IsIncapacitated() || mook.blindTime > 0f || mook.burnTime > 0f ||
                mook.tumbling || mook.backSomersaulting || mook.frontSomersaulting ||
                (bool)undeadDecapitated.GetValue(mook) || (bool)faceUsing.GetValue(mook) ||
                (float)AccessTools.Field(typeof(AlienMosquito), "forcedFlyingInDirectionTime").GetValue(mook) > 0f || seenPlayer == -1) return false;
            Memory memory = GetMemory(ai, mook);
            float now = Time.time;
            int target = seenPlayer;
            bool visible = HeroController.CanSeePlayer(mook.X, mook.Y, 0, ai.sightRangeX, ai.flyingSightYRange, ref target) &&
                target >= 0 && target < 4 && HeroController.PlayerIsAlive(target) && GameModeController.DoesPlayerNumDamage(mook.playerNum, target);
            if (visible)
            {
                Vector2 position = HeroController.GetPlayerPosition(target);
                float elapsed = now - memory.SeenAt;
                if (elapsed >= 0.05f && elapsed <= 0.6f && memory.Player == target)
                    memory.VisibleVelocity = Vector2.ClampMagnitude((position - memory.LastSeen) / elapsed, 120f);
                else if (elapsed > 0.6f || memory.Player != target) memory.VisibleVelocity = Vector2.zero;
                // Sampled visible positions only; no velocity/position reads after sight is lost.
                if (now >= memory.NextSense)
                {
                    memory.LastSeen = position; memory.SeenAt = now; memory.Player = target;
                    memory.NextSense = now + SenseInterval;
                }
                seenPlayer = target;
            }
            else if (!Plugin.Settings.RememberTargets || memory.Player < 0 || !HeroController.PlayerIsAlive(memory.Player) || now - memory.SeenAt > MemorySeconds)
            {
                memory.Player = -1; memory.Plan = Plan.None; seenPlayer = -10;
                ai.SetMentalState(MentalState.Suspicious); ai.ClearActionQueue(true); ai.AddAction(EnemyActionType.Wait, 0.3f);
                return true;
            }
            counter += Mathf.Clamp(Time.deltaTime, 0f, 0.033334f);
            Vector2 difference = memory.LastSeen - new Vector2(mook.X, mook.Y);
            Vector2 intercept = memory.LastSeen + Vector2.ClampMagnitude(memory.VisibleVelocity * 0.2f, 24f);
            Vector3 dive = new Vector3(intercept.x - mook.X, intercept.y - mook.Y, 0f);
            bool clearDive = !Physics.SphereCast(new Ray(new Vector3(mook.X, mook.Y + 8f, 0f), dive.normalized),
                Mathf.Max(2f, mook.HalfWidth), Mathf.Max(0f, dive.magnitude - 12f), Map.groundLayer | Map.fragileLayer);
            if (visible && clearDive && ((counter >= 0.6f && difference.y <= -32f && Mathf.Abs(difference.x) <= 192f) || counter >= ai.maxDiveDelay))
            {
                memory.Plan = Plan.Attack;
                mook.StartDiving(new Vector3(intercept.x, intercept.y, 0f), seenPlayer, -1f);
                return true;
            }
            memory.Plan = Plan.Search;
            if (!Plugin.Settings.PursueTargets) return true;
            float x = Mathf.Clamp(memory.LastSeen.x, (memory.AnchorColumn - PursuitTiles) * 16f, (memory.AnchorColumn + PursuitTiles) * 16f);
            float y = Mathf.Min(memory.LastSeen.y + 64f, mook.CalculateCeilingHeight() - 24f);
            float dx = Mathf.Clamp(x - mook.X, -16f, 16f), dy = Mathf.Clamp(y - mook.Y, -16f, 16f);
            if (!SoulAirSpace(mook, dx, dy))
            {
                dy = 16f;
                if (!SoulAirSpace(mook, dx, dy)) { dx = 0f; if (!SoulAirSpace(mook, dx, dy)) return true; }
            }
            left = dx < -3f; right = dx > 3f; up = dy > 3f; down = dy < -3f;
            memory.Plan = Plan.Pursue;
            return true;
        }

        private static bool SoulAirSpace(Mook mook, float dx, float dy)
        {
            float height = Mathf.Max(mook.height, mook.headHeight);
            if (mook.X + dx - mook.HalfWidth < 0f || mook.X + dx + mook.HalfWidth >= Map.Width * 16f ||
                mook.Y + dy < 0f || mook.Y + dy + height >= Map.Height * 16f) return false;
            Vector3 origin = new Vector3(mook.X, mook.Y + 1f + height * 0.5f, 0f), delta = new Vector3(dx, dy, 0f);
            Vector3 extents = new Vector3(mook.HalfWidth, height * 0.5f, 1f);
            int layers = Map.groundLayer | Map.fragileLayer;
            if (mook.GetType() == typeof(AlienMosquito))
            {
                AlienMosquito mosquito = (AlienMosquito)mook;
                Collider support = mosquito.flyingInvisibleParachute.GetComponent<Collider>();
                // Native flight enables this unit's own support collider inside its body.
                foreach (Collider collider in Physics.OverlapBox(origin, extents, Quaternion.identity, layers))
                    if (collider != support) return false;
                if (delta.sqrMagnitude >= 0.01f)
                    foreach (RaycastHit hit in Physics.BoxCastAll(origin, extents, delta.normalized, Quaternion.identity, delta.magnitude, layers))
                        if (hit.collider != support) return false;
                return true;
            }
            return !Physics.CheckBox(origin, extents, Quaternion.identity, layers) &&
                (delta.sqrMagnitude < 0.01f || !Physics.BoxCast(origin, extents, delta.normalized, Quaternion.identity, delta.magnitude, layers));
        }
    }

    [HarmonyPatch(typeof(MookWarlockAI), "LookForEnemy")]
    internal static class VisibleWarlockDiscovery
    {
        private static bool Prefix(MookWarlockAI __instance, Mook ___mook) { return !Controller.WarlockLook(__instance, ___mook); }
    }

    [HarmonyPatch(typeof(MookWarlockAI), "DoAlertedThink")]
    internal static class PlanPortalOpportunity
    {
        private static bool Prefix(MookWarlockAI __instance, Mook ___mook, ref int ___seenEnemyNum)
        { return !Controller.Tick(__instance, ___mook, ref ___seenEnemyNum, true); }
    }

    [HarmonyPatch(typeof(BoomerAI), "DoAlertedThink")]
    internal static class PlanSprayOpportunity
    {
        private static bool Prefix(BoomerAI __instance, Mook ___mook, ref int ___seenEnemyNum)
        { return !Controller.Tick(__instance, ___mook, ref ___seenEnemyNum, true); }
    }

    [HarmonyPatch(typeof(LostSoulAI), "PrepareToDive")]
    internal static class PlanSoulOpportunity
    {
        private static bool Prefix(LostSoulAI __instance, AlienMosquito ___mosquito, ref int ___seenEnemyNum,
            ref float ___divePositionCounter, ref bool left, ref bool right, ref bool up, ref bool down)
        {
            HellLostSoul soul = ___mosquito as HellLostSoul;
            return soul == null || !Controller.LostSoulInput(__instance, soul, ref ___seenEnemyNum, ref ___divePositionCounter,
                ref left, ref right, ref up, ref down);
        }
    }
}
