using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using System.Text;
using System.Threading;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CustomMapMultiplayer
{
    // 会话生命周期：进出房间、场景加载、Workshop 会话状态的建立与重置。
    internal static partial class HarmonyDiagnostics
    {
                private static void JoinLobbyPostfix()
        {
            _joinLobbyInProgress = false;
            ResetStalePauseStateForWorkshopSession("SteamLayer.JoinLobby completed");
        }

        private static void ObserveLifecycleBeforeTrace(
            MethodBase method,
            object instance,
            object[] arguments)
        {
            if (method == null || method.DeclaringType == null)
            {
                return;
            }

            if (method.DeclaringType.Name == "HeroController" &&
                (method.Name == "Dropout" || method.Name == "DropoutRPC"))
            {
                if (method.Name == "DropoutRPC")
                {
                    ForgetWorkshopLocalJoinRequest(arguments);
                    RememberLocalWorkshopDropout(arguments);
                }
                else if (arguments != null && arguments.Length > 0 && arguments[0] is int)
                {
                    var playerNum = (int)arguments[0];
                    if (!IsManualAfkDropoutPending(playerNum))
                    {
                        CaptureWorkshopDropoutHeroType(playerNum);
                    }
                }

                return;
            }

            if (method.DeclaringType.Name == "HeroController" &&
                method.Name == "RegisterHeroToPlayer")
            {
                RememberWorkshopHeroType(arguments);
                return;
            }

            if (method.DeclaringType.Name == "Player" && method.Name == "SetHeroType")
            {
                RememberWorkshopHeroType(instance as Player, arguments);
                return;
            }

            if (method.DeclaringType.Name == "Player" && method.Name == "Start")
            {
                PrepareLocalWorkshopRejoin(instance as Player);
                return;
            }

            if (method.DeclaringType.Name == "Player" && method.Name == "SetSpawnPositon")
            {
                CaptureDeferredSpawnPosition(instance as Player, arguments);
            }
        }

        private static bool IsWorkshopOnlineSession()
        {
            var settings = Plugin.Settings;
            if (settings == null || !settings.EnableOnlineWorkshopInjection)
            {
                return false;
            }

            if (!IsOnline())
            {
                return false;
            }

            if (_injectedForSession)
            {
                return true;
            }

            var configuredScene = GetConfiguredWorkshopSceneName();
            if (!string.IsNullOrEmpty(configuredScene) &&
                string.Equals(
                    SceneManager.GetActiveScene().name,
                    configuredScene,
                    StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            var phase = GetWorkshopLobbyData(WorkshopLobbyPhaseKey);
            return string.Equals(phase, WorkshopLobbyPhaseLoading, StringComparison.Ordinal) ||
                string.Equals(phase, WorkshopLobbyPhaseReady, StringComparison.Ordinal);
        }

        private static bool IsWorkshopOnlineClientSession()
        {
            return IsWorkshopOnlineSession() && !IsOnlineHost();
        }

        private static bool IsWorkshopOnlineHostSession()
        {
            return IsWorkshopOnlineSession() && IsOnlineHost();
        }

        private static void ClearLifecycleState()
        {
            ClearWorkshopAcidAuthorityState();
            ClearAfkDiagnosticsState();
            ClearWorkshopPickupSynchronizationState();
            ClearEntityFinalStateSynchronizationState();
            ClearDemolitionBroBombDetonationState();
            ClearMcBroverTurkeyDetonationState();
            PendingSpawnPositions.Clear();
            LocalWorkshopSpawnPositions.Clear();
            SnappedRemoteWorkshopCharacters.Clear();
            PendingLocalWorkshopRejoins.Clear();
            PreparedLocalWorkshopRejoins.Clear();
            WorkshopKnownHeroTypes.Clear();
            WorkshopDropoutHeroTypes.Clear();
            WorkshopDropoutControllerIds.Clear();
        }

        private static bool HasRegisteredLocalPid(int playerNum)
        {
            if (playerNum < 0 || playerNum >= 4 || HeroController.PIDS == null)
            {
                return false;
            }

            var pid = HeroController.PIDS[playerNum];
            if (pid == null)
            {
                return false;
            }

            try
            {
                var layer = GetCurrentConnectionLayer();
                if (layer == null)
                {
                    return false;
                }

                var pairsProperty = layer.GetType().GetProperty(
                    "PlayerIDPairs",
                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static);
                if (pairsProperty == null)
                {
                    return false;
                }

                var pairs = pairsProperty.GetValue(layer, null);
                if (pairs == null)
                {
                    return false;
                }

                var containsKey = pairs.GetType().GetMethod("ContainsKey");
                return containsKey != null && Convert.ToBoolean(
                    containsKey.Invoke(pairs, new object[] { pid }));
            }
            catch
            {
                return false;
            }
        }

        private static void LobbyCreatedPostfix()
        {
            PublishConfiguredWorkshopIdentity("lobby created");
            SetWorkshopLobbyReady(false, "lobby created");
            SetWorkshopLobbyPhase(WorkshopLobbyPhaseIdle, "lobby created");
        }

        private static void PlayerHasJoinedMatchPostfix()
        {
            try
            {
                if (!_sessionIsHost)
                {
                    return;
                }

                PublishConfiguredWorkshopIdentity("new member joined");

                var phase = GetWorkshopLobbyData(WorkshopLobbyPhaseKey);
                if (!string.IsNullOrEmpty(phase))
                {
                    SetWorkshopLobbyPhase(phase, "new member joined");
                }

                var readiness = GetWorkshopLobbyData(WorkshopLobbyReadyKey);
                if (!string.IsNullOrEmpty(readiness))
                {
                    SetWorkshopLobbyReady(
                        string.Equals(readiness, "1", StringComparison.Ordinal),
                        "new member joined");
                }
            }
            catch (Exception exception)
            {
                DiagnosticLog.Warning("Workshop lobby state rebroadcast after member join failed: " + exception);
            }
        }

        private static void LeaveMatchPostfix()
        {
            SetWorkshopLobbyReady(false, "leaving lobby");
            SetWorkshopLobbyPhase(string.Empty, "leaving lobby");

            if (_joinLobbyInProgress || DateTime.UtcNow <= _joinLobbyCleanupIgnoreUntilUtc)
            {
                DiagnosticLog.Trace(
                    "SteamLayer.LeaveMatch occurred during JoinLobby cleanup; diagnostic session remains open.");
                return;
            }

            ClearInjectedWorkshopRuntimeState("SteamLayer.LeaveMatch", true);
            DiagnosticLog.EndSession("SteamLayer.LeaveMatch");
        }

        internal static void DisableOnlineWorkshopInjection(string trigger)
        {
            try
            {
                if (_networkSessionActive && _sessionIsHost)
                {
                    SetWorkshopLobbyData(WorkshopLobbyIdKey, string.Empty, trigger);
                    SetWorkshopLobbyData(WorkshopLobbySceneKey, string.Empty, trigger);
                    SetWorkshopLobbyData(WorkshopLobbyCampaignKey, string.Empty, trigger);
                    SetWorkshopLobbyReady(false, trigger);
                    SetWorkshopLobbyPhase(WorkshopLobbyPhaseIdle, trigger);
                }

                ClearInjectedWorkshopRuntimeState(trigger, false);
                DiagnosticLog.Info(
                    "Workshop injection disabled and injected runtime state cleared; trigger=" +
                    trigger + ". The active scene was not changed.");
            }
            catch (Exception exception)
            {
                DiagnosticLog.Warning(
                    "Workshop injection disable cleanup failed; trigger=" + trigger +
                    "; error=" + exception + ".");
            }
        }

        private static void ClearInjectedWorkshopRuntimeState(string trigger, bool endNetworkSession)
        {
            var hadInjectedState = HasInjectedWorkshopRuntimeState();

            _injectedForSession = false;
            _workshopCompletionHandledForSession = true;
            _joinLobbyInProgress = false;
            _joinLobbyCleanupIgnoreUntilUtc = DateTime.MinValue;
            _workshopSpawnRebroadcastAtUtc = DateTime.MinValue;
            _workshopSpawnRebroadcastPending = false;
            _workshopSpawnRebroadcastUseCurrentPositions = false;
            ClearDuplicateWorkshopLoadSuppression();
            ClearWorkshopLoadRequest();
            ClearWorkshopLevelNumberOverride();
            ClearWorkshopOnlineLobbyReturnState();
            ClearWorkshopLocalJoinRequests();
            ClearLateJoinState();
            ClearLifecycleState();
            ClearWorkshopIdentityState();

            if (endNetworkSession)
            {
                _networkSessionActive = false;
                _sessionIsHost = false;
            }

            if (!hadInjectedState)
            {
                DiagnosticLog.Trace(
                    "Workshop runtime tracking cleared without changing game state; trigger=" +
                    trigger + ".");
                return;
            }

            var gameModeControllerType = AccessTools.TypeByName("GameModeController");
            var gameModeControllerField = gameModeControllerType == null
                ? null
                : gameModeControllerType.GetField(
                    "instance",
                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
            var gameModeController = gameModeControllerField == null
                ? null
                : gameModeControllerField.GetValue(null);
            if (gameModeController != null)
            {
                SetFieldOrProperty(gameModeController, "switchingLevel", false);
                SetFieldOrProperty(gameModeController, "waitingForAllPlayersToReady", false);
                SetFieldOrProperty(gameModeController, "levelFinished", false);
                SetFieldOrProperty(gameModeController, "nextScene", LevelSelectionController.MainMenuScene);
            }

            var state = GetGameStateInstance(AccessTools.TypeByName("GameState"));
            if (state != null)
            {
                SetFieldOrProperty(state, "loadCustomCampaign", false);
                SetFieldOrProperty(state, "immediatelyGoToCustomCampaign", false);
                SetFieldOrProperty(state, "customLevelID", string.Empty);
                SetFieldOrProperty(state, "campaignName", string.Empty);
                SetFieldOrProperty(state, "levelNumber", 0);
                SetFieldOrProperty(state, "sceneToLoad", LevelSelectionController.MainMenuScene);
            }

            var levelSelectionType = AccessTools.TypeByName("LevelSelectionController");
            SetStaticFieldOrProperty(levelSelectionType, "loadPublishedCampaign", false);
            SetStaticFieldOrProperty(levelSelectionType, "loadCustomCampaign", false);
            SetStaticFieldOrProperty(levelSelectionType, "isOnlineCampaign", false);
            SetStaticFieldOrProperty(levelSelectionType, "shownHelicopterIntro", false);
            SetStaticFieldOrProperty(levelSelectionType, "currentWorkshopLevel", null);
            SetStaticFieldOrProperty(levelSelectionType, "campaignToLoad", null);
            ClearCurrentCampaignForWorkshopLoad();
            global::Networking.Networking.PauseStream = false;
            ResetStalePauseStateForWorkshopSession(trigger, true);

            DiagnosticLog.Info(
                "Cleared injected Workshop game state so subsequent level selection uses the native campaign; " +
                "trigger=" + trigger + ".");
        }

        private static bool HasInjectedWorkshopRuntimeState()
        {
            if (_injectedForSession || _sessionWorkshopIdentityAdopted)
            {
                return true;
            }

            var state = GetGameStateInstance(AccessTools.TypeByName("GameState"));
            if (state == null || !GetBoolFieldOrProperty(state, "loadCustomCampaign"))
            {
                return false;
            }

            var customLevelId = GetStringFieldOrProperty(state, "customLevelID").Trim();
            var settings = Plugin.Settings;
            var savedWorkshopId = settings == null
                ? string.Empty
                : (settings.WorkshopId ?? string.Empty).Trim();
            return !string.IsNullOrEmpty(customLevelId) &&
                string.Equals(customLevelId, savedWorkshopId, StringComparison.Ordinal);
        }

        internal static void PrepareFrpDirectRoomExit(string trigger)
        {
            try
            {
                _networkSessionActive = false;
                _injectedForSession = false;
                _workshopCompletionHandledForSession = true;
                _joinLobbyInProgress = false;
                _joinLobbyCleanupIgnoreUntilUtc = DateTime.MinValue;
                _workshopSpawnRebroadcastAtUtc = DateTime.MinValue;
                _workshopSpawnRebroadcastPending = false;
                _workshopSpawnRebroadcastUseCurrentPositions = false;
                ClearDuplicateWorkshopLoadSuppression();
                ClearWorkshopLevelNumberOverride();
                ClearWorkshopOnlineLobbyReturnState();
                ClearWorkshopLocalJoinRequests();
                ClearLateJoinState();
                ClearLifecycleState();

                var gameModeControllerType = AccessTools.TypeByName("GameModeController");
                var gameModeControllerField = gameModeControllerType == null
                    ? null
                    : gameModeControllerType.GetField(
                        "instance",
                        BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
                var gameModeController = gameModeControllerField == null
                    ? null
                    : gameModeControllerField.GetValue(null);
                if (gameModeController != null)
                {
                    SetFieldOrProperty(gameModeController, "switchingLevel", false);
                    SetFieldOrProperty(gameModeController, "waitingForAllPlayersToReady", false);
                    SetFieldOrProperty(gameModeController, "levelFinished", false);
                    SetFieldOrProperty(gameModeController, "nextScene", LevelSelectionController.MainMenuScene);
                }

                var state = GetGameStateInstance(AccessTools.TypeByName("GameState"));
                if (state != null)
                {
                    SetFieldOrProperty(state, "loadCustomCampaign", false);
                    SetFieldOrProperty(state, "customLevelID", string.Empty);
                    SetFieldOrProperty(state, "campaignName", string.Empty);
                    SetFieldOrProperty(state, "sceneToLoad", LevelSelectionController.MainMenuScene);
                }

                var levelSelectionType = AccessTools.TypeByName("LevelSelectionController");
                SetStaticFieldOrProperty(levelSelectionType, "loadPublishedCampaign", false);
                SetStaticFieldOrProperty(levelSelectionType, "isOnlineCampaign", false);
                ClearCurrentCampaignForWorkshopLoad();
                global::Networking.Networking.PauseStream = false;
                DiagnosticLog.Info(
                    "FRP_DIRECT cleared pending Workshop and level-switch state before room exit; trigger=" +
                    trigger + ".");
            }
            catch (Exception exception)
            {
                DiagnosticLog.Warning(
                    "FRP_DIRECT room-exit state cleanup failed; trigger=" + trigger +
                    "; error=" + exception.GetType().Name + ".");
            }
        }

        internal static void CompleteFrpDirectRemoteRoomExit(string trigger)
        {
            global::Networking.Networking.PauseStream = false;
            _sessionIsHost = false;
            DiagnosticLog.EndSession("FRP Direct " + trigger);
        }

        private static void ObserveOnlineHostRole()
        {
            if (!_networkSessionActive)
            {
                return;
            }

            try
            {
                if (!IsOnline())
                {
                    return;
                }

                var onlineHost = IsOnlineHost();
                if (onlineHost == _sessionIsHost)
                {
                    return;
                }

                var previousRole = _sessionIsHost ? "host" : "client";
                _sessionIsHost = onlineHost;
                DiagnosticLog.Info(
                    "Online session role changed from " + previousRole + " to " +
                    (onlineHost ? "host" : "client") + ".");

                if (onlineHost)
                {
                    HandleWorkshopHostPromotion();
                }
                else
                {
                    ClearLateJoinState();
                }
            }
            catch (Exception exception)
            {
                DiagnosticLog.Warning("Online host-role observation failed: " + exception);
            }
        }

        private static void HandleWorkshopHostPromotion()
        {
            ClearLateJoinState();
            _joinLobbyInProgress = false;
            _joinLobbyCleanupIgnoreUntilUtc = DateTime.MinValue;

            if (!IsWorkshopOnlineSession())
            {
                DiagnosticLog.Info(
                    "Host promotion did not require Workshop state publication for the current session.");
                return;
            }

            var activeScene = SceneManager.GetActiveScene().name ?? string.Empty;
            var configuredScene = GetConfiguredWorkshopSceneName();
            var workshopSceneLoaded = !string.IsNullOrEmpty(configuredScene) &&
                string.Equals(activeScene, configuredScene, StringComparison.OrdinalIgnoreCase);
            var levelNumber = 0;
            var campaignName = string.Empty;

            try
            {
                var settings = Plugin.Settings;
                var state = GetGameStateInstance(AccessTools.TypeByName("GameState"));
                if (state != null)
                {
                    levelNumber = GetIntFieldOrProperty(state, "levelNumber");
                    campaignName = GetStringFieldOrProperty(state, "campaignName").Trim();

                    if (settings != null)
                    {
                        var workshopId = GetConfiguredWorkshopId();
                        if (!string.IsNullOrEmpty(workshopId))
                        {
                            SetFieldOrProperty(state, "customLevelID", workshopId);
                        }

                        var configuredCampaign = GetConfiguredWorkshopCampaignName();
                        if (!string.IsNullOrEmpty(configuredCampaign))
                        {
                            campaignName = configuredCampaign;
                            SetFieldOrProperty(state, "campaignName", configuredCampaign);
                        }
                    }

                    SetFieldOrProperty(state, "loadCustomCampaign", true);
                    if (workshopSceneLoaded)
                    {
                        SetFieldOrProperty(state, "sceneToLoad", activeScene);
                    }
                }

                var room = GetCurrentRoom();
                if (room == null)
                {
                    DiagnosticLog.Warning(
                        "Workshop host promotion could not publish RoomInfo because the current room is null.");
                }
                else
                {
                    room.PushUpdatedInfo(true, false);
                    DiagnosticLog.Info(
                        "Workshop host promotion published full RoomInfo: scene=" +
                        room.CurrentSceneName + "; levelNumber=" + room.levelNumber +
                        "; campaign=" + (room.campaignName ?? string.Empty) + ".");
                }

                SetWorkshopLobbyPhase(
                    workshopSceneLoaded ? WorkshopLobbyPhaseReady : WorkshopLobbyPhaseLoading,
                    "host migration");
                PublishConfiguredWorkshopIdentity("host migration");
                SetWorkshopLobbyReady(workshopSceneLoaded, "host migration");
                DiagnosticLog.Info(
                    "Workshop host promotion synchronized authoritative state: activeScene=" +
                    activeScene + "; configuredScene=" + configuredScene +
                    "; levelNumber=" + levelNumber + "; campaign=" + campaignName +
                    "; ready=" + workshopSceneLoaded + ".");
            }
            catch (Exception exception)
            {
                DiagnosticLog.Warning(
                    "Workshop state publication after host promotion failed: " + exception);
            }
        }

        private static void JoinedLobbyPostfix(object[] __args)
        {
            try
            {
                var room = __args != null && __args.Length > 0
                    ? __args[0] as RoomInfo
                    : null;
                _joinLobbyCleanupIgnoreUntilUtc = DateTime.MinValue;
                ResetStalePauseStateForWorkshopSession("ConnectionLayer.OnJoinedLobby");
                TrySynchronizeClientWorkshopIdentity(true, "joined lobby");
                QueueLateJoin(room);
            }
            catch (Exception exception)
            {
                DiagnosticLog.Warning("Late workshop join detection failed: " + exception);
            }
        }

        internal static void NotifySceneLoaded(Scene scene)
        {
            try
            {
                ClearWorkshopAcidPoolCache();
                ClearEntityFinalStateSynchronizationState();
                ClearDemolitionBroBombDetonationState();
                ClearMcBroverTurkeyDetonationState();
                var configuredScene = GetConfiguredWorkshopSceneName();
                if (string.Equals(scene.name, WorkshopVictorySceneName, StringComparison.OrdinalIgnoreCase) &&
                    _injectedForSession && IsOnline() && IsEscReturnPauseState())
                {
                    ArmWorkshopOnlineLobbyReturn("VictoryCustomCampaignSteam scene loaded");
                }

                if (string.Equals(scene.name, "MainMenu", StringComparison.OrdinalIgnoreCase) &&
                    _returnToWorkshopOnlineLobbyPending)
                {
                    _returnToWorkshopOnlineLobbyAtUtc = DateTime.UtcNow.AddMilliseconds(
                        WorkshopOnlineLobbyReturnDelayMilliseconds);
                    DiagnosticLog.Info(
                        "Workshop Esc return reached MainMenu; intermediate menu visuals will stay hidden " +
                        "until native initialization completes and the online lobby opens.");
                    SuppressWorkshopOnlineLobbyMainMenuVisuals(
                        GetMainMenuInstance(AccessTools.TypeByName("MainMenu")));
                }

                var isConfiguredWorkshopScene = string.Equals(
                    scene.name,
                    configuredScene,
                    StringComparison.OrdinalIgnoreCase);
                if (isConfiguredWorkshopScene)
                {
                    ResetStalePauseStateForWorkshopSession("Workshop scene loaded");
                    NormalizeLocalWorkshopPlayerControlState("Workshop scene loaded");
                }

                if (!_sessionIsHost && _lateJoinStarted &&
                    string.Equals(scene.name, _lateJoinScene, StringComparison.OrdinalIgnoreCase))
                {
                    _lateJoinClientSceneLoaded = true;
                    DiagnosticLog.Info(
                        "Late workshop client scene loaded; automatic local join is waiting for " +
                        "SpawnJoinedPlayers: scene=" + scene.name + ".");
                }

                if (!_sessionIsHost)
                {
                    return;
                }

                if (isConfiguredWorkshopScene)
                {
                    SetWorkshopLobbyPhase(WorkshopLobbyPhaseReady, "workshop scene loaded");
                    SetWorkshopLobbyReady(true, "workshop scene loaded");
                }
                else if (string.Equals(scene.name, "LoadingScreen", StringComparison.OrdinalIgnoreCase))
                {
                    SetWorkshopLobbyReady(false, "loading screen entered");
                    if (_injectedForSession)
                    {
                        SetWorkshopLobbyPhase(WorkshopLobbyPhaseLoading, "loading screen entered");
                    }
                }
            }
            catch (Exception exception)
            {
                DiagnosticLog.Warning("Workshop lobby readiness update failed: " + exception);
            }
        }

        private static void SubscribeWorkshopCompletion()
        {
            if (_workshopCompletionSubscribed)
            {
                return;
            }

            try
            {
                SteamController.LevelLoadCompleteEvent += WorkshopLevelLoadComplete;
                _workshopCompletionSubscribed = true;
                DiagnosticLog.Info("Workshop level-load completion callback subscribed.");
            }
            catch (Exception exception)
            {
                DiagnosticLog.Warning("Workshop level-load completion callback subscription failed: " + exception);
            }
        }

        private static void UnsubscribeWorkshopCompletion()
        {
            if (!_workshopCompletionSubscribed)
            {
                return;
            }

            try
            {
                SteamController.LevelLoadCompleteEvent -= WorkshopLevelLoadComplete;
            }
            catch (Exception exception)
            {
                DiagnosticLog.Warning("Workshop level-load completion callback removal failed: " + exception);
            }
            finally
            {
                _workshopCompletionSubscribed = false;
            }
        }

        private static void WorkshopLevelLoadComplete(Campaign campaign)
        {
            try
            {
                var settings = Plugin.Settings;
                if (settings == null || !settings.EnableOnlineWorkshopInjection ||
                    !_injectedForSession || _workshopCompletionHandledForSession)
                {
                    return;
                }

                if (campaign == null)
                {
                    DiagnosticLog.Warning("Workshop level-load completion returned a null campaign.");
                    return;
                }

                _workshopCompletionHandledForSession = true;
                SetCurrentCampaign(campaign);
                ObserveWorkshopGameModeConsistency(campaign);
                SetStaticFieldOrProperty(
                    AccessTools.TypeByName("LevelSelectionController"),
                    "loadPublishedCampaign",
                    true);
                SetStaticFieldOrProperty(
                    AccessTools.TypeByName("LevelSelectionController"),
                    "isOnlineCampaign",
                    true);

                var gameStateType = AccessTools.TypeByName("GameState");
                var instanceProperty = gameStateType == null
                    ? null
                    : gameStateType.GetProperty(
                        "Instance",
                        BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
                var state = instanceProperty == null ? null : instanceProperty.GetValue(null, null);
                if (state != null)
                {
                    SetFieldOrProperty(state, "loadCustomCampaign", true);

                    // Re-entry can receive the host's authoritative levelNumber
                    // before the Workshop campaign download completes.  Keep
                    // that value through the completion callback; resetting to
                    // zero selects MapData[0] when every campaign level shares
                    // the same Unity scene name (for example Test Evan2).
                    var completionLevelNumber = _workshopLevelNumberOverridePending
                        ? _workshopLevelNumberOverride
                        : (_lateJoinStarted ? _lateJoinLevelNumber : 0);
                    SetFieldOrProperty(state, "levelNumber", completionLevelNumber);
                    DiagnosticLog.Info(
                        "Workshop level-load completion preserved levelNumber=" +
                        completionLevelNumber + "; authoritativeOverride=" +
                        _workshopLevelNumberOverridePending + "; lateJoin=" + _lateJoinStarted + ".");
                }

                DiagnosticLog.Info("Workshop level-load completed; resuming GameState.LoadLevel.");
                var loadLevel = gameStateType == null
                    ? null
                    : gameStateType.GetMethod(
                        "LoadLevel",
                        BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance,
                        null,
                        new[] { typeof(string) },
                        null);
                if (loadLevel == null)
                {
                    DiagnosticLog.Warning("Workshop level-load completion could not find GameState.LoadLevel(string).");
                    return;
                }

                _skipDuplicateWorkshopSceneLoad = true;
                _skipDuplicateWorkshopSceneLoadUntilUtc =
                    DateTime.UtcNow.AddSeconds(DuplicateWorkshopLoadSuppressionSeconds);
                loadLevel.Invoke(null, new object[] { string.Empty });
                ClearWorkshopLevelNumberOverride();
            }
            catch (Exception exception)
            {
                DiagnosticLog.Warning("Workshop level-load completion handling failed: " + exception);
            }
        }

        private static void SetCurrentCampaign(Campaign campaign)
        {
            var type = AccessTools.TypeByName("LevelSelectionController");
            if (type == null)
            {
                throw new MissingMemberException("LevelSelectionController");
            }

            var property = type.GetProperty(
                "currentCampaign",
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
            if (property != null && property.CanWrite)
            {
                property.SetValue(null, campaign, null);
                return;
            }

            var field = type.GetField(
                "CurrentCampaign",
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
            if (field != null)
            {
                field.SetValue(null, campaign);
                return;
            }

            throw new MissingMemberException(type.FullName, "currentCampaign");
        }

        private static void ObserveWorkshopGameModeConsistency(Campaign campaign)
        {
            try
            {
                var header = GetFieldOrPropertyValue(campaign, "header");
                var campaignMode = GetFieldOrPropertyValue(header, "gameMode");
                var state = GetGameStateInstance(AccessTools.TypeByName("GameState"));
                var stateMode = GetFieldOrPropertyValue(state, "gameMode");
                var room = GetCurrentRoom();
                var roomMode = GetFieldOrPropertyValue(room, "gameMode");
                var observations = new[]
                {
                    new GameModeObservation("campaign", campaignMode),
                    new GameModeObservation("gameState", stateMode),
                    new GameModeObservation("room", roomMode)
                };
                var comparableCount = 0;
                string expected = null;
                var matches = true;
                foreach (var observation in observations)
                {
                    if (!observation.Available)
                    {
                        continue;
                    }

                    comparableCount++;
                    if (expected == null)
                    {
                        expected = observation.ComparisonValue;
                    }
                    else if (!string.Equals(
                        expected,
                        observation.ComparisonValue,
                        StringComparison.Ordinal))
                    {
                        matches = false;
                    }
                }

                var message =
                    "WORKSHOP_GAME_MODE_COMPARE campaign=" + observations[0].DisplayValue +
                    "; gameState=" + observations[1].DisplayValue +
                    "; room=" + observations[2].DisplayValue +
                    "; comparableSources=" + comparableCount +
                    "; match=" + (comparableCount >= 2 ? matches.ToString() : "unknown") +
                    "; action=observe-only.";
                if (comparableCount >= 2 && !matches)
                {
                    DiagnosticLog.Warning(message);
                }
                else
                {
                    DiagnosticLog.Info(message);
                }
            }
            catch (Exception exception)
            {
                DiagnosticLog.Warning(
                    "Workshop game-mode comparison failed; no game state was changed: " + exception);
            }
        }

        private sealed class GameModeObservation
        {
            public GameModeObservation(string source, object value)
            {
                Source = source;
                Available = value != null;
                if (value == null)
                {
                    ComparisonValue = string.Empty;
                    DisplayValue = "unavailable";
                    return;
                }

                ComparisonValue = Convert.ToString(
                    value,
                    System.Globalization.CultureInfo.InvariantCulture);
                DisplayValue = Sanitize(ComparisonValue, 80);
            }

            public string Source { get; private set; }
            public bool Available { get; private set; }
            public string ComparisonValue { get; private set; }
            public string DisplayValue { get; private set; }
        }

        private static void ClearDuplicateWorkshopLoadSuppression()
        {
            _skipDuplicateWorkshopSceneLoad = false;
            _skipDuplicateWorkshopSceneLoadUntilUtc = DateTime.MinValue;
        }

        private static void ClearWorkshopLevelNumberOverride()
        {
            _workshopLevelNumberOverridePending = false;
            _workshopLevelNumberOverride = 0;
            _workshopLevelNumberOverrideCustomLevelId = string.Empty;
            _workshopLevelNumberOverrideScene = string.Empty;
        }

        private static void CaptureAuthoritativeWorkshopLevelNumber(
            MethodBase method,
            object[] arguments)
        {
            if (method == null || method.DeclaringType == null ||
                method.DeclaringType.Name != "GameModeController" ||
                (method.Name != "LoadNextScene" && method.Name != "LoadSceneCore") ||
                !IsOnline() || !HasValidWorkshopInjectionConfiguration() ||
                arguments == null)
            {
                return;
            }

            object state = null;
            foreach (var argument in arguments)
            {
                if (argument != null && argument.GetType().Name == "GameState")
                {
                    state = argument;
                    break;
                }
            }

            if (state == null || !GetBoolFieldOrProperty(state, "loadCustomCampaign"))
            {
                return;
            }

            var settings = Plugin.Settings;
            var configuredWorkshopId = GetConfiguredWorkshopId();
            var customLevelId = GetStringFieldOrProperty(state, "customLevelID").Trim();
            if (!string.Equals(customLevelId, configuredWorkshopId, StringComparison.Ordinal))
            {
                return;
            }

            var configuredScene = GetConfiguredWorkshopSceneName();
            var scene = GetStringFieldOrProperty(state, "_sceneToLoad").Trim();
            if (string.IsNullOrEmpty(scene))
            {
                scene = GetStringFieldOrProperty(state, "sceneToLoad").Trim();
            }
            if (!string.IsNullOrEmpty(configuredScene) &&
                !string.Equals(scene, configuredScene, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            var levelNumber = GetIntFieldOrProperty(state, "levelNumber");
            if (levelNumber < 0)
            {
                return;
            }

            _workshopLevelNumberOverridePending = true;
            _workshopLevelNumberOverride = levelNumber;
            _workshopLevelNumberOverrideCustomLevelId = customLevelId;
            _workshopLevelNumberOverrideScene = scene;
            DiagnosticLog.Info(
                "Captured authoritative Workshop levelNumber=" + levelNumber +
                " from GameModeController." + method.Name +
                "; customLevelID=" + customLevelId + "; scene=" + scene + ".");
        }

        private static void RestoreAuthoritativeWorkshopLevelNumberBeforeCompletion(MethodBase method)
        {
            if (method == null || method.DeclaringType == null ||
                method.DeclaringType.Name != "SteamController" ||
                method.Name != "OnLevelLoadComplete" ||
                !_workshopLevelNumberOverridePending)
            {
                return;
            }

            try
            {
                var state = GetGameStateInstance(AccessTools.TypeByName("GameState"));
                if (state == null)
                {
                    DiagnosticLog.Warning(
                        "Could not restore authoritative Workshop levelNumber: GameState.Instance is null.");
                    return;
                }

                SetFieldOrProperty(state, "levelNumber", _workshopLevelNumberOverride);
                DiagnosticLog.Info(
                    "Restored Workshop levelNumber=" + _workshopLevelNumberOverride +
                    " before SteamController.OnLevelLoadComplete; customLevelID=" +
                    _workshopLevelNumberOverrideCustomLevelId + "; scene=" +
                    _workshopLevelNumberOverrideScene + ".");
            }
            catch (Exception exception)
            {
                DiagnosticLog.Warning(
                    "Restoring authoritative Workshop levelNumber failed: " + exception);
            }
        }

        private static void ApplyWorkshopState()
        {
            ApplyWorkshopState(true, "before AdminRPC<GameState>");
        }

        private static void ApplyWorkshopState(bool requireHost, string injectionContext)
        {
            try
            {
                var settings = Plugin.Settings;
                if (settings == null || !settings.EnableOnlineWorkshopInjection || _injectedForSession)
                {
                    return;
                }

                var workshopId = GetConfiguredWorkshopId();
                ulong numericId;
                if (!UInt64.TryParse(workshopId, out numericId) || numericId == 0)
                {
                    DiagnosticLog.Warning("Workshop injection skipped: WorkshopId is not a positive numeric ID.");
                    return;
                }

                if (!IsOnline())
                {
                    return;
                }

                if (requireHost && !IsOnlineHost())
                {
                    return;
                }

                var gameStateType = AccessTools.TypeByName("GameState");
                var instanceProperty = gameStateType == null
                    ? null
                    : gameStateType.GetProperty(
                        "Instance",
                        BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
                var state = instanceProperty == null ? null : instanceProperty.GetValue(null, null);

                if (state == null)
                {
                    DiagnosticLog.Warning("Workshop injection skipped: GameState.Instance is null.");
                    return;
                }

                SetFieldOrProperty(state, "customLevelID", workshopId);
                SetFieldOrProperty(state, "loadCustomCampaign", true);
                SetFieldOrProperty(state, "levelNumber", 0);

                var sceneName = GetConfiguredWorkshopSceneName();
                if (!string.IsNullOrEmpty(sceneName))
                {
                    SetFieldOrProperty(state, "sceneToLoad", sceneName);
                }

                var campaignName = GetConfiguredWorkshopCampaignName();
                if (!string.IsNullOrEmpty(campaignName))
                {
                    SetFieldOrProperty(state, "campaignName", campaignName);
                }

                if (!requireHost)
                {
                    ClearCurrentCampaignForWorkshopLoad();
                }

                _injectedForSession = true;
                if (_sessionIsHost)
                {
                    PublishConfiguredWorkshopIdentity(injectionContext);
                    SetWorkshopLobbyPhase(WorkshopLobbyPhaseLoading, injectionContext);
                }
                DiagnosticLog.Info(
                    "Online workshop state injected " + injectionContext + ": id=" + workshopId +
                    ", scene=" + sceneName + ", campaign=" + campaignName + ".");
                _workshopCompletionHandledForSession = false;
            }
            catch (Exception exception)
            {
                DiagnosticLog.Warning("Workshop state injection failed: " + exception);
            }
        }

        private static void ClearCurrentCampaignForWorkshopLoad()
        {
            var levelSelectionType = AccessTools.TypeByName("LevelSelectionController");
            if (levelSelectionType == null)
            {
                DiagnosticLog.Warning("Workshop load-level injection could not find LevelSelectionController.");
                return;
            }

            var field = levelSelectionType.GetField(
                "CurrentCampaign",
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
            if (field != null)
            {
                field.SetValue(null, null);
                DiagnosticLog.Info("Workshop load-level injection cleared the official current campaign.");
                return;
            }

            var property = levelSelectionType.GetProperty(
                "currentCampaign",
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
            if (property != null && property.CanWrite)
            {
                property.SetValue(null, null, null);
                DiagnosticLog.Info("Workshop load-level injection cleared the official current campaign.");
                return;
            }

            DiagnosticLog.Warning("Workshop load-level injection could not clear the official current campaign.");
        }

        private static void ResetWorkshopStateForNewSession(string trigger)
        {
            var shouldClearInjectedGameState =
                HasInjectedWorkshopRuntimeState() || HasValidWorkshopInjectionConfiguration();
            ClearWorkshopIdentityState();
            ClearDemolitionBroBombDetonationState();
            ClearMcBroverTurkeyDetonationState();
            if (shouldClearInjectedGameState)
            {
                ResetStalePauseStateForWorkshopSession(trigger, true);
            }
            _injectedForSession = false;
            _workshopCompletionHandledForSession = false;
            _joinLobbyInProgress = false;
            _workshopSpawnRebroadcastAtUtc = DateTime.MinValue;
            _workshopSpawnRebroadcastPending = false;
            _workshopSpawnRebroadcastUseCurrentPositions = false;
            ClearDuplicateWorkshopLoadSuppression();
            ClearWorkshopLevelNumberOverride();
            ClearWorkshopLocalJoinRequests();
            ClearLateJoinState();
            ClearLifecycleState();

            try
            {
                if (shouldClearInjectedGameState)
                {
                    ClearCurrentCampaignForWorkshopLoad();

                    var levelSelectionType = AccessTools.TypeByName("LevelSelectionController");
                    SetStaticFieldOrProperty(levelSelectionType, "loadPublishedCampaign", false);
                    SetStaticFieldOrProperty(levelSelectionType, "loadCustomCampaign", false);
                    SetStaticFieldOrProperty(levelSelectionType, "isOnlineCampaign", false);
                    SetStaticFieldOrProperty(levelSelectionType, "shownHelicopterIntro", false);
                    SetStaticFieldOrProperty(levelSelectionType, "currentWorkshopLevel", null);
                    SetStaticFieldOrProperty(levelSelectionType, "campaignToLoad", null);

                    var state = GetGameStateInstance(AccessTools.TypeByName("GameState"));
                    if (state != null)
                    {
                        SetFieldOrProperty(state, "customLevelID", string.Empty);
                        SetFieldOrProperty(state, "loadCustomCampaign", false);
                        SetFieldOrProperty(state, "immediatelyGoToCustomCampaign", false);
                        SetFieldOrProperty(state, "campaignName", string.Empty);
                        SetFieldOrProperty(state, "levelNumber", 0);
                    }
                }

                DiagnosticLog.Info(
                    "Workshop tracking reset before SteamLayer." + trigger +
                    "; clearedInjectedGameState=" + shouldClearInjectedGameState + ".");
            }
            catch (Exception exception)
            {
                DiagnosticLog.Warning("Workshop state reset before SteamLayer." + trigger + " failed: " + exception);
            }
        }
    }
}
