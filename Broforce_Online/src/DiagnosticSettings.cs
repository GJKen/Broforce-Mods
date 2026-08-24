namespace BroforceOnlineDiagnostics
{
    public sealed class DiagnosticSettings : UnityModManagerNet.UnityModManager.ModSettings
    {
        public const string DefaultWorkshopSceneName = "Test Evan2";

        public bool EnableOnlineWorkshopInjection;
        public bool DisableOnlineAfkSpectatorMode;
        public string WorkshopId;
        public string WorkshopCampaignName;
        public string WorkshopSceneName;
        public string DiagnosticSessionId;
        public string DiagnosticRole;
        public int DiagnosticSettingsVersion;

        public DiagnosticSettings()
        {
            EnableOnlineWorkshopInjection = false;
            DisableOnlineAfkSpectatorMode = false;
            WorkshopId = string.Empty;
            WorkshopCampaignName = string.Empty;
            WorkshopSceneName = DefaultWorkshopSceneName;
            DiagnosticSessionId = string.Empty;
            DiagnosticRole = string.Empty;
            DiagnosticSettingsVersion = 0;
        }
    }
}
