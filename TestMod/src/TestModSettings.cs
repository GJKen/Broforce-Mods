using UnityModManagerNet;
using UnityEngine;

namespace TestMod
{
    public sealed class TestModSettings : UnityModManager.ModSettings
    {
        public bool EnableAutoKill;
        public string Language;
        public string SettingsPanel;
        public bool EnableGameSpeed;
        public float GameSpeed;
        public KeyCode PauseKey;
        public KeyCode FrameStepKey;
        public bool HoldToStep;
        public int FrameStepRepeatRate;

        public TestModSettings()
        {
            EnableAutoKill = true;
            Language = "system";
            SettingsPanel = "speed";
            GameSpeed = 1f;
            PauseKey = KeyCode.None;
            FrameStepKey = KeyCode.None;
            HoldToStep = true;
            FrameStepRepeatRate = 10;
        }
    }
}
