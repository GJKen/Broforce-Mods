using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using System.IO;
using Networking;

namespace CustomMapMultiplayer
{
    internal static partial class HarmonyDiagnostics
    {
        private static void PatchWorldHitEffects()
        {
            var damage = AccessTools.Method(typeof(TestVanDammeAnim), "Damage", new[] {
                typeof(int), typeof(DamageType), typeof(float), typeof(float), typeof(int),
                typeof(UnityEngine.MonoBehaviour), typeof(float), typeof(float) }, null);
            _harmony.Patch(damage, null, null, new HarmonyMethod(typeof(HarmonyDiagnostics).GetMethod(
                "WorldHitEffectsTranspiler", BindingFlags.NonPublic | BindingFlags.Static)), null);
            _harmony.Patch(AccessTools.Method(typeof(TestVanDammeAnim), "Death", null, null), null, null,
                new HarmonyMethod(AccessTools.Method(typeof(HarmonyDiagnostics), "WorldHitEffectsTranspiler", null, null)), null);
        }

        private static IEnumerable<CodeInstruction> WorldHitEffectsTranspiler(IEnumerable<CodeInstruction> instructions)
        {
            foreach (var instruction in instructions)
            {
                var method = instruction.operand as MethodInfo;
                if (method != null && method.DeclaringType == typeof(EffectsController) &&
                    (method.Name == "CreateMeleeStrikeEffect" || method.Name == "CreateBloodParticles" && method.GetParameters().Length == 9))
                {
                    var load = new CodeInstruction(OpCodes.Ldarg_0, null);
                    load.labels.AddRange(instruction.labels); instruction.labels.Clear();
                    load.blocks.AddRange(instruction.blocks); instruction.blocks.Clear();
                    instruction.operand = AccessTools.Method(typeof(HarmonyDiagnostics),
                        method.Name == "CreateMeleeStrikeEffect" ? "WorldMeleeStrikeEffect" : "WorldBloodParticlesEffect", null, null);
                    yield return load;
                }
                yield return instruction;
            }
        }

        private static void WorldMeleeStrikeEffect(float x, float y, float vx, float vy, TestVanDammeAnim unit)
        {
            EffectsController.CreateMeleeStrikeEffect(x, y, vx, vy);
            SendWorldHitEffect(unit, 0, BloodColor.None, x, y, 0, 0f, 0f, 0f, vx, vy);
        }

        private static void WorldBloodParticlesEffect(BloodColor color, float x, float y, int count,
            float radiusX, float radiusY, float force, float vx, float vy, TestVanDammeAnim unit)
        {
            WorldEntry entry;
            if (_worldApplying == 0 || !WorldEntries.TryGetValue(unit.Nid, out entry) ||
                (entry.PresentedFlags & (WorldFlags.Dead | WorldFlags.Gibbed)) == 0)
                EffectsController.CreateBloodParticles(color, x, y, count, radiusX, radiusY, force, vx, vy);
            SendWorldHitEffect(unit, 1, color, x, y, count, radiusX, radiusY, force, vx, vy);
        }

        private static void SendWorldHitEffect(TestVanDammeAnim unit, byte kind, BloodColor color,
            float x, float y, int count, float radiusX, float radiusY, float force, float vx, float vy)
        {
            if (_worldApplying != 0 || _worldEpoch == 0 || !IsWorldSession() || !IsWorldObject(unit) || !unit.IsMine) return;
            // Capture the actual visual call. Blocked hits and early returns produce no invented blood.
            using (var stream = new MemoryStream())
            using (var writer = new BinaryWriter(stream))
            {
                writer.Write((byte)unit.Owner.AsInt); writer.Write(kind); writer.Write((int)color);
                writer.Write(_worldHitRequestor); writer.Write(_worldSettlingHit);
                writer.Write(x); writer.Write(y); writer.Write(count); writer.Write(radiusX); writer.Write(radiusY);
                writer.Write(force); writer.Write(vx); writer.Write(vy);
                global::Networking.Networking.RPC<long, byte[]>(PID.TargetOthers,
                    new RpcSignature<long, byte[]>(ReceiveWorldHitEffectRPC), _worldEpoch, stream.ToArray(), true);
            }
        }

        [AllowedRPC]
        private static void ReceiveWorldHitEffectRPC(long epoch, byte[] bytes)
        {
            if (_worldEpoch == 0 || epoch != _worldEpoch || !IsWorldSession() || RPCController.CurrentRPC == null) return;
            using (var stream = new MemoryStream(bytes))
            using (var reader = new BinaryReader(stream))
            {
                var owner = reader.ReadByte();
                var sender = RPCController.CurrentRPC.messageInfo.Sender;
                if (owner == PID.TargetServer.AsInt ? sender != PID.ServerID : sender.AsInt != owner) return;
                var kind = reader.ReadByte(); var color = (BloodColor)reader.ReadInt32();
                var requestor = reader.ReadInt32(); var request = reader.ReadInt32();
                var x = reader.ReadSingle(); var y = reader.ReadSingle(); var count = reader.ReadInt32();
                var radiusX = reader.ReadSingle(); var radiusY = reader.ReadSingle(); var force = reader.ReadSingle();
                var vx = reader.ReadSingle(); var vy = reader.ReadSingle();
                byte presented;
                if (requestor == PID.MyID.AsInt && WorldPresentedHitEffects.TryGetValue(request, out presented) &&
                    (presented & (1 << kind)) != 0) return;
                // No object lookup: a fatal hit must remain visible after the target has been removed.
                if (kind == 0) EffectsController.CreateMeleeStrikeEffect(x, y, vx, vy);
                else if (kind == 1) EffectsController.CreateBloodParticles(color, x, y, count, radiusX, radiusY, force, vx, vy);
            }
        }

        private static void PatchPlayerOverlapHighFiveBehavior()
        {
            try
            {
                var pressHighFiveMelee = typeof(TestVanDammeAnim).GetMethod(
                    "PressHighFiveMelee",
                    BindingFlags.Instance | BindingFlags.NonPublic,
                    null,
                    new[] { typeof(bool) },
                    null);
                var transpiler = typeof(HarmonyDiagnostics).GetMethod(
                    "PressHighFiveMeleeTranspiler",
                    BindingFlags.NonPublic | BindingFlags.Static);
                if (pressHighFiveMelee == null || transpiler == null)
                {
                    DiagnosticLog.Warning(
                        "Player-overlap melee patch could not resolve TestVanDammeAnim.PressHighFiveMelee.");
                    return;
                }

                _harmony.Patch(
                    pressHighFiveMelee,
                    null,
                    null,
                    new HarmonyMethod(transpiler),
                    null);
                DiagnosticLog.Info(
                    "Player-overlap melee behavior enabled; UMM setting controls automatic high-five suppression.");
            }
            catch (Exception exception)
            {
                DiagnosticLog.Warning(
                    "Player-overlap melee patch failed: " + exception);
            }
        }

        private static IEnumerable<CodeInstruction> PressHighFiveMeleeTranspiler(
            IEnumerable<CodeInstruction> instructions)
        {
            var result = new List<CodeInstruction>(instructions);
            var playerNearby = typeof(HeroController).GetMethod(
                "IsAnotherPlayerNearby",
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static,
                null,
                new[] { typeof(int), typeof(float), typeof(float), typeof(float), typeof(float) },
                null);
            var replacement = typeof(HarmonyDiagnostics).GetMethod(
                "IsAnotherPlayerNearbyForHighFive",
                BindingFlags.NonPublic | BindingFlags.Static);
            if (playerNearby == null || replacement == null)
            {
                DiagnosticLog.Warning(
                    "Player-overlap melee transpiler could not resolve the nearby-player methods.");
                return result;
            }

            var replacedCount = 0;
            for (var index = 0; index < result.Count; index++)
            {
                if (!playerNearby.Equals(result[index].operand as MethodInfo))
                {
                    continue;
                }

                result[index].operand = replacement;
                replacedCount++;
            }

            if (replacedCount != 1)
            {
                DiagnosticLog.Warning(
                    "Player-overlap melee transpiler expected one nearby-player check; found " +
                    replacedCount + ".");
            }

            return result;
        }

        private static bool IsAnotherPlayerNearbyForHighFive(
            int currentPlayerNum,
            float x,
            float y,
            float xRange,
            float yRange)
        {
            var settings = Plugin.Settings;
            if (settings != null && settings.DisablePlayerOverlapHighFive)
            {
                return false;
            }

            return HeroController.IsAnotherPlayerNearby(
                currentPlayerNum,
                x,
                y,
                xRange,
                yRange);
        }
    }
}
