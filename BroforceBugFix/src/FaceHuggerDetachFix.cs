using System.Collections.Generic;
using System.Reflection.Emit;
using HarmonyLib;

namespace BroforceBugFix
{
    internal static class FaceHuggerDetachFix
    {
        private const string HarmonyId = "GJKen.BroforceBugFix.FaceHuggerDetach";
        private static Harmony _harmony;

        internal static void Apply()
        {
            if (_harmony != null) return;
            var harmony = new Harmony(HarmonyId);
            harmony.Patch(AccessTools.Method(typeof(AlienFaceHugger), "DisconnectFaceHugger", null, null),
                null, null, new HarmonyMethod(typeof(FaceHuggerDetachFix), "Transpile", null), null);
            _harmony = harmony;
            Plugin.Log("Facehugger detach fix enabled.");
        }

        internal static void Remove()
        {
            if (_harmony == null) return;
            _harmony.UnpatchAll(HarmonyId);
            _harmony = null;
            Plugin.Log("Facehugger detach fix disabled.");
        }

        private static IEnumerable<CodeInstruction> Transpile(IEnumerable<CodeInstruction> instructions)
        {
            var original = AccessTools.Method(typeof(Unit), "IsFlexing", null, null);
            var replacement = AccessTools.Method(typeof(FaceHuggerDetachFix), "TargetIsFlexing", null, null);
            foreach (var instruction in instructions)
            {
                if (instruction.Calls(original))
                {
                    instruction.opcode = OpCodes.Call;
                    instruction.operand = replacement;
                }
                yield return instruction;
            }
        }

        private static bool TargetIsFlexing(Unit target)
        {
            // Broforce's snake can retain a victim-side link after losing its target.
            // Throwing here aborts that victim's cleanup and repeats Death/blood each frame.
            // Keep the native detach/launch sequence, treating a missing target as not flexing.
            return target != null && target.IsFlexing();
        }
    }
}
