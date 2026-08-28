using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using System.Text;
using System.Threading;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace BroforceCustomMapMultiplayer
{
    // Workshop 晚加入：加入请求排队、槽位准备、加入完成与超时处理。
    internal static partial class HarmonyDiagnostics
    {
                private static void ResetLateJoinPlayerRequestForRetry(string reason)
        {
            if (!_lateJoinPlayerJoinRequested)
            {
                return;
            }

            ClearWorkshopLocalJoinRequests();
            _lateJoinPlayerJoinRequested = false;
            _lateJoinPlayerRequestAtUtc = DateTime.MinValue;
            _lateJoinAutoJoinAtUtc = DateTime.UtcNow.AddMilliseconds(
                LateJoinAutoJoinDelayMilliseconds);
            DiagnosticLog.Warning(
                "Late workshop local player request did not produce an active local slot; " +
                "retrying the automatic join: reason=" + reason + ".");
        }

        private static void TryCompleteLateWorkshopJoin()
        {
            if (_lateJoinAutoJoinCompleted)
            {
                return;
            }

            if (DateTime.UtcNow > _lateJoinDeadlineUtc)
            {
                DiagnosticLog.Warning(
                    "Late workshop join timed out before the automatic local player join completed: scene=" +
                    _lateJoinScene + ".");
                _lateJoinAutoJoinCompleted = true;
                return;
            }

            if (!IsOnline() || IsOnlineHost())
            {
                return;
            }

            if (HasActiveWorkshopLocalPlayer())
            {
                ClearWorkshopLocalJoinRequests();
                _lateJoinPlayerJoinRequested = false;
                _lateJoinPlayerRequestAtUtc = DateTime.MinValue;
                DiagnosticLog.Info(
                    "Late workshop automatic join completed with one active local player slot.");
                _lateJoinAutoJoinCompleted = true;
                return;
            }

            if (_lateJoinPlayerJoinRequested &&
                DateTime.UtcNow - _lateJoinPlayerRequestAtUtc >=
                TimeSpan.FromSeconds(LateJoinPlayerRequestTimeoutSeconds))
            {
                ResetLateJoinPlayerRequestForRetry("request timeout");
            }

            if (_lateJoinPlayerJoinRequested || !_lateJoinClientSceneLoaded ||
                !_lateJoinSpawnJoinedPlayersSeen || DateTime.UtcNow < _lateJoinAutoJoinAtUtc ||
                string.Equals(GetLocalPidState(), "not-set", StringComparison.Ordinal))
            {
                return;
            }

            TryRequestLateJoinPlayer();
        }

        private static bool TryRequestLateJoinPlayer()
        {
            if (!IsOnline() || IsOnlineHost())
            {
                return false;
            }

            if (string.Equals(GetLocalPidState(), "not-set", StringComparison.Ordinal))
            {
                return false;
            }

            if (HasActiveWorkshopLocalPlayer())
            {
                _lateJoinPlayerJoinRequested = true;
                _lateJoinPlayerRequestAtUtc = DateTime.UtcNow;
                DiagnosticLog.Info(
                    "Late workshop join reused an existing local player slot; " +
                    "skipping AddLocalPlayer to avoid a duplicate local character.");
                return true;
            }

            RemoveExpiredWorkshopLocalJoinRequests();
            if (WorkshopLocalJoinRequests.Count > 0)
            {
                _lateJoinPlayerJoinRequested = true;
                _lateJoinPlayerRequestAtUtc = DateTime.UtcNow;
                DiagnosticLog.Info(
                    "Late workshop automatic join reused an already pending local-player request.");
                return true;
            }

            var nextPlayerNumber = GetImmediateNextUnusedPlayerNumber();
            if (nextPlayerNumber < 0)
            {
                if (!_lateJoinNoFreeSlotWarningLogged)
                {
                    _lateJoinNoFreeSlotWarningLogged = true;
                    DiagnosticLog.Warning(
                        "Late workshop automatic join is waiting for a free local player slot: " +
                        FormatWorkshopPlayerSlots() + ".");
                }

                _lateJoinAutoJoinAtUtc = DateTime.UtcNow.AddMilliseconds(
                    WorkshopLobbyReadyPollMilliseconds);
                return false;
            }

            _lateJoinNoFreeSlotWarningLogged = false;
            var controllerId = GetLateJoinControllerId(nextPlayerNumber);

            var heroControllerType = AccessTools.TypeByName("HeroController");
            var addLocalPlayer = heroControllerType == null
                ? null
                : heroControllerType.GetMethod(
                    "AddLocalPlayer",
                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static,
                    null,
                    new[] { typeof(int), typeof(int) },
                    null);
            if (addLocalPlayer == null)
            {
                DiagnosticLog.Warning("Late workshop join could not find HeroController.AddLocalPlayer(int, int).");
                return false;
            }

            try
            {
                addLocalPlayer.Invoke(null, new object[] { -1, controllerId });
                _lateJoinPlayerJoinRequested = true;
                _lateJoinPlayerRequestAtUtc = DateTime.UtcNow;
                DiagnosticLog.Info(
                    "Late workshop join requested a local player slot after scene readiness: player=" +
                    nextPlayerNumber + "; controller=" + controllerId + ".");
                return true;
            }
            catch (Exception exception)
            {
                DiagnosticLog.Warning("Late workshop local player request failed: " + exception);
                return false;
            }
        }

        private static void QueueLateJoin(RoomInfo room)
        {
            var settings = Plugin.Settings;
            if (room == null || settings == null || !settings.EnableOnlineWorkshopInjection || IsOnlineHost())
            {
                return;
            }

            TrySynchronizeClientWorkshopIdentity(true, "late workshop join detection");
            if (_workshopSubscriptionMissing)
            {
                return;
            }

            RefreshWorkshopLobbyDataIfNeeded("late workshop join detected");
            var configuredScene = GetConfiguredWorkshopSceneName();
            var hostScene = GetRoomInfoString(room, "CurrentSceneName");
            var lobbyPhase = GetWorkshopLobbyData(WorkshopLobbyPhaseKey);
            var workshopTransitionIsActive = string.Equals(
                lobbyPhase,
                WorkshopLobbyPhaseLoading,
                StringComparison.Ordinal) ||
                string.Equals(lobbyPhase, WorkshopLobbyPhaseReady, StringComparison.Ordinal);
            var hostAlreadyLoadedWorkshopScene = !string.IsNullOrEmpty(configuredScene) &&
                string.Equals(hostScene, configuredScene, StringComparison.OrdinalIgnoreCase);
            if (string.IsNullOrEmpty(configuredScene) ||
                (!hostAlreadyLoadedWorkshopScene && !workshopTransitionIsActive))
            {
                return;
            }

            ulong workshopId;
            var configuredWorkshopId = GetConfiguredWorkshopId();
            if (!UInt64.TryParse(configuredWorkshopId, out workshopId) || workshopId == 0)
            {
                DiagnosticLog.Warning(
                    "Late workshop join skipped: WorkshopId is not a positive numeric ID.");
                return;
            }

            _lateJoinScene = hostAlreadyLoadedWorkshopScene ? hostScene : configuredScene;
            _lateJoinCampaign = GetRoomInfoString(room, "campaignName");
            _lateJoinLevelNumber = GetRoomInfoInt(room, "levelNumber", 0);
            _lateJoinDeadlineUtc = DateTime.UtcNow.AddSeconds(LateJoinTimeoutSeconds);
            _lateJoinReadyPollAtUtc = DateTime.MinValue;
            _lateJoinPending = true;
            _lateJoinStarted = false;
            _lateJoinPlayerJoinRequested = false;
            _lateJoinClientSceneLoaded = false;
            _lateJoinSpawnJoinedPlayersSeen = false;
            _lateJoinAutoJoinCompleted = false;
            _lateJoinNoFreeSlotWarningLogged = false;
            _lateJoinAutoJoinAtUtc = DateTime.MinValue;
            DiagnosticLog.Info(
                "Late workshop join detected: hostScene=" + hostScene +
                ", campaign=" + (_lateJoinCampaign ?? string.Empty) +
                ", levelNumber=" + _lateJoinLevelNumber +
                ", lobbyPhase=" + lobbyPhase + ".");
        }

        private static void TryQueueLateJoinFromWorkshopPhase()
        {
            try
            {
                if (!IsOnline() || IsOnlineHost())
                {
                    return;
                }

                RefreshWorkshopLobbyDataIfNeeded("late workshop phase check");
                var phase = GetWorkshopLobbyData(WorkshopLobbyPhaseKey);
                if (!string.Equals(phase, WorkshopLobbyPhaseLoading, StringComparison.Ordinal) &&
                    !string.Equals(phase, WorkshopLobbyPhaseReady, StringComparison.Ordinal))
                {
                    return;
                }

                QueueLateJoin(GetCurrentRoom());
            }
            catch (Exception exception)
            {
                DiagnosticLog.Warning("Delayed late workshop join detection failed: " + exception);
            }
        }

        private static void TryStartLateJoin()
        {
            if (!IsOnline() || IsOnlineHost())
            {
                return;
            }

            var gameStateType = AccessTools.TypeByName("GameState");
            var state = GetGameStateInstance(gameStateType);
            var loadLevel = gameStateType == null
                ? null
                : gameStateType.GetMethod(
                    "LoadLevel",
                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance,
                    null,
                    new[] { typeof(string) },
                    null);
            if (state == null || loadLevel == null)
            {
                return;
            }

            if (!_injectedForSession)
            {
                ApplyWorkshopState(false, "for late client join");
            }

            if (!_injectedForSession)
            {
                return;
            }

            try
            {
                if (!string.IsNullOrEmpty(_lateJoinCampaign))
                {
                    SetFieldOrProperty(state, "campaignName", _lateJoinCampaign);
                }

                SetFieldOrProperty(state, "levelNumber", _lateJoinLevelNumber);
                _lateJoinPending = false;
                _lateJoinStarted = true;
                _lateJoinClientSceneLoaded = false;
                _lateJoinSpawnJoinedPlayersSeen = false;
                _lateJoinAutoJoinCompleted = false;
                _lateJoinNoFreeSlotWarningLogged = false;
                _lateJoinAutoJoinAtUtc = DateTime.MinValue;
                DiagnosticLog.Info(
                    "Starting late workshop join load: scene=" + _lateJoinScene +
                    ", campaign=" + (_lateJoinCampaign ?? string.Empty) +
                    ", levelNumber=" + _lateJoinLevelNumber +
                    "; automatic local join will wait for scene readiness.");
                loadLevel.Invoke(loadLevel.IsStatic ? null : state, new object[] { _lateJoinScene });
            }
            catch (Exception exception)
            {
                _lateJoinStarted = false;
                _injectedForSession = false;
                DiagnosticLog.Warning("Late workshop join load failed: " + exception);
            }
        }

        private static string GetLocalPidState()
        {
            try
            {
                var pidType = AccessTools.TypeByName("PID");
                var myIdHasBeenSet = pidType == null
                    ? null
                    : pidType.GetProperty(
                        "MyIdHasBeenSet",
                        BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
                if (myIdHasBeenSet == null)
                {
                    return "unknown";
                }

                return Convert.ToBoolean(myIdHasBeenSet.GetValue(null, null)) ? "set" : "not-set";
            }
            catch
            {
                return "error";
            }
        }

        private static void LogLateJoinWaitState(
            string phase,
            bool ready,
            string pidState,
            bool online,
            bool onlineHost)
        {
            var state =
                "phase=" + (phase ?? string.Empty) +
                "; readiness=" + (ready ? "ready" : "not-ready") +
                "; pid=" + (pidState ?? string.Empty) +
                "; online=" + online +
                "; onlineHost=" + onlineHost +
                "; scene=" + (_lateJoinScene ?? string.Empty);
            if (string.Equals(_lateJoinLastWaitState, state, StringComparison.Ordinal))
            {
                return;
            }

            _lateJoinLastWaitState = state;
            DiagnosticLog.Info("Late workshop join wait state: " + state + ".");
        }

        private static bool ShouldAllowRequestJoinGameController(int controllerNum, object requesteeID)
        {
            if (IsLateWorkshopHostSession())
            {
                var requesteePid = requesteeID as PID;
                var existingPlayerNumber = FindPlayerNumberForPid(requesteePid);
                if (existingPlayerNumber >= 0)
                {
                    DiagnosticLog.Warning(
                        "Suppressed duplicate late Workshop RequestJoinGame for an existing player slot: " +
                        "controller=" + controllerNum +
                        "; player=" + existingPlayerNumber + ".");
                    return true;
                }

                DiagnosticLog.Trace(
                    "Late workshop host bypassed the controller-registration guard for the first player-slot request: " +
                    "controller=" + controllerNum + ".");
                return false;
            }

            try
            {
                var controllerRegistrationGuard = FindRequestJoinGameControllerGuard();
                if (controllerRegistrationGuard == null)
                {
                    return true;
                }

                return Convert.ToBoolean(controllerRegistrationGuard.Invoke(
                    controllerRegistrationGuard.IsStatic ? null : GetCurrentConnectionLayer(),
                    new[] { (object)controllerNum, requesteeID }));
            }
            catch (Exception exception)
            {
                DiagnosticLog.Warning(
                    "RequestJoinGame controller-registration guard fallback failed: " + exception);
                return true;
            }
        }

        private static bool ShouldAllowRequestJoinGame()
        {
            var levelFinishedGetter = AccessTools.PropertyGetter(
                AccessTools.TypeByName("GameModeController"),
                "LevelFinished");
            if (levelFinishedGetter == null)
            {
                return true;
            }

            var levelFinished = Convert.ToBoolean(levelFinishedGetter.Invoke(null, null));
            if (IsLateWorkshopHostSession())
            {
                DiagnosticLog.Trace(
                    "Late workshop host allowed RequestJoinGame while the level-finished guard is active.");
                return false;
            }

            return levelFinished;
        }

        private static bool IsLateWorkshopHostSession()
        {
            if (!_injectedForSession || !IsOnline() || !IsOnlineHost())
            {
                return false;
            }

            var phase = GetWorkshopLobbyData(WorkshopLobbyPhaseKey);
            return string.Equals(phase, WorkshopLobbyPhaseLoading, StringComparison.Ordinal) ||
                string.Equals(phase, WorkshopLobbyPhaseReady, StringComparison.Ordinal);
        }

        private static void PrepareLateWorkshopJoinSlot(object[] arguments)
        {
            if (!IsLateWorkshopHostSession())
            {
                return;
            }

            var requesteePid = arguments != null && arguments.Length > 1
                ? arguments[1] as PID
                : null;
            var existingPlayerNumber = FindPlayerNumberForPid(requesteePid);
            if (existingPlayerNumber >= 0)
            {
                DiagnosticLog.Info(
                    "Late workshop RequestJoinGame reused an existing PID slot; " +
                    "skipping stale-slot cleanup: player=" + existingPlayerNumber + ".");
                return;
            }

            var nextPlayerNumber = GetImmediateNextUnusedPlayerNumber();
            DiagnosticLog.Info(
                "Late workshop RequestJoinGame slot state before native handling: next=" +
                nextPlayerNumber + "; " + FormatWorkshopPlayerSlots() + ".");
            if (nextPlayerNumber != -1)
            {
                return;
            }

            var staleSlot = FindEmptyLateWorkshopPlayerSlot();
            if (staleSlot < 0)
            {
                DiagnosticLog.Warning(
                    "Late workshop RequestJoinGame has no free slot and no empty stale Player object: " +
                    FormatWorkshopPlayerSlots() + ".");
                return;
            }

            var playersPlaying = GetPlayersPlayingArray();
            if (playersPlaying == null || staleSlot >= playersPlaying.Length)
            {
                DiagnosticLog.Warning(
                    "Late workshop player slot became unavailable before stale-slot cleanup.");
                return;
            }

            playersPlaying[staleSlot] = false;
            DiagnosticLog.Warning(
                "Cleared stale late workshop player slot before RequestJoinGame: player=" +
                staleSlot + "; " + FormatWorkshopPlayerSlots() + ".");
        }

        private static void RequestJoinGamePostfix(int controllerNum, PID requesteeID, string playerName)
        {
            if (!IsLateWorkshopHostSession())
            {
                return;
            }

            var assignedPlayerNumber = FindPlayerNumberForPid(requesteeID);
            DiagnosticLog.Info(
                "Late workshop RequestJoinGame state after native handling: controller=" +
                controllerNum + "; assignedPlayer=" + assignedPlayerNumber + "; " +
                FormatWorkshopPlayerSlots() + ".");
            if (assignedPlayerNumber < 0)
            {
                DiagnosticLog.Warning(
                    "Late workshop RequestJoinGame returned without registering the requestee PID.");
                return;
            }

            ReplayBufferedWorkshopInstancesToJoiner(requesteeID, assignedPlayerNumber);

            QueueWorkshopSpawnRebroadcast(
                "a late Workshop player registered: player=" + assignedPlayerNumber,
                750,
                true);
        }

        private static void ReplayBufferedWorkshopInstancesToJoiner(
            PID requesteeID,
            int assignedPlayerNumber)
        {
            if (requesteeID == null)
            {
                return;
            }

            try
            {
                InstantiationController.SendInstantiatedPrefabs(requesteeID);
                DiagnosticLog.Info(
                    "Late workshop replayed buffered network instances to the joining client: " +
                    "target=" + requesteeID + "; assignedPlayer=" + assignedPlayerNumber + ".");
            }
            catch (Exception exception)
            {
                DiagnosticLog.Warning(
                    "Late workshop buffered network-instance replay failed: " + exception);
            }
        }

        private static int GetImmediateNextUnusedPlayerNumber()
        {
            try
            {
                var method = typeof(HeroController).GetMethod(
                    "GetNextUnusedPlayerNumber",
                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
                return method == null ? -2 : Convert.ToInt32(method.Invoke(null, null));
            }
            catch (Exception exception)
            {
                DiagnosticLog.Warning(
                    "Reading the immediate Workshop player slot state failed: " + exception);
                return -2;
            }
        }

        private static int FindEmptyLateWorkshopPlayerSlot()
        {
            var playersPlaying = GetPlayersPlayingArray();
            if (playersPlaying == null || HeroController.players == null)
            {
                return -1;
            }

            var count = System.Math.Min(4, System.Math.Min(
                playersPlaying.Length,
                HeroController.players.Length));
            for (var index = 1; index < count; index++)
            {
                if (playersPlaying[index] && HeroController.players[index] == null)
                {
                    return index;
                }
            }

            return -1;
        }

        private static int FindPlayerNumberForPid(PID pid)
        {
            if (pid == null || HeroController.PIDS == null)
            {
                return -1;
            }

            var count = System.Math.Min(4, HeroController.PIDS.Length);
            for (var index = 0; index < count; index++)
            {
                var candidate = HeroController.PIDS[index];
                if (candidate != null && candidate.AsByte == pid.AsByte)
                {
                    return index;
                }
            }

            return -1;
        }

        private static string FormatWorkshopPlayerSlots()
        {
            var playersPlaying = GetPlayersPlayingArray();
            if (playersPlaying == null || HeroController.players == null ||
                HeroController.playerControllerIDs == null)
            {
                return "slots=unavailable";
            }

            var count = System.Math.Min(4, System.Math.Min(
                playersPlaying.Length,
                System.Math.Min(HeroController.players.Length, HeroController.playerControllerIDs.Length)));
            var builder = new StringBuilder("slots=");
            for (var index = 0; index < count; index++)
            {
                if (index > 0)
                {
                    builder.Append(",");
                }

                builder.Append(index);
                builder.Append("{playing=");
                builder.Append(playersPlaying[index]);
                builder.Append(";player=");
                builder.Append(HeroController.players[index] == null ? "null" : "present");
                builder.Append(";controller=");
                builder.Append(HeroController.playerControllerIDs[index]);
                builder.Append("}");
            }

            return builder.ToString();
        }

        private static void ClearLateJoinState()
        {
            _lateJoinPending = false;
            _lateJoinStarted = false;
            _lateJoinPlayerJoinRequested = false;
            _lateJoinClientSceneLoaded = false;
            _lateJoinSpawnJoinedPlayersSeen = false;
            _lateJoinAutoJoinCompleted = false;
            _lateJoinNoFreeSlotWarningLogged = false;
            _lateJoinDeadlineUtc = DateTime.MinValue;
            _lateJoinReadyPollAtUtc = DateTime.MinValue;
            _lateJoinTransitionPollAtUtc = DateTime.MinValue;
            _lateJoinLobbyRefreshAtUtc = DateTime.MinValue;
            _lateJoinAutoJoinAtUtc = DateTime.MinValue;
            _lateJoinPlayerRequestAtUtc = DateTime.MinValue;
            _lateJoinScene = string.Empty;
            _lateJoinCampaign = string.Empty;
            _lateJoinLevelNumber = 0;
            _lateJoinLastWaitState = string.Empty;
            _lateJoinLobbyRefreshWarningLogged = false;
        }

        private static int GetLateJoinControllerId(int playerNum)
        {
            int savedControllerId;
            if (WorkshopDropoutControllerIds.TryGetValue(playerNum, out savedControllerId) &&
                savedControllerId >= 0)
            {
                DiagnosticLog.Info(
                    "Reusing saved local Workshop controller for dropout rejoin: player=" +
                    playerNum + "; controller=" + savedControllerId + ".");
                return savedControllerId;
            }

            foreach (var pendingPlayerNum in PendingLocalWorkshopRejoins)
            {
                if (WorkshopDropoutControllerIds.TryGetValue(
                        pendingPlayerNum,
                        out savedControllerId) && savedControllerId >= 0)
                {
                    DiagnosticLog.Info(
                        "Reusing saved local Workshop controller for pending dropout rejoin: " +
                        "requestedPlayer=" + playerNum + "; savedPlayer=" + pendingPlayerNum +
                        "; controller=" + savedControllerId + ".");
                    return savedControllerId;
                }
            }

            if (_lastLocalWorkshopControllerId >= 0)
            {
                DiagnosticLog.Info(
                    "Reusing the last known local Workshop controller for late join: player=" +
                    playerNum + "; controller=" + _lastLocalWorkshopControllerId + ".");
                return _lastLocalWorkshopControllerId;
            }

            try
            {
                var activeInputController = InputReader.ActiveInputID;
                if (activeInputController >= 0 &&
                    activeInputController < InputReader.TOTAL_NUM_OF_CONTROL_IDS)
                {
                    DiagnosticLog.Info(
                        "Using the active local input controller for late Workshop join: player=" +
                        playerNum + "; controller=" + activeInputController + ".");
                    return activeInputController;
                }
            }
            catch (Exception exception)
            {
                DiagnosticLog.Warning(
                    "Late workshop automatic join could not read the active input controller: " +
                    exception.Message);
            }

            try
            {
                var platform = SingletonMono<Utility.Platforms.Platform>.Instance;
                if (platform != null)
                {
                    var controllerId = platform.GetPrimaryUserController();
                    if (controllerId >= 0)
                    {
                        return controllerId;
                    }
                }
            }
            catch (Exception exception)
            {
                DiagnosticLog.Warning(
                    "Late workshop automatic join could not read the primary controller: " +
                    exception.Message);
            }

            DiagnosticLog.Warning(
                "Late workshop automatic join is using the default local controller: controller=" +
                DefaultLateJoinControllerId + ".");
            return DefaultLateJoinControllerId;
        }
    }
}
