using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Reflection;

namespace CustomMapMultiplayer
{
    internal static partial class HarmonyDiagnostics
    {
        private static void PatchPlayerOverlapHighFiveBehavior()
        {
            try
            {
                var pressHighFiveMelee = typeof(TestVanDammeAnim).GetMethod(
                    "PressHighFiveMelee",
                    BindingFlags.Instance | BindingFlags.NonPublic,
                    null,
                    new[] { typeof(bool) },
                    null);
                var transpiler = typeof(HarmonyDiagnostics).GetMethod(
                    "PressHighFiveMeleeTranspiler",
                    BindingFlags.NonPublic | BindingFlags.Static);
                if (pressHighFiveMelee == null || transpiler == null)
                {
                    DiagnosticLog.Warning(
                        "Player-overlap melee patch could not resolve TestVanDammeAnim.PressHighFiveMelee.");
                    return;
                }

                _harmony.Patch(
                    pressHighFiveMelee,
                    null,
                    null,
                    new HarmonyMethod(transpiler),
                    null);
                DiagnosticLog.Info(
                    "Player-overlap melee behavior enabled; UMM setting controls automatic high-five suppression.");
            }
            catch (Exception exception)
            {
                DiagnosticLog.Warning(
                    "Player-overlap melee patch failed: " + exception);
            }
        }

        private static IEnumerable<CodeInstruction> PressHighFiveMeleeTranspiler(
            IEnumerable<CodeInstruction> instructions)
        {
            var result = new List<CodeInstruction>(instructions);
            var playerNearby = typeof(HeroController).GetMethod(
                "IsAnotherPlayerNearby",
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static,
                null,
                new[] { typeof(int), typeof(float), typeof(float), typeof(float), typeof(float) },
                null);
            var replacement = typeof(HarmonyDiagnostics).GetMethod(
                "IsAnotherPlayerNearbyForHighFive",
                BindingFlags.NonPublic | BindingFlags.Static);
            if (playerNearby == null || replacement == null)
            {
                DiagnosticLog.Warning(
                    "Player-overlap melee transpiler could not resolve the nearby-player methods.");
                return result;
            }

            var replacedCount = 0;
            for (var index = 0; index < result.Count; index++)
            {
                if (!playerNearby.Equals(result[index].operand as MethodInfo))
                {
                    continue;
                }

                result[index].operand = replacement;
                replacedCount++;
            }

            if (replacedCount != 1)
            {
                DiagnosticLog.Warning(
                    "Player-overlap melee transpiler expected one nearby-player check; found " +
                    replacedCount + ".");
            }

            return result;
        }

        private static bool IsAnotherPlayerNearbyForHighFive(
            int currentPlayerNum,
            float x,
            float y,
            float xRange,
            float yRange)
        {
            var settings = Plugin.Settings;
            if (settings != null && settings.DisablePlayerOverlapHighFive)
            {
                return false;
            }

            return HeroController.IsAnotherPlayerNearby(
                currentPlayerNum,
                x,
                y,
                xRange,
                yRange);
        }
    }
}
