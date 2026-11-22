using System.Collections.Generic;
using System.Reflection;
using Networking;
using UnityEngine;

namespace EnemyAIEnhancement
{
    internal static partial class Controller
    {
        private static readonly FieldInfo ninjaJump = typeof(MookNinjaAI).GetField("jumpOutSingleFrame",
            BindingFlags.Instance | BindingFlags.NonPublic);
        private static readonly FieldInfo ninjaSpotted = typeof(MookNinjaAI).GetField("hasBeenSpotted",
            BindingFlags.Instance | BindingFlags.NonPublic);
        private static readonly Dictionary<MookNinjaAI, ActionObject> ninjaWarnings = new Dictionary<MookNinjaAI, ActionObject>();
        private static readonly HashSet<MookNinjaAI> ninjaRevealsSent = new HashSet<MookNinjaAI>();

        internal static bool CanShareNinjaReveal(PolymorphicAI ai, Mook mook)
        {
            return mook != null && mook.GetType() == typeof(MookNinja) && ai.GetType() == typeof(MookNinjaAI) &&
                Supported(ai, mook) && ai.IsRevealed();
        }

        internal static void ShareNinjaReveal(PolymorphicAI ai, Mook mook)
        {
            if (Connect.IsOffline || !CanShareNinjaReveal(ai, mook) || ai.Nid.GameObjectID == 0) return;
            MookNinjaAI ninja = (MookNinjaAI)ai;
            if (!ninjaRevealsSent.Add(ninja)) return;
            // LateUpdate has already submitted the native queue on the same reliable RPC stream.
            global::Networking.Networking.RPC<NID>(PID.TargetOthers, new RpcSignature<NID>(ReceiveNinjaReveal), ai.Nid, false);
        }

        [AllowedRPC]
        private static void ReceiveNinjaReveal(NID id)
        {
            if (Connect.IsOffline || id.Sid != Connect.SessionID || RPCController.CurrentRPC == null) return;
            MookNinjaAI ai = Registry.GetObject(id) as MookNinjaAI;
            if (ai != null) AcceptNinjaReveal(ai, ai.GetComponent<Mook>(), RPCController.LastSender);
        }

        internal static bool AcceptNinjaReveal(PolymorphicAI ai, Mook mook, PID sender)
        {
            if (!Plugin.Enabled || mook == null || mook.GetType() != typeof(MookNinja) || ai.GetType() != typeof(MookNinjaAI) ||
                ai.IsMine || mook.IsMine || mook.health <= 0 || mook.playerNum >= 0 || mook.beingControlledByTriggerAction ||
                sender == null || sender != (ai.Owner == PID.TargetServer ? PID.ServerID : ai.Owner) ||
                sender != (mook.Owner == PID.TargetServer ? PID.ServerID : mook.Owner) || ai.IsRevealed()) return false;
            // Monotonic acknowledgement of an actual owner reveal, without FullyAlert or jump replay.
            // SetRevealed can HearSound while Hiding; synchronizing this flag must leave the queue intact.
            ninjaSpotted.SetValue(ai, true);
            return true;
        }

        internal static void TrackNinjaWarning(MookNinjaAI ai, Mook mook, bool wasHidden)
        {
            if (wasHidden && mook != null && mook.GetType() == typeof(MookNinja) &&
                ai.GetType() == typeof(MookNinjaAI) && (bool)ninjaJump.GetValue(ai))
            {
                // Track the actual native wait even while disabled; toggling must not end the ambush.
                ninjaWarnings[ai] = ai.actionQueue[0];
                memories.Remove(ai);
            }
        }

        private static bool NinjaNativePhase(PolymorphicAI ai, Mook mook)
        {
            if (mook == null || mook.GetType() != typeof(MookNinja) || ai.GetType() != typeof(MookNinjaAI)) return false;
            MookNinjaAI ninja = (MookNinjaAI)ai;
            if (!ninja.IsRevealed() || (bool)ninjaJump.GetValue(ninja) || !mook.IsOnGround() || Mathf.Abs(mook.yI) > 1f)
                return true;
            ActionObject warning;
            if (ninjaWarnings.TryGetValue(ninja, out warning))
            {
                if (ai.actionQueue.Contains(warning)) return true;
                ninjaWarnings.Remove(ninja);
            }
            return false;
        }
    }
}
