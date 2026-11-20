using System;
using System.Collections.Generic;
using System.IO;
using HarmonyLib;
using Networking;
using UnityEngine;

namespace CustomMapMultiplayer
{
    internal static partial class HarmonyDiagnostics
    {
        private sealed class WorldGrenadeSpawn
        {
            internal NID Id, Actor;
            internal string Prefab, Type;
            internal int Owner, Player, Seed;
            internal float X, Y, VX, VY, Life;
        }
        private static readonly Dictionary<NID, WorldGrenadeSpawn> WorldGrenadeSpawns = new Dictionary<NID, WorldGrenadeSpawn>();
        private static readonly Dictionary<NID, WorldGrenadeSpawn> WorldPendingGrenades = new Dictionary<NID, WorldGrenadeSpawn>();
        private static readonly Dictionary<string, Grenade> WorldGrenadePrefabs = new Dictionary<string, Grenade>();
        private static int _worldGrenadeReplicaUpdate;
        private static int _worldNetworkGrenadeSpawnDepth;

        private static void WorldNetworkGrenadeSpawnPrefix(out bool __state)
        {
            __state = _worldEpoch != 0 && IsWorldSession();
            if (__state) _worldNetworkGrenadeSpawnDepth++;
        }

        private static void WorldNetworkGrenadeSpawnFinalizer(bool __state)
        { if (__state) _worldNetworkGrenadeSpawnDepth--; }

        private static void WorldGrenadeUpdatePrefix(Grenade __instance, out WorldDamageContext __state)
        {
            __state = new WorldDamageContext { Previous = _worldDamageSource, VisualReplay = IsWorldReplica(__instance) };
            if (__state.VisualReplay) { _worldApplying++; _worldGrenadeReplicaUpdate++; }
            else if (IsWorldObject(__instance) && __instance.IsMine) _worldDamageSource = __instance;
        }

        private static void WorldGrenadeUpdateFinalizer(WorldDamageContext __state)
        {
            _worldDamageSource = __state.Previous;
            if (__state.VisualReplay) { _worldApplying--; _worldGrenadeReplicaUpdate--; }
        }

        private static bool WorldSpawnGrenadePrefix(Grenade grenadePrefab, MonoBehaviour firedBy, float x, float y,
            float xI, float yI, int playerNum, int seed, ref Grenade __result)
        {
            var actor = firedBy as BroforceObject;
            var hero = actor as Unit;
            if (_worldEpoch == 0 || !IsWorldSession() || actor == null || actor.Nid == NID.NoID ||
                !IsWorldObject(actor) && (hero == null || !hero.IsHero)) return true;
            WorldGrenadePrefabs[grenadePrefab.GetType().FullName + "|" + grenadePrefab.name] = grenadePrefab;
            if (_worldApplying != 0 || !actor.IsMine)
            {
                // Preserve the factory's non-null configuration contract without launching gameplay.
                __result = UnityEngine.Object.Instantiate<Grenade>(grenadePrefab);
                __result.enabled = false;
                __result.gameObject.SetActive(false);
                UnityEngine.Object.Destroy(__result.gameObject);
                return false;
            }
            var spawn = new WorldGrenadeSpawn { Id = Registry.AllocatePlayerUniqueID(), Actor = actor.Nid,
                Owner = actor.Owner.AsInt, Player = playerNum, Seed = seed, Prefab = grenadePrefab.name,
                Type = grenadePrefab.GetType().FullName, X = x, Y = y, VX = xI, VY = yI, Life = grenadePrefab.life };
            WorldGrenadeSpawns.Add(spawn.Id, spawn);
            SendWorldGrenadeSpawn(spawn, PID.TargetOthers);
            __result = CreateWorldGrenade(spawn, grenadePrefab, actor);
            return false;
        }

        private static Grenade CreateWorldGrenade(WorldGrenadeSpawn spawn, Grenade prefab, BroforceObject actor)
        {
            var grenade = UnityEngine.Object.Instantiate<Grenade>(prefab);
            var id = spawn.Id;
            Registry.RegisterGameObject(ref id, grenade.gameObject);
            foreach (var component in grenade.GetComponentsInChildren<NetworkObject>(true)) component.SetOwner(new PID(spawn.Owner));
            grenade.life = spawn.Life;
            grenade.SetupGrenade(spawn.Seed, spawn.Player, actor);
            // Launch can collide immediately. Identity/ownership must already be installed.
            grenade.Launch(spawn.X, spawn.Y, spawn.VX, spawn.VY);
            grenade.NetworkSetup(new PID(spawn.Owner));
            RegisterWorldObject(grenade);
            return grenade;
        }

        private static void SendWorldGrenadeSpawn(WorldGrenadeSpawn spawn, PID target)
        {
            using (var stream = new MemoryStream())
            using (var writer = new BinaryWriter(stream))
            {
                WriteWorldId(writer, spawn.Id); WriteWorldId(writer, spawn.Actor);
                writer.Write(spawn.Prefab); writer.Write(spawn.Type); writer.Write((byte)spawn.Owner);
                writer.Write(spawn.Player); writer.Write(spawn.Seed); writer.Write(spawn.X); writer.Write(spawn.Y);
                writer.Write(spawn.VX); writer.Write(spawn.VY); writer.Write(spawn.Life);
                global::Networking.Networking.RPC<long, byte[]>(target, new RpcSignature<long, byte[]>(ReceiveWorldGrenadeSpawnRPC),
                    _worldEpoch, stream.ToArray(), true);
            }
        }

        [AllowedRPC]
        private static void ReceiveWorldGrenadeSpawnRPC(long epoch, byte[] bytes)
        {
            if (_worldEpoch == 0 || epoch != _worldEpoch || !IsWorldSession() || RPCController.CurrentRPC == null) return;
            WorldGrenadeSpawn spawn;
            using (var stream = new MemoryStream(bytes))
            using (var reader = new BinaryReader(stream))
                spawn = new WorldGrenadeSpawn { Id = ReadWorldId(reader), Actor = ReadWorldId(reader),
                    Prefab = reader.ReadString(), Type = reader.ReadString(), Owner = reader.ReadByte(),
                    Player = reader.ReadInt32(), Seed = reader.ReadInt32(), X = reader.ReadSingle(), Y = reader.ReadSingle(),
                    VX = reader.ReadSingle(), VY = reader.ReadSingle(), Life = reader.ReadSingle() };
            var sender = RPCController.CurrentRPC.messageInfo.Sender;
            if (spawn.Id.Sid != Connect.SessionID ||
                (spawn.Owner == PID.TargetServer.AsInt ? sender != PID.ServerID : sender.AsInt != spawn.Owner)) return;
            if (!TryCreateWorldGrenade(spawn) && WorldPendingGrenades.Count < 256) WorldPendingGrenades[spawn.Id] = spawn;
        }

        private static bool TryCreateWorldGrenade(WorldGrenadeSpawn spawn)
        {
            if (WorldGrenadeSpawns.ContainsKey(spawn.Id) || Registry.IsKeyInUse(spawn.Id) || WorldRemovedObjects.Contains(spawn.Id.BaseID)) return true;
            var actor = Registry.GetObject(spawn.Actor) as BroforceObject;
            if (actor == null) return false;
            var key = spawn.Type + "|" + spawn.Prefab;
            Grenade prefab;
            if (!WorldGrenadePrefabs.TryGetValue(key, out prefab) || prefab == null)
            {
                foreach (Grenade candidate in Resources.FindObjectsOfTypeAll(typeof(Grenade)))
                    if (candidate.name == spawn.Prefab && candidate.GetType().FullName == spawn.Type)
                    { prefab = candidate; WorldGrenadePrefabs[key] = candidate; break; }
            }
            if (prefab == null) return false;
            WorldGrenadeSpawns.Add(spawn.Id, spawn);
            CreateWorldGrenade(spawn, prefab, actor);
            return true;
        }

        private static void UpdateWorldPendingGrenades()
        {
            WorldPendingApplied.Clear();
            foreach (var pair in WorldPendingGrenades)
                if (TryCreateWorldGrenade(pair.Value)) WorldPendingApplied.Add(pair.Key);
            foreach (var id in WorldPendingApplied) WorldPendingGrenades.Remove(id);
        }

        private static void SendWorldGrenadeSpawns(PID target)
        {
            foreach (var pair in WorldGrenadeSpawns)
            {
                var grenade = Registry.GetObject(pair.Key) as GameObject;
                if (grenade != null && grenade.GetComponent<Grenade>().IsMine) SendWorldGrenadeSpawn(pair.Value, target);
            }
        }
    }
}
