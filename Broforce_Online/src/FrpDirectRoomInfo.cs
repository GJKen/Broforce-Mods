using System;

namespace BroforceOnlineDiagnostics
{
    internal sealed class FrpDirectRoomInfo : RoomInfo
    {
        private const int WorkshopPhaseTokenIndex = 21;
        private const int WorkshopReadyTokenIndex = 22;
        private readonly FrpDirectLayer _layer;

        internal string WorkshopPhase { get; set; }
        internal bool WorkshopReady { get; set; }

        internal FrpDirectRoomInfo(FrpDirectLayer layer)
        {
            _layer = layer;
            WorkshopPhase = string.Empty;
        }

        internal FrpDirectRoomInfo(FrpDirectLayer layer, string encodedRoom)
        {
            _layer = layer;
            WorkshopPhase = string.Empty;
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
        }

        internal string EncodeForPeer()
        {
            var originalPassword = Password;
            try
            {
                // FRP transport authentication replaces Broforce's advertised lobby password.
                Password = string.Empty;
                return EncodeGameInfo() + DELIMITER + NormalizeWorkshopPhase(WorkshopPhase) +
                       DELIMITER + (WorkshopReady ? "1" : "0");
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
    }
}
