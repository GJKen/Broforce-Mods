using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using Networking;
using UnityEngine;

namespace EnemyAIEnhancement
{
    internal sealed class BossObservation
    {
        internal PID Owner;
        internal int Player = -1;
        internal Vector3 Position, Velocity, Aim, Committed;
        internal float SeenAt = float.NegativeInfinity, NextSense, NextSend, ReceivedAt;
        internal bool Visible, Npc, HasCommit;
    }

    internal static class BossController
    {
        internal static readonly Dictionary<Unit, BossObservation> Observations = new Dictionary<Unit, BossObservation>();
        private static Map currentMap;
        internal static int Selections, Received, Cycles;
        private static object Field(object value, string name) { return AccessTools.Field(value.GetType(), name).GetValue(value); }
        private static float Number(object value, string name) { return Convert.ToSingle(Field(value, name)); }
        private static PID Owner(NetworkObject value) { return value.Owner == PID.TargetServer ? PID.ServerID : value.Owner; }
        internal static bool Exact(Unit body)
        {
            PolymorphicAI ai = body.GetComponent<PolymorphicAI>();
            if (ai == null) return false;
            Type b = body.GetType(), a = ai.GetType();
            return b == typeof(SatanMiniboss) && a == typeof(SatanMinibossAI) ||
                b == typeof(DolphLundrenSoldier) && a == typeof(DolfLundgrenAI) ||
                b == typeof(TankBig) && a == typeof(MammothTankPolyAI) && ((Tank)body).weapon is TankMassiveGun ||
                b == typeof(Mookopter) && a == typeof(MookopterPolyAI) && ((Tank)body).weapon is TankChaingun ||
                b == typeof(GoliathMech) && a == typeof(GoliathMechAI) ||
                b == typeof(MammothKopter) && a == typeof(MammothKopterAI);
        }
        internal static IEnumerable<Unit> Weapons(Unit body)
        {
            Mech mech = body as Mech;
            if (mech != null)
            {
                if (mech.weapon != null) yield return mech.weapon;
                if (mech.chainGun1 != null && mech.chainGun1.health > 0) yield return mech.chainGun1;
                if (mech.chainGun2 != null && mech.chainGun2.health > 0) yield return mech.chainGun2;
                if (mech.grenadeHatch != null) yield return mech.grenadeHatch;
            }
            Tank tank = body as Tank;
            if (tank != null && tank.weapon != null) yield return tank.weapon;
            MammothKopter mammoth = body as MammothKopter;
            if (mammoth != null && mammoth.mookLauncher != null) yield return mammoth.mookLauncher;
        }
        internal static bool Applicable(Unit body)
        {
            if (!Plugin.Enabled || body == null || !Exact(body) || Map.Instance == null || !Map.Instance.HasBeenSetup ||
                Map.isEditing || !body.enabled || !body.gameObject.activeInHierarchy || body.health <= 0 || body.playerNum >= 0 ||
                body.IsHero || body.beingControlledByTriggerAction || body.IsIncapacitated()) return false;
            PolymorphicAI ai = body.GetComponent<PolymorphicAI>();
            Mook mook = body as Mook;
            if (mook != null && (Number(mook, "blindTime") > 0f || Number(mook, "stunTime") > 0f)) return false;
            if (!(body is Mook) && Number(body, "stunTime") > 0f) return false;
            foreach (Unit weapon in Weapons(body))
                if (weapon.health <= 0 || weapon.playerNum >= 0 || weapon.beingControlledByTriggerAction) return false;
            return true;
        }
        private static bool Matching(Unit body, bool local)
        {
            PolymorphicAI ai = body.GetComponent<PolymorphicAI>();
            if (Owner(body) != Owner(ai) || local && (!body.IsMine || !ai.IsMine)) return false;
            foreach (Unit weapon in Weapons(body))
                if (Owner(body) != Owner(weapon) || local && !weapon.IsMine) return false;
            return true;
        }
        internal static bool Owns(Unit body) { return Matching(body, true); }
        internal static bool Sight(Unit body, Vector3 origin, Vector3 point)
        {
            Vector3 delta = point + Vector3.up * 8f - origin;
            foreach (RaycastHit hit in Physics.RaycastAll(origin, delta.normalized, Mathf.Max(0f, delta.magnitude - 4f), Map.groundLayer | Map.fragileLayer))
                if (!hit.collider.transform.IsChildOf(body.transform)) return false;
            return true;
        }
        internal static BossObservation Observe(Unit body)
        {
            if (currentMap != Map.Instance) { Reset(); currentMap = Map.Instance; }
            if (!Applicable(body) || !Matching(body, false)) { Observations.Remove(body); return null; }
            BossObservation memory;
            Observations.TryGetValue(body, out memory);
            if (memory != null && memory.Owner != body.Owner) { Observations.Remove(body); memory = null; }
            if (!Owns(body))
            {
                if (memory != null && Time.time - memory.ReceivedAt > 6f) { Observations.Remove(body); return null; }
                return memory;
            }
            if (!body.GetComponent<PolymorphicAI>().enabled) return memory; // Native intro/flame pause: retain the committed point, never sense.
            if (memory == null) { memory = new BossObservation(); memory.Owner = body.Owner; Observations.Add(body, memory); }
            float now = Time.time;
            if (now < memory.NextSense) return memory;
            memory.NextSense = now + 0.2f;
            PolymorphicAI ai = body.GetComponent<PolymorphicAI>();
            Vector3 origin = new Vector3(body.X, body.Y + (body is Mookopter ? 0f : body is Mech ? 32f : 20f), 0f);
            memory.Npc = Controller.PreferNativeNPC(ai, body, false, Vector2.zero);
            int selected = -1; float best = float.MaxValue; Vector3 point = Vector3.zero;
            if (!memory.Npc) for (int i = 0; i < 4; i++)
            {
                if (!HeroController.PlayerIsAlive(i) || !GameModeController.DoesPlayerNumDamage(body.playerNum, i) || HeroController.players[i].character.IsInStealthMode()) continue;
                Vector3 candidate = HeroController.GetPlayerPosition(i); candidate.z = 0f;
                Vector3 delta = candidate + Vector3.up * 8f - origin;
                if (Mathf.Abs(delta.x) > ai.sightRangeX || Mathf.Abs(delta.y) > ai.sightRangeY || !Sight(body, origin, candidate)) continue;
                float score = delta.magnitude * (i == memory.Player ? 0.85f : 1f);
                if (score < best) { selected = i; best = score; point = candidate; }
            }
            memory.Visible = selected >= 0;
            if (memory.Visible)
            {
                float elapsed = now - memory.SeenAt;
                memory.Velocity = selected == memory.Player && elapsed >= 0.05f && elapsed <= 0.6f ? Vector3.ClampMagnitude((point - memory.Position) / elapsed, 120f) : Vector3.zero;
                memory.Player = selected; memory.Position = point; memory.SeenAt = now;
                // Native CR666 bullets 160, massive gun 600, rockets 90; no projectile parameter changes.
                float speed = body is DolphLundrenSoldier ? 160f : body is TankBig ? 600f : 90f;
                Vector3 predicted = point + Vector3.ClampMagnitude(memory.Velocity * Mathf.Min(0.35f, best / speed), 24f);
                predicted.x = Mathf.Clamp(predicted.x, 1f, Map.Width * 16f - 1f); predicted.y = Mathf.Clamp(predicted.y, 1f, Map.Height * 16f - 1f);
                memory.Aim = Sight(body, origin, predicted) ? predicted : point; Selections++;
            }
            else
            {
                memory.Velocity = Vector3.zero;
                if (memory.Npc || !Plugin.Settings.RememberTargets || memory.Player < 0 || !HeroController.PlayerIsAlive(memory.Player) || now - memory.SeenAt > 6f)
                    memory.Player = -1;
                memory.Aim = memory.Player < 0 ? new Vector3(body.X, body.Y, 0f) : memory.Position;
            }
            return memory;
        }
        private static void Publish(Unit body, BossObservation memory, bool commit)
        {
            if (Connect.IsOffline || body.Nid.GameObjectID == 0) return;
            if (!commit && Time.time < memory.NextSend) return;
            memory.NextSend = Time.time + 0.2f;
            global::Networking.Networking.RPC<NID, int, Vector3, bool>(PID.TargetOthers, new RpcSignature<NID, int, Vector3, bool>(Receive),
                body.Nid, memory.Npc ? -2 : memory.Player, commit ? memory.Committed : memory.Aim, commit, false);
        }
        [AllowedRPC]
        private static void Receive(NID id, int player, Vector3 aim, bool commit)
        {
            if (Connect.IsOffline || id.Sid != Connect.SessionID || RPCController.CurrentRPC == null) return;
            Unit body = Registry.GetObject(id) as Unit;
            if (body != null && body.Nid == id) Accept(body, RPCController.LastSender, player, aim, commit);
        }
        internal static bool Accept(Unit body, PID sender, int player, Vector3 aim, bool commit)
        {
            if (!Applicable(body) || !Matching(body, false)) { Observations.Remove(body); return false; }
            if (body.IsMine || sender == null || sender != Owner(body) || player < -2 || player > 3 ||
                float.IsNaN(aim.x) || float.IsNaN(aim.y) || float.IsInfinity(aim.x) || float.IsInfinity(aim.y) || aim.z != 0f ||
                aim.x < 0f || aim.y < 0f || aim.x > Map.Width * 16f || aim.y > Map.Height * 16f) return false;
            if (currentMap != Map.Instance) { Reset(); currentMap = Map.Instance; }
            BossObservation memory;
            if (!Observations.TryGetValue(body, out memory)) { memory = new BossObservation(); Observations.Add(body, memory); }
            memory.Owner = body.Owner; memory.Player = player < 0 ? -1 : player; memory.Npc = player == -2;
            memory.Aim = aim; memory.ReceivedAt = Time.time;
            if (commit) { memory.Committed = aim; memory.HasCommit = true; if (player >= 0) body.SetTargetPlayerNum(player, aim); }
            Received++; return true;
        }
        internal static void Update(PolymorphicAI ai)
        {
            Unit body = ai.GetComponent<Unit>();
            if (body == null || !Exact(body)) return;
            BossObservation memory = Observe(body);
            if (memory != null && Owns(body)) Publish(body, memory, false);
        }
        // Check every collidable body/attachment, including disabled physical kopter colliders.
        private static Bounds BodyBounds(Unit body)
        {
            Bounds bounds = new Bounds(new Vector3(body.X, body.Y + body.height / 2f, 0f), new Vector3(body.width * 2f, body.height, 2f));
            foreach (BoxCollider collider in body.GetComponentsInChildren<BoxCollider>(true))
            {
                if (collider.isTrigger) continue;
                Vector3 scale = collider.transform.lossyScale;
                bounds.Encapsulate(new Bounds(collider.transform.TransformPoint(collider.center), new Vector3(Mathf.Abs(collider.size.x * scale.x), Mathf.Abs(collider.size.y * scale.y), 2f)));
            }
            bounds.extents = new Vector3(bounds.extents.x, bounds.extents.y, 1f); return bounds;
        }
        private static bool Segment(Unit body, Vector3 from, Vector3 to, Bounds bounds)
        {
            Vector3 offset = bounds.center - new Vector3(body.X, body.Y, 0f);
            int steps = Mathf.Max(1, Mathf.CeilToInt(Vector3.Distance(from, to) / 8f));
            for (int i = 0; i <= steps; i++)
            {
                Vector3 center = Vector3.Lerp(from, to, (float)i / steps) + offset;
                if (center.x - bounds.extents.x < 0f || center.y - bounds.extents.y < 0f || center.x + bounds.extents.x > Map.Width * 16f || center.y + bounds.extents.y > Map.Height * 16f) return false;
                foreach (Collider hit in Physics.OverlapBox(center + Vector3.up * 2f, new Vector3(Mathf.Max(1f, bounds.extents.x - 1f), Mathf.Max(1f, bounds.extents.y - 2f), 1f), Quaternion.identity, Map.groundLayer | Map.fragileLayer))
                    if (!hit.transform.IsChildOf(body.transform)) return false;
            }
            return true;
        }
        private static bool Supported(Unit body, Vector3 point, Bounds bounds)
        {
            float offset = bounds.center.x - body.X;
            for (float x = -bounds.extents.x; x <= bounds.extents.x + 1f; x += 8f)
                if (!Physics.Raycast(point + new Vector3(offset + x, 6f, 0f), Vector3.down, 18f, Map.groundLayer)) return false;
            return Physics.Raycast(point + new Vector3(offset + bounds.extents.x, 6f, 0f), Vector3.down, 18f, Map.groundLayer);
        }
        private static bool JumpEnvelope(Unit body, Vector3 start, Vector3 end, Bounds bounds)
        {
            // Native jump/hover can travel diagonally before reaching the top. Check the interior,
            // not just three edges of an up/across/down route, including off-center attachments.
            float top = Mathf.Max(start.y, end.y) + 96f;
            for (float y = Mathf.Min(start.y, end.y); y <= top; y += 8f)
                if (!Segment(body, new Vector3(start.x, y, 0f), new Vector3(end.x, y, 0f), bounds)) return false;
            return Supported(body, end, bounds);
        }
        internal static bool Motion(PolymorphicAI ai, BossObservation memory)
        {
            Unit body = ai.GetComponent<Unit>(); Vector3 start = new Vector3(body.X, body.Y, 0f), end = memory.Aim;
            Bounds bounds = BodyBounds(body);
            if (ai is SatanMinibossAI && ai.GetSatanPattern() == SatanBossPattern.Jumping)
            {
                return JumpEnvelope(body, start, end, bounds);
            }
            if (ai is GoliathMechAI && (MechAIPattern)Field(ai, "patternType") == MechAIPattern.Stomping)
            {
                // Match the native branch using the point this cycle will commit. Neither
                // top defence nor the arena-edge bombardment actually performs a hover.
                if (Mathf.Abs(end.x - body.X) < 48f && Mathf.Abs(end.y - body.Y - 48f) < 48f) return true;
                int distance = ((GoliathMechAI)ai).UnitIsAboveHalfHealth() ? 8 : 16;
                int delta = Mathf.Clamp(Map.GetCollumn(end.x) - body.collumn, -distance, distance);
                int min = (int)Field(ai, "minCollumn"), max = (int)Field(ai, "maxCollumn");
                if (!(delta > 0 || body.collumn > min + 2) || !(delta < 0 || body.collumn < max - 2)) return true;
                int column = Mathf.Clamp(body.collumn + delta, min, max);
                end = new Vector3(Map.GetBlocksX(column), Number(ai, "originalGroundHeight"), 0f);
                return JumpEnvelope(body, start, end, bounds);
            }
            if (ai is MookopterPolyAI)
            {
                KopterPattern next = (KopterPattern)((int)(KopterPattern)Field(ai, "kopterPattern") + 1);
                bool left = next == KopterPattern.MovingLeft || next == KopterPattern.DescendingLeft || next == KopterPattern.AscendingLeft;
                if (next == KopterPattern.Hovering || next == KopterPattern.Restart) return true;
                string side = left ? "xMin" : "xMax";
                end.x = Number(ai, side);
                end.y = next == KopterPattern.MovingLeft ? start.y : next == KopterPattern.MovingRight ? Number(ai, "xMaxMaxHeight") * 0.75f : Mathf.Clamp(memory.Aim.y, Number(ai, side + "MinHeight"), Number(ai, side + "MaxHeight"));
                Vector3 turn = new Vector3(start.x, end.y, 0f);
                if (!Segment(body, start, turn, bounds) || !Segment(body, turn, end, bounds)) return false;
                if (next == KopterPattern.AscendingLeft || next == KopterPattern.AscendingRight)
                    return Segment(body, end, new Vector3(end.x, Number(ai, side + "MaxHeight") * 0.75f, 0f), bounds);
            }
            return true;
        }
        internal static bool Think(PolymorphicAI ai)
        {
            Unit body = ai.GetComponent<Unit>(); BossObservation memory = Observe(body);
            if (memory == null || !Owns(body) || memory.Npc) return true;
            if (!ai.enabled) return true;
            if (body is TankBig && ((Tank)body).weapon.IsFiring()) { ai.AddAction(EnemyActionType.Wait, 0.3f); return false; }
            if (memory.Player < 0) { ai.AddAction(EnemyActionType.Wait, 0.3f); return false; }
            bool motion = Motion(ai, memory);
            if (!motion && (!(ai is GoliathMechAI) || !memory.Visible)) { ai.AddAction(EnemyActionType.Wait, 0.3f); return false; }
            memory.Committed = memory.Aim; memory.HasCommit = true;
            AccessTools.Field(typeof(PolymorphicAI), "seenEnemyNum").SetValue(ai, memory.Player);
            body.SetTargetPlayerNum(memory.Player, memory.Committed); Publish(body, memory, true); Cycles++;
            if (!motion)
            {
                // Native Bombardment's stationary attack and full recovery. Leave the
                // pending Stomping pattern/index intact so a later safe route can run.
                bool turn = (bool)AccessTools.Method(typeof(GoliathMechAI), "CheckThinkTurnaround", null, null).Invoke(ai, null);
                ai.AddAction(EnemyActionType.Wait, turn ? 1.3f : 0.5f);
                ai.AddAction(EnemyActionType.Fire, ai.attackTime);
                ai.AddAction(EnemyActionType.Wait, 1.5f);
                return false;
            }
            if (ai is MammothTankPolyAI)
            {
                Tank tank = (Tank)body;
                ai.AddAction(EnemyActionType.FacePoint, Map.GetGridPoint(memory.Committed));
                if (memory.Visible && Mathf.Abs(memory.Position.x - body.X) < 90f && Mathf.Abs(memory.Position.y - body.Y - 20f) < 120f)
                { ai.AddAction(EnemyActionType.UseSpecial, 0.0334f); ai.AddAction(EnemyActionType.Wait, 2.5f); }
                else { ai.AddAction(EnemyActionType.Fire, 3f); ai.AddAction(EnemyActionType.Wait, 1.5f); }
                return false;
            }
            if (ai is DolfLundgrenAI && memory.Visible && Mathf.Abs(memory.Position.x - body.X) >= 36f && Mathf.Abs(memory.Position.y - body.Y) < 30f && Number(ai, "damagePressure") <= 32f)
            {
                int direction = memory.Committed.x > body.X ? 1 : -1;
                if ((int)Mathf.Sign(body.transform.localScale.x) != direction) ai.AddAction(EnemyActionType.FacePoint, Map.GetGridPoint(memory.Committed));
                bool special = Mathf.Abs(memory.Position.x - body.X) < 90f && Sight(body, new Vector3(body.X, body.Y + 6f, 0f), memory.Position);
                ai.AddAction(special ? EnemyActionType.UseSpecial : EnemyActionType.Fire, special ? Number(ai, "specialActionTime") : ai.attackTime);
                ai.AddAction(EnemyActionType.Wait, UnityEngine.Random.Range(0.75f, 1.5f)); return false;
            }
            return true;
        }
        internal static bool Look(PolymorphicAI ai)
        {
            Unit body = ai.GetComponent<Unit>(); BossObservation memory = Observe(body);
            if (memory == null || !Owns(body) || memory.Npc) return true;
            if (!memory.Visible) return false;
            if (ai is MammothTankPolyAI) return true; // Native first-sight wait is 6.7 seconds.
            ai.FullyAlert(memory.Position.x, memory.Position.y, memory.Player); return false;
        }
        internal static Vector3 Point(int player, PolymorphicAI ai)
        {
            Unit body = ai.GetComponent<Unit>(); BossObservation memory = Observe(body);
            if (memory == null || memory.Npc) return HeroController.GetPlayerPos(player);
            return memory.HasCommit ? memory.Committed : memory.Aim;
        }
        internal static Vector3 Position(int player, PolymorphicAI ai)
        {
            BossObservation memory = Observe(ai.GetComponent<Unit>());
            return memory == null || memory.Npc ? HeroController.GetPlayerPosition(player) : memory.HasCommit ? memory.Committed : memory.Aim;
        }
        internal static Vector3 NearestPoint(float x, float y, PolymorphicAI ai)
        {
            BossObservation memory = Observe(ai.GetComponent<Unit>());
            return memory == null || memory.Npc ? HeroController.GetNearestPlayerPos(x, y) : memory.HasCommit ? memory.Committed : memory.Aim;
        }
        internal static Vector3 RandomPoint(PolymorphicAI ai)
        {
            BossObservation memory = Observe(ai.GetComponent<Unit>());
            return memory == null || memory.Npc ? HeroController.GetRandomPlayerPos() : memory.HasCommit ? memory.Committed : memory.Aim;
        }
        internal static Vector3 SatanPoint(PolymorphicAI ai)
        {
            BossObservation memory = Observe(ai.GetComponent<Unit>());
            return memory == null || memory.Npc ? ai.GetTargetPosition() : memory.HasCommit ? memory.Committed : memory.Aim;
        }
        internal static void PointRef(int player, ref float x, ref float y, PolymorphicAI ai)
        {
            BossObservation memory = Observe(ai.GetComponent<Unit>());
            if (memory == null || memory.Npc) { HeroController.GetPlayerPos(player, ref x, ref y); return; }
            Vector3 p = memory.HasCommit ? memory.Committed : memory.Aim; x = p.x; y = p.y;
        }
        internal static int Nearest(float x, float y, float rx, float ry, PolymorphicAI ai)
        {
            BossObservation memory = Observe(ai.GetComponent<Unit>());
            return memory == null || memory.Npc ? HeroController.GetNearestPlayer(x, y, rx, ry) : memory.Player;
        }
        internal static int ActualNearest(float x, float y, float rx, float ry, PolymorphicAI ai)
        {
            BossObservation memory = Observe(ai.GetComponent<Unit>());
            return memory == null || memory.Npc ? HeroController.GetActualNearestPlayer(x, y, rx, ry) : memory.Player;
        }
        internal static bool NearestRef(float x, float y, float rx, float ry, ref int player, PolymorphicAI ai)
        {
            BossObservation memory = Observe(ai.GetComponent<Unit>());
            if (memory == null || memory.Npc) return HeroController.GetNearestPlayer(x, y, rx, ry, ref player);
            player = memory.Player; return player >= 0;
        }
        internal static bool Nearby(float x, float y, float rx, float ry, PolymorphicAI ai)
        {
            BossObservation memory = Observe(ai.GetComponent<Unit>());
            return memory == null || memory.Npc ? HeroController.IsPlayerNearby(x, y, rx, ry) : memory.Visible && Mathf.Abs(memory.Position.x - x) < rx && Mathf.Abs(memory.Position.y - y) < ry;
        }
        internal static void WeaponPoint(int player, ref float x, ref float y, TankWeapon weapon)
        {
            Unit body = weapon.GetComponentInParent<Tank>(); BossObservation memory = body == null || !Exact(body) ? null : Observe(body);
            MammothKopter mammoth = body as MammothKopter;
            if (mammoth != null && (!mammoth.grenadeLauncherEnabled || mammoth.mamKopAI.mamKopterThinkState != MammothKopterAI.ThinkState.ShootAtBottom)) memory = null;
            if (memory == null || memory.Npc) { HeroController.GetPlayerPos(player, ref x, ref y); return; }
            Vector3 p = memory.HasCommit ? memory.Committed : memory.Aim; x = p.x; y = p.y;
        }
        internal static void Launcher(HelicopterMookLauncher weapon)
        {
            MammothKopter body = weapon.GetComponentInParent<MammothKopter>();
            if (body == null || !body.grenadeLauncherEnabled || body.mamKopAI.mamKopterThinkState != MammothKopterAI.ThinkState.ShootAtBottom) return;
            BossObservation memory = Observe(body);
            if (memory == null || memory.Npc || !Owns(body)) return;
            weapon.SetTargetPlayerNum(memory.Player, memory.Aim);
        }
        internal static void Massive(TankMassiveGun weapon)
        {
            TankBig body = weapon.GetComponentInParent<TankBig>();
            if (body == null) return;
            BossObservation memory = Observe(body);
            int player = (int)Field(weapon, "targetPlayerNum");
            if (memory == null || memory.Npc)
            { if (player >= 0) weapon.SetTargetPlayerNum(player, HeroController.GetPlayerPos(player)); return; }
            weapon.SetTargetPlayerNum(memory.Player, memory.HasCommit ? memory.Committed : memory.Aim);
        }
        internal static void Rocket(PID destination, bool immediate, bool ignoreSession, bool delay, RpcSignature<float, float, int> method, float x, float y, int player, MechRocketBattery weapon)
        {
            GoliathMech body = weapon.GetComponentInParent<GoliathMech>(); BossObservation memory = body == null ? null : Observe(body);
            if (memory != null && !memory.Npc && memory.Player >= 0 && Owns(body))
            { Vector3 p = memory.HasCommit ? memory.Committed : memory.Aim; x = p.x; y = p.y + 8f; player = -1; }
            global::Networking.Networking.RPC<float, float, int>(destination, immediate, ignoreSession, delay, method, x, y, player);
        }
        internal static void Forget(NetworkObject value) { Unit body = value as Unit; if (body != null) Observations.Remove(body); }
        internal static void Reset() { Observations.Clear(); currentMap = null; }
    }

    [HarmonyPatch(typeof(PolymorphicAI), "Update")]
    internal static class ObserveBosses { private static void Postfix(PolymorphicAI __instance) { BossController.Update(__instance); } }
    [HarmonyPatch]
    internal static class BossCycle
    {
        private static IEnumerable<MethodBase> TargetMethods()
        {
            return new MethodBase[] {
                AccessTools.Method(typeof(SatanMinibossAI), "DoAlertedThink", null, null),
                AccessTools.Method(typeof(DolfLundgrenAI), "DoAlertedThink", null, null),
                AccessTools.Method(typeof(MammothTankPolyAI), "DoAlertedThink", null, null),
                AccessTools.Method(typeof(MookopterPolyAI), "DoAlertedThink", null, null),
                AccessTools.Method(typeof(GoliathMechAI), "DoAlertedThink", null, null)
            };
        }
        private static bool Prefix(PolymorphicAI __instance) { return BossController.Think(__instance); }
    }
    [HarmonyPatch]
    internal static class BossDiscovery
    {
        private static IEnumerable<MethodBase> TargetMethods()
        {
            return new MethodBase[] {
                AccessTools.Method(typeof(DolfLundgrenAI), "LookForEnemy", null, null),
                AccessTools.Method(typeof(MammothTankPolyAI), "LookForEnemy", null, null),
                AccessTools.Method(typeof(GoliathMechAI), "LookForEnemy", null, null)
            };
        }
        private static bool Prefix(PolymorphicAI __instance) { return BossController.Look(__instance); }
    }
    [HarmonyPatch]
    internal static class BossQueries
    {
        private static readonly Dictionary<MethodInfo, string> Replacements = new Dictionary<MethodInfo, string>
        {
            { AccessTools.Method(typeof(HeroController), "GetPlayerPos", new[] { typeof(int) }, null), "Point" },
            { AccessTools.Method(typeof(HeroController), "GetPlayerPosition", new[] { typeof(int) }, null), "Position" },
            { AccessTools.Method(typeof(HeroController), "GetPlayerPos", new[] { typeof(int), typeof(float).MakeByRefType(), typeof(float).MakeByRefType() }, null), "PointRef" },
            { AccessTools.Method(typeof(HeroController), "GetNearestPlayerPos", new[] { typeof(float), typeof(float) }, null), "NearestPoint" },
            { AccessTools.Method(typeof(HeroController), "GetRandomPlayerPos", Type.EmptyTypes, null), "RandomPoint" },
            { AccessTools.Method(typeof(HeroController), "GetNearestPlayer", new[] { typeof(float), typeof(float), typeof(float), typeof(float) }, null), "Nearest" },
            { AccessTools.Method(typeof(HeroController), "GetActualNearestPlayer", new[] { typeof(float), typeof(float), typeof(float), typeof(float) }, null), "ActualNearest" },
            { AccessTools.Method(typeof(HeroController), "GetNearestPlayer", new[] { typeof(float), typeof(float), typeof(float), typeof(float), typeof(int).MakeByRefType() }, null), "NearestRef" },
            { AccessTools.Method(typeof(HeroController), "IsPlayerNearby", new[] { typeof(float), typeof(float), typeof(float), typeof(float) }, null), "Nearby" }
        };
        private static IEnumerable<MethodBase> TargetMethods()
        {
            foreach (Type type in new[] { typeof(SatanMinibossAI), typeof(DolfLundgrenAI), typeof(MookopterPolyAI), typeof(GoliathMechAI) })
                foreach (string name in new[] { "DoAlertedThink", "StartBombardment", "IsPlayerTooClose", "IsPlayerOnTopOfMe", "CheckThinkTurnaround" })
                { MethodInfo method = type.GetMethod(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly); if (method != null) yield return method; }
        }
        private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            foreach (CodeInstruction instruction in instructions)
            {
                MethodInfo call = instruction.operand as MethodInfo; string name = null;
                if (call != null) Replacements.TryGetValue(call, out name);
                if (name == null) yield return instruction;
                else { yield return new CodeInstruction(OpCodes.Ldarg_0, null).MoveLabelsFrom(instruction); yield return new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(BossController), name, null, null)); }
            }
        }
    }
    [HarmonyPatch]
    internal static class BossWeapons
    {
        private static IEnumerable<MethodBase> TargetMethods()
        {
            return new MethodBase[] {
                AccessTools.Method(typeof(TankMassiveGun), "RunFiring", null, null),
                AccessTools.Method(typeof(HelicopterMookLauncher), "FireWeapon", null, null),
                AccessTools.Method(typeof(MechRocketBattery), "FireWeapon", null, null)
            };
        }
        private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions, MethodBase original)
        {
            foreach (CodeInstruction instruction in instructions)
            {
                MethodInfo call = instruction.operand as MethodInfo;
                string name = call != null && call.DeclaringType == typeof(HeroController) && call.Name == "GetPlayerPos" ? "WeaponPoint" :
                    call != null && original.DeclaringType == typeof(MechRocketBattery) && call.DeclaringType == typeof(global::Networking.Networking) && call.Name == "RPC" ? "Rocket" : null;
                if (name == null) yield return instruction;
                else { yield return new CodeInstruction(OpCodes.Ldarg_0, null).MoveLabelsFrom(instruction); yield return new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(BossController), name, null, null)); }
            }
        }
    }
    [HarmonyPatch(typeof(HelicopterMookLauncher), "FireWeapon")]
    internal static class EnabledBossLauncher { private static void Prefix(HelicopterMookLauncher __instance) { BossController.Launcher(__instance); } }
    [HarmonyPatch]
    internal static class SatanObservedTarget
    {
        private static IEnumerable<MethodBase> TargetMethods()
        {
            return new MethodBase[] {
                AccessTools.Method(typeof(SatanMiniboss), "StartFlameFiring", null, null),
                AccessTools.Method(typeof(SatanMiniboss), "AnimateSpecial2", null, null),
                AccessTools.Method(typeof(SatanMiniboss), "PressSpecial2", null, null),
                AccessTools.Method(typeof(SatanMiniboss), "AnimateForceField", null, null)
            };
        }
        private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            foreach (CodeInstruction instruction in instructions)
            {
                MethodInfo call = instruction.operand as MethodInfo;
                if (call != null && call.Name == "GetTargetPosition" && call.DeclaringType == typeof(PolymorphicAI))
                    yield return new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(BossController), "SatanPoint", null, null)).MoveLabelsFrom(instruction);
                else yield return instruction;
            }
        }
    }
    [HarmonyPatch]
    internal static class MassiveBossAim
    {
        private static IEnumerable<MethodBase> TargetMethods()
        {
            return new MethodBase[] {
                AccessTools.Method(typeof(TankMassiveGun), "ChangeMinigunFrame", null, null),
                AccessTools.Method(typeof(TankMassiveGun), "FireWeapon", null, null)
            };
        }
        private static void Prefix(TankMassiveGun __instance) { BossController.Massive(__instance); }
    }
    [HarmonyPatch(typeof(DolfLundgrenAI), "IsPlayerThisWay")]
    internal static class BossFacing
    {
        private static bool Prefix(DolfLundgrenAI __instance, int direction, ref bool __result)
        {
            BossObservation memory = BossController.Observe(__instance.GetComponent<Unit>());
            if (memory == null || memory.Npc) return true;
            __result = memory.Player >= 0 && (memory.Aim.x - __instance.transform.position.x) * direction >= 0f; return false;
        }
    }
}
