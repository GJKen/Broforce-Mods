using System;
using TestMod;
using UnityEngine;

internal static class FrameRateControllerTests
{
    private static int _checks;

    private static void Check(int target, int vsync, string description)
    {
        _checks++;
        if (Application.targetFrameRate != target || QualitySettings.vSyncCount != vsync)
            throw new Exception(description);
    }

    private static void Main()
    {
        var controller = new FrameRateController();
        Application.targetFrameRate = 144;
        QualitySettings.vSyncCount = 1;
        controller.ApplySettings();
        Check(144, 1, "Disabled control leaves native settings untouched");
        Plugin.Settings.EnableFrameRateLimit = true;
        controller.ApplySettings();
        Check(60, 0, "Enabled control applies the target and disables VSync");
        Plugin.Settings.TargetFrameRate = 120;
        controller.ApplySettings();
        Check(120, 0, "Changing the limit applies immediately");
        Plugin.Settings.DisableVSync = false;
        controller.ApplySettings();
        Check(120, 1, "Unchecking VSync uses the captured native setting");
        Plugin.Settings.TargetFrameRate = -1;
        controller.ApplySettings();
        Check(-1, 1, "Unlimited preserves the chosen VSync behavior");
        Plugin.Settings.EnableFrameRateLimit = false;
        controller.ApplySettings();
        Check(144, 1, "Disabling restores the original values after several edits");
        Application.targetFrameRate = 240;
        QualitySettings.vSyncCount = 2;
        controller.Release();
        Check(240, 2, "Repeated release does not overwrite later native changes");
        Plugin.Settings.EnableFrameRateLimit = true;
        Plugin.Settings.TargetFrameRate = 30;
        Plugin.Settings.DisableVSync = true;
        controller.ApplySettings();
        Check(30, 0, "Re-enabling applies the saved limit");
        controller.Release();
        Check(240, 2, "Stopping restores the newly captured native values");
        Console.WriteLine("Passed {0} frame rate lifecycle checks.", _checks);
    }
}

namespace TestMod
{
    internal static class Plugin
    {
        internal static readonly TestModSettings Settings = new TestModSettings();
    }

    internal sealed class TestModSettings
    {
        internal bool EnableFrameRateLimit;
        internal int TargetFrameRate = 60;
        internal bool DisableVSync = true;
    }
}

namespace UnityEngine
{
    internal static class Application { internal static int targetFrameRate; }
    internal static class QualitySettings { internal static int vSyncCount; }
}
