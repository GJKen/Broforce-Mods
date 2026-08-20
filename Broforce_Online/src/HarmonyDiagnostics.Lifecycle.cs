using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using System.Text;
using System.Threading;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace BroforceOnlineDiagnostics
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
                    CaptureWorkshopDropoutHeroType((int)arguments[0]);
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

            _networkSessionActive = false;
            DiagnosticLog.EndSession("SteamLayer.LeaveMatch");
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
                    SetFieldOrProperty(state, "levelNumber", 0);
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

        private static string GetConfiguredWorkshopSceneName()
        {
            var settings = Plugin.Settings;
            return settings == null ? string.Empty : (settings.WorkshopSceneName ?? string.Empty).Trim();
        }

        private static void ClearDuplicateWorkshopLoadSuppression()
        {
            _skipDuplicateWorkshopSceneLoad = false;
            _skipDuplicateWorkshopSceneLoadUntilUtc = DateTime.MinValue;
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

                var workshopId = (settings.WorkshopId ?? string.Empty).Trim();
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

                var sceneName = (settings.WorkshopSceneName ?? string.Empty).Trim();
                if (!string.IsNullOrEmpty(sceneName))
                {
                    SetFieldOrProperty(state, "sceneToLoad", sceneName);
                }

                var campaignName = (settings.WorkshopCampaignName ?? string.Empty).Trim();
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
            ResetStalePauseStateForWorkshopSession(trigger);
            _injectedForSession = false;
            _workshopCompletionHandledForSession = false;
            _joinLobbyInProgress = false;
            _workshopSpawnRebroadcastAtUtc = DateTime.MinValue;
            _workshopSpawnRebroadcastPending = false;
            _workshopSpawnRebroadcastUseCurrentPositions = false;
            ClearDuplicateWorkshopLoadSuppression();
            ClearWorkshopLocalJoinRequests();
            ClearLateJoinState();
            ClearLifecycleState();

            try
            {
                ClearCurrentCampaignForWorkshopLoad();

                var levelSelectionType = AccessTools.TypeByName("LevelSelectionController");
                SetStaticFieldOrProperty(levelSelectionType, "loadPublishedCampaign", false);
                SetStaticFieldOrProperty(levelSelectionType, "isOnlineCampaign", false);
                SetStaticFieldOrProperty(levelSelectionType, "shownHelicopterIntro", false);

                var gameStateType = AccessTools.TypeByName("GameState");
                var instanceProperty = gameStateType == null
                    ? null
                    : gameStateType.GetProperty(
                        "Instance",
                        BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
                var state = instanceProperty == null ? null : instanceProperty.GetValue(null, null);
                if (state != null)
                {
                    SetFieldOrProperty(state, "customLevelID", string.Empty);
                    SetFieldOrProperty(state, "loadCustomCampaign", false);
                    SetFieldOrProperty(state, "campaignName", string.Empty);
                    SetFieldOrProperty(state, "levelNumber", 0);
                }

                DiagnosticLog.Info("Workshop state reset before SteamLayer." + trigger + ".");
            }
            catch (Exception exception)
            {
                DiagnosticLog.Warning("Workshop state reset before SteamLayer." + trigger + " failed: " + exception);
            }
        }
    }
}
