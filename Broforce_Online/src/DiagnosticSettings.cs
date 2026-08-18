namespace BroforceOnlineDiagnostics
{
    public sealed class DiagnosticSettings : UnityModManagerNet.UnityModManager.ModSettings
    {
        public bool EnableOnlineWorkshopInjection;
        public string WorkshopId;
        public string WorkshopCampaignName;
        public string WorkshopSceneName;

        public DiagnosticSettings()
        {
            EnableOnlineWorkshopInjection = false;
            WorkshopId = "456121589";
            WorkshopCampaignName = "the sweet taste of freedom 3";
            WorkshopSceneName = "Test Evan2";
        }
    }
}
