using System.Reflection;
using HarmonyLib;
using Networking;

namespace CustomMapMultiplayer
{
    internal static partial class HarmonyDiagnostics
    {
        private static readonly MethodInfo BroveHeartSpecial = AccessTools.Method(typeof(BroveHeart), "UseSpecial", null, null);
        private static BroveHeart _receivingBroveHeartSpecial;

        private static void PatchBroveHeartSynchronization()
        {
            PatchWorldMethod(BroveHeartSpecial, "BroveHeartSpecialPrefix", null);
        }

        private static bool BroveHeartSpecialPrefix(BroveHeart __instance)
        {
            if (_worldEpoch == 0 || !IsWorldSession()) return true;
            // InputBits may miss a short press, or animate after ammo reaches zero.
            // Replicas execute the original skill only from the owner's command.
            if (!__instance.IsMine) return ReferenceEquals(__instance, _receivingBroveHeartSpecial);
            if (__instance.SpecialAmmo > 0)
            {
                // Queue before native SetSpecialAmmoRPC on the same ordered RPC
                // stream, so the original last-charge guard still succeeds.
                global::Networking.Networking.RPC<long, NID>(PID.TargetOthers,
                    new RpcSignature<long, NID>(ReceiveBroveHeartSpecialRPC), _worldEpoch, __instance.Nid, false);
            }
            return true;
        }

        [AllowedRPC]
        private static void ReceiveBroveHeartSpecialRPC(long epoch, NID id)
        {
            if (_worldEpoch == 0 || !IsWorldSession() || epoch != _worldEpoch ||
                id.Sid != Connect.SessionID || RPCController.CurrentRPC == null) return;
            var hero = Registry.GetObject(id) as BroveHeart;
            if (hero == null || hero.IsMine ||
                RPCController.CurrentRPC.messageInfo.Sender != (hero.Owner == PID.TargetServer ? PID.ServerID : hero.Owner)) return;
            _receivingBroveHeartSpecial = hero;
            try
            {
                // Native audio, bubble, BoostHeroes(5f), and 6.7f skill clock.
                // The native ammo setter already ignores nonowners.
                BroveHeartSpecial.Invoke(hero, null);
            }
            finally
            {
                _receivingBroveHeartSpecial = null;
            }
        }
    }
}
