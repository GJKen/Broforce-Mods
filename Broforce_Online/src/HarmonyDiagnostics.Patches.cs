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
    // Harmony 补丁安装与 IL 织入：各目标方法的 Patch 注册和 transpiler。
    internal static partial class HarmonyDiagnostics
    {
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

        private static void PatchOnlineAfkPrevention()
        {
            var updateMethod = typeof(Player).GetMethod(
                "Update",
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance,
                null,
                Type.EmptyTypes,
                null);
            var idleTimerField = typeof(Player).GetField(
                "idleTimer",
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            var prefixMethod = typeof(HarmonyDiagnostics).GetMethod(
                "PlayerUpdateAfkPreventionPrefix",
                BindingFlags.NonPublic | BindingFlags.Static);
            if (updateMethod == null || idleTimerField == null || prefixMethod == null)
            {
                DiagnosticLog.Warning(
                    "Online AFK prevention patch could not resolve Player.Update or Player.idleTimer.");
                return;
            }

            try
            {
                _harmony.Patch(updateMethod, new HarmonyMethod(prefixMethod), null, null, null);
                DiagnosticLog.Info(
                    "Online AFK prevention patch enabled; behavior is controlled by the UMM setting.");
            }
            catch (Exception exception)
            {
                DiagnosticLog.Warning(
                    "Online AFK prevention patch failed: " + exception);
            }
        }

        private static void PlayerUpdateAfkPreventionPrefix(Player __instance)
        {
            var settings = Plugin.Settings;
            if (__instance == null || settings == null ||
                !settings.DisableOnlineAfkSpectatorMode || !IsOnline() || !__instance.IsMine)
            {
                return;
            }

            SetFieldOrProperty(__instance, "idleTimer", 0f);
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
    }
}
