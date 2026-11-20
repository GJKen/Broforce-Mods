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
        private static TestVanDammeAnim _worldHitSoundUnit;
        private static BroforceObject _worldGrenadeSoundSource;
        private static readonly Dictionary<string, AudioClip> WorldHitSoundClips = new Dictionary<string, AudioClip>();

        private static void PatchWorldHitSounds()
        {
            foreach (var type in typeof(Grenade).Assembly.GetTypes())
                if (typeof(Grenade).IsAssignableFrom(type))
                    foreach (var method in type.GetMethods(WorldFields | BindingFlags.DeclaredOnly))
                        if (method.Name == "Death")
                            PatchWorldMethod(method, "WorldGrenadeSoundPrefix", null, null, "WorldGrenadeSoundFinalizer");
            foreach (var type in typeof(TestVanDammeAnim).Assembly.GetTypes())
                if (typeof(TestVanDammeAnim).IsAssignableFrom(type))
                    foreach (var method in type.GetMethods(WorldFields | BindingFlags.DeclaredOnly))
                        if (method.Name == "PlayHitSound" || method.Name == "PlayHurtSound" ||
                            method.Name == "PlayBurnSound" || method.Name == "PlayFizzleSound")
                            PatchWorldMethod(method, "WorldHitSoundPrefix", null, null, "WorldHitSoundFinalizer");
            PatchWorldMethod(AccessTools.Method(typeof(Sound), "PlaySoundEffectAt", new[] { typeof(AudioClip), typeof(float),
                typeof(Vector3), typeof(float), typeof(bool), typeof(bool), typeof(bool), typeof(float) }, null), null, "WorldHitSoundPostfix");
        }

        private static void WorldHitSoundPrefix(TestVanDammeAnim __instance, out TestVanDammeAnim __state)
        { __state = _worldHitSoundUnit; _worldHitSoundUnit = __instance; }

        private static void WorldHitSoundFinalizer(TestVanDammeAnim __state) { _worldHitSoundUnit = __state; }

        private static void WorldGrenadeSoundPrefix(Grenade __instance, out BroforceObject __state)
        { __state = _worldGrenadeSoundSource; _worldGrenadeSoundSource = __instance; }

        private static void WorldGrenadeSoundFinalizer(BroforceObject __state) { _worldGrenadeSoundSource = __state; }

        private static void WorldHitSoundPostfix(AudioClip clip, float v, Vector3 pos, float pitch,
            bool bypassReverb, bool bypassEffects, bool neverPool, float delay, AudioSource __result)
        {
            BroforceObject unit = _worldHitSoundUnit;
            if (unit == null && _worldGrenadeEffectsDepth == 0) unit = _worldGrenadeSoundSource;
            if (clip == null || __result == null || unit == null || _worldApplying != 0 || _worldEpoch == 0 ||
                !IsWorldSession() || !IsWorldObject(unit) || !unit.IsMine) return;
            var stream = new MemoryStream();
            using (var writer = new BinaryWriter(stream))
            {
                writer.Write((byte)unit.Owner.AsInt); writer.Write(clip.name);
                writer.Write(v); writer.Write(pos.x); writer.Write(pos.y); writer.Write(pos.z); writer.Write(pitch);
                writer.Write(bypassReverb); writer.Write(bypassEffects); writer.Write(neverPool); writer.Write(delay);
                global::Networking.Networking.RPC<long, byte[]>(PID.TargetOthers,
                    new RpcSignature<long, byte[]>(ReceiveWorldHitSoundRPC), _worldEpoch, stream.ToArray(), true);
            }
        }

        [AllowedRPC]
        private static void ReceiveWorldHitSoundRPC(long epoch, byte[] bytes)
        {
            if (!AcceptWorldRpc(epoch)) return;
            var stream = new MemoryStream(bytes);
            using (var reader = new BinaryReader(stream))
            {
                var owner = reader.ReadByte();
                if (!IsWorldRpcOwner(owner)) return;
                var name = reader.ReadString(); var volume = reader.ReadSingle();
                var position = new Vector3(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle());
                var pitch = reader.ReadSingle(); var reverb = reader.ReadBoolean(); var effects = reader.ReadBoolean();
                var pool = reader.ReadBoolean(); var delay = reader.ReadSingle();
                AudioClip clip;
                if (!WorldHitSoundClips.TryGetValue(name, out clip) || clip == null)
                    foreach (AudioClip candidate in Resources.FindObjectsOfTypeAll(typeof(AudioClip)))
                        if (candidate.name == name) { clip = candidate; WorldHitSoundClips[name] = candidate; break; }
                if (clip != null) Sound.GetInstance().PlaySoundEffectAt(clip, volume, position, pitch, reverb, effects, pool, delay);
            }
        }
    }
}
