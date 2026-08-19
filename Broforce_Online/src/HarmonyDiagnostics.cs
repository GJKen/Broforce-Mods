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
    internal static class HarmonyDiagnostics
    {
        private const string HarmonyId = "GJKen.BroforceOnlineDiagnostics.MethodTrace";
        private const int DuplicateWindowSeconds = 5;
        private const int DuplicateWorkshopLoadSuppressionSeconds = 5;
        private const int WorkshopLocalJoinRequestRetrySeconds = 10;
        private const int LateJoinPlayerRequestTimeoutSeconds = 5;
        private const int DefaultLateJoinControllerId = 0;
        private const int LateJoinAutoJoinDelayMilliseconds = 250;
        private const int LateJoinTimeoutSeconds = 120;
        private const int WorkshopLobbyReadyPollMilliseconds = 500;
        private const int WorkshopLobbyDataRefreshMilliseconds = 1000;
        private const string PressToJoinLocalizationKey = "LOC_HUD_PRESSTOJOIN";
        private const string WorkshopLobbyReadyKey = "GJKen_BroforceOnline_WorkshopReady";
        private const string WorkshopLobbyPhaseKey = "GJKen_BroforceOnline_WorkshopPhase";
        private const string WorkshopLobbyPhaseIdle = "idle";
        private const string WorkshopLobbyPhaseLoading = "loading";
        private const string WorkshopLobbyPhaseReady = "ready";
        private const string WorkshopVictorySceneName = "VictoryCustomCampaignSteam";
        private const int WorkshopOnlineLobbyReturnDelayMilliseconds = 250;
        private const int WorkshopOnlineLobbyNavigationFailureGraceMilliseconds = 1000;
        private const int WorkshopOnlineLobbyNavigationTimeoutSeconds = 30;
        private const int TraceCacheExpirySeconds = 60;
        private const int MaxTraceCacheEntries = 1024;

        private static readonly TraceTarget[] Targets =
        {
            new TraceTarget("MainMenu", "TryToGoToLobby"),
            new TraceTarget("MakeOnlineMenu", "DoHostGame"),
            new TraceTarget("Lobby", "TryJoin"),
            new TraceTarget("Lobby", "JoinTheGame"),
            new TraceTarget("SteamLayer", "CreateMatch"),
            new TraceTarget("SteamLayer", "JoinLobby"),
            new TraceTarget("SteamLayer", "LeaveMatch"),
            new TraceTarget("SteamLayer", "LobbyCreated_Callback"),
            new TraceTarget("SteamLayer", "LobbyJoined_Callback"),
            new TraceTarget("ConnectionLayer", "OnJoinedLobby"),
            new TraceTarget("ConnectionLayer", "PlayerHasJoinedMatch"),
            new TraceTarget("ConnectionLayer", "RegisterNewPlayer"),
            new TraceTarget("ConnectionLayer", "RegisterPlayerID"),
            new TraceTarget("ConnectionLayer", "RegisterDelayedLocalPIDSync"),
            new TraceTarget("ConnectionLayer", "BroadcastPlayerID"),
            new TraceTarget("ConnectionLayer", "UpdateOnlinePlayerList"),
            new TraceTarget("ConnectionLayer", "RemovePlayer"),
            new TraceTarget("SteamController", "LoadLevel"),
            new TraceTarget("SteamController", "Cloud_CloudGetPublishedFileDetailsResult"),
            new TraceTarget("SteamController", "Cloud_CloudDownloadUGCResult"),
            new TraceTarget("SteamController", "OnLevelLoadComplete"),
            new TraceTarget("RoomInfo", "RefreshInfo"),
            new TraceTarget("RoomInfo", "PushUpdatedInfo"),
            new TraceTarget("RoomInfo", "PullUpdatedInfo"),
            new TraceTarget("GameModeController", "SwitchLevel"),
            new TraceTarget("GameModeController", "LoadNextSceneFade"),
            new TraceTarget("GameModeController", "LoadNextScene"),
            new TraceTarget("GameModeController", "LoadSceneCore"),
            new TraceTarget("LevelSelectionController", "GotoNextCampaignScene"),
            new TraceTarget("LevelSelectionController", "GotoNextLevel"),
            new TraceTarget("LevelSelectionController", "GetMapDataForCampaign"),
            new TraceTarget("LevelSelectionController", "GetMapDataFromFile"),
            new TraceTarget("WorldMapController", "EnterMission"),
            new TraceTarget("GameState", "LoadLevel"),
            new TraceTarget("HeroController", "RequestJoinGame"),
            new TraceTarget("HeroController", "IsControIdRegisteredToPID"),
            new TraceTarget("HeroController", "MonitorPlayerDropin"),
            new TraceTarget("HeroController", "DeserializeForJoin"),
            new TraceTarget("HeroController", "SerializeForJoin"),
            new TraceTarget("HeroController", "AddPlayer"),
            new TraceTarget("HeroController", "AddLocalPlayer"),
            new TraceTarget("HeroController", "RegisterHeroToPlayer"),
            new TraceTarget("HeroController", "SpawnJoinedPlayers"),
            new TraceTarget("HeroController", "SetPlayerCharacter"),
            new TraceTarget("HeroController", "SetPlayerName"),
            new TraceTarget("HeroController", "UpdatePlayerData"),
            new TraceTarget("HeroController", "UpdatePlayerUserData"),
            new TraceTarget("HeroController", "HaveAllPlayersJoined"),
            new TraceTarget("HeroController", "HaveAllPlayersHaveSpawned"),
            new TraceTarget("HeroController", "FlagPlayerToDrop"),
            new TraceTarget("HeroController", "DeregisterPlayer"),
            new TraceTarget("HeroController", "Dropout"),
            new TraceTarget("HeroController", "DropoutRPC"),
            new TraceTarget("HeroController", "SetIsPlaying"),
            new TraceTarget("HeroController", "RequestAllPlayerData"),
            new TraceTarget("HeroController", "RequestHeroTypeFromMaster"),
            new TraceTarget("HeroController", "RequestHeroTypeFromMasterRPC"),
            new TraceTarget("HeroController", "RecieveHeroTypeFromMaster"),
            new TraceTarget("Player", "Awake"),
            new TraceTarget("Player", "Start"),
            new TraceTarget("Player", "RespawnBro"),
            new TraceTarget("Player", "InstantiateHero"),
            new TraceTarget("Player", "SpawnHero"),
            new TraceTarget("Player", "SetHeroType"),
            new TraceTarget("Player", "AssignCharacter"),
            new TraceTarget("Player", "SetSpawnPositon"),
            new TraceTarget("Player", "WorkOutSpawnPosition"),
            new TraceTarget("NewCustomCampaignMenu", "LaunchWorkShopLevel"),
            new TraceTarget("NewCustomCampaignMenu", "LevelLoadCompleteEvent"),
            new TraceTarget("NewCustomCampaignMenu", "LaunchOfflineCampaign"),
            new TraceTarget("WorkshopCustomCampaignBrowser", "LaunchLevel"),
            new TraceTarget("OnlineCustomCampaignBrowser", "LaunchLevel"),
            new TraceTarget("CustomCampaignMenu", "StartCampaign"),
            new TraceTarget("CustomCampaignMenu", "ContinueOnlineCampaign")
        };

        private static readonly object Sync = new object();
        private static readonly Dictionary<string, TraceCacheEntry> TraceCache =
            new Dictionary<string, TraceCacheEntry>();
        private static readonly Dictionary<int, DeferredSpawnPosition> PendingSpawnPositions =
            new Dictionary<int, DeferredSpawnPosition>();
        private static readonly Dictionary<int, DeferredSpawnPosition> LocalWorkshopSpawnPositions =
            new Dictionary<int, DeferredSpawnPosition>();
        private static readonly Dictionary<int, TestVanDammeAnim> SnappedRemoteWorkshopCharacters =
            new Dictionary<int, TestVanDammeAnim>();
        private static readonly HashSet<int> PendingLocalWorkshopRejoins =
            new HashSet<int>();
        private static readonly HashSet<int> PreparedLocalWorkshopRejoins =
            new HashSet<int>();
        private static readonly Dictionary<int, DateTime> WorkshopLocalJoinRequests =
            new Dictionary<int, DateTime>();
        private static readonly Dictionary<string, DateTime> WorkshopLocalJoinSuppressionWarnings =
            new Dictionary<string, DateTime>();
        private static readonly Dictionary<int, HeroType> WorkshopKnownHeroTypes =
            new Dictionary<int, HeroType>();
        private static readonly Dictionary<int, HeroType> WorkshopDropoutHeroTypes =
            new Dictionary<int, HeroType>();
        private static readonly Dictionary<int, int> WorkshopDropoutControllerIds =
            new Dictionary<int, int>();

        private static Harmony _harmony;
        private static int _sequence;
        private static bool _injectedForSession;
        private static bool _workshopCompletionHandledForSession;
        private static bool _skipDuplicateWorkshopSceneLoad;
        private static DateTime _skipDuplicateWorkshopSceneLoadUntilUtc;
        private static bool _lateJoinPending;
        private static bool _lateJoinStarted;
        private static bool _lateJoinPlayerJoinRequested;
        private static bool _lateJoinClientSceneLoaded;
        private static bool _lateJoinSpawnJoinedPlayersSeen;
        private static bool _lateJoinAutoJoinCompleted;
        private static bool _lateJoinNoFreeSlotWarningLogged;
        private static bool _joinLobbyInProgress;
        private static DateTime _lateJoinDeadlineUtc;
        private static DateTime _lateJoinReadyPollAtUtc;
        private static DateTime _lateJoinTransitionPollAtUtc;
        private static DateTime _lateJoinLobbyRefreshAtUtc;
        private static DateTime _lateJoinAutoJoinAtUtc;
        private static DateTime _lateJoinPlayerRequestAtUtc;
        private static DateTime _workshopSpawnRebroadcastAtUtc;
        private static bool _workshopSpawnRebroadcastPending;
        private static bool _workshopSpawnRebroadcastUseCurrentPositions;
        private static int _lastLocalWorkshopControllerId = -1;
        private static string _lateJoinScene;
        private static string _lateJoinCampaign;
        private static int _lateJoinLevelNumber;
        private static string _lateJoinLastWaitState;
        private static bool _lateJoinLobbyRefreshWarningLogged;
        private static bool _sessionIsHost;
        private static bool _networkSessionActive;
        private static DateTime _joinLobbyCleanupIgnoreUntilUtc;
        private static bool _returnToWorkshopOnlineLobbyPending;
        private static bool _returnToWorkshopOnlineLobbyAttempted;
        private static DateTime _returnToWorkshopOnlineLobbyAtUtc;
        private static bool _returnToWorkshopOnlineLobbyVisualsSuppressed;
        private static DateTime _returnToWorkshopOnlineLobbyNavigationStartedAtUtc;
        private static bool _restoreMainMenuAfterLobbyReturnPending;
        private static readonly Dictionary<Renderer, bool> MainMenuRendererStates =
            new Dictionary<Renderer, bool>();

        public static void Start()
        {
            if (_harmony != null)
            {
                return;
            }

            _harmony = new Harmony(HarmonyId);
            _injectedForSession = false;
            _workshopCompletionHandledForSession = false;
            _joinLobbyInProgress = false;
            _sessionIsHost = false;
            _networkSessionActive = false;
            _joinLobbyCleanupIgnoreUntilUtc = DateTime.MinValue;
            ClearWorkshopOnlineLobbyReturnState();
            _restoreMainMenuAfterLobbyReturnPending = false;
            _workshopSpawnRebroadcastAtUtc = DateTime.MinValue;
            _workshopSpawnRebroadcastPending = false;
            _workshopSpawnRebroadcastUseCurrentPositions = false;
            _lastLocalWorkshopControllerId = -1;
            ClearDuplicateWorkshopLoadSuppression();
            ClearWorkshopLocalJoinRequests();
            ClearLateJoinState();
            ClearLifecycleState();
            SubscribeWorkshopCompletion();
            var prefixMethod = typeof(HarmonyDiagnostics).GetMethod(
                "TracePrefix",
                BindingFlags.NonPublic | BindingFlags.Static);
            var prefix = new HarmonyMethod(prefixMethod);
            var joinLobbyPostfixMethod = typeof(HarmonyDiagnostics).GetMethod(
                "JoinLobbyPostfix",
                BindingFlags.NonPublic | BindingFlags.Static);
            var joinLobbyPostfix = new HarmonyMethod(joinLobbyPostfixMethod);
            var joinedLobbyPostfixMethod = typeof(HarmonyDiagnostics).GetMethod(
                "JoinedLobbyPostfix",
                BindingFlags.NonPublic | BindingFlags.Static);
            var joinedLobbyPostfix = new HarmonyMethod(joinedLobbyPostfixMethod);
            var lobbyCreatedPostfixMethod = typeof(HarmonyDiagnostics).GetMethod(
                "LobbyCreatedPostfix",
                BindingFlags.NonPublic | BindingFlags.Static);
            var lobbyCreatedPostfix = new HarmonyMethod(lobbyCreatedPostfixMethod);
            var playerHasJoinedMatchPostfixMethod = typeof(HarmonyDiagnostics).GetMethod(
                "PlayerHasJoinedMatchPostfix",
                BindingFlags.NonPublic | BindingFlags.Static);
            var playerHasJoinedMatchPostfix = new HarmonyMethod(playerHasJoinedMatchPostfixMethod);
            var requestJoinGameTranspilerMethod = typeof(HarmonyDiagnostics).GetMethod(
                "RequestJoinGameTranspiler",
                BindingFlags.NonPublic | BindingFlags.Static);
            var requestJoinGameTranspiler = new HarmonyMethod(requestJoinGameTranspilerMethod);
            var requestJoinGamePostfixMethod = typeof(HarmonyDiagnostics).GetMethod(
                "RequestJoinGamePostfix",
                BindingFlags.NonPublic | BindingFlags.Static);
            var requestJoinGamePostfix = new HarmonyMethod(requestJoinGamePostfixMethod);
            var playerStartPostfixMethod = typeof(HarmonyDiagnostics).GetMethod(
                "PlayerStartPostfix",
                BindingFlags.NonPublic | BindingFlags.Static);
            var playerStartPostfix = new HarmonyMethod(playerStartPostfixMethod);
            var assignCharacterPostfixMethod = typeof(HarmonyDiagnostics).GetMethod(
                "AssignCharacterPostfix",
                BindingFlags.NonPublic | BindingFlags.Static);
            var assignCharacterPostfix = new HarmonyMethod(assignCharacterPostfixMethod);
            var setPlayerCharacterPostfixMethod = typeof(HarmonyDiagnostics).GetMethod(
                "SetPlayerCharacterPostfix",
                BindingFlags.NonPublic | BindingFlags.Static);
            var setPlayerCharacterPostfix = new HarmonyMethod(setPlayerCharacterPostfixMethod);
            var patchedCount = 0;

            foreach (var target in Targets)
            {
                Type type;
                try
                {
                    type = AccessTools.TypeByName(target.TypeName);
                }
                catch (Exception exception)
                {
                    DiagnosticLog.Warning("Harmony type lookup failed for " + target + ": " + exception.Message);
                    continue;
                }

                if (type == null)
                {
                    DiagnosticLog.Warning("Harmony target type not found: " + target.TypeName);
                    continue;
                }

                var matched = false;
                var methods = type.GetMethods(
                    BindingFlags.Public |
                    BindingFlags.NonPublic |
                    BindingFlags.Instance |
                    BindingFlags.Static |
                    BindingFlags.DeclaredOnly);

                foreach (var method in methods)
                {
                    if (method.Name != target.MethodName || method.ContainsGenericParameters || method.IsAbstract)
                    {
                        continue;
                    }

                    matched = true;
                    try
                    {
                        var postfix = target.TypeName == "SteamLayer" && target.MethodName == "JoinLobby"
                            ? joinLobbyPostfix
                            : (target.TypeName == "SteamLayer" && target.MethodName == "LobbyCreated_Callback"
                                ? lobbyCreatedPostfix
                                : (target.TypeName == "ConnectionLayer" && target.MethodName == "PlayerHasJoinedMatch"
                                    ? playerHasJoinedMatchPostfix
                                    : (target.TypeName == "ConnectionLayer" && target.MethodName == "OnJoinedLobby"
                                    ? joinedLobbyPostfix
                                    : (target.TypeName == "SteamLayer" && target.MethodName == "LeaveMatch"
                                        ? new HarmonyMethod(typeof(HarmonyDiagnostics).GetMethod(
                                            "LeaveMatchPostfix",
                                            BindingFlags.NonPublic | BindingFlags.Static))
                                        : (target.TypeName == "Player" && target.MethodName == "Start"
                                            ? playerStartPostfix
                                            : (target.TypeName == "Player" && target.MethodName == "AssignCharacter"
                                                ? assignCharacterPostfix
                                                 : (target.TypeName == "HeroController" &&
                                                    target.MethodName == "SetPlayerCharacter"
                                                     ? setPlayerCharacterPostfix
                                                     : (target.TypeName == "HeroController" &&
                                                        target.MethodName == "RequestJoinGame"
                                                         ? requestJoinGamePostfix
                                                         : null))))))));
                        var transpiler = target.TypeName == "HeroController" &&
                                         target.MethodName == "RequestJoinGame"
                            ? requestJoinGameTranspiler
                            : null;
                        _harmony.Patch(method, prefix, postfix, transpiler, null);
                        patchedCount++;
                    }
                    catch (Exception exception)
                    {
                        DiagnosticLog.Warning("Harmony patch failed for " + DescribeMethod(method) + ": " + exception);
                    }
                }

                if (!matched)
                {
                    DiagnosticLog.Warning("Harmony target method not found: " + target);
                }
            }

            PatchSwitchLevelTranspiler();
            PatchWorldMapEnterMissionTranspiler();
            PatchGameStateLoadLevelPrefix();
            PatchLateHeroResponseGuard();
            PatchWorkshopHeroTypePreservation();
            PatchWorkshopJoinPromptSuppression();
            PatchMainMenuInitializationPostfix();
            PatchMainMenuInitializationDelay();
            PatchLobbyMainMenuReturnPostfix();
            PatchMainMenuMenuActiveSetter();
            PatchMainMenuShowRoutineCompletion();
            NotifySceneLoaded(SceneManager.GetActiveScene());

            DiagnosticLog.Info("Harmony method tracing enabled; patched methods=" + patchedCount + ".");
        }

        public static void Stop()
        {
            if (_harmony == null)
            {
                return;
            }

            try
            {
                UnsubscribeWorkshopCompletion();
                _harmony.UnpatchAll(HarmonyId);
                DiagnosticLog.Info("Harmony method tracing disabled.");
            }
            catch (Exception exception)
            {
                DiagnosticLog.Warning("Harmony unpatch failed: " + exception);
            }
            finally
            {
                _harmony = null;
                _injectedForSession = false;
                _workshopCompletionHandledForSession = false;
                _joinLobbyInProgress = false;
                _sessionIsHost = false;
                _networkSessionActive = false;
                _joinLobbyCleanupIgnoreUntilUtc = DateTime.MinValue;
                ClearWorkshopOnlineLobbyReturnState();
                _restoreMainMenuAfterLobbyReturnPending = false;
                MainMenuRendererStates.Clear();
                _workshopSpawnRebroadcastAtUtc = DateTime.MinValue;
                _workshopSpawnRebroadcastPending = false;
                _workshopSpawnRebroadcastUseCurrentPositions = false;
                _lastLocalWorkshopControllerId = -1;
                ClearDuplicateWorkshopLoadSuppression();
                ClearWorkshopLocalJoinRequests();
                ClearLateJoinState();
                ClearLifecycleState();
                lock (Sync)
                {
                    TraceCache.Clear();
                }
            }
        }

        private static bool TracePrefix(MethodBase __originalMethod, object __instance, object[] __args)
        {
            try
            {
                if (__originalMethod != null &&
                    __originalMethod.DeclaringType != null &&
                    __originalMethod.DeclaringType.Name == "HeroController" &&
                    __originalMethod.Name == "AddLocalPlayer")
                {
                    NormalizeWorkshopLocalJoinController(__args);
                    if (ShouldSuppressDuplicateWorkshopLocalJoin(__args))
                    {
                        return false;
                    }
                }

                if (__originalMethod != null &&
                    __originalMethod.DeclaringType != null &&
                    __originalMethod.DeclaringType.Name == "HeroController" &&
                    __originalMethod.Name == "SpawnJoinedPlayers")
                {
                    PrepareWorkshopSpawnJoinedPlayers();
                }

                if (__originalMethod.DeclaringType != null &&
                    __originalMethod.DeclaringType.Name == "SteamLayer" &&
                    (__originalMethod.Name == "CreateMatch" || __originalMethod.Name == "JoinLobby"))
                {
                    _sessionIsHost = __originalMethod.Name == "CreateMatch";
                    _networkSessionActive = true;
                    if (__originalMethod.Name == "JoinLobby")
                    {
                        _joinLobbyInProgress = true;
                        _joinLobbyCleanupIgnoreUntilUtc = DateTime.UtcNow.AddSeconds(5);
                    }

                    DiagnosticLog.BeginSession(
                        "SteamLayer." + __originalMethod.Name,
                        __originalMethod.Name == "CreateMatch" ? "host" : "client");
                    Interlocked.Exchange(ref _sequence, 0);
                    lock (Sync)
                    {
                        TraceCache.Clear();
                    }
                    ResetWorkshopStateForNewSession(__originalMethod.Name);
                }

                if (__originalMethod.DeclaringType != null &&
                    __originalMethod.DeclaringType.Name == "HeroController" &&
                    __originalMethod.Name == "RequestJoinGame")
                {
                    PrepareLateWorkshopJoinSlot();
                }

                ObserveWorkshopOnlineLobbyReturnBeforeTrace(__originalMethod, __args);
                ObserveLifecycleBeforeTrace(__originalMethod, __instance, __args);
                var sequence = Interlocked.Increment(ref _sequence);
                var message = BuildTraceMessage(__originalMethod, __instance, __args);
                var key = DescribeMethod(__originalMethod);
                string suppressionSummary;
                if (ShouldWrite(key, message, out suppressionSummary))
                {
                    if (!string.IsNullOrEmpty(suppressionSummary))
                    {
                        DiagnosticLog.Trace(suppressionSummary);
                    }

                    DiagnosticLog.Trace("TRACE #" + sequence + " " + message);
                }
            }
            catch (Exception exception)
            {
                DiagnosticLog.Warning("Harmony trace formatter failed: " + exception.Message);
            }

            return true;
        }

        private static bool ShouldSuppressDuplicateWorkshopLocalJoin(object[] arguments)
        {
            if (!IsWorkshopJoinProtectionActive() || arguments == null || arguments.Length < 2 ||
                !(arguments[0] is int) || !(arguments[1] is int))
            {
                return false;
            }

            var playerNum = (int)arguments[0];
            var controllerNum = (int)arguments[1];

            RemoveExpiredWorkshopLocalJoinRequests();

            if (HasActiveWorkshopLocalPlayer())
            {
                if (!HasActiveWorkshopLocalPlayerForController(controllerNum))
                {
                    RebindActiveLocalWorkshopPlayer(controllerNum);
                    LogWorkshopLocalJoinSuppressionWarning(
                        "a local slot is already active",
                        playerNum,
                        controllerNum);
                }

                return true;
            }

            DateTime previousRequestAtUtc;
            if (WorkshopLocalJoinRequests.TryGetValue(controllerNum, out previousRequestAtUtc) &&
                DateTime.UtcNow - previousRequestAtUtc <
                TimeSpan.FromSeconds(WorkshopLocalJoinRequestRetrySeconds))
            {
                return true;
            }

            if (WorkshopLocalJoinRequests.Count > 0)
            {
                LogWorkshopLocalJoinSuppressionWarning(
                    "another local slot request is pending",
                    playerNum,
                    controllerNum);
                return true;
            }

            WorkshopLocalJoinRequests[controllerNum] = DateTime.UtcNow;
            return false;
        }

        private static void RebindActiveLocalWorkshopPlayer(int controllerNum)
        {
            if (controllerNum < 0 || HeroController.PIDS == null ||
                HeroController.playerControllerIDs == null)
            {
                return;
            }

            var playersPlaying = GetPlayersPlayingArray();
            var count = System.Math.Min(
                4,
                System.Math.Min(HeroController.PIDS.Length, HeroController.playerControllerIDs.Length));
            for (var index = 0; index < count; index++)
            {
                var pid = HeroController.PIDS[index];
                if (pid == null || !pid.IsMine ||
                    (playersPlaying != null &&
                     (index >= playersPlaying.Length || !playersPlaying[index])))
                {
                    continue;
                }

                var previousController = HeroController.playerControllerIDs[index];
                HeroController.playerControllerIDs[index] = controllerNum;
                var player = HeroController.players == null || index >= HeroController.players.Length
                    ? null
                    : HeroController.players[index];
                if (player != null)
                {
                    SetFieldOrProperty(player, "controllerNum", controllerNum);
                }

                RememberLastLocalWorkshopController(controllerNum);
                DiagnosticLog.Warning(
                    "Switched active local Workshop player to the controller that requested join: " +
                    "player=" + index + "; previousController=" + previousController +
                    "; controller=" + controllerNum + ".");
                return;
            }
        }

        private static void RememberLastLocalWorkshopController(int controllerNum)
        {
            if (controllerNum >= 0)
            {
                _lastLocalWorkshopControllerId = controllerNum;
            }
        }

        private static void NormalizeWorkshopLocalJoinController(object[] arguments)
        {
            if (!IsWorkshopJoinProtectionActive() || arguments == null || arguments.Length < 2 ||
                !(arguments[0] is int) || !(arguments[1] is int) ||
                WorkshopDropoutControllerIds.Count == 0 || PendingLocalWorkshopRejoins.Count == 0)
            {
                return;
            }

            var requestedPlayerNum = (int)arguments[0];
            var requestedControllerId = (int)arguments[1];
            var playerNum = requestedPlayerNum;
            if (playerNum < 0)
            {
                playerNum = GetImmediateNextUnusedPlayerNumber();
            }

            var savedControllerId = -1;
            var hasSavedController = playerNum >= 0 &&
                WorkshopDropoutControllerIds.TryGetValue(playerNum, out savedControllerId) &&
                savedControllerId >= 0;
            if (!hasSavedController)
            {
                foreach (var pendingPlayerNum in PendingLocalWorkshopRejoins)
                {
                    if (WorkshopDropoutControllerIds.TryGetValue(
                            pendingPlayerNum,
                            out savedControllerId) && savedControllerId >= 0)
                    {
                        playerNum = pendingPlayerNum;
                        hasSavedController = true;
                        break;
                    }
                }
            }

            if (!hasSavedController || savedControllerId == requestedControllerId)
            {
                return;
            }

            arguments[1] = savedControllerId;
            DiagnosticLog.Info(
                "Rewrote local Workshop rejoin controller to saved binding: player=" +
                playerNum + "; requestedController=" + requestedControllerId +
                "; controller=" + savedControllerId + ".");
        }

        private static void LogWorkshopLocalJoinSuppressionWarning(
            string reason,
            int playerNum,
            int controllerNum)
        {
            var key = reason + "|" + controllerNum;
            var now = DateTime.UtcNow;
            DateTime previousWarningAtUtc;
            if (WorkshopLocalJoinSuppressionWarnings.TryGetValue(key, out previousWarningAtUtc) &&
                now - previousWarningAtUtc <
                TimeSpan.FromSeconds(WorkshopLocalJoinRequestRetrySeconds))
            {
                return;
            }

            WorkshopLocalJoinSuppressionWarnings[key] = now;
            DiagnosticLog.Warning(
                "Suppressed additional Workshop local-player request because " + reason + ": " +
                "player=" + playerNum + "; controller=" + controllerNum +
                "; further matching warnings are silent for " +
                WorkshopLocalJoinRequestRetrySeconds + " seconds.");
        }

        private static void RemoveExpiredWorkshopLocalJoinRequests()
        {
            if (WorkshopLocalJoinRequests.Count == 0)
            {
                return;
            }

            var now = DateTime.UtcNow;
            var expiredControllers = new List<int>();
            foreach (var request in WorkshopLocalJoinRequests)
            {
                if (now - request.Value >= TimeSpan.FromSeconds(WorkshopLocalJoinRequestRetrySeconds))
                {
                    expiredControllers.Add(request.Key);
                }
            }

            foreach (var controllerNum in expiredControllers)
            {
                WorkshopLocalJoinRequests.Remove(controllerNum);
            }
        }

        private static bool IsWorkshopJoinProtectionActive()
        {
            return IsOnline() && _networkSessionActive && HasValidWorkshopInjectionConfiguration();
        }

        private static bool HasValidWorkshopInjectionConfiguration()
        {
            var settings = Plugin.Settings;
            if (settings == null || !settings.EnableOnlineWorkshopInjection)
            {
                return false;
            }

            ulong workshopId;
            return UInt64.TryParse((settings.WorkshopId ?? string.Empty).Trim(), out workshopId) &&
                   workshopId != 0;
        }

        private static void ResetStalePauseStateForWorkshopSession(string trigger)
        {
            if (!HasValidWorkshopInjectionConfiguration())
            {
                return;
            }

            var previousStatus = PauseController.pauseStatus;
            var previousController = PauseController.pausedByController;
            var previousDelayInput = PauseController.DelayInput;
            var previousInputBlocked = InputReader.IsBlocked;

            PauseController.pauseStatus = PauseStatus.UnPaused;
            PauseController.pausedByController = -1;
            InputReader.IsBlocked = false;

            try
            {
                // DelayInput is exposed as a read-only property by this game build;
                // clear its backing static field so a previous menu transition cannot
                // keep Player.GetInput returning an empty input state.
                SetStaticFieldOrProperty(typeof(PauseController), "delayInput", false);
                var pauseController = PauseController.instance;
                if (pauseController != null)
                {
                    if (pauseController.pauseCam != null)
                    {
                        pauseController.pauseCam.gameObject.SetActive(false);
                    }

                    var playerListCanvasField = typeof(PauseController).GetField(
                        "playerListCanvas",
                        BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                    var playerListCanvas = playerListCanvasField == null
                        ? null
                        : playerListCanvasField.GetValue(pauseController) as Behaviour;
                    if (playerListCanvas != null)
                    {
                        playerListCanvas.enabled = false;
                        playerListCanvas.gameObject.SetActive(false);
                    }
                }

                DiagnosticLog.Info(
                    "Cleared stale pause state before Workshop online session: trigger=" + trigger +
                    "; previousStatus=" + previousStatus +
                    "; previousController=" + previousController +
                    "; previousDelayInput=" + previousDelayInput +
                    "; previousInputBlocked=" + previousInputBlocked + ".");
            }
            catch (Exception exception)
            {
                DiagnosticLog.Warning(
                    "Workshop online session cleared stale input ownership but could not hide all pause UI: " +
                    exception.Message);
            }

            NormalizeLocalWorkshopPlayerControlState(trigger);
        }

        private static void NormalizeLocalWorkshopPlayerControlState(string trigger)
        {
            try
            {
                var controller = HeroController.Instance;
                if (controller != null && controller.IDroppedOutThisRound)
                {
                    controller.IDroppedOutThisRound = false;
                    DiagnosticLog.Warning(
                        "Cleared stale local Workshop dropout-round flag: trigger=" + trigger + ".");
                }

                if (HeroController.PIDS == null || HeroController.players == null ||
                    HeroController.playerControllerIDs == null)
                {
                    return;
                }

                var count = System.Math.Min(4, System.Math.Min(
                    HeroController.PIDS.Length,
                    System.Math.Min(HeroController.players.Length, HeroController.playerControllerIDs.Length)));
                for (var index = 0; index < count; index++)
                {
                    var pid = HeroController.PIDS[index];
                    var player = HeroController.players[index];
                    var controllerNum = HeroController.playerControllerIDs[index];
                    int savedDropoutController;
                    if (pid != null && pid.IsMine &&
                        WorkshopDropoutControllerIds.TryGetValue(index, out savedDropoutController) &&
                        savedDropoutController >= 0 && controllerNum != savedDropoutController)
                    {
                        var savedControllerPrevious = controllerNum;
                        controllerNum = savedDropoutController;
                        HeroController.playerControllerIDs[index] = controllerNum;
                        DiagnosticLog.Warning(
                            "Restored saved local Workshop controller binding: player=" + index +
                            "; previousController=" + savedControllerPrevious +
                            "; controller=" + controllerNum + ".");
                    }
                    if (pid == null || !pid.IsMine || player == null || controllerNum < 0 ||
                        player.controllerNum == controllerNum)
                    {
                        continue;
                    }

                    var previousController = player.controllerNum;
                    SetFieldOrProperty(player, "controllerNum", controllerNum);
                    DiagnosticLog.Warning(
                        "Restored local Workshop controller ownership: player=" + index +
                        "; previousController=" + previousController +
                        "; controller=" + controllerNum +
                        "; trigger=" + trigger + ".");
                }
            }
            catch (Exception exception)
            {
                DiagnosticLog.Warning(
                    "Workshop local player control-state normalization failed: " + exception);
            }
        }

        private static bool HasActiveWorkshopLocalPlayer()
        {
            if (HeroController.PIDS == null)
            {
                return false;
            }

            var playersPlaying = GetPlayersPlayingArray();
            var count = System.Math.Min(4, HeroController.PIDS.Length);
            for (var index = 0; index < count; index++)
            {
                var pid = HeroController.PIDS[index];
                if (pid == null || !pid.IsMine)
                {
                    continue;
                }

                if (playersPlaying == null || index >= playersPlaying.Length || playersPlaying[index])
                {
                    return true;
                }
            }

            return false;
        }

        private static bool HasActiveWorkshopLocalPlayerForController(int controllerNum)
        {
            if (HeroController.PIDS == null || HeroController.playerControllerIDs == null)
            {
                return false;
            }

            var playersPlaying = GetPlayersPlayingArray();
            var count = System.Math.Min(
                4,
                System.Math.Min(HeroController.PIDS.Length, HeroController.playerControllerIDs.Length));
            for (var index = 0; index < count; index++)
            {
                var pid = HeroController.PIDS[index];
                if (pid == null || !pid.IsMine ||
                    HeroController.playerControllerIDs[index] != controllerNum)
                {
                    continue;
                }

                if (playersPlaying == null || index >= playersPlaying.Length || playersPlaying[index])
                {
                    return true;
                }
            }

            return false;
        }

        private static void PrepareWorkshopSpawnJoinedPlayers()
        {
            if (_lateJoinStarted && !_sessionIsHost)
            {
                if (!_lateJoinSpawnJoinedPlayersSeen)
                {
                    DiagnosticLog.Info(
                        "Late workshop SpawnJoinedPlayers observed; automatic local join is now eligible.");
                }

                _lateJoinSpawnJoinedPlayersSeen = true;
                _lateJoinAutoJoinAtUtc = DateTime.UtcNow.AddMilliseconds(
                    LateJoinAutoJoinDelayMilliseconds);
            }

            if (!IsWorkshopJoinProtectionActive() || HeroController.PIDS == null ||
                HeroController.players == null || HeroController.playerControllerIDs == null)
            {
                return;
            }

            var playersPlaying = GetPlayersPlayingArray();
            if (playersPlaying == null)
            {
                return;
            }

            var count = System.Math.Min(4, System.Math.Min(
                HeroController.PIDS.Length,
                System.Math.Min(HeroController.players.Length, HeroController.playerControllerIDs.Length)));
            var keptLocalPlayer = -1;
            for (var index = 0; index < count; index++)
            {
                var pid = HeroController.PIDS[index];
                if (pid == null || !pid.IsMine || index >= playersPlaying.Length || !playersPlaying[index])
                {
                    continue;
                }

                if (keptLocalPlayer < 0)
                {
                    keptLocalPlayer = index;
                    continue;
                }

                if (HeroController.players[index] != null)
                {
                    DiagnosticLog.Warning(
                        "Workshop found an extra local player slot with an existing Player object; " +
                        "leaving it untouched: player=" + index + ".");
                    continue;
                }

                playersPlaying[index] = false;
                HeroController.PIDS[index] = null;
                HeroController.playerControllerIDs[index] = -1;
                DiagnosticLog.Warning(
                    "Removed duplicate local Workshop player slot before SpawnJoinedPlayers: " +
                    "player=" + index + "; keptPlayer=" + keptLocalPlayer + ".");
            }
        }

        private static void ForgetWorkshopLocalJoinRequest(object[] arguments)
        {
            if (arguments == null || arguments.Length == 0 || !(arguments[0] is int))
            {
                return;
            }

            var playerNum = (int)arguments[0];
            if (playerNum >= 0 && playerNum < 4 && HeroController.PIDS != null &&
                playerNum < HeroController.PIDS.Length && HeroController.PIDS[playerNum] != null &&
                HeroController.PIDS[playerNum].IsMine)
            {
                ClearWorkshopLocalJoinRequests();
                if (_lateJoinStarted && !_lateJoinAutoJoinCompleted)
                {
                    _lateJoinPlayerJoinRequested = false;
                    _lateJoinPlayerRequestAtUtc = DateTime.MinValue;
                    _lateJoinAutoJoinAtUtc = DateTime.UtcNow.AddMilliseconds(
                        LateJoinAutoJoinDelayMilliseconds);
                    DiagnosticLog.Info(
                        "Late workshop local player dropout released the pending automatic join request; " +
                        "the same local slot can be requested again.");
                }
            }
        }

        private static void ClearWorkshopLocalJoinRequests()
        {
            WorkshopLocalJoinRequests.Clear();
            WorkshopLocalJoinSuppressionWarnings.Clear();
        }

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

        private static void RememberLocalWorkshopDropout(object[] arguments)
        {
            if (!IsWorkshopOnlineSession() || arguments == null || arguments.Length == 0)
            {
                return;
            }

            var playerNum = arguments[0] is int ? (int)arguments[0] : -1;
            if (playerNum < 0 || playerNum >= 4 || HeroController.PIDS == null)
            {
                return;
            }

            CaptureWorkshopDropoutHeroType(playerNum);

            var pid = HeroController.PIDS[playerNum];
            if (pid == null || !pid.IsMine)
            {
                return;
            }

            if (HeroController.playerControllerIDs != null &&
                playerNum < HeroController.playerControllerIDs.Length)
            {
                var controllerId = HeroController.playerControllerIDs[playerNum];
                if (controllerId >= 0)
                {
                    WorkshopDropoutControllerIds[playerNum] = controllerId;
                    DiagnosticLog.Info(
                        "Saved local Workshop controller for dropout rejoin: player=" +
                        playerNum + "; controller=" + controllerId + ".");
                }
            }

            PendingLocalWorkshopRejoins.Add(playerNum);
            if (_lateJoinStarted)
            {
                // A native dropout can remove the local Player object after the
                // initial late-join request was already marked complete. Re-arm
                // the same readiness path so the slot is requested again.
                _lateJoinAutoJoinCompleted = false;
                _lateJoinPlayerJoinRequested = false;
                _lateJoinPlayerRequestAtUtc = DateTime.MinValue;
                _lateJoinAutoJoinAtUtc = DateTime.UtcNow.AddMilliseconds(
                    LateJoinAutoJoinDelayMilliseconds);
                DiagnosticLog.Info(
                    "Re-armed late workshop automatic join after local dropout: " +
                    "player=" + playerNum + "; retry scheduled after " +
                    LateJoinAutoJoinDelayMilliseconds + "ms.");
            }
            DiagnosticLog.Warning(
                "Local Workshop player dropout observed: player=" + playerNum +
                "; waiting for the same local slot to rejoin so its round state can be restored.");
        }

        private static void CaptureWorkshopDropoutHeroType(int playerNum)
        {
            if (!IsWorkshopOnlineSession() || playerNum < 0 || playerNum >= 4)
            {
                return;
            }

            HeroType heroType;
            var player = HeroController.players == null || playerNum >= HeroController.players.Length
                ? null
                : HeroController.players[playerNum];
            if (!TryGetPlayerHeroType(player, out heroType) &&
                !WorkshopKnownHeroTypes.TryGetValue(playerNum, out heroType))
            {
                return;
            }

            if (heroType == HeroType.None)
            {
                return;
            }

            WorkshopDropoutHeroTypes[playerNum] = heroType;
            DiagnosticLog.Info(
                "Saved Workshop hero type for dropout rejoin: player=" +
                playerNum + "; hero=" + heroType + ".");
        }

        private static void RememberWorkshopHeroType(object[] arguments)
        {
            if (arguments == null || arguments.Length < 3 || !(arguments[1] is int))
            {
                return;
            }

            HeroType heroType;
            if (!TryConvertHeroType(arguments[2], out heroType))
            {
                return;
            }

            RememberWorkshopHeroType((int)arguments[1], heroType);
        }

        private static void RememberWorkshopHeroType(Player player, object[] arguments)
        {
            if (player == null || arguments == null || arguments.Length == 0)
            {
                return;
            }

            HeroType heroType;
            if (TryConvertHeroType(arguments[0], out heroType))
            {
                RememberWorkshopHeroType(player.playerNum, heroType);
            }
        }

        private static void RememberWorkshopHeroType(int playerNum, HeroType heroType)
        {
            if (!IsWorkshopOnlineSession() || playerNum < 0 || playerNum >= 4 || heroType == HeroType.None)
            {
                return;
            }

            WorkshopKnownHeroTypes[playerNum] = heroType;
            HeroType expectedHeroType;
            if (WorkshopDropoutHeroTypes.TryGetValue(playerNum, out expectedHeroType) &&
                expectedHeroType == heroType)
            {
                WorkshopDropoutHeroTypes.Remove(playerNum);
            }
        }

        private static bool TryGetPlayerHeroType(Player player, out HeroType heroType)
        {
            heroType = HeroType.None;
            if (player == null)
            {
                return false;
            }

            return TryConvertHeroType(GetFieldOrPropertyValue(player, "heroType"), out heroType) &&
                   heroType != HeroType.None;
        }

        private static bool TryConvertHeroType(object value, out HeroType heroType)
        {
            heroType = HeroType.None;
            if (value == null)
            {
                return false;
            }

            if (value is HeroType)
            {
                heroType = (HeroType)value;
                return true;
            }

            try
            {
                heroType = (HeroType)Enum.Parse(typeof(HeroType), value.ToString(), true);
                return true;
            }
            catch
            {
                return false;
            }
        }

        private static object GetFieldOrPropertyValue(object instance, string name)
        {
            if (instance == null)
            {
                return null;
            }

            var type = instance.GetType();
            var field = type.GetField(
                name,
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static);
            if (field != null)
            {
                return field.GetValue(instance);
            }

            var property = type.GetProperty(
                name,
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static);
            return property == null || !property.CanRead
                ? null
                : property.GetValue(instance, null);
        }

        internal static bool TryGetWorkshopRejoinHeroType(int playerNum, out HeroType heroType)
        {
            return WorkshopDropoutHeroTypes.TryGetValue(playerNum, out heroType) &&
                   heroType != HeroType.None;
        }

        private static void PrepareLocalWorkshopRejoin(Player player)
        {
            if (!IsWorkshopOnlineSession() || player == null || !player.IsMine ||
                !PendingLocalWorkshopRejoins.Contains(player.playerNum))
            {
                return;
            }

            PendingLocalWorkshopRejoins.Remove(player.playerNum);
            PreparedLocalWorkshopRejoins.Add(player.playerNum);
            var lives = GetIntFieldOrProperty(player, "lives");
            if (lives <= 0)
            {
                SetFieldOrProperty(player, "lives", 1);
                DiagnosticLog.Warning(
                    "Restored local Workshop player life before Player.Start: player=" +
                    player.playerNum + "; previousLives=" + lives + ".");
            }

            var controller = HeroController.Instance;
            if (controller != null && controller.IDroppedOutThisRound)
            {
                controller.IDroppedOutThisRound = false;
                DiagnosticLog.Warning(
                    "Restored local Workshop player round state before Player.Start: player=" +
                    player.playerNum + ".");
            }
        }

        private static void PlayerStartPostfix(Player __instance)
        {
            if (__instance != null && __instance.IsMine && IsWorkshopOnlineSession())
            {
                RememberLastLocalWorkshopController(__instance.controllerNum);
                ResetStalePauseStateForWorkshopSession("local Workshop Player.Start");
            }

            if (__instance != null && __instance.IsMine && _lateJoinStarted &&
                !_lateJoinAutoJoinCompleted)
            {
                ClearWorkshopLocalJoinRequests();
                _lateJoinPlayerJoinRequested = false;
                _lateJoinPlayerRequestAtUtc = DateTime.MinValue;
                _lateJoinAutoJoinCompleted = true;
                DiagnosticLog.Info(
                    "Late workshop automatic join confirmed by local Player.Start: player=" +
                    __instance.playerNum + "; controller=" + __instance.controllerNum + ".");
            }

            if (!IsWorkshopOnlineSession() || __instance == null || !__instance.IsMine ||
                !PreparedLocalWorkshopRejoins.Remove(__instance.playerNum))
            {
                return;
            }

            var lives = GetIntFieldOrProperty(__instance, "lives");
            if (lives <= 0)
            {
                SetFieldOrProperty(__instance, "lives", 1);
                DiagnosticLog.Warning(
                    "Restored local Workshop player life after rejoin: player=" +
                    __instance.playerNum + "; previousLives=" + lives + ".");
            }

            if (__instance.character == null && !__instance.awaitingHeroTypeFromServer &&
                HasRegisteredLocalPid(__instance.playerNum))
            {
                try
                {
                    DiagnosticLog.Info(
                        "Requesting a fresh hero after local Workshop player rejoin: player=" +
                        __instance.playerNum + ".");
                    __instance.RespawnBro(false);
                }
                catch (Exception exception)
                {
                    DiagnosticLog.Warning(
                        "Local Workshop player rejoin respawn failed: " + exception);
                }
            }
            else if (__instance.character == null && !__instance.awaitingHeroTypeFromServer)
            {
                DiagnosticLog.Warning(
                    "Skipped local Workshop rejoin respawn because the player PID is not registered: player=" +
                    __instance.playerNum + ".");
            }
        }

        private static void CaptureDeferredSpawnPosition(Player player, object[] arguments)
        {
            if (!IsWorkshopOnlineSession() || player == null || arguments == null || arguments.Length < 4 ||
                !(arguments[3] is Vector3))
            {
                return;
            }

            var bro = arguments[0] as TestVanDammeAnim;
            var position = (Vector3)arguments[3];
            var spawnType = arguments[1] is Player.SpawnType
                ? (Player.SpawnType)arguments[1]
                : Player.SpawnType.CustomSpawnPoint;
            var spawnViaAirDrop = arguments[2] is bool && (bool)arguments[2];

            SnapFirstRemoteWorkshopCharacter(player, bro, position);

            if (bro != null && player.IsMine)
            {
                LocalWorkshopSpawnPositions[player.playerNum] = new DeferredSpawnPosition(
                    spawnType,
                    spawnViaAirDrop,
                    position);
                QueueWorkshopSpawnRebroadcast(
                    "local player received its original spawn position; waiting for settled physics",
                    750,
                    true);
                DiagnosticLog.Info(
                    "Recorded local Workshop spawn position for exact rebroadcast: player=" +
                    player.playerNum + "; position=" + FormatVector3(position) + ".");
            }

            if (bro != null && player.character != null)
            {
                return;
            }

            PendingSpawnPositions[player.playerNum] = new DeferredSpawnPosition(
                spawnType,
                spawnViaAirDrop,
                position);
            DiagnosticLog.Warning(
                "Deferred Workshop spawn position: player=" + player.playerNum +
                "; position=" + FormatVector3(position) +
                "; broArgument=" + (bro == null ? "null" : "present") + ".");
        }

        private static void AssignCharacterPostfix(Player __instance)
        {
            if (__instance != null)
            {
                ApplyDeferredSpawnPosition(__instance.playerNum, __instance.character);
            }
        }

        private static void SetPlayerCharacterPostfix(int index, TestVanDammeAnim character)
        {
            ApplyDeferredSpawnPosition(index, character);
            if (character != null && IsWorkshopOnlineSession() && HeroController.PIDS != null &&
                index >= 0 && index < HeroController.PIDS.Length && HeroController.PIDS[index] != null &&
                HeroController.PIDS[index].IsMine)
            {
                if (HeroController.playerControllerIDs != null &&
                    index < HeroController.playerControllerIDs.Length)
                {
                    RememberLastLocalWorkshopController(HeroController.playerControllerIDs[index]);
                }
                ResetStalePauseStateForWorkshopSession("local Workshop character assigned");
            }

            if (character != null && _lateJoinStarted && !_lateJoinAutoJoinCompleted &&
                HeroController.PIDS != null && index >= 0 && index < HeroController.PIDS.Length &&
                HeroController.PIDS[index] != null && HeroController.PIDS[index].IsMine)
            {
                ClearWorkshopLocalJoinRequests();
                _lateJoinPlayerJoinRequested = false;
                _lateJoinPlayerRequestAtUtc = DateTime.MinValue;
                _lateJoinAutoJoinCompleted = true;
                DiagnosticLog.Info(
                    "Late workshop automatic join confirmed by local SetPlayerCharacter: player=" +
                    index + ".");
            }

            if (character != null)
            {
                QueueWorkshopSpawnRebroadcast(
                    "a Workshop character was assigned; rebroadcasting its current authoritative position",
                    750,
                    true);
            }
        }

        private static bool ApplyDeferredSpawnPosition(int playerNum, TestVanDammeAnim character)
        {
            if (character == null)
            {
                return false;
            }

            DeferredSpawnPosition pending;
            if (!PendingSpawnPositions.TryGetValue(playerNum, out pending))
            {
                return false;
            }

            try
            {
                var player = HeroController.players == null || playerNum < 0 ||
                             playerNum >= HeroController.players.Length
                    ? null
                    : HeroController.players[playerNum];
                if (player == null)
                {
                    return false;
                }

                PendingSpawnPositions.Remove(playerNum);
                player.SetSpawnPositon(
                    character,
                    pending.SpawnType,
                    pending.SpawnViaAirDrop,
                    pending.Position);
                SnapFirstRemoteWorkshopCharacter(player, character, pending.Position);
                DiagnosticLog.Warning(
                    "Applied deferred Workshop spawn position through native Player.SetSpawnPositon: player=" +
                    playerNum + "; position=" + FormatVector3(pending.Position) + ".");
                return true;
            }
            catch (Exception exception)
            {
                DiagnosticLog.Warning(
                    "Applying deferred Workshop spawn position failed: " + exception);
                return false;
            }
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

        private static void SnapFirstRemoteWorkshopCharacter(
            Player player,
            TestVanDammeAnim character,
            Vector3 position)
        {
            if (player == null || character == null || player.IsMine)
            {
                return;
            }

            TestVanDammeAnim alreadySnapped;
            if (SnappedRemoteWorkshopCharacters.TryGetValue(player.playerNum, out alreadySnapped) &&
                alreadySnapped == character)
            {
                return;
            }

            try
            {
                var currentPosition = character.transform.position;
                character.transform.position = new Vector3(
                    position.x,
                    position.y,
                    currentPosition.z);
                SnappedRemoteWorkshopCharacters[player.playerNum] = character;
                DiagnosticLog.Info(
                    "Snapped the first remote Workshop character to the authoritative spawn position: " +
                    "player=" + player.playerNum + "; position=" +
                    FormatVector3(character.transform.position) + ".");
            }
            catch (Exception exception)
            {
                DiagnosticLog.Warning(
                    "Remote Workshop spawn-position snap failed: " + exception);
            }
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

        internal static void Update()
        {
            TryRebroadcastWorkshopSpawns();
            TryReturnToWorkshopOnlineLobby();
            TryRecoverWorkshopOnlineLobbyNavigationFailure();

            if (_lateJoinStarted)
            {
                TryCompleteLateWorkshopJoin();
                return;
            }

            if (!_lateJoinPending)
            {
                if (DateTime.UtcNow < _lateJoinTransitionPollAtUtc)
                {
                    return;
                }

                _lateJoinTransitionPollAtUtc = DateTime.UtcNow.AddMilliseconds(
                    WorkshopLobbyReadyPollMilliseconds);
                TryQueueLateJoinFromWorkshopPhase();
                return;
            }

            if (DateTime.UtcNow > _lateJoinDeadlineUtc)
            {
                DiagnosticLog.Warning(
                    "Late workshop join timed out before the client could load " + _lateJoinScene + ".");
                ClearLateJoinState();
                return;
            }

            if (DateTime.UtcNow < _lateJoinReadyPollAtUtc)
            {
                return;
            }
            _lateJoinReadyPollAtUtc = DateTime.UtcNow.AddMilliseconds(
                WorkshopLobbyReadyPollMilliseconds);

            RefreshWorkshopLobbyDataIfNeeded("late join poll");
            var lobbyPhase = GetWorkshopLobbyData(WorkshopLobbyPhaseKey);
            var lobbyReady = string.Equals(
                GetWorkshopLobbyData(WorkshopLobbyReadyKey),
                "1",
                StringComparison.Ordinal);
            var pidState = GetLocalPidState();
            var online = IsOnline();
            var onlineHost = IsOnlineHost();
            LogLateJoinWaitState(lobbyPhase, lobbyReady, pidState, online, onlineHost);

            // A loading phase is already enough to start a parallel local Workshop
            // load. Waiting for ready made a client that joined during LoadingScreen
            // depend on a lobby-data update it might never receive.
            var workshopTransitionActive = lobbyReady ||
                string.Equals(lobbyPhase, WorkshopLobbyPhaseLoading, StringComparison.Ordinal) ||
                string.Equals(lobbyPhase, WorkshopLobbyPhaseReady, StringComparison.Ordinal);
            if (!workshopTransitionActive)
            {
                return;
            }

            if (string.Equals(pidState, "not-set", StringComparison.Ordinal))
            {
                return;
            }

            TryStartLateJoin();
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

        private static void TryReturnToWorkshopOnlineLobby()
        {
            if (!_returnToWorkshopOnlineLobbyPending ||
                _returnToWorkshopOnlineLobbyAttempted ||
                DateTime.UtcNow < _returnToWorkshopOnlineLobbyAtUtc ||
                !string.Equals(SceneManager.GetActiveScene().name, "MainMenu", StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            try
            {
                var mainMenuType = AccessTools.TypeByName("MainMenu");
                if (mainMenuType == null)
                {
                    _returnToWorkshopOnlineLobbyAttempted = true;
                    RestoreWorkshopOnlineLobbyMainMenuVisuals();
                    ClearWorkshopOnlineLobbyReturnState();
                    DiagnosticLog.Warning(
                        "Workshop Esc return could not find MainMenu; staying on the default main menu.");
                    return;
                }

                var instance = GetMainMenuInstance(mainMenuType);
                if (instance == null)
                {
                    return;
                }

                // MainMenu initializes its item list after a native three-second delay. Opening the
                // lobby before that coroutine finishes disables MainMenu and permanently cancels it.
                if (!GetBoolFieldOrProperty(instance, "hasInitialized"))
                {
                    return;
                }

                var playModeType = AccessTools.TypeByName("MultiplayerPlayMode");
                if (playModeType == null)
                {
                    _returnToWorkshopOnlineLobbyAttempted = true;
                    RestoreWorkshopOnlineLobbyMainMenuVisuals();
                    ClearWorkshopOnlineLobbyReturnState();
                    DiagnosticLog.Warning(
                        "Workshop Esc return could not find MultiplayerPlayMode; staying on the default main menu.");
                    return;
                }

                var method = mainMenuType.GetMethod(
                    "TryToGoToLobby",
                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static,
                    null,
                    new[] { playModeType },
                    null);
                if (method == null)
                {
                    _returnToWorkshopOnlineLobbyAttempted = true;
                    RestoreWorkshopOnlineLobbyMainMenuVisuals();
                    ClearWorkshopOnlineLobbyReturnState();
                    DiagnosticLog.Warning(
                        "Workshop Esc return could not find MainMenu.TryToGoToLobby(MultiplayerPlayMode); staying on the default main menu.");
                    return;
                }

                _returnToWorkshopOnlineLobbyAttempted = true;
                _returnToWorkshopOnlineLobbyNavigationStartedAtUtc = DateTime.UtcNow;
                SuppressWorkshopOnlineLobbyMainMenuVisuals(instance);
                ResetStalePauseStateForWorkshopSession("Workshop Esc return after MainMenu initialization");
                var onlineMode = Enum.Parse(playModeType, "Online");
                method.Invoke(method.IsStatic ? null : instance, new[] { onlineMode });
                DiagnosticLog.Info(
                    "Workshop Esc return waited for MainMenu initialization and navigated directly " +
                    "to the online lobby browser via " +
                    "MainMenu.TryToGoToLobby(MultiplayerPlayMode.Online).");
            }
            catch (Exception exception)
            {
                RestoreWorkshopOnlineLobbyMainMenuVisuals();
                ClearWorkshopOnlineLobbyReturnState();
                DiagnosticLog.Warning(
                    "Workshop Esc return to the online lobby browser failed: " + exception);
            }
        }

        private static void TryRecoverWorkshopOnlineLobbyNavigationFailure()
        {
            if (!_returnToWorkshopOnlineLobbyAttempted ||
                !_returnToWorkshopOnlineLobbyVisualsSuppressed ||
                !string.Equals(SceneManager.GetActiveScene().name, "MainMenu", StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            var mainMenu = GetMainMenuInstance(AccessTools.TypeByName("MainMenu")) as MainMenu;
            if (mainMenu == null)
            {
                return;
            }

            if (mainMenu.lobby != null && mainMenu.lobby.gameObject.activeSelf)
            {
                _returnToWorkshopOnlineLobbyVisualsSuppressed = false;
                DiagnosticLog.Info(
                    "Workshop Esc return opened the online lobby without rendering the intermediate main menu.");
                ClearWorkshopOnlineLobbyReturnState();
                return;
            }

            var navigationTimedOut =
                _returnToWorkshopOnlineLobbyNavigationStartedAtUtc != DateTime.MinValue &&
                DateTime.UtcNow >= _returnToWorkshopOnlineLobbyNavigationStartedAtUtc.AddSeconds(
                    WorkshopOnlineLobbyNavigationTimeoutSeconds);
            var failureGraceElapsed =
                _returnToWorkshopOnlineLobbyNavigationStartedAtUtc != DateTime.MinValue &&
                DateTime.UtcNow >= _returnToWorkshopOnlineLobbyNavigationStartedAtUtc.AddMilliseconds(
                    WorkshopOnlineLobbyNavigationFailureGraceMilliseconds);
            if (!navigationTimedOut &&
                (IsPersistentOverlayMessageShowing() || !failureGraceElapsed))
            {
                return;
            }

            RestoreWorkshopOnlineLobbyMainMenuVisuals();
            ClearWorkshopOnlineLobbyReturnState();
            DiagnosticLog.Warning(
                "Workshop Esc return could not open the online lobby; restored the fully initialized " +
                "main menu instead of leaving hidden or incomplete controls.");
        }

        private static void SuppressWorkshopOnlineLobbyMainMenuVisuals(object instance)
        {
            if (!_returnToWorkshopOnlineLobbyPending)
            {
                return;
            }

            var mainMenu = instance as MainMenu;
            if (mainMenu == null)
            {
                return;
            }

            var alreadySuppressed = _returnToWorkshopOnlineLobbyVisualsSuppressed;
            mainMenu.MenuActive = false;
            if (mainMenu.logo != null)
            {
                mainMenu.logo.SetActive(false);
            }

            _returnToWorkshopOnlineLobbyVisualsSuppressed = true;
            if (!alreadySuppressed)
            {
                DiagnosticLog.Info(
                    "Workshop Esc return suppressed intermediate MainMenu visuals while native menu " +
                    "initialization remains active.");
            }
        }

        private static void RestoreWorkshopOnlineLobbyMainMenuVisuals()
        {
            if (!_returnToWorkshopOnlineLobbyVisualsSuppressed)
            {
                return;
            }

            var mainMenu = GetMainMenuInstance(AccessTools.TypeByName("MainMenu")) as MainMenu;
            if (mainMenu != null)
            {
                if (mainMenu.logo != null)
                {
                    mainMenu.logo.SetActive(true);
                }

                mainMenu.MenuActive = true;
                mainMenu.TransitionIn();
            }

            InputReader.IsBlocked = false;
            _returnToWorkshopOnlineLobbyVisualsSuppressed = false;
        }

        private static bool IsPersistentOverlayMessageShowing()
        {
            try
            {
                var overlayType = AccessTools.TypeByName("Interface.PersistentOverlayUI");
                var property = overlayType == null
                    ? null
                    : overlayType.GetProperty(
                        "IsOverlayMessageShowing",
                        BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
                return property != null && Convert.ToBoolean(property.GetValue(null, null));
            }
            catch
            {
                return false;
            }
        }

        private static object GetMainMenuInstance(Type mainMenuType)
        {
            if (mainMenuType == null)
            {
                return null;
            }

            var instanceField = mainMenuType.GetField(
                "instance",
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
            if (instanceField != null)
            {
                var instance = instanceField.GetValue(null);
                if (instance != null)
                {
                    return instance;
                }
            }

            return UnityEngine.Object.FindObjectOfType(mainMenuType);
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
            var configuredWorkshopId = (settings.WorkshopId ?? string.Empty).Trim();
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

        private static bool IsWorkshopLobbyReady()
        {
            return string.Equals(
                GetWorkshopLobbyData(WorkshopLobbyReadyKey),
                "1",
                StringComparison.Ordinal);
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

        private static void RefreshWorkshopLobbyDataIfNeeded(string context)
        {
            if (_sessionIsHost || DateTime.UtcNow < _lateJoinLobbyRefreshAtUtc)
            {
                return;
            }

            _lateJoinLobbyRefreshAtUtc = DateTime.UtcNow.AddMilliseconds(
                WorkshopLobbyDataRefreshMilliseconds);
            try
            {
                var lobbyId = GetLobbySteamId();
                if (lobbyId == null)
                {
                    return;
                }

                var matchmakingType = AccessTools.TypeByName("Steamworks.SteamMatchmaking");
                var requestLobbyData = FindSteamMatchmakingMethod(matchmakingType, "RequestLobbyData", 1);
                if (requestLobbyData == null)
                {
                    if (!_lateJoinLobbyRefreshWarningLogged)
                    {
                        _lateJoinLobbyRefreshWarningLogged = true;
                        DiagnosticLog.Warning(
                            "Workshop lobby data refresh could not find SteamMatchmaking.RequestLobbyData.");
                    }
                    return;
                }

                var result = Convert.ToBoolean(requestLobbyData.Invoke(null, new[] { lobbyId }));
                DiagnosticLog.Trace(
                    "Workshop lobby data refresh requested: context=" + context +
                    "; result=" + result + ".");
            }
            catch (Exception exception)
            {
                if (!_lateJoinLobbyRefreshWarningLogged)
                {
                    _lateJoinLobbyRefreshWarningLogged = true;
                    DiagnosticLog.Warning("Workshop lobby data refresh failed: " + exception);
                }
            }
        }

        private static bool SetWorkshopLobbyReady(bool ready, string context)
        {
            try
            {
                if (!_sessionIsHost)
                {
                    return false;
                }

                return SetWorkshopLobbyData(
                    WorkshopLobbyReadyKey,
                    ready ? "1" : "0",
                    "readiness=" + (ready ? "ready" : "not-ready") + "; " + context);
            }
            catch (Exception exception)
            {
                DiagnosticLog.Warning("Workshop lobby readiness write failed: " + exception);
                return false;
            }
        }

        private static bool SetWorkshopLobbyPhase(string phase, string context)
        {
            return SetWorkshopLobbyData(WorkshopLobbyPhaseKey, phase ?? string.Empty, "phase=" +
                (string.IsNullOrEmpty(phase) ? "cleared" : phase) + "; " + context);
        }

        private static string GetWorkshopLobbyData(string key)
        {
            try
            {
                var lobbyId = GetLobbySteamId();
                if (lobbyId == null)
                {
                    return string.Empty;
                }

                var matchmakingType = AccessTools.TypeByName("Steamworks.SteamMatchmaking");
                var getLobbyData = FindSteamMatchmakingMethod(matchmakingType, "GetLobbyData", 2);
                if (getLobbyData == null)
                {
                    return string.Empty;
                }

                return getLobbyData.Invoke(null, new[] { lobbyId, key }) as string ?? string.Empty;
            }
            catch
            {
                return string.Empty;
            }
        }

        private static bool SetWorkshopLobbyData(string key, string value, string context)
        {
            try
            {
                if (!_sessionIsHost)
                {
                    return false;
                }

                var lobbyId = GetLobbySteamId();
                if (lobbyId == null)
                {
                    DiagnosticLog.Warning(
                        "Workshop lobby data could not find a valid lobby ID; key=" + key +
                        "; context=" + context + ".");
                    return false;
                }

                var matchmakingType = AccessTools.TypeByName("Steamworks.SteamMatchmaking");
                var setLobbyData = FindSteamMatchmakingMethod(matchmakingType, "SetLobbyData", 3);
                if (setLobbyData == null)
                {
                    DiagnosticLog.Warning("Workshop lobby data could not find SteamMatchmaking.SetLobbyData.");
                    return false;
                }

                var result = Convert.ToBoolean(setLobbyData.Invoke(
                    null,
                    new[] { lobbyId, key, value ?? string.Empty }));
                if (result)
                {
                    if (key == WorkshopLobbyReadyKey)
                    {
                        DiagnosticLog.Info(
                            "Workshop lobby readiness=" +
                            (value == "1" ? "ready" : "not-ready") +
                            "; context=" + context + ".");
                    }
                    else if (key == WorkshopLobbyPhaseKey)
                    {
                        DiagnosticLog.Info(
                            "Workshop lobby phase=" + (value ?? string.Empty) +
                            "; context=" + context + ".");
                    }
                    else
                    {
                        DiagnosticLog.Info(
                            "Workshop lobby data " + key + "=" + (value ?? string.Empty) +
                            "; context=" + context + ".");
                    }
                }
                else
                {
                    DiagnosticLog.Warning(
                        "Workshop lobby data write returned false; key=" + key +
                        "; context=" + context + ".");
                }

                return result;
            }
            catch (Exception exception)
            {
                DiagnosticLog.Warning("Workshop lobby data write failed: " + exception);
                return false;
            }
        }

        private static object GetLobbySteamId()
        {
            var steamLayerType = AccessTools.TypeByName("SteamLayer");
            if (steamLayerType == null)
            {
                return null;
            }

            var instanceProperty = steamLayerType.GetProperty(
                "Instance",
                BindingFlags.Public |
                BindingFlags.NonPublic |
                BindingFlags.Static |
                BindingFlags.FlattenHierarchy);
            var steamLayer = instanceProperty == null
                ? null
                : instanceProperty.GetValue(null, null);

            // SteamLayer.Instance is a static field in the current game build,
            // not a Unity component/property. Do not pass SteamLayer to
            // FindObjectOfType unless a future build actually makes it a Unity
            // object; Unity logs an error for ordinary game classes.
            if (steamLayer == null)
            {
                var instanceField = steamLayerType.GetField(
                    "Instance",
                    BindingFlags.Public |
                    BindingFlags.NonPublic |
                    BindingFlags.Static |
                    BindingFlags.FlattenHierarchy);
                steamLayer = instanceField == null
                    ? null
                    : instanceField.GetValue(null);
            }

            if (steamLayer == null)
            {
                if (typeof(UnityEngine.Object).IsAssignableFrom(steamLayerType))
                {
                    steamLayer = UnityEngine.Object.FindObjectOfType(steamLayerType);
                }
            }
            if (steamLayer == null)
            {
                return null;
            }

            var lobbyProperty = steamLayerType.GetProperty(
                "LobbySteamId",
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static);
            var lobbyGetter = lobbyProperty == null ? null : lobbyProperty.GetGetMethod(true);
            var lobbyId = lobbyGetter == null
                ? null
                : lobbyProperty.GetValue(lobbyGetter.IsStatic ? null : steamLayer, null);
            if (lobbyId == null || !IsValidSteamId(lobbyId))
            {
                var lobbyField = steamLayerType.GetField(
                    "_lobbySteamID",
                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static);
                lobbyId = lobbyField == null
                    ? null
                    : lobbyField.GetValue(lobbyField.IsStatic ? null : steamLayer);
            }
            if (lobbyId == null)
            {
                return null;
            }

            return IsValidSteamId(lobbyId) ? lobbyId : null;
        }

        private static bool IsValidSteamId(object steamId)
        {
            if (steamId == null)
            {
                return false;
            }

            var isValid = steamId.GetType().GetMethod(
                "IsValid",
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            if (isValid == null)
            {
                return true;
            }

            return Convert.ToBoolean(isValid.Invoke(steamId, null));
        }

        private static MethodInfo FindSteamMatchmakingMethod(Type type, string name, int parameterCount)
        {
            if (type == null)
            {
                return null;
            }

            var methods = type.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
            foreach (var method in methods)
            {
                if (method.Name == name && method.GetParameters().Length == parameterCount)
                {
                    return method;
                }
            }

            return null;
        }

        private static RoomInfo GetCurrentRoom()
        {
            try
            {
                var connectType = AccessTools.TypeByName("Connect");
                var layerGetter = connectType == null
                    ? null
                    : AccessTools.PropertyGetter(connectType, "Layer");
                var layer = layerGetter == null ? null : layerGetter.Invoke(null, null);
                var connectionType = AccessTools.TypeByName("ConnectionLayer");
                var roomGetter = connectionType == null
                    ? null
                    : AccessTools.PropertyGetter(connectionType, "Room");
                return roomGetter == null ? null : roomGetter.Invoke(layer, null) as RoomInfo;
            }
            catch
            {
                return null;
            }
        }

        private static IEnumerable<CodeInstruction> RequestJoinGameTranspiler(
            IEnumerable<CodeInstruction> instructions)
        {
            var result = new List<CodeInstruction>(instructions);
            var levelFinishedGetter = AccessTools.PropertyGetter(
                AccessTools.TypeByName("GameModeController"),
                "LevelFinished");
            var controllerRegistrationGuard = FindRequestJoinGameControllerGuard();
            var bypassGetter = typeof(HarmonyDiagnostics).GetMethod(
                "ShouldAllowRequestJoinGame",
                BindingFlags.NonPublic | BindingFlags.Static);
            var controllerGuardBypass = typeof(HarmonyDiagnostics).GetMethod(
                "ShouldAllowRequestJoinGameController",
                BindingFlags.NonPublic | BindingFlags.Static);
            if (levelFinishedGetter == null || bypassGetter == null ||
                controllerRegistrationGuard == null || controllerGuardBypass == null)
            {
                DiagnosticLog.Warning(
                    "Late workshop join patch could not resolve HeroController.RequestJoinGame guard methods.");
                return result;
            }
            var replacedLevelFinished = false;
            var replacedControllerGuard = false;

            for (var index = 0; index < result.Count; index++)
            {
                var method = result[index].operand as MethodInfo;
                if (method == levelFinishedGetter)
                {
                    result[index].operand = bypassGetter;
                    replacedLevelFinished = true;
                }
                else if (method == controllerRegistrationGuard)
                {
                    result[index].operand = controllerGuardBypass;
                    replacedControllerGuard = true;
                }
            }

            if (!replacedLevelFinished)
            {
                DiagnosticLog.Warning(
                    "Late workshop join patch could not find HeroController.RequestJoinGame level-finished guard.");
            }
            if (!replacedControllerGuard)
            {
                DiagnosticLog.Warning(
                    "Late workshop join patch could not find HeroController.RequestJoinGame controller-registration guard.");
            }
            if (replacedLevelFinished && replacedControllerGuard)
            {
                DiagnosticLog.Info(
                    "Late workshop join patch enabled for HeroController.RequestJoinGame " +
                    "level-finished and controller-registration guards.");
            }

            return result;
        }

        private static MethodInfo FindRequestJoinGameControllerGuard()
        {
            var heroControllerType = AccessTools.TypeByName("HeroController");
            if (heroControllerType == null)
            {
                return null;
            }

            var methods = heroControllerType.GetMethods(
                BindingFlags.Public |
                BindingFlags.NonPublic |
                BindingFlags.Static |
                BindingFlags.Instance);
            foreach (var method in methods)
            {
                if ((method.Name == "IsControIdRegisteredToPID" ||
                     method.Name == "IsControllerIdRegisteredToPID" ||
                     method.Name == "IsControllerIDRegisteredToPID") &&
                    method.GetParameters().Length == 2)
                {
                    return method;
                }
            }

            return null;
        }

        private static bool ShouldAllowRequestJoinGameController(int controllerNum, object requesteeID)
        {
            if (IsLateWorkshopHostSession())
            {
                DiagnosticLog.Trace(
                    "Late workshop host bypassed the RequestJoinGame controller-registration return: " +
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

        private static object GetCurrentConnectionLayer()
        {
            try
            {
                var connectType = AccessTools.TypeByName("Connect");
                var layerGetter = connectType == null
                    ? null
                    : AccessTools.PropertyGetter(connectType, "Layer");
                return layerGetter == null ? null : layerGetter.Invoke(null, null);
            }
            catch
            {
                return null;
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

        private static void PrepareLateWorkshopJoinSlot()
        {
            if (!IsLateWorkshopHostSession())
            {
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

            QueueWorkshopSpawnRebroadcast(
                "a late Workshop player registered: player=" + assignedPlayerNumber,
                750,
                true);
        }

        private static void TryRebroadcastWorkshopSpawns()
        {
            if (!_workshopSpawnRebroadcastPending ||
                DateTime.UtcNow < _workshopSpawnRebroadcastAtUtc)
            {
                return;
            }

            _workshopSpawnRebroadcastPending = false;
            var useCurrentPositions = _workshopSpawnRebroadcastUseCurrentPositions;
            _workshopSpawnRebroadcastUseCurrentPositions = false;
            if (!IsWorkshopOnlineSession() || HeroController.players == null)
            {
                return;
            }

            var rebroadcastCount = 0;
            var rebroadcastPositions = new StringBuilder();
            for (var index = 0; index < HeroController.players.Length; index++)
            {
                var player = HeroController.players[index];
                DeferredSpawnPosition position;
                if (player == null || player.character == null || !player.IsMine ||
                    !LocalWorkshopSpawnPositions.TryGetValue(index, out position))
                {
                    continue;
                }

                try
                {
                    var spawnType = position.SpawnType;
                    var spawnPosition = position.Position;
                    if (useCurrentPositions)
                    {
                        spawnPosition = player.character.transform.position;
                    }

                    if (rebroadcastPositions.Length > 0)
                    {
                        rebroadcastPositions.Append(",");
                    }

                    rebroadcastPositions.Append(index);
                    rebroadcastPositions.Append("=");
                    rebroadcastPositions.Append(FormatVector3(spawnPosition));

                    Networking.Networking.RPC<
                        TestVanDammeAnim,
                        Player.SpawnType,
                        bool,
                        Vector3>(
                        PID.TargetOthers,
                        new RpcSignature<
                            TestVanDammeAnim,
                            Player.SpawnType,
                            bool,
                            Vector3>(player.SetSpawnPositon),
                        player.character,
                        spawnType,
                        position.SpawnViaAirDrop,
                        spawnPosition,
                        false);
                    rebroadcastCount++;
                }
                catch (Exception exception)
                {
                    DiagnosticLog.Warning(
                    "Workshop spawn-position rebroadcast failed for local player=" +
                        index + ": " + exception);
                }
            }

            DiagnosticLog.Info(
                "Workshop spawn-position rebroadcast completed with authoritative current positions: localPlayers=" +
                rebroadcastCount + "; positions=" + rebroadcastPositions + ".");
        }

        private static void QueueWorkshopSpawnRebroadcast(string reason, int delayMilliseconds)
        {
            QueueWorkshopSpawnRebroadcast(reason, delayMilliseconds, false);
        }

        private static void QueueWorkshopSpawnRebroadcast(
            string reason,
            int delayMilliseconds,
            bool useCurrentPosition)
        {
            if (!IsWorkshopOnlineSession())
            {
                return;
            }

            _workshopSpawnRebroadcastPending = true;
            _workshopSpawnRebroadcastAtUtc = DateTime.UtcNow.AddMilliseconds(delayMilliseconds);
            _workshopSpawnRebroadcastUseCurrentPositions =
                _workshopSpawnRebroadcastUseCurrentPositions || useCurrentPosition;
            DiagnosticLog.Trace(
                "Scheduled exact Workshop spawn-position rebroadcast: " + reason + ".");
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

        private static bool[] GetPlayersPlayingArray()
        {
            try
            {
                var field = typeof(HeroController).GetField(
                    "playersPlaying",
                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
                return field == null ? null : field.GetValue(null) as bool[];
            }
            catch (Exception exception)
            {
                DiagnosticLog.Warning(
                    "Reading HeroController.playersPlaying failed: " + exception);
                return null;
            }
        }

        private static object GetGameStateInstance(Type gameStateType)
        {
            if (gameStateType == null)
            {
                return null;
            }

            var instanceProperty = gameStateType.GetProperty(
                "Instance",
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
            return instanceProperty == null ? null : instanceProperty.GetValue(null, null);
        }

        private static string GetRoomInfoString(RoomInfo room, string fieldName)
        {
            var field = typeof(RoomInfo).GetField(
                fieldName,
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            if (field != null)
            {
                var value = field.GetValue(room) as string;
                return value == null ? string.Empty : value.Trim();
            }

            var property = typeof(RoomInfo).GetProperty(
                fieldName,
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            if (property != null && property.CanRead)
            {
                var value = property.GetValue(room, null) as string;
                return value == null ? string.Empty : value.Trim();
            }

            return string.Empty;
        }

        private static int GetRoomInfoInt(RoomInfo room, string fieldName, int fallback)
        {
            var field = typeof(RoomInfo).GetField(
                fieldName,
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            if (field != null)
            {
                try
                {
                    return Convert.ToInt32(field.GetValue(room));
                }
                catch
                {
                    return fallback;
                }
            }

            return fallback;
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

        private static void ClearWorkshopOnlineLobbyReturnState()
        {
            _returnToWorkshopOnlineLobbyPending = false;
            _returnToWorkshopOnlineLobbyAttempted = false;
            _returnToWorkshopOnlineLobbyAtUtc = DateTime.MinValue;
            _returnToWorkshopOnlineLobbyVisualsSuppressed = false;
            _returnToWorkshopOnlineLobbyNavigationStartedAtUtc = DateTime.MinValue;
        }

        private static void PatchSwitchLevelTranspiler()
        {
            var type = AccessTools.TypeByName("GameModeController");
            if (type == null)
            {
                DiagnosticLog.Warning("Workshop injection target type not found: GameModeController");
                return;
            }

            var method = type.GetMethod(
                "SwitchLevel",
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static);
            if (method == null)
            {
                DiagnosticLog.Warning("Workshop injection target method not found: GameModeController.SwitchLevel");
                return;
            }

            var transpilerMethod = typeof(HarmonyDiagnostics).GetMethod(
                "SwitchLevelTranspiler",
                BindingFlags.NonPublic | BindingFlags.Static);
            try
            {
                _harmony.Patch(method, null, null, new HarmonyMethod(transpilerMethod), null);
            }
            catch (Exception exception)
            {
                DiagnosticLog.Warning("Workshop injection transpiler failed: " + exception);
            }
        }

        private static bool _workshopCompletionSubscribed;

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

        private static void SetStaticFieldOrProperty(Type type, string name, object value)
        {
            if (type == null)
            {
                throw new MissingMemberException(name);
            }

            var field = type.GetField(
                name,
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
            if (field != null)
            {
                field.SetValue(null, value);
                return;
            }

            var property = type.GetProperty(
                name,
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
            if (property != null && property.CanWrite)
            {
                property.SetValue(null, value, null);
                return;
            }

            throw new MissingMemberException(type.FullName, name);
        }

        private static void PatchWorldMapEnterMissionTranspiler()
        {
            var type = AccessTools.TypeByName("WorldMapController");
            if (type == null)
            {
                DiagnosticLog.Warning("Workshop injection target type not found: WorldMapController");
                return;
            }

            var method = type.GetMethod(
                "EnterMission",
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static);
            if (method == null)
            {
                DiagnosticLog.Warning("Workshop injection target method not found: WorldMapController.EnterMission");
                return;
            }

            var transpilerMethod = typeof(HarmonyDiagnostics).GetMethod(
                "EnterMissionTranspiler",
                BindingFlags.NonPublic | BindingFlags.Static);
            try
            {
                _harmony.Patch(method, null, null, new HarmonyMethod(transpilerMethod), null);
                DiagnosticLog.Info("Workshop injection patch enabled for WorldMapController.EnterMission.");
            }
            catch (Exception exception)
            {
                DiagnosticLog.Warning("Workshop injection transpiler failed for WorldMapController.EnterMission: " + exception);
            }
        }

        private static void PatchGameStateLoadLevelPrefix()
        {
            var type = AccessTools.TypeByName("GameState");
            if (type == null)
            {
                DiagnosticLog.Warning("Workshop injection target type not found: GameState");
                return;
            }

            var method = type.GetMethod(
                "LoadLevel",
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static);
            if (method == null)
            {
                DiagnosticLog.Warning("Workshop injection target method not found: GameState.LoadLevel");
                return;
            }

            var prefixMethod = typeof(HarmonyDiagnostics).GetMethod(
                "GameStateLoadLevelPrefix",
                BindingFlags.NonPublic | BindingFlags.Static);
            try
            {
                _harmony.Patch(method, new HarmonyMethod(prefixMethod), null, null, null);
                DiagnosticLog.Info("Workshop injection prefix enabled for GameState.LoadLevel.");
            }
            catch (Exception exception)
            {
                DiagnosticLog.Warning("Workshop injection prefix failed for GameState.LoadLevel: " + exception);
            }
        }

        private static bool GameStateLoadLevelPrefix(string nextScene)
        {
            try
            {
                PrepareWorkshopOnlineLobbyMainMenuLoad(nextScene);

                if (_skipDuplicateWorkshopSceneLoad &&
                    DateTime.UtcNow <= _skipDuplicateWorkshopSceneLoadUntilUtc &&
                    !string.IsNullOrEmpty(nextScene) &&
                    string.Equals(nextScene, GetConfiguredWorkshopSceneName(), StringComparison.Ordinal))
                {
                    ClearDuplicateWorkshopLoadSuppression();
                    DiagnosticLog.Info(
                        "Skipped duplicate GameState.LoadLevel for workshop scene after completion callback.");
                    return false;
                }

                if (_skipDuplicateWorkshopSceneLoad &&
                    DateTime.UtcNow > _skipDuplicateWorkshopSceneLoadUntilUtc)
                {
                    ClearDuplicateWorkshopLoadSuppression();
                }

                var activeSceneName = SceneManager.GetActiveScene().name;
                if (string.IsNullOrEmpty(activeSceneName) ||
                    activeSceneName.IndexOf("MissionScreen", StringComparison.OrdinalIgnoreCase) < 0)
                {
                    return true;
                }

                ApplyWorkshopState(false, "before GameState.LoadLevel from mission screen");
            }
            catch (Exception exception)
            {
                DiagnosticLog.Warning("Workshop load-level injection failed: " + exception);
            }

            return true;
        }

        private static void PrepareWorkshopOnlineLobbyMainMenuLoad(string nextScene)
        {
            if (!_returnToWorkshopOnlineLobbyPending ||
                !string.Equals(nextScene, "MainMenu", StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            var gameStateType = AccessTools.TypeByName("GameState");
            var state = GetGameStateInstance(gameStateType);
            if (state == null)
            {
                DiagnosticLog.Warning(
                    "Workshop Esc return could not clear the custom-campaign menu redirect because " +
                    "GameState.Instance is null.");
                return;
            }

            SetFieldOrProperty(state, "immediatelyGoToCustomCampaign", false);
            DiagnosticLog.Info(
                "Workshop Esc return cleared GameState.immediatelyGoToCustomCampaign before MainMenu load.");
        }

        private static void PatchLateHeroResponseGuard()
        {
            var type = AccessTools.TypeByName("HeroController");
            var method = type == null
                ? null
                : type.GetMethod(
                    "RecieveHeroTypeFromMaster",
                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static);
            if (method == null)
            {
                DiagnosticLog.Warning("Late hero-response guard target not found.");
                return;
            }

            var prefixMethod = typeof(HarmonyDiagnostics).GetMethod(
                "RecieveHeroTypeFromMasterPrefix",
                BindingFlags.NonPublic | BindingFlags.Static);
            try
            {
                _harmony.Patch(method, new HarmonyMethod(prefixMethod), null, null, null);
                DiagnosticLog.Info("Late hero-response guard enabled.");
            }
            catch (Exception exception)
            {
                DiagnosticLog.Warning("Late hero-response guard patch failed: " + exception);
            }
        }

        private static bool RecieveHeroTypeFromMasterPrefix(object[] __args)
        {
            NormalizeWorkshopHeroResponsePlayerNumber(__args);
            PreserveWorkshopHeroTypePrefix(null, __args);
            var playerNum = FindPlayerNumberArgument(__args);
            if (!Plugin.ShouldSkipLateHeroResponse(playerNum))
            {
                return true;
            }

            DiagnosticLog.Warning(
                "Skipped a late hero-type response after local fallback for player " + playerNum + ".");
            return false;
        }

        private static void NormalizeWorkshopHeroResponsePlayerNumber(object[] arguments)
        {
            if (!IsWorkshopOnlineSession() || arguments == null || arguments.Length < 2)
            {
                return;
            }

            var responsePlayerNum = arguments[1] is int ? (int)arguments[1] : -1;
            var pendingPlayerNum = FindPendingLocalHeroResponsePlayer();
            if (responsePlayerNum >= 0 && responsePlayerNum < 4 &&
                HeroController.players != null && responsePlayerNum < HeroController.players.Length &&
                HeroController.players[responsePlayerNum] != null &&
                (HeroController.players[responsePlayerNum].IsMine || pendingPlayerNum < 0 ||
                 pendingPlayerNum == responsePlayerNum))
            {
                return;
            }

            if (pendingPlayerNum < 0)
            {
                return;
            }

            arguments[1] = pendingPlayerNum;
            DiagnosticLog.Warning(
                "Normalized malformed Workshop hero response player number: received=" +
                responsePlayerNum + "; using local pending player=" + pendingPlayerNum + ".");
        }

        private static int FindPendingLocalHeroResponsePlayer()
        {
            if (HeroController.players == null || HeroController.PIDS == null)
            {
                return -1;
            }

            var count = System.Math.Min(4, System.Math.Min(
                HeroController.players.Length,
                HeroController.PIDS.Length));
            var dropoutPlayerNum = -1;
            var pendingPlayerNum = -1;
            for (var index = 0; index < count; index++)
            {
                var player = HeroController.players[index];
                var pid = HeroController.PIDS[index];
                if (player == null || pid == null || !pid.IsMine || player.character != null ||
                    !player.awaitingHeroTypeFromServer)
                {
                    continue;
                }

                if (WorkshopDropoutHeroTypes.ContainsKey(index))
                {
                    dropoutPlayerNum = index;
                    break;
                }

                if (pendingPlayerNum < 0)
                {
                    pendingPlayerNum = index;
                }
                else
                {
                    // Do not redirect an ambiguous response to an arbitrary slot.
                    return -1;
                }
            }

            return dropoutPlayerNum >= 0 ? dropoutPlayerNum : pendingPlayerNum;
        }

        private static void PatchWorkshopHeroTypePreservation()
        {
            var heroControllerType = AccessTools.TypeByName("HeroController");
            var requestMethod = heroControllerType == null
                ? null
                : heroControllerType.GetMethod(
                    "RequestHeroTypeFromMasterRPC",
                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static);
            if (requestMethod != null)
            {
                try
                {
                    var prefix = typeof(HarmonyDiagnostics).GetMethod(
                        "PreserveWorkshopHeroTypePrefix",
                        BindingFlags.NonPublic | BindingFlags.Static);
                    _harmony.Patch(requestMethod, new HarmonyMethod(prefix), null, null, null);
                    DiagnosticLog.Info("Workshop dropout hero-type preservation enabled for the master request path.");
                }
                catch (Exception exception)
                {
                    DiagnosticLog.Warning(
                        "Workshop dropout hero-type preservation patch failed for the master request path: " +
                        exception);
                }
            }
            else
            {
                DiagnosticLog.Warning(
                    "Workshop dropout hero-type preservation target not found: " +
                    "HeroController.RequestHeroTypeFromMasterRPC.");
            }

            var playerType = AccessTools.TypeByName("Player");
            if (playerType == null)
            {
                return;
            }

            var spawnPrefix = typeof(HarmonyDiagnostics).GetMethod(
                "PreserveWorkshopHeroTypePrefix",
                BindingFlags.NonPublic | BindingFlags.Static);
            var matched = false;
            foreach (var method in playerType.GetMethods(
                         BindingFlags.Public | BindingFlags.NonPublic |
                         BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly))
            {
                if (method.Name != "SpawnHero" || method.ContainsGenericParameters || method.IsAbstract)
                {
                    continue;
                }

                matched = true;
                try
                {
                    _harmony.Patch(method, new HarmonyMethod(spawnPrefix), null, null, null);
                }
                catch (Exception exception)
                {
                    DiagnosticLog.Warning(
                        "Workshop dropout hero-type preservation patch failed for " +
                        DescribeMethod(method) + ": " + exception);
                }
            }

            if (!matched)
            {
                DiagnosticLog.Warning("Workshop dropout hero-type preservation target not found: Player.SpawnHero.");
            }
        }

        private static void PreserveWorkshopHeroTypePrefix(object __instance, object[] __args)
        {
            if (!IsWorkshopOnlineSession() || __args == null)
            {
                return;
            }

            var playerNum = __instance is Player
                ? ((Player)__instance).playerNum
                : FindPlayerNumberArgument(__args);
            HeroType savedHeroType;
            if (!TryGetWorkshopRejoinHeroType(playerNum, out savedHeroType))
            {
                return;
            }

            for (var index = 0; index < __args.Length; index++)
            {
                HeroType argumentHeroType;
                if (!TryConvertHeroType(__args[index], out argumentHeroType))
                {
                    continue;
                }

                if (argumentHeroType == savedHeroType)
                {
                    return;
                }

                __args[index] = savedHeroType;
                DiagnosticLog.Warning(
                    "Restored saved Workshop hero type during dropout rejoin: player=" +
                    playerNum + "; requested=" + argumentHeroType +
                    "; restored=" + savedHeroType + ".");
                return;
            }
        }

        private static int FindPlayerNumberArgument(object[] arguments)
        {
            if (arguments == null)
            {
                return -1;
            }

            foreach (var argument in arguments)
            {
                if (argument is int)
                {
                    var value = (int)argument;
                    if (value >= 0 && value < 4)
                    {
                        return value;
                    }
                }
            }

            return -1;
        }

        private static void PatchWorkshopJoinPromptSuppression()
        {
            var type = AccessTools.TypeByName("LevelTitle");
            var method = type == null
                ? null
                : type.GetMethod(
                    "ShowText",
                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static,
                    null,
                    new[] { typeof(string), typeof(float), typeof(bool) },
                    null);
            if (method == null)
            {
                DiagnosticLog.Warning("Workshop join-prompt suppression target not found.");
                return;
            }

            var prefixMethod = typeof(HarmonyDiagnostics).GetMethod(
                "LevelTitleShowTextPrefix",
                BindingFlags.NonPublic | BindingFlags.Static);
            try
            {
                _harmony.Patch(method, new HarmonyMethod(prefixMethod), null, null, null);
                DiagnosticLog.Info("Workshop join-prompt suppression enabled.");
            }
            catch (Exception exception)
            {
                DiagnosticLog.Warning("Workshop join-prompt suppression patch failed: " + exception);
            }
        }

        private static void PatchMainMenuInitializationPostfix()
        {
            var type = AccessTools.TypeByName("MainMenu");
            var method = type == null
                ? null
                : type.GetMethod(
                    "InitializeMenu",
                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance,
                    null,
                    Type.EmptyTypes,
                    null);
            if (method == null)
            {
                DiagnosticLog.Warning("Workshop lobby return target not found: MainMenu.InitializeMenu.");
                return;
            }

            var postfixMethod = typeof(HarmonyDiagnostics).GetMethod(
                "MainMenuInitializeMenuPostfix",
                BindingFlags.NonPublic | BindingFlags.Static);
            try
            {
                _harmony.Patch(method, null, new HarmonyMethod(postfixMethod), null, null);
                DiagnosticLog.Info("Workshop lobby return postfix enabled for MainMenu.InitializeMenu.");
            }
            catch (Exception exception)
            {
                DiagnosticLog.Warning(
                    "Workshop lobby return postfix failed for MainMenu.InitializeMenu: " + exception);
            }
        }

        private static void PatchMainMenuInitializationDelay()
        {
            var mainMenuType = AccessTools.TypeByName("MainMenu");
            if (mainMenuType == null)
            {
                DiagnosticLog.Warning("Workshop lobby return delay target type not found: MainMenu.");
                return;
            }

            MethodInfo moveNext = null;
            var nestedTypes = mainMenuType.GetNestedTypes(
                BindingFlags.Public | BindingFlags.NonPublic);
            foreach (var nestedType in nestedTypes)
            {
                if (nestedType.Name.IndexOf("DelayInitializeMenu", StringComparison.Ordinal) < 0)
                {
                    continue;
                }

                moveNext = nestedType.GetMethod(
                    "MoveNext",
                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                if (moveNext != null)
                {
                    break;
                }
            }

            if (moveNext == null)
            {
                DiagnosticLog.Warning(
                    "Workshop lobby return delay target method not found: MainMenu.DelayInitializeMenu.MoveNext.");
                return;
            }

            var transpilerMethod = typeof(HarmonyDiagnostics).GetMethod(
                "MainMenuInitializationDelayTranspiler",
                BindingFlags.NonPublic | BindingFlags.Static);
            try
            {
                _harmony.Patch(moveNext, null, null, new HarmonyMethod(transpilerMethod), null);
                DiagnosticLog.Info(
                    "Workshop lobby return delay patch enabled; pending returns use a zero-second menu initialization delay.");
            }
            catch (Exception exception)
            {
                DiagnosticLog.Warning(
                    "Workshop lobby return delay patch failed: " + exception);
            }
        }

        private static IEnumerable<CodeInstruction> MainMenuInitializationDelayTranspiler(
            IEnumerable<CodeInstruction> instructions)
        {
            var result = new List<CodeInstruction>(instructions);
            var getter = typeof(HarmonyDiagnostics).GetMethod(
                "GetMainMenuInitializationDelay",
                BindingFlags.NonPublic | BindingFlags.Static);
            var replaced = false;

            for (var index = 0; index < result.Count; index++)
            {
                var instruction = result[index];
                var operandValue = instruction.operand is float
                    ? (float)instruction.operand
                    : 0f;
                if (!replaced && instruction.opcode == OpCodes.Ldc_R4 &&
                    instruction.operand is float && operandValue > 2.999f && operandValue < 3.001f)
                {
                    result[index] = new CodeInstruction(OpCodes.Call, getter);
                    replaced = true;
                }
            }

            if (!replaced)
            {
                DiagnosticLog.Warning(
                    "Workshop lobby return delay patch found no 3-second WaitForSeconds constant.");
            }

            return result;
        }

        private static float GetMainMenuInitializationDelay()
        {
            return _returnToWorkshopOnlineLobbyPending ? 0f : 3f;
        }

        private static void PatchLobbyMainMenuReturnPostfix()
        {
            var lobbyType = AccessTools.TypeByName("Lobby");
            var method = lobbyType == null
                ? null
                : lobbyType.GetMethod(
                    "GoBackToMainMenu",
                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance,
                    null,
                    Type.EmptyTypes,
                    null);
            if (method == null)
            {
                DiagnosticLog.Warning("MainMenu return layout target not found: Lobby.GoBackToMainMenu.");
                return;
            }

            var postfixMethod = typeof(HarmonyDiagnostics).GetMethod(
                "LobbyGoBackToMainMenuPrefix",
                BindingFlags.NonPublic | BindingFlags.Static);
            try
            {
                _harmony.Patch(method, new HarmonyMethod(postfixMethod), null, null, null);
                DiagnosticLog.Info(
                    "MainMenu return layout prefix enabled for Lobby.GoBackToMainMenu.");
            }
            catch (Exception exception)
            {
                DiagnosticLog.Warning(
                    "MainMenu return layout prefix failed for Lobby.GoBackToMainMenu: " + exception);
            }
        }

        private static void LobbyGoBackToMainMenuPrefix()
        {
            try
            {
                var mainMenu = GetMainMenuInstance(AccessTools.TypeByName("MainMenu")) as MainMenu;
                if (mainMenu == null || !GetBoolFieldOrProperty(mainMenu, "hasInitialized"))
                {
                    return;
                }

                // Prepare the regular spacing before native MainMenu.Show() starts its
                // entrance coroutine, so the animation keeps its original timing.
                _restoreMainMenuAfterLobbyReturnPending = true;
                SetMenuHighlightIndex(0);
                InvokeMenuLayoutMethod(mainMenu, "RearragneSpacing");
                SetMainMenuItemsActive(mainMenu, false);
                SetMainMenuChromeActive(mainMenu, false);
                SetMainMenuItemRenderersActive(mainMenu, false);
                DiagnosticLog.Info(
                    "Prepared the regular MainMenu layout before native Lobby.GoBackToMainMenu.Show().");
            }
            catch (Exception exception)
            {
                DiagnosticLog.Warning(
                    "MainMenu layout restoration after Lobby.GoBackToMainMenu could not be queued: " + exception);
            }
        }

        private static void PatchMainMenuShowRoutineCompletion()
        {
            var mainMenuType = AccessTools.TypeByName("MainMenu");
            if (mainMenuType == null)
            {
                DiagnosticLog.Warning("MainMenu.ShowRoutine completion target type not found.");
                return;
            }

            MethodInfo moveNext = null;
            foreach (var nestedType in mainMenuType.GetNestedTypes(
                BindingFlags.Public | BindingFlags.NonPublic))
            {
                if (nestedType.Name.IndexOf("ShowRoutine", StringComparison.Ordinal) < 0)
                {
                    continue;
                }

                moveNext = nestedType.GetMethod(
                    "MoveNext",
                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                if (moveNext != null)
                {
                    break;
                }
            }

            if (moveNext == null)
            {
                DiagnosticLog.Warning(
                    "MainMenu.ShowRoutine completion target method not found.");
                return;
            }

            var postfixMethod = typeof(HarmonyDiagnostics).GetMethod(
                "MainMenuShowRoutineMoveNextPostfix",
                BindingFlags.NonPublic | BindingFlags.Static);
            try
            {
                _harmony.Patch(moveNext, null, new HarmonyMethod(postfixMethod), null, null);
                DiagnosticLog.Info(
                    "MainMenu.ShowRoutine completion patch enabled for post-return layout restoration.");
            }
            catch (Exception exception)
            {
                DiagnosticLog.Warning(
                    "MainMenu.ShowRoutine completion patch failed: " + exception);
            }
        }

        private static void PatchMainMenuMenuActiveSetter()
        {
            var property = typeof(Menu).GetProperty(
                "MenuActive",
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            var setter = property == null ? null : property.GetSetMethod(true);
            if (setter == null)
            {
                DiagnosticLog.Warning("MainMenu MenuActive setter target not found.");
                return;
            }

            var postfixMethod = typeof(HarmonyDiagnostics).GetMethod(
                "MainMenuMenuActiveSetterPostfix",
                BindingFlags.NonPublic | BindingFlags.Static);
            try
            {
                _harmony.Patch(setter, null, new HarmonyMethod(postfixMethod), null, null);
                DiagnosticLog.Info(
                    "MainMenu MenuActive setter patch enabled for return-animation visual gating.");
            }
            catch (Exception exception)
            {
                DiagnosticLog.Warning(
                    "MainMenu MenuActive setter patch failed: " + exception);
            }
        }

        private static void MainMenuMenuActiveSetterPostfix(Menu __instance, bool value)
        {
            if (!_restoreMainMenuAfterLobbyReturnPending || !value ||
                !(__instance is MainMenu))
            {
                return;
            }

            SetMainMenuItemsActive(__instance as MainMenu, false);
            SetMainMenuChromeActive(__instance as MainMenu, false);
            SetMainMenuItemRenderersActive(__instance as MainMenu, false);
        }

        private static void MainMenuShowRoutineMoveNextPostfix(bool __result)
        {
            if (!_restoreMainMenuAfterLobbyReturnPending)
            {
                return;
            }

            var mainMenu = GetMainMenuInstance(AccessTools.TypeByName("MainMenu")) as MainMenu;
            if (__result)
            {
                SetMainMenuItemsActive(mainMenu, false);
                SetMainMenuChromeActive(mainMenu, false);
                SetMainMenuItemRenderersActive(mainMenu, false);
                return;
            }

            _restoreMainMenuAfterLobbyReturnPending = false;
            try
            {
                if (mainMenu == null || !GetBoolFieldOrProperty(mainMenu, "hasInitialized"))
                {
                    return;
                }

                mainMenu.MenuActive = true;
                if (mainMenu.logo != null)
                {
                    mainMenu.logo.SetActive(true);
                }

                SetMainMenuItemsActive(mainMenu, true);
                SetMainMenuChromeActive(mainMenu, true);
                SetMainMenuItemRenderersActive(mainMenu, true);
                InputReader.IsBlocked = false;
                DiagnosticLog.Info(
                    "Native MainMenu.ShowRoutine completed after Lobby.GoBackToMainMenu.");
            }
            catch (Exception exception)
            {
                DiagnosticLog.Warning(
                    "MainMenu layout restoration after ShowRoutine completion failed: " + exception);
            }
        }

        private static void SetMainMenuItemsActive(MainMenu mainMenu, bool active)
        {
            if (mainMenu == null)
            {
                return;
            }

            var field = typeof(Menu).GetField(
                "items",
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            var value = field == null ? null : field.GetValue(mainMenu) as Array;
            if (value == null)
            {
                return;
            }

            foreach (var item in value)
            {
                var component = item as Component;
                if (component != null)
                {
                    component.gameObject.SetActive(active);
                }
            }
        }

        private static void SetMainMenuChromeActive(MainMenu mainMenu, bool active)
        {
            if (mainMenu == null)
            {
                return;
            }

            var highlightField = typeof(Menu).GetField(
                "menuHighlight",
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            var highlight = highlightField == null
                ? null
                : highlightField.GetValue(mainMenu) as Component;
            if (highlight != null)
            {
                highlight.gameObject.SetActive(active);
            }

            var holderField = typeof(Menu).GetField(
                "menuHolder",
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            var holder = holderField == null
                ? null
                : holderField.GetValue(mainMenu) as GameObject;
            if (holder != null)
            {
                holder.SetActive(active);
            }
        }

        private static void SetMainMenuItemRenderersActive(MainMenu mainMenu, bool active)
        {
            if (mainMenu == null)
            {
                return;
            }

            var field = typeof(Menu).GetField(
                "items",
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            var value = field == null ? null : field.GetValue(mainMenu) as Array;
            if (value == null)
            {
                return;
            }

            foreach (var item in value)
            {
                var component = item as Component;
                if (component == null)
                {
                    continue;
                }

                var renderers = component.GetComponentsInChildren<Renderer>(true);
                foreach (var renderer in renderers)
                {
                    if (renderer == null)
                    {
                        continue;
                    }

                    if (!active)
                    {
                        if (!MainMenuRendererStates.ContainsKey(renderer))
                        {
                            MainMenuRendererStates.Add(renderer, renderer.enabled);
                        }

                        renderer.enabled = false;
                    }
                    else
                    {
                        bool originalState;
                        if (MainMenuRendererStates.TryGetValue(renderer, out originalState))
                        {
                            renderer.enabled = originalState;
                        }
                    }
                }
            }

            if (active)
            {
                MainMenuRendererStates.Clear();
            }
        }

        private static void InvokeMenuLayoutMethod(MainMenu mainMenu, string methodName)
        {
            if (mainMenu == null || string.IsNullOrEmpty(methodName))
            {
                return;
            }

            var method = typeof(Menu).GetMethod(
                methodName,
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance,
                null,
                Type.EmptyTypes,
                null);
            if (method == null)
            {
                DiagnosticLog.Warning("MainMenu layout method not found: Menu." + methodName + ".");
                return;
            }

            method.Invoke(mainMenu, null);
        }

        private static void SetMenuHighlightIndex(int index)
        {
            var field = typeof(Menu).GetField(
                "highlightIndex",
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            if (field == null)
            {
                DiagnosticLog.Warning("MainMenu highlight index field not found.");
                return;
            }

            var mainMenu = GetMainMenuInstance(AccessTools.TypeByName("MainMenu"));
            if (mainMenu != null)
            {
                field.SetValue(mainMenu, index);
            }
        }

        private static void MainMenuInitializeMenuPostfix(object __instance)
        {
            if (!_returnToWorkshopOnlineLobbyPending ||
                _returnToWorkshopOnlineLobbyAttempted ||
                !string.Equals(SceneManager.GetActiveScene().name, "MainMenu", StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            SuppressWorkshopOnlineLobbyMainMenuVisuals(__instance);
            TryReturnToWorkshopOnlineLobby();
        }

        private static void ObserveWorkshopOnlineLobbyReturnBeforeTrace(
            MethodBase method,
            object[] arguments)
        {
            if (method == null || method.DeclaringType == null ||
                method.DeclaringType.Name != "GameModeController" ||
                method.Name != "LoadNextScene" ||
                !IsWorkshopOnlineSession() ||
                !IsEscReturnPauseState() ||
                !IsWorkshopVictoryGameState(arguments))
            {
                return;
            }

            ArmWorkshopOnlineLobbyReturn("GameModeController.LoadNextScene");
            var state = arguments[0];
            SetFieldOrProperty(state, "sceneToLoad", "MainMenu");
            SetFieldOrProperty(state, "loadCustomCampaign", false);
            SetFieldOrProperty(state, "immediatelyGoToCustomCampaign", false);
            DiagnosticLog.Info(
                "Workshop Esc return redirected VictoryCustomCampaignSteam directly to MainMenu; " +
                "the completion-time and rating screens will be skipped.");
        }

        private static bool IsWorkshopVictoryGameState(object[] arguments)
        {
            if (arguments == null || arguments.Length == 0 || arguments[0] == null)
            {
                return false;
            }

            var state = arguments[0];
            var sceneName = GetStringFieldOrProperty(state, "_sceneToLoad");
            if (string.IsNullOrEmpty(sceneName))
            {
                sceneName = GetStringFieldOrProperty(state, "sceneToLoad");
            }

            return string.Equals(sceneName, WorkshopVictorySceneName, StringComparison.OrdinalIgnoreCase) &&
                   GetBoolFieldOrProperty(state, "loadCustomCampaign") &&
                   !string.IsNullOrEmpty(GetStringFieldOrProperty(state, "customLevelID"));
        }

        private static bool IsEscReturnPauseState()
        {
            return PauseController.pauseStatus == PauseStatus.MenuPause ||
                   PauseController.pauseStatus == PauseStatus.ConfirmationPause;
        }

        private static void ArmWorkshopOnlineLobbyReturn(string source)
        {
            if (_returnToWorkshopOnlineLobbyPending || _returnToWorkshopOnlineLobbyAttempted)
            {
                return;
            }

            _returnToWorkshopOnlineLobbyPending = true;
            _returnToWorkshopOnlineLobbyAtUtc = DateTime.MinValue;
            DiagnosticLog.Info(
                "Queued direct return to the online lobby browser after Workshop Esc return: source=" +
                source + ".");
        }

        private static bool LevelTitleShowTextPrefix(string s)
        {
            if (!ShouldSuppressWorkshopJoinPrompt(s))
            {
                return true;
            }

            HideActiveWorkshopJoinPrompt();
            DiagnosticLog.Info("Suppressed the in-game Press To Join banner for the Workshop client.");
            return false;
        }

        private static void HideActiveWorkshopJoinPrompt()
        {
            try
            {
                var levelTitleType = AccessTools.TypeByName("LevelTitle");
                var levelTitle = levelTitleType == null
                    ? null
                    : UnityEngine.Object.FindObjectOfType(levelTitleType) as Component;
                if (levelTitle != null && levelTitle.gameObject.activeSelf)
                {
                    levelTitle.gameObject.SetActive(false);
                }
            }
            catch (Exception exception)
            {
                DiagnosticLog.Warning("Active Workshop join-prompt hide failed: " + exception.Message);
            }
        }

        private static bool ShouldSuppressWorkshopJoinPrompt(string text)
        {
            var settings = Plugin.Settings;
            if (settings == null || !settings.EnableOnlineWorkshopInjection ||
                !IsWorkshopOnlineSession() || string.IsNullOrEmpty(text))
            {
                return false;
            }

            try
            {
                var languageManagerType = AccessTools.TypeByName("Localisation.LanguageManager");
                var getLocalisedString = languageManagerType == null
                    ? null
                    : languageManagerType.GetMethod(
                        "GetLocalisedString",
                        BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance,
                        null,
                        new[] { typeof(string) },
                        null);
                if (languageManagerType == null || getLocalisedString == null)
                {
                    return false;
                }

                // Instance is declared on LanguageManager's generic base class.
                // Find the live Unity object instead of depending on inherited
                // static-property reflection, which differs between game builds.
                var languageManager = UnityEngine.Object.FindObjectOfType(languageManagerType);
                if (languageManager == null)
                {
                    return false;
                }

                var joinPrompt = getLocalisedString.Invoke(
                    languageManager,
                    new object[] { PressToJoinLocalizationKey }) as string;
                return !string.IsNullOrEmpty(joinPrompt) &&
                    string.Equals(text, joinPrompt, StringComparison.Ordinal);
            }
            catch (Exception exception)
            {
                DiagnosticLog.Warning("Workshop join-prompt comparison failed: " + exception.Message);
                return false;
            }
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

        private static IEnumerable<CodeInstruction> SwitchLevelTranspiler(IEnumerable<CodeInstruction> instructions)
        {
            return InsertWorkshopInjection(instructions, "GameModeController.SwitchLevel");
        }

        private static IEnumerable<CodeInstruction> EnterMissionTranspiler(IEnumerable<CodeInstruction> instructions)
        {
            return InsertWorkshopInjection(instructions, "WorldMapController.EnterMission");
        }

        private static IEnumerable<CodeInstruction> InsertWorkshopInjection(
            IEnumerable<CodeInstruction> instructions,
            string targetName)
        {
            var result = new List<CodeInstruction>();
            var injector = typeof(HarmonyDiagnostics).GetMethod(
                "ApplyWorkshopState",
                BindingFlags.NonPublic | BindingFlags.Static,
                null,
                Type.EmptyTypes,
                null);
            var inserted = false;

            foreach (var instruction in instructions)
            {
                if (!inserted && IsGameStateAdminRpc(instruction))
                {
                    result.Add(new CodeInstruction(OpCodes.Call, injector));
                    inserted = true;
                }

                result.Add(instruction);
            }

            if (!inserted)
            {
                DiagnosticLog.Warning("Workshop injection point not found in " + targetName + ".");
            }

            return result;
        }

        private static bool IsGameStateAdminRpc(CodeInstruction instruction)
        {
            var method = instruction.operand as MethodInfo;
            if (method != null && method.Name == "AdminRPC")
            {
                var genericArguments = method.IsGenericMethod ? method.GetGenericArguments() : new Type[0];
                if (genericArguments.Length == 1 && genericArguments[0].Name == "GameState")
                {
                    return true;
                }
            }

            return instruction.operand != null &&
                   instruction.operand.ToString().IndexOf("AdminRPC<GameState>", StringComparison.Ordinal) >= 0;
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

        private static bool IsOnlineHost()
        {
            var connectType = AccessTools.TypeByName("Connect");
            if (connectType == null || !IsOnline())
            {
                return false;
            }

            var hostGetter = AccessTools.PropertyGetter(connectType, "IsHost");
            return hostGetter != null && Convert.ToBoolean(hostGetter.Invoke(null, null));
        }

        private static bool IsOnline()
        {
            var connectType = AccessTools.TypeByName("Connect");
            if (connectType == null)
            {
                return false;
            }

            var offlineGetter = AccessTools.PropertyGetter(connectType, "IsOffline");
            return offlineGetter == null || !Convert.ToBoolean(offlineGetter.Invoke(null, null));
        }

        private static void SetFieldOrProperty(object instance, string name, object value)
        {
            var type = instance.GetType();
            var field = type.GetField(
                name,
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static);
            if (field != null)
            {
                field.SetValue(instance, value);
                return;
            }

            var property = type.GetProperty(
                name,
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static);
            if (property != null && property.CanWrite)
            {
                property.SetValue(instance, value, null);
                return;
            }

            throw new MissingMemberException(type.FullName, name);
        }

        private static int GetIntFieldOrProperty(object instance, string name)
        {
            if (instance == null)
            {
                return 0;
            }

            var type = instance.GetType();
            var field = type.GetField(
                name,
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static);
            if (field != null)
            {
                return Convert.ToInt32(field.GetValue(instance));
            }

            var property = type.GetProperty(
                name,
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static);
            if (property != null && property.CanRead)
            {
                return Convert.ToInt32(property.GetValue(instance, null));
            }

            return 0;
        }

        private static bool GetBoolFieldOrProperty(object instance, string name)
        {
            if (instance == null)
            {
                return false;
            }

            var type = instance.GetType();
            var field = type.GetField(
                name,
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static);
            if (field != null)
            {
                try
                {
                    return Convert.ToBoolean(field.GetValue(instance));
                }
                catch
                {
                    return false;
                }
            }

            var property = type.GetProperty(
                name,
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static);
            if (property != null && property.CanRead)
            {
                try
                {
                    return Convert.ToBoolean(property.GetValue(instance, null));
                }
                catch
                {
                    return false;
                }
            }

            return false;
        }

        private static string GetStringFieldOrProperty(object instance, string name)
        {
            if (instance == null)
            {
                return string.Empty;
            }

            var type = instance.GetType();
            var field = type.GetField(
                name,
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static);
            if (field != null)
            {
                try
                {
                    var value = field.GetValue(instance);
                    return value == null ? string.Empty : Convert.ToString(value);
                }
                catch
                {
                    return string.Empty;
                }
            }

            var property = type.GetProperty(
                name,
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static);
            if (property != null && property.CanRead)
            {
                try
                {
                    var value = property.GetValue(instance, null);
                    return value == null ? string.Empty : Convert.ToString(value);
                }
                catch
                {
                    return string.Empty;
                }
            }

            return string.Empty;
        }

        private static string BuildTraceMessage(
            MethodBase method,
            object instance,
            object[] arguments)
        {
            var builder = new StringBuilder();
            builder.Append(DescribeMethod(method));
            builder.Append("(");

            var parameters = method.GetParameters();
            for (var index = 0; index < parameters.Length; index++)
            {
                if (index > 0)
                {
                    builder.Append(", ");
                }

                var parameter = parameters[index];
                builder.Append(parameter.Name);
                builder.Append("=");
                var value = arguments != null && index < arguments.Length ? arguments[index] : null;
                builder.Append(FormatArgument(parameter.Name, value));
            }

            builder.Append(")");
            var state = BuildSafeObjectSummary(instance);
            if (!string.IsNullOrEmpty(state))
            {
                builder.Append("; state=");
                builder.Append(state);
            }

            return builder.ToString();
        }

        private static string FormatArgument(string parameterName, object value)
        {
            if (IsSensitiveName(parameterName))
            {
                return "<redacted>";
            }

            if (value == null)
            {
                return "null";
            }

            if (value is Vector3)
            {
                return FormatVector3((Vector3)value);
            }

            var summary = BuildSafeObjectSummary(value);
            if (!string.IsNullOrEmpty(summary))
            {
                return summary;
            }

            var component = value as Component;
            if (component != null)
            {
                try
                {
                    return "<" + value.GetType().Name +
                           " position=" + FormatVector3(component.transform.position) + ">";
                }
                catch
                {
                    return "<" + value.GetType().Name + ">";
                }
            }

            var type = value.GetType();
            if (type.IsEnum || value is bool || value is byte || value is short ||
                value is int || value is long || value is float || value is double || value is decimal)
            {
                return Convert.ToString(value, System.Globalization.CultureInfo.InvariantCulture);
            }

            var text = value as string;
            if (text != null)
            {
                return "\"" + Sanitize(text, 160) + "\"";
            }

            return "<" + type.FullName + ">";
        }

        private static string FormatVector3(Vector3 value)
        {
            return string.Format(
                System.Globalization.CultureInfo.InvariantCulture,
                "({0:0.###},{1:0.###},{2:0.###})",
                value.x,
                value.y,
                value.z);
        }

        private static string BuildSafeObjectSummary(object value)
        {
            if (value == null)
            {
                return string.Empty;
            }

            var typeName = value.GetType().FullName;
            switch (typeName)
            {
                case "RoomInfo":
                    return FormatFields(value, new[]
                    {
                        "gameMode", "campaignName", "CurrentSceneName", "capacity", "_playerCount",
                        "returnToWorldMap", "levelNumber", "totalLevels", "worldMapProgress",
                        "liberatedAreas", "invalidInfo", "hardMode", "hardcoreMode"
                    });
                case "GameState":
                    return FormatFields(value, new[]
                    {
                        "_sceneToLoad", "_campaignName", "levelNumber", "customLevelID",
                        "loadCustomCampaign", "loadMode", "gameMode", "levelEditorActive",
                        "returnToWorldMap", "arcadeHardMode", "persistPastLevelLoad"
                    });
                case "LevelSelectionController":
                    return FormatFields(value, new[]
                    {
                        "_levelFileNameToLoad", "JoinScene", "CampaignScene", "OnlineCampaign",
                        "OfflineCampaign", "DefaultCampaign", "loadPublishedCampaign", "isOnlineCampaign",
                        "currentWorkshopLevel"
                    });
                case "GameModeController":
                    return FormatFields(value, new[]
                    {
                        "switchingLevel", "nextScene", "levelHasStarted", "levelFinished",
                        "waitingForAllPlayersToReady", "switchSilently"
                    });
                case "HeroController":
                    return FormatHeroControllerState(value);
                case "Player":
                    return FormatPlayerState(value);
                case "PID":
                    return FormatPid(value);
                case "WorkshopLevelDetails":
                    return FormatFields(value, new[]
                    {
                        "name", "fileid", "fileName", "tags", "isWWBLevel", "wasCompletedSuccessfully"
                    });
                case "Campaign":
                    return FormatFields(value, new[] { "name", "levels", "brodownLevel" });
                case "CampaignHeader":
                    return FormatFields(value, new[]
                    {
                        "name", "length", "md5", "isPublished", "gameMode"
                    });
                case "MakeOnlineMenu":
                    return FormatFields(value, new[] { "state", "playerLimit", "canChangePassword", "canChangeName" });
                default:
                    return string.Empty;
            }
        }

        private static string FormatHeroControllerState(object value)
        {
            return FormatFields(value, new[]
            {
                "playersPlaying", "players", "PIDS", "playerControllerIDs",
                "heroesHaveBeenReleasedFromTransport", "brosHaveBeenReleased",
                "WaitForAllPlayersToSpawnBeforeStarting", "AllPlayersHaveJoined"
            });
        }

        private static string FormatPlayerState(object value)
        {
            var builder = new StringBuilder();
            builder.Append(FormatFields(value, new[]
            {
                "playerNum", "lives", "firstDeployment", "_awaitingHeroTypeFromServer", "heroType"
            }));
            builder.Length--;
            builder.Append(", IsMine=");
            builder.Append(FormatReadableProperty(value, "IsMine"));
            builder.Append(", controllerNum=");
            builder.Append(FormatReadableProperty(value, "controllerNum"));
            builder.Append(", character=");
            var player = value as Player;
            if (player != null && player.character != null)
            {
                try
                {
                    builder.Append("<" + player.character.GetType().Name +
                                   " position=" + FormatVector3(player.character.transform.position) + ">");
                }
                catch
                {
                    builder.Append(FormatReadableProperty(value, "character"));
                }
            }
            else
            {
                builder.Append(FormatReadableProperty(value, "character"));
            }
            builder.Append("}");
            return builder.ToString();
        }

        private static string FormatPid(object value)
        {
            return "PID{IsMine=" + FormatReadableProperty(value, "IsMine") + "}";
        }

        private static string FormatReadableProperty(object value, string propertyName)
        {
            try
            {
                var property = value.GetType().GetProperty(
                    propertyName,
                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static);
                if (property == null || !property.CanRead || property.GetIndexParameters().Length != 0)
                {
                    return "<missing>";
                }

                return FormatFieldValue(property.GetValue(value, null));
            }
            catch (Exception exception)
            {
                return "<error:" + exception.GetType().Name + ">";
            }
        }

        private static string FormatFields(object value, string[] fieldNames)
        {
            var builder = new StringBuilder();
            builder.Append(value.GetType().Name);
            builder.Append("{");
            var wroteValue = false;

            foreach (var fieldName in fieldNames)
            {
                var field = value.GetType().GetField(
                    fieldName,
                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static);
                if (field == null)
                {
                    continue;
                }

                object fieldValue;
                try
                {
                    fieldValue = field.GetValue(value);
                }
                catch
                {
                    continue;
                }

                if (wroteValue)
                {
                    builder.Append(", ");
                }

                builder.Append(fieldName);
                builder.Append("=");
                builder.Append(FormatFieldValue(fieldValue));
                wroteValue = true;
            }

            builder.Append("}");
            return builder.ToString();
        }

        private static string FormatFieldValue(object value)
        {
            if (value == null)
            {
                return "null";
            }

            var nestedSummary = BuildSafeObjectSummary(value);
            if (!string.IsNullOrEmpty(nestedSummary))
            {
                return nestedSummary;
            }

            var array = value as Array;
            if (array != null)
            {
                return FormatArrayValue(array);
            }

            var type = value.GetType();
            if (type.IsEnum || type.IsPrimitive || value is decimal)
            {
                return Convert.ToString(value, System.Globalization.CultureInfo.InvariantCulture);
            }

            var text = value as string;
            if (text != null)
            {
                return "\"" + Sanitize(text, 160) + "\"";
            }

            return "<" + type.Name + ">";
        }

        private static string FormatArrayValue(Array array)
        {
            var builder = new StringBuilder();
            var elementType = array.GetType().GetElementType();
            builder.Append(elementType == null ? "Array" : elementType.Name);
            builder.Append("[");
            builder.Append(array.Length);
            builder.Append("]{");

            var maxItems = System.Math.Min(array.Length, 8);
            for (var index = 0; index < maxItems; index++)
            {
                if (index > 0)
                {
                    builder.Append(",");
                }

                builder.Append(FormatFieldValue(array.GetValue(index)));
            }

            if (array.Length > maxItems)
            {
                builder.Append(",...");
            }

            builder.Append("}");
            return builder.ToString();
        }

        private static bool IsSensitiveName(string name)
        {
            if (string.IsNullOrEmpty(name))
            {
                return false;
            }

            var lowered = name.ToLowerInvariant();
            return lowered.Contains("password") || lowered.Contains("token") ||
                   lowered.Contains("secret") || lowered.Contains("credential");
        }

        private static string Sanitize(string value, int maxLength)
        {
            var builder = new StringBuilder(value.Length);
            for (var index = 0; index < value.Length; index++)
            {
                var current = value[index];
                if (current == '\r')
                {
                    builder.Append("\\r");
                }
                else if (current == '\n')
                {
                    builder.Append("\\n");
                }
                else if (char.IsHighSurrogate(current))
                {
                    if (index + 1 < value.Length && char.IsLowSurrogate(value[index + 1]))
                    {
                        builder.Append(current);
                        builder.Append(value[++index]);
                    }
                    else
                    {
                        builder.Append("\\u");
                        builder.Append(((int)current).ToString("X4"));
                    }
                }
                else if (char.IsLowSurrogate(current))
                {
                    builder.Append("\\u");
                    builder.Append(((int)current).ToString("X4"));
                }
                else
                {
                    builder.Append(current);
                }
            }

            var result = builder.ToString();
            if (result.Length > maxLength)
            {
                result = result.Substring(0, maxLength) + "...";
            }

            return result;
        }

        private static bool ShouldWrite(string key, string message, out string suppressionSummary)
        {
            suppressionSummary = string.Empty;
            lock (Sync)
            {
                var now = DateTime.UtcNow;
                var cacheKey = ShouldCoalesceByMethod(key) ? key : key + "\n" + message;
                TraceCacheEntry previous;
                if (TraceCache.TryGetValue(cacheKey, out previous) &&
                    now - previous.Timestamp < TimeSpan.FromSeconds(DuplicateWindowSeconds))
                {
                    previous.SuppressedCount++;
                    previous.LastMessage = message;
                    return false;
                }

                if (previous != null && previous.SuppressedCount > 0)
                {
                    suppressionSummary =
                        "TRACE_SUPPRESSED method=" + key +
                        "; count=" + previous.SuppressedCount +
                        "; latest=" + Sanitize(message, 500);
                }

                TraceCache[cacheKey] = new TraceCacheEntry(message, now);
                PruneTraceCache(now);
                return true;
            }
        }

        private static bool ShouldCoalesceByMethod(string key)
        {
            return key.EndsWith("ConnectionLayer.UpdateOnlinePlayerList", StringComparison.Ordinal) ||
                   key.EndsWith("RoomInfo.RefreshInfo", StringComparison.Ordinal) ||
                   key.EndsWith("RoomInfo.PushUpdatedInfo", StringComparison.Ordinal) ||
                   key.EndsWith("RoomInfo.PullUpdatedInfo", StringComparison.Ordinal) ||
                   key.EndsWith("HeroController.UpdatePlayerData", StringComparison.Ordinal) ||
                   key.EndsWith("HeroController.UpdatePlayerUserData", StringComparison.Ordinal) ||
                   key.EndsWith("Player.RespawnBro", StringComparison.Ordinal);
        }

        private static void PruneTraceCache(DateTime now)
        {
            if (TraceCache.Count <= MaxTraceCacheEntries)
            {
                return;
            }

            var cutoff = now - TimeSpan.FromSeconds(TraceCacheExpirySeconds);
            var staleKeys = new List<string>();
            foreach (var pair in TraceCache)
            {
                if (pair.Value.Timestamp < cutoff)
                {
                    staleKeys.Add(pair.Key);
                }
            }

            foreach (var staleKey in staleKeys)
            {
                TraceCache.Remove(staleKey);
            }

            while (TraceCache.Count > MaxTraceCacheEntries)
            {
                string oldestKey = null;
                DateTime oldestTimestamp = DateTime.MaxValue;
                foreach (var pair in TraceCache)
                {
                    if (pair.Value.Timestamp < oldestTimestamp)
                    {
                        oldestKey = pair.Key;
                        oldestTimestamp = pair.Value.Timestamp;
                    }
                }

                if (oldestKey == null)
                {
                    break;
                }

                TraceCache.Remove(oldestKey);
            }
        }

        private static string DescribeMethod(MethodBase method)
        {
            var typeName = method.DeclaringType == null ? "<unknown>" : method.DeclaringType.FullName;
            return typeName + "." + method.Name;
        }

        private sealed class DeferredSpawnPosition
        {
            public DeferredSpawnPosition(
                Player.SpawnType spawnType,
                bool spawnViaAirDrop,
                Vector3 position)
            {
                SpawnType = spawnType;
                SpawnViaAirDrop = spawnViaAirDrop;
                Position = position;
            }

            public Player.SpawnType SpawnType { get; private set; }
            public bool SpawnViaAirDrop { get; private set; }
            public Vector3 Position { get; private set; }
        }

        private sealed class TraceTarget
        {
            public TraceTarget(string typeName, string methodName)
            {
                TypeName = typeName;
                MethodName = methodName;
            }

            public string TypeName { get; private set; }
            public string MethodName { get; private set; }

            public override string ToString()
            {
                return TypeName + "." + MethodName;
            }
        }

        private sealed class TraceCacheEntry
        {
            public TraceCacheEntry(string message, DateTime timestamp)
            {
                LastMessage = message;
                Timestamp = timestamp;
            }

            public string LastMessage { get; set; }
            public DateTime Timestamp { get; private set; }
            public int SuppressedCount { get; set; }
        }
    }
}
