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
    // 核心入口：Start/Stop/Update/TracePrefix
    // 其他功能模块已拆分到 HarmonyDiagnostics.*.cs 文件
    internal static partial class HarmonyDiagnostics
    {
        private const string HarmonyId = "GJKen.BroforceOnlineDiagnostics.MethodTrace";
        private const int DuplicateWindowSeconds = 5;
        private const int DuplicateWorkshopLoadSuppressionSeconds = 5;
        private const int WorkshopLocalJoinRequestRetrySeconds = 10;
        private const int LateJoinPlayerRequestTimeoutSeconds = 45;
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
            new TraceTarget("BroforceOnlineDiagnostics.FrpDirectLayer", "CreateMatch"),
            new TraceTarget("BroforceOnlineDiagnostics.FrpDirectLayer", "JoinLobby"),
            new TraceTarget("BroforceOnlineDiagnostics.FrpDirectLayer", "LeaveMatch"),
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
        private static readonly Dictionary<int, int> WorkshopLocalJoinSlots =
            new Dictionary<int, int>();
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
        private static bool _workshopLevelNumberOverridePending;
        private static int _workshopLevelNumberOverride;
        private static string _workshopLevelNumberOverrideCustomLevelId;
        private static string _workshopLevelNumberOverrideScene;
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
            ClearWorkshopLevelNumberOverride();
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
            var spawnJoinedPlayersPostfixMethod = typeof(HarmonyDiagnostics).GetMethod(
                "SpawnJoinedPlayersPostfix",
                BindingFlags.NonPublic | BindingFlags.Static);
            var spawnJoinedPlayersPostfix = new HarmonyMethod(spawnJoinedPlayersPostfixMethod);
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
                        var postfix = GetPostfixForTarget(
                            target,
                            joinLobbyPostfix,
                            lobbyCreatedPostfix,
                            playerHasJoinedMatchPostfix,
                            joinedLobbyPostfix,
                            playerStartPostfix,
                            assignCharacterPostfix,
                            setPlayerCharacterPostfix,
                            requestJoinGamePostfix,
                            spawnJoinedPlayersPostfix);
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
            PatchWorkshopPickupSynchronization();
            PatchOnlineAfkPrevention();
            PatchLevelOutcomeDiagnostics();
            PatchWorkshopLevelEndReentryGuard();
            PatchMainMenuInitializationPostfix();
            PatchMainMenuInitializationDelay();
            PatchLobbyMainMenuReturnPostfix();
            PatchMainMenuMenuActiveSetter();
            PatchMainMenuShowRoutineCompletion();
            NotifySceneLoaded(SceneManager.GetActiveScene());
            OptionalBroModDiagnostics.LogCompatibilitySnapshot("diagnostics-start");

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
                ClearWorkshopLevelNumberOverride();
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

        private static HarmonyMethod GetPostfixForTarget(
            TraceTarget target,
            HarmonyMethod joinLobbyPostfix,
            HarmonyMethod lobbyCreatedPostfix,
            HarmonyMethod playerHasJoinedMatchPostfix,
            HarmonyMethod joinedLobbyPostfix,
            HarmonyMethod playerStartPostfix,
            HarmonyMethod assignCharacterPostfix,
            HarmonyMethod setPlayerCharacterPostfix,
            HarmonyMethod requestJoinGamePostfix,
            HarmonyMethod spawnJoinedPlayersPostfix)
        {
            if ((target.TypeName == "SteamLayer" ||
                 target.TypeName == "BroforceOnlineDiagnostics.FrpDirectLayer") &&
                target.MethodName == "JoinLobby")
            {
                return joinLobbyPostfix;
            }
            if ((target.TypeName == "SteamLayer" && target.MethodName == "LobbyCreated_Callback") ||
                (target.TypeName == "BroforceOnlineDiagnostics.FrpDirectLayer" &&
                 target.MethodName == "CreateMatch"))
            {
                return lobbyCreatedPostfix;
            }
            if (target.TypeName == "ConnectionLayer" && target.MethodName == "PlayerHasJoinedMatch")
            {
                return playerHasJoinedMatchPostfix;
            }
            if (target.TypeName == "ConnectionLayer" && target.MethodName == "OnJoinedLobby")
            {
                return joinedLobbyPostfix;
            }
            if ((target.TypeName == "SteamLayer" ||
                 target.TypeName == "BroforceOnlineDiagnostics.FrpDirectLayer") &&
                target.MethodName == "LeaveMatch")
            {
                return new HarmonyMethod(typeof(HarmonyDiagnostics).GetMethod(
                    "LeaveMatchPostfix", BindingFlags.NonPublic | BindingFlags.Static));
            }
            if (target.TypeName == "Player" && target.MethodName == "Start")
            {
                return playerStartPostfix;
            }
            if (target.TypeName == "Player" && target.MethodName == "AssignCharacter")
            {
                return assignCharacterPostfix;
            }
            if (target.TypeName == "HeroController" && target.MethodName == "SetPlayerCharacter")
            {
                return setPlayerCharacterPostfix;
            }
            if (target.TypeName == "HeroController" && target.MethodName == "RequestJoinGame")
            {
                return requestJoinGamePostfix;
            }
            if (target.TypeName == "HeroController" && target.MethodName == "SpawnJoinedPlayers")
            {
                return spawnJoinedPlayersPostfix;
            }

            return null;
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

                if (__originalMethod != null &&
                    __originalMethod.DeclaringType != null &&
                    __originalMethod.DeclaringType.Name == "Player" &&
                    __originalMethod.Name == "Start")
                {
                    RepairPendingLocalWorkshopPlayerOwnership(__instance as Player);
                }

                if (__originalMethod.DeclaringType != null &&
                    (__originalMethod.DeclaringType.Name == "SteamLayer" ||
                     __originalMethod.DeclaringType.Name == "FrpDirectLayer") &&
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
                        __originalMethod.DeclaringType.Name + "." + __originalMethod.Name,
                        __originalMethod.Name == "CreateMatch" ? "host" : "client");
                    OptionalBroModDiagnostics.LogCompatibilitySnapshot("network-session-start");
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
                    PrepareLateWorkshopJoinSlot(__args);
                }

                CaptureAuthoritativeWorkshopLevelNumber(__originalMethod, __args);
                RestoreAuthoritativeWorkshopLevelNumberBeforeCompletion(__originalMethod);
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

        internal static void Update()
        {
            ObserveOnlineHostRole();
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

        private static bool _workshopCompletionSubscribed;
    }
}
