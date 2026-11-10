using UnityEngine;

namespace TestMod
{
    internal sealed class FrameRateController
    {
        private bool _captured;
        private int _originalTargetFrameRate;
        private int _originalVSyncCount;

        internal void ApplySettings()
        {
            if (!Plugin.Settings.EnableFrameRateLimit)
            {
                Release();
                return;
            }

            if (!_captured)
            {
                _originalTargetFrameRate = Application.targetFrameRate;
                _originalVSyncCount = QualitySettings.vSyncCount;
                _captured = true;
            }

            Application.targetFrameRate = Plugin.Settings.TargetFrameRate;
            QualitySettings.vSyncCount = Plugin.Settings.DisableVSync ? 0 : _originalVSyncCount;
        }

        internal void Release()
        {
            if (!_captured) return;
            Application.targetFrameRate = _originalTargetFrameRate;
            QualitySettings.vSyncCount = _originalVSyncCount;
            _captured = false;
        }
    }
}
