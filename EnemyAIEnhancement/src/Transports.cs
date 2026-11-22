using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using Networking;
using UnityEngine;

namespace EnemyAIEnhancement
{
    internal sealed class TransportObservation
    {
        internal PID Owner;
        internal int Player = -1;
        internal Vector3 Point, Velocity;
        internal float SeenAt = float.NegativeInfinity, NextSense, NextSend;
        internal bool Visible, Planned;
    }

    internal static class TransportController
    {
        internal static readonly Dictionary<Tank, TransportObservation> Observations = new Dictionary<Tank, TransportObservation>();
        internal static int Selections, Plans, Received;
        private static Map currentMap;
        private static readonly FieldInfo stun = AccessTools.Field(typeof(Tank), "stunTime");
        private static readonly FieldInfo seen = AccessTools.Field(typeof(PolymorphicAI), "seenEnemyNum");
        private static readonly FieldInfo spawned = AccessTools.Field(typeof(MookMotorBike), "mookSpawned");
        private static readonly FieldInfo count = AccessTools.Field(typeof(MookTruck), "mookSpawnCount");
        private static readonly FieldInfo moved = AccessTools.Field(typeof(MookDumpTruckAI), "_hasMoved");
        private static readonly FieldInfo passed = AccessTools.Field(typeof(MookDumpTruckAI), "hasMovedPastPlayer");
        private static readonly FieldInfo maxDistance = AccessTools.Field(typeof(MookDumpTruckAI), "maxDistance");
        private static readonly FieldInfo chucking = AccessTools.Field(typeof(DrillCarrier), "chuckingMooks");
        private static PID Owner(NetworkObject value) { return value.Owner == PID.TargetServer ? PID.ServerID : value.Owner; }

        internal static bool Exact(Tank tank)
        {
            if (tank == null) return false;
            if (tank.GetType() == typeof(DrillCarrier)) return tank.enemyAI == null;
            if (tank.enemyAI == null) return false;
            Type body = tank.GetType(), ai = tank.enemyAI.GetType();
            return body == typeof(MookTruck) && ai == typeof(MookTruckPolyAI) ||
                body == typeof(MookDumpTruck) && ai == typeof(MookDumpTruckAI) ||
                body == typeof(MookMotorBike) && ai == typeof(MookMotorBikePolyAI);
        }

        internal static bool Applicable(Tank tank)
        {
            return Plugin.Enabled && Exact(tank) && tank.enabled && tank.gameObject.activeInHierarchy && !Map.isEditing &&
                tank.health > 0 && tank.actionState != ActionState.Dead && tank.playerNum < 0 && !tank.IsHero &&
                !tank.beingControlledByTriggerAction && !tank.IsIncapacitated() && (float)stun.GetValue(tank) <= 0f &&
                (tank.enemyAI == null || tank.enemyAI.enabled && Owner(tank) == Owner(tank.enemyAI));
        }

        internal static bool Owns(Tank tank) { return tank.IsMine && (tank.enemyAI == null || tank.enemyAI.IsMine); }

        internal static TransportObservation Observe(Tank tank)
        {
            if (currentMap != Map.Instance) { Observations.Clear(); currentMap = Map.Instance; }
            if (!Applicable(tank)) { Observations.Remove(tank); return null; }
            TransportObservation memory;
            Observations.TryGetValue(tank, out memory);
            if (memory != null && memory.Owner != tank.Owner) { Observations.Remove(tank); memory = null; }
            if (!Owns(tank)) return memory;
            if (memory == null) { memory = new TransportObservation(); memory.Owner = tank.Owner; Observations.Add(tank, memory); }
            float now = Time.time;
            if (now < memory.NextSense) return memory;
            memory.NextSense = now + 0.2f;
            int selected = -1; float best = float.MaxValue; Vector3 point = Vector3.zero;
            Vector3 origin = new Vector3(tank.X, tank.Y + (tank is MookMotorBike ? 12f : 32f), 0f);
            float rangeX = tank.enemyAI == null ? 240f : tank.enemyAI.sightRangeX;
            float rangeY = tank.enemyAI == null ? 120f : Mathf.Max(32f, tank.enemyAI.sightRangeY);
            bool npc = tank.enemyAI != null && Controller.PreferNativeNPC(tank.enemyAI, tank, false, Vector2.zero);
            for (int i = 0; !npc && i < 4; i++)
            {
                if (!HeroController.PlayerIsAlive(i) || !GameModeController.DoesPlayerNumDamage(tank.playerNum, i) || HeroController.players[i].character.IsInStealthMode()) continue;
                Vector3 candidate = HeroController.GetPlayerPosition(i); candidate.z = 0f;
                Vector3 delta = candidate + Vector3.up * 8f - origin;
                if (Mathf.Abs(delta.x) > rangeX || Mathf.Abs(delta.y) > rangeY || !VehicleController.ClearSight(tank, origin, candidate)) continue;
                float score = delta.magnitude * (i == memory.Player ? 0.85f : 1f);
                if (score < best) { best = score; selected = i; point = candidate; }
            }
            memory.Visible = selected >= 0;
            if (memory.Visible)
            {
                float elapsed = now - memory.SeenAt;
                memory.Velocity = selected == memory.Player && elapsed >= 0.05f && elapsed <= 0.6f ? Vector3.ClampMagnitude((point - memory.Point) / elapsed, 120f) : Vector3.zero;
                memory.Point = point; memory.Player = selected; memory.SeenAt = now; Selections++;
            }
            else
            {
                memory.Velocity = Vector3.zero;
                if (npc || !Plugin.Settings.RememberTargets || now - memory.SeenAt > 6f || memory.Player < 0 || !HeroController.PlayerIsAlive(memory.Player)) memory.Player = -1;
            }
            // Only the body owner observes. The host may consume this copy when native spawning belongs to it.
            if (!Connect.IsOffline && tank.Nid.GameObjectID != 0 && now >= memory.NextSend)
            {
                memory.NextSend = now + 0.2f;
                global::Networking.Networking.RPC<NID, int, Vector3>(PID.TargetOthers, new RpcSignature<NID, int, Vector3>(Receive), tank.Nid, memory.Player, memory.Point, false);
            }
            return memory;
        }

        [AllowedRPC]
        private static void Receive(NID id, int player, Vector3 point)
        {
            if (Connect.IsOffline || id.Sid != Connect.SessionID || RPCController.CurrentRPC == null) return;
            Tank tank = Registry.GetObject(id) as Tank;
            if (tank != null) Accept(tank, RPCController.LastSender, player, point);
        }
        internal static bool Accept(Tank tank, PID sender, int player, Vector3 point)
        {
            if (!Applicable(tank) || tank.IsMine || sender == null || sender != Owner(tank) || player < -1 || player > 3 ||
                float.IsNaN(point.x) || float.IsNaN(point.y) || float.IsInfinity(point.x) || float.IsInfinity(point.y) ||
                point.x < 0f || point.y < 0f || point.x > Map.Width * 16f || point.y > Map.Height * 16f || point.z != 0f) return false;
            if (currentMap != Map.Instance) { Observations.Clear(); currentMap = Map.Instance; }
            TransportObservation memory;
            if (!Observations.TryGetValue(tank, out memory)) { memory = new TransportObservation(); Observations.Add(tank, memory); }
            memory.Owner = tank.Owner; memory.Player = player; memory.Point = point; memory.SeenAt = Time.time;
            Received++; return true;
        }

        private static bool ClearBody(Tank tank, float x, float y, float halfWidth, float height)
        {
            foreach (Collider collider in Physics.OverlapBox(new Vector3(x, y + height * 0.5f + 2f, 0f), new Vector3(halfWidth, height * 0.5f, 2f), Quaternion.identity, Map.groundLayer | Map.fragileLayer))
                if (!collider.transform.IsChildOf(tank.transform)) return false;
            return true;
        }
        // Include physical child colliders, rather than the small inherited Tank.width alone.
        internal static bool SafeDrive(Tank tank, float destination)
        {
            float width = tank.widthAgainstTerrain, height = tank.height;
            foreach (Collider collider in tank.GetComponentsInChildren<Collider>())
                if (collider.enabled && !collider.isTrigger)
                { width = Mathf.Max(width, Mathf.Abs(collider.bounds.min.x - tank.X), Mathf.Abs(collider.bounds.max.x - tank.X)); height = Mathf.Max(height, collider.bounds.max.y - tank.Y); }
            float low = Mathf.Min(tank.X, destination), high = Mathf.Max(tank.X, destination);
            if (low - width < 8f || high + width > Map.Width * 16f - 8f || tank.Y < 8f || tank.Y + height >= Map.Height * 16f) return false;
            int steps = Mathf.Max(1, Mathf.CeilToInt((high - low) / 4f));
            for (int i = 0; i <= steps; i++)
                if (!ClearBody(tank, Mathf.Lerp(low, high, (float)i / steps), tank.Y, width, height)) return false;
            for (float x = low - width; x <= high + width; x += 4f)
            {
                RaycastHit hit;
                if (!Physics.Raycast(new Vector3(x, tank.Y + 3f, 0f), Vector3.down, out hit, 6f, Map.groundLayer) || hit.collider.transform.IsChildOf(tank.transform)) return false;
            }
            return true;
        }
        internal static bool ExitClear(Tank tank, int direction)
        {
            float from = tank.X + direction * 8f, to = tank.X + direction * 72f;
            if (Mathf.Min(from, to) < 8f || Mathf.Max(from, to) > Map.Width * 16f - 8f) return false;
            for (float x = Mathf.Min(from, to); x <= Mathf.Max(from, to); x += 8f)
                if (!ClearBody(tank, x, tank.Y + 24f, 6f, 20f)) return false;
            return true;
        }

        private static bool Deployable(Tank tank, TransportObservation memory)
        {
            float dx = memory.Point.x - tank.X;
            return memory.Visible && dx >= 40f && dx <= 176f && Mathf.Abs(memory.Point.y - tank.Y) <= 64f && SafeDrive(tank, tank.X) && ExitClear(tank, 1);
        }
        private static bool Committed(MookTruck tank)
        {
            return (int)count.GetValue(tank) > 0 || tank.enemyAI.actionQueue.Count > 0 && tank.enemyAI.actionQueue[0].type == EnemyActionType.Fire;
        }
        internal static bool TruckStop(MookTruckPolyAI ai)
        {
            MookTruck tank = ai.GetComponent<MookTruck>();
            if (!Applicable(tank) || !Owns(tank) || Committed(tank)) return true;
            TransportObservation memory = Observe(tank);
            if (ai.actionQueue.Count > 0 && ai.actionQueue[0].type == EnemyActionType.Move && Deployable(tank, memory))
            {
                ai.ClearActionQueue(true); seen.SetValue(ai, memory.Player); ai.SetMentalState(MentalState.Alerted);
                ai.AddAction(EnemyActionType.Fire, 1E+12f); memory.Planned = true; Plans++;
                if (ai is MookDumpTruckAI) passed.SetValue(ai, true);
            }
            return false; // A range-only nearby check cannot stop for an unseen hero.
        }
        internal static bool TruckThink(MookTruckPolyAI ai)
        {
            MookTruck tank = ai.GetComponent<MookTruck>();
            if (!Applicable(tank) || !Owns(tank) || Committed(tank)) return true;
            TransportObservation memory = Observe(tank);
            if (!memory.Visible || memory.Planned) return true;
            bool dump = ai is MookDumpTruckAI;
            if (dump && (bool)moved.GetValue(ai)) return true;
            float destination = Mathf.Max(tank.X - (dump ? (float)maxDistance.GetValue(ai) : 160f), memory.Point.x - 80f);
            destination = Mathf.Min(tank.X, Map.GetCollumn(destination) * 16f);
            if (!Deployable(tank, memory) && (!Plugin.Settings.PursueTargets || destination >= tank.X - 8f || !SafeDrive(tank, destination) || !ExitClearAt(tank, destination))) return true;
            ai.ClearActionQueue(true); seen.SetValue(ai, memory.Player); ai.SetMentalState(MentalState.Alerted);
            if (!Deployable(tank, memory))
            {
                ai.AddAction(EnemyActionType.Move, Map.GetGridPoint(destination, tank.Y));
                if (dump) moved.SetValue(ai, true); // Set after native AddAction's one-drive gate.
            }
            ai.AddAction(EnemyActionType.Fire, 1E+12f); memory.Planned = true; Plans++; return false;
        }
        private static bool ExitClearAt(Tank tank, float x)
        {
            if (x + 80f > Map.Width * 16f - 8f) return false;
            for (float px = x + 8f; px <= x + 72f; px += 8f)
                if (!ClearBody(tank, px, tank.Y + 24f, 6f, 20f)) return false;
            return true;
        }

        internal static bool BikeInput(MookMotorBikePolyAI ai, ref bool left, ref bool right, ref bool fire)
        {
            MookMotorBike bike = ai.GetComponent<MookMotorBike>();
            if (!Applicable(bike) || !Owns(bike) || (bool)spawned.GetValue(bike) || !bike.IsOnGround() || Mathf.Abs(bike.yI) > 1f ||
                Controller.PreferNativeNPC(ai, bike, false, Vector2.zero)) return true;
            TransportObservation memory = Observe(bike);
            right = false; left = false; fire = false;
            bool rolling = Mathf.Abs(bike.xI) > 1f;
            if (memory.Player < 0) { left = rolling; return false; }
            float dx = memory.Point.x - bike.X;
            // Native vehicle has no braking input: validate the whole approach before starting.
            if (rolling || Plugin.Settings.PursueTargets && dx < -16f && SafeDrive(bike, Mathf.Min(bike.X, memory.Point.x + ai.attackXRange)))
            { left = true; ai.SetMentalState(MentalState.Alerted); }
            Vector3 intercept = memory.Point + (memory.Visible ? Vector3.ClampMagnitude(memory.Velocity * 0.2f, 24f) : Vector3.zero);
            float reach = ai.attackXRange + (bike.nuclearBombSprite != null ? 12f : 16f);
            if (memory.Visible && Mathf.Abs(intercept.y - bike.Y) <= 32f && intercept.x <= bike.X + 8f && bike.X - intercept.x <= reach)
                fire = true;
            return false;
        }
        internal static int DrillDirection(int native, DrillCarrier tank)
        {
            if (!Connect.IsHost || !Applicable(tank) || !(bool)chucking.GetValue(tank)) return native;
            TransportObservation memory = Observe(tank);
            if (memory == null || memory.Player < 0 || Time.time - memory.SeenAt > 6f) return native;
            int direction = memory.Point.x < tank.X ? -1 : 1;
            if (!ExitClear(tank, direction)) return native;
            Plans++; return direction;
        }
        internal static void Tick(Tank tank) { if (Exact(tank)) Observe(tank); }
        internal static void Forget(NetworkObject value) { Tank tank = value as Tank; if (tank != null) Observations.Remove(tank); }
        internal static void Reset() { Observations.Clear(); currentMap = null; }
    }

    [HarmonyPatch(typeof(Tank), "Update")]
    internal static class ObserveTransports { private static void Postfix(Tank __instance) { TransportController.Tick(__instance); } }
    [HarmonyPatch]
    internal static class TransportStopOpportunity
    {
        private static IEnumerable<MethodBase> TargetMethods()
        {
            return new MethodBase[] {
                AccessTools.Method(typeof(MookTruckPolyAI), "StopForNearbyPlayer", null, null),
                AccessTools.Method(typeof(MookDumpTruckAI), "StopForNearbyPlayer", null, null)
            };
        }
        private static bool Prefix(MookTruckPolyAI __instance) { return TransportController.TruckStop(__instance); }
    }
    [HarmonyPatch]
    internal static class TransportParkingOpportunity
    {
        private static IEnumerable<MethodBase> TargetMethods()
        {
            return new MethodBase[] {
                AccessTools.Method(typeof(MookTruckPolyAI), "LookForEnemy", null, null),
                AccessTools.Method(typeof(MookTruckPolyAI), "DoAlertedThink", null, null),
                AccessTools.Method(typeof(MookDumpTruckAI), "LookForEnemy", null, null),
                AccessTools.Method(typeof(MookDumpTruckAI), "DoAlertedThink", null, null)
            };
        }
        private static bool Prefix(MookTruckPolyAI __instance) { return TransportController.TruckThink(__instance); }
    }
    [HarmonyPatch(typeof(MookMotorBikePolyAI), "GetInput")]
    internal static class TransportBikeOpportunity
    {
        private static bool Prefix(MookMotorBikePolyAI __instance, ref bool left, ref bool right, ref bool up, ref bool down,
            ref bool jump, ref bool fire, ref bool special1, ref bool special2, ref bool special3, ref bool special4, ref bool climbButton)
        {
            bool native = TransportController.BikeInput(__instance, ref left, ref right, ref fire);
            if (!native) up = down = jump = special1 = special2 = special3 = special4 = climbButton = false;
            return native;
        }
    }
    [HarmonyPatch(typeof(DrillCarrier), "FireWeapon")]
    internal static class TransportDrillOpportunity
    {
        private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            foreach (CodeInstruction instruction in instructions)
            {
                // Native local 0 is the already obstacle-adjusted throw direction, immediately before the sound/spawn.
                if (instruction.operand is FieldInfo && ((FieldInfo)instruction.operand).Name == "throwSound")
                {
                    // The stack already contains the sound receiver and this; keep it intact.
                    yield return new CodeInstruction(OpCodes.Ldloc_0, null);
                    yield return new CodeInstruction(OpCodes.Ldarg_0, null);
                    yield return new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(TransportController), "DrillDirection", null, null));
                    yield return new CodeInstruction(OpCodes.Stloc_0, null);
                }
                yield return instruction;
            }
        }
    }
}
