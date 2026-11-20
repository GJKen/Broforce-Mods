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
        private static readonly FieldInfo WorldDoodadDamaged = AccessTools.Field(typeof(DoodadDestroyable), "isDamaged");

        private static void CaptureWorldDoodadState(BroforceObject obj, WorldSnapshot state)
        {
            var doodad = obj as Doodad;
            if (doodad == null) return;
            state.DoodadVisual = 1;
            var destroyable = doodad as DoodadDestroyable;
            var sprite = destroyable == null ? doodad.GetComponent<SpriteSM>() : destroyable.sprite;
            if (sprite != null) { state.DoodadVisual |= 2; state.DoodadPixel = sprite.lowerLeftPixel; }
            var renderer = doodad.GetComponent<Renderer>();
            if (renderer != null)
            {
                var material = renderer.sharedMaterial;
                if (material != null)
                    state.DoodadMaterial = material == doodad.damageStateMaterial ? (byte)1 :
                        material == doodad.damageGreaterStateMaterial ? (byte)2 :
                        material == doodad.skeletonMaterial ? (byte)3 : material == doodad.bloodyMaterial ? (byte)4 : (byte)0;
            }
            if (destroyable == null) return;
            state.DoodadVisual |= 4;
            if ((bool)WorldDoodadDamaged.GetValue(destroyable)) state.DoodadVisual |= 8;
            if (destroyable.collidersPristine != null && destroyable.collidersPristine.activeSelf) state.DoodadColliders |= 1;
            if (destroyable.collidersDamaged != null && destroyable.collidersDamaged.activeSelf) state.DoodadColliders |= 2;
            if (destroyable.collidersDestroyed != null && destroyable.collidersDestroyed.activeSelf) state.DoodadColliders |= 4;
            var collider = destroyable.GetComponent<Collider>();
            if (collider != null && collider.enabled) state.DoodadColliders |= 8;
            var children = destroyable.prinstineChildren;
            if (children != null)
            {
                state.DoodadChildren = new byte[children.Length];
                for (var i = 0; i < children.Length; i++)
                    state.DoodadChildren[i] = children[i] != null && children[i].activeSelf ? (byte)1 : (byte)0;
            }
        }

        private static bool WorldDoodadChanged(WorldSnapshot a, WorldSnapshot b)
        {
            if (a.DoodadVisual != b.DoodadVisual || a.DoodadColliders != b.DoodadColliders ||
                a.DoodadMaterial != b.DoodadMaterial || a.DoodadPixel.x != b.DoodadPixel.x || a.DoodadPixel.y != b.DoodadPixel.y) return true;
            var ac = a.DoodadChildren; var bc = b.DoodadChildren;
            if ((ac == null ? 0 : ac.Length) != (bc == null ? 0 : bc.Length)) return true;
            if (ac != null) for (var i = 0; i < ac.Length; i++) if (ac[i] != bc[i]) return true;
            return false;
        }

        private static void ApplyWorldDoodadState(Doodad doodad, WorldSnapshot state)
        {
            if (state.DoodadVisual == 0) return;
            // Apply captured results, never Hurt/Damage: baselines must not replay transient effects.
            var destroyable = doodad as DoodadDestroyable;
            var sprite = destroyable == null ? doodad.GetComponent<SpriteSM>() : destroyable.sprite;
            if ((state.DoodadVisual & 2) != 0 && sprite != null) sprite.SetLowerLeftPixel(state.DoodadPixel);
            var material = state.DoodadMaterial == 1 ? doodad.damageStateMaterial : state.DoodadMaterial == 2 ? doodad.damageGreaterStateMaterial :
                state.DoodadMaterial == 3 ? doodad.skeletonMaterial : state.DoodadMaterial == 4 ? doodad.bloodyMaterial : null;
            var renderer = doodad.GetComponent<Renderer>();
            if (material != null && renderer != null) renderer.sharedMaterial = material;
            if (destroyable == null || (state.DoodadVisual & 4) == 0) return;
            WorldDoodadDamaged.SetValue(destroyable, (state.DoodadVisual & 8) != 0);
            if (destroyable.collidersPristine != null) destroyable.collidersPristine.SetActive((state.DoodadColliders & 1) != 0);
            if (destroyable.collidersDamaged != null) destroyable.collidersDamaged.SetActive((state.DoodadColliders & 2) != 0);
            if (destroyable.collidersDestroyed != null) destroyable.collidersDestroyed.SetActive((state.DoodadColliders & 4) != 0);
            var collider = destroyable.GetComponent<Collider>();
            if (collider != null) collider.enabled = (state.DoodadColliders & 8) != 0;
            if (state.DoodadChildren != null && destroyable.prinstineChildren != null)
                for (var i = 0; i < System.Math.Min(state.DoodadChildren.Length, destroyable.prinstineChildren.Length); i++)
                    if (destroyable.prinstineChildren[i] != null) destroyable.prinstineChildren[i].SetActive(state.DoodadChildren[i] != 0);
        }

        private static void PatchWorldDoodads()
        {
            foreach (var name in new[] { "MakeEffectsDamaged", "DamageEffects" })
            {
                var method = AccessTools.Method(name == "DamageEffects" ? typeof(Doodad) : typeof(DoodadDestroyable), name, null, null);
                _harmony.Patch(method, new HarmonyMethod(AccessTools.Method(typeof(HarmonyDiagnostics), "WorldDoodadEffectPrefix", null, null)),
                    new HarmonyMethod(AccessTools.Method(typeof(HarmonyDiagnostics), "WorldDoodadEffectPostfix", null, null)), null, null);
            }
        }

        private static void WorldDoodadEffectPrefix(out UnityEngine.Random.State __state)
        { __state = UnityEngine.Random.state; }

        private static void WorldDoodadEffectPostfix(Doodad __instance, MethodBase __originalMethod, UnityEngine.Random.State __state)
        {
            if (_worldApplying != 0 || _worldEpoch == 0 || !IsWorldSession() || !IsWorldObject(__instance) || !__instance.IsMine) return;
            var entry = RegisterWorldObject(__instance);
            using (var stream = new MemoryStream())
            using (var w = new BinaryWriter(stream))
            {
                WriteWorldId(w, __instance.Nid); w.Write((byte)__instance.Owner.AsInt); w.Write(entry.Life);
                w.Write(++_worldSequence); w.Write(__originalMethod.Name == "DamageEffects");
                w.Write(__instance.X); w.Write(__instance.Y);
                var damage = __instance.lastDamageObject;
                w.Write(damage == null ? 0f : damage.xForce); w.Write(damage == null ? 0f : damage.yForce);
                w.Write(WorldRandomStateSupported);
                if (WorldRandomStateSupported) WriteWorldRandomState(w, __state);
                global::Networking.Networking.RPC<long, byte[]>(PID.TargetOthers,
                    new RpcSignature<long, byte[]>(ReceiveWorldDoodadEffectRPC), _worldEpoch, stream.ToArray(), true);
            }
        }

        [AllowedRPC]
        private static void ReceiveWorldDoodadEffectRPC(long epoch, byte[] bytes)
        {
            if (_worldEpoch == 0 || epoch != _worldEpoch || !IsWorldSession() || RPCController.CurrentRPC == null) return;
            using (var stream = new MemoryStream(bytes))
            using (var r = new BinaryReader(stream))
            {
                var id = ReadWorldId(r); var owner = r.ReadByte(); var life = r.ReadInt32(); var revision = r.ReadInt32();
                var sender = RPCController.CurrentRPC.messageInfo.Sender;
                if (id.Sid != Connect.SessionID || (owner == PID.TargetServer.AsInt ? sender != PID.ServerID : sender.AsInt != owner)) return;
                var optional = r.ReadBoolean(); var x = r.ReadSingle(); var y = r.ReadSingle();
                var vx = r.ReadSingle(); var vy = r.ReadSingle(); var hasRandom = r.ReadBoolean();
                UnityEngine.Random.State random = default(UnityEngine.Random.State);
                if (hasRandom && !TryReadWorldRandomState(r, out random)) return;
                var doodad = Registry.GetObject(id) as Doodad;
                if (doodad == null || doodad.IsMine || doodad.Owner.AsInt != owner || doodad.isDead) return;
                var entry = RegisterWorldObject(doodad);
                if (entry.Life != life || revision <= entry.DoodadEffectRevision) return;
                entry.DoodadEffectRevision = revision;
                var position = doodad.transform.position; var damage = doodad.lastDamageObject; var previous = UnityEngine.Random.state;
                _worldApplying++;
                try
                {
                    if (hasRandom) UnityEngine.Random.state = random;
                    doodad.SetPosition(new Vector3(x, y, position.z));
                    doodad.lastDamageObject = new DamageObject(0, DamageType.Normal, vx, vy, x, y, null);
                    var method = AccessTools.Method(optional ? typeof(Doodad) : typeof(DoodadDestroyable), optional ? "DamageEffects" : "MakeEffectsDamaged", null, null);
                    method.Invoke(doodad, optional ? new object[] { vx, vy } : null);
                }
                finally
                {
                    doodad.SetPosition(position); doodad.lastDamageObject = damage;
                    if (hasRandom) UnityEngine.Random.state = previous;
                    _worldApplying--;
                }
            }
        }
    }
}
