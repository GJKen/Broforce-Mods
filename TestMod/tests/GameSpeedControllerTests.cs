// Offline state-machine tests: compile with the real GameSpeedController and settings.
using System;
using System.Reflection;
using TestMod;

internal static class GameSpeedControllerTests
{
    private static GameSpeedController _controller;
    private static int _checks;

    private static void Check(bool condition, string description)
    {
        _checks++;
        if (!condition) throw new Exception(description);
    }

    private static void Tick(string callback)
    {
        typeof(GameSpeedController).GetMethod(callback, BindingFlags.Instance | BindingFlags.NonPublic)
            .Invoke(_controller, null);
    }

    private static bool AllowsGameFrame()
    {
        return (bool)typeof(FrozenGameFramePatch).GetMethod("Prefix",
            BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, null);
    }

    private static void CheckOriginalTiming()
    {
        Check(UnityEngine.Time.fixedDeltaTime == 0.02f, "Restore fixed step");
        Check(UnityEngine.Time.captureFramerate == 24, "Restore pre-existing capture frame rate");
    }

    private static void HoldInput(float realTime, bool keyDown, bool held)
    {
        UnityEngine.Time.realtimeSinceStartup = realTime;
        UnityEngine.Input.Pressed = keyDown ? UnityEngine.KeyCode.N : UnityEngine.KeyCode.None;
        UnityEngine.Input.Held = held ? UnityEngine.KeyCode.N : UnityEngine.KeyCode.None;
        Tick("Update");
    }

    private static void FinishStep()
    {
        Tick("LateUpdate");
        Tick("LateUpdate");
    }

    private static void CheckHoldToStep()
    {
        var start = _controller.SteppedFrames;
        Check(Plugin.Settings.HoldToStep && Plugin.Settings.FrameStepRepeatRate == 10,
            "Hold-to-step is enabled at 10 steps per second by default");
        Plugin.Settings.EnableGameSpeed = true;
        _controller.ApplySettings();
        HoldInput(10f, true, true);
        FinishStep();
        Check(_controller.SteppedFrames == start + 1, "Key down immediately advances one frame");
        var apiCalls = Plugin.StepRequests;
        HoldInput(10.2f, false, true);
        FinishStep();
        Check(_controller.SteppedFrames == start + 1, "A short hold does not start repeat");
        HoldInput(10.351f, false, true);
        FinishStep();
        Check(_controller.SteppedFrames == start + 2, "Repeat starts after the hold delay");
        HoldInput(10.44f, false, true);
        FinishStep();
        Check(_controller.SteppedFrames == start + 2, "Default repeat waits 100ms between steps");
        HoldInput(10.452f, false, true);
        FinishStep();
        Check(_controller.SteppedFrames == start + 3 && _controller.IsPaused,
            "Held key continues stepping while game time is paused");
        Check(Plugin.StepRequests == apiCalls, "Repeats bypass the API that saves settings");
        HoldInput(10.6f, false, false);
        FinishStep();
        Check(_controller.SteppedFrames == start + 3 && _controller.IsFrozen, "Releasing the key stops repeat");
        CheckOriginalTiming();

        HoldInput(11f, true, true);
        FinishStep();
        HoldInput(12f, false, true);
        FinishStep();
        Check(_controller.SteppedFrames == start + 5, "A long hitch advances at most one repeat");
        HoldInput(12.01f, false, true);
        FinishStep();
        Check(_controller.SteppedFrames == start + 5, "No burst of catch-up frames after a hitch");

        Plugin.Settings.FrameStepRepeatRate = 1;
        HoldInput(14f, true, true);
        FinishStep();
        HoldInput(14.351f, false, true);
        FinishStep();
        HoldInput(14.8f, false, true);
        FinishStep();
        Check(_controller.SteppedFrames == start + 7, "One-step-per-second rate does not repeat early");
        HoldInput(15.352f, false, true);
        FinishStep();
        Check(_controller.SteppedFrames == start + 8, "One-step-per-second rate repeats after a second");
        Plugin.Settings.FrameStepRepeatRate = 60;
        HoldInput(16f, true, true);
        FinishStep();
        HoldInput(16.351f, false, true);
        FinishStep();
        HoldInput(16.372f, false, true);
        FinishStep();
        Check(_controller.SteppedFrames == start + 11, "60-step rate allows repeats about 17ms apart");

        HoldInput(16.4f, false, true);
        HoldInput(16.41f, false, false);
        FinishStep();
        Check(_controller.SteppedFrames == start + 11, "Release cancels an unstarted repeat request");
        Plugin.Settings.HoldToStep = false;
        HoldInput(17f, true, true);
        FinishStep();
        HoldInput(18f, false, true);
        FinishStep();
        Check(_controller.SteppedFrames == start + 12, "Disabling hold-to-step keeps taps working without repeats");
        Plugin.Settings.HoldToStep = true;

        HoldInput(19f, true, true);
        FinishStep();
        UnityEngine.Application.isFocused = false;
        HoldInput(20f, false, true);
        UnityEngine.Application.isFocused = true;
        HoldInput(21f, false, true);
        FinishStep();
        Check(_controller.SteppedFrames == start + 13, "Regaining focus requires a new key press for repeat");

        HoldInput(22f, true, true);
        FinishStep();
        HoldInput(22.4f, false, true);
        Plugin.CapturingHotkey = true;
        HoldInput(22.41f, false, true);
        Plugin.CapturingHotkey = false;
        HoldInput(23f, false, true);
        FinishStep();
        Check(_controller.SteppedFrames == start + 14, "Binding cancels queued repeats and disarms the held key");

        HoldInput(24f, true, true);
        FinishStep();
        PauseController.pauseStatus = PauseStatus.Paused;
        HoldInput(25f, false, true);
        PauseController.pauseStatus = PauseStatus.UnPaused;
        HoldInput(26f, false, true);
        FinishStep();
        Check(_controller.SteppedFrames == start + 15, "Native pause menu cancels repeat until a new press");

        HoldInput(27f, true, true);
        FinishStep();
        _controller.SetPaused(false);
        HoldInput(28f, false, true);
        FinishStep();
        Check(_controller.SteppedFrames == start + 16 && !_controller.IsPaused,
            "Resume stops repeat even if the key remains held");
        HoldInput(29f, true, true);
        FinishStep();
        Plugin.Settings.EnableGameSpeed = false;
        _controller.ApplySettings();
        HoldInput(30f, false, true);
        FinishStep();
        Check(_controller.SteppedFrames == start + 17 && UnityEngine.Time.timeScale == 0.75f,
            "Disabling speed control stops repeats and restores the original speed");
        CheckOriginalTiming();
    }

    public static void Main()
    {
        Plugin.Settings = new TestModSettings();
        Check(!Plugin.Settings.EnableGameSpeed && Plugin.Settings.GameSpeed == 1f,
            "Speed control is opt-in at normal speed");
        Check(Plugin.Settings.PauseKey == UnityEngine.KeyCode.None &&
            Plugin.Settings.FrameStepKey == UnityEngine.KeyCode.None, "No default hotkeys");
        UnityEngine.Time.timeScale = 0.75f;
        UnityEngine.Time.fixedDeltaTime = 0.02f;
        UnityEngine.Time.captureFramerate = 24;
        _controller = new GameSpeedController();
        Plugin.Controller = _controller;
        _controller.ApplySettings();
        Check(UnityEngine.Time.timeScale == 0.75f, "Inactive control preserves game speed");

        Plugin.Settings.EnableGameSpeed = true;
        Plugin.Settings.GameSpeed = 0.25f;
        _controller.ApplySettings();
        Check(UnityEngine.Time.timeScale == 0.25f, "Apply slow motion");
        Check(AllowsGameFrame(), "Running game callbacks are allowed");
        _controller.SetPaused(true);
        Check(_controller.IsFrozen && UnityEngine.Time.timeScale == 0f, "Freeze immediately");
        Check(!AllowsGameFrame(), "Paused callbacks cannot process input or divide by zero");
        Check(_controller.RequestFrameStep(), "Accept step request");
        Check(_controller.RequestFrameStep(), "Coalesce duplicate requests");
        Check(_controller.IsFrozen, "Request waits for the end of the current frame");
        Tick("LateUpdate");
        Check(!_controller.IsFrozen && _controller.IsPaused, "Allow one frame while remaining paused");
        Check(UnityEngine.Time.timeScale == 1f && UnityEngine.Time.captureFramerate == 60 &&
            UnityEngine.Time.fixedDeltaTime == 1f / 60f, "A step uses normal speed and fixed 1/60 timing");
        Check(AllowsGameFrame(), "Update and LateUpdate can run during the step");
        _controller.ApplySettings();
        Check(UnityEngine.Time.timeScale == 1f, "GUI changes cannot zero the active step's time scale");
        Tick("LateUpdate");
        Check(_controller.IsFrozen && UnityEngine.Time.timeScale == 0f && _controller.SteppedFrames == 1,
            "Step ends after one frame and returns to pause");
        CheckOriginalTiming();
        Tick("LateUpdate");
        Check(_controller.SteppedFrames == 1 && _controller.IsFrozen, "No extra frame without a new press");

        for (var frame = 0; frame < 3; frame++)
        {
            _controller.RequestFrameStep();
            Tick("LateUpdate");
            Tick("LateUpdate");
        }
        Check(_controller.SteppedFrames == 4 && _controller.IsFrozen, "Repeated presses advance separate frames");
        _controller.SetPaused(false);
        Check(UnityEngine.Time.timeScale == 0.25f && !_controller.IsFrozen, "Resume at saved speed");
        PauseController.pauseStatus = PauseStatus.Paused;
        Check(!_controller.RequestFrameStep() && !_controller.IsPaused, "Reject step while native menu is paused");
        PauseController.pauseStatus = PauseStatus.UnPaused;
        _controller.RequestFrameStep();
        _controller.SetPaused(false);
        Tick("LateUpdate");
        Check(_controller.SteppedFrames == 4 && !_controller.IsPaused, "Resume cancels a queued step");

        Plugin.Settings.PauseKey = UnityEngine.KeyCode.P;
        Plugin.Settings.FrameStepKey = UnityEngine.KeyCode.N;
        UnityEngine.Input.Pressed = UnityEngine.KeyCode.P;
        Plugin.CapturingHotkey = true;
        Tick("Update");
        Check(!_controller.IsPaused, "Binding does not trigger a gameplay hotkey");
        Plugin.CapturingHotkey = false;
        UnityEngine.Application.isFocused = false;
        Tick("Update");
        Check(!_controller.IsPaused, "Background input does not trigger pause");
        UnityEngine.Application.isFocused = true;
        Tick("Update");
        Check(_controller.IsPaused, "Pause hotkey works");
        UnityEngine.Input.Pressed = UnityEngine.KeyCode.N;
        Tick("Update");
        UnityEngine.Input.Pressed = UnityEngine.KeyCode.None;
        Tick("LateUpdate");
        Tick("LateUpdate");
        Check(_controller.SteppedFrames == 5 && _controller.IsPaused, "Step hotkey advances one frame");

        _controller.RequestFrameStep();
        Tick("LateUpdate");
        Plugin.Settings.EnableGameSpeed = false;
        _controller.ApplySettings();
        Check(UnityEngine.Time.timeScale == 0.75f && !_controller.IsPaused, "Disabling restores original speed mid-step");
        CheckOriginalTiming();
        Check(AllowsGameFrame(), "Disabling releases frozen game callbacks");
        Plugin.Settings.EnableGameSpeed = true;
        _controller.SetPaused(true);
        Tick("OnDisable");
        Check(UnityEngine.Time.timeScale == 0.75f && !_controller.IsPaused, "Component disable also restores speed");
        CheckOriginalTiming();
        CheckHoldToStep();
        Console.WriteLine("PASS: " + _checks + " game-speed checks");
    }
}

namespace TestMod
{
    internal static class Plugin
    {
        internal static TestModSettings Settings;
        internal static GameSpeedController Controller;
        internal static bool CapturingHotkey;
        internal static bool IsGameFrameFrozen { get { return Controller.IsFrozen; } }
        internal static bool IsGameSpeedEnabled { get { return Settings.EnableGameSpeed; } }
        internal static void SetGamePaused(bool paused) { Controller.SetPaused(paused); }
        internal static int StepRequests;
        internal static bool StepOneFrame() { StepRequests++; return Controller.RequestFrameStep(); }
    }
}

namespace UnityEngine
{
    internal class MonoBehaviour { }
    internal sealed class DefaultExecutionOrder : Attribute
    {
        internal DefaultExecutionOrder(int order) { }
    }
    public enum KeyCode { None, P, N }
    internal static class Time
    {
        internal static float timeScale, fixedDeltaTime, realtimeSinceStartup;
        internal static int captureFramerate;
    }
    internal static class Application { internal static bool isFocused = true; }
    internal static class Input
    {
        internal static KeyCode Pressed;
        internal static KeyCode Held;
        internal static bool GetKeyDown(KeyCode key) { return Pressed == key; }
        internal static bool GetKey(KeyCode key) { return Held == key; }
    }
}

namespace UnityModManagerNet
{
    public static class UnityModManager { public class ModSettings { } }
}

namespace HarmonyLib
{
    internal sealed class HarmonyPatch : Attribute
    {
        internal HarmonyPatch() { }
        internal HarmonyPatch(Type type, string method) { }
    }
}

internal class TestVanDammeAnim : UnityEngine.MonoBehaviour { }
internal class TimeController : UnityEngine.MonoBehaviour { }
internal class PauseController : UnityEngine.MonoBehaviour { internal static PauseStatus pauseStatus; }
internal enum PauseStatus { UnPaused, Paused }
