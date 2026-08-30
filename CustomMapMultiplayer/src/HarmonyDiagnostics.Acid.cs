using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using Networking;

namespace CustomMapMultiplayer
{
    // Observes the native acid/death path and routes Workshop hero acid checks through the host.
    internal static partial class HarmonyDiagnostics
    {
        private const float WorkshopAcidRequestRetrySeconds = 0.75f;
        private const float WorkshopAcidScanCacheSeconds = 0.05f;
        private const float WorkshopAcidPoolRefreshSeconds = 1f;
        private const float WorkshopAcidAuthorityScanIntervalSeconds = 0.1f;
        private const float WorkshopAcidHorizontalRadius = 4f;
        private const float WorkshopAcidMinVerticalOffset = -2.5f;
        private const float WorkshopAcidMaxVerticalOffset = 10f;
        private static readonly Dictionary<NID, float> PendingWorkshopAcidRequests =
            new Dictionary<NID, float>();
        private static readonly Dictionary<NID, float> LastWorkshopAuthorityAcidAt =
            new Dictionary<NID, float>();
        private static readonly Dictionary<int, WorkshopAcidScanCache> WorkshopAcidScanCacheByHero =
            new Dictionary<int, WorkshopAcidScanCache>();
        private static WorkshopAcidPoolCacheEntry[] _workshopAcidPools =
            new WorkshopAcidPoolCacheEntry[0];
        private static string _workshopAcidPoolSceneName = string.Empty;
        private static float _workshopAcidPoolsRefreshedAt = float.NegativeInfinity;
        private static float _nextWorkshopAcidAuthorityScanAt = float.NegativeInfinity;
        private static bool _workshopAcidPoolRefreshWarningLogged;
        private static readonly HashSet<string> WorkshopAcidAuthorityGateDiagnostics =
            new HashSet<string>(StringComparer.Ordinal);
        private static readonly HashSet<NID> UnresolvedWorkshopAcidApplyDiagnostics =
            new HashSet<NID>();
        private static MethodInfo _nativeCoverInAcidRpcMethod;

        private sealed class WorkshopAcidScanCache
        {
            internal float ScannedAt;
            internal bool HasAcid;
        }

        private struct WorkshopAcidPoolCacheEntry
        {
            internal DoodadAcidPool Pool;
            internal bool IsAcid;
        }

        private static void PatchAcidDiagnostics()
        {
            var nativeCoverInAcidMethod = typeof(TestVanDammeAnim).GetMethod(
                "CoverInAcid",
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance,
                null,
                Type.EmptyTypes,
                null);
            _nativeCoverInAcidRpcMethod = typeof(TestVanDammeAnim).GetMethod(
                "CoverInAcidRPC",
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance,
                null,
                Type.EmptyTypes,
                null);
            var playerHasDiedRpc = typeof(HeroController).GetMethod(
                "PlayerHasDiedRPC",
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance,
                null,
                new[] { typeof(int) },
                null);
            var coverInAcidPrefix = typeof(HarmonyDiagnostics).GetMethod(
                "CoverInAcidDiagnosticsPrefix",
                BindingFlags.NonPublic | BindingFlags.Static);
            var coverInAcidPostfix = typeof(HarmonyDiagnostics).GetMethod(
                "CoverInAcidDiagnosticsPostfix",
                BindingFlags.NonPublic | BindingFlags.Static);
            var coverInAcidRpcPrefix = typeof(HarmonyDiagnostics).GetMethod(
                "CoverInAcidRpcDiagnosticsPrefix",
                BindingFlags.NonPublic | BindingFlags.Static);
            var coverInAcidRpcPostfix = typeof(HarmonyDiagnostics).GetMethod(
                "CoverInAcidRpcDiagnosticsPostfix",
                BindingFlags.NonPublic | BindingFlags.Static);
            var playerHasDiedRpcPrefix = typeof(HarmonyDiagnostics).GetMethod(
                "PlayerHasDiedRpcDiagnosticsPrefix",
                BindingFlags.NonPublic | BindingFlags.Static);
            var playerHasDiedRpcPostfix = typeof(HarmonyDiagnostics).GetMethod(
                "PlayerHasDiedRpcDiagnosticsPostfix",
                BindingFlags.NonPublic | BindingFlags.Static);
            var patchedCount = 0;
            var coverInAcidEntryCount = 0;
            if (nativeCoverInAcidMethod != null && _nativeCoverInAcidRpcMethod != null &&
                coverInAcidPrefix != null && coverInAcidPostfix != null)
            {
                coverInAcidEntryCount = PatchAcidMethod(
                    nativeCoverInAcidMethod,
                    coverInAcidPrefix,
                    coverInAcidPostfix);
                patchedCount += coverInAcidEntryCount;
            }

            if (_nativeCoverInAcidRpcMethod != null &&
                coverInAcidRpcPrefix != null && coverInAcidRpcPostfix != null)
            {
                patchedCount += PatchAcidMethod(
                    _nativeCoverInAcidRpcMethod,
                    coverInAcidRpcPrefix,
                    coverInAcidRpcPostfix);
            }
            else
            {
                DiagnosticLog.Warning(
                    "PLAYER_ACID could not install CoverInAcidRPC diagnostics.");
            }

            if (playerHasDiedRpc != null &&
                playerHasDiedRpcPrefix != null && playerHasDiedRpcPostfix != null)
            {
                patchedCount += PatchAcidMethod(
                    playerHasDiedRpc,
                    playerHasDiedRpcPrefix,
                    playerHasDiedRpcPostfix);
            }
            else
            {
                DiagnosticLog.Warning(
                    "PLAYER_ACID could not install PlayerHasDiedRPC diagnostics.");
            }

            if (coverInAcidEntryCount != 1)
            {
                DiagnosticLog.Warning(
                    "PLAYER_ACID Workshop authority could not patch TestVanDammeAnim.CoverInAcid.");
            }
            else
            {
                DiagnosticLog.Info(
                    "PLAYER_ACID Workshop authority enabled at TestVanDammeAnim.CoverInAcid.");
            }

            DiagnosticLog.Info(
                "PLAYER_ACID diagnostics enabled; patched methods=" + patchedCount + ".");
        }

        private static int PatchAcidMethod(
            MethodInfo target,
            MethodInfo prefix,
            MethodInfo postfix)
        {
            try
            {
                _harmony.Patch(
                    target,
                    new HarmonyMethod(prefix),
                    new HarmonyMethod(postfix),
                    null,
                    null);
                return 1;
            }
            catch (Exception exception)
            {
                DiagnosticLog.Warning(
                    "PLAYER_ACID patch failed for " + DescribeMethod(target) + ": " + exception);
                return 0;
            }
        }

        private static bool IsWorkshopAcidAuthoritySession()
        {
            return IsWorkshopAcidConfiguredSceneSession() &&
                   (_injectedForSession || _sessionWorkshopIdentityAdopted ||
                    IsConfiguredWorkshopGameState());
        }

        private static bool IsWorkshopAcidConfiguredSceneSession()
        {
            return _networkSessionActive && IsOnline() &&
                   HasValidWorkshopInjectionConfiguration() &&
                   IsConfiguredWorkshopSceneActive();
        }

        private static bool HasWorkshopAcidAt(TestVanDammeAnim character)
        {
            if (character == null || Map.Instance == null || !Map.Instance.HasBeenSetup)
            {
                return false;
            }

            var heroId = character.GetInstanceID();
            var now = UnityEngine.Time.unscaledTime;
            WorkshopAcidScanCache cached;
            if (WorkshopAcidScanCacheByHero.TryGetValue(heroId, out cached) &&
                now - cached.ScannedAt < WorkshopAcidScanCacheSeconds)
            {
                PerformanceTelemetry.Hit(PerformanceMetric.AcidHeroCache);
                return cached.HasAcid;
            }

            PerformanceTelemetry.Miss(PerformanceMetric.AcidHeroCache);
            var scanStartedAt = PerformanceTelemetry.Begin(PerformanceMetric.AcidHeroScan);
            var hasAcid = false;
            var acidPools = GetWorkshopAcidPools();
            PerformanceTelemetry.AddItems(PerformanceMetric.AcidHeroScan, acidPools.Length);
            for (var index = 0; index < acidPools.Length; index++)
            {
                var acidPool = acidPools[index];
                if (acidPool.Pool == null || !acidPool.IsAcid ||
                    acidPool.Pool.fullness <= 0.2f)
                {
                    continue;
                }

                if (UnityEngine.Mathf.Abs(
                        acidPool.Pool.centerX - character.X) > WorkshopAcidHorizontalRadius)
                {
                    continue;
                }

                var verticalOffset = acidPool.Pool.centerY - character.Y;
                if (verticalOffset < WorkshopAcidMinVerticalOffset ||
                    verticalOffset > WorkshopAcidMaxVerticalOffset)
                {
                    continue;
                }

                hasAcid = true;
                break;
            }

            if (cached == null)
            {
                cached = new WorkshopAcidScanCache();
                WorkshopAcidScanCacheByHero[heroId] = cached;
            }
            cached.ScannedAt = now;
            cached.HasAcid = hasAcid;
            if (hasAcid)
            {
                PerformanceTelemetry.Hit(PerformanceMetric.AcidHeroScan);
            }
            else
            {
                PerformanceTelemetry.Miss(PerformanceMetric.AcidHeroScan);
            }
            PerformanceTelemetry.End(PerformanceMetric.AcidHeroScan, scanStartedAt);
            return hasAcid;
        }

        private static WorkshopAcidPoolCacheEntry[] GetWorkshopAcidPools()
        {
            var sceneName = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
            var now = UnityEngine.Time.unscaledTime;
            if (string.Equals(
                    _workshopAcidPoolSceneName,
                    sceneName,
                    StringComparison.Ordinal) &&
                now >= _workshopAcidPoolsRefreshedAt &&
                now - _workshopAcidPoolsRefreshedAt < WorkshopAcidPoolRefreshSeconds)
            {
                return _workshopAcidPools;
            }

            var refreshStartedAt = PerformanceTelemetry.Begin(PerformanceMetric.AcidPoolRefresh);
            try
            {
                var discoveredPools = UnityEngine.Object.FindObjectsOfType<DoodadAcidPool>();
                if (discoveredPools == null || discoveredPools.Length == 0)
                {
                    _workshopAcidPools = new WorkshopAcidPoolCacheEntry[0];
                }
                else
                {
                    var cachedPools = new List<WorkshopAcidPoolCacheEntry>(discoveredPools.Length);
                    for (var index = 0; index < discoveredPools.Length; index++)
                    {
                        var pool = discoveredPools[index];
                        if (pool == null)
                        {
                            continue;
                        }

                        cachedPools.Add(new WorkshopAcidPoolCacheEntry
                        {
                            Pool = pool,
                            IsAcid = pool.fluidType == DoodadBloodPool.FluidType.Acid
                        });
                    }

                    _workshopAcidPools = cachedPools.ToArray();
                }

                _workshopAcidPoolSceneName = sceneName;
                _workshopAcidPoolsRefreshedAt = now;
                _workshopAcidPoolRefreshWarningLogged = false;
                WorkshopAcidScanCacheByHero.Clear();
            }
            catch (Exception exception)
            {
                _workshopAcidPools = new WorkshopAcidPoolCacheEntry[0];
                _workshopAcidPoolSceneName = sceneName;
                _workshopAcidPoolsRefreshedAt = now;
                WorkshopAcidScanCacheByHero.Clear();
                if (!_workshopAcidPoolRefreshWarningLogged)
                {
                    DiagnosticLog.Warning(
                        "PLAYER_ACID Workshop acid pool cache refresh failed: " + exception.Message);
                    _workshopAcidPoolRefreshWarningLogged = true;
                }
            }

            PerformanceTelemetry.AddItems(
                PerformanceMetric.AcidPoolRefresh,
                _workshopAcidPools == null ? 0 : _workshopAcidPools.Length);
            PerformanceTelemetry.End(PerformanceMetric.AcidPoolRefresh, refreshStartedAt);
            return _workshopAcidPools;
        }

        private static void TryApplyWorkshopRemoteHeroAcid()
        {
            if (!IsWorkshopAcidAuthoritySession() || !IsOnlineHost() ||
                HeroController.players == null ||
                Map.Instance == null || !Map.Instance.HasBeenSetup)
            {
                return;
            }

            var now = UnityEngine.Time.unscaledTime;
            if (now < _nextWorkshopAcidAuthorityScanAt)
            {
                return;
            }

            _nextWorkshopAcidAuthorityScanAt =
                now + WorkshopAcidAuthorityScanIntervalSeconds;
            PerformanceTelemetry.AddItems(
                PerformanceMetric.AcidAuthority,
                HeroController.players.Length);

            for (var index = 0; index < HeroController.players.Length; index++)
            {
                var player = HeroController.players[index];
                var character = player == null ? null : player.character;
                if (character == null || !character.IsHero ||
                    character.health <= 0 || character.hasBeenCoverInAcid ||
                    character.invulnerable || !character.canBeCoveredInAcid)
                {
                    continue;
                }

                // The host's own CoverInAcid entry can be missed by Workshop maps;
                // scan the host hero here as a local fallback while keeping remote
                // heroes on the existing host-authoritative path.
                if (character.IsMine)
                {
                    if (HasWorkshopAcidAt(character))
                    {
                        TryApplyWorkshopLocalHeroAcid(character, "host-local-scan");
                    }
                    continue;
                }

                try
                {
                    if (!HasWorkshopAcidAt(character))
                    {
                        continue;
                    }

                    TryBroadcastWorkshopHeroAcid(character, "host-map-scan");
                }
                catch (Exception exception)
                {
                    DiagnosticLog.Warning(
                        "PLAYER_ACID Workshop host map scan failed for player=" + index +
                        ": " + exception.Message);
                }
            }
        }

        private static void RequestWorkshopHeroAcid(TestVanDammeAnim character)
        {
            if (character == null || !character.IsMine || character.health <= 0 ||
                character.hasBeenCoverInAcid || character.invulnerable ||
                !character.canBeCoveredInAcid)
            {
                return;
            }

            try
            {
                var nid = Registry.GetNID(character);
                if (nid == NID.NoID)
                {
                    DiagnosticLog.Warning(
                        "PLAYER_ACID Workshop client could not request authority because hero NID is unregistered.");
                    return;
                }

                var now = UnityEngine.Time.unscaledTime;
                float previousRequestAt;
                if (PendingWorkshopAcidRequests.TryGetValue(nid, out previousRequestAt) &&
                    now - previousRequestAt < WorkshopAcidRequestRetrySeconds)
                {
                    return;
                }

                PendingWorkshopAcidRequests[nid] = now;
                Networking.Networking.RPC<NID>(
                    PID.TargetServer,
                    new RpcSignature<NID>(RequestWorkshopHeroAcidRPC),
                    nid,
                    false);
                DiagnosticLog.Trace(
                    "PLAYER_ACID authority-request; nid=" + nid + "; role=client.");
            }
            catch (Exception exception)
            {
                DiagnosticLog.Warning(
                    "PLAYER_ACID Workshop client authority request failed: " + exception.Message);
            }
        }


        private static void TryApplyWorkshopLocalHeroAcid(
            TestVanDammeAnim character,
            string source)
        {
            if (!IsWorkshopAcidAuthoritySession() || !IsOnlineHost() ||
                character == null || !character.IsHero || !character.IsMine ||
                character.health <= 0 || character.hasBeenCoverInAcid ||
                character.invulnerable || !character.canBeCoveredInAcid)
            {
                return;
            }

            TryBroadcastWorkshopHeroAcid(character, source);
            if (character.hasBeenCoverInAcid)
            {
                return;
            }

            try
            {
                InvokeNativeCoverInAcidRPC(character);
                DiagnosticLog.Trace(
                    "PLAYER_ACID authority-applied; nid=" + Registry.GetNID(character) +
                    "; source=" + source + "; local=true.");
            }
            catch (Exception exception)
            {
                DiagnosticLog.Warning(
                    "PLAYER_ACID Workshop host local apply failed: " + exception.Message);
            }
        }

        private static void TryBroadcastWorkshopHeroAcid(
            TestVanDammeAnim character,
            string source)
        {
            if (!IsWorkshopAcidAuthoritySession() || !IsOnlineHost() ||
                character == null || !character.IsHero || character.health <= 0 ||
                character.hasBeenCoverInAcid || character.invulnerable ||
                !character.canBeCoveredInAcid)
            {
                return;
            }

            var nid = Registry.GetNID(character);
            if (nid == NID.NoID)
            {
                return;
            }

            var now = UnityEngine.Time.unscaledTime;
            float previousApplyAt;
            if (LastWorkshopAuthorityAcidAt.TryGetValue(nid, out previousApplyAt) &&
                now - previousApplyAt < WorkshopAcidRequestRetrySeconds)
            {
                return;
            }
            LastWorkshopAuthorityAcidAt[nid] = now;
            Networking.Networking.RPC<NID>(
                PID.TargetAll,
                new RpcSignature<NID>(ApplyWorkshopHeroAcidRPC),
                nid,
                false);
            DiagnosticLog.InfoFileOnly(
                "PLAYER_ACID authority-apply; nid=" + nid + "; source=" + source + ".");
        }

        [AllowedRPC]
        private static void RequestWorkshopHeroAcidRPC(NID nid)
        {
            if (!IsWorkshopAcidAuthoritySession() || !IsOnlineHost() || nid == NID.NoID)
            {
                return;
            }

            try
            {
                var character = Registry.GetObject(nid) as TestVanDammeAnim;
                if (character == null || !character.IsHero || character.IsMine ||
                    character.health <= 0 || character.hasBeenCoverInAcid ||
                    character.invulnerable || !character.canBeCoveredInAcid)
                {
                    return;
                }

                if (!HasWorkshopAcidAt(character))
                {
                    DiagnosticLog.Trace(
                        "PLAYER_ACID authority-reject; nid=" + nid + "; reason=host-map-no-acid.");
                    return;
                }

                TryBroadcastWorkshopHeroAcid(character, "host-map-request");
            }
            catch (Exception exception)
            {
                DiagnosticLog.Warning(
                    "PLAYER_ACID Workshop host authority validation failed: " + exception.Message);
            }
        }

        [AllowedRPC]
        private static void ApplyWorkshopHeroAcidRPC(NID nid)
        {
            if (!IsWorkshopAcidAuthoritySession() || nid == NID.NoID)
            {
                return;
            }

            try
            {
                var character = Registry.GetObject(nid) as TestVanDammeAnim;
                if (character == null || !character.IsHero)
                {
                    if (UnresolvedWorkshopAcidApplyDiagnostics.Add(nid))
                    {
                        DiagnosticLog.Warning(
                            "PLAYER_ACID authority apply could not resolve hero NID; nid=" +
                            nid + ".");
                    }
                    return;
                }

                if (character.health <= 0 ||
                    character.hasBeenCoverInAcid || character.invulnerable ||
                    !character.canBeCoveredInAcid)
                {
                    return;
                }

                // The native RPC API exposes no caller PID. Revalidate any apply that
                // executes on the host so a remote cannot bypass the host map check.
                if (IsOnlineHost() && !HasWorkshopAcidAt(character))
                {
                    DiagnosticLog.Trace(
                        "PLAYER_ACID authority-reject; nid=" + nid +
                        "; reason=host-apply-map-no-acid.");
                    return;
                }

                PendingWorkshopAcidRequests.Remove(nid);
                InvokeNativeCoverInAcidRPC(character);
                DiagnosticLog.Trace(
                    "PLAYER_ACID authority-applied; nid=" + nid + ".");
            }
            catch (Exception exception)
            {
                DiagnosticLog.Warning(
                    "PLAYER_ACID Workshop authority apply failed: " + exception.Message);
            }
        }

        private static void InvokeNativeCoverInAcidRPC(TestVanDammeAnim character)
        {
            if (character == null || _nativeCoverInAcidRpcMethod == null)
            {
                return;
            }

            try
            {
                _nativeCoverInAcidRpcMethod.Invoke(character, null);
            }
            catch (TargetInvocationException exception)
            {
                throw exception.InnerException ?? exception;
            }
        }

        private static void ClearWorkshopAcidAuthorityState()
        {
            PendingWorkshopAcidRequests.Clear();
            LastWorkshopAuthorityAcidAt.Clear();
            ClearWorkshopAcidPoolCache();
            WorkshopAcidAuthorityGateDiagnostics.Clear();
            UnresolvedWorkshopAcidApplyDiagnostics.Clear();
        }

        private static void ClearWorkshopAcidPoolCache()
        {
            WorkshopAcidScanCacheByHero.Clear();
            _workshopAcidPools = new WorkshopAcidPoolCacheEntry[0];
            _workshopAcidPoolSceneName = string.Empty;
            _workshopAcidPoolsRefreshedAt = float.NegativeInfinity;
            _nextWorkshopAcidAuthorityScanAt = float.NegativeInfinity;
            _workshopAcidPoolRefreshWarningLogged = false;
        }

        private static bool CoverInAcidDiagnosticsPrefix(
            TestVanDammeAnim __instance,
            ref bool __state)
        {
            PerformanceTelemetry.Count(PerformanceMetric.AcidHook);
            __state = true;
            var suppressNativeFallback = false;
            LogAcidObservation("CoverInAcid", "before", __instance, -1);
            if (__instance == null || !__instance.IsHero)
            {
                return true;
            }

            try
            {
                var networkSessionActive = _networkSessionActive;
                var isOnline = IsOnline();
                var injectedForSession = _injectedForSession;
                var hasValidWorkshopInjectionConfiguration =
                    HasValidWorkshopInjectionConfiguration();
                var isConfiguredWorkshopSceneActive = IsConfiguredWorkshopSceneActive();
                var isConfiguredWorkshopGameState = IsConfiguredWorkshopGameState();
                var configuredSceneSession =
                    networkSessionActive && isOnline &&
                    hasValidWorkshopInjectionConfiguration &&
                    isConfiguredWorkshopSceneActive;
                suppressNativeFallback = configuredSceneSession;
                var authoritySession =
                    configuredSceneSession &&
                    (injectedForSession || _sessionWorkshopIdentityAdopted ||
                     isConfiguredWorkshopGameState);
                var isOnlineHost = IsOnlineHost();
                var decision = authoritySession
                    ? (isOnlineHost ? "host-check" : "client-request")
                    : (configuredSceneSession ? "authority-wait" : "native-fallback");
                LogWorkshopAcidAuthorityGate(
                    __instance,
                    isOnlineHost,
                    networkSessionActive,
                    isOnline,
                    injectedForSession,
                    hasValidWorkshopInjectionConfiguration,
                    isConfiguredWorkshopSceneActive,
                    isConfiguredWorkshopGameState,
                    decision);
                if (!authoritySession)
                {
                    if (!configuredSceneSession)
                    {
                        return true;
                    }

                    __state = false;
                    return false;
                }

                __state = false;
                if (isOnlineHost)
                {
                    if (HasWorkshopAcidAt(__instance))
                    {
                        TryApplyWorkshopLocalHeroAcid(__instance, "host-cover-entry");
                    }
                }
                else if (__instance.IsMine)
                {
                    RequestWorkshopHeroAcid(__instance);
                    if (HasWorkshopAcidAt(__instance))
                    {
                        try
                        {
                            InvokeNativeCoverInAcidRPC(__instance);
                            DiagnosticLog.Trace(
                                "PLAYER_ACID authority-applied; nid=" +
                                Registry.GetNID(__instance) +
                                "; source=client-local-predict; local=true.");
                        }
                        catch (Exception exception)
                        {
                            DiagnosticLog.Warning(
                                "PLAYER_ACID Workshop client local apply failed: " +
                                exception.Message);
                        }
                    }
                }

                // All remote mirrors wait for ApplyWorkshopHeroAcidRPC.
                return false;
            }
            catch (Exception exception)
            {
                __state = !suppressNativeFallback;
                DiagnosticLog.Warning(
                    "PLAYER_ACID Workshop authority CoverInAcid prefix failed " +
                    (suppressNativeFallback ? "closed" : "open") + ": " +
                    exception.Message);
                return !suppressNativeFallback;
            }
        }

        private static void CoverInAcidDiagnosticsPostfix(
            TestVanDammeAnim __instance,
            bool __state)
        {
            if (__state)
            {
                LogAcidObservation("CoverInAcid", "after", __instance, -1);
            }
        }

        private static void LogWorkshopAcidAuthorityGate(
            TestVanDammeAnim character,
            bool isOnlineHost,
            bool networkSessionActive,
            bool isOnline,
            bool injectedForSession,
            bool hasValidWorkshopInjectionConfiguration,
            bool isConfiguredWorkshopSceneActive,
            bool isConfiguredWorkshopGameState,
            string decision)
        {
            var nid = ReadAcidNid(character);
            var stateKey =
                nid + "|" +
                (character != null && character.IsHero ? "1" : "0") +
                (character != null && character.IsMine ? "1" : "0") +
                (isOnlineHost ? "1" : "0") +
                (networkSessionActive ? "1" : "0") +
                (isOnline ? "1" : "0") +
                (injectedForSession ? "1" : "0") +
                (hasValidWorkshopInjectionConfiguration ? "1" : "0") +
                (isConfiguredWorkshopSceneActive ? "1" : "0") +
                (isConfiguredWorkshopGameState ? "1" : "0") +
                "|" + decision;
            if (!WorkshopAcidAuthorityGateDiagnostics.Add(stateKey))
            {
                return;
            }

            var player = GetAcidPlayer(character);
            var playerNum = player == null
                ? "n/a"
                : player.playerNum.ToString(CultureInfo.InvariantCulture);
            var message =
                "PLAYER_ACID event=authority-gate" +
                "; playerNum=" + playerNum +
                "; nid=" + nid +
                "; isHero=" + (character != null && character.IsHero) +
                "; isMine=" + (character != null && character.IsMine) +
                "; isOnlineHost=" + isOnlineHost +
                "; networkSessionActive=" + networkSessionActive +
                "; isOnline=" + isOnline +
                "; injectedForSession=" + injectedForSession +
                "; hasValidWorkshopInjectionConfiguration=" +
                hasValidWorkshopInjectionConfiguration +
                "; isConfiguredWorkshopSceneActive=" +
                isConfiguredWorkshopSceneActive +
                "; isConfiguredWorkshopGameState=" +
                isConfiguredWorkshopGameState +
                "; decision=" + decision + ".";
            DiagnosticLog.InfoFileOnly(message);
            DiagnosticLog.Trace(message);
        }

        private static void CoverInAcidRpcDiagnosticsPrefix(TestVanDammeAnim __instance)
        {
            PerformanceTelemetry.Count(PerformanceMetric.AcidHook);
            LogAcidObservation("CoverInAcidRPC", "before", __instance, -1);
        }

        private static void CoverInAcidRpcDiagnosticsPostfix(TestVanDammeAnim __instance)
        {
            PerformanceTelemetry.Count(PerformanceMetric.AcidHook);
            LogAcidObservation("CoverInAcidRPC", "after", __instance, -1);
        }

        private static void PlayerHasDiedRpcDiagnosticsPrefix(
            int playerNum)
        {
            LogAcidObservation(
                "PlayerHasDiedRPC",
                "before",
                GetAcidCharacterForPlayer(playerNum),
                playerNum);
        }

        private static void PlayerHasDiedRpcDiagnosticsPostfix(
            int playerNum)
        {
            LogAcidObservation(
                "PlayerHasDiedRPC",
                "after",
                GetAcidCharacterForPlayer(playerNum),
                playerNum);
        }

        private static void LogAcidObservation(
            string eventName,
            string phase,
            TestVanDammeAnim character,
            int requestedPlayerNum)
        {
            var observationStartedAt = PerformanceTelemetry.Begin(PerformanceMetric.AcidObservation);
            try
            {
                var player = GetAcidPlayer(character);
                var playerNum = player == null
                    ? (requestedPlayerNum < 0 ? "n/a" : requestedPlayerNum.ToString(CultureInfo.InvariantCulture))
                    : player.playerNum.ToString(CultureInfo.InvariantCulture);
                var requested = requestedPlayerNum < 0
                    ? "n/a"
                    : requestedPlayerNum.ToString(CultureInfo.InvariantCulture);
                var message =
                    "PLAYER_ACID event=" + eventName +
                    "; phase=" + phase +
                    "; playerNum=" + playerNum +
                    "; requestedPlayerNum=" + requested +
                    "; nid=" + ReadAcidNid(character) +
                    "; isMine=" + ReadAcidIsMine(character) +
                    "; position=" + ReadAcidPosition(character) +
                    "; acidMeltTimer=" + ReadAcidField(character, "acidMeltTimer") +
                    "; hasBeenCoverInAcid=" + ReadAcidField(character, "hasBeenCoverInAcid");
                DiagnosticLog.InfoFileOnly(message);
                DiagnosticLog.Trace(message);
            }
            catch (Exception exception)
            {
                DiagnosticLog.Warning(
                    "PLAYER_ACID observation failed for " + eventName + ": " + exception.Message);
            }
            finally
            {
                PerformanceTelemetry.End(
                    PerformanceMetric.AcidObservation,
                    observationStartedAt);
            }
        }

        private static Player GetAcidPlayer(TestVanDammeAnim character)
        {
            if (character == null)
            {
                return null;
            }

            try
            {
                return GetFieldOrPropertyValue(character, "player") as Player;
            }
            catch
            {
                return null;
            }
        }

        private static TestVanDammeAnim GetAcidCharacterForPlayer(int playerNum)
        {
            try
            {
                if (HeroController.players == null || playerNum < 0 ||
                    playerNum >= HeroController.players.Length ||
                    HeroController.players[playerNum] == null)
                {
                    return null;
                }

                return HeroController.players[playerNum].character;
            }
            catch
            {
                return null;
            }
        }

        private static string ReadAcidNid(TestVanDammeAnim character)
        {
            if (character == null)
            {
                return "n/a";
            }

            try
            {
                return character.Nid.ToString();
            }
            catch (Exception exception)
            {
                return "error:" + exception.GetType().Name;
            }
        }

        private static string ReadAcidIsMine(TestVanDammeAnim character)
        {
            if (character == null)
            {
                return "n/a";
            }

            try
            {
                return character.IsMine.ToString();
            }
            catch (Exception exception)
            {
                return "error:" + exception.GetType().Name;
            }
        }

        private static string ReadAcidPosition(TestVanDammeAnim character)
        {
            if (character == null)
            {
                return "n/a";
            }

            try
            {
                return FormatVector3(character.transform.position);
            }
            catch (Exception exception)
            {
                return "error:" + exception.GetType().Name;
            }
        }

        private static string ReadAcidField(TestVanDammeAnim character, string fieldName)
        {
            if (character == null)
            {
                return "n/a";
            }

            try
            {
                var value = GetFieldOrPropertyValue(character, fieldName);
                return value == null
                    ? "n/a"
                    : Convert.ToString(value, CultureInfo.InvariantCulture);
            }
            catch (Exception exception)
            {
                return "error:" + exception.GetType().Name;
            }
        }
    }
}
