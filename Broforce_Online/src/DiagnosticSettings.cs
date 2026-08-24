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
        public bool EnableFrpDirectPrototype;
        public bool EnableFrpDirectGameLayer;
        public string FrpDirectRole;
        public int FrpDirectLocalPort;
        public string FrpDirectServerEndpoint;
        // Retained for migration from settings version 3. The UI no longer exposes these fields.
        public string FrpDirectServerAddress;
        public int FrpDirectServerPort;
        public string FrpDirectRoomPassword;
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
            EnableFrpDirectPrototype = false;
            EnableFrpDirectGameLayer = false;
            FrpDirectRole = "host";
            FrpDirectLocalPort = 27045;
            FrpDirectServerEndpoint = string.Empty;
            FrpDirectServerAddress = string.Empty;
            FrpDirectServerPort = 27045;
            FrpDirectRoomPassword = string.Empty;
            DiagnosticSettingsVersion = 0;
        }
    }
}
