using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using HarmonyLib;
using Networking;
using UnityEngine;

namespace CustomMapMultiplayer
{
    internal static partial class HarmonyDiagnostics
    {
        private sealed class WorldProjectileSpawn
        {
            internal NID Id, Actor;
            internal string Prefab, Type;
            internal int Owner, Player, Seed;
            internal float X, Y, VX, VY, Offset;
        }

        private static readonly Dictionary<NID, WorldProjectileSpawn> WorldProjectileSpawns =
            new Dictionary<NID, WorldProjectileSpawn>();
        private static readonly Dictionary<NID, WorldProjectileSpawn> WorldPendingProjectiles =
            new Dictionary<NID, WorldProjectileSpawn>();
        private static readonly Dictionary<string, Projectile> WorldProjectilePrefabs =
            new Dictionary<string, Projectile>();
        private static readonly Dictionary<Type, FieldInfo[]> WorldProjectileFields =
            new Dictionary<Type, FieldInfo[]>();
        private static readonly HashSet<string> WorldMissingProjectilePrefabs = new HashSet<string>();
        private static readonly FieldInfo WorldRocketTrail = AccessTools.Field(typeof(Rocket), "trailParticles");
        private static readonly FieldInfo WorldProjectileDelta = AccessTools.Field(typeof(Projectile), "t");
        private static readonly FieldInfo WorldShellDip = AccessTools.Field(typeof(HeavyShell), "dipCounter");
        private static readonly Dictionary<Type, MethodInfo> WorldProjectileEffectMethods = new Dictionary<Type, MethodInfo>();
        private static readonly FieldInfo[] WorldRandomStateFields = GetWorldRandomStateFields();
        private static readonly bool WorldRandomStateSupported = SupportsWorldRandomState();
        private static readonly Dictionary<Projectile, UnityEngine.Random.State> WorldProjectileVisualRandomStates =
            new Dictionary<Projectile, UnityEngine.Random.State>();
        private static int _worldProjectileEffectsDepth;
        private static readonly Type[] WorldProjectileEffectParameters =
        {
            typeof(bool), typeof(float), typeof(float), typeof(bool), typeof(Vector3), typeof(Vector3)
        };
        private static BroforceObject _worldDamageSource;
        private static bool _worldHitMessagesPending;
        private static int _worldHitSequence;

        private struct WorldDamageContext
        {
            internal BroforceObject Previous;
            internal bool VisualReplay;
            internal bool EffectContext;
            internal Unit PreviousStatus;
            internal bool StatusContext;
        }

        private static void PatchWorldProjectiles()
        {
            PatchWorldMethod(AccessTools.Method(typeof(PredabroSpear), "Death", Type.EmptyTypes, null), "WorldSpearDeathPrefix", null);
            PatchWorldMethod(typeof(Projectile).GetMethod("RunLife", WorldFields), "WorldSpearRunLifePrefix", null);
            PatchWorldMethod(AccessTools.Method(typeof(ProjectileController), "SpawnProjectileOverNetwork", null, null), "WorldSpearSyncSpawnPrefix", null);
            var syncUpdate = AccessTools.Method(typeof(SyncController), "Update", null, null);
            _harmony.Patch(syncUpdate,
                new HarmonyMethod(typeof(HarmonyDiagnostics).GetMethod("WorldSpearSyncUpdatePrefix", BindingFlags.NonPublic | BindingFlags.Static)),
                new HarmonyMethod(typeof(HarmonyDiagnostics).GetMethod("WorldSpearSyncUpdatePostfix", BindingFlags.NonPublic | BindingFlags.Static)),
                null,
                new HarmonyMethod(typeof(HarmonyDiagnostics).GetMethod("WorldSpearSyncUpdateFinalizer", BindingFlags.NonPublic | BindingFlags.Static)));
            foreach (var method in typeof(ProjectileController).GetMethods(BindingFlags.Public | BindingFlags.Static))
                if (method.Name == "SpawnProjectileLocally")
                    PatchWorldMethod(method, "WorldSpawnProjectilePrefix", null);
            foreach (var type in new[] { typeof(Projectile), typeof(Rocket), typeof(HeavyShell), typeof(PlasmaBullet) })
                _harmony.CreateReversePatcher(type.GetMethod("MakeEffects", WorldFields | BindingFlags.DeclaredOnly),
                    new HarmonyMethod(typeof(HarmonyDiagnostics).GetMethod(type == typeof(PlasmaBullet) ? "WorldBasePlasmaEffects" :
                        type == typeof(HeavyShell) ? "WorldBaseHeavyShellEffects" :
                        type == typeof(Rocket) ? "WorldBaseRocketEffects" : "WorldBaseProjectileEffects", BindingFlags.NonPublic | BindingFlags.Static)))
                    .Patch(HarmonyReversePatchType.Original);
            _harmony.CreateReversePatcher(typeof(Rocket).GetMethod("RunSmokeTrail", WorldFields),
                new HarmonyMethod(typeof(HarmonyDiagnostics).GetMethod("WorldBaseRocketSmoke", BindingFlags.NonPublic | BindingFlags.Static)))
                .Patch(HarmonyReversePatchType.Original);
            _harmony.CreateReversePatcher(typeof(HeavyShell).GetMethod("RunPredictionTrail", WorldFields),
                new HarmonyMethod(typeof(HarmonyDiagnostics).GetMethod("WorldBaseHeavyShellPrediction", BindingFlags.NonPublic | BindingFlags.Static)))
                .Patch(HarmonyReversePatchType.Original);
            foreach (var method in typeof(Map).GetMethods(BindingFlags.Public | BindingFlags.Static))
                if (method.Name == "HeadShotUnit" || method.Name == "KnockAndDamageUnit" && method.GetParameters().Length == 11)
                    PatchWorldMethod(method, "WorldProjectileHitPrefix", null);
            PatchWorldMethod(typeof(MapController).GetMethod("Damage_Networked", BindingFlags.Public | BindingFlags.Static), "WorldProjectileBlockHitPrefix", null);
            foreach (var method in typeof(Map).GetMethods(BindingFlags.Public | BindingFlags.Static))
                if (method.Name == "DamageDoodads" || method.Name == "BurnUnitsAround_Local" ||
                    method.Name == "HitProjectiles" || method.Name == "DamageProjectile")
                    _harmony.Patch(method, new HarmonyMethod(AccessTools.Method(typeof(HarmonyDiagnostics),
                        method.Name == "DamageDoodads" ? "WorldAreaDamageSenderPrefix" :
                        method.Name == "BurnUnitsAround_Local" ? "WorldAreaDamageFiredPrefix" :
                        method.Name == "DamageProjectile" ? "WorldAreaDamageProjectilePrefix" : "WorldAreaDamagePrefix", null, null)),
                        null, null, new HarmonyMethod(AccessTools.Method(typeof(HarmonyDiagnostics), "WorldAreaDamageFinalizer", null, null)));
            PatchWorldMethod(AccessTools.Method(typeof(ProjectileController), "SpawnGrenadeLocally", null, null), "WorldSpawnGrenadePrefix", null);
            _harmony.Patch(AccessTools.Method(typeof(ProjectileController), "SpawnGrenadeOverNetwork", null, null),
                new HarmonyMethod(AccessTools.Method(typeof(HarmonyDiagnostics), "WorldNetworkGrenadeSpawnPrefix", null, null)), null, null,
                new HarmonyMethod(AccessTools.Method(typeof(HarmonyDiagnostics), "WorldNetworkGrenadeSpawnFinalizer", null, null)));
            PatchWorldCarriers();
        }

        private static void PatchWorldDamageContext(MethodBase method, string prefix)
        {
            var postfix = prefix == "WorldProjectileEffectsPrefix" ? new HarmonyMethod(typeof(HarmonyDiagnostics).GetMethod(
                "WorldProjectileEffectsPostfix", BindingFlags.NonPublic | BindingFlags.Static)) : null;
            var transpiler = prefix == "WorldProjectileEffectsPrefix" &&
                (method.DeclaringType == typeof(Rocket) || method.DeclaringType == typeof(HeavyShell) ||
                 method.DeclaringType == typeof(PlasmaBullet) || method.DeclaringType == typeof(SachelPack) ||
                 method.DeclaringType == typeof(SachelPackTurkey))
                ? new HarmonyMethod(typeof(HarmonyDiagnostics).GetMethod("WorldProjectileVisualRandomTranspiler", BindingFlags.NonPublic | BindingFlags.Static))
                : null;
            _harmony.Patch(method, new HarmonyMethod(typeof(HarmonyDiagnostics).GetMethod(prefix, BindingFlags.NonPublic | BindingFlags.Static)), postfix, transpiler,
                new HarmonyMethod(typeof(HarmonyDiagnostics).GetMethod("WorldProjectileContextFinalizer", BindingFlags.NonPublic | BindingFlags.Static)));
        }

        private static IEnumerable<CodeInstruction> WorldProjectileVisualRandomTranspiler(IEnumerable<CodeInstruction> instructions, MethodBase __originalMethod)
        {
            var capture = typeof(HarmonyDiagnostics).GetMethod("CaptureWorldProjectileVisualRandomState",
                BindingFlags.NonPublic | BindingFlags.Static);
            var shell = __originalMethod.DeclaringType == typeof(HeavyShell);
            var plasma = __originalMethod.DeclaringType == typeof(PlasmaBullet);
            var satchel = __originalMethod.DeclaringType == typeof(SachelPack);
            var turkey = __originalMethod.DeclaringType == typeof(SachelPackTurkey);
            var matches = 0;
            foreach (var instruction in instructions)
            {
                var called = instruction.operand as MethodInfo;
                if (called != null && (satchel || plasma ? called.DeclaringType == typeof(EffectsController) && called.Name == "CreateShrapnel" :
                    turkey ? called.DeclaringType == typeof(EffectsController) && called.Name == "CreatePlumes" :
                    shell ? called.DeclaringType == typeof(Map) && called.Name == "ShakeTrees" :
                    called.DeclaringType == typeof(EffectsController) &&
                    (called.Name == "CreateSparkShower" || called.Name == "CreateHugeExplosion" || called.Name == "CreateMassiveExplosion")))
                {
                    yield return new CodeInstruction(System.Reflection.Emit.OpCodes.Ldarg_0, null);
                    yield return new CodeInstruction(System.Reflection.Emit.OpCodes.Call, capture);
                    matches++;
                }
                yield return instruction;
            }
            if (matches != (shell || plasma || satchel || turkey ? 1 : 3))
                throw new InvalidOperationException("Could not locate every " + __originalMethod.DeclaringType.Name + ".MakeEffects visual branch.");
        }

        private static FieldInfo[] GetWorldRandomStateFields()
        {
            var fields = typeof(UnityEngine.Random.State).GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            Array.Sort(fields, (left, right) => string.CompareOrdinal(left.Name, right.Name));
            return fields;
        }

        private static bool SupportsWorldRandomState()
        {
            if (WorldRandomStateFields.Length == 0) return false;
            foreach (var field in WorldRandomStateFields)
                if (field.FieldType != typeof(int)) return false;
            return true;
        }

        private static void CaptureWorldProjectileVisualRandomState(Projectile projectile)
        {
            if (WorldRandomStateSupported && _worldDamageSource == projectile && _worldProjectileEffectsDepth > 0)
                WorldProjectileVisualRandomStates[projectile] = UnityEngine.Random.state;
        }

        private static void WriteWorldRandomState(BinaryWriter writer, UnityEngine.Random.State state)
        {
            object boxed = state;
            writer.Write((byte)WorldRandomStateFields.Length);
            foreach (var field in WorldRandomStateFields) writer.Write((int)field.GetValue(boxed));
        }

        private static bool TryReadWorldRandomState(BinaryReader reader, out UnityEngine.Random.State state)
        {
            state = default(UnityEngine.Random.State);
            var count = reader.ReadByte();
            if (!WorldRandomStateSupported || count != WorldRandomStateFields.Length) return false;
            object boxed = state;
            foreach (var field in WorldRandomStateFields) field.SetValue(boxed, reader.ReadInt32());
            state = (UnityEngine.Random.State)boxed;
            return true;
        }

        private static void WorldProjectileFirePrefix(Projectile __instance, out WorldDamageContext __state)
        {
            BeginWorldProjectileContext(__instance, out __state);
        }

        private static bool BeginWorldProjectileContext(Projectile projectile, out WorldDamageContext previous)
        {
            previous = new WorldDamageContext { Previous = _worldDamageSource };
            var tracked = _worldEpoch != 0 && IsWorldSession() && IsWorldObject(projectile);
            _worldDamageSource = tracked && projectile.IsMine ? projectile : null;
            return tracked;
        }

        private static void WorldProjectileContextFinalizer(WorldDamageContext __state)
        {
            _worldDamageSource = __state.Previous;
            if (__state.StatusContext) _worldStatusSource = __state.PreviousStatus;
            if (__state.VisualReplay) _worldApplying--;
            if (__state.EffectContext && --_worldProjectileEffectsDepth == 0)
                WorldProjectileVisualRandomStates.Clear();
        }

        private static bool WorldProjectileHitPrefix(MonoBehaviour __0, Unit __1, int __2, DamageType __3,
            float __4, float __5, int __6, bool __7, float __8, float __9, MethodBase __originalMethod)
        {
            if (_worldApplying == 0 && IsWorldRemoteHeroAttack(__0, __1)) return false;
            var source = GetWorldHitSource(__0);
            if (_worldApplying != 0 || source == null || __1 == null || __1.IsMine ||
                _worldDamageSource == null && !IsWorldObject(__1)) return true;
            SendWorldProjectileHit(source, __1, __0, __2, __3, __4, __5, __6, __7, __8, __9,
                __originalMethod.Name == "HeadShotUnit" ? (byte)1 : (byte)0);
            return false;
        }

        private static bool WorldProjectileBlockHitPrefix(MonoBehaviour damageSender, GameObject damageReciever,
            int damage, DamageType type, float forceX, float forceY, float x, float y)
        {
            var source = GetWorldHitSource(damageSender);
            if (_worldApplying != 0) return true;
            DamageType actualType;
            var target = ResolveWorldHitReceiver(damageReciever, type, out actualType);
            if (target == null || target.Nid == NID.NoID ||
                _worldDamageSource == null && !IsWorldObject(target)) return true;
            var relay = damageReciever.GetComponent<DamageRelay>();
            var contact = relay == null ? NID.NoID : Registry.GetNID(relay);
            if (relay != null && contact == NID.NoID) return true;
            if (IsWorldRemoteHeroAttack(damageSender, target)) return false;
            if (source == null) return true;
            if (target.IsMine)
            {
                if (HasWorldOwnedHitPresentation(target))
                    SendWorldOwnedHitPresentation(target, damage, actualType, forceX, forceY, (int)Mathf.Sign(forceX));
                return true;
            }
            SendWorldProjectileContactHit(source, target, damageSender, damage, type, forceX, forceY,
                (int)Mathf.Sign(forceX), false, x, y, 2, contact, actualType);
            return false;
        }

        private static BroforceObject GetWorldHitSource(MonoBehaviour actor)
        {
            // Mine.Explode already settles through each peer's native collapse.
            if (actor is Mine) return null;
            if (_worldDamageSource != null) return _worldDamageSource;
            // Hero-owned splash effects can hit on later animation frames, outside
            // Projectile.Update. Route those hits through the same owner settlement.
            var hero = actor as Unit;
            return _worldEpoch != 0 && IsWorldSession() && hero != null && hero.IsHero && hero.IsMine ? hero : null;
        }

        private static void SendWorldProjectileHit(BroforceObject source, BroforceObject target, MonoBehaviour actor, int damage,
            DamageType type, float vx, float vy, int direction, bool knock, float x, float y, byte kind)
        {
            SendWorldProjectileContactHit(source, target, actor, damage, type, vx, vy, direction, knock, x, y, kind, NID.NoID, type);
        }

        private static void SendWorldProjectileContactHit(BroforceObject source, BroforceObject target, MonoBehaviour actor, int damage,
            DamageType type, float vx, float vy, int direction, bool knock, float x, float y, byte kind, NID contact, DamageType actualType)
        {
            var request = kind <= 2 ? BeginWorldHitPresentation(target, type, vx, vy, knock || kind == 2) : 0;
            if (kind <= 2) PresentWorldHitDirected(target, request, damage, actualType, vx, vy, direction, target is Unit && kind == 2 ? (byte)0 : kind);
            using (var stream = new MemoryStream())
            using (var writer = new BinaryWriter(stream))
            {
                WriteWorldId(writer, target.Nid); WriteWorldId(writer, Registry.GetNID(actor));
                writer.Write((byte)source.Owner.AsInt); writer.Write(kind);
                writer.Write(damage); writer.Write((int)type); writer.Write(vx); writer.Write(vy);
                writer.Write(direction); writer.Write(knock); writer.Write(x); writer.Write(y);
                writer.Write(request);
                WriteWorldId(writer, contact);
                if (kind >= 7)
                {
                    var entry = RegisterWorldObject(target);
                    WriteWorldId(writer, source.Nid);
                    writer.Write(entry.Life); writer.Write(++entry.DirectSequence);
                }
                global::Networking.Networking.RPC<long, byte[]>(target.Owner,
                    new RpcSignature<long, byte[]>(ReceiveWorldProjectileHitRPC), _worldEpoch, stream.ToArray(), true);
                _worldHitMessagesPending = true;
            }
        }

        private static void SendWorldOwnedHitPresentation(BroforceObject target, int damage, DamageType type,
            float vx, float vy, int direction)
        {
            if (_worldEpoch == 0 || !IsWorldSession() || !target.IsMine || damage <= 0) return;
            using (var stream = new MemoryStream())
            using (var writer = new BinaryWriter(stream))
            {
                WriteWorldId(writer, target.Nid); writer.Write((byte)target.Owner.AsInt);
                writer.Write(damage); writer.Write((int)type); writer.Write(vx); writer.Write(vy); writer.Write(direction);
                global::Networking.Networking.RPC<long, byte[]>(PID.TargetOthers,
                    new RpcSignature<long, byte[]>(ReceiveWorldOwnedHitPresentationRPC), _worldEpoch, stream.ToArray(), true);
                _worldHitMessagesPending = true;
            }
        }

        [AllowedRPC]
        private static void ReceiveWorldOwnedHitPresentationRPC(long epoch, byte[] bytes)
        {
            if (_worldEpoch == 0 || epoch != _worldEpoch || !IsWorldSession() || RPCController.CurrentRPC == null) return;
            using (var stream = new MemoryStream(bytes))
            using (var reader = new BinaryReader(stream))
            {
                var id = ReadWorldId(reader); var owner = reader.ReadByte();
                var sender = RPCController.CurrentRPC.messageInfo.Sender;
                if (id.Sid != Connect.SessionID ||
                    (owner == PID.TargetServer.AsInt ? sender != PID.ServerID : sender.AsInt != owner)) return;
                var target = Registry.GetObject(id) as BroforceObject;
                if (target == null || target.Owner.AsInt != owner || target.IsMine || !IsWorldObject(target) ||
                    !HasWorldOwnedHitPresentation(target)) return;
                var damage = reader.ReadInt32(); var type = (DamageType)reader.ReadInt32();
                var vx = reader.ReadSingle(); var vy = reader.ReadSingle(); var direction = reader.ReadInt32();
                PresentWorldSpecialHit(target, new WorldHitPresentation { Health = target.health }, 0,
                    damage, type, vx, vy, direction, 0);
            }
        }

        [AllowedRPC]
        private static void ReceiveWorldProjectileHitRPC(long epoch, byte[] bytes)
        {
            if (_worldEpoch == 0 || epoch != _worldEpoch || !IsWorldSession() || RPCController.CurrentRPC == null) return;
            using (var stream = new MemoryStream(bytes))
            using (var reader = new BinaryReader(stream))
            {
                var id = ReadWorldId(reader); var actorId = ReadWorldId(reader); var owner = reader.ReadByte();
                var sender = RPCController.CurrentRPC.messageInfo.Sender;
                if (id.Sid != Connect.SessionID || (owner == PID.TargetServer.AsInt ? sender != PID.ServerID : sender.AsInt != owner)) return;
                var target = Registry.GetObject(id) as BroforceObject;
                if (target == null || !target.IsMine) return;
                var actor = Registry.GetObject(actorId) as MonoBehaviour;
                var kind = reader.ReadByte(); var damage = reader.ReadInt32(); var type = (DamageType)reader.ReadInt32();
                var vx = reader.ReadSingle(); var vy = reader.ReadSingle(); var direction = reader.ReadInt32();
                var knock = reader.ReadBoolean(); var x = reader.ReadSingle(); var y = reader.ReadSingle();
                var request = reader.ReadInt32();
                var contactId = ReadWorldId(reader);
                if (kind >= 7)
                {
                    var sourceId = ReadWorldId(reader);
                    var life = reader.ReadInt32(); var sequence = reader.ReadInt32();
                    var source = Registry.GetObject(sourceId) as BroforceObject;
                    if (kind > 9 || !(target is Unit) || !IsWorldObject(target) || target.destroyed ||
                        sourceId.Sid != Connect.SessionID || source == null || source.Owner.AsInt != owner ||
                        !(source is Unit && ((Unit)source).IsHero) && !IsWorldObject(source)) return;
                    var entry = RegisterWorldObject(target);
                    // Publish a revival that happened since the last periodic snapshot first.
                    var unit = (Unit)target;
                    if (entry.Last != null && (entry.Last.Flags & WorldFlags.Dead) != 0 &&
                        unit.health > 0 && unit.actionState != ActionState.Dead) SendWorldHitResult(target);
                    if (entry.Life != life || entry.Last != null && (entry.Last.Flags & WorldFlags.Removed) != 0) return;
                    int previous;
                    if (entry.DirectReceived != null && entry.DirectReceived.TryGetValue(sender.AsInt, out previous) && sequence <= previous) return;
                    if (entry.DirectReceived == null) entry.DirectReceived = new Dictionary<int, int>();
                    entry.DirectReceived[sender.AsInt] = sequence;
                }
                var contact = Registry.GetObject(contactId) as DamageRelay;
                DamageType actualType;
                var receiver = contactId == NID.NoID ? target.gameObject : contact == null ? null : contact.gameObject;
                if (receiver != null && ResolveWorldHitReceiver(receiver, type, out actualType) != target) receiver = null;
                var crate = target as CrateBlock;
                var drop = crate == null ? null : WorldCratePickup.GetValue(crate) as Pickupable;
                var previousRequest = _worldSettlingHit; var previousRequestor = _worldHitRequestor;
                var previousTarget = _worldReceivingTarget;
                _worldReceivingTarget = target;
                _worldSettlingHit = request;
                _worldHitRequestor = sender.AsInt;
                _worldReceivingHit++;
                try
                {
                if (kind >= 3 && kind <= 9)
                    ApplyWorldDirectMutation(target, actor, kind, damage, type, vx, vy, direction, x, y);
                else if (kind == 2)
                {
                    if (receiver != null) receiver.SendMessage("Damage", new DamageObject(damage, type, vx, vy, x, y, actor));
                }
                else
                {
                    var unit = target as Unit;
                    if (unit == null) return;
                    if (knock) unit.Knock(type, vx, vy, false);
                    if (kind == 1) unit.HeadShot(damage, type, vx, vy, direction, x, y, actor);
                    else unit.Damage(damage, type, vx, vy, direction, actor, x, y);
                }
                }
                finally
                {
                    _worldReceivingHit--; _worldReceivingTarget = previousTarget;
                    _worldSettlingHit = previousRequest; _worldHitRequestor = previousRequestor;
                }
                // Capture after the entire damage/death call, including corpse setup.
                SendWorldHitResult(target);
                var bossPiece = target as BossPiece;
                if (bossPiece != null)
                    foreach (var connected in bossPiece.connectedPieces)
                        if (connected != null && connected != target && connected.IsMine) SendWorldHitResult(connected);
                if (drop != null && drop.gameObject.activeSelf) SendWorldHitResult(drop);
                if (request != 0)
                    global::Networking.Networking.RPC<long, NID, int>(sender,
                        new RpcSignature<long, NID, int>(ReceiveWorldHitAcknowledgementRPC), epoch, id, request, true);
                _worldHitMessagesPending = true;
            }
        }

        private static int BeginWorldHitPresentation(BroforceObject target, DamageType type, float vx, float vy, bool knock)
        {
            var unit = target as TestVanDammeAnim;
            if (target.IsMine || !IsWorldObject(target) || target.destroyed || target.health <= 0 && unit == null ||
                target is Unit && (((Unit)target).IsHero || ((Unit)target).invulnerable)) return 0;
            var entry = RegisterWorldObject(target);
            if (entry.Last == null) entry.Last = CaptureWorldState(entry);
            if (unit != null && knock) unit.Knock(type, vx, vy, false);
            entry.PendingHit = ++_worldHitSequence;
            entry.PendingHitUntil = Time.unscaledTime + 1f;
            return entry.PendingHit;
        }

        [AllowedRPC]
        private static void ReceiveWorldHitAcknowledgementRPC(long epoch, NID id, int request)
        {
            if (_worldEpoch == 0 || epoch != _worldEpoch || !IsWorldSession() || RPCController.CurrentRPC == null) return;
            WorldEntry entry;
            if (!WorldEntries.TryGetValue(id, out entry)) return;
            var targetOwner = entry.Object != null ? entry.Object.Owner : new PID(entry.Last.Owner);
            var owner = targetOwner == PID.TargetServer ? PID.ServerID : targetOwner;
            if (RPCController.CurrentRPC.messageInfo.Sender != owner) return;
            WorldPresentedHitEffects.Remove(request);
            if (entry.PendingHit == request) EndWorldHitPresentation(entry);
        }

        private static void EndWorldHitPresentation(WorldEntry entry)
        {
            WorldPresentedHitEffects.Remove(entry.PendingHit);
            entry.PendingHit = 0;
            FinishWorldHitPresentation(entry, entry.Last);
            if (entry.Last == null || entry.Object == null || (entry.Last.Flags & WorldFlags.Removed) != 0) return;
            _worldApplying++;
            try { ApplyWorldObjectState(entry, entry.Last); }
            finally { _worldApplying--; }
            entry.ReceivedAt = Time.unscaledTime;
        }

        private static void SendWorldHitResult(BroforceObject target)
        {
            if (!IsWorldObject(target)) return;
            var entry = RegisterWorldObject(target);
            // OnDestroy may already have published a tombstone during damage.
            if (entry.Last != null && (entry.Last.Flags & WorldFlags.Removed) != 0) return;
            var state = CaptureWorldState(entry);
            state.Revision = ++_worldSequence;
            entry.Last = state;
            entry.SentAt = state.SentAt;
            entry.Dirty = false;
            SendWorldStates(new[] { state }, PID.TargetOthers, true);
        }

        private static void FlushWorldHitMessages()
        {
            if (!_worldHitMessagesPending) return;
            _worldHitMessagesPending = false;
            // Keep same-frame native collapse and result messages in send order,
            // without waiting for RPCBatcher's 50 ms timer or flushing each hit.
            RPCBatcher.FlushQueue();
        }

        private static bool WorldProjectileUpdatePrefix(Projectile __instance, MethodBase __originalMethod, out WorldDamageContext __state)
        {
            if (!BeginWorldProjectileContext(__instance, out __state) || __instance.IsMine) return true;
            if (__originalMethod.Name == "FixedUpdate") return false;
            var entry = RegisterWorldObject(__instance);
            if (entry.Last == null)
            {
                entry.Last = CaptureWorldState(entry);
                entry.LerpFrom = new Vector2(__instance.X, __instance.Y);
                entry.LerpRotation = __instance.transform.eulerAngles.z;
                entry.ReceivedAt = Time.unscaledTime - WorldSendInterval;
            }
            var state = entry.Last;
            Vector2 xy;
            float rotation;
            ApplyWorldProjectileMotion(__instance, entry, Time.unscaledTime, state, out xy, out rotation);
            __instance.SetPosition(new Vector3(xy.x, xy.y, __instance.transform.position.z));
            __instance.transform.rotation = Quaternion.Euler(0f, 0f, rotation);
            var shell = __instance as HeavyShell;
            if (shell != null && shell.cross != null && shell.showPredictionTrail)
            {
                // HeavyShell.RunProjectile also draws the red trajectory and landing marker.
                // Advance only its visual prediction; movement and hits remain owner-controlled.
                var delta = Mathf.Clamp(Time.deltaTime, 0f, 0.0334f);
                WorldProjectileDelta.SetValue(shell, delta);
                WorldShellDip.SetValue(shell, (float)WorldShellDip.GetValue(shell) + delta);
                WorldBaseHeavyShellPrediction(shell);
                WorldBaseHeavyShellPrediction(shell);
            }
            var rocket = __instance as Rocket;
            if (rocket != null)
            {
                WorldBaseRocketSmoke(rocket, Time.deltaTime);
                var trail = (Component)WorldRocketTrail.GetValue(rocket);
                if (trail != null) trail.transform.position = rocket.transform.position + rocket.transform.TransformVector(new Vector3(7f, 0f, 0f));
            }
            return false;
        }

        private static bool WorldNativeStatePrefix(NetworkObject __instance, State state)
        {
            if (WorldNativeSyncPrefix(__instance)) return true;
            var spear = __instance as PredabroSpear;
            if (spear == null) return false;
            var entry = RegisterWorldObject(spear);
            if (entry.SpearEnding) return false;
            var motion = entry.SpearMotion;
            if (motion != null && motion.Count > 0 && state.TimeStamp <= motion[motion.Count - 1].Time) return false;
            var position = spear.Position;
            var velocity = spear.Velocity;
            var properties = spear.Props;
            for (var i = 0; i < properties.Length; i++)
            {
                if (properties[i].Name == "Position") position = (Vector2)state.Values[i];
                else if (properties[i].Name == "Velocity") velocity = (Vector2)state.Values[i];
            }
            // Render in the receiver's clock domain; the native timestamp remains for ordering.
            var owner = spear.Owner == PID.TargetServer ? PID.ServerID : spear.Owner;
            var receivedAt = Time.unscaledTime - Mathf.Max(0f, PingController.GetPing(owner));
            if (motion == null)
            {
                motion = new List<WorldProjectileSample>(6);
                motion.Add(new WorldProjectileSample
                {
                    Time = state.TimeStamp - WorldSpearSyncInterval,
                    LocalTime = receivedAt - WorldSpearSyncInterval,
                    X = spear.X, Y = spear.Y, VX = velocity.x, VY = velocity.y,
                    Rotation = spear.transform.eulerAngles.z
                });
                entry.SpearMotion = motion;
            }
            if (receivedAt <= motion[motion.Count - 1].LocalTime)
                receivedAt = motion[motion.Count - 1].LocalTime + 0.001f;
            motion.Add(new WorldProjectileSample
            {
                Time = state.TimeStamp, LocalTime = receivedAt,
                X = position.x, Y = position.y, VX = velocity.x, VY = velocity.y,
                Rotation = entry.Last == null ? spear.transform.eulerAngles.z : entry.Last.Rotation
            });
            if (motion.Count > 6) motion.RemoveAt(0);
            return false;
        }

        private static bool WorldSpearDeathPrefix(PredabroSpear __instance)
        {
            if (_worldEpoch == 0 || !IsWorldSession()) return true;
            var entry = RegisterWorldObject(__instance);
            // RunLife already presented the terminal impact. OnDisable calls Death again;
            // direct Death also destroys the object before presenting its own impact.
            if (_worldApplying != 0 || !__instance.IsMine || entry.SpearEnding ||
                entry.SpearEffectFrame == Time.frameCount) return false;
            SendWorldSpearTermination(__instance, entry);
            return true;
        }

        private static void WorldSpearRunLifePrefix(Projectile __instance)
        {
            var spear = __instance as PredabroSpear;
            if (spear == null || _worldEpoch == 0 || !IsWorldSession() || !spear.IsMine || !spear.Syncronize)
                return;
            var delta = WorldProjectileDelta == null ? Time.deltaTime : (float)WorldProjectileDelta.GetValue(spear);
            if (spear.life <= delta) SendWorldSpearTermination(spear, RegisterWorldObject(spear));
        }

        private static void SendWorldSpearTermination(PredabroSpear spear, WorldEntry entry)
        {
            if (entry.SpearEnding) return;
            var revision = ++_worldSequence;
            var x = spear.X;
            var y = spear.Y;
            ApplyWorldSpearTermination(entry, spear, x, y, revision);
            global::Networking.Networking.RPC<long, NID, byte, int, float, float>(PID.TargetOthers,
                new RpcSignature<long, NID, byte, int, float, float>(ReceiveWorldSpearTerminationRPC),
                _worldEpoch, spear.Nid, (byte)spear.Owner.AsInt, revision, x, y, true);
        }

        private static bool ApplyWorldSpearTermination(WorldEntry entry, PredabroSpear spear,
            float x, float y, int revision)
        {
            if (entry.Last != null && entry.Last.Revision >= revision) return false;
            entry.SpearEnding = true;
            entry.SpearMotion = null;
            entry.ProjectileMotion = null;
            if (entry.Last == null)
                entry.Last = new WorldSnapshot { Id = entry.Id, Type = spear.GetType().Name, Owner = spear.Owner.AsInt };
            entry.Last.Revision = revision;
            entry.Last.X = x;
            entry.Last.Y = y;
            entry.Last.VX = 0f;
            entry.Last.VY = 0f;
            entry.Last.Rotation = spear.transform.eulerAngles.z;
            entry.Last.SentAt = Time.unscaledTime;
            spear.SetXY(x, y);
            spear.SetPosition();
            return true;
        }

        [AllowedRPC]
        private static void ReceiveWorldSpearTerminationRPC(long epoch, NID id, byte owner, int revision, float x, float y)
        {
            if (_worldEpoch == 0 || epoch != _worldEpoch || !IsWorldSession() || RPCController.CurrentRPC == null ||
                id.Sid != Connect.SessionID) return;
            var sender = RPCController.CurrentRPC.messageInfo.Sender;
            if (owner == PID.TargetServer.AsInt ? sender != PID.ServerID : sender.AsInt != owner) return;
            WorldEntry entry;
            if (!WorldEntries.TryGetValue(id, out entry))
            {
                var registered = Registry.GetObject(id) as PredabroSpear;
                if (registered == null || registered.IsMine) return;
                entry = RegisterWorldObject(registered);
            }
            var spear = entry.Object as PredabroSpear;
            if (spear == null || spear.IsMine) return;
            ApplyWorldSpearTermination(entry, spear, x, y, revision);
        }

        private static void WorldSpearSyncSpawnPrefix(Projectile prefab, ref bool synced)
        {
            if (IsWorldSession() && prefab is PredabroSpear)
                synced = true;
        }

        private static void WorldSpearSyncUpdatePrefix(
            out List<KeyValuePair<NID, NetworkObject>> __state)
        {
            __state = null;
            if (_worldEpoch == 0 || !IsWorldSession() || Connect.IsOffline) return;

            var components = Registry.ComponentsToSync;
            foreach (var pair in components)
            {
                if (!(pair.Value is PredabroSpear) || !pair.Value.Syncronize || !pair.Value.IsMine) continue;
                if (__state == null) __state = new List<KeyValuePair<NID, NetworkObject>>();
                __state.Add(pair);
            }

            if (__state == null) return;
            foreach (var pair in __state) components.Remove(pair.Key);
        }

        private static void WorldSpearSyncUpdatePostfix(
            List<KeyValuePair<NID, NetworkObject>> __state)
        {
            SendWorldSpearStates(__state);
            RestoreWorldSpearSyncTargets(__state);
        }

        private static Exception WorldSpearSyncUpdateFinalizer(
            Exception __exception,
            List<KeyValuePair<NID, NetworkObject>> __state)
        {
            RestoreWorldSpearSyncTargets(__state);
            return __exception;
        }

        private static void SendWorldSpearStates(List<KeyValuePair<NID, NetworkObject>> spears)
        {
            if (spears == null || spears.Count == 0 || Time.unscaledTime < _worldSpearSyncAt) return;

            var stream = new UnityStream();
            var stateCount = 0;
            foreach (var pair in spears)
            {
                var spear = pair.Value as PredabroSpear;
                if (spear == null || !spear.Syncronize || !spear.IsMine || !spear.ReadyTobeSynced()) continue;
                stream.Serialize<State>(new State(pair.Key, spear));
                stateCount++;
            }

            if (stateCount == 0) return;
            global::Networking.Networking.UnreliableRPC<byte[]>(
                PID.TargetOthers, true, false,
                new RpcSignature<byte[]>(ReceiveWorldSpearStatesRPC), stream.ByteArray);
            _worldSpearSyncAt = Time.unscaledTime + WorldSpearSyncInterval;
            if (_worldSpearLoggedFirstSend) return;
            _worldSpearLoggedFirstSend = true;
            DiagnosticLog.Info("WORLD_STATE first 60 Hz spear state sent.");
        }

        private static void RestoreWorldSpearSyncTargets(List<KeyValuePair<NID, NetworkObject>> spears)
        {
            if (spears == null || spears.Count == 0) return;
            var components = Registry.ComponentsToSync;
            foreach (var pair in spears)
                if (pair.Value != null && pair.Value.Syncronize)
                    components[pair.Key] = pair.Value;
            spears.Clear();
        }

        [AllowedRPC]
        private static void ReceiveWorldSpearStatesRPC(byte[] bytes)
        {
            if (_worldEpoch == 0 || RPCController.CurrentRPC == null) return;
            var sender = RPCController.CurrentRPC.messageInfo.Sender;
            var stream = new UnityStream(bytes);
            while (!stream.Finished)
            {
                var state = (State)stream.DeserializeNext();
                if (state.Key.Sid != Connect.SessionID) continue;
                object component;
                if (!Registry.Components.TryGetValue(state.Key, out component)) continue;
                var spear = component as PredabroSpear;
                if (spear == null || spear.IsMine) continue;
                var owner = spear.Owner == PID.TargetServer ? PID.ServerID : spear.Owner;
                if (sender != owner) continue;
                var entry = RegisterWorldObject(spear);
                if (entry.SpearEnding) continue;
                spear.AddState(state);
                if (_worldSpearLoggedFirstReceive) continue;
                _worldSpearLoggedFirstReceive = true;
                DiagnosticLog.Info("WORLD_STATE first 60 Hz spear state received.");
            }
        }

        private static void RecordWorldProjectileMotion(WorldEntry entry, WorldSnapshot state, float receivedAt)
        {
            if (entry.ProjectileMotion == null) entry.ProjectileMotion = new List<WorldProjectileSample>(6);
            if (!entry.HasProjectileClockOffset)
            {
                entry.ProjectileClockOffset = receivedAt - state.SentAt;
                entry.HasProjectileClockOffset = true;
            }

            var time = state.SentAt + entry.ProjectileClockOffset;
            var samples = entry.ProjectileMotion;
            if (samples.Count == 0)
            {
                samples.Add(new WorldProjectileSample
                {
                    Time = time - WorldSendInterval, X = entry.LerpFrom.x, Y = entry.LerpFrom.y,
                    VX = state.VX, VY = state.VY, Rotation = entry.LerpRotation
                });
            }
            else if (time <= samples[samples.Count - 1].Time)
            {
                time = samples[samples.Count - 1].Time + 0.001f;
            }

            samples.Add(new WorldProjectileSample
            {
                Time = time, X = state.X, Y = state.Y, VX = state.VX, VY = state.VY, Rotation = state.Rotation
            });
            if (samples.Count > 6) samples.RemoveAt(0);
        }

        private static void ResetWorldProjectileMotion(WorldEntry entry)
        {
            entry.SpearMotion = null;
            entry.ProjectileMotion = null;
            entry.ProjectileClockOffset = 0f;
            entry.HasProjectileClockOffset = false;
        }

        private static void ApplyWorldProjectileMotion(Projectile projectile, WorldEntry entry, float now,
            WorldSnapshot state, out Vector2 position, out float rotation)
        {
            if (TryGetWorldSpearTerminalMotion(projectile, entry, out position, out rotation)) return;
            if (entry.SpearMotion != null && entry.SpearMotion.Count > 0)
            {
                var owner = projectile.Owner == PID.TargetServer ? PID.ServerID : projectile.Owner;
                var spearRenderTime = now - Mathf.Max(0f, PingController.GetPing(owner)) - WorldSpearSyncInterval;
                if (TryGetWorldSpearMotion(entry.SpearMotion, spearRenderTime, WorldSpearSyncInterval, projectile.life,
                    projectile.doubleSpeed, out position, out rotation)) return;
            }
            var samples = entry.ProjectileMotion;
            if (samples == null || samples.Count < 2)
            {
                var age = now - entry.ReceivedAt;
                var fraction = Mathf.Clamp01(age / WorldSendInterval);
                position = Vector2.Lerp(entry.LerpFrom, new Vector2(state.X, state.Y), fraction);
                position += new Vector2(state.VX, state.VY) * Mathf.Max(0f, age - WorldSendInterval) *
                    (projectile.doubleSpeed ? 2f : 1f);
                rotation = Mathf.LerpAngle(entry.LerpRotation, state.Rotation, fraction);
                return;
            }

            var renderTime = now - WorldSendInterval;
            var first = samples[0];
            if (renderTime <= first.Time)
            {
                position = new Vector2(first.X, first.Y);
                rotation = first.Rotation;
                return;
            }

            for (var i = 1; i < samples.Count; i++)
            {
                var next = samples[i];
                if (renderTime > next.Time) continue;
                var previous = samples[i - 1];
                var duration = next.Time - previous.Time;
                var fraction = duration <= 0f ? 1f : Mathf.Clamp01((renderTime - previous.Time) / duration);
                position = Vector2.Lerp(new Vector2(previous.X, previous.Y), new Vector2(next.X, next.Y), fraction);
                rotation = Mathf.LerpAngle(previous.Rotation, next.Rotation, fraction);
                return;
            }

            var latest = samples[samples.Count - 1];
            var extrapolation = Mathf.Max(0f, renderTime - latest.Time);
            position = new Vector2(latest.X, latest.Y) +
                new Vector2(latest.VX, latest.VY) * extrapolation * (projectile.doubleSpeed ? 2f : 1f);
            rotation = latest.Rotation;
        }

        private static bool TryGetWorldSpearMotion(List<WorldProjectileSample> samples, float renderTime,
            float maxExtrapolation, float remainingLife, bool doubleSpeed, out Vector2 position, out float rotation)
        {
            if (samples == null || samples.Count == 0)
            {
                position = Vector2.zero;
                rotation = 0f;
                return false;
            }

            var first = samples[0];
            if (renderTime <= first.LocalTime)
            {
                position = new Vector2(first.X, first.Y);
                rotation = first.Rotation;
                return true;
            }

            for (var i = 1; i < samples.Count; i++)
            {
                var next = samples[i];
                if (renderTime > next.LocalTime) continue;
                var previous = samples[i - 1];
                var duration = next.LocalTime - previous.LocalTime;
                var fraction = duration <= 0f ? 1f : Mathf.Clamp01((renderTime - previous.LocalTime) / duration);
                position = Vector2.Lerp(new Vector2(previous.X, previous.Y), new Vector2(next.X, next.Y), fraction);
                rotation = Mathf.LerpAngle(previous.Rotation, next.Rotation, fraction);
                return true;
            }

            var latest = samples[samples.Count - 1];
            var extrapolation = Mathf.Min(renderTime - latest.LocalTime,
                Mathf.Min(maxExtrapolation, Mathf.Max(0f, remainingLife)));
            var speed = doubleSpeed ? 2f : 1f;
            position = new Vector2(latest.X, latest.Y) + new Vector2(latest.VX, latest.VY) * (extrapolation * speed);
            rotation = latest.Rotation;
            return true;
        }

        private static bool TryGetWorldSpearTerminalMotion(Projectile projectile, WorldEntry entry,
            out Vector2 position, out float rotation)
        {
            if (!(projectile is PredabroSpear) || !entry.SpearEnding)
            {
                position = Vector2.zero;
                rotation = 0f;
                return false;
            }
            position = new Vector2(projectile.X, projectile.Y);
            rotation = projectile.transform.eulerAngles.z;
            return true;
        }

        private static bool WorldProjectileSpawnCheckPrefix(Projectile __instance)
        {
            if (!IsWorldReplica(__instance)) return true;
            Map.RegisterProjectile(__instance);
            if (__instance.isDamageable) Map.RegisterDamageableProjectile(__instance);
            return false;
        }

        private static bool WorldProjectileEffectsPrefix(Projectile __instance, MethodBase __originalMethod,
            out WorldDamageContext __state)
        {
            var tracked = BeginWorldProjectileContext(__instance, out __state);
            __state.EffectContext = true;
            _worldProjectileEffectsDepth++;
            if (WorldRandomStateSupported && tracked && __instance.IsMine && _worldProjectileEffectsDepth == 1)
                WorldProjectileVisualRandomStates[__instance] = UnityEngine.Random.state;
            if (_worldApplying != 0 || !tracked) return true;
            if (__instance.IsMine)
            {
                if (__instance is PredabroSpear)
                    RegisterWorldObject(__instance).SpearEffectFrame = Time.frameCount;
                return true;
            }
            var rpc = RPCController.CurrentRPC as NonStaticRPCObject;
            var owner = __instance.Owner == PID.TargetServer ? PID.ServerID : __instance.Owner;
            if (rpc != null && rpc.targetID == __instance.Nid && rpc.methodName == "MakeEffects" && rpc.messageInfo.Sender == owner)
            {
                // A character may replace this method and send its own native impact RPC.
                // Let that handler run, while suppressing world damage during visual replay.
                foreach (var patch in Harmony.GetPatchInfo(__originalMethod).Prefixes)
                    if (patch.owner != _harmony.Id)
                    {
                        __state.VisualReplay = true;
                        _worldApplying++;
                        return true;
                    }
            }
            return false;
        }

        private static void WorldProjectileEffectsPostfix(Projectile __instance, MethodBase __originalMethod, bool __runOriginal,
            bool __0, float __1, float __2, bool __3, Vector3 __4, Vector3 __5)
        {
            // A replacement prefix owns its effects and transport; do not invent a base impact.
            if (!__runOriginal || _worldApplying != 0 || _worldDamageSource != __instance) return;
            MethodInfo method;
            if (!WorldProjectileEffectMethods.TryGetValue(__instance.GetType(), out method))
            {
                for (var type = __instance.GetType(); type != null && typeof(Projectile).IsAssignableFrom(type); type = type.BaseType)
                {
                    method = type.GetMethod("MakeEffects", WorldFields | BindingFlags.DeclaredOnly, null,
                        WorldProjectileEffectParameters, null);
                    if (method != null) break;
                }
                if (method == null) return;
                WorldProjectileEffectMethods.Add(__instance.GetType(), method);
            }
            // Base calls belong to the same impact, so publish only the outer override.
            if (!__originalMethod.Equals(method)) return;
            UnityEngine.Random.State randomState = default(UnityEngine.Random.State);
            var hasRandomState = WorldRandomStateSupported &&
                WorldProjectileVisualRandomStates.TryGetValue(__instance, out randomState);
            using (var stream = new MemoryStream())
            using (var writer = new BinaryWriter(stream))
            {
                WriteWorldId(writer, __instance.Nid); writer.Write((byte)__instance.Owner.AsInt);
                writer.Write(__0); writer.Write(__1); writer.Write(__2); writer.Write(__3);
                writer.Write(__4.x); writer.Write(__4.y); writer.Write(__4.z);
                writer.Write(__5.x); writer.Write(__5.y); writer.Write(__5.z);
                writer.Write(__instance.xI); writer.Write(__instance.yI);
                writer.Write(hasRandomState);
                if (hasRandomState) WriteWorldRandomState(writer, randomState);
                // Removal snapshots execute immediately. Keep impact presentation ahead of them
                // on the same reliable stream instead of leaving it in the delayed RPC queue.
                global::Networking.Networking.RPC<long, byte[]>(PID.TargetOthers,
                    new RpcSignature<long, byte[]>(ReceiveWorldProjectileEffectsRPC), _worldEpoch, stream.ToArray(), true);
            }
        }

        [AllowedRPC]
        private static void ReceiveWorldProjectileEffectsRPC(long epoch, byte[] bytes)
        {
            if (_worldEpoch == 0 || epoch != _worldEpoch || !IsWorldSession() || RPCController.CurrentRPC == null) return;
            using (var stream = new MemoryStream(bytes))
            using (var reader = new BinaryReader(stream))
            {
                var id = ReadWorldId(reader);
                var owner = reader.ReadByte();
                var sender = RPCController.CurrentRPC.messageInfo.Sender;
                if (id.Sid != Connect.SessionID || (owner == PID.TargetServer.AsInt ? sender != PID.ServerID : sender.AsInt != owner)) return;
                WorldEntry entry;
                if (!WorldEntries.TryGetValue(id, out entry) || !(entry.Object is Projectile) || entry.Object == null) return;
                var particles = reader.ReadBoolean(); var x = reader.ReadSingle(); var y = reader.ReadSingle(); var ray = reader.ReadBoolean();
                var normal = new Vector3(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle());
                var point = new Vector3(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle());
                var vx = reader.ReadSingle(); var vy = reader.ReadSingle();
                var hasRandomState = reader.ReadBoolean();
                UnityEngine.Random.State randomState = default(UnityEngine.Random.State);
                if (hasRandomState && !TryReadWorldRandomState(reader, out randomState)) return;
                _worldApplying++;
                var previousRandomState = UnityEngine.Random.state;
                var projectile = (Projectile)entry.Object;
                var previousVX = projectile.xI; var previousVY = projectile.yI;
                try
                {
                    // HitUnits can temporarily scale velocity; use the values at the actual impact.
                    projectile.xI = vx; projectile.yI = vy;
                    var method = AccessTools.Method(projectile.GetType(), "MakeEffects", WorldProjectileEffectParameters, null);
                    var rocket = entry.Object as Rocket;
                    if (method.DeclaringType != typeof(Projectile) && method.DeclaringType != typeof(Rocket) &&
                        method.DeclaringType != typeof(HeavyShell) && method.DeclaringType != typeof(PlasmaBullet))
                    {
                        if (hasRandomState) UnityEngine.Random.state = randomState;
                        method.Invoke(projectile, new object[] { particles, x, y, ray, normal, point });
                    }
                    else if (rocket != null)
                    {
                        // Rocket.MakeEffects keeps these visuals in its owner-only damage branch.
                        Map.ShakeTrees(x, y, 128f, 48f, 100f);
                        EffectsController.CreateExplosionRangePop(x, y, -1f, rocket.range * 3f);
                        if (hasRandomState) UnityEngine.Random.state = randomState;
                        WorldBaseRocketEffects(rocket, particles, x, y, ray, normal, point);
                    }
                    else if (entry.Object is HeavyShell)
                    {
                        if (hasRandomState) UnityEngine.Random.state = randomState;
                        // WorldEffectPrefix suppresses the original ground/unit/doodad damage
                        // during replay; keep the shell's cross, tree shake and huge explosion.
                        WorldBaseHeavyShellEffects((HeavyShell)entry.Object, particles, x, y, ray, normal, point);
                    }
                    else
                    {
                        if (hasRandomState) UnityEngine.Random.state = randomState;
                        if (projectile is PlasmaBullet)
                            WorldBasePlasmaEffects((PlasmaBullet)projectile, particles, x, y, ray, normal, point);
                        else WorldBaseProjectileEffects(projectile, particles, x, y, ray, normal, point);
                    }
                }
                finally
                {
                    projectile.xI = previousVX; projectile.yI = previousVY;
                    if (hasRandomState) UnityEngine.Random.state = previousRandomState;
                    _worldApplying--;
                }
            }
        }

        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
        private static void WorldBaseRocketSmoke(Rocket instance, float t)
        {
            throw new NotSupportedException("Rocket smoke must be reverse patched before use.");
        }

        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
        private static void WorldBasePlasmaEffects(PlasmaBullet instance, bool particles, float x, float y, bool useRayCast, Vector3 hitNormal, Vector3 hitPoint)
        {
            throw new NotSupportedException("Plasma visuals must be reverse patched before use.");
        }

        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
        private static void WorldBaseProjectileEffects(Projectile instance, bool particles, float x, float y, bool useRayCast, Vector3 hitNormal, Vector3 hitPoint)
        {
            throw new NotSupportedException("Projectile visuals must be reverse patched before use.");
        }

        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
        private static void WorldBaseRocketEffects(Rocket instance, bool particles, float x, float y, bool useRayCast, Vector3 hitNormal, Vector3 hitPoint)
        {
            throw new NotSupportedException("Rocket visuals must be reverse patched before use.");
        }

        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
        private static void WorldBaseHeavyShellEffects(HeavyShell instance, bool particles, float x, float y, bool useRayCast, Vector3 hitNormal, Vector3 hitPoint)
        {
            throw new NotSupportedException("HeavyShell visuals must be reverse patched before use.");
        }

        private static void WorldBaseHeavyShellPrediction(HeavyShell instance)
        {
            throw new NotSupportedException("HeavyShell prediction visuals must be reverse patched before use.");
        }

        private static bool WorldSpawnProjectilePrefix(object[] __args, ref Projectile __result)
        {
            var actor = __args[1] as BroforceObject;
            // Heroes are excluded from world-state snapshots, but their locally spawned
            // projectiles still need a shared identity before Fire can hit anything.
            var hero = actor as Unit;
            if (_worldEpoch == 0 || !IsWorldSession() || actor == null || actor.Nid == NID.NoID ||
                !IsWorldObject(actor) && (hero == null || !hero.IsHero)) return true;
            var prefab = (Projectile)__args[0];
            WorldProjectilePrefabs[prefab.GetType().FullName + "|" + prefab.name] = prefab;
            var position = new Vector3((float)__args[2], (float)__args[3], 0f);
            if (!actor.IsMine)
            {
                // Callers sometimes configure the returned projectile. Return a real, inert instance,
                // without Fire: Fire already performs collision and damage before its first Update.
                __result = UnityEngine.Object.Instantiate<Projectile>(prefab, position, Quaternion.identity);
                __result.enabled = false;
                __result.gameObject.SetActive(false);
                UnityEngine.Object.Destroy(__result.gameObject);
                return false;
            }
            var spawn = new WorldProjectileSpawn
            {
                Id = Registry.AllocatePlayerUniqueID(), Actor = actor.Nid, Owner = actor.Owner.AsInt,
                Prefab = prefab.name, Type = prefab.GetType().FullName,
                X = position.x, Y = position.y, VX = (float)__args[4], VY = (float)__args[5],
                Player = (int)(__args[6] is bool ? __args[7] : __args[6]),
                Offset = __args.Length > 7 ? (float)__args[__args.Length - 1] : 0f,
                Seed = UnityEngine.Random.Range(-10000, 10000)
            };
            WorldProjectileSpawns.Add(spawn.Id, spawn);
            // Publish identity and initial parameters before Fire can destroy the instance at spawn.
            SendWorldProjectileSpawn(spawn, PID.TargetOthers);
            __result = CreateWorldProjectile(spawn, prefab, actor);
            return false;
        }

        private static Projectile CreateWorldProjectile(WorldProjectileSpawn spawn, Projectile prefab, BroforceObject actor)
        {
            var projectile = UnityEngine.Object.Instantiate<Projectile>(prefab,
                new Vector3(spawn.X, spawn.Y, 0f), Quaternion.identity);
            var id = spawn.Id;
            Registry.RegisterGameObject(ref id, projectile.gameObject);
            foreach (var component in projectile.GetComponentsInChildren<NetworkObject>(true)) component.SetOwner(new PID(spawn.Owner));
            projectile.SetSeed(spawn.Seed);
            projectile.Fire(spawn.X, spawn.Y, spawn.VX, spawn.VY, spawn.Offset, spawn.Player, actor);
            RegisterWorldObject(projectile);
            return projectile;
        }

        private static void SendWorldProjectileSpawn(WorldProjectileSpawn spawn, PID target)
        {
            using (var stream = new MemoryStream())
            using (var writer = new BinaryWriter(stream))
            {
                WriteWorldId(writer, spawn.Id); WriteWorldId(writer, spawn.Actor);
                writer.Write(spawn.Prefab); writer.Write(spawn.Type); writer.Write((byte)spawn.Owner);
                writer.Write(spawn.Player); writer.Write(spawn.Seed); writer.Write(spawn.X); writer.Write(spawn.Y);
                writer.Write(spawn.VX); writer.Write(spawn.VY); writer.Write(spawn.Offset);
                global::Networking.Networking.RPC<long, byte[]>(target,
                    new RpcSignature<long, byte[]>(ReceiveWorldProjectileRPC), _worldEpoch, stream.ToArray(), true);
            }
        }

        private static void SendWorldProjectileSpawns(PID target)
        {
            foreach (var pair in WorldProjectileSpawns)
            {
                // Component ordering is prefab-specific; use the actor's owner for routing, not a guessed component ID.
                var actor = Registry.GetObject(pair.Value.Actor) as BroforceObject;
                if (actor == null || !actor.IsMine) continue;
                var instance = Registry.GetObject(pair.Key) as GameObject;
                if (instance != null) SendWorldProjectileSpawn(pair.Value, target);
            }
        }

        [AllowedRPC]
        private static void ReceiveWorldProjectileRPC(long epoch, byte[] bytes)
        {
            if (_worldEpoch == 0 || epoch != _worldEpoch || !IsWorldSession() || RPCController.CurrentRPC == null) return;
            WorldProjectileSpawn spawn;
            using (var stream = new MemoryStream(bytes))
            using (var reader = new BinaryReader(stream))
            {
                spawn = new WorldProjectileSpawn
                {
                    Id = ReadWorldId(reader), Actor = ReadWorldId(reader), Prefab = reader.ReadString(), Type = reader.ReadString(),
                    Owner = reader.ReadByte(), Player = reader.ReadInt32(), Seed = reader.ReadInt32(),
                    X = reader.ReadSingle(), Y = reader.ReadSingle(), VX = reader.ReadSingle(), VY = reader.ReadSingle(), Offset = reader.ReadSingle()
                };
            }
            var sender = RPCController.CurrentRPC.messageInfo.Sender;
            if (spawn.Id.Sid != Connect.SessionID ||
                (spawn.Owner == PID.TargetServer.AsInt ? sender != PID.ServerID : sender.AsInt != spawn.Owner)) return;
            if (!TryCreateWorldProjectile(spawn) && WorldPendingProjectiles.Count < 256) WorldPendingProjectiles[spawn.Id] = spawn;
        }

        private static bool TryCreateWorldProjectile(WorldProjectileSpawn spawn)
        {
            if (WorldProjectileSpawns.ContainsKey(spawn.Id) || Registry.IsKeyInUse(spawn.Id)) return true;
            // A state tombstone may arrive before the spawn descriptor.
            if (WorldRemovedObjects.Contains(spawn.Id.BaseID)) return true;
            var actor = Registry.GetObject(spawn.Actor) as BroforceObject;
            if (actor == null) return false;
            var key = spawn.Type + "|" + spawn.Prefab;
            Projectile prefab;
            if (!WorldProjectilePrefabs.TryGetValue(key, out prefab) || prefab == null)
            {
                foreach (var component in actor.GetComponents<MonoBehaviour>())
                {
                    if (component == null) continue;
                    FieldInfo[] fields;
                    if (!WorldProjectileFields.TryGetValue(component.GetType(), out fields))
                    {
                        var collected = new List<FieldInfo>();
                        for (var type = component.GetType(); type != null && type != typeof(MonoBehaviour); type = type.BaseType)
                            foreach (var field in type.GetFields(WorldFields | BindingFlags.DeclaredOnly))
                                if (typeof(Projectile).IsAssignableFrom(field.FieldType)) collected.Add(field);
                        fields = collected.ToArray(); WorldProjectileFields[component.GetType()] = fields;
                    }
                    foreach (var field in fields)
                    {
                        var candidate = field.GetValue(component) as Projectile;
                        if (candidate != null) WorldProjectilePrefabs[candidate.GetType().FullName + "|" + candidate.name] = candidate;
                    }
                }
                if (!WorldProjectilePrefabs.TryGetValue(key, out prefab) && !WorldMissingProjectilePrefabs.Contains(key))
                {
                    foreach (var asset in Resources.FindObjectsOfTypeAll(typeof(Projectile)))
                    {
                        var candidate = (Projectile)asset;
                        if (candidate.name == spawn.Prefab && candidate.GetType().FullName == spawn.Type)
                        { prefab = candidate; WorldProjectilePrefabs[key] = candidate; break; }
                    }
                    if (prefab == null)
                    {
                        WorldMissingProjectilePrefabs.Add(key);
                        DiagnosticLog.Warning("WORLD_STATE projectile prefab not found: " + key + ".");
                    }
                }
            }
            if (prefab == null) return false;
            WorldProjectileSpawns.Add(spawn.Id, spawn);
            CreateWorldProjectile(spawn, prefab, actor);
            return true;
        }

        private static void UpdateWorldPendingProjectiles()
        {
            WorldPendingApplied.Clear();
            foreach (var pair in WorldPendingProjectiles)
                if (TryCreateWorldProjectile(pair.Value)) WorldPendingApplied.Add(pair.Key);
            foreach (var id in WorldPendingApplied) WorldPendingProjectiles.Remove(id);
        }
    }
}
