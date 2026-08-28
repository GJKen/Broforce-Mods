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
    // Steam Lobby 状态同步与返回大厅：大厅数据读写、主菜单动画与可见性控制。
    internal static partial class HarmonyDiagnostics
    {
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

        private static bool IsWorkshopLobbyReady()
        {
            return string.Equals(
                GetWorkshopLobbyData(WorkshopLobbyReadyKey),
                "1",
                StringComparison.Ordinal);
        }

        private static void RefreshWorkshopLobbyDataIfNeeded(string context)
        {
            if (_sessionIsHost || DateTime.UtcNow < _lateJoinLobbyRefreshAtUtc)
            {
                return;
            }
            if (Connect.Layer is FrpDirectLayer)
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
                var frpLayer = Connect.Layer as FrpDirectLayer;
                if (frpLayer != null)
                {
                    return frpLayer.GetRoomMetadata(key);
                }

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

                var frpLayer = Connect.Layer as FrpDirectLayer;
                if (frpLayer != null)
                {
                    var frpResult = frpLayer.SetRoomMetadata(key, value ?? string.Empty);
                    if (frpResult)
                    {
                        DiagnosticLog.Info(
                            "Workshop FRP room data " + key + "=" + (value ?? string.Empty) +
                            "; context=" + context + ".");
                    }
                    return frpResult;
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

        private static void ClearWorkshopOnlineLobbyReturnState()
        {
            _returnToWorkshopOnlineLobbyPending = false;
            _returnToWorkshopOnlineLobbyAttempted = false;
            _returnToWorkshopOnlineLobbyAtUtc = DateTime.MinValue;
            _returnToWorkshopOnlineLobbyVisualsSuppressed = false;
            _returnToWorkshopOnlineLobbyNavigationStartedAtUtc = DateTime.MinValue;
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
    }
}
