using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;

namespace TestMod
{
    [HarmonyPatch(typeof(HeroController), "GetGetFollowPosition")]
    internal static class CameraFollowPatch
    {
        [HarmonyTranspiler]
        private static IEnumerable<CodeInstruction> FilterFollowPlayers(
            IEnumerable<CodeInstruction> instructions)
        {
            var result = new List<CodeInstruction>(instructions);
            var isPlaying = typeof(HeroController).GetMethod("IsPlaying",
                BindingFlags.Public | BindingFlags.Static, null, new[] { typeof(int) }, null);
            var includePlayer = typeof(Plugin).GetMethod("IsPlayerIncludedInCameraFollow",
                BindingFlags.NonPublic | BindingFlags.Static);
            if (isPlaying == null || includePlayer == null)
            {
                Plugin.Log("Camera follow patch could not resolve the player filter methods.");
                return result;
            }

            var replacements = 0;
            for (var index = 0; index < result.Count; index++)
            {
                var instruction = result[index];
                if (instruction.opcode == OpCodes.Call && isPlaying.Equals(instruction.operand as MethodInfo))
                {
                    instruction.operand = includePlayer;
                    replacements++;
                }
            }

            if (replacements == 0)
                Plugin.Log("Camera follow patch found no player checks in HeroController.GetGetFollowPosition.");

            return result;
        }
    }
}
