using System;

namespace BroforceOnlineDiagnostics
{
    internal sealed class FrpDirectRoomInfo : RoomInfo
    {
        private const int WorkshopPhaseTokenIndex = 21;
        private const int WorkshopReadyTokenIndex = 22;
        private const int WorkshopIdTokenIndex = 23;
        private const int WorkshopSceneTokenIndex = 24;
        private const int WorkshopCampaignTokenIndex = 25;
        private readonly FrpDirectLayer _layer;

        internal string WorkshopPhase { get; set; }
        internal bool WorkshopReady { get; set; }
        internal string WorkshopId { get; set; }
        internal string WorkshopScene { get; set; }
        internal string WorkshopCampaign { get; set; }

        internal FrpDirectRoomInfo(FrpDirectLayer layer)
        {
            _layer = layer;
            WorkshopPhase = string.Empty;
            WorkshopId = string.Empty;
            WorkshopScene = string.Empty;
            WorkshopCampaign = string.Empty;
        }

        internal FrpDirectRoomInfo(FrpDirectLayer layer, string encodedRoom)
        {
            _layer = layer;
            WorkshopPhase = string.Empty;
            WorkshopId = string.Empty;
            WorkshopScene = string.Empty;
            WorkshopCampaign = string.Empty;
            ApplyEncodedRoom(encodedRoom);
        }

        internal void ApplyEncodedRoom(string encodedRoom)
        {
            var tokens = DecodeGameInfo(encodedRoom ?? string.Empty);
            Password = string.Empty;
            WorkshopPhase = tokens != null && tokens.Length > WorkshopPhaseTokenIndex
                ? NormalizeWorkshopPhase(tokens[WorkshopPhaseTokenIndex])
                : string.Empty;
            WorkshopReady = tokens != null && tokens.Length > WorkshopReadyTokenIndex &&
                            string.Equals(tokens[WorkshopReadyTokenIndex], "1", StringComparison.Ordinal);
            WorkshopId = tokens != null && tokens.Length > WorkshopIdTokenIndex
                ? DecodeMetadata(tokens[WorkshopIdTokenIndex])
                : string.Empty;
            WorkshopScene = tokens != null && tokens.Length > WorkshopSceneTokenIndex
                ? DecodeMetadata(tokens[WorkshopSceneTokenIndex])
                : string.Empty;
            WorkshopCampaign = tokens != null && tokens.Length > WorkshopCampaignTokenIndex
                ? DecodeMetadata(tokens[WorkshopCampaignTokenIndex])
                : string.Empty;
        }

        internal string EncodeForPeer()
        {
            var originalPassword = Password;
            try
            {
                // FRP transport authentication replaces Broforce's advertised lobby password.
                Password = string.Empty;
                return EncodeGameInfo() + DELIMITER + NormalizeWorkshopPhase(WorkshopPhase) +
                       DELIMITER + (WorkshopReady ? "1" : "0") +
                       DELIMITER + EncodeMetadata(WorkshopId) +
                       DELIMITER + EncodeMetadata(WorkshopScene) +
                       DELIMITER + EncodeMetadata(WorkshopCampaign);
            }
            finally
            {
                Password = originalPassword;
            }
        }

        public override void PullUpdatedInfo()
        {
            // Room state is pushed by FrpDirectTransport.
        }

        public override void PushUpdatedInfo(bool refreshBaseInfo, bool unused)
        {
            if (_layer == null || !_layer.IsHost)
            {
                return;
            }

            base.PushUpdatedInfo(refreshBaseInfo, unused);
            _layer.PublishRoomState(this);
        }

        protected override int GetRoomMemberCount()
        {
            var localPlayers = base.GetRoomMemberCount();
            return global::System.Math.Max(
                localPlayers,
                _layer == null ? 1 : _layer.RoomMemberCount);
        }

        private static string NormalizeWorkshopPhase(string value)
        {
            value = (value ?? string.Empty).Trim().ToLowerInvariant();
            return value == "idle" || value == "loading" || value == "ready"
                ? value
                : string.Empty;
        }

        private static string EncodeMetadata(string value)
        {
            return Uri.EscapeDataString((value ?? string.Empty).Trim());
        }

        private static string DecodeMetadata(string value)
        {
            try
            {
                return Uri.UnescapeDataString(value ?? string.Empty).Trim();
            }
            catch
            {
                return string.Empty;
            }
        }
    }
}
