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
    // 角色退出重入：控制器绑定恢复、英雄类型恢复、重复加入抑制、暂停状态清理。
    internal static partial class HarmonyDiagnostics
    {
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
    }
}
