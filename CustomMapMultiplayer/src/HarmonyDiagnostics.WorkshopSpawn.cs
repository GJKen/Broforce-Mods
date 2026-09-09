using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using System.Text;
using System.Threading;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CustomMapMultiplayer
{
    // 角色生成与出生点同步：延迟出生点捕获、应用与重广播。
    internal static partial class HarmonyDiagnostics
    {
        private static void ResetWorkshopSpawnTracking(string reason)
        {
            PendingSpawnPositions.Clear();
            InitialWorkshopSpawnPositions.Clear();
            InitialWorkshopSpawnCaptureArmed.Clear();
            InitialWorkshopSpawnCaptureClosed.Clear();
            LocalWorkshopSpawnPositions.Clear();
            SnappedRemoteWorkshopCharacters.Clear();
            _workshopSpawnRebroadcastAtUtc = DateTime.MinValue;
            _workshopSpawnRebroadcastPending = false;
            _workshopSpawnRebroadcastUseCurrentPositions = false;
            DiagnosticLog.Trace("Workshop spawn tracking reset: reason=" + reason + ".");
        }

        private static void PrepareWorkshopSpawnJoinedPlayers()
        {
            if (_lateJoinStarted && !_sessionIsHost)
            {
                if (!_lateJoinSpawnJoinedPlayersSeen)
                {
                    DiagnosticLog.Info(
                        "Late workshop SpawnJoinedPlayers observed; automatic local join is now eligible.");
                }

                _lateJoinSpawnJoinedPlayersSeen = true;
                _lateJoinAutoJoinAtUtc = DateTime.UtcNow.AddMilliseconds(
                    LateJoinAutoJoinDelayMilliseconds);
            }

            if (!IsWorkshopJoinProtectionActive() || HeroController.PIDS == null ||
                HeroController.players == null || HeroController.playerControllerIDs == null)
            {
                return;
            }

            var playersPlaying = GetPlayersPlayingArray();
            if (playersPlaying == null)
            {
                return;
            }

            var count = System.Math.Min(4, System.Math.Min(
                HeroController.PIDS.Length,
                System.Math.Min(HeroController.players.Length, HeroController.playerControllerIDs.Length)));
            var keptLocalPlayer = -1;
            for (var index = 0; index < count; index++)
            {
                var pid = HeroController.PIDS[index];
                if (pid == null || !pid.IsMine || index >= playersPlaying.Length || !playersPlaying[index])
                {
                    continue;
                }

                if (keptLocalPlayer < 0)
                {
                    keptLocalPlayer = index;
                    continue;
                }

                if (HeroController.players[index] != null)
                {
                    DiagnosticLog.Warning(
                        "Workshop found an extra local player slot with an existing Player object; " +
                        "leaving it untouched: player=" + index + ".");
                    continue;
                }

                playersPlaying[index] = false;
                HeroController.PIDS[index] = null;
                HeroController.playerControllerIDs[index] = -1;
                DiagnosticLog.Warning(
                    "Removed duplicate local Workshop player slot before SpawnJoinedPlayers: " +
                    "player=" + index + "; keptPlayer=" + keptLocalPlayer + ".");
            }
        }

        private static void SpawnJoinedPlayersPostfix()
        {
            if (!IsWorkshopOnlineClientSession() || HeroController.players == null)
            {
                return;
            }

            var repaired = 0;
            for (var index = 0; index < HeroController.players.Length; index++)
            {
                var player = HeroController.players[index];
                if (player == null)
                {
                    continue;
                }

                var before = HeroController.PIDS == null || index >= HeroController.PIDS.Length
                    ? null
                    : HeroController.PIDS[index];
                RepairPendingLocalWorkshopPlayerOwnership(player);
                var after = HeroController.PIDS == null || index >= HeroController.PIDS.Length
                    ? null
                    : HeroController.PIDS[index];
                if (before != after && after != null && after.IsMine)
                {
                    repaired++;
                }
            }

            if (repaired > 0)
            {
                DiagnosticLog.Info(
                    "Workshop SpawnJoinedPlayers ownership reconciliation completed: repaired=" +
                    repaired + ".");
            }
        }

        private static void ArmWorkshopInitialSpawnCapture(Player player)
        {
            if (!IsWorkshopOnlineSession() || !IsWorkshopInitialSpawnRespawnFixEnabled() ||
                player == null || !player.firstDeployment ||
                InitialWorkshopSpawnPositions.ContainsKey(player.playerNum) ||
                InitialWorkshopSpawnCaptureClosed.Contains(player.playerNum))
            {
                return;
            }

            if (InitialWorkshopSpawnCaptureArmed.Add(player.playerNum))
            {
                DiagnosticLog.Trace(
                    "Armed Workshop initial spawn capture: player=" + player.playerNum +
                    "; owner=" + (player.IsMine ? "local" : "remote") +
                    "; firstDeployment=true.");
            }
        }

        private static void CaptureDeferredSpawnPosition(Player player, object[] arguments)
        {
            if (!IsWorkshopOnlineSession() || player == null || arguments == null || arguments.Length < 4 ||
                !(arguments[3] is Vector3))
            {
                return;
            }

            var bro = arguments[0] as TestVanDammeAnim;
            var position = (Vector3)arguments[3];
            var spawnType = arguments[1] is Player.SpawnType
                ? (Player.SpawnType)arguments[1]
                : Player.SpawnType.CustomSpawnPoint;
            var spawnViaAirDrop = arguments[2] is bool && (bool)arguments[2];
            var initialSpawnRespawnFixEnabled = IsWorkshopInitialSpawnRespawnFixEnabled();
            if (!initialSpawnRespawnFixEnabled)
            {
                InitialWorkshopSpawnCaptureArmed.Remove(player.playerNum);
            }

            var hasInitialSpawn = initialSpawnRespawnFixEnabled &&
                InitialWorkshopSpawnPositions.ContainsKey(player.playerNum);
            var initialCaptureArmed = initialSpawnRespawnFixEnabled &&
                InitialWorkshopSpawnCaptureArmed.Contains(player.playerNum);
            var initialCaptureClosed = InitialWorkshopSpawnCaptureClosed.Contains(player.playerNum);
            var isFirstDeployment = initialSpawnRespawnFixEnabled && !initialCaptureClosed &&
                (player.firstDeployment || initialCaptureArmed);

            if (isFirstDeployment)
            {
                InitialWorkshopSpawnCaptureArmed.Remove(player.playerNum);
            }

            if (!hasInitialSpawn && isFirstDeployment)
            {
                if (bro != null && IsStableDirectInitialSpawn(spawnType, spawnViaAirDrop, position))
                {
                    InitialWorkshopSpawnPositions[player.playerNum] = new DeferredSpawnPosition(
                        spawnType,
                        spawnViaAirDrop,
                        position);
                    hasInitialSpawn = true;
                    InitialWorkshopSpawnCaptureClosed.Add(player.playerNum);
                    DiagnosticLog.Info(
                        "Recorded immutable Workshop initial spawn backup: player=" +
                        player.playerNum + "; spawnType=" + spawnType +
                        "; spawnViaAirDrop=" + spawnViaAirDrop +
                        "; position=" + FormatVector3(position) +
                        "; owner=" + (player.IsMine ? "local" : "remote") +
                        "; firstDeployment=true.");
                }
                else if (spawnType != Player.SpawnType.CustomSpawnPoint ||
                         spawnViaAirDrop || position.x < 0f)
                {
                    InitialWorkshopSpawnCaptureClosed.Add(player.playerNum);
                    DiagnosticLog.Trace(
                        "Skipped Workshop initial spawn backup: player=" + player.playerNum +
                        "; spawnType=" + spawnType +
                        "; spawnViaAirDrop=" + spawnViaAirDrop +
                        "; position=" + FormatVector3(position) +
                        "; reason=not-a-stable-direct-map-spawn.");
                }
            }

            var nativePosition = position;
            DeferredSpawnPosition initialSpawn;
            if (initialSpawnRespawnFixEnabled && !isFirstDeployment &&
                spawnType == Player.SpawnType.DropInDuringGame &&
                !spawnViaAirDrop &&
                InitialWorkshopSpawnPositions.TryGetValue(player.playerNum, out initialSpawn) &&
                IsAbnormalDropInRespawnPosition(nativePosition))
            {
                position = initialSpawn.Position;
                arguments[3] = position;
                DiagnosticLog.Warning(
                    "Used immutable Workshop initial spawn backup for native drop-in respawn: " +
                    "player=" + player.playerNum + "; nativeSpawnType=" + spawnType +
                    "; nativePosition=" + FormatVector3(nativePosition) +
                    "; appliedSpawnType=" + spawnType +
                    "; appliedPosition=" + FormatVector3(position) + ".");
            }

            SnapFirstRemoteWorkshopCharacter(player, bro, position);

            if (bro != null && player.IsMine)
            {
                LocalWorkshopSpawnPositions[player.playerNum] = new DeferredSpawnPosition(
                    spawnType,
                    spawnViaAirDrop,
                    position);
                QueueWorkshopSpawnRebroadcast(
                    isFirstDeployment
                        ? "local player received its initial spawn position; waiting for settled physics"
                        : "local player received a later respawn position; preserving initial backup",
                    750,
                    true);
                DiagnosticLog.Trace(
                    "Updated mutable local Workshop rebroadcast position: player=" +
                    player.playerNum + "; spawnType=" + spawnType +
                    "; position=" + FormatVector3(position) +
                    "; initialBackup=" +
                    (hasInitialSpawn ? "preserved" : "missing") + ".");
            }

            if (bro != null && player.character != null)
            {
                return;
            }

            PendingSpawnPositions[player.playerNum] = new DeferredSpawnPosition(
                spawnType,
                spawnViaAirDrop,
                position);
            DiagnosticLog.Warning(
                "Deferred Workshop spawn position: player=" + player.playerNum +
                "; position=" + FormatVector3(position) +
                "; broArgument=" + (bro == null ? "null" : "present") + ".");
        }

        private static bool IsStableDirectInitialSpawn(
            Player.SpawnType spawnType,
            bool spawnViaAirDrop,
            Vector3 position)
        {
            return spawnType == Player.SpawnType.CustomSpawnPoint &&
                !spawnViaAirDrop &&
                position.x >= 0f;
        }

        private static bool IsAbnormalDropInRespawnPosition(Vector3 position)
        {
            return position.x < 0f || position.y > SortOfFollow.GetScreenMaxY() + 64f;
        }

        private static bool IsWorkshopInitialSpawnRespawnFixEnabled()
        {
            return Plugin.Settings != null && Plugin.Settings.EnableWorkshopDropInRespawnFix;
        }

        private static void AssignCharacterPostfix(Player __instance)
        {
            if (__instance != null)
            {
                ApplyDeferredSpawnPosition(__instance.playerNum, __instance.character);
            }
        }

        private static void SetPlayerCharacterPostfix(int index, TestVanDammeAnim character)
        {
            ApplyDeferredSpawnPosition(index, character);
            if (character != null && IsWorkshopOnlineSession() && HeroController.PIDS != null &&
                index >= 0 && index < HeroController.PIDS.Length && HeroController.PIDS[index] != null &&
                HeroController.PIDS[index].IsMine)
            {
                if (HeroController.playerControllerIDs != null &&
                    index < HeroController.playerControllerIDs.Length)
                {
                    RememberLastLocalWorkshopController(HeroController.playerControllerIDs[index]);
                }
                ResetStalePauseStateForWorkshopSession("local Workshop character assigned");
            }

            if (character != null && _lateJoinStarted && !_lateJoinAutoJoinCompleted &&
                HeroController.PIDS != null && index >= 0 && index < HeroController.PIDS.Length &&
                HeroController.PIDS[index] != null && HeroController.PIDS[index].IsMine)
            {
                ClearWorkshopLocalJoinRequests();
                _lateJoinPlayerJoinRequested = false;
                _lateJoinPlayerRequestAtUtc = DateTime.MinValue;
                _lateJoinAutoJoinCompleted = true;
                DiagnosticLog.Info(
                    "Late workshop automatic join confirmed by local SetPlayerCharacter: player=" +
                    index + ".");
            }

            if (character != null)
            {
                QueueWorkshopSpawnRebroadcast(
                    "a Workshop character was assigned; rebroadcasting its current authoritative position",
                    750,
                    true);
            }
        }

        private static bool ApplyDeferredSpawnPosition(int playerNum, TestVanDammeAnim character)
        {
            if (character == null)
            {
                return false;
            }

            DeferredSpawnPosition pending;
            if (!PendingSpawnPositions.TryGetValue(playerNum, out pending))
            {
                return false;
            }

            try
            {
                var player = HeroController.players == null || playerNum < 0 ||
                             playerNum >= HeroController.players.Length
                    ? null
                    : HeroController.players[playerNum];
                if (player == null)
                {
                    return false;
                }

                PendingSpawnPositions.Remove(playerNum);
                player.SetSpawnPositon(
                    character,
                    pending.SpawnType,
                    pending.SpawnViaAirDrop,
                    pending.Position);
                SnapFirstRemoteWorkshopCharacter(player, character, pending.Position);
                DiagnosticLog.Warning(
                    "Applied deferred Workshop spawn position through native Player.SetSpawnPositon: player=" +
                    playerNum + "; position=" + FormatVector3(pending.Position) + ".");
                return true;
            }
            catch (Exception exception)
            {
                DiagnosticLog.Warning(
                    "Applying deferred Workshop spawn position failed: " + exception);
                return false;
            }
        }

        private static void SnapFirstRemoteWorkshopCharacter(
            Player player,
            TestVanDammeAnim character,
            Vector3 position)
        {
            if (player == null || character == null || player.IsMine)
            {
                return;
            }

            TestVanDammeAnim alreadySnapped;
            if (SnappedRemoteWorkshopCharacters.TryGetValue(player.playerNum, out alreadySnapped) &&
                alreadySnapped == character)
            {
                return;
            }

            try
            {
                var currentPosition = character.transform.position;
                character.transform.position = new Vector3(
                    position.x,
                    position.y,
                    currentPosition.z);
                SnappedRemoteWorkshopCharacters[player.playerNum] = character;
                DiagnosticLog.Info(
                    "Snapped the first remote Workshop character to the authoritative spawn position: " +
                    "player=" + player.playerNum + "; position=" +
                    FormatVector3(character.transform.position) + ".");
            }
            catch (Exception exception)
            {
                DiagnosticLog.Warning(
                    "Remote Workshop spawn-position snap failed: " + exception);
            }
        }

        private static void TryRebroadcastWorkshopSpawns()
        {
            if (!_workshopSpawnRebroadcastPending ||
                DateTime.UtcNow < _workshopSpawnRebroadcastAtUtc)
            {
                return;
            }

            _workshopSpawnRebroadcastPending = false;
            var useCurrentPositions = _workshopSpawnRebroadcastUseCurrentPositions;
            _workshopSpawnRebroadcastUseCurrentPositions = false;
            if (!IsWorkshopOnlineSession() || HeroController.players == null)
            {
                return;
            }

            var rebroadcastCount = 0;
            var rebroadcastPositions = new StringBuilder();
            for (var index = 0; index < HeroController.players.Length; index++)
            {
                var player = HeroController.players[index];
                DeferredSpawnPosition position;
                if (player == null || player.character == null || !player.IsMine ||
                    !LocalWorkshopSpawnPositions.TryGetValue(index, out position))
                {
                    continue;
                }

                try
                {
                    var spawnType = position.SpawnType;
                    var spawnPosition = position.Position;
                    if (useCurrentPositions)
                    {
                        spawnPosition = player.character.transform.position;
                    }

                    if (rebroadcastPositions.Length > 0)
                    {
                        rebroadcastPositions.Append(",");
                    }

                    rebroadcastPositions.Append(index);
                    rebroadcastPositions.Append("=");
                    rebroadcastPositions.Append(FormatVector3(spawnPosition));

                    Networking.Networking.RPC<
                        TestVanDammeAnim,
                        Player.SpawnType,
                        bool,
                        Vector3>(
                        PID.TargetOthers,
                        new RpcSignature<
                            TestVanDammeAnim,
                            Player.SpawnType,
                            bool,
                            Vector3>(player.SetSpawnPositon),
                        player.character,
                        spawnType,
                        position.SpawnViaAirDrop,
                        spawnPosition,
                        false);
                    rebroadcastCount++;
                }
                catch (Exception exception)
                {
                    DiagnosticLog.Warning(
                    "Workshop spawn-position rebroadcast failed for local player=" +
                        index + ": " + exception);
                }
            }

            DiagnosticLog.Info(
                "Workshop spawn-position rebroadcast completed with authoritative current positions: localPlayers=" +
                rebroadcastCount + "; positions=" + rebroadcastPositions + ".");
        }

        private static void QueueWorkshopSpawnRebroadcast(string reason, int delayMilliseconds)
        {
            QueueWorkshopSpawnRebroadcast(reason, delayMilliseconds, false);
        }

        private static void QueueWorkshopSpawnRebroadcast(
            string reason,
            int delayMilliseconds,
            bool useCurrentPosition)
        {
            if (!IsWorkshopOnlineSession())
            {
                return;
            }

            _workshopSpawnRebroadcastPending = true;
            _workshopSpawnRebroadcastAtUtc = DateTime.UtcNow.AddMilliseconds(delayMilliseconds);
            _workshopSpawnRebroadcastUseCurrentPositions =
                _workshopSpawnRebroadcastUseCurrentPositions || useCurrentPosition;
            DiagnosticLog.Trace(
                "Scheduled exact Workshop spawn-position rebroadcast: " + reason + ".");
        }

        private sealed class DeferredSpawnPosition
        {
            public DeferredSpawnPosition(
                Player.SpawnType spawnType,
                bool spawnViaAirDrop,
                Vector3 position)
            {
                SpawnType = spawnType;
                SpawnViaAirDrop = spawnViaAirDrop;
                Position = position;
            }

            public Player.SpawnType SpawnType { get; private set; }
            public bool SpawnViaAirDrop { get; private set; }
            public Vector3 Position { get; private set; }
        }
    }
}
