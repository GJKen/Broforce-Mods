using Networking;
using UnityEngine;

namespace TestMod
{
    public static partial class Plugin
    {
        private enum PlayerCheat { Invincible, Special, Clear, AutoRevive, Revive }
        private static PID _cheatNetworkHost;
        private static float _nextCheatSyncAt;

        private static void SendPlayerCheatCommand(int playerNumber, PlayerCheat cheat, bool enabled)
        {
            if (Connect.IsOffline || PID.IamServer())
            {
                ApplyPlayerCheatCommand(playerNumber, cheat, enabled);
                return;
            }
            global::Networking.Networking.RPC<int, int, bool>(PID.TargetServer, true, false,
                new RpcSignature<int, int, bool>(RequestPlayerCheatRPC), playerNumber, (int)cheat, enabled);
            RPCBatcher.FlushQueue();
        }

        [AllowedRPC]
        private static void RequestPlayerCheatRPC(int playerNumber, int cheat, bool enabled)
        {
            if (Connect.IsOffline || !PID.IamServer() || _behaviour == null ||
                RPCController.CurrentRPC == null ||
                !Connect.Layer.PlayerIDPairs.ContainsKey(RPCController.CurrentRPC.messageInfo.Sender) ||
                playerNumber < 0 || playerNumber > 3 || cheat < 0 || cheat > (int)PlayerCheat.Revive) return;
            RefreshPlayerCheatSession();
            ApplyPlayerCheatCommand(playerNumber, (PlayerCheat)cheat, enabled);
        }

        private static void ApplyPlayerCheatCommand(int playerNumber, PlayerCheat cheat, bool enabled)
        {
            var bit = 1 << playerNumber;
            switch (cheat)
            {
                case PlayerCheat.Invincible:
                    _invinciblePlayers = enabled ? _invinciblePlayers | bit : _invinciblePlayers & ~bit;
                    break;
                case PlayerCheat.Special:
                    _infiniteSpecialPlayers = enabled ? _infiniteSpecialPlayers | bit : _infiniteSpecialPlayers & ~bit;
                    break;
                case PlayerCheat.Clear:
                    _invinciblePlayers = _infiniteSpecialPlayers = _autoRevivePlayers = 0;
                    break;
                case PlayerCheat.AutoRevive:
                    _autoRevivePlayers = enabled ? _autoRevivePlayers | bit : _autoRevivePlayers & ~bit;
                    break;
                case PlayerCheat.Revive:
                    SendPlayerRevival(playerNumber);
                    return;
            }
            if (!Connect.IsOffline) BroadcastPlayerCheats();
        }

        private static void BroadcastPlayerCheats()
        {
            global::Networking.Networking.RPC<int, int, int>(PID.TargetOthers, true, false,
                new RpcSignature<int, int, int>(ReceivePlayerCheatsRPC), _invinciblePlayers,
                _infiniteSpecialPlayers, _autoRevivePlayers);
            RPCBatcher.FlushQueue();
        }

        [AllowedRPC]
        private static void ReceivePlayerCheatsRPC(int invinciblePlayers, int specialPlayers, int autoRevivePlayers)
        {
            if (Connect.IsOffline || _behaviour == null || RPCController.CurrentRPC == null ||
                RPCController.CurrentRPC.messageInfo.Sender != PID.ServerID ||
                invinciblePlayers < 0 || invinciblePlayers > 15 || specialPlayers < 0 || specialPlayers > 15 ||
                autoRevivePlayers < 0 || autoRevivePlayers > 15) return;
            RefreshPlayerCheatSession();
            _invinciblePlayers = invinciblePlayers;
            _infiniteSpecialPlayers = specialPlayers;
            _autoRevivePlayers = autoRevivePlayers;
        }

        private static void RefreshPlayerCheatSession()
        {
            var host = Connect.IsOffline ? null : PID.ServerID;
            if (_cheatNetworkHost == host) return;
            ResetPlayerCheats();
            _cheatNetworkHost = host;
        }

        private static void UpdatePlayerCheatNetwork()
        {
            RefreshPlayerCheatSession();
            if (!Connect.IsOffline && PID.IamServer() && Time.realtimeSinceStartup >= _nextCheatSyncAt)
            {
                _nextCheatSyncAt = Time.realtimeSinceStartup + 1f;
                // Reapply the snapshot after a level load, or when another peer joins/enables the mod.
                BroadcastPlayerCheats();
            }
        }

        private static void ResetPlayerCheats()
        {
            _invinciblePlayers = _infiniteSpecialPlayers = _autoRevivePlayers = 0;
            _cheatNetworkHost = null;
            _nextCheatSyncAt = 0f;
            ResetPlayerRevival();
        }

        private static void StopPlayerCheats()
        {
            if (_invinciblePlayers != 0 || _infiniteSpecialPlayers != 0 || _autoRevivePlayers != 0)
                SendPlayerCheatCommand(0, PlayerCheat.Clear, false);
            ResetPlayerCheats();
        }
    }
}
