using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using UnityEngine.SceneManagement;

namespace CustomMapMultiplayer
{
    // Low-frequency online level outcome diagnostics. These patches only observe state.
    internal static partial class HarmonyDiagnostics
    {
        private static void PatchLevelOutcomeDiagnostics()
        {
            var levelFinishPrefixMethod = GetPatchMethod("LevelFinishOutcomePrefix");
            var levelFinishPostfixMethod = GetPatchMethod("LevelFinishOutcomePostfix");
            var removeLifePrefixMethod = GetPatchMethod("RemoveLifeOutcomePrefix");
            var removeLifePostfixMethod = GetPatchMethod("RemoveLifeOutcomePostfix");
            if (levelFinishPrefixMethod == null || levelFinishPostfixMethod == null ||
                removeLifePrefixMethod == null || removeLifePostfixMethod == null)
            {
                DiagnosticLog.Warning("Level outcome diagnostics could not resolve patch methods.");
                return;
            }

            var patchedCount = 0;
            patchedCount += PatchLevelOutcomeMethods(
                AccessTools.TypeByName("GameModeController"),
                "LevelFinish",
                levelFinishPrefixMethod,
                levelFinishPostfixMethod);
            patchedCount += PatchLevelOutcomeMethods(
                AccessTools.TypeByName("Player"),
                "RemoveLife",
                removeLifePrefixMethod,
                removeLifePostfixMethod);

            if (patchedCount == 0)
            {
                DiagnosticLog.Warning(
                    "Level outcome diagnostics could not find GameModeController.LevelFinish or Player.RemoveLife.");
            }
            else
            {
                DiagnosticLog.Info(
                    "Level outcome diagnostics enabled; patched methods=" + patchedCount + ".");
            }
        }

        private static int PatchLevelOutcomeMethods(
            Type type,
            string methodName,
            MethodInfo prefixMethod,
            MethodInfo postfixMethod)
        {
            if (type == null)
            {
                return 0;
            }

            var patchedCount = 0;
            var methods = type.GetMethods(
                BindingFlags.Public |
                BindingFlags.NonPublic |
                BindingFlags.Instance |
                BindingFlags.Static |
                BindingFlags.DeclaredOnly);
            foreach (var method in methods)
            {
                if (method.Name != methodName || method.IsAbstract || method.ContainsGenericParameters)
                {
                    continue;
                }

                try
                {
                    _harmony.Patch(
                        method,
                        new HarmonyMethod(prefixMethod),
                        new HarmonyMethod(postfixMethod),
                        null,
                        null);
                    patchedCount++;
                }
                catch (Exception exception)
                {
                    DiagnosticLog.Warning(
                        "Level outcome patch failed for " + DescribeMethod(method) + ": " + exception);
                }
            }

            return patchedCount;
        }

        private static void LevelFinishOutcomePrefix(
            MethodBase __originalMethod,
            object[] __args,
            out LevelOutcomeObservation __state)
        {
            __state = CreateLevelOutcomeObservation(__originalMethod, null, null, __args);
        }

        private static void LevelFinishOutcomePostfix(
            LevelOutcomeObservation __state)
        {
            CompleteLevelOutcomeObservation(null, null, __state);
        }

        private static void RemoveLifeOutcomePrefix(
            MethodBase __originalMethod,
            Player __instance,
            object[] __args,
            out LevelOutcomeObservation __state)
        {
            __state = CreateLevelOutcomeObservation(__originalMethod, __instance, __instance, __args);
        }

        private static void RemoveLifeOutcomePostfix(
            Player __instance,
            LevelOutcomeObservation __state)
        {
            CompleteLevelOutcomeObservation(__instance, __instance, __state);
        }

        private static LevelOutcomeObservation CreateLevelOutcomeObservation(
            MethodBase method,
            object instance,
            Player player,
            object[] arguments)
        {
            try
            {
                if (!_networkSessionActive || !IsOnline() ||
                    method == null || method.DeclaringType == null)
                {
                    return null;
                }

                return new LevelOutcomeObservation(
                    DescribeMethod(method),
                    DescribeLevelOutcomeArguments(method, arguments),
                    BuildLevelOutcomeSnapshot(instance, player));
            }
            catch (Exception exception)
            {
                DiagnosticLog.Warning("Level outcome pre-state capture failed: " + exception.Message);
                return null;
            }
        }

        private static void CompleteLevelOutcomeObservation(
            object instance,
            Player player,
            LevelOutcomeObservation observation)
        {
            if (observation == null)
            {
                return;
            }

            try
            {
                var after = BuildLevelOutcomeSnapshot(instance, player);
                var message =
                    "LEVEL_OUTCOME method=" + observation.Method +
                    "; args={" + observation.Arguments +
                    "}; before={" + observation.Before +
                    "}; after={" + after + "}";
                DiagnosticLog.Info(message);
                DiagnosticLog.Trace(message);
            }
            catch (Exception exception)
            {
                DiagnosticLog.Warning("Level outcome post-state capture failed: " + exception.Message);
            }
        }

        private static string DescribeLevelOutcomeArguments(MethodBase method, object[] arguments)
        {
            if (method == null)
            {
                return string.Empty;
            }

            var builder = new StringBuilder();
            var parameters = method.GetParameters();
            for (var index = 0; index < parameters.Length; index++)
            {
                if (index > 0)
                {
                    builder.Append(",");
                }

                builder.Append(parameters[index].Name)
                    .Append("=");
                var value = arguments != null && index < arguments.Length ? arguments[index] : null;
                builder.Append(FormatOutcomeValue(value));
            }

            return builder.ToString();
        }

        private static string BuildLevelOutcomeSnapshot(object instance, Player player)
        {
            var controller = instance != null && instance.GetType().Name == "GameModeController"
                ? instance
                : GetStaticFieldOrPropertyValue(AccessTools.TypeByName("GameModeController"), "Instance");
            if (controller == null)
            {
                controller = GetStaticFieldOrPropertyValue(
                    AccessTools.TypeByName("GameModeController"),
                    "instance");
            }
            var state = GetGameStateInstance(AccessTools.TypeByName("GameState"));
            var room = GetCurrentRoom();
            var builder = new StringBuilder();

            builder.Append("scene=")
                .Append(Sanitize(SceneManager.GetActiveScene().name ?? string.Empty, 80))
                .Append(";player=")
                .Append(player == null ? "n/a" : player.playerNum.ToString())
                .Append(";lives=")
                .Append(player == null ? "n/a" : GetIntFieldOrProperty(player, "lives").ToString());
            AppendHeroCounts(builder);
            builder.Append(";helicopter=")
                .Append(ReadHeroCount(delegate { return HeroController.GetPlayersOnHelicopterAmount(); }))
                .Append(";levelFinished=")
                .Append(FormatOutcomeValue(GetFieldOrPropertyValue(controller, "levelFinished")))
                .Append(";switchingLevel=")
                .Append(FormatOutcomeValue(GetFieldOrPropertyValue(controller, "switchingLevel")))
                .Append(";waitingForReady=")
                .Append(FormatOutcomeValue(GetFieldOrPropertyValue(controller, "waitingForAllPlayersToReady")))
                .Append(";nextScene=")
                .Append(FormatOutcomeValue(GetFieldOrPropertyValue(controller, "nextScene")))
                .Append(";stateLevel=")
                .Append(state == null ? "n/a" : GetIntFieldOrProperty(state, "levelNumber").ToString())
                .Append(";stateScene=");
            var stateScene = GetStringFieldOrProperty(state, "_sceneToLoad");
            if (string.IsNullOrEmpty(stateScene))
            {
                stateScene = GetStringFieldOrProperty(state, "sceneToLoad");
            }
            builder.Append(string.IsNullOrEmpty(stateScene) ? "n/a" : Sanitize(stateScene, 80))
                .Append(";stateMode=")
                .Append(FormatOutcomeValue(GetFieldOrPropertyValue(state, "gameMode")))
                .Append(";stateLoadMode=")
                .Append(FormatOutcomeValue(GetFieldOrPropertyValue(state, "loadMode")))
                .Append(";roomLevel=")
                .Append(room == null ? "n/a" : GetRoomLevelNumber(room, -1).ToString())
                .Append(";roomScene=");
            var roomScene = room == null ? string.Empty : TrimRoomInfoString(room.CurrentSceneName);
            builder.Append(string.IsNullOrEmpty(roomScene) ? "n/a" : Sanitize(roomScene, 80))
                .Append(";roomMode=")
                .Append(FormatOutcomeValue(GetFieldOrPropertyValue(room, "gameMode")));
            return builder.ToString();
        }

        private static object GetStaticFieldOrPropertyValue(Type type, string name)
        {
            if (type == null)
            {
                return null;
            }

            try
            {
                var field = type.GetField(
                    name,
                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
                if (field != null)
                {
                    return field.GetValue(null);
                }

                var property = type.GetProperty(
                    name,
                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
                return property == null || !property.CanRead ? null : property.GetValue(null, null);
            }
            catch
            {
                return null;
            }
        }

        private static void AppendHeroCounts(StringBuilder builder)
        {
            builder.Append(";alive=")
                .Append(ReadHeroCount(delegate { return HeroController.GetPlayersAliveCount(); }))
                .Append(";local=")
                .Append(ReadHeroCount(delegate { return HeroController.GetLocalPlayerCount(); }))
                .Append(";totalLives=")
                .Append(ReadHeroCount(delegate { return HeroController.GetTotalLives(); }));
        }

        private static string ReadHeroCount(Func<int> reader)
        {
            try
            {
                return reader().ToString();
            }
            catch (Exception exception)
            {
                return "error:" + exception.GetType().Name;
            }
        }

        private static string FormatOutcomeValue(object value)
        {
            if (value == null)
            {
                return "n/a";
            }

            var type = value.GetType();
            if (type.IsEnum || type.IsPrimitive || value is decimal)
            {
                return Convert.ToString(value, System.Globalization.CultureInfo.InvariantCulture);
            }

            return Sanitize(Convert.ToString(value), 100);
        }

        private sealed class LevelOutcomeObservation
        {
            public LevelOutcomeObservation(string method, string arguments, string before)
            {
                Method = method;
                Arguments = arguments;
                Before = before;
            }

            public string Method { get; private set; }
            public string Arguments { get; private set; }
            public string Before { get; private set; }
        }
    }
}
