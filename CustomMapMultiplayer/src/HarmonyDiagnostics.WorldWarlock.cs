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
        private sealed class WorldWarlockEvent
        {
            internal NID Id, Actor, Child;
            internal int Owner, Target, Direction, Prefab = -1;
            internal float X, Y, Delay, Rise;
            internal bool Created, Spawned;
        }

        private static readonly Dictionary<NID, WorldWarlockEvent> WorldWarlockEvents = new Dictionary<NID, WorldWarlockEvent>();
        private static readonly MethodInfo WorldWarlockSpawnUnit = AccessTools.Method(typeof(WarlockPortal), "SpawnUnit", null, null);

        private static void PatchWorldWarlock()
        {
            PatchWorldMethod(AccessTools.Method(typeof(WarlockPortalProjectile), "Setup", null, null), null, "WorldWarlockSetupPostfix");
            PatchWorldMethod(AccessTools.Method(typeof(WarlockPortal), "Setup", null, null), null, "WorldWarlockPortalPostfix");
            PatchWorldMethod(WorldWarlockSpawnUnit, "WorldWarlockSpawnPrefix", "WorldWarlockSpawnPostfix");
            PatchWorldMethod(AccessTools.Method(typeof(WarlockPortal), "Update", null, null), "WorldWarlockUpdatePrefix", null);
        }

        private static void WorldWarlockSetupPostfix(WarlockPortalProjectile __instance, Warlock _owner,
            GridPoint _target, int _targetPlayer, NID _portalNid, NID _summonUnitNid)
        {
            if (_worldEpoch == 0 || !IsWorldSession() || !IsWorldObject(__instance) || !__instance.IsMine) return;
            WorldSpawn spawn;
            if (!WorldProjectileSpawns.TryGetValue(__instance.Nid.BaseID, out spawn)) return;
            spawn.WarlockSetup = true; spawn.Warlock = _owner.Nid; spawn.Portal = _portalNid; spawn.Child = _summonUnitNid;
            spawn.TargetColumn = _target.collumn; spawn.TargetRow = _target.row; spawn.TargetPlayer = _targetPlayer;
            SendWorldProjectileSpawn(spawn, PID.TargetOthers);
        }

        private static bool IsWorldWarlockPortal(WarlockPortal portal)
        {
            return _worldEpoch != 0 && IsWorldSession() && portal.owner != null && IsWorldObject(portal.owner);
        }

        private static void SetWorldWarlockOwner(GameObject obj, PID owner)
        {
            foreach (var component in obj.GetComponentsInChildren<NetworkObject>(true)) component.SetOwner(owner);
        }

        private static void WorldWarlockPortalPostfix(WarlockPortal __instance)
        {
            if (!IsWorldWarlockPortal(__instance)) return;
            SetWorldWarlockOwner(__instance.gameObject, __instance.owner.Owner);
            if (__instance.IsMine && _worldApplying == 0) SendWorldWarlockPortal(__instance, -1);
        }

        private static bool WorldWarlockSpawnPrefix(WarlockPortal __instance)
        {
            return !IsWorldWarlockPortal(__instance) || __instance.IsMine || _worldApplying != 0;
        }

        private static void WorldWarlockSpawnPostfix(WarlockPortal __instance)
        {
            if (!IsWorldWarlockPortal(__instance) || __instance.unit == null) return;
            SetWorldWarlockOwner(__instance.unit.gameObject, __instance.Owner);
            if (!__instance.IsMine || _worldApplying != 0) return;
            // The owner makes the native random choice. Replicas replay that exact prefab once.
            for (var i = 0; i < __instance.unitPrefabs.Length; i++)
                if (__instance.unit.GetType() == __instance.unitPrefabs[i].GetType() &&
                    __instance.unit.name == __instance.unitPrefabs[i].name + "(Clone)")
                { SendWorldWarlockPortal(__instance, i); return; }
            throw new InvalidOperationException("Warlock summoned a unit outside its native prefab list.");
        }

        private static bool WorldWarlockUpdatePrefix(WarlockPortal __instance)
        {
            // The mirror cannot enter native SpawnUnit before the owner's selected child arrives.
            return !IsWorldWarlockPortal(__instance) || __instance.IsMine || __instance.unit != null;
        }

        private static void SendWorldWarlockPortal(WarlockPortal portal, int prefab)
        {
            var stream = new MemoryStream();
            using (var writer = new BinaryWriter(stream))
            {
                WriteWorldId(writer, portal.Nid); WriteWorldId(writer, portal.owner.Nid); WriteWorldId(writer, portal.summonedUnitNid);
                writer.Write((byte)portal.Owner.AsInt); writer.Write(portal.targetPlayer); writer.Write((int)portal.direction);
                writer.Write(portal.transform.position.x); writer.Write(portal.transform.position.y);
                writer.Write(prefab); writer.Write(portal.launchDelay); writer.Write(portal.riseTime);
                global::Networking.Networking.RPC<long, byte[]>(PID.TargetOthers,
                    new RpcSignature<long, byte[]>(ReceiveWorldWarlockPortalRPC), _worldEpoch, stream.ToArray(), true);
            }
        }

        [AllowedRPC]
        private static void ReceiveWorldWarlockPortalRPC(long epoch, byte[] bytes)
        {
            if (!AcceptWorldRpc(epoch)) return;
            WorldWarlockEvent next;
            using (var reader = new BinaryReader(new MemoryStream(bytes)))
                next = new WorldWarlockEvent { Id = ReadWorldId(reader), Actor = ReadWorldId(reader), Child = ReadWorldId(reader),
                    Owner = reader.ReadByte(), Target = reader.ReadInt32(), Direction = reader.ReadInt32(),
                    X = reader.ReadSingle(), Y = reader.ReadSingle(), Prefab = reader.ReadInt32(),
                    Delay = reader.ReadSingle(), Rise = reader.ReadSingle() };
            if (!IsWorldRpcObject(next.Id, next.Owner) || next.Actor.Sid != Connect.SessionID ||
                next.Child.Sid != Connect.SessionID || next.Prefab < -1) return;
            WorldWarlockEvent current;
            if (WorldWarlockEvents.TryGetValue(next.Id, out current))
            {
                if (current.Spawned || current.Prefab >= 0 && next.Prefab < 0) return;
                next.Created = current.Created;
            }
            WorldWarlockEvents[next.Id] = next;
            ApplyWorldWarlockEvent(next);
        }

        private static void UpdateWorldWarlockEvents()
        {
            foreach (var next in WorldWarlockEvents.Values)
                if (!next.Created || next.Prefab >= 0 && !next.Spawned) ApplyWorldWarlockEvent(next);
        }

        private static void ApplyWorldWarlockEvent(WorldWarlockEvent next)
        {
            var actor = Registry.GetObject(next.Actor) as Warlock;
            if (actor == null || actor.Owner.AsInt != next.Owner || actor.IsMine) return;
            var prefab = actor.warlockPortalProjectile.warlockPortalPrefab;
            if (next.Prefab >= prefab.unitPrefabs.Length) return;
            var portal = Registry.GetObject(next.Id) as WarlockPortal;
            if (!next.Created)
            {
                if (portal != null || Registry.IsKeyInUse(next.Id.BaseID)) return;
                portal = UnityEngine.Object.Instantiate<WarlockPortal>(prefab, new Vector3(next.X, next.Y, 0f), Quaternion.identity);
                var id = next.Id.BaseID;
                Registry.RegisterGameObject(ref id, portal.gameObject);
                portal.owner = actor; actor.activePortal = portal;
                portal.direction = (DirectionEnum)next.Direction;
                portal.Setup(next.Target, 0f, next.Child);
                next.Created = true;
            }
            if (portal == null || next.Prefab < 0 || next.Spawned) return;
            if (Registry.IsKeyInUse(next.Child.BaseID)) return;
            var choices = portal.unitPrefabs;
            portal.unitPrefabs = new[] { choices[next.Prefab] };
            portal.launchDelay = next.Delay; portal.riseTime = next.Rise;
            _worldApplying++;
            try { WorldWarlockSpawnUnit.Invoke(portal, null); next.Spawned = true; }
            finally { _worldApplying--; portal.unitPrefabs = choices; }
        }
    }
}
