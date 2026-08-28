using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using Networking;
using UnityEngine;

namespace CustomMapMultiplayer
{
    // Synchronizes only McBrover's currentTurkey active detonation.
    internal static partial class HarmonyDiagnostics
    {
        private static readonly HashSet<NID> SentMcBroverTurkeyDetonations =
            new HashSet<NID>();
        private static readonly HashSet<NID> AppliedMcBroverTurkeyDetonations =
            new HashSet<NID>();
        private static readonly HashSet<NID> AppliedMcBroverTurkeyEffects =
            new HashSet<NID>();
        private static readonly Dictionary<NID, PendingMcBroverTurkeyDetonation> PendingMcBroverTurkeyDetonations =
            new Dictionary<NID, PendingMcBroverTurkeyDetonation>();
        private const int McBroverTurkeyLookupTimeoutMilliseconds = 2000;

        private static void PatchMcBroverTurkeyDetonationSynchronization()
        {
            try
            {
                var useSpecial = typeof(McBrover).GetMethod("UseSpecial", BindingFlags.Instance | BindingFlags.NonPublic, null, Type.EmptyTypes, null);
                var transpiler = typeof(HarmonyDiagnostics).GetMethod("McBroverUseSpecialTranspiler", BindingFlags.NonPublic | BindingFlags.Static);
                if (useSpecial == null || transpiler == null)
                {
                    DiagnosticLog.Warning("MCBROVER_TURKEY McBrover.UseSpecial hook could not be resolved.");
                    return;
                }

                _harmony.Patch(useSpecial, null, null, new HarmonyMethod(transpiler), null);
                var spawn = typeof(ProjectileController).GetMethod("SpawnProjectileOverNetwork", BindingFlags.Public | BindingFlags.Static, null,
                    new[] { typeof(Projectile), typeof(MonoBehaviour), typeof(float), typeof(float), typeof(float), typeof(float), typeof(bool), typeof(int), typeof(bool), typeof(bool), typeof(float) }, null);
                var spawnPostfix = typeof(HarmonyDiagnostics).GetMethod("McBroverTurkeySpawnPostfix", BindingFlags.NonPublic | BindingFlags.Static);
                if (spawn != null && spawnPostfix != null)
                {
                    _harmony.Patch(spawn, null, new HarmonyMethod(spawnPostfix), null, null);
                }
                else
                {
                    DiagnosticLog.Warning("MCBROVER_TURKEY creation logging hook could not be resolved.");
                }

                var onDestroy = typeof(SachelPack).GetMethod(
                    "OnDestroy",
                    BindingFlags.Instance | BindingFlags.NonPublic);
                var onDestroyPrefix = typeof(HarmonyDiagnostics).GetMethod(
                    "McBroverTurkeyOnDestroyPrefix",
                    BindingFlags.NonPublic | BindingFlags.Static);
                var onDestroyPostfix = typeof(HarmonyDiagnostics).GetMethod(
                    "McBroverTurkeyOnDestroyPostfix",
                    BindingFlags.NonPublic | BindingFlags.Static);
                if (onDestroy != null && onDestroyPrefix != null && onDestroyPostfix != null)
                {
                    _harmony.Patch(
                        onDestroy,
                        new HarmonyMethod(onDestroyPrefix),
                        new HarmonyMethod(onDestroyPostfix),
                        null,
                        null);
                }
                else
                {
                    DiagnosticLog.Warning(
                        "MCBROVER_TURKEY final destruction logging hook could not be resolved.");
                }

                var makeEffects = typeof(SachelPackTurkey).GetMethod(
                    "MakeEffects",
                    BindingFlags.Instance | BindingFlags.NonPublic);
                var makeEffectsPrefix = typeof(HarmonyDiagnostics).GetMethod(
                    "McBroverTurkeyMakeEffectsPrefix",
                    BindingFlags.NonPublic | BindingFlags.Static);
                if (makeEffects != null && makeEffectsPrefix != null)
                {
                    _harmony.Patch(
                        makeEffects,
                        new HarmonyMethod(makeEffectsPrefix),
                        null,
                        null,
                        null);
                }
                else
                {
                    DiagnosticLog.Warning(
                        "MCBROVER_TURKEY effect idempotency hook could not be resolved.");
                }

                DiagnosticLog.Info("MCBROVER_TURKEY active detonation synchronization enabled.");
            }
            catch (Exception exception)
            {
                DiagnosticLog.Warning("MCBROVER_TURKEY synchronization patch failed: " + exception);
            }
        }

        private static IEnumerable<CodeInstruction> McBroverUseSpecialTranspiler(IEnumerable<CodeInstruction> instructions)
        {
            var result = new List<CodeInstruction>(instructions);
            var projectileDeath = typeof(Projectile).GetMethod("Death", BindingFlags.Instance | BindingFlags.Public, null, Type.EmptyTypes, null);
            var detonate = typeof(HarmonyDiagnostics).GetMethod("DetonateMcBroverTurkey", BindingFlags.NonPublic | BindingFlags.Static);
            if (projectileDeath == null || detonate == null)
            {
                DiagnosticLog.Warning("MCBROVER_TURKEY transpiler could not resolve detonation methods.");
                return result;
            }

            var deathCallIndex = -1;
            var deathCallCount = 0;
            for (var index = 0; index < result.Count; index++)
            {
                if (projectileDeath.Equals(result[index].operand as MethodInfo))
                {
                    deathCallIndex = index;
                    deathCallCount++;
                }
            }

            if (deathCallCount != 1)
            {
                DiagnosticLog.Warning("MCBROVER_TURKEY expected one Projectile.Death call in McBrover.UseSpecial; found " + deathCallCount + ".");
                return result;
            }

            result[deathCallIndex].opcode = OpCodes.Call;
            result[deathCallIndex].operand = detonate;
            return result;
        }

        private static void McBroverTurkeySpawnPostfix(Projectile __result, MonoBehaviour FiredBy)
        {
            var turkey = __result as SachelPackTurkey;
            if (turkey == null || !(FiredBy is McBrover))
            {
                return;
            }

            try
            {
                var nid = Registry.GetNID(turkey);
                DiagnosticLog.Info("MCBROVER_TURKEY owner created; nid=" + nid + "; isMine=" + turkey.IsMine + "; position=" + turkey.X + "," + turkey.Y + ".");
            }
            catch (Exception exception)
            {
                DiagnosticLog.Warning("MCBROVER_TURKEY owner creation logging failed: " + exception.Message + ".");
            }
        }

        private static void McBroverTurkeyOnDestroyPrefix(
            SachelPack __instance,
            ref NID __state)
        {
            var turkey = __instance as SachelPackTurkey;
            __state = turkey == null || !(turkey.firedBy is McBrover)
                ? NID.NoID
                : Registry.GetNID(turkey);
        }

        private static void McBroverTurkeyOnDestroyPostfix(
            SachelPack __instance,
            NID __state)
        {
            if (__state == NID.NoID)
            {
                return;
            }

            DiagnosticLog.Info(
                "MCBROVER_TURKEY final OnDestroy completed; nid=" + __state + ".");
        }

        private static bool McBroverTurkeyMakeEffectsPrefix(
            SachelPackTurkey __instance)
        {
            if (__instance == null || !(__instance.firedBy is McBrover))
            {
                return true;
            }

            var nid = Registry.GetNID(__instance);
            if (nid == NID.NoID)
            {
                return true;
            }
            if (!AppliedMcBroverTurkeyDetonations.Contains(nid))
            {
                AppliedMcBroverTurkeyEffects.Add(nid);
                return true;
            }

            if (AppliedMcBroverTurkeyEffects.Add(nid))
            {
                return true;
            }

            DiagnosticLog.Info(
                "MCBROVER_TURKEY duplicate remote explosion effect ignored; nid=" + nid + ".");
            return false;
        }

        private static void DetonateMcBroverTurkey(Projectile projectile)
        {
            var turkey = projectile as SachelPackTurkey;
            if (turkey == null)
            {
                if (projectile != null)
                {
                    projectile.Death();
                }
                return;
            }

            try
            {
                if (IsMcBroverTurkeyDetonationSession() && turkey.IsMine)
                {
                    var nid = Registry.GetNID(turkey);
                    if (nid == NID.NoID)
                    {
                        DiagnosticLog.Warning("MCBROVER_TURKEY owner NID unregistered; original Death continues.");
                    }
                    else if (!SentMcBroverTurkeyDetonations.Add(nid))
                    {
                        DiagnosticLog.Info("MCBROVER_TURKEY duplicate event ignored; side=owner; nid=" + nid + ".");
                        return;
                    }
                    else
                    {
                        var x = turkey.X;
                        var y = turkey.Y;
                        global::Networking.Networking.RPC<NID, float, float>(PID.TargetOthers, new RpcSignature<NID, float, float>(ApplyMcBroverTurkeyDetonationRPC), nid, x, y, false);
                        DiagnosticLog.Info("MCBROVER_TURKEY owner send; nid=" + nid + "; position=" + x + "," + y + ".");
                    }
                }
            }
            catch (Exception exception)
            {
                DiagnosticLog.Warning("MCBROVER_TURKEY owner send failed; original Death continues: " + exception);
            }

            turkey.Death();
            DiagnosticLog.Info("MCBROVER_TURKEY owner local Death executed; nid=" + Registry.GetNID(turkey) + "; destroyed=" + (turkey == null || turkey.gameObject == null) + ".");
        }

        [AllowedRPC]
        private static void ApplyMcBroverTurkeyDetonationRPC(NID nid, float x, float y)
        {
            if (!IsMcBroverTurkeyDetonationSession() || nid == NID.NoID)
            {
                return;
            }

            if (AppliedMcBroverTurkeyDetonations.Contains(nid))
            {
                DiagnosticLog.Info("MCBROVER_TURKEY duplicate event ignored; side=remote; nid=" + nid + ".");
                return;
            }

            if (TryApplyMcBroverTurkeyDetonation(nid, x, y))
            {
                return;
            }

            PendingMcBroverTurkeyDetonations[nid] = new PendingMcBroverTurkeyDetonation(
                x,
                y,
                DateTime.UtcNow.AddMilliseconds(McBroverTurkeyLookupTimeoutMilliseconds));
            DiagnosticLog.Warning("MCBROVER_TURKEY remote NID not registered yet; retry queued; nid=" + nid + ".");
        }

        private static bool TryApplyMcBroverTurkeyDetonation(NID nid, float x, float y)
        {
            Projectile projectile = null;
            try
            {
                projectile = Registry.GetObject(nid) as Projectile;
            }
            catch (Exception exception)
            {
                DiagnosticLog.Warning("MCBROVER_TURKEY remote NID lookup failed; nid=" + nid + "; error=" + exception.Message + ".");
            }

            var turkey = projectile as SachelPackTurkey;
            if (turkey == null || turkey.gameObject == null)
            {
                return false;
            }

            AppliedMcBroverTurkeyDetonations.Add(nid);
            PendingMcBroverTurkeyDetonations.Remove(nid);
            turkey.Position = new Vector2(x, y);
            turkey.Death();
            DiagnosticLog.Info("MCBROVER_TURKEY remote NID hit and Death executed; nid=" + nid + "; position=" + x + "," + y + ".");
            return true;
        }

        private static void TryApplyPendingMcBroverTurkeyDetonations()
        {
            if (PendingMcBroverTurkeyDetonations.Count == 0)
            {
                return;
            }

            var pendingNids = new List<NID>(PendingMcBroverTurkeyDetonations.Keys);
            foreach (var nid in pendingNids)
            {
                PendingMcBroverTurkeyDetonation pending;
                if (!PendingMcBroverTurkeyDetonations.TryGetValue(nid, out pending))
                {
                    continue;
                }

                if (TryApplyMcBroverTurkeyDetonation(nid, pending.X, pending.Y))
                {
                    continue;
                }

                if (DateTime.UtcNow < pending.DeadlineUtc)
                {
                    continue;
                }

                PendingMcBroverTurkeyDetonations.Remove(nid);
                DiagnosticLog.Warning("MCBROVER_TURKEY remote NID remained unregistered; residual risk detected; nid=" + nid + ".");
            }
        }

        private static bool IsMcBroverTurkeyDetonationSession()
        {
            return _networkSessionActive && IsOnline() && PID.MyIdHasBeenSet && PID.ServerHasBeenSet;
        }

        private static void ClearMcBroverTurkeyDetonationState()
        {
            SentMcBroverTurkeyDetonations.Clear();
            AppliedMcBroverTurkeyDetonations.Clear();
            AppliedMcBroverTurkeyEffects.Clear();
            PendingMcBroverTurkeyDetonations.Clear();
        }

        private struct PendingMcBroverTurkeyDetonation
        {
            internal readonly float X;
            internal readonly float Y;
            internal readonly DateTime DeadlineUtc;

            internal PendingMcBroverTurkeyDetonation(float x, float y, DateTime deadlineUtc)
            {
                X = x;
                Y = y;
                DeadlineUtc = deadlineUtc;
            }
        }
    }
}
