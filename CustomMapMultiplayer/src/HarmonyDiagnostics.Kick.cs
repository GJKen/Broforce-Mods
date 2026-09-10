using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace CustomMapMultiplayer
{
    internal static partial class HarmonyDiagnostics
    {
        private static readonly HashSet<int> PendingNativeKickDropouts =
            new HashSet<int>();
        private static readonly Dictionary<int, RejectedNativeKickDropout>
            PendingRejectedNativeKickDropouts =
            new Dictionary<int, RejectedNativeKickDropout>();
        private const int RejectedNativeKickDropoutWindowMilliseconds = 5000;
        private static FieldInfo _nativeKickEligibilityField;
        private static FieldInfo _nativeKickBubbleField;

        private sealed class RejectedNativeKickDropout
        {
            internal PID Sender;
            internal DateTime ExpiresAtUtc;
        }

        private static void PatchOnlineKickPermissions()
        {
            try
            {
                var checkForKick = typeof(TestVanDammeAnim).GetMethod(
                    "CheckForKick",
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
                    null,
                    Type.EmptyTypes,
                    null);
                var kick = typeof(TestVanDammeAnim).GetMethod(
                    "Kick",
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
                    null,
                    Type.EmptyTypes,
                    null);
                var kickRpc = typeof(TestVanDammeAnim).GetMethod(
                    "KickRPC",
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
                    null,
                    Type.EmptyTypes,
                    null);
                var notifyPlayerThatHeHasBeenKicked = typeof(Connect).GetMethod(
                    "NotifyPlayerThatHeHasBeenKicked",
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
                    null,
                    new[] { typeof(PID) },
                    null);
                var kickRoutineMoveNext = FindNativeKickRoutineMoveNext();
                _nativeKickEligibilityField = typeof(TestVanDammeAnim).GetField(
                    "ElgilbleToBeKicked",
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                _nativeKickBubbleField = typeof(TestVanDammeAnim).GetField(
                    "kickPlayerBubble",
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                var checkForKickPrefix = typeof(HarmonyDiagnostics).GetMethod(
                    "CheckForKickPrefix",
                    BindingFlags.NonPublic | BindingFlags.Static);
                var checkForKickPostfix = typeof(HarmonyDiagnostics).GetMethod(
                    "CheckForKickPostfix",
                    BindingFlags.NonPublic | BindingFlags.Static);
                var kickPrefix = typeof(HarmonyDiagnostics).GetMethod(
                    "KickPrefix",
                    BindingFlags.NonPublic | BindingFlags.Static);
                var kickRpcPrefix = typeof(HarmonyDiagnostics).GetMethod(
                    "KickRpcPrefix",
                    BindingFlags.NonPublic | BindingFlags.Static);
                var notifyPlayerThatHeHasBeenKickedPrefix = typeof(HarmonyDiagnostics).GetMethod(
                    "NotifyPlayerThatHeHasBeenKickedPrefix",
                    BindingFlags.NonPublic | BindingFlags.Static);
                var dropoutRpc = typeof(HeroController).GetMethod(
                    "DropoutRPC",
                    BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic,
                    null,
                    new[] { typeof(int) },
                    null);
                var dropoutRpcPrefix = typeof(HarmonyDiagnostics).GetMethod(
                    "DropoutRpcPermissionPrefix",
                    BindingFlags.NonPublic | BindingFlags.Static);
                var kickRoutineTranspiler = typeof(HarmonyDiagnostics).GetMethod(
                    "KickRoutineTranspiler",
                    BindingFlags.NonPublic | BindingFlags.Static);

                if (checkForKick == null || kick == null || kickRpc == null ||
                    notifyPlayerThatHeHasBeenKicked == null ||
                    kickRoutineMoveNext == null || checkForKickPrefix == null ||
                    checkForKickPostfix == null || kickPrefix == null ||
                    kickRpcPrefix == null || notifyPlayerThatHeHasBeenKickedPrefix == null ||
                    dropoutRpc == null ||
                    dropoutRpcPrefix == null || kickRoutineTranspiler == null)
                {
                    DiagnosticLog.Warning(
                        "Online kick permission patch could not resolve TestVanDammeAnim kick methods.");
                    return;
                }

                _harmony.Patch(
                    checkForKick,
                    new HarmonyMethod(checkForKickPrefix),
                    new HarmonyMethod(checkForKickPostfix),
                    null,
                    null);
                _harmony.Patch(
                    kick,
                    new HarmonyMethod(kickPrefix),
                    null,
                    null,
                    null);
                _harmony.Patch(
                    kickRpc,
                    new HarmonyMethod(kickRpcPrefix),
                    null,
                    null,
                    null);
                _harmony.Patch(
                    notifyPlayerThatHeHasBeenKicked,
                    new HarmonyMethod(notifyPlayerThatHeHasBeenKickedPrefix),
                    null,
                    null,
                    null);
                _harmony.Patch(
                    dropoutRpc,
                    new HarmonyMethod(dropoutRpcPrefix),
                    null,
                    null,
                    null);
                _harmony.Patch(
                    kickRoutineMoveNext,
                    null,
                    null,
                    new HarmonyMethod(kickRoutineTranspiler),
                    null);
                DiagnosticLog.Info(
                    "Online kick permission enabled; only the current host may initiate native kicks; " +
                    "authorized native KickRPC execution remains enabled on every endpoint; " +
                    "unauthorized host kick notifications are rejected; " +
                    "online kick explosions are suppressed by the multiplayer setting.");
            }
            catch (Exception exception)
            {
                DiagnosticLog.Warning(
                    "Online kick permission patch failed: " + exception);
            }
        }

        private static bool CheckForKickPrefix(
            TestVanDammeAnim __instance,
            out bool __state)
        {
            __state = false;
            if (!IsOnline())
            {
                return true;
            }

            if (!IsOnlineHost())
            {
                return false;
            }

            // The native check displays the same kick bubble on an idle target,
            // including the local host. Temporarily hide only that target state;
            // the host still runs the nearby-player kick check below it.
            if (!IsOnlineKickFixEnabled())
            {
                return true;
            }

            if (__instance != null && __instance.IsMine && IsNativeKickEligible(__instance))
            {
                SetNativeKickEligible(__instance, false);
                __state = true;
            }

            return true;
        }

        private static void CheckForKickPostfix(
            TestVanDammeAnim __instance,
            bool __state)
        {
            if (__state && __instance != null)
            {
                SetNativeKickEligible(__instance, true);
                HideNativeKickBubble(__instance);
            }
        }

        private static IEnumerable<CodeInstruction> KickRoutineTranspiler(
            IEnumerable<CodeInstruction> instructions)
        {
            var result = new List<CodeInstruction>(instructions);
            var damageGround = typeof(MapController).GetMethod(
                "DamageGround",
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static,
                null,
                new[]
                {
                    typeof(MonoBehaviour),
                    typeof(int),
                    typeof(DamageType),
                    typeof(float),
                    typeof(float),
                    typeof(float),
                    typeof(Collider[]),
                    typeof(bool)
                },
                null);
            var explodeUnits = typeof(Map).GetMethod(
                "ExplodeUnits",
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static,
                null,
                new[]
                {
                    typeof(MonoBehaviour),
                    typeof(int),
                    typeof(DamageType),
                    typeof(float),
                    typeof(float),
                    typeof(float),
                    typeof(float),
                    typeof(float),
                    typeof(float),
                    typeof(int),
                    typeof(bool),
                    typeof(bool),
                    typeof(bool)
                },
                null);
            var damageGroundReplacement = typeof(HarmonyDiagnostics).GetMethod(
                "HandleNativeKickGroundDamage",
                BindingFlags.NonPublic | BindingFlags.Static);
            var explodeUnitsReplacement = typeof(HarmonyDiagnostics).GetMethod(
                "HandleNativeKickExplosion",
                BindingFlags.NonPublic | BindingFlags.Static);
            if (damageGround == null || explodeUnits == null ||
                damageGroundReplacement == null || explodeUnitsReplacement == null)
            {
                DiagnosticLog.Warning(
                    "Native kick explosion suppression could not resolve Map damage methods.");
                return result;
            }

            var replacedGroundDamage = 0;
            var replacedExplosions = 0;
            foreach (var instruction in result)
            {
                if (damageGround.Equals(instruction.operand as MethodInfo))
                {
                    instruction.operand = damageGroundReplacement;
                    replacedGroundDamage++;
                }
                else if (explodeUnits.Equals(instruction.operand as MethodInfo))
                {
                    instruction.operand = explodeUnitsReplacement;
                    replacedExplosions++;
                }
            }

            if (replacedGroundDamage != 1 || replacedExplosions != 2)
            {
                DiagnosticLog.Warning(
                    "Native kick explosion suppression expected one ground-damage call and two " +
                    "explosion calls; found groundDamage=" + replacedGroundDamage +
                    "; explosions=" + replacedExplosions + ".");
            }

            return result;
        }

        private static bool HandleNativeKickGroundDamage(
            MonoBehaviour source,
            int damage,
            DamageType damageType,
            float xRange,
            float yRange,
            float x,
            Collider[] ignoredColliders,
            bool damageGround)
        {
            if (IsOnlineKickFixEnabled() && IsOnline())
            {
                return false;
            }

            return MapController.DamageGround(
                source,
                damage,
                damageType,
                xRange,
                yRange,
                x,
                ignoredColliders,
                damageGround);
        }

        private static int HandleNativeKickExplosion(
            MonoBehaviour source,
            int damage,
            DamageType damageType,
            float xRange,
            float yRange,
            float x,
            float y,
            float force,
            float upwardForce,
            int playerNum,
            bool damageMooks,
            bool damageHeroes,
            bool destroyUnits)
        {
            if (IsOnlineKickFixEnabled() && IsOnline())
            {
                return 0;
            }

            return Map.ExplodeUnits(
                source,
                damage,
                damageType,
                xRange,
                yRange,
                x,
                y,
                force,
                upwardForce,
                playerNum,
                damageMooks,
                damageHeroes,
                destroyUnits);
        }

        private static MethodInfo FindNativeKickRoutineMoveNext()
        {
            var nestedTypes = typeof(TestVanDammeAnim).GetNestedTypes(
                BindingFlags.Public | BindingFlags.NonPublic);
            foreach (var nestedType in nestedTypes)
            {
                if (!nestedType.Name.StartsWith("<KickRoutine>", StringComparison.Ordinal))
                {
                    continue;
                }

                return nestedType.GetMethod(
                    "MoveNext",
                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            }

            return null;
        }

        private static bool KickPrefix()
        {
            return CanInitiateNativeKick();
        }

        private static bool KickRpcPrefix(TestVanDammeAnim __instance)
        {
            if (IsOnline())
            {
                // KickRPC is invoked while the native RPC dispatcher exposes the
                // serialized sender through RPCController.CurrentRPC/LastSender.
                // The host's own TargetAll invocation also has the host PID as sender.
                var sender = RPCController.LastSender;
                var unauthorizedSender = sender != PID.ServerID;
                if (unauthorizedSender)
                {
                    if (IsOnlineHost() && __instance != null && __instance.player != null)
                    {
                        RememberRejectedNativeKickDropout(
                            __instance.player.playerNum,
                            sender);
                    }

                    DiagnosticLog.Warning(
                        "Rejected native KickRPC because the RPC sender is not " +
                        "the current server: sender=" +
                        (sender == null ? "null" : sender.ToString()) +
                        "; server=" + PID.ServerID +
                        "; endpointIsHost=" + IsOnlineHost() + ".");
                    return false;
                }
            }

            if (IsOnlineKickFixEnabled() && IsWorkshopOnlineSession() && __instance != null &&
                __instance.player != null && __instance.player.IsMine)
            {
                var playerNum = __instance.player.playerNum;
                if (playerNum >= 0 && playerNum < 4)
                {
                    PendingNativeKickDropouts.Add(playerNum);
                    DiagnosticLog.Info(
                        "Observed native host kick for local Workshop player; " +
                        "automatic rejoin will be skipped: player=" + playerNum + ".");
                }
            }

            // An authorized host RPC executes on every endpoint so the host's kick remains synchronized.
            return true;
        }

        private static bool NotifyPlayerThatHeHasBeenKickedPrefix(PID TheAssholeResponsible)
        {
            if (!IsOnline() || !IsOnlineHost())
            {
                return true;
            }

            var sender = RPCController.LastSender;
            if (sender == PID.ServerID)
            {
                return true;
            }

            DiagnosticLog.Warning(
                "Rejected native host kick notification: sender=" +
                (sender == null ? "null" : sender.ToString()) +
                "; responsible=" +
                (TheAssholeResponsible == null ? "null" : TheAssholeResponsible.ToString()) +
                "; server=" + PID.ServerID + ".");
            return false;
        }

        private static bool DropoutRpcPermissionPrefix(int playerNum)
        {
            if (!IsOnline() || !IsOnlineHost())
            {
                return true;
            }

            RejectedNativeKickDropout pending;
            if (!PendingRejectedNativeKickDropouts.TryGetValue(playerNum, out pending))
            {
                return true;
            }

            if (DateTime.UtcNow > pending.ExpiresAtUtc)
            {
                PendingRejectedNativeKickDropouts.Remove(playerNum);
                return true;
            }

            var sender = RPCController.LastSender;
            if (sender == null || sender == PID.ServerID || sender != pending.Sender)
            {
                return true;
            }

            PendingRejectedNativeKickDropouts.Remove(playerNum);
            DiagnosticLog.Warning(
                "Rejected native DropoutRPC after an unauthorized KickRPC: player=" +
                playerNum + "; sender=" + sender + "; server=" + PID.ServerID + ".");
            return false;
        }

        private static void RememberRejectedNativeKickDropout(int playerNum, PID sender)
        {
            if (playerNum < 0 || playerNum >= 4 || sender == null || sender == PID.ServerID)
            {
                return;
            }

            PendingRejectedNativeKickDropouts[playerNum] =
                new RejectedNativeKickDropout
                {
                    Sender = sender,
                    ExpiresAtUtc = DateTime.UtcNow.AddMilliseconds(
                        RejectedNativeKickDropoutWindowMilliseconds)
                };
        }

        private static bool ConsumeNativeKickDropout(int playerNum)
        {
            return PendingNativeKickDropouts.Remove(playerNum);
        }

        private static void ClearNativeKickDropouts()
        {
            PendingNativeKickDropouts.Clear();
            PendingRejectedNativeKickDropouts.Clear();
        }

        private static bool CanInitiateNativeKick()
        {
            return !IsOnline() || IsOnlineHost();
        }

        private static bool IsOnlineKickFixEnabled()
        {
            return Plugin.Settings != null && Plugin.Settings.EnableOnlineKickFix;
        }

        private static bool IsNativeKickEligible(TestVanDammeAnim character)
        {
            return _nativeKickEligibilityField != null &&
                   (bool)_nativeKickEligibilityField.GetValue(character);
        }

        private static void SetNativeKickEligible(
            TestVanDammeAnim character,
            bool value)
        {
            if (_nativeKickEligibilityField != null)
            {
                _nativeKickEligibilityField.SetValue(character, value);
            }
        }

        private static void HideNativeKickBubble(TestVanDammeAnim character)
        {
            if (_nativeKickBubbleField == null)
            {
                return;
            }

            var bubble = _nativeKickBubbleField.GetValue(character) as ReactionBubble;
            if (bubble != null)
            {
                bubble.GoAway();
            }
        }
    }
}
