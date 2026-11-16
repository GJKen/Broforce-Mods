using System.IO;
using Networking;
using UnityEngine;

namespace CustomMapMultiplayer
{
    internal static partial class HarmonyDiagnostics
    {
        private static void WorldUnitShockPrefix(out UnityEngine.Random.State __state)
        {
            __state = UnityEngine.Random.state;
        }

        private static void WorldUnitShockPostfix(TestVanDammeAnim __instance, float yOffset,
            bool __runOriginal, UnityEngine.Random.State __state)
        {
            if (!__runOriginal || _worldApplying != 0 || _worldEpoch == 0 || !IsWorldSession() ||
                !IsWorldObject(__instance) || !__instance.IsMine) return;
            using (var stream = new MemoryStream())
            using (var writer = new BinaryWriter(stream))
            {
                WriteWorldId(writer, __instance.Nid); writer.Write((byte)__instance.Owner.AsInt);
                writer.Write(__instance.X); writer.Write(__instance.Y); writer.Write(__instance.transform.position.z);
                writer.Write(yOffset); writer.Write(WorldRandomStateSupported);
                if (WorldRandomStateSupported) WriteWorldRandomState(writer, __state);
                global::Networking.Networking.RPC<long, byte[]>(PID.TargetOthers,
                    new RpcSignature<long, byte[]>(ReceiveWorldUnitShockRPC), _worldEpoch, stream.ToArray(), true);
            }
        }

        [AllowedRPC]
        private static void ReceiveWorldUnitShockRPC(long epoch, byte[] bytes)
        {
            if (_worldEpoch == 0 || epoch != _worldEpoch || !IsWorldSession() || RPCController.CurrentRPC == null) return;
            using (var stream = new MemoryStream(bytes))
            using (var reader = new BinaryReader(stream))
            {
                var id = ReadWorldId(reader); var owner = reader.ReadByte();
                var sender = RPCController.CurrentRPC.messageInfo.Sender;
                if (id.Sid != Connect.SessionID || (owner == PID.TargetServer.AsInt ? sender != PID.ServerID : sender.AsInt != owner)) return;
                var x = reader.ReadSingle(); var y = reader.ReadSingle(); var z = reader.ReadSingle();
                var yOffset = reader.ReadSingle(); var hasRandomState = reader.ReadBoolean();
                UnityEngine.Random.State randomState = default(UnityEngine.Random.State);
                if (hasRandomState && !TryReadWorldRandomState(reader, out randomState)) return;
                var previousRandomState = UnityEngine.Random.state;
                try
                {
                    if (hasRandomState) UnityEngine.Random.state = randomState;
                    // Visual portion of TestVanDammeAnim.CreateElectricShockPuff only.
                    // Replaying Damage would also repeat plasma accumulation and delayed explosions.
                    var puff = EffectsController.CreateElectricShock(x - 1f + UnityEngine.Random.value * 2f,
                        y + yOffset + 9f - UnityEngine.Random.value * 2f, z - 5f,
                        UnityEngine.Random.value * .02f, new Vector3(0f, 0f, .1f));
                    var unit = Registry.GetObject(id) as TestVanDammeAnim;
                    if (unit != null) puff.transform.parent = unit.transform;
                    puff.transform.localScale = new Vector3(UnityEngine.Random.Range(0, 2) * 2 - 1, 1f, 1f);
                    puff.gameObject.layer = LayerMask.NameToLayer("Units");
                }
                finally
                {
                    if (hasRandomState) UnityEngine.Random.state = previousRandomState;
                }
            }
        }
    }
}
