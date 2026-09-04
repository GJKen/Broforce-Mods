using System;
using System.Globalization;
using System.Reflection;
using System.Text;
using HarmonyLib;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CustomMapMultiplayer
{
    // Low-frequency observation around Broforce's native 35-second online AFK path.
    internal static partial class HarmonyDiagnostics
    {
        private const float NativeAfkTimeoutSeconds = 35f;
        private const float AfkCountingLogSeconds = 5f;
        private const float AfkWarningLogSeconds = 30f;
        private const double NativeAfkDropoutCallbackWindowSeconds = 2d;
        private const double ManualAfkDropoutCallbackWindowSeconds = 5d;

        private static readonly AfkPlayerLogState[] AfkPlayerLogStates =
            new AfkPlayerLogState[4];
        private static readonly DateTime[] NativeAfkDropoutPendingUntilUtc =
            new DateTime[4];
        private static readonly bool[] NativeAfkDropoutObserved = new bool[4];
        private static readonly bool[] AfkPreventionLogged = new bool[4];
        private static readonly bool[] PendingManualAfkRequests = new bool[4];
        private static readonly bool[] ManualAfkDropoutActive = new bool[4];
        private static readonly DateTime[] ManualAfkDropoutPendingUntilUtc =
            new DateTime[4];
        private static System.Reflection.FieldInfo _playerIdleTimerField;
        private static MethodInfo _menuInstantiateItemsMethod;
        private static string _pauseMenuAfkActionRoute;

        private static void PatchPauseMenuAfkMenu()
        {
            var pauseMenuInstantiateItems = typeof(PauseMenu).GetMethod(
                "InstantiateItems",
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance,
                null,
                Type.EmptyTypes,
                null);
            var pauseMenuPostfixMethod = typeof(HarmonyDiagnostics).GetMethod(
                "PauseMenuInstantiateItemsPostfix",
                BindingFlags.NonPublic | BindingFlags.Static);
            var menuUpdate = typeof(Menu).GetMethod(
                "Update",
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance,
                null,
                Type.EmptyTypes,
                null);
            var menuUpdatePostfixMethod = typeof(HarmonyDiagnostics).GetMethod(
                "MenuUpdateAfkFontMaterialPostfix",
                BindingFlags.NonPublic | BindingFlags.Static);
            if (pauseMenuInstantiateItems == null || pauseMenuPostfixMethod == null ||
                menuUpdate == null || menuUpdatePostfixMethod == null)
            {
                DiagnosticLog.Warning(
                    "PauseMenu AFK menu patch could not resolve its target methods.");
                return;
            }

            try
            {
                _menuInstantiateItemsMethod = typeof(Menu).GetMethod(
                    "InstantiateItems",
                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance,
                    null,
                    Type.EmptyTypes,
                    null);
                if (_menuInstantiateItemsMethod == null)
                {
                    DiagnosticLog.Warning(
                        "PauseMenu AFK menu patch could not resolve Menu.InstantiateItems.");
                    return;
                }

                var pauseMenuPostfix = new HarmonyMethod(pauseMenuPostfixMethod)
                {
                    priority = Priority.Last
                };
                _harmony.Patch(pauseMenuInstantiateItems, null, pauseMenuPostfix, null, null);

                var menuUpdatePostfix = new HarmonyMethod(menuUpdatePostfixMethod)
                {
                    priority = Priority.Last
                };
                _harmony.Patch(menuUpdate, null, menuUpdatePostfix, null, null);
                DiagnosticLog.Info(
                    "PauseMenu AFK menu patch enabled after PauseMenu.InstantiateItems and Menu.Update.");
            }
            catch (Exception exception)
            {
                DiagnosticLog.Warning(
                    "PauseMenu AFK menu patch failed: " + exception);
            }
        }

        private static void PauseMenuInstantiateItemsPostfix(object __instance)
        {
            if (__instance == null || !(__instance is PauseMenu))
            {
                return;
            }

            try
            {
                SynchronizePauseMenuItems(__instance);
                UpdatePauseMenuAfkTextAndVisuals(__instance);
            }
            catch (Exception exception)
            {
                DiagnosticLog.Warning(
                    "PauseMenu AFK menu synchronization failed after InstantiateItems: " +
                    exception);
            }
        }

        private static void MenuUpdateAfkFontMaterialPostfix(object __instance)
        {
            var pauseMenuType = AccessTools.TypeByName("PauseMenu");
            if (__instance == null || pauseMenuType == null ||
                !pauseMenuType.IsInstanceOfType(__instance))
            {
                return;
            }

            try
            {
                SynchronizePauseMenuItems(__instance);
                UpdatePauseMenuAfkTextAndVisuals(__instance);
            }
            catch (Exception exception)
            {
                DiagnosticLog.Warning(
                    "PauseMenu AFK menu update failed after Menu.Update: " +
                    exception);
            }
        }

        private static void SynchronizePauseMenuItems(object pauseMenu)
        {
            var masterItems = GetMemberValue(pauseMenu, "masterItems") as Array;
            var items = GetMemberValue(pauseMenu, "items") as Array;
            if (masterItems == null || items == null || masterItems.Length == items.Length)
            {
                return;
            }

            if (_menuInstantiateItemsMethod == null)
            {
                return;
            }

            _menuInstantiateItemsMethod.Invoke(pauseMenu, null);
            masterItems = GetMemberValue(pauseMenu, "masterItems") as Array;
            items = GetMemberValue(pauseMenu, "items") as Array;
            if (masterItems != null && items != null && masterItems.Length == items.Length)
            {
                DiagnosticLog.Info(
                    "PauseMenu menu arrays synchronized after RocketLib injection; count=" +
                    masterItems.Length + ".");
            }
            else
            {
                DiagnosticLog.Warning(
                    "PauseMenu menu arrays remain mismatched after synchronization attempt; masterItems=" +
                    (masterItems == null ? -1 : masterItems.Length) +
                    "; items=" + (items == null ? -1 : items.Length) + ".");
            }
        }

        private static void UpdatePauseMenuAfkTextAndVisuals(object pauseMenu)
        {
            var masterItems = GetMemberValue(pauseMenu, "masterItems") as Array;
            var items = GetMemberValue(pauseMenu, "items") as Array;
            if (masterItems == null || items == null || masterItems.Length != items.Length)
            {
                return;
            }

            var afkIndex = FindPauseMenuAfkIndex(masterItems);
            if (afkIndex < 0 || afkIndex >= items.Length)
            {
                return;
            }

            var afkItemUi = items.GetValue(afkIndex);
            var preference = Plugin.Settings == null ? null : Plugin.Settings.SettingsLanguage;
            var localizedText = SettingsUiLocalization.Get(preference).ManualAfkButton;
            SetPauseMenuAfkText(afkItemUi, localizedText);

            var sourceIndex = FindNativePauseMenuFontSourceIndex(
                masterItems,
                items,
                masterItems.Length,
                afkIndex);
            if (sourceIndex < 0)
            {
                return;
            }

            var sourceItemUi = items.GetValue(sourceIndex);
            if (HasMatchingPauseMenuTextVisuals(sourceItemUi, afkItemUi))
            {
                return;
            }

            string copiedParts;
            if (CopyPauseMenuTextVisuals(sourceItemUi, afkItemUi, out copiedParts))
            {
                DiagnosticLog.Info(
                    "PauseMenu AFK font/material repaired; index=" + afkIndex +
                    "; sourceIndex=" + sourceIndex +
                    "; text=" + localizedText +
                    "; copied=" + copiedParts + ".");
            }
        }

        private static int FindPauseMenuAfkIndex(Array masterItems)
        {
            for (var index = 0; index < masterItems.Length; index++)
            {
                var masterItem = masterItems.GetValue(index);
                var invokeMethod = GetMemberValue(masterItem, "invokeMethod") as string;
                if (string.IsNullOrEmpty(invokeMethod) ||
                    !invokeMethod.StartsWith("RocketLib_", StringComparison.Ordinal))
                {
                    continue;
                }

                if (_pauseMenuAfkActionRoute == null)
                {
                    var name = GetMemberValue(masterItem, "name") as string;
                    if (string.Equals(
                            name,
                            Plugin.PauseMenuAfkActionDisplayText,
                            StringComparison.Ordinal))
                    {
                        _pauseMenuAfkActionRoute = invokeMethod;
                    }
                }

                if (string.Equals(
                        invokeMethod,
                        _pauseMenuAfkActionRoute,
                        StringComparison.Ordinal))
                {
                    return index;
                }
            }

            return -1;
        }

        private static void SetPauseMenuAfkText(object itemUi, string text)
        {
            if (itemUi == null)
            {
                return;
            }

            if (!string.Equals(GetMemberValue(itemUi, "text") as string, text, StringComparison.Ordinal))
            {
                SetMemberValue(itemUi, "text", text);
            }

            var itemText = GetMemberValue(itemUi, "ItemText") as TextMesh;
            var backdropText = GetMemberValue(itemUi, "BackdropText") as TextMesh;
            if (itemText != null && !string.Equals(itemText.text, text, StringComparison.Ordinal))
            {
                itemText.text = text;
            }

            if (backdropText != null && !string.Equals(backdropText.text, text, StringComparison.Ordinal))
            {
                backdropText.text = text;
            }
        }

        private static int FindNativePauseMenuFontSourceIndex(
            Array masterItems,
            Array items,
            int itemCount,
            int afkIndex)
        {
            var optionsIndex = -1;
            var fallbackIndex = -1;
            for (var index = 0; index < itemCount; index++)
            {
                if (index == afkIndex)
                {
                    continue;
                }

                var masterItem = masterItems.GetValue(index);
                var invokeMethod = GetMemberValue(masterItem, "invokeMethod") as string;
                if (!string.IsNullOrEmpty(invokeMethod) &&
                    invokeMethod.StartsWith("RocketLib_", StringComparison.Ordinal))
                {
                    continue;
                }

                var itemUi = items.GetValue(index);
                if (!HasUsablePauseMenuTextVisuals(itemUi))
                {
                    continue;
                }

                if (fallbackIndex < 0)
                {
                    fallbackIndex = index;
                }

                var name = GetMemberValue(masterItem, "name") as string;
                if (string.Equals(name, "OPTIONS", StringComparison.OrdinalIgnoreCase))
                {
                    optionsIndex = index;
                    break;
                }
            }

            return optionsIndex >= 0 ? optionsIndex : fallbackIndex;
        }

        private static bool HasUsablePauseMenuTextVisuals(object itemUi)
        {
            var itemText = GetMemberValue(itemUi, "ItemText") as TextMesh;
            var backdropText = GetMemberValue(itemUi, "BackdropText") as TextMesh;
            return itemText != null && backdropText != null &&
                itemText.font != null && backdropText.font != null &&
                itemText.GetComponent<MeshRenderer>() != null &&
                backdropText.GetComponent<MeshRenderer>() != null;
        }

        private static bool HasMatchingPauseMenuTextVisuals(
            object sourceItemUi,
            object targetItemUi)
        {
            var sourceItemText = GetMemberValue(sourceItemUi, "ItemText") as TextMesh;
            var sourceBackdropText = GetMemberValue(sourceItemUi, "BackdropText") as TextMesh;
            var targetItemText = GetMemberValue(targetItemUi, "ItemText") as TextMesh;
            var targetBackdropText = GetMemberValue(targetItemUi, "BackdropText") as TextMesh;
            if (sourceItemText == null || sourceBackdropText == null ||
                targetItemText == null || targetBackdropText == null)
            {
                return false;
            }

            var sourceItemRenderer = sourceItemText.GetComponent<MeshRenderer>();
            var sourceBackdropRenderer = sourceBackdropText.GetComponent<MeshRenderer>();
            var targetItemRenderer = targetItemText.GetComponent<MeshRenderer>();
            var targetBackdropRenderer = targetBackdropText.GetComponent<MeshRenderer>();
            return sourceItemRenderer != null && sourceBackdropRenderer != null &&
                targetItemRenderer != null && targetBackdropRenderer != null &&
                targetItemText.font == sourceItemText.font &&
                targetBackdropText.font == sourceBackdropText.font &&
                targetItemRenderer.sharedMaterial == sourceItemRenderer.sharedMaterial &&
                targetBackdropRenderer.sharedMaterial == sourceBackdropRenderer.sharedMaterial;
        }

        private static bool CopyPauseMenuTextVisuals(
            object sourceItemUi,
            object targetItemUi,
            out string copiedParts)
        {
            copiedParts = string.Empty;
            var sourceItemText = GetMemberValue(sourceItemUi, "ItemText") as TextMesh;
            var sourceBackdropText = GetMemberValue(sourceItemUi, "BackdropText") as TextMesh;
            var targetItemText = GetMemberValue(targetItemUi, "ItemText") as TextMesh;
            var targetBackdropText = GetMemberValue(targetItemUi, "BackdropText") as TextMesh;
            if (sourceItemText == null || sourceBackdropText == null ||
                targetItemText == null || targetBackdropText == null)
            {
                return false;
            }

            var sourceItemRenderer = sourceItemText.GetComponent<MeshRenderer>();
            var sourceBackdropRenderer = sourceBackdropText.GetComponent<MeshRenderer>();
            var targetItemRenderer = targetItemText.GetComponent<MeshRenderer>();
            var targetBackdropRenderer = targetBackdropText.GetComponent<MeshRenderer>();
            if (sourceItemRenderer == null || sourceBackdropRenderer == null ||
                targetItemRenderer == null || targetBackdropRenderer == null ||
                sourceItemText.font == null || sourceBackdropText.font == null)
            {
                return false;
            }

            targetItemText.font = sourceItemText.font;
            targetBackdropText.font = sourceBackdropText.font;
            targetItemRenderer.sharedMaterial = GetRendererMaterial(sourceItemRenderer);
            targetBackdropRenderer.sharedMaterial = GetRendererMaterial(sourceBackdropRenderer);
            copiedParts = "ItemText.font,BackdropText.font,ItemText.material,BackdropText.material";
            return targetItemRenderer.sharedMaterial != null &&
                targetBackdropRenderer.sharedMaterial != null;
        }

        private static Material GetRendererMaterial(MeshRenderer renderer)
        {
            if (renderer == null)
            {
                return null;
            }

            return renderer.sharedMaterial;
        }

        private static object GetMemberValue(object instance, string name)
        {
            if (instance == null)
            {
                return null;
            }

            var type = instance.GetType();
            while (type != null)
            {
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
                if (property != null && property.CanRead)
                {
                    return property.GetValue(instance, null);
                }

                type = type.BaseType;
            }

            return null;
        }

        private static bool SetMemberValue(object instance, string name, object value)
        {
            if (instance == null)
            {
                return false;
            }

            var type = instance.GetType();
            while (type != null)
            {
                var property = type.GetProperty(
                    name,
                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static);
                if (property != null && property.CanWrite)
                {
                    property.SetValue(instance, value, null);
                    return true;
                }

                var field = type.GetField(
                    name,
                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static);
                if (field != null && !field.IsInitOnly)
                {
                    field.SetValue(instance, value);
                    return true;
                }

                type = type.BaseType;
            }

            return false;
        }

        internal static bool CanShowManualAfkMenuItem()
        {
            try
            {
                if (!IsManualAfkContextActive() || HeroController.players == null)
                {
                    return false;
                }

                return FindLocalAfkPlayer() != null;
            }
            catch (Exception exception)
            {
                DiagnosticLog.Warning("Manual AFK menu visibility check failed; item hidden: " + exception);
                return false;
            }
        }

        internal static bool RequestLocalAfk()
        {
            if (!IsManualAfkContextActive())
            {
                DiagnosticLog.Info(
                    "Manual AFK request ignored because the online game is not ready for a local AFK request.");
                return false;
            }

            var participantCount = CountAfkRoomParticipants();
            if (participantCount < 2)
            {
                var preference = Plugin.Settings == null ? null : Plugin.Settings.SettingsLanguage;
                var notice = SettingsUiLocalization.Get(preference).ManualAfkSinglePlayerNotice;
                Plugin.ShowFrpDirectNotice(notice);
                LogAfkEvent(
                    "AFK_STATE event=manual-request-rejected-single-player; participants=" +
                    participantCount + ".");
                return false;
            }

            var player = FindLocalAfkPlayer();
            if (player != null)
            {
                if (_playerIdleTimerField == null)
                {
                    DiagnosticLog.Warning("Manual AFK request ignored because Player.idleTimer was not resolved.");
                    return false;
                }

                _playerIdleTimerField.SetValue(player, NativeAfkTimeoutSeconds + 1f);
                PendingManualAfkRequests[player.playerNum] = true;
                ManualAfkDropoutActive[player.playerNum] = true;
                ManualAfkDropoutPendingUntilUtc[player.playerNum] = DateTime.UtcNow.AddSeconds(
                    ManualAfkDropoutCallbackWindowSeconds);
                LogAfkEvent("AFK_STATE event=manual-requested; player=" + player.playerNum +
                    "; controller=" + GetAfkControllerNumber(player) +
                    "; idleSeconds=" + FormatAfkSeconds(NativeAfkTimeoutSeconds + 1f));
                return true;
            }

            DiagnosticLog.Info("Manual AFK request ignored because no local player is currently playing.");
            return false;
        }

        private static int CountAfkRoomParticipants()
        {
            if (HeroController.PIDS == null)
            {
                return 0;
            }

            var count = 0;
            var slotCount = global::System.Math.Min(4, HeroController.PIDS.Length);
            for (var index = 0; index < slotCount; index++)
            {
                if (HeroController.PIDS[index] != null)
                {
                    count++;
                }
            }

            return count;
        }

        private static bool IsManualAfkContextActive()
        {
            if (!_networkSessionActive || !IsOnline() || Connect.IsOffline ||
                HeroController.players == null || HeroController.PIDS == null ||
                _joinLobbyInProgress || _lateJoinPending ||
                _returnToWorkshopOnlineLobbyPending)
            {
                return false;
            }

            if (string.Equals(
                    SceneManager.GetActiveScene().name,
                    "MainMenu",
                    StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            var gameModeControllerType = AccessTools.TypeByName("GameModeController");
            var instanceField = gameModeControllerType == null
                ? null
                : gameModeControllerType.GetField(
                    "instance",
                    System.Reflection.BindingFlags.Public |
                    System.Reflection.BindingFlags.NonPublic |
                    System.Reflection.BindingFlags.Static);
            var gameModeController = instanceField == null
                ? null
                : instanceField.GetValue(null);
            if (gameModeController == null)
            {
                return false;
            }

            return gameModeController != null &&
                !GetBoolFieldOrProperty(gameModeController, "switchingLevel") &&
                !GetBoolFieldOrProperty(gameModeController, "levelFinished") &&
                !GetBoolFieldOrProperty(gameModeController, "waitingForAllPlayersToReady");
        }

        private static Player FindLocalAfkPlayer()
        {
            var activeInputController = InputReader.ActiveInputID;
            Player onlyCandidate = null;
            Player controllerCandidate = null;
            var candidateCount = 0;
            var controllerCandidateCount = 0;

            for (var index = 0; index < HeroController.players.Length; index++)
            {
                var player = HeroController.players[index];
                if (!IsLocalAfkPlayer(player) || !IsAfkSlotPlaying(player.playerNum))
                {
                    continue;
                }

                candidateCount++;
                onlyCandidate = player;
                if (activeInputController >= 0 &&
                    GetAfkControllerNumber(player) == activeInputController)
                {
                    controllerCandidate = player;
                    controllerCandidateCount++;
                }
            }

            if (controllerCandidateCount == 1)
            {
                return controllerCandidate;
            }

            if (candidateCount == 1)
            {
                return onlyCandidate;
            }

            if (candidateCount > 1)
            {
                DiagnosticLog.Warning(
                    "Manual AFK request ignored because multiple local player slots were eligible: " +
                    "candidates=" + candidateCount + "; activeController=" + activeInputController + ".");
            }

            return null;
        }

        private static bool IsLocalAfkPlayer(Player player)
        {
            if (player == null || !player.IsMine || player.playerNum < 0 ||
                player.playerNum >= PendingManualAfkRequests.Length || HeroController.PIDS == null ||
                player.playerNum >= HeroController.PIDS.Length)
            {
                return false;
            }

            var slotPid = HeroController.PIDS[player.playerNum];
            return slotPid != null && slotPid.IsMine;
        }

        private static int GetAfkControllerNumber(Player player)
        {
            if (player == null)
            {
                return -1;
            }

            if (player.controllerNum >= 0)
            {
                return player.controllerNum;
            }

            var playerNum = player.playerNum;
            return HeroController.playerControllerIDs != null && playerNum >= 0 &&
                   playerNum < HeroController.playerControllerIDs.Length
                ? HeroController.playerControllerIDs[playerNum]
                : -1;
        }

        private static bool IsManualAfkPending(int playerNum)
        {
            return playerNum >= 0 && playerNum < PendingManualAfkRequests.Length &&
                PendingManualAfkRequests[playerNum];
        }

        private static bool IsManualAfkDropoutPending(int playerNum)
        {
            if (playerNum < 0 || playerNum >= ManualAfkDropoutPendingUntilUtc.Length)
            {
                return false;
            }

            if (ManualAfkDropoutActive[playerNum])
            {
                return true;
            }

            var pendingUntilUtc = ManualAfkDropoutPendingUntilUtc[playerNum];
            if (pendingUntilUtc == DateTime.MinValue || DateTime.UtcNow > pendingUntilUtc)
            {
                ManualAfkDropoutPendingUntilUtc[playerNum] = DateTime.MinValue;
                return false;
            }

            return true;
        }

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
            observation.ManualRequest = IsManualAfkPending(playerNum);
            observation.PreventionEnabled = Plugin.Settings != null &&
                Plugin.Settings.DisableOnlineAfkSpectatorMode && !observation.ManualRequest;
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
                PendingManualAfkRequests[index] = false;
                ManualAfkDropoutActive[index] = false;
                ManualAfkDropoutPendingUntilUtc[index] = DateTime.MinValue;
            }
        }

        private struct AfkUpdateObservation
        {
            public bool Active;
            public Player Player;
            public int PlayerNum;
            public float BeforeTimer;
            public float Delta;
            public bool ManualRequest;
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
