using UnityModManagerNet;

namespace TestMod
{
    public sealed class TestModSettings : UnityModManager.ModSettings
    {
        public bool EnableAutoKill;
        public string Language;

        public TestModSettings()
        {
            EnableAutoKill = true;
            Language = "system";
        }
    }
}
