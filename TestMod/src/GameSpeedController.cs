using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace TestMod
{
    [DefaultExecutionOrder(32000)]
    internal sealed class GameSpeedController : MonoBehaviour
    {
        private const int StepFrameRate = 60;
        private const float FrameStepHoldDelay = 0.35f;
        private bool _controlsTime;
        private float _originalTimeScale;
        private float _originalFixedDeltaTime;
        private int _originalCaptureFramerate;
        private int _pendingSteps;
        private bool _stepInFlight;
        private bool _frameStepHeld;
        private bool _repeatStepRequested;
        private float _nextRepeatAt;

        internal bool IsPaused { get; private set; }
        internal bool IsFrozen { get { return IsPaused && !_stepInFlight; } }
        internal int SteppedFrames { get; private set; }

        internal void ApplySettings()
        {
            if (!Plugin.Settings.EnableGameSpeed)
            {
                ReleaseTime();
                return;
            }

            if (!_controlsTime)
            {
                _originalTimeScale = Time.timeScale;
                _originalFixedDeltaTime = Time.fixedDeltaTime;
                _originalCaptureFramerate = Time.captureFramerate;
                _controlsTime = true;
            }

            Time.timeScale = IsPaused ? (_stepInFlight ? 1f : 0f) : Plugin.Settings.GameSpeed;
        }

        internal void SetPaused(bool paused)
        {
            SetPaused(paused, false);
        }

        internal void SetPaused(bool paused, bool preserveHeldKey)
        {
            IsPaused = paused;
            _pendingSteps = 0;
            _stepInFlight = false;
            if (!paused || !preserveHeldKey) ResetFrameStepRepeat();
            if (_controlsTime)
            {
                RestoreStepTiming();
            }

            ApplySettings();
        }

        internal bool RequestFrameStep()
        {
            if (!Plugin.CanStepGameFrame)
            {
                return false;
            }

            if (!IsPaused)
            {
                SetPaused(true);
            }

            _pendingSteps = System.Math.Max(_pendingSteps, 1);
            return true;
        }

        internal void QueueNetworkFrameStep()
        {
            _pendingSteps++;
        }

        private void Update()
        {
            Plugin.UpdateGameSpeedNetwork();
            if (Plugin.CapturingHotkey || !Application.isFocused ||
                PauseController.pauseStatus != PauseStatus.UnPaused)
            {
                ResetFrameStepRepeat();
                return;
            }

            var settings = Plugin.Settings;
            if (settings.PauseKey != KeyCode.None && Input.GetKeyDown(settings.PauseKey))
            {
                Plugin.SetGamePaused(!IsPaused);
            }
            else if (settings.FrameStepKey != KeyCode.None && Input.GetKeyDown(settings.FrameStepKey))
            {
                _frameStepHeld = Plugin.StepOneFrame() && settings.HoldToStep;
                _nextRepeatAt = Time.realtimeSinceStartup + FrameStepHoldDelay;
            }
            else if (!_frameStepHeld || !settings.HoldToStep ||
                settings.FrameStepKey == KeyCode.None || !Input.GetKey(settings.FrameStepKey))
            {
                ResetFrameStepRepeat();
            }
            else if (Time.realtimeSinceStartup >= _nextRepeatAt)
            {
                // Repeats are transient requests; do not save settings on every frame.
                _repeatStepRequested = true;
                var interval = 1f / settings.FrameStepRepeatRate;
                _nextRepeatAt += interval;
                if (_nextRepeatAt < Time.realtimeSinceStartup)
                    _nextRepeatAt = Time.realtimeSinceStartup + interval;
            }
        }

        private void ResetFrameStepRepeat()
        {
            _frameStepHeld = false;
            _repeatStepRequested = false;
        }

        // Schedule from the end of a frame: Unity samples the next deltaTime before Update.
        // Closing the step here lets every game Update and LateUpdate finish exactly once.
        private void LateUpdate()
        {
            if (_repeatStepRequested)
            {
                _repeatStepRequested = false;
                Plugin.RequestGameSpeedStep();
            }

            if (!_controlsTime)
            {
                return;
            }

            if (_stepInFlight)
            {
                _stepInFlight = false;
                SteppedFrames++;
                RestoreStepTiming();
            }

            if (IsPaused && _pendingSteps > 0 && Plugin.CanStepGameFrame)
            {
                _pendingSteps--;
                _stepInFlight = true;
                Time.captureFramerate = StepFrameRate;
                Time.fixedDeltaTime = 1f / StepFrameRate;
                Time.timeScale = 1f;
            }
            else
            {
                Time.timeScale = IsPaused ? 0f : Plugin.Settings.GameSpeed;
            }

            if (IsFrozen) Plugin.UpdateFrozenNetworkObjects();
        }

        private void RestoreStepTiming()
        {
            Time.captureFramerate = _originalCaptureFramerate;
            Time.fixedDeltaTime = _originalFixedDeltaTime;
        }

        internal void ReleaseTime()
        {
            IsPaused = false;
            _pendingSteps = 0;
            _stepInFlight = false;
            ResetFrameStepRepeat();
            if (_controlsTime)
            {
                _controlsTime = false;
                RestoreStepTiming();
                Time.timeScale = _originalTimeScale;
            }
        }

        private void OnDisable()
        {
            ReleaseTime();
        }
    }

    // Zero deltaTime alone still runs input and divides by timeScale in several bros.
    // Freeze the game's frame callbacks too; UMM and mod callbacks remain responsive.
    [HarmonyPatch]
    internal static class FrozenGameFramePatch
    {
        private static IEnumerable<MethodBase> TargetMethods()
        {
            foreach (var type in typeof(TestVanDammeAnim).Assembly.GetTypes())
            {
                if (!typeof(MonoBehaviour).IsAssignableFrom(type))
                {
                    continue;
                }

                foreach (var method in type.GetMethods(BindingFlags.Instance | BindingFlags.Public |
                    BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
                {
                    if (!method.IsAbstract && !method.ContainsGenericParameters &&
                        method.ReturnType == typeof(void) && method.GetParameters().Length == 0 &&
                        (method.Name == "Update" || method.Name == "LateUpdate" || method.Name == "FixedUpdate") &&
                        type != typeof(TimeController) && type != typeof(PauseController) &&
                        !IsNetworkService(type))
                    {
                        yield return method;
                    }
                }
            }
        }

        internal static bool IsNetworkService(Type type)
        {
            // Steam callbacks, transport, RPC dispatch and state exchange must survive a freeze.
            return type == typeof(Connect) || type == typeof(RPCController) ||
                type == typeof(RPCBatcher) || type == typeof(PingController) ||
                type == typeof(SyncController) || type == typeof(NetworkTimeSync) ||
                type.FullName == "SteamManager";
        }

        private static bool Prefix()
        {
            return !Plugin.IsGameFrameFrozen;
        }
    }

    [HarmonyPatch(typeof(TimeController), "Update")]
    internal static class GameTimeUpdatePatch
    {
        private static bool Prefix()
        {
            return !Plugin.IsGameSpeedEnabled;
        }
    }

    [HarmonyPatch(typeof(TimeController), "StopTime")]
    internal static class GameSlowMotionPatch
    {
        private static bool Prefix()
        {
            return !Plugin.IsGameSpeedEnabled;
        }
    }
}
