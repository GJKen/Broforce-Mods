using System.Collections.Generic;
using UnityEngine;

namespace CustomMapMultiplayer
{
    internal static partial class HarmonyDiagnostics
    {
        private static readonly List<WorldEntry> WorldRegistrations = new List<WorldEntry>();
        private static readonly HashSet<NetworkObject> WorldRetiredObjects = new HashSet<NetworkObject>();

        private static bool WorldNetworkDestroyPrefix(NetworkObject __instance)
        {
            if (WorldRetiredObjects.Remove(__instance)) return false;
            WorldDestroyPrefix(__instance);
            return true;
        }

        private static void QueueWorldRegistration(WorldEntry entry)
        {
            entry.RegistrationFrame = Time.frameCount;
            WorldRegistrations.Add(entry);
        }

        private static bool MatchesWorldTombstone(WorldEntry entry)
        {
            var obj = entry.Object; var state = entry.Last;
            return obj != null && state != null && (state.Flags & WorldFlags.Removed) != 0 &&
                state.Life == entry.Life && obj.GetType().Name == state.Type &&
                obj.Owner.AsInt == state.Owner && !obj.IsMine;
        }

        private static void UpdateWorldRegistrations()
        {
            for (var i = WorldRegistrations.Count - 1; i >= 0; i--)
            {
                var entry = WorldRegistrations[i];
                // Registry runs before InitializeInstance.NetworkSetup and before Start.
                // Leave the object intact until the native caller has finished initializing it.
                if (Time.frameCount <= entry.RegistrationFrame) continue;
                WorldRegistrations.RemoveAt(i);
                entry.RegistrationFrame = -1;
                WorldSnapshot pending;
                if (WorldPending.TryGetValue(entry.Id, out pending) && pending.Life > entry.Life && TryApplyWorldState(pending))
                    WorldPending.Remove(entry.Id);
                if (MatchesWorldTombstone(entry)) RemoveLateWorldObject(entry.Object);
            }
        }

        private static void RemoveLateWorldObject(BroforceObject obj)
        {
            // Consume a terminal result without Death/Collapse/Gib or their gameplay side effects.
            // Native OnDestroy does not remove every Map collection or Registry key.
            var go = obj.gameObject;
            foreach (var comp in obj.OnlyDestroyScriptOnSync ? new Component[] { obj } : go.GetComponentsInChildren<Component>(true))
            {
                var id = Registry.GetNID(comp);
                var world = comp as BroforceObject;
                if (world != null)
                {
                    world.destroyed = true;
                    var unit = world as Unit;
                    if (unit != null) Map.RemoveUnit(unit);
                    var block = world as Block;
                    if (block != null && Map.IsWithinBounds(block.collumn, block.row)) Map.SetBlockEmpty(block, block.collumn, block.row);
                    var projectile = world as Projectile;
                    if (projectile != null) Map.RemoveProjectile(projectile);
                    var satchel = world as SachelPack;
                    if (satchel != null) Map.RemoveSachelPack(satchel);
                    var grenade = world as Grenade;
                    if (grenade != null) { Map.RemoveGrenade(grenade); Map.RemoveShootableGrenade(grenade); }
                    var doodad = world as Doodad;
                    if (doodad != null)
                    {
                        Map.RemoveDestroyableDoodad(doodad); Map.RemoveDecalDoodad(doodad);
                        Map.RemoveGrassOrBlood(doodad); Map.RemoveStaticDoodad(doodad); Map.RemoveJigglyBit(doodad);
                    }
                }
                var net = comp as NetworkObject;
                if (net != null)
                {
                    net.SyncDestroy = false; WorldRetiredObjects.Add(net);
                    if (id != NID.NoID && !Registry.DestroyedMapObjects.Contains(id)) Registry.DestroyedMapObjects.Add(id);
                }
                if (id != NID.NoID)
                {
                    Registry.Components.Remove(id); Registry.ComponentsToSync.Remove(id); Registry.Keys.Remove(comp);
                }
            }
            if (obj.OnlyDestroyScriptOnSync)
            {
                obj.enabled = false;
                UnityEngine.Object.Destroy(obj);
                return;
            }
            var baseId = Registry.GetNID(go);
            if (baseId != NID.NoID) { Registry.Components.Remove(baseId); Registry.Keys.Remove(go); }
            go.SetActive(false);
            UnityEngine.Object.Destroy(go);
        }
    }
}
