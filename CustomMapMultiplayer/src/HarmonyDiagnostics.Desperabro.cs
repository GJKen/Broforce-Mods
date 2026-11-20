using System.Reflection;
using HarmonyLib;
using Networking;

namespace CustomMapMultiplayer
{
    internal static partial class HarmonyDiagnostics
    {
        private static readonly FieldInfo MariachiParent = AccessTools.Field(typeof(Desperabro), "desperabro");
        private static readonly FieldInfo MariachiCampa = AccessTools.Field(typeof(Desperabro), "campaBro");
        private static readonly FieldInfo MariachiQuino = AccessTools.Field(typeof(Desperabro), "quinoBro");
        private static readonly FieldInfo MariachiSerenading = AccessTools.Field(typeof(Desperabro), "isSerenading");
        private static readonly MethodInfo MariachiStart = AccessTools.Method(typeof(Desperabro), "StartSerenading", null, null);
        private static readonly MethodInfo MariachiStop = AccessTools.Method(typeof(Desperabro), "StopSerenading", null, null);
        private static readonly MethodInfo MariachiFinishAudio = AccessTools.Method(typeof(Desperabro), "FinishSerenadingAudio", null, null);

        private static void PatchMariachiSynchronization()
        {
            PatchWorldMethod(AccessTools.Method(typeof(Desperabro), "SetupMariachiBro", null, null), null, "MariachiSetupPostfix");
            PatchWorldMethod(AccessTools.Method(typeof(Desperabro), "Start", null, null), null, "MariachiInitializedPostfix");
            PatchWorldMethod(MariachiStart, null, "MariachiStartPostfix");
            PatchWorldMethod(MariachiStop, "MariachiStopPrefix", "MariachiStopPostfix");
            PatchWorldMethod(AccessTools.Method(typeof(Desperabro), "UseSpecial", null, null), "MariachiOwnerPrefix", null);
            PatchWorldMethod(AccessTools.Method(typeof(Desperabro), "FinishSerenadingAndUnleashHell", null, null), "MariachiOwnerPrefix", null);
        }

        private static bool IsMariachiSession()
        {
            return _worldEpoch != 0 && IsWorldSession();
        }

        private static bool MariachiOwnerPrefix(Desperabro __instance)
        {
            // Replicas receive reliable transitions, rather than replaying special
            // animation/input and independently producing the ending shock wave.
            return !IsMariachiSession() || __instance.IsMine;
        }

        private static void MariachiSetupPostfix(Desperabro __instance, int __1, int __3)
        {
            if (!IsMariachiSession() || __1 < 0 || __1 >= HeroController.players.Length ||
                HeroController.players[__1] == null || (__3 != 0 && __3 != 1)) return;
            var parent = HeroController.players[__1].character as Desperabro;
            if (parent == null || parent == __instance || parent.Owner != __instance.Owner ||
                parent.mariachiBroType != Desperabro.MariachiBroType.Desperabro) return;
            // Native Setup initializes the instrument, but only SpawnMariachiBand
            // establishes these links, and that method runs on the owner alone.
            MariachiParent.SetValue(__instance, parent);
            (__3 == 0 ? MariachiCampa : MariachiQuino).SetValue(parent, __instance);
            MariachiInitializedPostfix(__instance);
        }

        private static void MariachiInitializedPostfix(Desperabro __instance)
        {
            // BroBase.Start disables owner sync, even when Setup ran first.
            // Partners have no directional input to enable it again afterward.
            if (IsMariachiSession() && __instance.mariachiBroType != Desperabro.MariachiBroType.Desperabro &&
                __instance.IsMine && !__instance.Syncronize) __instance.EnableSyncing(true, true);
        }

        private static void MariachiStartPostfix(Desperabro __instance)
        {
            if (IsMariachiSession() && __instance.IsMine &&
                __instance.mariachiBroType == Desperabro.MariachiBroType.Desperabro)
                SendMariachiTransition(__instance, true);
        }

        private static void MariachiStopPrefix(Desperabro __instance, out bool __state)
        {
            __state = (bool)MariachiSerenading.GetValue(__instance);
        }

        private static void MariachiStopPostfix(Desperabro __instance, bool __state)
        {
            if (__state && IsMariachiSession() && __instance.IsMine &&
                __instance.mariachiBroType == Desperabro.MariachiBroType.Desperabro)
                SendMariachiTransition(__instance, false);
        }

        private static void SendMariachiTransition(Desperabro hero, bool active)
        {
            global::Networking.Networking.RPC<long, NID, bool>(PID.TargetOthers,
                new RpcSignature<long, NID, bool>(ReceiveMariachiTransitionRPC), _worldEpoch, hero.Nid, active, false);
        }

        [AllowedRPC]
        private static void ReceiveMariachiTransitionRPC(long epoch, NID id, bool active)
        {
            if (!IsMariachiSession() || epoch != _worldEpoch || id.Sid != Connect.SessionID ||
                RPCController.CurrentRPC == null) return;
            var hero = Registry.GetObject(id) as Desperabro;
            if (hero == null || hero.IsMine || hero.mariachiBroType != Desperabro.MariachiBroType.Desperabro ||
                RPCController.CurrentRPC.messageInfo.Sender != (hero.Owner == PID.TargetServer ? PID.ServerID : hero.Owner)) return;
            if (active)
            {
                // Start retains the native presentation and timer. Its spawn
                // method already excludes nonowners, so no extra band is created.
                if (!(bool)MariachiSerenading.GetValue(hero)) MariachiStart.Invoke(hero, null);
                return;
            }
            if ((bool)MariachiSerenading.GetValue(hero)) MariachiFinishAudio.Invoke(hero, null);
            MariachiStop.Invoke(hero, null);
            var campa = MariachiCampa.GetValue(hero) as Desperabro;
            var quino = MariachiQuino.GetValue(hero) as Desperabro;
            if (campa != null) MariachiStop.Invoke(campa, null);
            if (quino != null) MariachiStop.Invoke(quino, null);
            // Blind/shock wave/statistics/jump remain on the original owning
            // Finish stack. Native position sync supplies the replica's jump.
        }
    }
}
