using HarmonyLib;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Reflection;
using Networking;
using UnityEngine;
using Utility;

namespace CustomMapMultiplayer
{
    internal static partial class HarmonyDiagnostics
    {
        private const float WorldSendInterval = 1f / 12f;
        private const float WorldSpearSyncInterval = 1f / 60f;
        private const int WorldProtocol = 24;
        private const BindingFlags WorldFields = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        private static readonly Dictionary<NID, WorldEntry> WorldEntries = new Dictionary<NID, WorldEntry>();
        private static readonly Dictionary<NID, WorldSnapshot> WorldPending = new Dictionary<NID, WorldSnapshot>();
        private static readonly List<WorldEntry> WorldCandidates = new List<WorldEntry>();
        private static readonly List<WorldEntry> WorldUnits = new List<WorldEntry>();
        private static readonly List<WorldSnapshot> WorldMotionBatch = new List<WorldSnapshot>();
        private static readonly List<WorldSnapshot> WorldCriticalBatch = new List<WorldSnapshot>();
        private static readonly List<NID> WorldPendingApplied = new List<NID>();
        private static readonly HashSet<NID> WorldRemovedObjects = new HashSet<NID>();
        private static readonly FieldInfo WorldReviveSource = AccessTools.Field(typeof(TestVanDammeAnim), "reviveSource");
        private static readonly FieldInfo WorldZombie = AccessTools.Field(typeof(Unit), "isZombie");
        private static readonly FieldInfo WorldTankTurn = AccessTools.Field(typeof(Tank), "turnFrame");
        private static readonly FieldInfo WorldTruckUnfold = AccessTools.Field(typeof(MookArtilleryTruck), "unfoldFrame");
        private static readonly FieldInfo WorldTankDead = AccessTools.Field(typeof(Tank), "isDead");
        private static readonly FieldInfo WorldUnitSprite = AccessTools.Field(typeof(TestVanDammeAnim), "sprite");
        private static readonly FieldInfo WorldUnitGunSprite = AccessTools.Field(typeof(TestVanDammeAnim), "gunSprite");
        private static readonly FieldInfo WorldPlasmaCounter = AccessTools.Field(typeof(Unit), "plasmaCounter");
        private static readonly FieldInfo WorldPlasmaDamage = AccessTools.Field(typeof(Unit), "plasmaDamage");
        private static readonly FieldInfo WorldShockCounter = AccessTools.Field(typeof(Unit), "shockCounter");
        private static readonly FieldInfo WorldDogCorpse = AccessTools.Field(typeof(DogAI), "attractedToCorpse");
        private static readonly MethodInfo WorldDisarmShield = typeof(MookRiotShield).GetMethod("DisarmShield", WorldFields);
        private static long _worldEpoch;
        private static int _worldSequence;
        private static int _worldApplying;
        private static int _worldServer = -1;
        private static bool _worldSeeded;
        private static float _worldSendAt;
        private static float _worldSpearSyncAt;
        private static float _worldRequestAt;
        private static bool _worldSpearLoggedFirstSend;
        private static bool _worldSpearLoggedFirstReceive;

        [Flags]
        private enum WorldFlags : ushort
        {
            // SpritePose preserves corpses and electrified living mooks.
            None = 0, Removed = 1, Dead = 2, Mega = 4, Collapsed = 8,
            Destroyed = 16, DoodadDead = 32, Disturbed = 64, Zombie = 128, SpritePose = 256, Gibbed = 512, Shield = 1024, Exploded = 2048,
            Pickup = 4096, PickupActive = 8192, PickupCollected = 16384, MechAnimated = 32768
        }

        private sealed class WorldSnapshot
        {
            internal NID Id;
            internal string Type;
            internal int Revision, Life, Owner, Health, MaxHealth, Action, Facing, Turn, Unfold, Player;
            internal short Input;
            internal float X, Y, VX, VY, Scale, Rotation, LifeTime, GroundHeight, SentAt;
            internal int Row, Column;
            internal NID Parent, Pilot, ReviveSource;
            internal WorldFlags Flags;
            internal int PoseFrame;
            internal float PoseBlastX, PoseBlastY, PoseScaleY, GibX, GibY;
            internal DamageType GibType;
            internal Vector2 PosePixel;
            internal Vector3 PoseOffset;
            internal bool Electrified, PoseGunActive;
            internal float BlindTime, ScaredTime;
            internal int Mental;
            internal float PickupDelay;
            internal int PickupType;
            internal int ArmourDamage, FireDamage, ArmourKnocks;
            internal float ShieldTime;
            internal bool Invulnerable, BossDamaged, GunnerEjected;
            internal float BurnTime, BurnExplosion;
            internal int BurnDamage;
            internal bool Oily;
            internal byte DoodadVisual, DoodadColliders, DoodadMaterial;
            internal Vector2 DoodadPixel;
            internal byte[] DoodadChildren;
            internal float StatusTime, StunTime, FrozenTime, PlasmaTime, ShockTime, AcidTime, InseminatedTime, InseminationTime;
            internal int PlasmaDamage, InseminationFrame;
            internal bool Acid, TearGas;
            internal NID Inseminator = NID.NoID;
            internal string StatusMaterial = "";
            internal float StatusReceivedAt;
        }

        private struct WorldProjectileSample
        {
            internal float Time, X, Y, VX, VY, Rotation;
            internal float LocalTime;
        }

        private sealed class WorldEntry
        {
            internal BroforceObject Object;
            internal NID Id;
            internal WorldSnapshot Last;
            internal int Life = 1;
            internal int DirectSequence;
            internal Dictionary<int, int> DirectReceived;
            internal int DeathLife = -1;
            internal float SentAt, ReceivedAt, CheckAt;
            internal Vector2 LerpFrom;
            internal bool Dirty = true;
            internal SpriteSM Sprite;
            internal bool Gibbed;
            internal DamageType GibType;
            internal float GibX, GibY;
            internal bool Transient;
            internal float LerpRotation;
            internal bool Exploded;
            internal List<WorldProjectileSample> ProjectileMotion;
            internal float ProjectileClockOffset;
            internal bool HasProjectileClockOffset;
            internal List<WorldProjectileSample> SpearMotion;
            internal int SpearEffectFrame = -1;
            internal bool SpearEnding;
            internal int PendingHit;
            internal float PendingHitUntil;
            internal WorldHitPresentation Presentation;
            internal WorldFlags PresentedFlags;
            internal int RegistrationFrame = -1, DoodadEffectRevision;
        }

        private static bool IsWorldSession()
        {
            if (!_networkSessionActive) return false;
            var layer = Connect.Layer;
            return layer != null && layer.Room != null && !layer.IsOffline && PID.AreIDsSet &&
                !SceneLoader.IsSceneLoading && Map.MapData != null && !Map.isEditing;
        }

        private static bool IsWorldObject(BroforceObject obj)
        {
            var unit = obj as Unit;
            return obj != null && !(unit != null && unit.IsHero) &&
                (unit != null || obj is Doodad || obj is Block || obj is Projectile || obj is Grenade || obj is Pickupable || IsWorldBossPart(obj)) &&
                obj.Nid != NID.NoID;
        }

        private static bool IsCaptiveWorldVillager(BroforceObject obj)
        {
            // TorturedVillager positions only the inactive captive's Transform.
            // X/Y are prefab coordinates until FreeVillagerRPC initializes them.
            return obj is Villager && !obj.gameObject.activeInHierarchy;
        }

        private static bool IsWorldReplica(BroforceObject obj)
        {
            return _worldEpoch != 0 && IsWorldSession() && IsWorldObject(obj) && !obj.IsMine;
        }

        private static void PatchWorldMethod(MethodBase method, string prefix, string postfix)
        {
            _harmony.Patch(method,
                prefix == null ? null : new HarmonyMethod(typeof(HarmonyDiagnostics).GetMethod(prefix, BindingFlags.NonPublic | BindingFlags.Static)),
                postfix == null ? null : new HarmonyMethod(typeof(HarmonyDiagnostics).GetMethod(postfix, BindingFlags.NonPublic | BindingFlags.Static)), null, null);
        }

        private static void PatchEntityFinalStateSynchronization()
        {
            WorldHeroStatusMethods.Clear();
            // Resolve overrides once. Runtime updates use registered candidates, never an assembly or map scan.
            foreach (var type in typeof(Unit).Assembly.GetTypes())
            {
                if (!typeof(BroforceObject).IsAssignableFrom(type)) continue;
                foreach (var method in type.GetMethods(WorldFields | BindingFlags.DeclaredOnly))
                {
                    if (method.IsAbstract || method.IsStatic || method.ContainsGenericParameters) continue;
                    var name = method.Name;
                    if (name == "OnDestroy")
                        // Mook and some doodads override OnDestroy without calling the base implementation.
                        PatchWorldMethod(method, "WorldDestroyPrefix", null);
                    else if (name == "Damage" || name == "DamageInternal" || name == "DamageOptional" ||
                        name == "Death" || name == "QuietDeath" || name == "Gib" || name == "GibNow" || name == "Burn" ||
                        name == "BurnInternal" || name == "ForceBurn" || name == "SetAlight_Local")
                        PatchWorldMethod(method, WorldDirectMutationKind(method) == 0 ? "WorldMutationPrefix" :
                            WorldDirectMutationKind(method) == 3 ? "WorldDirectOptionalMutationPrefix" : "WorldDirectMutationPrefix", "WorldMutationPostfix");
                    else if (name == "RunBurnLogic")
                        PatchWorldDamageContext(method, "WorldBurnLogicPrefix");
                    else if (name == "RunExploding" && typeof(BarrelBlock).IsAssignableFrom(type))
                        PatchWorldDamageContext(method, "WorldBarrelExplodingPrefix");
                    else if (name == "BurnOthers" || name == "StopBurning" || name == "BurnSurroundingOil")
                        PatchWorldMethod(method, "WorldBurnEffectPrefix", null);
                    else if (name == "Revive")
                        PatchWorldMethod(method, null, "WorldMutationPostfix");
                    else if (name == "Collapse" || name == "CollapseForced" || name == "ActuallyCollapse" ||
                        name == "DestroyBlockInternal")
                        PatchWorldMethod(method, "WorldCollapsePrefix", "WorldMutationPostfix");
                    else if (name == "RunMovement" && typeof(Unit).IsAssignableFrom(type))
                        _harmony.Patch(method,
                            new HarmonyMethod(typeof(HarmonyDiagnostics).GetMethod("WorldMovementPrefix", BindingFlags.NonPublic | BindingFlags.Static)),
                            null, null, new HarmonyMethod(typeof(HarmonyDiagnostics).GetMethod("WorldMovementFinalizer", BindingFlags.NonPublic | BindingFlags.Static)));
                    else if (name == "Update" && typeof(Pickupable).IsAssignableFrom(type))
                        _harmony.Patch(method,
                            new HarmonyMethod(typeof(HarmonyDiagnostics).GetMethod("WorldPickupUpdatePrefix", BindingFlags.NonPublic | BindingFlags.Static)),
                            null, new HarmonyMethod(typeof(HarmonyDiagnostics).GetMethod("WorldPickupVisibilityTranspiler", BindingFlags.NonPublic | BindingFlags.Static)), null);
                    else if (name == "ChangeFrame" && typeof(TestVanDammeAnim).IsAssignableFrom(type))
                        PatchWorldMethod(method, "WorldPoseAnimationPrefix", "WorldElectrifiedPosePostfix");
                    else if (name == "CreateGibEffects" && typeof(TestVanDammeAnim).IsAssignableFrom(type))
                        PatchWorldMethod(method, "WorldGibEffectsPrefix", null);
                    else if ((name == "Update" || name == "FixedUpdate") && typeof(Projectile).IsAssignableFrom(type))
                        PatchWorldDamageContext(method, "WorldProjectileUpdatePrefix");
                    else if (name == "Fire" && typeof(Projectile).IsAssignableFrom(type))
                        PatchWorldDamageContext(method, "WorldProjectileFirePrefix");
                    else if (name == "CheckSpawnPoint" && typeof(Projectile).IsAssignableFrom(type))
                        PatchWorldMethod(method, "WorldProjectileSpawnCheckPrefix", null);
                    else if (name == "MakeEffects" && method.GetParameters().Length == 6 && typeof(Projectile).IsAssignableFrom(type))
                        PatchWorldDamageContext(method, "WorldProjectileEffectsPrefix");
                    else if (name == "MakeEffects" && method.GetParameters().Length == 0 && typeof(Grenade).IsAssignableFrom(type))
                        _harmony.Patch(method, new HarmonyMethod(AccessTools.Method(typeof(HarmonyDiagnostics), "WorldGrenadeEffectsPrefix", null, null)),
                            new HarmonyMethod(AccessTools.Method(typeof(HarmonyDiagnostics), "WorldGrenadeEffectsPostfix", null, null)), null,
                            new HarmonyMethod(AccessTools.Method(typeof(HarmonyDiagnostics), "WorldGrenadeEffectsFinalizer", null, null)));
                    else if (name == "Update" && typeof(Grenade).IsAssignableFrom(type))
                        _harmony.Patch(method, new HarmonyMethod(AccessTools.Method(typeof(HarmonyDiagnostics), "WorldGrenadeUpdatePrefix", null, null)),
                            null, null, new HarmonyMethod(AccessTools.Method(typeof(HarmonyDiagnostics), "WorldGrenadeUpdateFinalizer", null, null)));
                    else if ((name == "Panic" || name == "Blind" || name == "Terrify" || name == "TearGas" || name == "Stun" || name == "Freeze") && typeof(Unit).IsAssignableFrom(type))
                        PatchWorldMethod(method, "WorldStatusPrefix", "WorldMutationPostfix");
                    else if (name == "MakeEffects" && method.GetParameters().Length == 0 &&
                        (typeof(MookSuicide).IsAssignableFrom(type) || typeof(MookSuicideUndead).IsAssignableFrom(type)))
                        PatchWorldDamageContext(method, "WorldSuicideEffectsPrefix");
                }
            }
            // Copy only the base visual method: overrides can drop grenades or deal explosion damage.
            _harmony.CreateReversePatcher(typeof(TestVanDammeAnim).GetMethod("CreateGibEffects", WorldFields | BindingFlags.DeclaredOnly),
                new HarmonyMethod(typeof(HarmonyDiagnostics).GetMethod("WorldBaseGibEffects", BindingFlags.NonPublic | BindingFlags.Static)))
                .Patch(HarmonyReversePatchType.Original);
            PatchWorldMethod(typeof(DogAI).GetMethod("IsAtActionGridpoint", WorldFields | BindingFlags.DeclaredOnly), null, "WorldDogArrivalPostfix");
            PatchWorldMethod(typeof(Registry).GetMethod("RegisterObject", BindingFlags.Public | BindingFlags.Static), null, "WorldRegisterPostfix");
            PatchWorldMethod(typeof(Registry).GetMethod("Reset", BindingFlags.Public | BindingFlags.Static), null, "ClearEntityFinalStateSynchronizationState");
            PatchWorldMethod(typeof(NetworkObject).GetMethod("OnDestroy", WorldFields), "WorldNetworkDestroyPrefix", null);
            PatchWorldMethod(typeof(NetworkObject).GetMethod("RunSync", WorldFields), "WorldNativeSyncPrefix", null);
            PatchWorldMethod(typeof(NetworkObject).GetMethod("AddState", WorldFields), "WorldNativeStatePrefix", null);
            PatchWorldMethod(WorldDisarmShield, "WorldShieldDisarmPrefix", "WorldShieldDisarmPostfix");
            PatchWorldMethod(typeof(Unit).GetMethod("DeathRPC", WorldFields), "WorldDeathRpcPrefix", null);
            PatchWorldMethod(typeof(Unit).GetMethod("QuietDeathRPC", WorldFields), "WorldDeathRpcPrefix", null);
            foreach (var type in new[] { typeof(Map), typeof(MapController) })
                foreach (var method in type.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static))
                    if (method.Name.StartsWith("Hit", StringComparison.Ordinal) ||
                        method.Name.StartsWith("Damage", StringComparison.Ordinal) ||
                        method.Name.StartsWith("Burn", StringComparison.Ordinal) ||
                        method.Name == "ExplodeUnits" || method.Name == "KnockAndDamageUnit" ||
                        method.Name == "TearGasUnits" || method.Name == "FreezeUnits" || method.Name == "PanicUnits" || method.Name == "BlindUnits")
                        PatchWorldMethod(method, "WorldEffectPrefix", null);
            PatchWorldProjectiles();
            PatchWorldReplayGeneration();
            PatchWorldHitEffects();
            PatchWorldHitPresentation();
            PatchWorldDoodads();
            PatchWorldPersistentStatus();
            PatchMariachiSynchronization();
            PatchBroveHeartSynchronization();
            PatchWorldMethod(AccessTools.Method(typeof(RPCController), "SendRPC", null, null), "WorldBossReplayRpcPrefix", null);
            PatchWorldMethod(typeof(RPCController).GetMethod("CreateRPC", BindingFlags.Public | BindingFlags.Static),
                "WorldBlockRpcPrefix", null);
            PatchWorldMethod(AccessTools.Method(typeof(Block), "CheckForMine", null, null), "WorldCheckForMinePrefix", null);
            _harmony.Patch(AccessTools.Method(typeof(Mine), "Explode", null, null),
                new HarmonyMethod(AccessTools.Method(typeof(HarmonyDiagnostics), "WorldMineExplosionPrefix", null, null)), null, null,
                new HarmonyMethod(AccessTools.Method(typeof(HarmonyDiagnostics), "WorldMineExplosionFinalizer", null, null)));
            DiagnosticLog.Info("WORLD_STATE authoritative object state synchronization enabled; protocol=" + WorldProtocol + ".");
        }

        private static void WorldBlockRpcPrefix(ref object targetObject, ref MethodInfo methodInfo, ref object[] parameters, ref bool immediate)
        {
            // A grenade can detonate during Launch. Its native instantiate/setup must
            // execute before the immediate effect/removal events on the same stream.
            if (_worldNetworkGrenadeSpawnDepth > 0) immediate = true;
            if (_worldEpoch != 0 && IsWorldSession() && targetObject is RemoteControlExplosiveCar &&
                methodInfo.Name == "MakeEffects")
            {
                // Play the native car explosion before its immediate removal snapshot
                // retires the replica and makes the interpolated effect RPC lose its target.
                immediate = true;
            }
            if (_worldEpoch != 0 && IsWorldSession() && methodInfo.Name == "ReceiveWorldOwnedHitPresentationRPC")
            {
                immediate = true;
                _worldHitMessagesPending = true;
            }
            if (_worldEpoch != 0 && IsWorldSession() && IsWorldBossPart(targetObject as BroforceObject))
            {
                immediate = true;
                _worldHitMessagesPending = true;
            }
            if (_worldEpoch != 0 && IsWorldSession() && targetObject is Block &&
                (methodInfo.Name == "ActuallyCollapse" || methodInfo.Name == "DestroyBlockInternal"))
            {
                // Native collapse includes debris and crate effects. Execute it in
                // send order before an immediate state snapshot marks it destroyed.
                immediate = true;
                PreserveWorldMineCountdown(ref targetObject, ref methodInfo, ref parameters);
            }
        }

        private static WorldEntry RegisterWorldObject(BroforceObject obj)
        {
            if (!IsWorldObject(obj)) return null;
            WorldEntry entry;
            if (WorldEntries.TryGetValue(obj.Nid, out entry))
            {
                if (entry.Object == null)
                {
                    entry.Object = obj;
                    entry.Transient = obj is Projectile || obj is Grenade;
                    if (!WorldCandidates.Contains(entry)) WorldCandidates.Add(entry);
                    if (obj is Unit && !WorldUnits.Contains(entry)) WorldUnits.Add(entry);
                    QueueWorldRegistration(entry);
                }
                return entry;
            }
            entry = new WorldEntry { Object = obj, Id = obj.Nid, Transient = obj is Projectile || obj is Grenade };
            WorldEntries.Add(entry.Id, entry);
            WorldCandidates.Add(entry);
            if (obj is Unit) WorldUnits.Add(entry);
            QueueWorldRegistration(entry);
            return entry;
        }

        private static void WorldRegisterPostfix(object comp, bool __result)
        {
            if (!__result) return;
            var obj = comp as BroforceObject;
            var hero = obj as Unit;
            if (hero != null && hero.IsHero) PatchWorldHeroStatusContext(hero.GetType());
            // Ordinary static blocks are enrolled only when changed.
            if (obj != null && (!(obj is Block) || obj is FallingBlock || WorldEntries.ContainsKey(obj.Nid))) RegisterWorldObject(obj);
        }

        private static bool WorldMutationPrefix(BroforceObject __instance, MethodBase __originalMethod)
        {
            if (_worldGrenadeReplicaUpdate > 0 && __instance is Grenade) return false;
            if (_worldApplying != 0)
                return __originalMethod.Name != "Damage" && __originalMethod.Name != "DamageInternal" &&
                    __originalMethod.Name != "DamageOptional" && __originalMethod.Name != "Burn" &&
                    __originalMethod.Name != "BurnInternal" && __originalMethod.Name != "ForceBurn" &&
                    __originalMethod.Name != "SetAlight_Local";
            if (!IsWorldSession() || _worldEpoch == 0 || !IsWorldObject(__instance)) return true;
            // Bosses already broadcast native phase/lifecycle RPCs. Accept those
            // only from their owner; damage itself still settles only there.
            if (IsWorldBossPart(__instance) && __originalMethod.Name != "Damage" &&
                __originalMethod.Name != "DamageInternal" && __originalMethod.Name != "DamageOptional" &&
                RPCController.CurrentRPC != null && RPCController.CurrentRPC.messageInfo.Sender ==
                (__instance.Owner == PID.TargetServer ? PID.ServerID : __instance.Owner)) return true;
            RegisterWorldObject(__instance).Dirty = true;
            // Collection already runs once per peer through the native Collect RPC.
            // Its local disappearance must not be blocked by target ownership.
            if (__instance is Pickupable && __originalMethod.Name == "Death") return true;
            // DamagePolicy deliberately simulates remote player attacks on the target's owner.
            // Settle there once; replicas receive the result rather than executing a second death/explosion.
            if (!__instance.IsMine) return false;
            var unit = __instance as Unit;
            if (unit != null && (__originalMethod.Name == "Death" || __originalMethod.Name == "QuietDeath"))
                unit.deathNotificationSent = true;
            return true;
        }

        private static bool WorldCollapsePrefix(BroforceObject __instance)
        {
            if (_worldApplying != 0) return true;
            // Keep native reliable map-collapse RPCs; the versioned state also repairs missed registration.
            if (IsWorldSession() && IsWorldObject(__instance))
            {
                RegisterWorldObject(__instance).Dirty = true;
                if (_worldEpoch != 0 && !__instance.IsMine)
                {
                    var rpc = RPCController.CurrentRPC;
                    var owner = __instance.Owner == PID.TargetServer ? PID.ServerID : __instance.Owner;
                    return rpc != null && rpc.messageInfo.Sender == owner;
                }
            }
            return true;
        }

        private static bool WorldEffectPrefix()
        {
            // Lifecycle replay updates local collision/animation, never repeats world damage or knockback.
            return _worldApplying == 0;
        }

        private static void WorldMutationPostfix(BroforceObject __instance, MethodBase __originalMethod)
        {
            var block = __instance as Block;
            if (__originalMethod.Name == "DestroyBlockInternal" && block != null && block.destroyed && IsWorldReplica(block))
                RefreshWorldBlockEdges(block);
            if (_worldApplying == 0 && IsWorldSession() && IsWorldObject(__instance))
            {
                // SetAlight_Local and Burn do not call the native DoEnabling.
                if (block != null && block.burnTime > 0f) block.enabled = true;
                RegisterWorldObject(__instance).Dirty = true;
            }
        }

        private static void RefreshWorldBlockEdges(Block block)
        {
            // Replica collapse uses collapseBlocksAround=false to avoid simulating another chain.
            // Restore the omitted foreground borders without disturbing supports or oil tanks.
            if (Map.GetBlock(block.collumn, block.row) != block) return;
            var below = Map.GetBlock(block.collumn, block.row - 1);
            var above = Map.GetBlock(block.collumn, block.row + 1);
            var left = Map.GetBlock(block.collumn - 1, block.row);
            var right = Map.GetBlock(block.collumn + 1, block.row);
            // Dirt/brick Show*Edge can create another background piece for a dead neighbour.
            if (below != null && !below.destroyed && below.health > 0) below.ShowTopEdge();
            if (above != null && !above.destroyed && above.health > 0) above.ShowBottomEdge();
            if (left != null && !left.destroyed && left.health > 0) left.ShowRightEdge();
            if (right != null && !right.destroyed && right.health > 0) right.ShowLeftEdge();
        }

        private static bool WorldMovementPrefix(BroforceObject __instance, out bool __state)
        {
            __state = false;
            if (!IsWorldReplica(__instance)) return true;
            // Existing corpses can gib immediately, but their movement callbacks
            // must not independently gib/detonate them while waiting for the owner.
            if (__instance.health <= 0) return false;
            WorldEntry entry;
            if (!WorldEntries.TryGetValue(__instance.Nid, out entry) || entry.PendingHit == 0 || Time.unscaledTime >= entry.PendingHitUntil) return false;
            // Native collision/gravity presents the local knock, without allowing
            // movement callbacks to deal damage or generate another world explosion.
            __state = true;
            _worldApplying++;
            return true;
        }

        private static void WorldMovementFinalizer(bool __state)
        {
            if (__state) _worldApplying--;
        }

        private static void WorldDogArrivalPostfix(DogAI __instance, ActionObject __0, ref bool __result)
        {
            if (!__result || _worldEpoch == 0 || !IsWorldSession() || !__instance.IsMine ||
                __0.type != EnemyActionType.Move || !(bool)WorldDogCorpse.GetValue(__instance)) return;
            var unit = __instance.GetComponent<Unit>();
            // Eating requires the same grid cell; the usual HalfWidth arrival tolerance can stop outside it.
            __result = unit.collumn == __0.gridPoint.collumn && unit.row == __0.gridPoint.row;
        }

        private static void WorldGibEffectsPrefix(TestVanDammeAnim __instance, DamageType damageType, float xI, float yI)
        {
            if (_worldApplying != 0 || _worldEpoch == 0 || !IsWorldSession() ||
                !IsWorldObject(__instance) || !__instance.IsMine || __instance.destroyed) return;
            var entry = RegisterWorldObject(__instance);
            entry.Gibbed = true; entry.GibType = damageType; entry.GibX = xI; entry.GibY = yI;
            entry.Dirty = true;
        }

        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
        private static void WorldBaseGibEffects(TestVanDammeAnim instance, DamageType damageType, float xI, float yI)
        {
            throw new NotSupportedException("The base gib visual method must be reverse patched before use.");
        }

        private static bool WorldPoseAnimationPrefix(TestVanDammeAnim __instance)
        {
            if (_worldApplying != 0 || !IsWorldReplica(__instance)) return true;
            WorldEntry entry;
            if (WorldEntries.TryGetValue(__instance.Nid, out entry) && entry.Presentation != null &&
                (entry.Presentation.Flags & WorldFlags.Dead) != 0)
            {
                ShowWorldPredictedCorpse(__instance);
                return false;
            }
            if (!WorldEntries.TryGetValue(__instance.Nid, out entry) || entry.Last == null ||
                (entry.Last.Flags & WorldFlags.SpritePose) == 0) return true;
            // Preserve the owner's corpse or electrified pose instead of choosing a local animation.
            ApplyWorldPose(entry, entry.Last);
            return false;
        }

        private static bool HasWorldElectrifiedPose(TestVanDammeAnim unit)
        {
            var mook = unit as Mook;
            return mook != null && mook.showElectrifiedFrames &&
                ((float)WorldPlasmaCounter.GetValue(unit) > 0f || (float)WorldShockCounter.GetValue(unit) > 0f ||
                (unit.health <= 0 && (int)WorldPlasmaDamage.GetValue(unit) > 0));
        }

        private static void WorldElectrifiedPosePostfix(TestVanDammeAnim __instance, bool __runOriginal)
        {
            if (!__runOriginal || _worldApplying != 0 || _worldEpoch == 0 || !IsWorldSession() ||
                !(__instance is Mook) || !IsWorldObject(__instance) || !__instance.IsMine) return;
            var entry = RegisterWorldObject(__instance);
            var previous = entry.Last;
            var electrified = HasWorldElectrifiedPose(__instance);
            if (!electrified && (previous == null || !previous.Electrified)) return;
            if (entry.Sprite == null) entry.Sprite = (SpriteSM)WorldUnitSprite.GetValue(__instance);
            var gun = (SpriteSM)WorldUnitGunSprite.GetValue(__instance);
            // Base and override postfixes can observe the same pose. Reject it before allocating a snapshot.
            if (previous != null && previous.Electrified == electrified && entry.Sprite != null &&
                previous.PosePixel == entry.Sprite.lowerLeftPixel && previous.PoseOffset == entry.Sprite.offset &&
                previous.PoseGunActive == (gun != null && gun.gameObject.activeSelf)) return;
            var state = CaptureWorldState(entry);
            // The flash alternates every .033 seconds. The 12 Hz movement tick would skip its frames.
            // Use the existing versioned state channel at each actual visual change, including its end.
            state.Revision = ++_worldSequence;
            entry.Last = state;
            entry.SentAt = Time.unscaledTime;
            SendWorldStates(new[] { state }, PID.TargetOthers, true);
        }

        private static bool WorldNativeSyncPrefix(NetworkObject __instance)
        {
            if (IsWorldBossPart(__instance as BroforceObject)) return true;
            if (!IsWorldReplica(__instance as BroforceObject)) return true;
            // RunSync normally drains this queue. Do not keep sorting states we no longer consume.
            __instance.states.Clear();
            return false;
        }

        private static bool WorldShieldDisarmPrefix(MookRiotShield __instance, out bool __state)
        {
            WorldEntry entry;
            __state = _worldApplying != 0 && WorldEntries.TryGetValue(__instance.Nid, out entry) &&
                entry.Presentation != null && entry.Presentation.ShieldLost;
            if (__state) _worldSuppressShieldDebris++;
            return _worldApplying != 0 || !IsWorldReplica(__instance);
        }

        private static void WorldShieldDisarmPostfix(MookRiotShield __instance, bool __state)
        {
            if (__state) _worldSuppressShieldDebris--;
            WorldMutationPostfix(__instance, WorldDisarmShield);
        }

        private static bool WorldDeathRpcPrefix(Unit __instance)
        {
            return _worldEpoch == 0 || !IsWorldSession() || !IsWorldObject(__instance);
        }

        private static void WorldDestroyPrefix(NetworkObject __instance)
        {
            var obj = __instance as BroforceObject;
            if (_worldApplying != 0 || _worldEpoch == 0 || !IsWorldSession() || !IsWorldObject(obj) || !obj.IsMine) return;
            var entry = RegisterWorldObject(obj);
            // Overrides which do call base can pass through several patched OnDestroy methods.
            if (entry.Last != null && (entry.Last.Flags & WorldFlags.Removed) != 0) return;
            var spear = obj as PredabroSpear;
            if (spear != null) SendWorldSpearTermination(spear, entry);
            entry.Last = CaptureWorldState(entry);
            entry.Last.Flags |= WorldFlags.Removed;
            entry.Last.Revision = ++_worldSequence;
            RecordWorldRemoval(entry.Id);
            SendWorldStates(new[] { entry.Last }, PID.TargetOthers, true);
        }

        private static void RecordWorldRemoval(NID id)
        {
            WorldRemovedObjects.Add(id);
            // Spawn descriptors use a GameObject ID (component 0). State snapshots
            // use the full component ID: removing a switch must not retire its villager.
            WorldRemovedObjects.Add(id.BaseID);
        }

        private static WorldSnapshot CaptureWorldState(WorldEntry entry)
        {
            var obj = entry.Object;
            var state = new WorldSnapshot
            {
                Id = entry.Id, Type = obj.GetType().Name, Owner = obj.Owner.AsInt, Life = entry.Life,
                SentAt = Time.unscaledTime,
                X = obj.X, Y = obj.Y, VX = obj.xI, VY = obj.yI,
                Scale = obj.transform.localScale.x, Rotation = obj.transform.eulerAngles.z,
                Health = obj.health, MaxHealth = obj.maxHealth,
                Parent = NID.NoID, Pilot = NID.NoID, ReviveSource = NID.NoID
            };
            if (obj.destroyed) state.Flags |= WorldFlags.Destroyed;
            if (entry.Exploded) state.Flags |= WorldFlags.Exploded;
            if (obj.collapsed) state.Flags |= WorldFlags.Collapsed;
            if (entry.Gibbed)
            {
                // Preserve the cause in the tombstone; destruction may happen before the next send tick.
                state.Flags |= WorldFlags.Gibbed | WorldFlags.Removed;
                state.GibType = entry.GibType; state.GibX = entry.GibX; state.GibY = entry.GibY;
            }
            var unit = obj as Unit;
            if (unit != null)
            {
                state.Action = (int)unit.actionState;
                state.Player = unit.playerNum;
                if (unit.actionState == ActionState.Dead || unit.health <= 0) state.Flags |= WorldFlags.Dead;
                if ((bool)WorldZombie.GetValue(unit)) state.Flags |= WorldFlags.Zombie;
            }
            var tvd = obj as TestVanDammeAnim;
            if (tvd != null)
            {
                state.Input = tvd.InputBits;
                state.GroundHeight = tvd.groundHeight;
                state.Parent = Registry.GetNID(tvd.SyncParent);
                state.Pilot = Registry.GetNID(tvd.pilottedUnit);
                state.ReviveSource = Registry.GetNID(WorldReviveSource.GetValue(tvd));
                state.Electrified = HasWorldElectrifiedPose(tvd);
                CaptureWorldPersistentStatus(tvd, state);
                if ((state.Flags & WorldFlags.Dead) != 0 || state.Electrified || state.FrozenTime > 0f || state.Acid ||
                    state.Inseminator != NID.NoID || entry.Last != null && entry.Last.Electrified)
                {
                    if (entry.Sprite == null) entry.Sprite = (SpriteSM)WorldUnitSprite.GetValue(tvd);
                    if (entry.Sprite != null)
                    {
                        state.Flags |= WorldFlags.SpritePose;
                        state.PoseFrame = tvd.frame;
                        state.PoseBlastX = tvd.xIBlast; state.PoseBlastY = tvd.yIBlast;
                        state.PoseScaleY = tvd.transform.localScale.y;
                        state.PosePixel = entry.Sprite.lowerLeftPixel;
                        state.PoseOffset = entry.Sprite.offset;
                        var gun = (SpriteSM)WorldUnitGunSprite.GetValue(tvd);
                        state.PoseGunActive = gun != null && gun.gameObject.activeSelf;
                    }
                }
            }
            var dog = obj as MookDog;
            var mook = obj as Mook;
            if (mook != null)
            {
                state.BlindTime = Mathf.Max(0f, mook.blindTime);
                state.ScaredTime = Mathf.Max(0f, (float)WorldScaredTime.GetValue(mook));
                state.Mental = mook.enemyAI == null ? 0 : (int)mook.enemyAI.mentalState;
            }
            if (dog != null && dog.isMegaDog) state.Flags |= WorldFlags.Mega;
            var shield = obj as MookRiotShield;
            if (shield != null && shield.hasShield) state.Flags |= WorldFlags.Shield;
            var tank = obj as Tank;
            if (tank != null)
            {
                state.Facing = tank.facingDirection;
                state.Turn = (int)WorldTankTurn.GetValue(tank);
            }
            if (obj is MookArtilleryTruck) state.Unfold = (int)WorldTruckUnfold.GetValue(obj);
            var doodad = obj as Doodad;
            if (doodad != null && doodad.isDead) state.Flags |= WorldFlags.DoodadDead;
            var block = obj as Block;
            if (block != null)
            {
                state.Row = block.row; state.Column = block.collumn;
                if (block.disturbed) state.Flags |= WorldFlags.Disturbed;
            }
            var projectile = obj as Projectile;
            if (projectile != null) state.LifeTime = projectile.life;
            var grenade = obj as Grenade;
            if (grenade != null) state.LifeTime = grenade.life;
            var pickup = obj as Pickupable;
            if (pickup != null) CaptureWorldPickupState(pickup, state);
            CaptureWorldSpecialHitState(obj, state);
            CaptureWorldBurnState(obj, state);
            CaptureWorldDoodadState(obj, state);
            if (entry.Last != null && (entry.Last.Flags & WorldFlags.Dead) != 0 &&
                (state.Flags & WorldFlags.Dead) == 0) state.Life = ++entry.Life;
            return state;
        }

        private static bool WorldCriticalChanged(WorldSnapshot a, WorldSnapshot b)
        {
            return a == null || a.Owner != b.Owner || a.Flags != b.Flags || a.Life != b.Life ||
                a.Health != b.Health || a.MaxHealth != b.MaxHealth || a.Facing != b.Facing ||
                a.ArmourDamage != b.ArmourDamage || a.FireDamage != b.FireDamage || a.Invulnerable != b.Invulnerable || a.BossDamaged != b.BossDamaged ||
                a.GunnerEjected != b.GunnerEjected || WorldDoodadChanged(a, b) ||
                WorldPersistentStatusChanged(a, b) ||
                a.Electrified != b.Electrified ||
                a.BurnDamage != b.BurnDamage || a.Oily != b.Oily || (a.BurnTime > 0f) != (b.BurnTime > 0f) ||
                (a.BurnExplosion > 0f) != (b.BurnExplosion > 0f) ||
                a.Mental != b.Mental || (a.BlindTime > 0f) != (b.BlindTime > 0f) || (a.ScaredTime > 0f) != (b.ScaredTime > 0f) ||
                a.Parent != b.Parent || a.Pilot != b.Pilot || a.ReviveSource != b.ReviveSource || a.PickupType != b.PickupType;
        }

        private static bool WorldMotionChanged(WorldSnapshot a, WorldSnapshot b)
        {
            return a == null || Mathf.Abs(a.X - b.X) > 0.05f || Mathf.Abs(a.Y - b.Y) > 0.05f ||
                a.VX != b.VX || a.VY != b.VY || a.Scale != b.Scale || a.Action != b.Action ||
                a.Input != b.Input || a.Turn != b.Turn || a.Unfold != b.Unfold ||
                a.Row != b.Row || a.Column != b.Column || Mathf.Abs(a.Rotation - b.Rotation) > 0.1f ||
                a.GroundHeight != b.GroundHeight || a.BlindTime != b.BlindTime || a.ScaredTime != b.ScaredTime ||
                a.BurnTime != b.BurnTime || a.BurnExplosion != b.BurnExplosion ||
                a.PoseBlastX != b.PoseBlastX || a.PoseBlastY != b.PoseBlastY ||
                a.PoseScaleY != b.PoseScaleY || a.PosePixel != b.PosePixel || a.PoseOffset != b.PoseOffset ||
                a.PoseGunActive != b.PoseGunActive;
        }

        private static void UpdateWorldStateSynchronization()
        {
            if (!IsWorldSession()) return;
            var now = Time.unscaledTime;
            if (_worldServer != PID.ServerID.AsInt)
            {
                _worldServer = PID.ServerID.AsInt;
                _worldEpoch = 0;
                ClearWorldPersistentEffects();
                _worldRequestAt = 0f;
                _worldSpearSyncAt = 0f;
                _worldSpearLoggedFirstSend = false;
                _worldSpearLoggedFirstReceive = false;
                WorldPending.Clear();
                WorldRemovedObjects.Clear(); WorldRegistrations.Clear();
                foreach (var entry in WorldEntries.Values)
                {
                    entry.Last = null; entry.Dirty = true; ResetWorldProjectileMotion(entry);
                    entry.RegistrationFrame = -1; entry.DoodadEffectRevision = 0;
                    entry.SpearEnding = false; entry.SpearEffectFrame = -1;
                }
            }
            if (!_worldSeeded)
            {
                foreach (var value in Registry.Components.Values)
                {
                    var obj = value as BroforceObject;
                    var hero = obj as Unit;
                    if (hero != null && hero.IsHero) PatchWorldHeroStatusContext(hero.GetType());
                    if (obj != null && (!(obj is Block) || obj is FallingBlock)) RegisterWorldObject(obj);
                }
                _worldSeeded = true;
            }
            if (_worldEpoch == 0 && Connect.IsHost) _worldEpoch = DateTime.UtcNow.Ticks;
            if (!Connect.IsHost && _worldEpoch == 0 && now >= _worldRequestAt)
            {
                _worldRequestAt = now + 1f;
                global::Networking.Networking.RPC<int, PID, long>(PID.TargetServer,
                    new RpcSignature<int, PID, long>(RequestWorldBaselineRPC), WorldProtocol, PID.MyID, 0L, false);
            }
            if (_worldEpoch == 0) return;
            UpdateWorldRegistrations();
            UpdateWorldPersistentEffects();
            if (now < _worldSendAt) return;
            _worldSendAt = now + WorldSendInterval;
            var motion = WorldMotionBatch;
            var critical = WorldCriticalBatch;
            motion.Clear(); critical.Clear();
            var activeCount = 0;
            // The list can grow during a lifecycle callback; entries already captured remain valid.
            for (var i = 0; i < WorldCandidates.Count; i++)
            {
                var entry = WorldCandidates[i];
                var obj = entry.Object;
                if (entry.PendingHit != 0 && now >= entry.PendingHitUntil) EndWorldHitPresentation(entry);
                if (obj == null || entry.Last != null && (entry.Last.Flags & WorldFlags.Removed) != 0)
                {
                    if (entry.Transient)
                    {
                        // Keep the full lifecycle/version tombstone for native late registrations.
                        WorldProjectileSpawns.Remove(entry.Id.BaseID);
                        WorldGrenadeSpawns.Remove(entry.Id.BaseID);
                    }
                    continue;
                }
                WorldCandidates[activeCount++] = entry;
                if (!IsWorldObject(obj) || !obj.IsMine || IsCaptiveWorldVillager(obj) ||
                    (!entry.Dirty && now < entry.CheckAt)) continue;
                var moving = obj is Unit || entry.Transient || obj is FallingBlock && ((Block)obj).disturbed ||
                    obj is Pickupable && obj.gameObject.activeInHierarchy;
                entry.CheckAt = now + (moving ? WorldSendInterval : 0.5f);
                var state = CaptureWorldState(entry);
                var important = WorldCriticalChanged(entry.Last, state);
                if (important || WorldMotionChanged(entry.Last, state) || moving && now - entry.SentAt >= 1f)
                {
                    state.Revision = ++_worldSequence;
                    entry.Last = state;
                    entry.SentAt = now;
                    (important ? critical : motion).Add(state);
                }
                entry.Dirty = false;
            }
            // Compact once instead of shifting the remaining list for every removed object.
            if (activeCount < WorldCandidates.Count)
                WorldCandidates.RemoveRange(activeCount, WorldCandidates.Count - activeCount);
            SendWorldStates(critical, PID.TargetOthers, true);
            SendWorldStates(motion, PID.TargetOthers, false);
            UpdateWorldPendingProjectiles();
            UpdateWorldPendingGrenades();
            WorldPendingApplied.Clear();
            foreach (var pair in WorldPending)
            {
                if (TryApplyWorldState(pair.Value)) WorldPendingApplied.Add(pair.Key);
            }
            foreach (var id in WorldPendingApplied) WorldPending.Remove(id);
        }

        [AllowedRPC]
        private static void RequestWorldBaselineRPC(int protocol, PID requestee, long epoch)
        {
            if (protocol != WorldProtocol || !IsWorldSession()) return;
            if (Connect.IsHost && epoch == 0)
            {
                if (_worldEpoch == 0) _worldEpoch = DateTime.UtcNow.Ticks;
                global::Networking.Networking.RPC<long, byte>(requestee,
                    new RpcSignature<long, byte>(ReceiveWorldEpochRPC), _worldEpoch, Connect.SessionID.AsByte, false);
                return;
            }
            // TargetServer also runs this locally. Other owners answer the broadcast request after the epoch.
            if (_worldEpoch == 0 || epoch != _worldEpoch) return;
            SendWorldBaseline(requestee);
        }

        [AllowedRPC]
        private static void ReceiveWorldEpochRPC(long epoch, byte sid)
        {
            if (!IsWorldSession() || sid != Connect.SessionID.AsByte ||
                RPCController.CurrentRPC == null || RPCController.CurrentRPC.messageInfo.Sender != PID.ServerID ||
                epoch < _worldEpoch) return;
            if (_worldEpoch == epoch) return;
            ClearWorldPersistentEffects();
            _worldEpoch = epoch;
            _worldServer = PID.ServerID.AsInt;
            _worldSpearSyncAt = 0f;
            _worldSpearLoggedFirstSend = false;
            _worldSpearLoggedFirstReceive = false;
            WorldPending.Clear();
            WorldRemovedObjects.Clear(); WorldRegistrations.Clear();
            foreach (var entry in WorldEntries.Values)
            {
                entry.Last = null; entry.Dirty = true; entry.DeathLife = -1; ResetWorldProjectileMotion(entry);
                entry.RegistrationFrame = -1; entry.DoodadEffectRevision = 0;
                entry.SpearEnding = false; entry.SpearEffectFrame = -1;
            }
            global::Networking.Networking.RPC<int, PID, long>(PID.TargetOthers,
                new RpcSignature<int, PID, long>(RequestWorldBaselineRPC), WorldProtocol, PID.MyID, _worldEpoch, false);
            DiagnosticLog.InfoFileOnly("WORLD_STATE epoch received; candidates=" + WorldCandidates.Count + ".");
        }

        private static void SendWorldBaseline(PID target)
        {
            var states = new List<WorldSnapshot>();
            foreach (var entry in WorldEntries.Values)
            {
                if (entry.Object != null && entry.Object.IsMine && IsWorldObject(entry.Object) && !IsCaptiveWorldVillager(entry.Object))
                {
                    var state = CaptureWorldState(entry);
                    state.Revision = ++_worldSequence;
                    entry.Last = state;
                    states.Add(state);
                }
                else if (entry.Last != null && (entry.Last.Flags & WorldFlags.Removed) != 0 &&
                    (entry.Last.Owner == PID.MyID.AsInt || entry.Last.Owner == PID.TargetServer.AsInt && Connect.IsHost))
                    states.Add(entry.Last);
            }
            SendWorldProjectileSpawns(target);
            SendWorldGrenadeSpawns(target);
            SendWorldStates(states, target, true);
            SendWorldPersistentEffects(target);
        }

        private static void WriteWorldId(BinaryWriter writer, NID id)
        {
            writer.Write(id.Sid.AsByte); writer.Write((byte)id.OwnerID.AsInt);
            writer.Write(id.GameObjectID); writer.Write(id.ComponentID);
        }

        private static NID ReadWorldId(BinaryReader reader)
        {
            return new NID(new SID(reader.ReadByte()), new PID(reader.ReadByte()), reader.ReadUInt32(), reader.ReadUInt16());
        }

        private static void WriteWorldState(BinaryWriter w, WorldSnapshot s)
        {
            WriteWorldId(w, s.Id); w.Write(s.Type); w.Write(s.Revision); w.Write(s.SentAt); w.Write(s.Life); w.Write((byte)s.Owner);
            w.Write((ushort)s.Flags); w.Write(s.X); w.Write(s.Y); w.Write(s.VX); w.Write(s.VY);
            w.Write(s.Scale); w.Write(s.Rotation); w.Write(s.Health); w.Write(s.MaxHealth);
            w.Write(s.Action); w.Write(s.Facing); w.Write(s.Input); w.Write(s.Turn); w.Write(s.Unfold); w.Write(s.Player);
            w.Write(s.LifeTime); w.Write(s.Row); w.Write(s.Column); w.Write(s.GroundHeight);
            w.Write(s.BlindTime); w.Write(s.ScaredTime); w.Write(s.Mental);
            WriteWorldPersistentStatus(w, s);
            w.Write(s.ArmourDamage); w.Write(s.FireDamage); w.Write(s.ArmourKnocks); w.Write(s.ShieldTime); w.Write(s.Invulnerable); w.Write(s.BossDamaged);
            w.Write(s.GunnerEjected); w.Write(s.Electrified);
            w.Write(s.BurnTime); w.Write(s.BurnDamage); w.Write(s.Oily); w.Write(s.BurnExplosion);
            w.Write(s.DoodadVisual);
            if (s.DoodadVisual != 0)
            {
                w.Write(s.DoodadColliders); w.Write(s.DoodadMaterial);
                w.Write(s.DoodadPixel.x); w.Write(s.DoodadPixel.y);
                w.Write((ushort)(s.DoodadChildren == null ? 0 : s.DoodadChildren.Length));
                if (s.DoodadChildren != null) w.Write(s.DoodadChildren);
            }
            WriteWorldId(w, s.Parent); WriteWorldId(w, s.Pilot); WriteWorldId(w, s.ReviveSource);
            if ((s.Flags & WorldFlags.SpritePose) != 0)
            {
                w.Write(s.PoseFrame); w.Write(s.PoseBlastX); w.Write(s.PoseBlastY);
                w.Write(s.PoseScaleY); w.Write(s.PosePixel.x); w.Write(s.PosePixel.y);
                w.Write(s.PoseOffset.x); w.Write(s.PoseOffset.y); w.Write(s.PoseOffset.z);
                w.Write(s.PoseGunActive);
            }
            if ((s.Flags & WorldFlags.Gibbed) != 0)
            {
                w.Write((int)s.GibType); w.Write(s.GibX); w.Write(s.GibY);
            }
            if ((s.Flags & WorldFlags.Pickup) != 0)
            {
                w.Write(s.PickupType); w.Write(s.PickupDelay);
            }
        }

        private static WorldSnapshot ReadWorldState(BinaryReader r)
        {
            var s = new WorldSnapshot();
            s.Id = ReadWorldId(r); s.Type = r.ReadString(); s.Revision = r.ReadInt32(); s.SentAt = r.ReadSingle();
            s.Life = r.ReadInt32(); s.Owner = r.ReadByte();
            s.Flags = (WorldFlags)r.ReadUInt16(); s.X = r.ReadSingle(); s.Y = r.ReadSingle(); s.VX = r.ReadSingle(); s.VY = r.ReadSingle();
            s.Scale = r.ReadSingle(); s.Rotation = r.ReadSingle(); s.Health = r.ReadInt32(); s.MaxHealth = r.ReadInt32();
            s.Action = r.ReadInt32(); s.Facing = r.ReadInt32(); s.Input = r.ReadInt16(); s.Turn = r.ReadInt32(); s.Unfold = r.ReadInt32(); s.Player = r.ReadInt32();
            s.LifeTime = r.ReadSingle(); s.Row = r.ReadInt32(); s.Column = r.ReadInt32(); s.GroundHeight = r.ReadSingle();
            s.BlindTime = r.ReadSingle(); s.ScaredTime = r.ReadSingle(); s.Mental = r.ReadInt32();
            ReadWorldPersistentStatus(r, s);
            s.ArmourDamage = r.ReadInt32(); s.FireDamage = r.ReadInt32(); s.ArmourKnocks = r.ReadInt32(); s.ShieldTime = r.ReadSingle(); s.Invulnerable = r.ReadBoolean(); s.BossDamaged = r.ReadBoolean();
            s.GunnerEjected = r.ReadBoolean(); s.Electrified = r.ReadBoolean();
            s.BurnTime = r.ReadSingle(); s.BurnDamage = r.ReadInt32(); s.Oily = r.ReadBoolean(); s.BurnExplosion = r.ReadSingle();
            s.DoodadVisual = r.ReadByte();
            if (s.DoodadVisual != 0)
            {
                s.DoodadColliders = r.ReadByte(); s.DoodadMaterial = r.ReadByte();
                s.DoodadPixel = new Vector2(r.ReadSingle(), r.ReadSingle());
                s.DoodadChildren = r.ReadBytes(r.ReadUInt16());
            }
            s.Parent = ReadWorldId(r); s.Pilot = ReadWorldId(r); s.ReviveSource = ReadWorldId(r);
            if ((s.Flags & WorldFlags.SpritePose) != 0)
            {
                s.PoseFrame = r.ReadInt32();
                s.PoseBlastX = r.ReadSingle(); s.PoseBlastY = r.ReadSingle(); s.PoseScaleY = r.ReadSingle();
                s.PosePixel = new Vector2(r.ReadSingle(), r.ReadSingle());
                s.PoseOffset = new Vector3(r.ReadSingle(), r.ReadSingle(), r.ReadSingle());
                s.PoseGunActive = r.ReadBoolean();
            }
            if ((s.Flags & WorldFlags.Gibbed) != 0)
            {
                s.GibType = (DamageType)r.ReadInt32(); s.GibX = r.ReadSingle(); s.GibY = r.ReadSingle();
            }
            if ((s.Flags & WorldFlags.Pickup) != 0)
            {
                s.PickupType = r.ReadInt32(); s.PickupDelay = r.ReadSingle();
            }
            return s;
        }

        private static void SendWorldStates(IList<WorldSnapshot> states, PID target, bool reliable)
        {
            if (states.Count == 0) return;
            using (var stream = new MemoryStream())
            using (var writer = new BinaryWriter(stream))
            {
                for (var i = 0; i < states.Count; i++)
                {
                    WriteWorldState(writer, states[i]);
                    if (stream.Length < 1000 && i + 1 < states.Count) continue;
                    var bytes = CompressWorldStates(stream.ToArray());
                    if (reliable)
                        global::Networking.Networking.RPC<long, byte[]>(target,
                            new RpcSignature<long, byte[]>(ReceiveCompressedWorldStatesRPC), _worldEpoch, bytes, true);
                    else
                        global::Networking.Networking.UnreliableRPC<long, byte[]>(target, true, false,
                            new RpcSignature<long, byte[]>(ReceiveCompressedWorldStatesRPC), _worldEpoch, bytes);
                    stream.SetLength(0); stream.Position = 0;
                }
            }
        }

        private static byte[] CompressWorldStates(byte[] bytes)
        {
            using (var output = new MemoryStream())
            {
                using (var compressor = new DeflateStream(output, CompressionMode.Compress, true))
                    compressor.Write(bytes, 0, bytes.Length);
                return output.ToArray();
            }
        }

        [AllowedRPC]
        private static void ReceiveCompressedWorldStatesRPC(long epoch, byte[] bytes)
        {
            if (!IsWorldSession() || _worldEpoch == 0 || epoch != _worldEpoch || RPCController.CurrentRPC == null) return;
            using (var input = new MemoryStream(bytes))
            using (var decompressor = new DeflateStream(input, CompressionMode.Decompress))
            using (var output = new MemoryStream())
            {
                // Unity's Mono inflater can stop at input EOF with output still buffered.
                // Read the whole batch (1000 bytes plus one snapshot) in one buffer.
                var buffer = new byte[4096];
                int count;
                while ((count = decompressor.Read(buffer, 0, buffer.Length)) > 0)
                    output.Write(buffer, 0, count);
                ReceiveWorldStatesRPC(epoch, output.ToArray());
            }
        }

        [AllowedRPC]
        private static void ReceiveWorldStatesRPC(long epoch, byte[] bytes)
        {
            if (!IsWorldSession() || _worldEpoch == 0 || epoch != _worldEpoch || RPCController.CurrentRPC == null) return;
            var sender = RPCController.CurrentRPC.messageInfo.Sender;
            using (var stream = new MemoryStream(bytes))
            using (var reader = new BinaryReader(stream))
            {
                while (stream.Position < stream.Length)
                {
                    var state = ReadWorldState(reader);
                    state.StatusReceivedAt = Time.time;
                    if (state.Id.Sid != Connect.SessionID ||
                        (state.Owner == PID.TargetServer.AsInt ? sender != PID.ServerID : sender.AsInt != state.Owner)) continue;
                    if (!TryApplyWorldState(state) && WorldPending.Count < 4096)
                    {
                        WorldSnapshot previous;
                        if (!WorldPending.TryGetValue(state.Id, out previous) || previous.Life < state.Life ||
                            previous.Life == state.Life && previous.Revision <= state.Revision)
                            WorldPending[state.Id] = state;
                    }
                }
            }
        }

        private static bool TryApplyWorldState(WorldSnapshot state)
        {
            WorldEntry entry;
            if (!WorldEntries.TryGetValue(state.Id, out entry))
            {
                var registered = Registry.GetObject(state.Id) as BroforceObject;
                if (registered == null)
                {
                    if ((state.Flags & WorldFlags.Removed) == 0) return false;
                    // Retain the tombstone even when destruction beat registration.
                    entry = new WorldEntry { Id = state.Id };
                    WorldEntries.Add(state.Id, entry); WorldCandidates.Add(entry);
                }
                else entry = RegisterWorldObject(registered);
            }
            if (entry == null || state.Life < entry.Life || entry.Last != null &&
                (entry.Last.Life > state.Life || entry.Last.Life == state.Life && entry.Last.Revision >= state.Revision)) return true;
            var obj = entry.Object;
            if (obj != null && entry.RegistrationFrame >= 0) return false;
            if (obj != null && (!IsWorldObject(obj) || obj.IsMine || obj.GetType().Name != state.Type || obj.Owner.AsInt != state.Owner)) return true;
            if (entry.Last != null && (entry.Last.Flags & WorldFlags.Removed) != 0 &&
                (entry.Last.Type != state.Type || entry.Last.Owner != state.Owner ||
                 state.Life == entry.Last.Life && (state.Flags & WorldFlags.Removed) == 0)) return true;
            if (state.Life > entry.Life)
            {
                entry.Life = state.Life;
                WorldRemovedObjects.Remove(state.Id); WorldRemovedObjects.Remove(state.Id.BaseID);
                Registry.DestroyedMapObjects.Remove(state.Id);
                entry.DoodadEffectRevision = 0;
            }
            if (entry.SpearEnding && obj is PredabroSpear && (state.Flags & WorldFlags.Removed) == 0) return true;
            if (obj == null && (state.Flags & WorldFlags.Removed) == 0) return false;
            if ((state.Flags & WorldFlags.Removed) == 0 && obj is TestVanDammeAnim && !ResolveWorldInseminator(state)) return false;
            if (WaitForWorldGibConfirmation(entry, state))
            {
                entry.Last = state;
                entry.ReceivedAt = Time.unscaledTime;
                return true;
            }
            _worldApplying++;
            try
            {
                if (obj != null)
                {
                    if ((state.Flags & (WorldFlags.Dead | WorldFlags.Gibbed | WorldFlags.Destroyed | WorldFlags.Removed)) != 0)
                    {
                        FinishWorldHitPresentation(entry, state);
                        entry.PendingHit = 0;
                    }
                    if ((state.Flags & WorldFlags.Exploded) != 0 &&
                        (entry.Last == null || (entry.Last.Flags & WorldFlags.Exploded) == 0))
                        ReplayWorldSuicideEffects(obj, state);
                    if ((state.Flags & WorldFlags.Removed) != 0)
                    {
                        RecordWorldRemoval(state.Id);
                        var spear = obj as PredabroSpear;
                        if (spear != null)
                            ApplyWorldSpearTermination(entry, spear, state.X, state.Y, state.Revision);
                        var block = obj as Block;
                        if (block != null && !block.destroyed) block.DestroyBlockInternal(false);
                        var unit = obj as Unit;
                        if (unit != null)
                        {
                            var tvd = unit as TestVanDammeAnim;
                            if ((state.Flags & WorldFlags.Gibbed) != 0 && tvd != null && !tvd.destroyed &&
                                (entry.PresentedFlags & WorldFlags.Gibbed) == 0)
                            {
                                tvd.SetPosition(new Vector3(state.X, state.Y, tvd.transform.position.z));
                                var dog = tvd as MookDog;
                                if (dog != null && (state.Flags & WorldFlags.Mega) != 0) dog.gibs = dog.megaDogGibs;
                                WorldBaseGibEffects(tvd, state.GibType, state.GibX, state.GibY);
                            }
                            unit.destroyed = true;
                            Map.RemoveUnit(unit);
                        }
                        RemoveLateWorldObject(obj);
                    }
                    else if (!ApplyWorldObjectState(entry, state)) return false;
                }
                else if ((state.Flags & WorldFlags.Removed) != 0) RecordWorldRemoval(state.Id);
                var receivedAt = Time.unscaledTime;
                if (obj is Projectile && (state.Flags & WorldFlags.Removed) == 0)
                    RecordWorldProjectileMotion(entry, state, receivedAt);
                entry.Last = state;
                entry.Life = state.Life;
                if (state.Health > 0 && (state.Flags & (WorldFlags.Dead | WorldFlags.Gibbed | WorldFlags.Destroyed | WorldFlags.Removed)) == 0)
                    entry.PresentedFlags = WorldFlags.None;
                entry.ReceivedAt = receivedAt;
            }
            finally { _worldApplying--; }
            return true;
        }

        private static bool ApplyWorldObjectState(WorldEntry entry, WorldSnapshot state)
        {
            var obj = entry.Object;
            // Retry after the native release RPC; an early active snapshot must not
            // move the captive or seed interpolation from uninitialized coordinates.
            if (IsCaptiveWorldVillager(obj)) return false;
            ApplyWorldSpecialHitState(obj, state);
            ApplyWorldBurnState(obj, state);
            if (IsWorldBossPart(obj))
            {
                // Animated boss parts retain their native hierarchy and motion sync.
                // Only confirmed health/lifecycle is replayed here, never Damage.
                obj.health = state.Health; obj.maxHealth = state.MaxHealth;
                if ((state.Flags & WorldFlags.Dead) != 0 && !(bool)WorldHitField(obj, "isDead").GetValue(obj))
                    AccessTools.Method(obj.GetType(), "Death", Type.EmptyTypes, null).Invoke(obj, null);
                return true;
            }
            var predictedDrop = entry.Presentation != null && entry.Presentation.Crate != null;
            if (predictedDrop)
            {
                if ((state.Flags & (WorldFlags.PickupActive | WorldFlags.PickupCollected)) == 0) return true;
                entry.Presentation = null;
            }
            if (entry.PendingHit != 0)
            {
                if ((state.Flags & (WorldFlags.Dead | WorldFlags.Gibbed | WorldFlags.Removed | WorldFlags.Destroyed)) == 0)
                {
                    // Keep receiving health and revisions, but an older motion
                    // packet must not erase a knock still awaiting its result.
                    obj.health = state.Health; obj.maxHealth = state.MaxHealth;
                    return true;
                }
                entry.PendingHit = 0;
            }
            entry.LerpFrom = new Vector2(obj.X, obj.Y);
            entry.LerpRotation = obj.transform.eulerAngles.z;
            var pickup = obj as Pickupable;
            var smoothPickup = pickup != null && pickup.gameObject.activeSelf && (predictedDrop || entry.Last != null &&
                (entry.Last.Flags & WorldFlags.PickupActive) != 0);
            if (!(obj is Projectile) && !(obj is Pickupable) || entry.Last == null)
                obj.SetPosition(new Vector3(state.X, state.Y, obj.transform.position.z));
            var dog = obj as MookDog;
            if (dog != null && (state.Flags & WorldFlags.Mega) != 0 && !dog.isMegaDog) dog.TransformIntoMegaDog();
            var unit = obj as Unit;
            var tvd = obj as TestVanDammeAnim;
            var shield = obj as MookRiotShield;
            if (shield != null && shield.hasShield && (state.Flags & WorldFlags.Shield) == 0)
                WorldDisarmShield.Invoke(shield, new object[] { state.VX });
            if (unit != null)
            {
                if ((state.Flags & WorldFlags.Dead) != 0 && entry.DeathLife != state.Life)
                {
                    // Native AI Sync can set actionState before the lifecycle message arrives.
                    // DeathLife, rather than that visual state, tracks whether initialization ran.
                    unit.health = state.Health;
                    unit.deathNotificationSent = true;
                    unit.Death(state.VX, state.VY, new DamageObject(0, DamageType.Normal, state.VX, state.VY, state.X, state.Y, null));
                    entry.DeathLife = state.Life;
                }
                else if ((state.Flags & WorldFlags.Dead) == 0 && unit.actionState == ActionState.Dead && tvd != null)
                {
                    var source = Registry.GetObject(state.ReviveSource) as TestVanDammeAnim;
                    if (source == null) return false;
                    tvd.Revive(state.Player, false, source);
                    entry.DeathLife = -1;
                }
                unit.actionState = (ActionState)state.Action;
                WorldZombie.SetValue(unit, (state.Flags & WorldFlags.Zombie) != 0);
                ApplyWorldStatus(unit, state, 0f);
            }
            if (tvd != null)
            {
                tvd.InputBits = state.Input;
                tvd.groundHeight = state.GroundHeight;
                tvd.SyncParent = Registry.GetObject(state.Parent) as Transform;
                if (state.Pilot != NID.NoID && (tvd.pilottedUnit == null || tvd.pilottedUnit.Nid != state.Pilot))
                {
                    var pilot = Registry.GetObject(state.Pilot) as Unit;
                    if (pilot != null) tvd.StartPilotingUnit(pilot);
                }
            }
            var tank = obj as Tank;
            if (tank != null)
            {
                tank.facingDirection = state.Facing;
                WorldTankTurn.SetValue(tank, state.Turn);
                WorldTankDead.SetValue(tank, (state.Flags & WorldFlags.Dead) != 0);
            }
            if (obj is MookArtilleryTruck) WorldTruckUnfold.SetValue(obj, state.Unfold);
            var doodad = obj as Doodad;
            if (doodad != null)
            {
                if ((state.Flags & WorldFlags.DoodadDead) != 0 && !doodad.isDead) doodad.Death();
                if ((state.Flags & WorldFlags.Collapsed) != 0 && !doodad.collapsed) doodad.Collapse();
                ApplyWorldDoodadState(doodad, state);
            }
            var block = obj as Block;
            if (block != null)
            {
                if ((state.Flags & WorldFlags.Destroyed) != 0)
                {
                    if (!block.destroyed) block.DestroyBlockInternal(false);
                    else if (entry.Last == null || (entry.Last.Flags & WorldFlags.Destroyed) == 0)
                        RefreshWorldBlockEdges(block);
                }
                var falling = block as FallingBlock;
                if (falling != null && !block.destroyed && (block.row != state.Row || block.collumn != state.Column))
                {
                    falling.ClearBlock();
                    block.row = state.Row; block.collumn = state.Column;
                    if ((state.Flags & WorldFlags.Disturbed) == 0) Map.AssignBlock(block, block.collumn, block.row);
                }
                block.disturbed = (state.Flags & WorldFlags.Disturbed) != 0;
                obj.transform.rotation = Quaternion.Euler(0f, 0f, state.Rotation);
            }
            obj.health = state.Health; obj.maxHealth = state.MaxHealth;
            obj.xI = state.VX; obj.yI = state.VY;
            obj.transform.localScale = new Vector3(state.Scale, obj.transform.localScale.y, obj.transform.localScale.z);
            var projectile = obj as Projectile;
            if (projectile != null) projectile.life = state.LifeTime;
            var grenade = obj as Grenade;
            if (grenade != null) grenade.life = state.LifeTime;
            if (pickup != null) ApplyWorldPickupState(pickup, state);
            if (projectile == null && pickup == null) obj.SetPosition(new Vector3(state.X, state.Y, obj.transform.position.z));
            if (pickup != null && !smoothPickup)
            {
                pickup.SetPosition(new Vector3(state.X, state.Y, pickup.transform.position.z));
                entry.LerpFrom = new Vector2(state.X, state.Y);
            }
            ApplyWorldPose(entry, state);
            return true;
        }

        private static void ApplyWorldPose(WorldEntry entry, WorldSnapshot state)
        {
            if ((state.Flags & WorldFlags.SpritePose) == 0) return;
            var unit = (TestVanDammeAnim)entry.Object;
            unit.frame = state.PoseFrame;
            unit.groundHeight = state.GroundHeight;
            unit.xI = state.VX; unit.yI = state.VY;
            unit.xIBlast = state.PoseBlastX; unit.yIBlast = state.PoseBlastY;
            unit.transform.rotation = Quaternion.Euler(0f, 0f, state.Rotation);
            unit.transform.localScale = new Vector3(state.Scale, state.PoseScaleY, unit.transform.localScale.z);
            if (entry.Sprite == null) entry.Sprite = (SpriteSM)WorldUnitSprite.GetValue(unit);
            if (entry.Sprite == null) return;
            if (entry.Sprite.lowerLeftPixel != state.PosePixel) entry.Sprite.SetLowerLeftPixel(state.PosePixel);
            if (entry.Sprite.offset != state.PoseOffset) entry.Sprite.SetOffset(state.PoseOffset);
            var gun = (SpriteSM)WorldUnitGunSprite.GetValue(unit);
            if (gun != null && gun.gameObject.activeSelf != state.PoseGunActive)
                gun.gameObject.SetActive(state.PoseGunActive);
        }

        internal static void LateUpdateWorldReplicas()
        {
            if (_worldEpoch == 0 || !IsWorldSession()) return;
            FlushWorldHitMessages();
            var now = Time.unscaledTime;
            for (var i = WorldUnits.Count - 1; i >= 0; i--)
            {
                var entry = WorldUnits[i];
                var unit = entry.Object as Unit;
                if (unit == null) { WorldUnits.RemoveAt(i); continue; }
                var state = entry.Last;
                if (entry.PendingHit != 0)
                {
                    if (now < entry.PendingHitUntil) continue;
                    EndWorldHitPresentation(entry);
                    state = entry.Last;
                }
                if (!unit.enabled || !unit.gameObject.activeInHierarchy || unit.IsMine || unit.IsHero ||
                    state == null || (state.Flags & WorldFlags.Removed) != 0) continue;
                var xy = Vector2.Lerp(entry.LerpFrom, new Vector2(state.X, state.Y),
                    Mathf.Clamp01((now - entry.ReceivedAt) / WorldSendInterval));
                unit.SetPosition(new Vector3(xy.x, xy.y, unit.transform.position.z));
                unit.health = state.Health;
                unit.actionState = (ActionState)state.Action;
                ApplyWorldStatus(unit, state, (now - entry.ReceivedAt) * Time.timeScale);
                unit.transform.localScale = new Vector3(state.Scale, unit.transform.localScale.y, unit.transform.localScale.z);
                var tvd = unit as TestVanDammeAnim;
                if (tvd != null) tvd.groundHeight = state.GroundHeight;
                var tank = unit as Tank;
                if (tank != null) tank.facingDirection = state.Facing;
                ApplyWorldPose(entry, state);
            }
        }

        private static void ClearEntityFinalStateSynchronizationState()
        {
            WorldEntries.Clear(); WorldCandidates.Clear(); WorldUnits.Clear(); WorldPending.Clear();
            WorldRegistrations.Clear(); WorldRetiredObjects.Clear();
            ClearWorldPersistentEffects();
            WorldMotionBatch.Clear(); WorldCriticalBatch.Clear();
            WorldRemovedObjects.Clear(); WorldPendingApplied.Clear(); WorldProjectileSpawns.Clear(); WorldPendingProjectiles.Clear();
            WorldProjectileVisualRandomStates.Clear(); _worldProjectileEffectsDepth = 0;
            WorldGrenadeSpawns.Clear(); WorldPendingGrenades.Clear();
            _worldGrenadeEffectsDepth = 0; _worldGrenadeReplicaUpdate = 0; _worldNetworkGrenadeSpawnDepth = 0;
            _worldHitSoundUnit = null; _worldGrenadeSoundSource = null;
            _worldEpoch = 0; _worldServer = -1; _worldSeeded = false;
            _worldSendAt = 0f; _worldRequestAt = 0f;
            _worldDamageSource = null; _worldStatusSource = null;
            _worldDirectSource = null; _worldReceivingHit = 0; _worldReceivingTarget = null; _worldCountdownMine = null;
            _worldHitMessagesPending = false;
            _worldHitSequence = 0;
            WorldPresentedHitEffects.Clear();
            _worldSettlingHit = 0; _worldHitRequestor = -1; _worldSuppressCrateDebris = 0;
        }
    }
}
