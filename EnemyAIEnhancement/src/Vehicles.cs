using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using Networking;
using UnityEngine;

namespace EnemyAIEnhancement
{
    internal sealed class VehicleObservation
    {
        internal PID Owner;
        internal int Player = -1;
        internal Vector3 Position, Velocity, Aim;
        internal float SeenAt = float.NegativeInfinity, NextSense, NextSend;
        internal bool Visible, Npc, Salvo, SalvoStarted;
    }

    internal static class VehicleController
    {
        internal static readonly Dictionary<Tank, VehicleObservation> Observations = new Dictionary<Tank, VehicleObservation>();
        private static Map currentMap;
        internal static int Selections, Received;
        private static readonly FieldInfo stun = AccessTools.Field(typeof(Tank), "stunTime");
        private static readonly FieldInfo seen = AccessTools.Field(typeof(PolymorphicAI), "seenEnemyNum");
        private static readonly FieldInfo salvos = AccessTools.Field(typeof(TankPolyAI), "salvosFired");
        private static readonly FieldInfo firing = AccessTools.Field(typeof(MookArtilleryTruck), "firing");
        private static readonly FieldInfo artilleryTarget = AccessTools.Field(typeof(MookArtilleryTruck), "targetPosition");
        private static readonly FieldInfo special = AccessTools.Field(typeof(TankCannon), "special");
        private static readonly FieldInfo descended = AccessTools.Field(typeof(MookBlimpAI), "hasDescended");
        private static readonly FieldInfo descend = AccessTools.Field(typeof(MookBlimpAI), "mustDescend");
        private static readonly FieldInfo amount = AccessTools.Field(typeof(MookBlimpAI), "descendAmount");
        private static bool UsesWeapon(Tank tank) { return tank.GetType() == typeof(Tank) || tank.GetType() == typeof(MookGunplacement); }

        internal static bool Exact(Tank tank)
        {
            PolymorphicAI ai = tank.enemyAI;
            if (ai == null) return false;
            Type type = tank.GetType(), decision = ai.GetType();
            if (type == typeof(Tank) && decision == typeof(TankPolyAI) && tank.weapon != null)
                return tank.weapon.GetType() == typeof(TankCannon) || tank.weapon.GetType() == typeof(TankMookLauncher) || tank.weapon.GetType() == typeof(TankrocketBattery);
            return type == typeof(MookArtilleryTruck) && decision == typeof(MookArtilleryTruckPolyAI) ||
                type == typeof(MookBlimp) && decision == typeof(MookBlimpAI) ||
                type == typeof(MookGunplacement) && decision == typeof(MookGunplacementAI) && tank.weapon != null && tank.weapon.GetType() == typeof(TankChaingun);
        }

        internal static bool Applicable(Tank tank)
        {
            return Plugin.Enabled && Exact(tank) && tank.enabled && tank.enemyAI.enabled && tank.gameObject.activeInHierarchy &&
                !Map.isEditing && tank.health > 0 && tank.actionState != ActionState.Dead && tank.playerNum < 0 && !tank.IsHero &&
                !tank.beingControlledByTriggerAction && !tank.IsIncapacitated() && (float)stun.GetValue(tank) <= 0f &&
                (!UsesWeapon(tank) || tank.weapon.enabled && tank.weapon.health > 0 && tank.weapon.playerNum < 0 && !tank.weapon.beingControlledByTriggerAction);
        }

        private static PID Owner(NetworkObject value) { return value.Owner == PID.TargetServer ? PID.ServerID : value.Owner; }
        private static bool MatchingOwners(Tank tank)
        {
            return Owner(tank) == Owner(tank.enemyAI) && (!UsesWeapon(tank) || Owner(tank) == Owner(tank.weapon));
        }
        internal static bool Owns(Tank tank)
        {
            return tank.IsMine && tank.enemyAI.IsMine && Owner(tank) == Owner(tank.enemyAI) &&
                (!UsesWeapon(tank) || tank.weapon.IsMine && Owner(tank) == Owner(tank.weapon));
        }

        internal static bool ClearSight(Tank tank, Vector3 origin, Vector3 point)
        {
            Vector3 ray = point + Vector3.up * 8f - origin;
            foreach (RaycastHit hit in Physics.RaycastAll(origin, ray.normalized, Mathf.Max(0f, ray.magnitude - 5f), Map.groundLayer | Map.fragileLayer))
                if (!hit.collider.transform.IsChildOf(tank.transform)) return false;
            return true;
        }

        internal static VehicleObservation Observe(Tank tank)
        {
            if (currentMap != Map.Instance) { Observations.Clear(); currentMap = Map.Instance; }
            VehicleObservation memory;
            if (!Applicable(tank) || !MatchingOwners(tank)) { Observations.Remove(tank); return null; }
            Observations.TryGetValue(tank, out memory);
            if (memory != null && memory.Owner != tank.Owner) { Observations.Remove(tank); memory = null; }
            if (!Owns(tank)) return memory;
            if (memory == null) { memory = new VehicleObservation(); memory.Owner = tank.Owner; Observations.Add(tank, memory); }
            float now = Time.time;
            if (now < memory.NextSense) return memory;
            memory.NextSense = now + 0.2f;
            PolymorphicAI ai = tank.enemyAI;
            float eye = tank is MookArtilleryTruck ? 48f : tank is MookGunplacement ? 20f : tank is MookBlimp ? 0f : 24f;
            Vector3 origin = new Vector3(tank.X, tank.Y + eye, 0f);
            // Native NPC combat retains priority and its own target representation.
            memory.Npc = Controller.PreferNativeNPC(ai, tank, false, Vector2.zero);
            int selected = -1; float best = float.MaxValue; Vector3 point = Vector3.zero;
            if (!memory.Npc) for (int i = 0; i < 4; i++)
            {
                if (!HeroController.PlayerIsAlive(i) || !GameModeController.DoesPlayerNumDamage(tank.playerNum, i) || HeroController.players[i].character.IsInStealthMode()) continue;
                Vector3 candidate = HeroController.GetPlayerPosition(i); candidate.z = 0f;
                Vector3 delta = candidate + Vector3.up * 8f - origin;
                if (Mathf.Abs(delta.x) > ai.sightRangeX || Mathf.Abs(delta.y) > ai.sightRangeY || !ClearSight(tank, origin, candidate)) continue;
                // The fixed gun has no turn/move input or elevated muzzle aiming.
                if (tank is MookGunplacement && (delta.x * tank.facingDirection <= 16f || Mathf.Abs(candidate.y + 8f - origin.y) > 24f)) continue;
                float score = delta.magnitude * (i == memory.Player ? 0.85f : 1f);
                if (score < best) { best = score; selected = i; point = candidate; }
            }
            memory.Visible = selected >= 0;
            if (memory.Visible)
            {
                float elapsed = now - memory.SeenAt;
                memory.Velocity = selected == memory.Player && elapsed >= 0.05f && elapsed <= 0.6f ? Vector3.ClampMagnitude((point - memory.Position) / elapsed, 120f) : Vector3.zero;
                memory.Position = point; memory.Player = selected; memory.SeenAt = now;
                float speed = tank.weapon is TankCannon ? ((TankCannon)tank.weapon).fireSpeed : tank.weapon is TankrocketBattery ? 90f : 180f;
                Vector3 predicted = point + Vector3.ClampMagnitude(memory.Velocity * Mathf.Clamp(best / Mathf.Max(1f, speed), 0f, 0.35f), 24f);
                predicted.x = Mathf.Clamp(predicted.x, 1f, Map.Width * 16f - 1f); predicted.y = Mathf.Clamp(predicted.y, 1f, Map.Height * 16f - 1f);
                memory.Aim = ClearSight(tank, origin, predicted) ? predicted : point; Selections++;
            }
            else
            {
                memory.Velocity = Vector3.zero;
                if (memory.Npc || !Plugin.Settings.RememberTargets || memory.Player < 0 || !HeroController.PlayerIsAlive(memory.Player) || now - memory.SeenAt > 6f)
                { memory.Player = -1; memory.Aim = Vector3.zero; }
                else memory.Aim = memory.Position;
            }
            // Artillery snapshots its target before unfolding. Never retarget a committed salvo.
            if (tank is MookArtilleryTruck && !memory.Salvo && !(bool)firing.GetValue(tank) && memory.Player >= 0)
                tank.SetTargetPlayerNum(memory.Player, memory.Aim);
            if (UsesWeapon(tank)) tank.weapon.SetTargetPlayerNum(memory.Npc ? ai.GetSeenPlayerNum() : memory.Player, memory.Aim);
            if (!Connect.IsOffline && tank.Nid.GameObjectID != 0 && now >= memory.NextSend)
            {
                memory.NextSend = now + 0.2f;
                global::Networking.Networking.RPC<NID, int, Vector3>(PID.TargetOthers, new RpcSignature<NID, int, Vector3>(ReceiveAim), tank.Nid, memory.Npc ? -2 : memory.Player, memory.Aim, false);
            }
            return memory;
        }

        [AllowedRPC]
        private static void ReceiveAim(NID id, int player, Vector3 aim)
        {
            if (Connect.IsOffline || id.Sid != Connect.SessionID || RPCController.CurrentRPC == null) return;
            Tank tank = Registry.GetObject(id) as Tank;
            if (tank != null) AcceptAim(tank, RPCController.LastSender, player, aim);
        }

        internal static bool AcceptAim(Tank tank, PID sender, int player, Vector3 aim)
        {
            if (!Applicable(tank) || tank.IsMine || sender == null || sender != Owner(tank) || Owner(tank) != Owner(tank.enemyAI) ||
                (UsesWeapon(tank) && Owner(tank.weapon) != Owner(tank)) || player < -2 || player > 3 ||
                float.IsNaN(aim.x) || float.IsNaN(aim.y) || float.IsInfinity(aim.x) || float.IsInfinity(aim.y) ||
                aim.x < 0f || aim.y < 0f || aim.x > Map.Width * 16f || aim.y > Map.Height * 16f || aim.z != 0f) return false;
            if (currentMap != Map.Instance) { Observations.Clear(); currentMap = Map.Instance; }
            VehicleObservation memory;
            if (!Observations.TryGetValue(tank, out memory)) { memory = new VehicleObservation(); Observations.Add(tank, memory); }
            memory.Owner = tank.Owner; memory.Player = player < 0 ? -1 : player; memory.Aim = aim; memory.Npc = player == -2;
            if (tank is MookArtilleryTruck && !memory.Salvo && !(bool)firing.GetValue(tank) && player >= 0) tank.SetTargetPlayerNum(player, aim);
            if (UsesWeapon(tank) && !memory.Npc) tank.weapon.SetTargetPlayerNum(player, aim);
            Received++; return true;
        }

        internal static void CommitArtillery(MookArtilleryTruck tank)
        {
            if (!Applicable(tank) || !Owns(tank) || (bool)firing.GetValue(tank)) return;
            VehicleObservation memory = Observe(tank);
            if (memory.Npc || memory.Player < 0) return;
            memory.Salvo = true; memory.SalvoStarted = false;
            if (!Connect.IsOffline)
                global::Networking.Networking.RPC<NID, int, Vector3>(PID.TargetOthers, new RpcSignature<NID, int, Vector3>(ReceiveSalvo), tank.Nid, memory.Player, (Vector3)artilleryTarget.GetValue(tank), false);
        }

        [AllowedRPC]
        private static void ReceiveSalvo(NID id, int player, Vector3 aim)
        {
            if (Connect.IsOffline || id.Sid != Connect.SessionID || RPCController.CurrentRPC == null || player < 0) return;
            MookArtilleryTruck tank = Registry.GetObject(id) as MookArtilleryTruck;
            if (tank == null || !AcceptAim(tank, RPCController.LastSender, player, aim)) return;
            Observations[tank].Salvo = true; Observations[tank].SalvoStarted = (bool)firing.GetValue(tank);
            // The native Fire queue can arrive before the next periodic aim message.
            // This exact committed point remains fixed through all three native shells.
            tank.SetTargetPlayerNum(player, aim);
        }

        internal static void EndArtillery(MookArtilleryTruck tank)
        {
            VehicleObservation memory;
            if (!Observations.TryGetValue(tank, out memory)) return;
            if ((bool)firing.GetValue(tank)) memory.SalvoStarted = true;
            else if (memory.SalvoStarted) { memory.Salvo = false; memory.SalvoStarted = false; }
        }

        internal static void Update(PolymorphicAI ai)
        {
            Tank tank = ai.GetComponent<Tank>();
            if (tank == null || !Exact(tank)) return;
            VehicleObservation memory = Observe(tank);
            if (memory == null || !Owns(tank) || memory.Npc || !memory.Visible || ai is MookBlimpAI || ai.mentalState != MentalState.Idle ||
                tank.actionState == ActionState.Turning || tank.weapon != null && tank.weapon.IsFiring()) return;
            ai.FullyAlert(memory.Position.x, memory.Position.y, memory.Player);
        }

        internal static bool Think(PolymorphicAI ai)
        {
            Tank tank = ai.GetComponent<Tank>();
            if (tank == null || !Applicable(tank) || !Owns(tank)) return true;
            VehicleObservation memory = Observe(tank);
            if (memory.Npc) return true;
            if (ai is MookBlimpAI) return true; // Native Fire/BecomeIdle and its complete drop cycle.
            if (tank.weapon != null && (tank.weapon.IsFiring() || tank.weapon is TankCannon && (bool)special.GetValue(tank.weapon)) ||
                tank is MookArtilleryTruck && (bool)firing.GetValue(tank))
            { ai.AddAction(EnemyActionType.Wait, 0.3f); return false; }
            if (memory.Player < 0) { ai.AddAction(EnemyActionType.Wait, 0.3f); return false; }
            seen.SetValue(ai, memory.Player);
            if (!(tank is MookGunplacement)) ai.AddAction(EnemyActionType.FacePoint, Map.GetGridPoint(memory.Aim));
            TankPolyAI tankAi = ai as TankPolyAI;
            if (tankAi != null)
            {
                if (tankAi.useSpecialWhenEnemyClose && memory.Visible && Mathf.Abs(memory.Position.x - tank.X) < 90f && Mathf.Abs(memory.Position.y - tank.Y - 20f) < 120f)
                { ai.AddAction(EnemyActionType.UseSpecial, 0.0334f); ai.AddAction(EnemyActionType.Wait, 2.5f); return false; }
                ai.AddAction(EnemyActionType.Wait, ai.sightDelay / 2f); ai.AddAction(EnemyActionType.Fire, ai.attackTime);
                int count = (int)salvos.GetValue(ai) + 1;
                ai.AddAction(EnemyActionType.Wait, count <= 3 ? UnityEngine.Random.Range(2.5f, 3.5f) : UnityEngine.Random.Range(4.5f, 5.5f));
                salvos.SetValue(ai, count <= 3 ? count : 0);
            }
            else if (tank is MookArtilleryTruck)
            {
                tank.SetTargetPlayerNum(memory.Player, memory.Aim);
                ai.AddAction(EnemyActionType.Wait, ai.sightDelay); ai.AddAction(EnemyActionType.Fire, ai.attackTime); ai.AddAction(EnemyActionType.Wait, 3f);
            }
            else { ai.AddAction(EnemyActionType.Wait, ai.delayBetweenFiring); ai.AddAction(EnemyActionType.Fire, ((MookGunplacementAI)ai).fireTime); }
            return false;
        }

        internal static bool BlimpIdle(MookBlimpAI ai)
        {
            Tank tank = ai.GetComponent<Tank>();
            if (!Applicable(tank) || !Owns(tank)) return true;
            VehicleObservation memory = Observe(tank);
            if (memory.Npc) return true;
            if (!memory.Visible) { ai.AddAction(EnemyActionType.Wait, 0.33f); return false; }
            // Only supported motion is the original one-time descent. No horizontal steering.
            ai.willDescendAutomaticallyOnce = false;
            ai.FullyAlert(memory.Position.x, memory.Position.y, memory.Player);
            ai.ClearActionQueue(true);
            if (!(bool)descended.GetValue(ai))
            {
                if ((bool)descend.GetValue(ai)) ai.AddAction(EnemyActionType.Hover, Map.GetGridPoint(tank.X, tank.Y - (float)amount.GetValue(ai)));
                descended.SetValue(ai, true);
            }
            return false;
        }

        private static Tank Body(TankWeapon weapon) { return weapon.GetComponentInParent<Tank>(); }
        internal static Vector3 CannonPoint(int player, TankCannon weapon)
        {
            Tank tank = Body(weapon);
            if (tank == null || tank.weapon != weapon) return HeroController.GetPlayerPos(player);
            VehicleObservation memory = Observe(tank);
            if (memory == null || memory.Npc) return HeroController.GetPlayerPos(player);
            return memory != null && memory.Player >= 0 ? memory.Aim : weapon.transform.position + Vector3.left * tank.facingDirection * 100f;
        }
        internal static void LauncherPoint(int player, ref float x, ref float y, TankMookLauncher weapon)
        {
            Tank tank = Body(weapon);
            if (tank == null || tank.weapon != weapon) { HeroController.GetPlayerPos(player, ref x, ref y); return; }
            VehicleObservation memory = Observe(tank);
            if (memory == null || memory.Npc) { HeroController.GetPlayerPos(player, ref x, ref y); return; }
            x = memory != null && memory.Player >= 0 ? memory.Aim.x : 0f; y = memory != null && memory.Player >= 0 ? memory.Aim.y : 0f;
        }
        internal static int LauncherNearest(float x, float y, float rangeX, float rangeY, TankMookLauncher weapon)
        {
            Tank tank = Body(weapon);
            if (tank == null || tank.weapon != weapon) return HeroController.GetActualNearestPlayer(x, y, rangeX, rangeY);
            VehicleObservation memory = Observe(tank);
            if (memory == null || memory.Npc) return HeroController.GetActualNearestPlayer(x, y, rangeX, rangeY);
            return memory == null ? -1 : memory.Player;
        }
        internal static void RocketTarget(PID destination, bool immediate, bool ignoreSession, bool delay, RpcSignature<float, float, int> method, float x, float y, int player, TankrocketBattery weapon)
        {
            Tank tank = Body(weapon);
            if (tank != null && Applicable(tank) && Owns(tank))
            {
                VehicleObservation memory = Observe(tank);
                if (!memory.Npc && memory.Player >= 0) { x = memory.Aim.x; y = memory.Aim.y + 8f; player = -1; }
            }
            // Native projectile RPC carries the actual shot's fixed observed point to both peers.
            global::Networking.Networking.RPC<float, float, int>(destination, immediate, ignoreSession, delay, method, x, y, player);
        }
        internal static void Forget(NetworkObject value) { Tank tank = value as Tank; if (tank != null) Observations.Remove(tank); }
        internal static void Reset() { Observations.Clear(); currentMap = null; }
    }

    [HarmonyPatch(typeof(PolymorphicAI), "Update")]
    internal static class ObserveVehicles { private static void Postfix(PolymorphicAI __instance) { VehicleController.Update(__instance); } }
    [HarmonyPatch]
    internal static class VehicleAttackOpportunity
    {
        private static IEnumerable<MethodBase> TargetMethods()
        {
            return new MethodBase[] {
                AccessTools.Method(typeof(TankPolyAI), "DoAlertedThink", null, null),
                AccessTools.Method(typeof(MookArtilleryTruckPolyAI), "DoAlertedThink", null, null),
                AccessTools.Method(typeof(MookGunplacementAI), "DoAlertedThink", null, null)
            };
        }
        private static bool Prefix(PolymorphicAI __instance) { return VehicleController.Think(__instance); }
    }
    [HarmonyPatch(typeof(MookBlimpAI), "DoIdleThink")]
    internal static class BlimpDeploymentOpportunity { private static bool Prefix(MookBlimpAI __instance) { return VehicleController.BlimpIdle(__instance); } }
    [HarmonyPatch(typeof(MookArtilleryTruck), "FireWeapon")]
    internal static class CommitArtilleryAim { private static void Prefix(MookArtilleryTruck __instance) { VehicleController.CommitArtillery(__instance); } }
    [HarmonyPatch(typeof(MookArtilleryTruck), "AnimateIdle")]
    internal static class EndArtilleryAim { private static void Postfix(MookArtilleryTruck __instance) { VehicleController.EndArtillery(__instance); } }

    [HarmonyPatch]
    internal static class VehicleWeaponTarget
    {
        private static IEnumerable<MethodBase> TargetMethods()
        {
            return new MethodBase[] {
                AccessTools.Method(typeof(TankCannon), "Update", null, null),
                AccessTools.Method(typeof(TankMookLauncher), "FireWeapon", null, null),
                AccessTools.Method(typeof(TankrocketBattery), "FireWeapon", null, null)
            };
        }
        private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions, MethodBase original)
        {
            foreach (CodeInstruction instruction in instructions)
            {
                MethodInfo called = instruction.operand as MethodInfo;
                string replacement = null;
                if (called != null && called.DeclaringType == typeof(HeroController) && called.Name == "GetPlayerPos")
                    replacement = original.DeclaringType == typeof(TankCannon) ? "CannonPoint" : "LauncherPoint";
                if (called != null && original.DeclaringType == typeof(TankMookLauncher) && called.DeclaringType == typeof(HeroController) && called.Name == "GetActualNearestPlayer") replacement = "LauncherNearest";
                if (called != null && original.DeclaringType == typeof(TankrocketBattery) && called.DeclaringType == typeof(global::Networking.Networking) && called.Name == "RPC") replacement = "RocketTarget";
                if (replacement == null) yield return instruction;
                else
                {
                    yield return new CodeInstruction(OpCodes.Ldarg_0, null).MoveLabelsFrom(instruction);
                    yield return new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(VehicleController), replacement, null, null));
                }
            }
        }
    }
}
