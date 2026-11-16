using System;
using HarmonyLib;
using Networking;
using UnityEngine;

namespace TestMod
{
    public static partial class Plugin
    {
        private sealed class PlayerDeath
        {
            internal Player Player;
            internal Map Map;
            internal TestVanDammeAnim Character;
            internal HeroType HeroType;
            internal Vector3 Position;
            internal bool Requested;
            internal bool Spawning;
        }

        private static readonly PlayerDeath[] PlayerDeaths = new PlayerDeath[4];

        internal static void CapturePlayerDeath(TestVanDammeAnim character)
        {
            var index = character.playerNum;
            if (_behaviour == null || !character.IsHero || index < 0 || index > 3 ||
                Map.Instance == null || GameModeController.LevelFinished) return;
            var player = HeroController.players[index];
            if (player == null || player.character != character || !HeroController.IsPlaying(index)) return;
            var previous = PlayerDeaths[index];
            // Death can be followed by several gib calls. Keep the first death position.
            if (previous != null && ReferenceEquals(previous.Character, character) &&
                previous.Player == player && previous.Map == Map.Instance) return;
            PlayerDeaths[index] = new PlayerDeath { Player = player, Map = Map.Instance,
                Character = character, HeroType = character.heroType,
                Position = new Vector3(character.X, character.Y, 0f) };
        }

        public static bool CanRevivePlayer(int playerNumber)
        {
            if (_behaviour == null || playerNumber < 0 || playerNumber > 3 ||
                Map.Instance == null || GameModeController.LevelFinished ||
                !HeroController.IsPlaying(playerNumber)) return false;
            var player = HeroController.players[playerNumber];
            if (player == null || player.IsAlive() || player.firstDeployment) return false;
            var death = PlayerDeaths[playerNumber];
            return death != null && death.Player == player && death.Map == Map.Instance &&
                !death.Spawning && (player.character == null || ReferenceEquals(player.character, death.Character));
        }

        public static bool RevivePlayer(int playerNumber)
        {
            if (playerNumber < 0 || playerNumber > 3)
                throw new ArgumentOutOfRangeException("playerNumber", "Use a player number from 0 to 3.");
            if (!CanRevivePlayer(playerNumber)) return false;
            SetPlayerCheat(playerNumber, PlayerCheat.Revive, true);
            return true;
        }

        private static void SendPlayerRevival(int playerNumber)
        {
            var player = HeroController.players[playerNumber];
            if (player == null) return;
            if (Connect.IsOffline || player.IsMine)
                QueuePlayerRevival(playerNumber);
            else if (HeroController.PIDS[playerNumber] != null)
            {
                global::Networking.Networking.RPC<int>(HeroController.PIDS[playerNumber], true, false,
                    new RpcSignature<int>(ReceivePlayerRevivalRPC), playerNumber);
                RPCBatcher.FlushQueue();
            }
        }

        [AllowedRPC]
        private static void ReceivePlayerRevivalRPC(int playerNumber)
        {
            if (Connect.IsOffline || RPCController.CurrentRPC == null ||
                RPCController.CurrentRPC.messageInfo.Sender != PID.ServerID) return;
            QueuePlayerRevival(playerNumber);
        }

        private static void QueuePlayerRevival(int playerNumber)
        {
            if (!CanRevivePlayer(playerNumber) || !HeroController.players[playerNumber].IsMine) return;
            PlayerDeaths[playerNumber].Requested = true;
        }

        private static void UpdatePlayerRevival()
        {
            for (var index = 0; index < 4; index++)
            {
                var player = HeroController.players[index];
                var death = PlayerDeaths[index];
                if (death != null && (death.Player != player || death.Map != Map.Instance ||
                    !HeroController.IsPlaying(index) || (player != null && player.character != null &&
                    player.character.IsAlive() && !ReferenceEquals(player.character, death.Character))))
                    PlayerDeaths[index] = null;
                // Also supports enabling the mod after the player has already died.
                if (player != null && player.character != null && !player.character.IsAlive())
                    CapturePlayerDeath(player.character);
                if (player == null || !player.IsMine || !CanRevivePlayer(index)) continue;
                death = PlayerDeaths[index];
                if (!death.Requested && !IsPlayerAutoReviveEnabled(index)) continue;
                death.Spawning = true;
                death.Requested = false;
                if (player.Lives < 1) player.Lives = 1;
                // Use the game's networked spawn path; trigger swaps spawn at playerFollowPos.
                player.playerFollowPos = death.Position;
                player.changingBroFromTrigger = true;
                player.awaitingHeroTypeFromServer = true;
                player.SpawnHero(death.HeroType);
            }
        }

        private static void ResetPlayerRevival()
        {
            Array.Clear(PlayerDeaths, 0, PlayerDeaths.Length);
        }
    }

    [HarmonyPatch(typeof(TestVanDammeAnim), "Death")]
    internal static class PlayerDeathPositionPatch
    {
        private static void Prefix(TestVanDammeAnim __instance)
        {
            Plugin.CapturePlayerDeath(__instance);
        }
    }
}
