using HarmonyLib;
using System;
using System.Reflection;
using UnityEngine.SceneManagement;

namespace BroforceOnlineDiagnostics
{
    internal static partial class HarmonyDiagnostics
    {
        private static bool _workshopLevelEndSuppressionLogged;
        private static bool _workshopOutcomeReentrySuppressionLogged;

        private static void PatchWorkshopLevelEndReentryGuard()
        {
            var actionType = AccessTools.TypeByName("LevelEventAction");
            var actionMethod = actionType == null
                ? null
                : actionType.GetMethod(
                    "Start",
                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance,
                    null,
                    Type.EmptyTypes,
                    null);
            var prefixMethod = typeof(HarmonyDiagnostics).GetMethod(
                "LevelEventActionStartPrefix",
                BindingFlags.NonPublic | BindingFlags.Static);
            if (actionMethod == null || prefixMethod == null)
            {
                DiagnosticLog.Warning(
                    "Workshop level-end reentry guard could not resolve LevelEventAction.Start.");
            }
            else
            {
                try
                {
                    _harmony.Patch(
                        actionMethod,
                        new HarmonyMethod(prefixMethod),
                        null,
                        null,
                        null);
                    DiagnosticLog.Info(
                        "Workshop level-end reentry guard enabled for LevelEventAction.Start.");
                }
                catch (Exception exception)
                {
                    DiagnosticLog.Warning(
                        "Workshop level-end action guard patch failed: " + exception);
                }
            }

            PatchWorkshopOutcomeReentryGuard();
        }

        private static void PatchWorkshopOutcomeReentryGuard()
        {
            var method = typeof(GameModeController).GetMethod(
                "DetermineLevelOutcome",
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance,
                null,
                Type.EmptyTypes,
                null);
            var prefixMethod = typeof(HarmonyDiagnostics).GetMethod(
                "DetermineLevelOutcomePrefix",
                BindingFlags.NonPublic | BindingFlags.Static);
            if (method == null || prefixMethod == null)
            {
                DiagnosticLog.Warning(
                    "Workshop outcome reentry guard could not resolve " +
                    "GameModeController.DetermineLevelOutcome.");
                return;
            }

            try
            {
                _harmony.Patch(method, new HarmonyMethod(prefixMethod), null, null, null);
                DiagnosticLog.Info(
                    "Workshop outcome reentry guard enabled for " +
                    "GameModeController.DetermineLevelOutcome.");
            }
            catch (Exception exception)
            {
                DiagnosticLog.Warning(
                    "Workshop outcome reentry guard patch failed: " + exception);
            }
        }

        private static bool LevelEventActionStartPrefix(object __instance)
        {
            try
            {
                if (!IsWorkshopOnlineSession() ||
                    !IsConfiguredWorkshopSceneActive() ||
                    !IsConfiguredWorkshopGameState() ||
                    !IsWorkshopLevelEndAction(__instance))
                {
                    return true;
                }

                var controller = GameModeController.Instance;
                if (controller == null ||
                    !GetBoolFieldOrProperty(controller, "switchingLevel"))
                {
                    _workshopLevelEndSuppressionLogged = false;
                    return true;
                }

                SetFieldOrProperty(controller, "levelFinished", true);
                if (!_workshopLevelEndSuppressionLogged)
                {
                    _workshopLevelEndSuppressionLogged = true;
                    LogSuppressedWorkshopLevelEndReentry(controller, __instance);
                }
                return false;
            }
            catch (Exception exception)
            {
                DiagnosticLog.Warning(
                    "Workshop level-end reentry guard failed open: " + exception.Message);
                return true;
            }
        }

        private static bool DetermineLevelOutcomePrefix(object __instance)
        {
            try
            {
                if (!IsWorkshopOnlineSession() ||
                    !IsConfiguredWorkshopSceneActive() ||
                    !IsConfiguredWorkshopGameState() ||
                    __instance == null ||
                    !GetBoolFieldOrProperty(__instance, "switchingLevel") ||
                    !IsSuccessfulLevelOutcome(__instance))
                {
                    _workshopOutcomeReentrySuppressionLogged = false;
                    return true;
                }

                SetFieldOrProperty(__instance, "levelFinished", true);
                SetFieldOrProperty(__instance, "winTimer", 1000f);
                if (!_workshopOutcomeReentrySuppressionLogged)
                {
                    _workshopOutcomeReentrySuppressionLogged = true;
                    LogSuppressedWorkshopOutcomeReentry(__instance);
                }
                return false;
            }
            catch (Exception exception)
            {
                DiagnosticLog.Warning(
                    "Workshop outcome reentry guard failed open: " + exception.Message);
                return true;
            }
        }

        private static bool IsConfiguredWorkshopSceneActive()
        {
            var configuredScene = GetConfiguredWorkshopSceneName();
            return !string.IsNullOrEmpty(configuredScene) &&
                   string.Equals(
                       SceneManager.GetActiveScene().name,
                       configuredScene,
                       StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsConfiguredWorkshopGameState()
        {
            var settings = Plugin.Settings;
            var state = GetGameStateInstance(AccessTools.TypeByName("GameState"));
            if (settings == null || state == null ||
                !GetBoolFieldOrProperty(state, "loadCustomCampaign"))
            {
                return false;
            }

            var configuredWorkshopId = (settings.WorkshopId ?? string.Empty).Trim();
            var activeWorkshopId = GetStringFieldOrProperty(state, "customLevelID").Trim();
            return !string.IsNullOrEmpty(configuredWorkshopId) &&
                   string.Equals(
                       activeWorkshopId,
                       configuredWorkshopId,
                       StringComparison.Ordinal);
        }

        private static bool IsWorkshopLevelEndAction(object action)
        {
            var info = GetFieldOrPropertyValue(action, "info");
            var actionType = GetFieldOrPropertyValue(info, "levelActionType");
            var actionTypeName = actionType == null ? string.Empty : actionType.ToString();
            return string.Equals(
                       actionTypeName,
                       "LevelEndSuccess",
                       StringComparison.Ordinal) ||
                   string.Equals(
                       actionTypeName,
                       "LevelEndSuccessSilent",
                       StringComparison.Ordinal);
        }

        private static bool IsSuccessfulLevelOutcome(object controller)
        {
            var result = GetFieldOrPropertyValue(controller, "levelResult");
            return result != null &&
                   string.Equals(result.ToString(), "Success", StringComparison.Ordinal);
        }

        private static void LogSuppressedWorkshopLevelEndReentry(
            object controller,
            object action)
        {
            var state = GetGameStateInstance(AccessTools.TypeByName("GameState"));
            var room = GetCurrentRoom();
            var info = GetFieldOrPropertyValue(action, "info");
            var actionType = GetFieldOrPropertyValue(info, "levelActionType");
            DiagnosticLog.Warning(
                "Suppressed repeated Workshop level-end action while the native level switch " +
                "was already in progress; action=" + actionType +
                "; scene=" + SceneManager.GetActiveScene().name +
                "; stateLevel=" +
                (state == null ? "n/a" : GetIntFieldOrProperty(state, "levelNumber").ToString()) +
                "; roomLevel=" +
                (room == null ? "n/a" : GetRoomInfoInt(room, "levelNumber", -1).ToString()) +
                "; nextScene=" + GetStringFieldOrProperty(controller, "nextScene") + ".");
        }

        private static void LogSuppressedWorkshopOutcomeReentry(object controller)
        {
            var state = GetGameStateInstance(AccessTools.TypeByName("GameState"));
            var room = GetCurrentRoom();
            DiagnosticLog.Warning(
                "Suppressed repeated Workshop success outcome calculation while the native " +
                "level switch was already in progress; scene=" +
                SceneManager.GetActiveScene().name +
                "; stateLevel=" +
                (state == null ? "n/a" : GetIntFieldOrProperty(state, "levelNumber").ToString()) +
                "; roomLevel=" +
                (room == null ? "n/a" : GetRoomInfoInt(room, "levelNumber", -1).ToString()) +
                "; nextScene=" + GetStringFieldOrProperty(controller, "nextScene") + ".");
        }
    }
}
