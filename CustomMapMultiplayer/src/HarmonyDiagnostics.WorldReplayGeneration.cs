using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;

namespace CustomMapMultiplayer
{
    internal static partial class HarmonyDiagnostics
    {
        private static int _worldReplayGenerationDepth;

        private static bool IsWorldReplayFactory(MethodInfo method)
        {
            return method != null && method.IsStatic &&
                (method.DeclaringType == typeof(MapController) && method.Name.StartsWith("Spawn", StringComparison.Ordinal) ||
                 method.DeclaringType == typeof(global::Networking.Networking) && method.Name.StartsWith("Instantiate", StringComparison.Ordinal));
        }

        private static bool CanSkipWorldReplayFactory(IList<CodeInstruction> code, int i)
        {
            var method = code[i].operand as MethodInfo;
            // Never substitute null for a value that the original caller will use.
            // APC/Crate lifecycle factories discard their return; component setup
            // factories (e.g. grenade summons) retain their original call contract.
            return IsWorldReplayFactory(method) && (method.ReturnType == typeof(void) ||
                !method.ReturnType.IsValueType && i + 1 < code.Count &&
                (code[i + 1].opcode == OpCodes.Pop || IsWorldUnusedFactoryLocal(code, i + 1)));
        }

        private static int WorldReplayLocalIndex(CodeInstruction instruction, bool store)
        {
            var op = instruction.opcode;
            if (op == (store ? OpCodes.Stloc_0 : OpCodes.Ldloc_0)) return 0;
            if (op == (store ? OpCodes.Stloc_1 : OpCodes.Ldloc_1)) return 1;
            if (op == (store ? OpCodes.Stloc_2 : OpCodes.Ldloc_2)) return 2;
            if (op == (store ? OpCodes.Stloc_3 : OpCodes.Ldloc_3)) return 3;
            if (store ? op != OpCodes.Stloc && op != OpCodes.Stloc_S :
                op != OpCodes.Ldloc && op != OpCodes.Ldloc_S && op != OpCodes.Ldloca && op != OpCodes.Ldloca_S) return -1;
            var local = instruction.operand as LocalBuilder;
            if (local != null) return local.LocalIndex;
            var info = instruction.operand as LocalVariableInfo;
            return info != null ? info.LocalIndex : Convert.ToInt32(instruction.operand);
        }

        private static bool IsWorldUnusedFactoryLocal(IList<CodeInstruction> code, int store)
        {
            // The shipped APC binary stores its first soldier in an unread local.
            // Only omit that factory if no instruction ever reads the local or
            // takes its address; used return values keep their native contract.
            var local = WorldReplayLocalIndex(code[store], true);
            if (local < 0) return false;
            for (var i = 0; i < code.Count; i++)
                if (WorldReplayLocalIndex(code[i], false) == local) return false;
            return true;
        }

        private static void PatchWorldReplayGeneration()
        {
            var count = 0;
            foreach (var type in typeof(Unit).Assembly.GetTypes())
            {
                if (!typeof(BroforceObject).IsAssignableFrom(type)) continue;
                foreach (var method in type.GetMethods(WorldFields | BindingFlags.DeclaredOnly))
                {
                    if (method.IsAbstract || method.IsStatic || method.ContainsGenericParameters ||
                        !(method.Name == "Death" || method.Name == "QuietDeath" || method.Name == "Gib" ||
                          method.Name == "MakeEffects" || method.Name == "DestroyBlockInternal" || method.Name == "ActuallyCollapse")) continue;
                    var code = PatchProcessor.GetOriginalInstructions(method, null);
                    var found = false;
                    for (var i = 0; i < code.Count; i++) found |= CanSkipWorldReplayFactory(code, i);
                    if (!found) continue;
                    PatchWorldMethod(method, "WorldReplayGenerationScopePrefix", null,
                        "WorldReplayGenerationTranspiler", "WorldReplayGenerationScopeFinalizer");
                    count++;
                }
            }
            // Money is a void public factory, including helpers invoked by death
            // callbacks outside the BroforceObject hierarchy. Existing pickups are
            // still launched normally; neither Launch nor native InstantiateRPC is blocked.
            PatchWorldMethod(AccessTools.Method(typeof(PickupableController), "CreateMoneySpray", null, null),
                "WorldReplayGenerationPrefix", null);
            DiagnosticLog.Info("WORLD_STATE replay generation boundary enabled; lifecycle methods=" + count + ".");
        }

        private static void WorldReplayGenerationScopePrefix(BroforceObject __instance, out bool __state)
        {
            // Native collapse confirmation also calls MakeEffects outside the
            // state-application scope. Its replica still releases existing items,
            // but must not create a second set of networked rewards.
            __state = _worldApplying != 0 || IsWorldReplica(__instance);
            if (__state) _worldReplayGenerationDepth++;
        }

        private static void WorldReplayGenerationScopeFinalizer(bool __state)
        {
            if (__state) _worldReplayGenerationDepth--;
        }

        private static bool WorldReplayGenerationPrefix() { return _worldApplying == 0 && _worldReplayGenerationDepth == 0; }

        private static IEnumerable<CodeInstruction> WorldReplayGenerationTranspiler(IEnumerable<CodeInstruction> instructions, ILGenerator generator)
        {
            var code = new List<CodeInstruction>(instructions);
            var result = new List<CodeInstruction>();
            var allow = AccessTools.Method(typeof(HarmonyDiagnostics), "WorldReplayGenerationPrefix", null, null);
            for (var i = 0; i < code.Count; i++)
            {
                var call = code[i];
                if (!CanSkipWorldReplayFactory(code, i)) { result.Add(call); continue; }
                var method = (MethodInfo)call.operand;
                var run = generator.DefineLabel();
                var done = generator.DefineLabel();
                var guard = new CodeInstruction(OpCodes.Call, allow);
                guard.labels.AddRange(call.labels); call.labels.Clear();
                guard.blocks.AddRange(call.blocks); call.blocks.Clear();
                result.Add(guard);
                result.Add(new CodeInstruction(OpCodes.Brtrue, run));
                foreach (var parameter in method.GetParameters()) result.Add(new CodeInstruction(OpCodes.Pop, null));
                if (method.ReturnType != typeof(void)) result.Add(new CodeInstruction(OpCodes.Ldnull, null));
                result.Add(new CodeInstruction(OpCodes.Br, done));
                call.labels.Add(run);
                result.Add(call);
                var end = new CodeInstruction(OpCodes.Nop, null); end.labels.Add(done);
                result.Add(end);
            }
            return result;
        }
    }
}
