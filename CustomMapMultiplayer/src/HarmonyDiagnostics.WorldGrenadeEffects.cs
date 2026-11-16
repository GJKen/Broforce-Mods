using System.IO;
using System.Reflection;
using HarmonyLib;
using Networking;
using UnityEngine;

namespace CustomMapMultiplayer
{
    internal static partial class HarmonyDiagnostics
    {
        private static readonly MethodInfo WorldGrenadeDeathSound = AccessTools.Method(typeof(Grenade), "PlayDeathSound", null, null);

        private static bool WorldGrenadeEffectsPrefix(Grenade __instance, out UnityEngine.Random.State __state)
        {
            __state = UnityEngine.Random.state;
            return _worldApplying != 0 || !IsWorldReplica(__instance);
        }

        private static void WorldGrenadeEffectsPostfix(Grenade __instance, bool __runOriginal, UnityEngine.Random.State __state)
        {
            if (!__runOriginal || _worldApplying != 0 || _worldEpoch == 0 || !IsWorldSession() ||
                !IsWorldObject(__instance) || !__instance.IsMine) return;
            using (var stream = new MemoryStream())
            using (var writer = new BinaryWriter(stream))
            {
                WriteWorldId(writer, __instance.Nid); writer.Write((byte)__instance.Owner.AsInt);
                writer.Write(__instance.X); writer.Write(__instance.Y); writer.Write(__instance.range);
                writer.Write(__instance.hugeExplosion); writer.Write(__instance.hugeExplosionLowPassM);
                writer.Write(WorldRandomStateSupported);
                if (WorldRandomStateSupported) WriteWorldRandomState(writer, __state);
                global::Networking.Networking.RPC<long, byte[]>(PID.TargetOthers,
                    new RpcSignature<long, byte[]>(ReceiveWorldGrenadeEffectsRPC), _worldEpoch, stream.ToArray(), true);
            }
        }

        [AllowedRPC]
        private static void ReceiveWorldGrenadeEffectsRPC(long epoch, byte[] bytes)
        {
            if (_worldEpoch == 0 || epoch != _worldEpoch || !IsWorldSession() || RPCController.CurrentRPC == null) return;
            using (var stream = new MemoryStream(bytes))
            using (var reader = new BinaryReader(stream))
            {
                var id = ReadWorldId(reader); var owner = reader.ReadByte();
                var sender = RPCController.CurrentRPC.messageInfo.Sender;
                if (id.Sid != Connect.SessionID || (owner == PID.TargetServer.AsInt ? sender != PID.ServerID : sender.AsInt != owner)) return;
                var x = reader.ReadSingle(); var y = reader.ReadSingle(); var range = reader.ReadSingle();
                var huge = reader.ReadBoolean(); var lowPass = reader.ReadSingle();
                var hasRandomState = reader.ReadBoolean();
                UnityEngine.Random.State randomState = default(UnityEngine.Random.State);
                if (hasRandomState && !TryReadWorldRandomState(reader, out randomState)) return;
                var previousRandomState = UnityEngine.Random.state;
                try
                {
                    if (hasRandomState) UnityEngine.Random.state = randomState;
                    // Replay only the visuals, even if removal already destroyed the grenade.
                    // Grenade.MakeEffects also damages doodads; never rerun that gameplay here.
                    EffectsController.CreateExplosionRangePop(x, y, -1f, range * 2f);
                    if (huge)
                        EffectsController.CreateHugeExplosion(x, y, range * .25f, range * .25f, 120f, 1f, 24f, 1f, .5f, 2, 140, 250f, range * 1.5f, lowPass, .2f);
                    else
                        EffectsController.CreateExplosion(x, y, range * .25f, range * .25f, 120f, 1f, range * 1.5f, 1f, 0f, true);
                    var grenade = Registry.GetObject(id) as Grenade;
                    if (grenade != null) WorldGrenadeDeathSound.Invoke(grenade, null);
                }
                finally
                {
                    if (hasRandomState) UnityEngine.Random.state = previousRandomState;
                }
            }
        }
    }
}
