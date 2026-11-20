using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Reflection;
using HarmonyLib;
using Networking;
using UnityEngine;

namespace CustomMapMultiplayer
{
    internal static partial class HarmonyDiagnostics
    {
        private sealed class WorldEffectState
        {
            internal int Owner, Serial, Revision;
            internal bool Removed, Laser;
            internal string Prefab, Type;
            internal NID NativeId = NID.NoID;
            internal Vector3 Position;
            internal float Clock, Remaining;
            internal float ReceivedAt;
            internal byte[] Data;
        }
        private sealed class WorldEffectEntry
        {
            internal Component Object;
            internal WorldCarrier Tag;
            internal NID NativeId;
            internal int Owner, Serial;
            internal string Prefab, Type;
            internal WorldEffectState Last;
            internal HashSet<long> VisibleCells = new HashSet<long>();
        }
        private static readonly Dictionary<long, WorldEffectEntry> WorldEffects = new Dictionary<long, WorldEffectEntry>();
        private static readonly Dictionary<long, WorldEffectState> WorldEffectPending = new Dictionary<long, WorldEffectState>();
        private static readonly string[] WorldFlashFields = { "frameRate", "counter", "damageUnitsTimer", "maxTime", "explosionsRemaining",
            "minGridCollumns", "minGridRows", "maxGridCollumns", "maxGridRows", "startCollumn", "startRow", "playerNum", "forceDirection" };
        private static readonly string[] WorldLaserFields = { "angle", "rotationSpeed", "life", "beamStartupDelayCounter", "beamStartupDelayDelay",
            "disappateTime", "dissapating", "currentLength", "playerNum", "damageCounter", "beamWidth", "damageBeamWidth" };
        private static readonly Dictionary<string, FieldInfo> WorldEffectFields = new Dictionary<string, FieldInfo>();
        private static int _worldEffectSerial, _worldEffectRevision;
        private static float _worldEffectsSendAt;
        private static bool _worldRestoringEffect;

        private static long WorldEffectKey(int owner, int serial) { return ((long)owner << 32) | (uint)serial; }
        private static FieldInfo WorldEffectField(Type type, string name)
        {
            var key = type.FullName + ":" + name;
            FieldInfo field;
            if (!WorldEffectFields.TryGetValue(key, out field)) WorldEffectFields[key] = field = AccessTools.Field(type, name);
            return field;
        }
        private static float EffectFloat(object obj, string name) { return (float)WorldEffectField(obj.GetType(), name).GetValue(obj); }
        private static void SetEffect(object obj, string name, object value) { WorldEffectField(obj.GetType(), name).SetValue(obj, value); }
        private static void TrackWorldPersistentEffect(Component obj, WorldCarrier tag, NID nativeId)
        {
            WorldEffects[WorldEffectKey(tag.Owner, tag.Serial)] = new WorldEffectEntry { Object = obj, Tag = tag,
                NativeId = nativeId, Owner = tag.Owner, Serial = tag.Serial, Prefab = tag.Prefab, Type = tag.Type };
        }

        private static void WriteEffectFields(BinaryWriter w, object obj, string[] fields)
        {
            foreach (var name in fields)
            {
                var field = WorldEffectField(obj.GetType(), name); var value = field.GetValue(obj);
                if (field.FieldType == typeof(float)) w.Write((float)value);
                else if (field.FieldType == typeof(bool)) w.Write((bool)value);
                else w.Write(Convert.ToInt32(value));
            }
        }
        private static void ReadEffectFields(BinaryReader r, object obj, string[] fields)
        {
            foreach (var name in fields)
            {
                var field = WorldEffectField(obj.GetType(), name);
                object value = field.FieldType == typeof(float) ? (object)r.ReadSingle() :
                    field.FieldType == typeof(bool) ? (object)r.ReadBoolean() : r.ReadInt32();
                if (field.FieldType.IsEnum) value = Enum.ToObject(field.FieldType, value);
                field.SetValue(obj, value);
            }
        }
        private static void WriteEffectPoints(BinaryWriter w, List<FlashBangPoint> points)
        {
            w.Write(points.Count);
            foreach (var p in points) { w.Write(p.collumn); w.Write(p.row); w.Write(p.directionCollumn); w.Write(p.directionRow); }
        }
        private static List<FlashBangPoint> ReadEffectPoints(BinaryReader r)
        {
            var result = new List<FlashBangPoint>(); var count = r.ReadInt32();
            for (var i = 0; i < count; i++) { var c = r.ReadInt32(); var row = r.ReadInt32(); result.Add(new FlashBangPoint(c, row, Time.time, r.ReadInt32(), r.ReadInt32())); }
            return result;
        }
        private static WorldEffectState CaptureWorldEffect(WorldEffectEntry entry)
        {
            var obj = entry.Object; var tag = entry.Tag;
            var s = new WorldEffectState { Owner = entry.Owner, Serial = entry.Serial, Revision = ++_worldEffectRevision,
                Prefab = entry.Prefab, Type = entry.Type,
                NativeId = entry.NativeId, Clock = Time.realtimeSinceStartup, Laser = obj is LaserBeam,
                Removed = obj == null || !obj.gameObject.activeInHierarchy || tag.Serial != entry.Serial };
            if (s.Removed) return s;
            s.Position = obj.transform.position;
            using (var stream = new MemoryStream())
            using (var w = new BinaryWriter(stream))
            {
                var flash = obj as FlashBangExplosion;
                if (flash != null)
                {
                    if (!flash.DoneFirstUpdate) return null;
                    s.Remaining = Mathf.Max(0f, EffectFloat(flash, "maxTime") - (Time.time - EffectFloat(flash, "startTime")));
                    WriteEffectFields(w, flash, WorldFlashFields);
                    var used = (bool[,])WorldEffectField(flash.GetType(), "usedGridPoints").GetValue(flash);
                    w.Write(used.GetLength(0)); w.Write(used.GetLength(1));
                    var bits = new byte[(used.Length + 7) / 8]; var index = 0;
                    foreach (var value in used) { if (value) bits[index / 8] |= (byte)(1 << (index % 8)); index++; }
                    w.Write(bits.Length); w.Write(bits); WriteEffectPoints(w, flash.activeGridPoints);
                    var random = (Randomf)WorldEffectField(flash.GetType(), "random").GetValue(flash);
                    w.Write(random.seed); w.Write(random.NoOfCalls);
                    if (flash is HolyWaterExplosion) WriteEffectPoints(w, (List<FlashBangPoint>)WorldEffectField(flash.GetType(), "persistentPoints").GetValue(flash));
                }
                else
                {
                    var laser = (LaserBeam)obj;
                    s.Remaining = Mathf.Max(0f, laser.beamStartupDelayDelay - EffectFloat(laser, "beamStartupDelayCounter")) +
                        ((bool)WorldEffectField(typeof(LaserBeam), "dissapating").GetValue(laser) ? laser.disappateTime : laser.life + laser.disappateTime);
                    WriteEffectFields(w, laser, WorldLaserFields);
                    var p = (Vector3)WorldEffectField(typeof(LaserBeam), "currentOrigin").GetValue(laser);
                    w.Write(p.x); w.Write(p.y); w.Write(p.z);
                }
                s.Data = stream.ToArray();
            }
            if (s.Remaining <= 0f) s.Removed = true;
            return s;
        }

        private static void WriteWorldEffect(BinaryWriter w, WorldEffectState s)
        {
            w.Write(s.Owner); w.Write(s.Serial); w.Write(s.Revision); w.Write(s.Removed); w.Write(s.Laser);
            w.Write(s.Prefab); w.Write(s.Type); WriteWorldId(w, s.NativeId);
            w.Write(s.Position.x); w.Write(s.Position.y); w.Write(s.Position.z); w.Write(s.Clock); w.Write(s.Remaining);
            w.Write(s.Data == null ? 0 : s.Data.Length); if (s.Data != null) w.Write(s.Data);
        }
        private static WorldEffectState ReadWorldEffect(BinaryReader r)
        {
            var s = new WorldEffectState { Owner = r.ReadInt32(), Serial = r.ReadInt32(), Revision = r.ReadInt32(), Removed = r.ReadBoolean(),
                Laser = r.ReadBoolean(), Prefab = r.ReadString(), Type = r.ReadString(), NativeId = ReadWorldId(r),
                Position = new Vector3(r.ReadSingle(), r.ReadSingle(), r.ReadSingle()), Clock = r.ReadSingle(), Remaining = r.ReadSingle() };
            s.Data = r.ReadBytes(r.ReadInt32()); return s;
        }
        private static float WorldEffectAge(WorldEffectState s)
        { return Mathf.Max(s.ReceivedAt > 0f ? Time.time - s.ReceivedAt : 0f, Mathf.Max(0f, PingController.GetLocalTime(new PID(s.Owner)) - s.Clock) * Time.timeScale); }
        private static bool IsNewWorldEffect(WorldEffectState previous, WorldEffectState next)
        { return previous == null || next.Revision > previous.Revision && (!previous.Removed || next.Removed); }
        private static bool IsExpiredWorldEffect(WorldEffectState state, float age)
        { return state.Removed || state.Remaining <= age; }

        private static void SendWorldPersistentEffects(PID target)
        {
            foreach (var entry in new List<WorldEffectEntry>(WorldEffects.Values))
            {
                if (entry.Owner != PID.MyID.AsInt) continue;
                if (target == PID.TargetOthers && entry.Last != null && entry.Last.Removed) continue;
                var s = entry.Last != null && entry.Last.Removed ? entry.Last : CaptureWorldEffect(entry);
                if (s == null) continue;
                entry.Last = s;
                using (var stream = new MemoryStream())
                {
                    using (var zip = new DeflateStream(stream, CompressionMode.Compress, true))
                    using (var w = new BinaryWriter(zip)) WriteWorldEffect(w, s);
                    global::Networking.Networking.RPC<long, byte[]>(target,
                        new RpcSignature<long, byte[]>(ReceiveWorldPersistentEffectRPC), _worldEpoch, stream.ToArray(), true);
                }
            }
        }
        [AllowedRPC]
        private static void ReceiveWorldPersistentEffectRPC(long epoch, byte[] bytes)
        {
            if (!IsWorldSession() || _worldEpoch == 0 || epoch != _worldEpoch || RPCController.CurrentRPC == null) return;
            WorldEffectState s;
            using (var input = new MemoryStream(bytes))
            using (var zip = new DeflateStream(input, CompressionMode.Decompress))
            using (var r = new BinaryReader(zip)) s = ReadWorldEffect(r);
            if (RPCController.CurrentRPC.messageInfo.Sender.AsInt != s.Owner || s.Owner == PID.MyID.AsInt) return;
            s.ReceivedAt = Time.time;
            var key = WorldEffectKey(s.Owner, s.Serial); WorldEffectEntry entry; WorldEffectState pending;
            if (WorldEffects.TryGetValue(key, out entry) && !IsNewWorldEffect(entry.Last, s) ||
                WorldEffectPending.TryGetValue(key, out pending) && !IsNewWorldEffect(pending, s)) return;
            WorldEffectPending[key] = s;
        }

        private static bool ApplyWorldPersistentEffect(WorldEffectState s)
        {
            var key = WorldEffectKey(s.Owner, s.Serial); WorldEffectEntry entry;
            WorldEffects.TryGetValue(key, out entry);
            var age = WorldEffectAge(s);
            if (IsExpiredWorldEffect(s, age))
            {
                if (entry != null && entry.Object != null) RetireWorldEffect(entry);
                WorldEffects[key] = new WorldEffectEntry { Last = s, Owner = s.Owner, Serial = s.Serial, NativeId = s.NativeId };
                return true;
            }
            var created = entry == null || entry.Object == null || entry.Tag.Serial != s.Serial;
            if (created)
            {
                Component obj = s.NativeId == NID.NoID ? null : Registry.GetObject(s.NativeId) as LaserBeam;
                if (obj == null)
                {
                    foreach (var resource in Resources.FindObjectsOfTypeAll(s.Laser ? typeof(LaserBeam) : typeof(FlashBangExplosion)))
                    {
                        var prefab = (Component)resource;
                        if (prefab.gameObject.scene.IsValid() || prefab.name != s.Prefab || prefab.GetType().FullName != s.Type) continue;
                        _worldRestoringEffect = true; _worldApplying++;
                        try
                        {
                            obj = s.Laser ? (Component)UnityEngine.Object.Instantiate(prefab, s.Position, Quaternion.identity) :
                                (Component)EffectsController.InstantiateEffect((FlashBangExplosion)prefab, s.Position, Quaternion.identity);
                            if (!s.Laser) ((FlashBangExplosion)obj).Setup(0, null, DirectionEnum.Any);
                        }
                        finally { _worldApplying--; _worldRestoringEffect = false; }
                        break;
                    }
                }
                if (obj == null) return false;
                var tag = obj.GetComponent<WorldCarrier>() ?? obj.gameObject.AddComponent<WorldCarrier>();
                tag.Presentation = true; tag.Restored = true; tag.Owner = s.Owner; tag.Serial = s.Serial; tag.Prefab = s.Prefab; tag.Type = s.Type;
                entry = new WorldEffectEntry { Object = obj, Tag = tag, NativeId = s.NativeId, Owner = s.Owner, Serial = s.Serial };
                WorldEffects[key] = entry;
            }
            entry.Last = s; entry.Tag.Restored = true;
            var flash = entry.Object as FlashBangExplosion;
            using (var stream = new MemoryStream(s.Data))
            using (var r = new BinaryReader(stream))
            {
                if (flash != null)
                {
                    ReadEffectFields(r, flash, WorldFlashFields);
                    var columns = r.ReadInt32(); var rows = r.ReadInt32(); var bits = r.ReadBytes(r.ReadInt32());
                    var used = new bool[columns, rows]; var index = 0;
                    for (var c = 0; c < columns; c++) for (var row = 0; row < rows; row++, index++) used[c, row] = (bits[index / 8] & (1 << (index % 8))) != 0;
                    SetEffect(flash, "usedGridPoints", used); flash.activeGridPoints = ReadEffectPoints(r);
                    SetEffect(flash, "random", new Randomf(r.ReadInt32()) { NoOfCalls = r.ReadInt32() });
                    if (flash is HolyWaterExplosion) SetEffect(flash, "persistentPoints", ReadEffectPoints(r));
                    SetEffect(flash, "startTime", Time.time); SetEffect(flash, "maxTime", s.Remaining - age);
                    SetEffect(flash, "sound", Sound.GetInstance()); flash.DoneFirstUpdate = true;
                    if (flash.lightExplosion != null || flash.explosionPuffs.Length > 0)
                    {
                        // A surviving cloud cell needs its puff, not its old hit/sound/terrain event.
                        var prefab = flash.lightExplosion != null ? flash.lightExplosion : flash.explosionPuffs[0];
                        foreach (var point in flash.activeGridPoints)
                            if (entry.VisibleCells.Add(((long)point.collumn << 32) | (uint)point.row))
                            {
                                EffectsController.CreateEffect(prefab, Map.GetBlocksX(point.collumn) + 8f,
                                    Map.GetBlocksY(point.row) + 4f, 0f, 0f, Vector3.zero, null);
                            }
                    }
                }
                else
                {
                    var laser = (LaserBeam)entry.Object;
                    ReadEffectFields(r, laser, WorldLaserFields);
                    var origin = new Vector3(r.ReadSingle(), r.ReadSingle(), r.ReadSingle());
                    SetEffect(laser, "attachedTransform", null); laser.targetTransform = null;
                    SetEffect(laser, "localPosition", origin); SetEffect(laser, "currentOrigin", origin);
                    var startup = Mathf.Max(0f, laser.beamStartupDelayDelay - EffectFloat(laser, "beamStartupDelayCounter"));
                    SetEffect(laser, "beamStartupDelayCounter", EffectFloat(laser, "beamStartupDelayCounter") + age);
                    if (age > startup) laser.life -= age - startup;
                    laser.laserBeamVisuals.Fire(Mathf.Max(0f, startup - age), laser.beamWidth * laser.visualsWidthM, Mathf.Max(0f, laser.life), laser.disappateTime);
                    AccessTools.Method(typeof(LaserBeam), "RunAngle", null, null).Invoke(laser, new object[] { 0f });
                    AccessTools.Method(typeof(LaserBeam), "DrawLaser", null, null).Invoke(laser, null);
                    AccessTools.Method(typeof(LaserBeam), "CalculateWidth", null, null).Invoke(laser, null);
                }
            }
            return true;
        }

        private static void RetireWorldEffect(WorldEffectEntry entry)
        {
            if (entry.Tag == null || entry.Tag.Serial != entry.Serial) return;
            // Puffs expire through their native pool. A saved reference may already belong to a later acquisition.
            var flash = entry.Object as FlashBangExplosion;
            if (flash != null) flash.EffectDie();
            else
            {
                var go = entry.Object.gameObject;
                foreach (var component in go.GetComponentsInChildren<Component>(true))
                {
                    var id = Registry.GetNID(component);
                    if (id != NID.NoID) { Registry.Components.Remove(id); Registry.ComponentsToSync.Remove(id); Registry.Keys.Remove(component); }
                    var network = component as NetworkObject;
                    if (network != null) { network.SyncDestroy = false; WorldRetiredObjects.Add(network); }
                }
                var root = Registry.GetNID(go);
                if (root != NID.NoID) { Registry.Components.Remove(root); Registry.Keys.Remove(go); }
                go.SetActive(false); UnityEngine.Object.Destroy(go);
            }
        }
        private static void UpdateWorldPersistentEffects()
        {
            var done = new List<long>();
            foreach (var pair in WorldEffectPending) if (ApplyWorldPersistentEffect(pair.Value)) done.Add(pair.Key);
            foreach (var key in done) WorldEffectPending.Remove(key);
            foreach (var entry in WorldEffects.Values)
                if (entry.Owner != PID.MyID.AsInt && entry.Last != null && !entry.Last.Removed &&
                    entry.Last.Remaining <= WorldEffectAge(entry.Last))
                { if (entry.Object != null) RetireWorldEffect(entry); entry.Last.Removed = true; }
            if (Time.unscaledTime < _worldEffectsSendAt) return;
            _worldEffectsSendAt = Time.unscaledTime + .25f;
            SendWorldPersistentEffects(PID.TargetOthers);
        }
        private static void ClearWorldPersistentEffects()
        {
            foreach (var entry in WorldEffects.Values)
                if (entry.Tag != null && entry.Tag.Presentation && entry.Object != null) RetireWorldEffect(entry);
            WorldEffects.Clear(); WorldEffectPending.Clear(); _worldEffectsSendAt = 0f;
        }

        private static void PatchWorldLasers()
        {
            foreach (var method in typeof(LaserBeam).GetMethods(WorldFields | BindingFlags.DeclaredOnly))
                if (method.Name == "Fire") PatchWorldMethod(method, null, "WorldLaserFirePostfix");
            _harmony.Patch(AccessTools.Method(typeof(LaserBeam), "Update", null, null), new HarmonyMethod(AccessTools.Method(typeof(HarmonyDiagnostics), "WorldLaserContextPrefix", null, null)),
                null, null, new HarmonyMethod(AccessTools.Method(typeof(HarmonyDiagnostics), "WorldProjectileContextFinalizer", null, null)));
            PatchWorldMethod(AccessTools.Method(typeof(LaserBeam), "RunDamage", null, null), "WorldLaserDamagePrefix", null);
        }
        private static void WorldLaserFirePostfix(LaserBeam __instance, MonoBehaviour originObject)
        {
            if (_worldEpoch == 0 || !IsWorldSession()) return;
            var tag = __instance.GetComponent<WorldCarrier>() ?? __instance.gameObject.AddComponent<WorldCarrier>();
            tag.Prefab = __instance.name.Replace("(Clone)", ""); tag.Type = __instance.GetType().FullName;
            var source = originObject == null ? null : originObject.GetComponentInParent<NetworkObject>();
            tag.Presentation = __instance.Nid != NID.NoID ? !__instance.IsMine : source != null ? !source.IsMine : !Connect.IsHost;
            tag.Actor = source as BroforceObject;
            if (tag.Presentation)
            {
                if (__instance.Nid == NID.NoID)
                { __instance.gameObject.SetActive(false); UnityEngine.Object.Destroy(__instance.gameObject); return; }
                foreach (var pair in WorldEffects)
                    if (pair.Value.NativeId == __instance.Nid && __instance.Nid != NID.NoID)
                    {
                        if (pair.Value.Object != null && pair.Value.Object != __instance) RetireWorldEffect(pair.Value);
                        pair.Value.Object = __instance; pair.Value.Tag = tag;
                        tag.Owner = pair.Value.Last.Owner; tag.Serial = pair.Value.Last.Serial;
                        if (pair.Value.Last.Removed) RetireWorldEffect(pair.Value);
                        else ApplyWorldPersistentEffect(pair.Value.Last);
                        break;
                    }
                return;
            }
            tag.Owner = PID.MyID.AsInt; tag.Serial = ++_worldEffectSerial;
            TrackWorldPersistentEffect(__instance, tag, __instance.Nid);
        }
        private static void WorldLaserContextPrefix(LaserBeam __instance, out WorldDamageContext __state)
        {
            __state = new WorldDamageContext { Previous = _worldDamageSource };
            var tag = __instance.GetComponent<WorldCarrier>();
            if (tag == null || _worldEpoch == 0 || !IsWorldSession()) return;
            if (tag.Presentation || !__instance.IsMine && __instance.Nid != NID.NoID)
            { __state.VisualReplay = true; _worldApplying++; _worldDamageSource = null; }
            else _worldDamageSource = tag.Actor;
        }
        private static bool WorldLaserDamagePrefix(LaserBeam __instance)
        {
            var tag = __instance.GetComponent<WorldCarrier>();
            return _worldEpoch == 0 || !IsWorldSession() || tag == null || !tag.Presentation;
        }
    }
}
