using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using Networking;
using UnityEngine;

namespace EnemyAIEnhancement
{
    internal sealed class WormObservation
    {
        internal PID Owner;
        internal int Player = -1;
        internal Vector3 Position, Velocity, Aim;
        internal float SeenAt = float.NegativeInfinity, NextSense, NextSend;
        internal bool Visible;
    }

    internal static class WormController
    {
        internal static readonly Dictionary<AlienMinibossSandWorm, WormObservation> Observations = new Dictionary<AlienMinibossSandWorm, WormObservation>();
        private static Map currentMap;
        internal static int Selections, Received;
        private static readonly FieldInfo activated = AccessTools.Field(typeof(AlienSandWorm), "activated");
        private static readonly FieldInfo top = AccessTools.Field(typeof(AlienMinibossSandWorm), "highestPoint");
        private static readonly FieldInfo bulge = AccessTools.Field(typeof(AlienMinibossSandWorm), "throatBulgeMoving");
        private static readonly FieldInfo firing = AccessTools.Field(typeof(AlienMinibossSandWorm), "waitingForAnimationToFire");
        private static readonly FieldInfo biting = AccessTools.Field(typeof(AlienMinibossSandWorm), "waitingForAnimationToBite");
        private static readonly FieldInfo swallowed = AccessTools.Field(typeof(AlienMinibossSandWorm), "hasSwallowedGrenade");
        private static readonly FieldInfo stun = AccessTools.Field(typeof(AlienMinibossSandWorm), "stunTime");
        private static readonly FieldInfo blind = AccessTools.Field(typeof(AlienWormFacehuggerLauncher), "blindTime");
        private static readonly FieldInfo boneSetup = AccessTools.Field(typeof(HellBoneWormMiniboss), "vomittingSetup");
        private static readonly FieldInfo boneVomit = AccessTools.Field(typeof(HellBoneWormMiniboss), "vomitting");
        private static readonly FieldInfo giantSetup = AccessTools.Field(typeof(AlienGiantBossSandworm), "vomittingSetup");
        private static readonly FieldInfo giantVomit = AccessTools.Field(typeof(AlienGiantBossSandworm), "vomitting");
        private static readonly FieldInfo giantHold = AccessTools.Field(typeof(AlienGiantBossSandworm), "secondarFireHoldPositionTime");
        private static readonly FieldInfo boneCount = AccessTools.Field(typeof(HellBoneWormMiniboss), "fireProjectileCount");
        private static readonly MethodInfo boneSound = AccessTools.Method(typeof(HellBoneWormMiniboss), "PlayFireSound", null, null);

        internal static bool Exact(AlienMinibossSandWorm worm)
        {
            Type type = worm.GetType();
            return type == typeof(AlienMinibossSandWorm) || type == typeof(HellBoneWormMiniboss) ||
                type == typeof(AlienWormFacehuggerLauncher) || type == typeof(AlienGiantBossSandworm);
        }

        internal static bool Applicable(AlienMinibossSandWorm worm)
        {
            return Plugin.Enabled && Exact(worm) && worm.enabled && worm.gameObject.activeInHierarchy && !Map.isEditing &&
                worm.health > 0 && worm.actionState != ActionState.Dead && worm.playerNum < 0 && !worm.IsHero &&
                !worm.beingControlledByTriggerAction && !worm.IsIncapacitated();
        }

        internal static bool NativePhase(AlienMinibossSandWorm worm)
        {
            return !(bool)activated.GetValue(worm) || worm.Y < (float)top.GetValue(worm) ||
                (bool)bulge.GetValue(worm) || (bool)firing.GetValue(worm) || (bool)biting.GetValue(worm) ||
                (bool)swallowed.GetValue(worm) || (float)stun.GetValue(worm) > 0f ||
                (worm.GetType() == typeof(AlienWormFacehuggerLauncher) && (float)blind.GetValue(worm) > 0f) ||
                (worm.GetType() == typeof(HellBoneWormMiniboss) && ((bool)boneSetup.GetValue(worm) || (bool)boneVomit.GetValue(worm))) ||
                (worm.GetType() == typeof(AlienGiantBossSandworm) && ((bool)giantSetup.GetValue(worm) ||
                    (bool)giantVomit.GetValue(worm) || (float)giantHold.GetValue(worm) > 0f));
        }

        private static bool ClearSight(AlienMinibossSandWorm worm, float x, float y, Vector3 point)
        {
            Vector3 origin = new Vector3(x, y, 0f);
            Vector3 ray = point + Vector3.up * 6f - origin;
            // Worm neck balls are ground-layer colliders in the actual prefabs. Only this
            // source's own colliders are excluded from sight; body/damage collision is untouched.
            foreach (RaycastHit hit in Physics.RaycastAll(origin, ray.normalized, Mathf.Max(0f, ray.magnitude - 6f), Map.groundLayer | Map.fragileLayer))
                if (!hit.collider.transform.IsChildOf(worm.transform)) return false;
            return true;
        }

        // Replaces only the native nearest-position query after ascent, not the body/attack sequence.
        internal static Vector3 Target(float x, float y, AlienMinibossSandWorm worm)
        {
            if (currentMap != Map.Instance) { Observations.Clear(); currentMap = Map.Instance; }
            if (!Applicable(worm))
            {
                Observations.Remove(worm);
                return HeroController.GetNearestPlayerPos(x, y);
            }
            WormObservation memory;
            if (Observations.TryGetValue(worm, out memory) && memory.Owner != worm.Owner)
            { Observations.Remove(worm); memory = null; }
            // A committed throat, bite, stun or boss sweep keeps its native aim and timing.
            if (NativePhase(worm)) return worm.Pos;
            if (!worm.IsMine) return memory == null ? Vector3.zero : memory.Aim;
            if (memory == null)
            {
                memory = new WormObservation(); memory.Owner = worm.Owner; Observations.Add(worm, memory);
            }
            float now = Time.time;
            if (now < memory.NextSense) return memory.Aim;
            memory.NextSense = now + 0.2f;
            int selected = -1; Vector3 point = Vector3.zero; float best = float.MaxValue;
            for (int i = 0; i < 4; i++)
            {
                if (!HeroController.PlayerIsAlive(i) || !GameModeController.DoesPlayerNumDamage(worm.playerNum, i) ||
                    HeroController.players[i].character.IsInStealthMode() || !ClearSight(worm, x, y, HeroController.GetPlayerPosition(i))) continue;
                Vector3 candidate = HeroController.GetPlayerPosition(i);
                candidate.z = 0f; // Native hero transforms have a small rendering-depth offset.
                float distance = Vector2.Distance(new Vector2(x, y), candidate);
                if (distance >= worm.maxRange) continue;
                float score = distance * (i == memory.Player ? 0.85f : 1f);
                if (score < best) { best = score; point = candidate; selected = i; }
            }
            memory.Visible = selected >= 0;
            if (memory.Visible)
            {
                float elapsed = now - memory.SeenAt;
                memory.Velocity = selected == memory.Player && elapsed >= 0.05f && elapsed <= 0.6f
                    ? Vector3.ClampMagnitude((point - memory.Position) / elapsed, 120f) : Vector3.zero;
                memory.Position = point; memory.Player = selected; memory.SeenAt = now;
                // Straight launched souls and ballistic facehuggers have different flight windows.
                float dx = Mathf.Abs(point.x - x);
                float speed = worm is HellBoneWormMiniboss ? ((HellBoneWormMiniboss)worm).lostSoulFireSpeed :
                    worm.fireSpeed + Mathf.Sqrt(dx) * worm.fireSpeedAdjustPerUnitXSqrt + dx * worm.fireSpeedAdjustPerUnitX;
                float window = worm is AlienWormFacehuggerLauncher ? 0.25f : worm is HellBoneWormMiniboss ? 0.45f : 0.35f;
                float limit = worm is AlienWormFacehuggerLauncher ? 16f : 24f;
                Vector3 predicted = point + Vector3.ClampMagnitude(memory.Velocity * Mathf.Clamp(Vector2.Distance(new Vector2(x, y), point) / Mathf.Max(1f, speed), 0f, window), limit);
                predicted.x = Mathf.Clamp(predicted.x, 1f, Map.Width * 16f - 1f);
                predicted.y = Mathf.Clamp(predicted.y, 1f, Map.Height * 16f - 1f);
                memory.Aim = Vector2.Distance(new Vector2(x, y), predicted) < worm.maxRange &&
                    ClearSight(worm, x, y, predicted) ? predicted : point;
                Selections++;
            }
            else
            {
                memory.Velocity = Vector3.zero;
                if (!Plugin.Settings.RememberTargets || memory.Player < 0 || !HeroController.PlayerIsAlive(memory.Player) || now - memory.SeenAt > 6f)
                { memory.Player = -1; memory.Aim = Vector3.zero; }
                else memory.Aim = memory.Position;
            }
            if (!Connect.IsOffline && worm.Nid.GameObjectID != 0 && now >= memory.NextSend)
            {
                memory.NextSend = now + 0.2f;
                global::Networking.Networking.RPC<NID, Vector3>(PID.TargetOthers, new RpcSignature<NID, Vector3>(ReceiveAim), worm.Nid, memory.Aim, false);
            }
            return memory.Aim;
        }

        [AllowedRPC]
        private static void ReceiveAim(NID id, Vector3 aim)
        {
            if (Connect.IsOffline || id.Sid != Connect.SessionID || RPCController.CurrentRPC == null) return;
            AlienMinibossSandWorm worm = Registry.GetObject(id) as AlienMinibossSandWorm;
            if (worm != null) AcceptAim(worm, RPCController.LastSender, aim);
        }

        internal static bool AcceptAim(AlienMinibossSandWorm worm, PID sender, Vector3 aim)
        {
            if (!Applicable(worm) || worm.IsMine || sender == null ||
                sender != (worm.Owner == PID.TargetServer ? PID.ServerID : worm.Owner) ||
                float.IsNaN(aim.x) || float.IsNaN(aim.y) || float.IsInfinity(aim.x) || float.IsInfinity(aim.y) ||
                aim.x < 0f || aim.y < 0f || aim.x > Map.Width * 16f || aim.y > Map.Height * 16f || aim.z != 0f) return false;
            if (currentMap != Map.Instance) { Observations.Clear(); currentMap = Map.Instance; }
            WormObservation memory;
            if (!Observations.TryGetValue(worm, out memory)) { memory = new WormObservation(); Observations.Add(worm, memory); }
            memory.Owner = worm.Owner; memory.Aim = aim; Received++;
            return true;
        }

        internal static bool BoneShot(HellBoneWormMiniboss worm)
        {
            if (!Plugin.Enabled || worm.GetType() != typeof(HellBoneWormMiniboss) || worm.playerNum >= 0 ||
                worm.beingControlledByTriggerAction || Map.isEditing || worm.IsMine) return true;
            // Native FireProjectile lacks ownership checking. Preserve the paired-shot counter and
            // local presentation; only the owner runs network spawn/FlyInDirection.
            boneCount.SetValue(worm, (int)boneCount.GetValue(worm) + 1);
            Vector3 right = worm.headTransform.right;
            Vector3 point = worm.headTransform.position + right * worm.headFirePointOffset.x + worm.headTransform.up * worm.headFirePointOffset.y;
            EffectsController.CreateSlimeParticles(worm.bloodColor, point.x, point.y, 24, 4f, 4f, 120f, right.x * 180f, right.y * 180f);
            boneSound.Invoke(worm, null);
            return false;
        }

        internal static void Forget(NetworkObject value)
        {
            AlienMinibossSandWorm worm = value as AlienMinibossSandWorm;
            if (worm != null) Observations.Remove(worm);
        }
        internal static void Reset() { Observations.Clear(); currentMap = null; }
    }

    [HarmonyPatch(typeof(AlienMinibossSandWorm), "RunWormActivated")]
    internal static class ObserveWormTarget
    {
        private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            MethodInfo nearest = AccessTools.Method(typeof(HeroController), "GetNearestPlayerPos", new Type[] { typeof(float), typeof(float) }, null);
            foreach (CodeInstruction instruction in instructions)
            {
                if (instruction.Calls(nearest))
                {
                    yield return new CodeInstruction(OpCodes.Ldarg_0, null).MoveLabelsFrom(instruction);
                    yield return new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(WormController), "Target", null, null));
                }
                else yield return instruction;
            }
        }
    }

    [HarmonyPatch(typeof(HellBoneWormMiniboss), "FireProjectile")]
    internal static class OwnBonewormSouls
    {
        private static bool Prefix(HellBoneWormMiniboss __instance) { return WormController.BoneShot(__instance); }
    }

    [HarmonyPatch(typeof(AlienMinibossSandWorm), "Deactivate")]
    internal static class ForgetInactiveWorm
    {
        private static void Postfix(AlienMinibossSandWorm __instance) { WormController.Forget(__instance); }
    }
}
