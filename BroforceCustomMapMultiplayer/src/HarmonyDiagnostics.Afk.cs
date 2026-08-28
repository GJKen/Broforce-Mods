using System;
using System.Globalization;
using System.Text;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace BroforceCustomMapMultiplayer
{
    // Low-frequency observation around Broforce's native 35-second online AFK path.
    internal static partial class HarmonyDiagnostics
    {
        private const float NativeAfkTimeoutSeconds = 35f;
        private const float AfkCountingLogSeconds = 5f;
        private const float AfkWarningLogSeconds = 30f;
        private const double NativeAfkDropoutCallbackWindowSeconds = 2d;

        private static readonly AfkPlayerLogState[] AfkPlayerLogStates =
            new AfkPlayerLogState[4];
        private static readonly DateTime[] NativeAfkDropoutPendingUntilUtc =
            new DateTime[4];
        private static readonly bool[] NativeAfkDropoutObserved = new bool[4];
        private static readonly bool[] AfkPreventionLogged = new bool[4];
        private static System.Reflection.FieldInfo _playerIdleTimerField;

        private static AfkUpdateObservation BeginAfkUpdateObservation(Player player)
        {
            var observation = new AfkUpdateObservation();
            if (player == null || !_networkSessionActive || Connect.IsOffline || !player.IsMine)
            {
                return observation;
            }

            var playerNum = player.playerNum;
            if (playerNum < 0 || playerNum >= AfkPlayerLogStates.Length)
            {
                return observation;
            }

            observation.Active = true;
            observation.Player = player;
            observation.PlayerNum = playerNum;
            observation.BeforeTimer = ReadPlayerIdleTimer(player);
            observation.Delta = Time.unscaledDeltaTime;
            observation.PreventionEnabled = Plugin.Settings != null &&
                Plugin.Settings.DisableOnlineAfkSpectatorMode;
            observation.MayReachNativeTimeout =
                !observation.PreventionEnabled &&
                observation.BeforeTimer > 0f &&
                observation.BeforeTimer + observation.Delta >= NativeAfkTimeoutSeconds;

            if (observation.MayReachNativeTimeout)
            {
                NativeAfkDropoutPendingUntilUtc[playerNum] = DateTime.UtcNow.AddSeconds(
                    NativeAfkDropoutCallbackWindowSeconds);
                NativeAfkDropoutObserved[playerNum] = false;
                observation.BeforeSnapshot = BuildAfkSnapshot(playerNum, player);
            }

            return observation;
        }

        private static void CompleteAfkUpdateObservation(
            Player player,
            AfkUpdateObservation observation)
        {
            if (!observation.Active)
            {
                return;
            }

            var playerNum = observation.PlayerNum;
            if (playerNum < 0 || playerNum >= AfkPlayerLogStates.Length)
            {
                return;
            }

            var state = AfkPlayerLogStates[playerNum];
            if (!ReferenceEquals(state.Player, player))
            {
                state.Player = player;
                state.LogStage = 0;
            }

            if (observation.PreventionEnabled && !AfkPreventionLogged[playerNum])
            {
                AfkPreventionLogged[playerNum] = true;
                LogAfkEvent(
                    "AFK_STATE event=prevention-active; player=" + playerNum +
                    "; scope=local-player; nativeTimeoutSeconds=35; state={" +
                    BuildAfkSnapshot(playerNum, player) + "}");
            }

            var afterTimer = ReadPlayerIdleTimer(player);
            var dropoutObserved = NativeAfkDropoutObserved[playerNum];
            var currentPlayer = GetPlayerForAfkSlot(playerNum);
            var slotPlaying = IsAfkSlotPlaying(playerNum);
            var removed = currentPlayer == null || !slotPlaying;

            if (observation.MayReachNativeTimeout &&
                (dropoutObserved || removed) && state.LogStage < 3)
            {
                state.LogStage = 3;
                LogAfkEvent(
                    "AFK_STATE event=timeout-triggered; source=native-Player.Update; player=" +
                    playerNum +
                    "; idleBefore=" + FormatAfkSeconds(observation.BeforeTimer) +
                    "; frameDelta=" + FormatAfkSeconds(observation.Delta) +
                    "; dropoutObserved=" + dropoutObserved +
                    "; removed=" + removed +
                    "; before={" + observation.BeforeSnapshot +
                    "}; after={" + BuildAfkSnapshot(playerNum, currentPlayer) + "}");
            }
            else if (afterTimer >= AfkWarningLogSeconds && state.LogStage < 2)
            {
                state.LogStage = 2;
                LogAfkEvent(
                    "AFK_TIMER event=warning; player=" + playerNum +
                    "; idleSeconds=" + FormatAfkSeconds(afterTimer) +
                    "; timeoutSeconds=35; state={" +
                    BuildAfkSnapshot(playerNum, player) + "}");
            }
            else if (afterTimer >= AfkCountingLogSeconds && state.LogStage < 1)
            {
                state.LogStage = 1;
                LogAfkEvent(
                    "AFK_TIMER event=counting; player=" + playerNum +
                    "; idleSeconds=" + FormatAfkSeconds(afterTimer) +
                    "; timeoutSeconds=35; state={" +
                    BuildAfkSnapshot(playerNum, player) + "}");
            }
            else if (afterTimer < 0.1f && state.LogStage > 0 && state.LogStage < 3)
            {
                LogAfkEvent(
                    "AFK_TIMER event=reset; player=" + playerNum +
                    "; idleBefore=" + FormatAfkSeconds(observation.BeforeTimer) +
                    "; preventionEnabled=" + observation.PreventionEnabled +
                    "; state={" + BuildAfkSnapshot(playerNum, player) + "}");
                state.LogStage = 0;
            }

            if (afterTimer < 0.1f)
            {
                NativeAfkDropoutPendingUntilUtc[playerNum] = DateTime.MinValue;
            }

            NativeAfkDropoutObserved[playerNum] = false;
            AfkPlayerLogStates[playerNum] = state;
        }

        private static AfkDropoutObservation BeginAfkDropoutObservation(int playerNum)
        {
            var observation = new AfkDropoutObservation();
            if (!_networkSessionActive || !IsOnline() || playerNum < 0 || playerNum >= 4)
            {
                return observation;
            }

            var player = GetPlayerForAfkSlot(playerNum);
            if (_joinLobbyInProgress ||
                DateTime.UtcNow <= _joinLobbyCleanupIgnoreUntilUtc ||
                (player == null && !IsAfkSlotPlaying(playerNum)))
            {
                return observation;
            }

            observation.Active = true;
            observation.PlayerNum = playerNum;
            observation.NativeAfkTimeout = IsNativeAfkDropoutPending(playerNum);
            observation.Before = BuildAfkSnapshot(playerNum, player);
            if (observation.NativeAfkTimeout)
            {
                NativeAfkDropoutObserved[playerNum] = true;
            }

            return observation;
        }

        private static void CompleteAfkDropoutObservation(AfkDropoutObservation observation)
        {
            if (!observation.Active)
            {
                return;
            }

            var playerNum = observation.PlayerNum;
            var reason = observation.NativeAfkTimeout ? "native-afk-timeout" : "unknown";
            var after = BuildAfkSnapshot(playerNum, GetPlayerForAfkSlot(playerNum));
            LogAfkEvent(
                "PLAYER_DROPOUT event=applied; player=" + playerNum +
                "; reason=" + reason +
                "; before={" + observation.Before +
                "}; after={" + after + "}");

            if (observation.NativeAfkTimeout)
            {
                var state = AfkPlayerLogStates[playerNum];
                if (state.LogStage < 3)
                {
                    state.LogStage = 3;
                    AfkPlayerLogStates[playerNum] = state;
                    LogAfkEvent(
                        "AFK_STATE event=timeout-triggered; source=native-Player.Update" +
                        "; player=" + playerNum +
                        "; dropoutObserved=true; removed=" +
                        (!IsAfkSlotPlaying(playerNum) ||
                         GetPlayerForAfkSlot(playerNum) == null) +
                        "; before={" + observation.Before +
                        "}; after={" + after + "}");
                }
            }

            NativeAfkDropoutPendingUntilUtc[playerNum] = DateTime.MinValue;
        }

        private static bool IsNativeAfkDropoutPending(int playerNum)
        {
            var pendingUntilUtc = NativeAfkDropoutPendingUntilUtc[playerNum];
            if (pendingUntilUtc == DateTime.MinValue || DateTime.UtcNow > pendingUntilUtc)
            {
                NativeAfkDropoutPendingUntilUtc[playerNum] = DateTime.MinValue;
                return false;
            }

            return true;
        }

        private static string BuildAfkSnapshot(int playerNum, Player player)
        {
            var builder = new StringBuilder();
            builder.Append("scene=");
            builder.Append(Sanitize(SceneManager.GetActiveScene().name ?? string.Empty, 80));
            builder.Append(";slotPlaying=");
            builder.Append(IsAfkSlotPlaying(playerNum));
            builder.Append(";playerPresent=");
            builder.Append(player != null);
            builder.Append(";isMine=");
            builder.Append(ReadAfkPlayerIsMine(player));
            builder.Append(";lives=");
            builder.Append(player == null ? "n/a" : GetIntFieldOrProperty(player, "lives").ToString());
            builder.Append(";characterPresent=");
            builder.Append(player != null && player.character != null);
            builder.Append(";characterAlive=");
            builder.Append(ReadAfkCharacterAlive(player));
            builder.Append(";idleSeconds=");
            builder.Append(player == null ? "n/a" : FormatAfkSeconds(ReadPlayerIdleTimer(player)));
            builder.Append(";alive=");
            builder.Append(ReadHeroCount(delegate { return HeroController.GetPlayersAliveCount(); }));
            builder.Append(";local=");
            builder.Append(ReadHeroCount(delegate { return HeroController.GetLocalPlayerCount(); }));
            builder.Append(";totalLives=");
            builder.Append(ReadHeroCount(delegate { return HeroController.GetTotalLives(); }));
            return builder.ToString();
        }

        private static Player GetPlayerForAfkSlot(int playerNum)
        {
            try
            {
                return playerNum >= 0 && playerNum < HeroController.players.Length
                    ? HeroController.players[playerNum]
                    : null;
            }
            catch
            {
                return null;
            }
        }

        private static bool IsAfkSlotPlaying(int playerNum)
        {
            var playersPlaying = GetPlayersPlayingArray();
            return playersPlaying != null && playerNum >= 0 && playerNum < playersPlaying.Length &&
                playersPlaying[playerNum];
        }

        private static string ReadAfkPlayerIsMine(Player player)
        {
            if (player == null)
            {
                return "n/a";
            }

            try
            {
                return player.IsMine.ToString();
            }
            catch
            {
                return "error";
            }
        }

        private static string ReadAfkCharacterAlive(Player player)
        {
            if (player == null || player.character == null)
            {
                return "n/a";
            }

            try
            {
                return player.character.IsAlive().ToString();
            }
            catch
            {
                return "error";
            }
        }

        private static float ReadPlayerIdleTimer(Player player)
        {
            if (player == null || _playerIdleTimerField == null)
            {
                return 0f;
            }

            try
            {
                return Convert.ToSingle(
                    _playerIdleTimerField.GetValue(player),
                    CultureInfo.InvariantCulture);
            }
            catch
            {
                return 0f;
            }
        }

        private static string FormatAfkSeconds(float value)
        {
            return value.ToString("0.000", CultureInfo.InvariantCulture);
        }

        private static void LogAfkEvent(string message)
        {
            DiagnosticLog.Info(message);
            DiagnosticLog.Trace(message);
        }

        private static void ClearAfkDiagnosticsState()
        {
            for (var index = 0; index < AfkPlayerLogStates.Length; index++)
            {
                AfkPlayerLogStates[index] = new AfkPlayerLogState();
                NativeAfkDropoutPendingUntilUtc[index] = DateTime.MinValue;
                NativeAfkDropoutObserved[index] = false;
                AfkPreventionLogged[index] = false;
            }
        }

        private struct AfkUpdateObservation
        {
            public bool Active;
            public Player Player;
            public int PlayerNum;
            public float BeforeTimer;
            public float Delta;
            public bool PreventionEnabled;
            public bool MayReachNativeTimeout;
            public string BeforeSnapshot;
        }

        private struct AfkDropoutObservation
        {
            public bool Active;
            public int PlayerNum;
            public bool NativeAfkTimeout;
            public string Before;
        }

        private struct AfkPlayerLogState
        {
            public Player Player;
            public int LogStage;
        }
    }
}
