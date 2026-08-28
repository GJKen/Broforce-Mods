using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using Networking;
using UnityEngine;

namespace BroforceOnlineDiagnostics
{
    // Only synchronizes DemolitionBro's second-press currentBomb detonation.
    internal static partial class HarmonyDiagnostics
    {
        private static readonly HashSet<NID> SentDemolitionBombDetonations =
            new HashSet<NID>();
        private static readonly HashSet<NID> AppliedDemolitionBombDetonations =
            new HashSet<NID>();

        private static void PatchDemolitionBroBombDetonationSynchronization()
        {
            try
            {
                var useFire = typeof(DemolitionBro).GetMethod(
                    "UseFire",
                    BindingFlags.Instance | BindingFlags.NonPublic,
                    null,
                    Type.EmptyTypes,
                    null);
                var transpiler = typeof(HarmonyDiagnostics).GetMethod(
                    "DemolitionBroUseFireTranspiler",
                    BindingFlags.NonPublic | BindingFlags.Static);
                if (useFire == null || transpiler == null)
                {
                    DiagnosticLog.Warning(
                        "DEMOLITION_BOMB DemolitionBro.UseFire hook could not be resolved.");
                    return;
                }

                _harmony.Patch(useFire, null, null, new HarmonyMethod(transpiler), null);
                DiagnosticLog.Info(
                    "DEMOLITION_BOMB active detonation synchronization enabled for DemolitionBro.");
            }
            catch (Exception exception)
            {
                DiagnosticLog.Warning(
                    "DEMOLITION_BOMB synchronization patch failed: " + exception);
            }
        }

        private static IEnumerable<CodeInstruction> DemolitionBroUseFireTranspiler(
            IEnumerable<CodeInstruction> instructions)
        {
            var result = new List<CodeInstruction>(instructions);
            var projectileDeath = typeof(Projectile).GetMethod(
                "Death",
                BindingFlags.Instance | BindingFlags.Public,
                null,
                Type.EmptyTypes,
                null);
            var detonate = typeof(HarmonyDiagnostics).GetMethod(
                "DetonateDemolitionBomb",
                BindingFlags.NonPublic | BindingFlags.Static);
            if (projectileDeath == null || detonate == null)
            {
                DiagnosticLog.Warning(
                    "DEMOLITION_BOMB transpiler could not resolve detonation methods.");
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
                DiagnosticLog.Warning(
                    "DEMOLITION_BOMB expected one Projectile.Death call in DemolitionBro.UseFire; found " +
                    deathCallCount + ".");
                return result;
            }

            result[deathCallIndex].opcode = OpCodes.Call;
            result[deathCallIndex].operand = detonate;
            return result;
        }

        private static void DetonateDemolitionBomb(Projectile projectile)
        {
            if (projectile == null)
            {
                return;
            }

            try
            {
                if (IsDemolitionBombDetonationSession() && projectile.IsMine)
                {
                    var nid = Registry.GetNID(projectile);
                    if (nid == NID.NoID)
                    {
                        DiagnosticLog.Warning(
                            "DEMOLITION_BOMB owner NID unregistered; original Death continues.");
                    }
                    else if (!SentDemolitionBombDetonations.Add(nid))
                    {
                        DiagnosticLog.Info(
                            "DEMOLITION_BOMB duplicate event ignored; side=owner; nid=" + nid + ".");
                        return;
                    }
                    else
                    {
                        var x = projectile.X;
                        var y = projectile.Y;
                        global::Networking.Networking.RPC<NID, float, float>(
                            PID.TargetOthers,
                            new RpcSignature<NID, float, float>(ApplyDemolitionBombDetonationRPC),
                            nid,
                            x,
                            y,
                            false);
                        DiagnosticLog.Info(
                            "DEMOLITION_BOMB owner send; nid=" + nid +
                            "; position=" + x + "," + y + ".");
                    }
                }
            }
            catch (Exception exception)
            {
                DiagnosticLog.Warning(
                    "DEMOLITION_BOMB owner send failed; original Death continues: " + exception);
            }

            // Preserve the original immediate, virtual call on the owning stack.
            projectile.Death();
        }

        [AllowedRPC]
        private static void ApplyDemolitionBombDetonationRPC(NID nid, float x, float y)
        {
            if (!IsDemolitionBombDetonationSession() || nid == NID.NoID)
            {
                return;
            }

            if (!AppliedDemolitionBombDetonations.Add(nid))
            {
                DiagnosticLog.Info(
                    "DEMOLITION_BOMB duplicate event ignored; side=remote; nid=" + nid + ".");
                return;
            }

            Projectile projectile = null;
            try
            {
                projectile = Registry.GetObject(nid) as Projectile;
            }
            catch (Exception exception)
            {
                DiagnosticLog.Warning(
                    "DEMOLITION_BOMB remote NID lookup failed; nid=" + nid +
                    "; error=" + exception.Message + ".");
            }

            if (projectile == null || projectile.gameObject == null)
            {
                DiagnosticLog.Warning(
                    "DEMOLITION_BOMB remote NID unregistered; nid=" + nid + ".");
                return;
            }

            projectile.Position = new Vector2(x, y);
            projectile.Death();
            DiagnosticLog.Info(
                "DEMOLITION_BOMB remote NID hit and detonated; nid=" + nid +
                "; position=" + x + "," + y + ".");
        }

        private static bool IsDemolitionBombDetonationSession()
        {
            return _networkSessionActive && IsOnline() &&
                   PID.MyIdHasBeenSet && PID.ServerHasBeenSet;
        }

        private static void ClearDemolitionBroBombDetonationState()
        {
            SentDemolitionBombDetonations.Clear();
            AppliedDemolitionBombDetonations.Clear();
        }
    }
}
