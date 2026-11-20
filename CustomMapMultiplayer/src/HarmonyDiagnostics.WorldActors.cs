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
        private static readonly FieldInfo WorldScaredTime = AccessTools.Field(typeof(Mook), "scaredTime");
        private static readonly HashSet<MethodBase> WorldHeroStatusMethods = new HashSet<MethodBase>();
        private static Unit _worldStatusSource;

        private static void PatchWorldHeroStatusContext(Type type)
        {
            foreach (var name in new[] { "Update", "LateUpdate", "FixedUpdate" })
            {
                var method = type.GetMethod(name, WorldFields);
                if (method == null || !typeof(Unit).IsAssignableFrom(method.DeclaringType)) continue;
                // Harmony requires the declaration, not an inherited MethodInfo reflected from a hero subtype.
                method = AccessTools.DeclaredMethod(method.DeclaringType, name, Type.EmptyTypes, null);
                if (WorldHeroStatusMethods.Contains(method)) continue;
                PatchWorldMethod(method, "WorldHeroStatusPrefix", null, null, "WorldHeroStatusFinalizer");
                WorldHeroStatusMethods.Add(method);
            }
        }

        private static void WorldHeroStatusPrefix(Unit __instance, out Unit __state)
        {
            __state = _worldStatusSource;
            _worldStatusSource = _worldEpoch != 0 && IsWorldSession() && __instance.IsHero ? __instance : null;
        }

        private static void WorldHeroStatusFinalizer(Unit __state)
        {
            _worldStatusSource = __state;
        }

        private static bool WorldStatusPrefix(Unit __instance, MethodBase __originalMethod, object[] __args)
        {
            if (_worldEpoch == 0 || !IsWorldSession() || !IsWorldObject(__instance) || _worldApplying != 0) return true;
            if (__instance.IsMine) return true;
            // Native replica AI also calls Panic. Only an owning hero may initiate a new remote status.
            if (_worldStatusSource == null || !_worldStatusSource.IsMine || RPCController.CurrentRPC != null) return false;
            var kind = __originalMethod.Name == "Stun" ? 7 : __originalMethod.Name == "Freeze" ? 8 : __originalMethod.Name == "TearGas" ? 6 : __originalMethod.Name == "Panic" ? __args.Length - 1 :
                (__originalMethod.Name == "Terrify" ? 3 : (__args.Length == 0 ? 4 : 5));
            var stream = new MemoryStream();
            using (var writer = new BinaryWriter(stream))
            {
                WriteWorldId(writer, __instance.Nid); WriteWorldId(writer, _worldStatusSource.Nid);
                writer.Write((byte)kind);
                writer.Write(kind == 2 ? (int)__args[0] : 0);
                writer.Write(kind == 1 || kind >= 5 ? (float)__args[0] : (kind == 2 ? (float)__args[1] : 0f));
                writer.Write(kind <= 2 && (bool)__args[__args.Length - 1]);
                global::Networking.Networking.RPC<long, byte[]>(__instance.Owner,
                    new RpcSignature<long, byte[]>(ReceiveWorldStatusRPC), _worldEpoch, stream.ToArray(), false);
            }
            return false;
        }

        [AllowedRPC]
        private static void ReceiveWorldStatusRPC(long epoch, byte[] bytes)
        {
            if (!AcceptWorldRpc(epoch)) return;
            var stream = new MemoryStream(bytes);
            using (var reader = new BinaryReader(stream))
            {
                var id = ReadWorldId(reader); var sourceId = ReadWorldId(reader);
                var target = Registry.GetObject(id) as Unit;
                var source = Registry.GetObject(sourceId) as Unit;
                if (id.Sid != Connect.SessionID || sourceId.Sid != Connect.SessionID ||
                    target == null || !IsWorldObject(target) || !target.IsMine || source == null || !source.IsHero ||
                    RPCController.CurrentRPC.messageInfo.Sender != (source.Owner == PID.TargetServer ? PID.ServerID : source.Owner)) return;
                var kind = reader.ReadByte(); var direction = reader.ReadInt32();
                var time = reader.ReadSingle(); var forget = reader.ReadBoolean();
                switch (kind)
                {
                    case 0: target.Panic(forget); break;
                    case 1: target.Panic(time, forget); break;
                    case 2: target.Panic(direction, time, forget); break;
                    case 3: target.Terrify(); break;
                    case 4: target.Blind(); break;
                    case 5: target.Blind(time); break;
                    case 6: target.TearGas(time); break;
                    case 7: target.Stun(time); break;
                    case 8: target.Freeze(time); break;
                }
            }
        }

        private static void ApplyWorldStatus(Unit unit, WorldSnapshot state, float age)
        {
            var tvd = unit as TestVanDammeAnim;
            age = Mathf.Max(age, WorldStatusAge(state));
            if (tvd != null) ApplyWorldPersistentStatus(tvd, state, age);
            var mook = unit as Mook;
            if (mook == null) return;
            mook.blindTime = Mathf.Max(0f, state.BlindTime - age);
            if (age == 0f || state.ScaredTime > 0f)
                WorldScaredTime.SetValue(mook, Mathf.Max(0f, state.ScaredTime - age));
            if (mook.enemyAI != null) mook.enemyAI.mentalState = (MentalState)state.Mental;
        }

        private static bool WorldSuicideEffectsPrefix(BroforceObject __instance, out WorldDamageContext __state)
        {
            __state = new WorldDamageContext { Previous = _worldDamageSource };
            if (_worldApplying != 0) return false;
            if (_worldEpoch == 0 || !IsWorldSession() || !IsWorldObject(__instance)) return true;
            if (!__instance.IsMine) return false;
            var suicide = __instance as MookSuicide;
            var undead = __instance as MookSuicideUndead;
            if (suicide != null ? suicide.exploded || suicide.HasBeenCoveredInAcid() : undead.exploded) return true;
            var entry = RegisterWorldObject(__instance);
            entry.Exploded = true; entry.Dirty = true;
            // Reuse target-owner damage routing for the explosion, including remote heroes.
            _worldDamageSource = __instance;
            return true;
        }

        private static void ReplayWorldSuicideEffects(BroforceObject obj, WorldSnapshot state)
        {
            var suicide = obj as MookSuicide;
            var undead = obj as MookSuicideUndead;
            if (suicide == null && undead == null) return;
            var x = state.X; var y = state.Y;
            var range = suicide != null ? suicide.range : undead.range;
            EffectsController.CreateExplosionRangePop(x, y, -1f, range * 2f);
            EffectsController.CreateShrapnel(suicide != null ? suicide.shrapnel : undead.shrapnel, x, y, 3f, 160f, 40f, 0f, 60f);
            if (suicide != null)
            {
                suicide.exploded = true;
                for (var i = 0; i < 4; i++)
                {
                    var offset = UnityEngine.Random.insideUnitCircle;
                    EffectsController.CreateEffect(i < 2 ? suicide.smoke1 : suicide.smoke2,
                        x + offset.x * range * 0.25f, y + offset.y * range * 0.25f, 0f,
                        UnityEngine.Random.value * (i == 1 ? 0.2f : 0.5f),
                        i == 1 ? UnityEngine.Random.insideUnitSphere * range * 3f : (Vector3)(offset * range * 3f), null);
                }
                var fire = new[] { suicide.fire1, suicide.fire2, suicide.fire3 };
                for (var i = 0; i < 3; i++)
                {
                    var offset = UnityEngine.Random.insideUnitCircle;
                    var spread = i == 1 ? 0.15f : 0.25f;
                    EffectsController.CreateShaderExplosion(suicide.explosionSize,
                        new Vector3(x + offset.x * range * spread, y + offset.y * range * spread),
                        i == 1 ? 0f : (i == 0 ? UnityEngine.Random.value * 0.5f : 0.15f + UnityEngine.Random.value * 0.4f));
                    offset = UnityEngine.Random.insideUnitCircle;
                    EffectsController.CreateEffect(fire[i], x + offset.x * range * 0.25f, y + offset.y * range * 0.25f,
                        0.1f + UnityEngine.Random.value * 0.2f, offset * range * 1.5f);
                }
            }
            else
            {
                undead.exploded = true;
                EffectsController.CreateSlimeExplosion(x, y, 15f, 15f, 140f, 0f, 0f, 0f, 0f, 0, 20, 120f, 0f, Vector3.up, BloodColor.Red);
            }
            // Damage, burning, terrain changes and Gib are delivered through authoritative state.
            SortOfFollow.Shake(1f);
        }
    }
}
