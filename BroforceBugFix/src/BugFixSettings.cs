using UnityModManagerNet;

namespace BroforceBugFix
{
    public sealed class BugFixSettings : UnityModManager.ModSettings
    {
        public bool EnableAllFixes = true;
        public bool EnableDoodadCrateReentryFix = true;
    }
}
