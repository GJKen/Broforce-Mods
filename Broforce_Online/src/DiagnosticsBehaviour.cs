using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace BroforceOnlineDiagnostics
{
    internal sealed class DiagnosticsBehaviour : MonoBehaviour
    {
        private const float SnapshotIntervalSeconds = 2f;
        private const float HeroFallbackCheckIntervalSeconds = 1f;
        private const float HeroFallbackDelaySeconds = 18f;
        private const float UnityErrorDuplicateWindowSeconds = 5f;
        private const int MaxUnityErrorStackLength = 4000;
        private const int MaxUnityErrorSignatureLength = 512;
        private const int MaxUnityErrorStates = 64;

        private float _nextSnapshotAt;
        private float _nextHeroFallbackCheckAt;
        private string _lastScene;
        private string _lastModeHint;
        private string _lastNetworkHint;
        private readonly Dictionary<int, HeroFallbackState> _heroFallbackStates =
            new Dictionary<int, HeroFallbackState>();
        private readonly HashSet<int> _fallbackResponseGuards = new HashSet<int>();
        private readonly Dictionary<string, UnityErrorState> _unityErrorStates =
            new Dictionary<string, UnityErrorState>();
        private FrpDirectTransport _frpDirectTransport;

        public static DiagnosticsBehaviour Create()
        {
            var gameObject = new GameObject("BroforceOnlineDiagnostics");
            DontDestroyOnLoad(gameObject);
            var behaviour = gameObject.AddComponent<DiagnosticsBehaviour>();
            behaviour.Initialize();
            return behaviour;
        }

        public void Stop()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            Application.logMessageReceived -= OnUnityLog;
            FrpDirectNetworkManager.Stop();
            if (_frpDirectTransport != null)
            {
                _frpDirectTransport.Dispose();
                _frpDirectTransport = null;
            }
            if (gameObject != null)
            {
                Destroy(gameObject);
            }
        }

        private void Initialize()
        {
            SceneManager.sceneLoaded += OnSceneLoaded;
            Application.logMessageReceived += OnUnityLog;
            DiagnosticLog.Info("Initial scene: " + DescribeScene(SceneManager.GetActiveScene()));
            DiagnosticLog.Info("Persistent data path: " + Application.persistentDataPath);
            DiagnosticLog.Info("Unity version: " + Application.unityVersion);
            _nextSnapshotAt = Time.unscaledTime;
            _nextHeroFallbackCheckAt = Time.unscaledTime;
            _frpDirectTransport = new FrpDirectTransport();
            _frpDirectTransport.Apply(Plugin.Settings, true);
            FrpDirectNetworkManager.ApplyConfiguredLayer(_frpDirectTransport);
        }

        private void Update()
        {
            HarmonyDiagnostics.Update();
            if (_frpDirectTransport != null)
            {
                _frpDirectTransport.Update();
            }

            var now = Time.unscaledTime;
            if (now >= _nextSnapshotAt)
            {
                _nextSnapshotAt = now + SnapshotIntervalSeconds;
                EmitSnapshot();
            }

            if (now >= _nextHeroFallbackCheckAt)
            {
                _nextHeroFallbackCheckAt = now + HeroFallbackCheckIntervalSeconds;
                RecoverStalledLocalHeroRequests(now);
            }
        }

        internal void ApplyFrpDirectSettings(bool forceRestart)
        {
            if (_frpDirectTransport != null)
            {
                _frpDirectTransport.Apply(Plugin.Settings, forceRestart);
                FrpDirectNetworkManager.ApplyConfiguredLayer(_frpDirectTransport);
            }
        }

        internal FrpDirectTransport GetFrpDirectTransport()
        {
            return _frpDirectTransport;
        }

        internal string GetFrpDirectStatus()
        {
            return _frpDirectTransport == null ? "Disabled" : _frpDirectTransport.Status;
        }

        private void RecoverStalledLocalHeroRequests(float now)
        {
            var settings = Plugin.Settings;
            var configuredScene = settings == null
                ? string.Empty
                : (settings.WorkshopSceneName ?? string.Empty).Trim();
            if (settings == null || !settings.EnableOnlineWorkshopInjection ||
                string.IsNullOrEmpty(configuredScene) ||
                !string.Equals(SceneManager.GetActiveScene().name, configuredScene, StringComparison.OrdinalIgnoreCase))
            {
                _heroFallbackStates.Clear();
                return;
            }

            var players = HeroController.players;
            if (players == null)
            {
                return;
            }

            for (var index = 0; index < players.Length; index++)
            {
                var player = players[index];
                if (player == null)
                {
                    continue;
                }

                var key = player.GetInstanceID();
                if (!player.IsMine || player.character != null || !player.awaitingHeroTypeFromServer)
                {
                    _heroFallbackStates.Remove(key);
                    continue;
                }

                HeroFallbackState state;
                if (!_heroFallbackStates.TryGetValue(key, out state))
                {
                    state = new HeroFallbackState(now + HeroFallbackDelaySeconds);
                    _heroFallbackStates.Add(key, state);
                    continue;
                }

                if (now < state.FallbackAt)
                {
                    continue;
                }

                if (!state.FallbackUsed)
                {
                    UseLocalHeroFallback(player, state);
                }
            }
        }

        private void UseLocalHeroFallback(Player player, HeroFallbackState state)
        {
            state.FallbackUsed = true;
            try
            {
                HeroType heroType;
                if (HarmonyDiagnostics.TryGetWorkshopRejoinHeroType(player.playerNum, out heroType))
                {
                    DiagnosticLog.Info(
                        "Using the saved Workshop hero type for local dropout rejoin fallback: player=" +
                        player.playerNum + "; hero=" + heroType + ".");
                }
                else
                {
                    var unlockedHeroes = HeroUnlockController.GetUnlockedHeroes(true);
                    var availableHeroes = unlockedHeroes == null
                        ? new List<HeroType>()
                        : new List<HeroType>(unlockedHeroes);
                    var yetToBeSeenHeroes = PlayerProgress.Instance == null ||
                                            PlayerProgress.Instance.yetToBePlayedUnlockedHeroes == null
                        ? new List<HeroType>()
                        : new List<HeroType>(PlayerProgress.Instance.yetToBePlayedUnlockedHeroes);
                    heroType = HeroController.GetHeroType(
                        player.playerNum,
                        availableHeroes,
                        yetToBeSeenHeroes,
                        true);
                }

                _fallbackResponseGuards.Add(player.GetInstanceID());
                player.awaitingHeroTypeFromServer = false;
                DiagnosticLog.Warning(
                    "Using local hero fallback for player " + player.playerNum +
                    " after " + HeroFallbackDelaySeconds + " seconds without a reply; hero=" + heroType + ".");
                player.SpawnHero(heroType);
            }
            catch (Exception exception)
            {
                DiagnosticLog.Error(
                    "Local hero fallback failed for player " + player.playerNum + ": " + exception);
            }
        }

        internal bool ShouldSkipLateHeroResponse(int playerNum)
        {
            var players = HeroController.players;
            if (players == null || playerNum < 0 || playerNum >= players.Length)
            {
                return false;
            }

            var player = players[playerNum];
            if (player == null || !player.IsMine)
            {
                return false;
            }

            var key = player.GetInstanceID();
            if (!_fallbackResponseGuards.Contains(key))
            {
                return false;
            }

            return !player.awaitingHeroTypeFromServer;
        }

        private void EmitSnapshot()
        {
            var scene = DescribeScene(SceneManager.GetActiveScene());
            if (scene != _lastScene)
            {
                _lastScene = scene;
                DiagnosticLog.Info("Active scene changed: " + scene);
            }

            var modeHint = ReflectionProbe.FindModeHint();
            if (modeHint != _lastModeHint)
            {
                _lastModeHint = modeHint;
                if (!string.IsNullOrEmpty(modeHint))
                {
                    DiagnosticLog.Info("Mode hint changed: " + modeHint);
                }
            }

            var networkHint = ReflectionProbe.FindNetworkHint();
            if (networkHint != _lastNetworkHint)
            {
                _lastNetworkHint = networkHint;
                if (!string.IsNullOrEmpty(networkHint))
                {
                    DiagnosticLog.Info("Network hint changed: " + networkHint);
                }
            }
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            _heroFallbackStates.Clear();
            _fallbackResponseGuards.Clear();
            HarmonyDiagnostics.NotifySceneLoaded(scene);
            DiagnosticLog.Info("Scene loaded: " + DescribeScene(scene) + "; loadMode=" + mode);
        }

        private void OnUnityLog(string condition, string stackTrace, LogType type)
        {
            if (type != LogType.Exception && type != LogType.Error)
            {
                return;
            }

            var safeCondition = condition ?? string.Empty;
            var safeStackTrace = stackTrace ?? string.Empty;
            var signature = BuildUnityErrorSignature(type, safeCondition);
            var now = Time.unscaledTime;
            UnityErrorState state;
            if (_unityErrorStates.TryGetValue(signature, out state) && now < state.NextWriteAt)
            {
                state.SuppressedCount++;
                return;
            }

            if (state == null)
            {
                if (_unityErrorStates.Count >= MaxUnityErrorStates)
                {
                    _unityErrorStates.Clear();
                }

                state = new UnityErrorState(signature);
                _unityErrorStates.Add(signature, state);
            }
            else if (state.SuppressedCount > 0)
            {
                DiagnosticLog.Warning(
                    "Suppressed " + state.SuppressedCount +
                    " repeated Unity errors: " + state.Signature);
            }

            state.SuppressedCount = 0;
            state.NextWriteAt = now + UnityErrorDuplicateWindowSeconds;
            DiagnosticLog.Error("Unity log: " + FormatUnityError(safeCondition, safeStackTrace));
        }

        private static string BuildUnityErrorSignature(LogType type, string condition)
        {
            var firstLine = condition ?? string.Empty;
            var lineBreak = firstLine.IndexOfAny(new[] { '\r', '\n' });
            if (lineBreak >= 0)
            {
                firstLine = firstLine.Substring(0, lineBreak);
            }

            firstLine = firstLine.Trim();
            if (firstLine.Length > MaxUnityErrorSignatureLength)
            {
                firstLine = firstLine.Substring(0, MaxUnityErrorSignatureLength) + "...";
            }

            return type + "|" + firstLine;
        }

        private static string FormatUnityError(string condition, string stackTrace)
        {
            if (string.IsNullOrEmpty(stackTrace))
            {
                return condition;
            }

            var value = condition + "\n" + stackTrace;
            if (value.Length > MaxUnityErrorStackLength)
            {
                value = value.Substring(0, MaxUnityErrorStackLength) + "...";
            }

            return value;
        }

        private static string DescribeScene(Scene scene)
        {
            return string.Format("name={0}, buildIndex={1}, path={2}, loaded={3}", scene.name, scene.buildIndex, scene.path, scene.isLoaded);
        }

        private sealed class HeroFallbackState
        {
            public HeroFallbackState(float fallbackAt)
            {
                FallbackAt = fallbackAt;
            }

            public float FallbackAt { get; set; }
            public bool FallbackUsed { get; set; }
        }

        private sealed class UnityErrorState
        {
            public UnityErrorState(string signature)
            {
                Signature = signature;
            }

            public string Signature { get; private set; }
            public int SuppressedCount { get; set; }
            public float NextWriteAt { get; set; }
        }
    }
}
