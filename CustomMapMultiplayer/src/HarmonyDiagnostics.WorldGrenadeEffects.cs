using System;
using System.IO;
using System.Reflection;
using HarmonyLib;
using Networking;
using UnityEngine;

namespace CustomMapMultiplayer
{
    internal static partial class HarmonyDiagnostics
    {
        private static readonly FieldInfo WorldGrenadeRandom = AccessTools.Field(typeof(Grenade), "random");
        private static int _worldGrenadeEffectsDepth;
        private struct WorldGrenadeEffectContext
        {
            internal UnityEngine.Random.State Random;
            internal int Seed, Calls;
            internal BroforceObject Previous;
        }

        private static bool WorldGrenadeEffectsPrefix(Grenade __instance, out WorldGrenadeEffectContext __state)
        {
            var random = (Randomf)WorldGrenadeRandom.GetValue(__instance);
            __state = new WorldGrenadeEffectContext { Random = UnityEngine.Random.state,
                Seed = random.seed, Calls = random.NoOfCalls, Previous = _worldDamageSource };
            _worldGrenadeEffectsDepth++;
            if (IsWorldObject(__instance) && __instance.IsMine) _worldDamageSource = __instance;
            return _worldApplying != 0 || !IsWorldReplica(__instance);
        }

        private static void WorldGrenadeEffectsFinalizer(WorldGrenadeEffectContext __state)
        {
            _worldGrenadeEffectsDepth--;
            _worldDamageSource = __state.Previous;
        }

        private static void WorldGrenadeEffectsPostfix(Grenade __instance, MethodBase __originalMethod,
            bool __runOriginal, WorldGrenadeEffectContext __state)
        {
            if (!__runOriginal || _worldApplying != 0 || _worldEpoch == 0 || !IsWorldSession() ||
                !IsWorldObject(__instance) || !__instance.IsMine ||
                __originalMethod != AccessTools.Method(__instance.GetType(), "MakeEffects", Type.EmptyTypes, null)) return;
            var stream = new MemoryStream();
            using (var writer = new BinaryWriter(stream))
            {
                WriteWorldId(writer, __instance.Nid); writer.Write((byte)__instance.Owner.AsInt);
                writer.Write(__instance.X); writer.Write(__instance.Y);
                writer.Write(__state.Seed); writer.Write(__state.Calls);
                writer.Write(WorldRandomStateSupported);
                if (WorldRandomStateSupported) WriteWorldRandomState(writer, __state.Random);
                global::Networking.Networking.RPC<long, byte[]>(PID.TargetOthers,
                    new RpcSignature<long, byte[]>(ReceiveWorldGrenadeEffectsRPC), _worldEpoch, stream.ToArray(), true);
            }
        }

        [AllowedRPC]
        private static void ReceiveWorldGrenadeEffectsRPC(long epoch, byte[] bytes)
        {
            if (!AcceptWorldRpc(epoch)) return;
            var stream = new MemoryStream(bytes);
            using (var reader = new BinaryReader(stream))
            {
                var id = ReadWorldId(reader); var owner = reader.ReadByte();
                if (!IsWorldRpcObject(id, owner)) return;
                var x = reader.ReadSingle(); var y = reader.ReadSingle();
                var seed = reader.ReadInt32(); var calls = reader.ReadInt32();
                var hasRandom = reader.ReadBoolean();
                UnityEngine.Random.State randomState = default(UnityEngine.Random.State);
                if (hasRandom && !TryReadWorldRandomState(reader, out randomState)) return;
                var grenade = Registry.GetObject(id) as Grenade;
                if (grenade == null || grenade.IsMine) return;
                var previous = UnityEngine.Random.state;
                var previousRandom = WorldGrenadeRandom.GetValue(grenade);
                var position = grenade.transform.position;
                _worldApplying++;
                try
                {
                    if (hasRandom) UnityEngine.Random.state = randomState;
                    WorldGrenadeRandom.SetValue(grenade, new Randomf(seed) { NoOfCalls = calls });
                    grenade.SetPosition(new Vector3(x, y, grenade.transform.position.z));
                    AccessTools.Method(grenade.GetType(), "MakeEffects", Type.EmptyTypes, null).Invoke(grenade, null);
                }
                finally
                {
                    grenade.SetPosition(position);
                    WorldGrenadeRandom.SetValue(grenade, previousRandom);
                    if (hasRandom) UnityEngine.Random.state = previous;
                    _worldApplying--;
                }
            }
        }
    }
}
