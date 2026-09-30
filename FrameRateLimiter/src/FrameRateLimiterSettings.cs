using UnityModManagerNet;

namespace FrameRateLimiter
{
    public sealed class FrameRateLimiterSettings : UnityModManager.ModSettings
    {
        public const int DefaultFrameRate = 60;

        public int TargetFrameRate = DefaultFrameRate;
        public bool DisableVSync = true;
        public bool UseChinese = true;

        public void Normalize()
        {
            if (TargetFrameRate == 0)
            {
                TargetFrameRate = DefaultFrameRate;
            }
            else if (TargetFrameRate < -1)
            {
                TargetFrameRate = 1;
            }
            else if (TargetFrameRate > 1000)
            {
                TargetFrameRate = 1000;
            }
        }
    }
}
