using System;
using System.Collections.Generic;

namespace BroforceOnlineDiagnostics
{
    internal sealed class FrpDirectLayer : ConnectionLayer, IDisposable
    {
        private readonly FrpDirectTransport _transport;
        private readonly IDWrapper _localId;
        private IDWrapper _remoteId;
        private bool _remoteJoined;
        private bool _roomReady;
        private bool _roomQueryPending;
        private bool _disposed;

        internal FrpDirectLayer(FrpDirectTransport transport)
        {
            if (transport == null)
            {
                throw new ArgumentNullException("transport");
            }

            _transport = transport;
            _localId = new IDWrapper(FormatMachineId(transport.LocalMachineId), PID.NoID, true);
            Subscribe();
            DiagnosticLog.Info(
                "FRP_DIRECT game RPC uses FRP transport; custom Workshop content uses Steam loading.");
        }

        public override bool IsOffline { get { return Room == null; } }
        public override bool IsHost { get { return _transport.IsHost; } }
        public override bool IsOnlineRoomReady { get { return _roomReady; } }
        public override bool ReadyToFindLobby
        {
            get { return !_transport.IsHost && _transport.IsHandshakeComplete; }
        }
        public override bool CanInviteFriends { get { return false; } }
        public override bool CanEditLobbyName { get { return true; } }
        // Broforce uses the layer type to choose the custom-content provider.
        // FRP replaces Steam networking, but Workshop campaigns still come from Steam.
        public override LayerType ConnectionType { get { return LayerType.Steam; } }
        public override string Host { get { return IsHost ? _localId.UnderlyingID : RemoteUnderlyingId; } }
        public override string HostName
        {
            get
            {
                if (IsHost)
                {
                    return Connect.PlayerName;
                }

                var server = GetPIDWrapper(PID.ServerID);
                return server == null ? "FRP Direct Host" : server.PlayerName;
            }
        }
        public override IDWrapper MyNetworkLayerID { get { return _localId; } }

        internal int RoomMemberCount { get { return _remoteJoined ? 2 : 1; } }

        public override void CreateMatch()
        {
            if (!IsHost)
            {
                DiagnosticLog.Warning("FRP_DIRECT client cannot create a room while configured as Client.");
                OnMatchingError();
                return;
            }

            base.CreateMatch();
            ResetRoomState();
            var room = new FrpDirectRoomInfo(this);
            room.HostName = Connect.PlayerName;
            room.GameName = Connect.GameName;
            OnCreatedLobby(room, _localId);
            _roomReady = true;
            DiagnosticLog.Info("FRP_DIRECT Broforce room created; waiting for the authenticated client.");
        }

        public override void FindLobby()
        {
            LobbyList.Clear();
            queryCancelled = false;
            matchQueryHandled = false;
            DebugLobby.state = MatchMakingState.FindingMatch;
            _roomQueryPending = !IsHost;
            if (IsHost || !_transport.RequestRoomState())
            {
                matchQueryHandled = true;
                OnReceiveLobbyListing(LobbyList);
                if (!_transport.IsHandshakeComplete)
                {
                    DiagnosticLog.Warning("FRP_DIRECT room query is waiting for the transport handshake.");
                }
            }
        }

        public override void JoinLobby(RoomInfo room, int controllerId, string password, Action completed)
        {
            var frpRoom = room as FrpDirectRoomInfo;
            if (IsHost || frpRoom == null || !_transport.IsHandshakeComplete)
            {
                OnFailedToJoinGame();
                return;
            }

            SetNewLobbyRoom(frpRoom);
            _roomReady = false;
            connectionState = ConnectionState.Connecting;
            if (!_transport.RequestJoin())
            {
                base.LeaveMatch(controllerId);
                OnFailedToJoinGame();
            }
        }

        public override void LeaveMatch(int controllerId)
        {
            if (Room != null)
            {
                _transport.SendLeaveNotice();
            }

            HarmonyDiagnostics.PrepareFrpDirectRoomExit("local LeaveMatch");
            ResetRoomState();
            base.LeaveMatch(controllerId);
            OnGameDestroyed();
        }

        public override void ShutDown()
        {
            base.ShutDown();
            Dispose();
            FrpDirectNetworkManager.ReleaseLayer(this);
        }

        public override void Update()
        {
            base.Update();
            UpdateOnlinePlayerList();
        }

        protected override string[] GetAllOnlinePlayerNames()
        {
            var names = new List<string>(2);
            var localName = Connect.PlayerName;
            names.Add(string.IsNullOrEmpty(localName) ? "Local Player" : localName);

            if (!_remoteJoined || !_transport.IsHandshakeComplete || _remoteId == null)
            {
                return names.ToArray();
            }

            PID remotePid = null;
            foreach (var pair in PlayerIDPairs)
            {
                if (pair.Value == null || !pair.Value.Connected || !pair.Value.Equals(_remoteId))
                {
                    continue;
                }
                if (remotePid == null || pair.Key.AsByte < remotePid.AsByte)
                {
                    remotePid = pair.Key;
                }
            }

            var remoteName = remotePid == null ? string.Empty : remotePid.PlayerName;
            names.Add(string.IsNullOrEmpty(remoteName) ? "FRP Direct Player" : remoteName);
            return names.ToArray();
        }

        public override void SendData(PID target, byte[] bytes)
        {
            if (!ShouldSendToRemote(target) || !_transport.SendGameData(bytes))
            {
                return;
            }
        }

        public override IDWrapper GetPIDWrapper(PID pid)
        {
            IDWrapper wrapper;
            return PlayerIDPairs.TryGetValue(pid, out wrapper) ? wrapper : null;
        }

        public override bool IsPlayerDisconnected(PID pid)
        {
            var wrapper = GetPIDWrapper(pid);
            return wrapper == null || !wrapper.Connected ||
                   (IsRemote(wrapper) && !_transport.IsHandshakeComplete);
        }

        public override bool IsReadyToRecieveRPCs(PID pid)
        {
            if (pid == PID.MyID)
            {
                return true;
            }
            if (pid == PID.TargetOthers || pid == PID.TargetAll)
            {
                return _remoteJoined && _transport.IsHandshakeComplete;
            }
            if (pid == PID.TargetServer)
            {
                return IsHost || (_remoteJoined && _transport.IsHandshakeComplete);
            }

            var wrapper = GetPIDWrapper(pid);
            return _transport.IsHandshakeComplete && wrapper != null && wrapper.Connected;
        }

        internal void PublishRoomState(FrpDirectRoomInfo room)
        {
            if (IsHost && room != null && Room == room)
            {
                _transport.SendRoomState(true, room.EncodeForPeer());
            }
        }

        internal string GetRoomMetadata(string key)
        {
            var room = Room as FrpDirectRoomInfo;
            if (room == null)
            {
                return string.Empty;
            }
            if (string.Equals(key, "GJKen_BroforceOnline_WorkshopReady", StringComparison.Ordinal))
            {
                return room.WorkshopReady ? "1" : "0";
            }
            if (string.Equals(key, "GJKen_BroforceOnline_WorkshopPhase", StringComparison.Ordinal))
            {
                return room.WorkshopPhase ?? string.Empty;
            }
            return string.Empty;
        }

        internal bool SetRoomMetadata(string key, string value)
        {
            var room = Room as FrpDirectRoomInfo;
            if (!IsHost || room == null)
            {
                return false;
            }
            if (string.Equals(key, "GJKen_BroforceOnline_WorkshopReady", StringComparison.Ordinal))
            {
                room.WorkshopReady = string.Equals(value, "1", StringComparison.Ordinal);
            }
            else if (string.Equals(key, "GJKen_BroforceOnline_WorkshopPhase", StringComparison.Ordinal))
            {
                room.WorkshopPhase = value ?? string.Empty;
            }
            else
            {
                return false;
            }

            PublishRoomState(room);
            return true;
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            Unsubscribe();
        }

        private void OnHandshakeCompleted()
        {
            EnsureRemoteId();
            if (!IsHost && _roomQueryPending)
            {
                _transport.RequestRoomState();
            }
            if (IsHost && Room != null)
            {
                var room = Room as FrpDirectRoomInfo;
                if (room != null)
                {
                    room.PushUpdatedInfo(true, false);
                }
            }
        }

        private void OnRoomQueryReceived()
        {
            if (!IsHost || Room == null)
            {
                _transport.SendRoomState(false, string.Empty);
                return;
            }

            var room = Room as FrpDirectRoomInfo;
            if (room == null)
            {
                _transport.SendRoomState(false, string.Empty);
                return;
            }

            room.PushUpdatedInfo(true, false);
        }

        private void OnRoomStateReceived(bool hasRoom, string encodedRoom)
        {
            var joinedRoom = Room as FrpDirectRoomInfo;
            if (_remoteJoined && joinedRoom != null)
            {
                if (hasRoom)
                {
                    joinedRoom.ApplyEncodedRoom(encodedRoom);
                }
                return;
            }

            LobbyList.Clear();
            if (hasRoom)
            {
                var room = new FrpDirectRoomInfo(this, encodedRoom);
                if (!room.invalidInfo)
                {
                    LobbyList.Add(room);
                }
            }
            matchQueryHandled = true;
            _roomQueryPending = false;
            if (!queryCancelled)
            {
                OnReceiveLobbyListing(LobbyList);
            }
        }

        private void OnJoinRequestReceived()
        {
            var room = Room as FrpDirectRoomInfo;
            if (!IsHost || room == null)
            {
                _transport.RejectJoin("room_not_available");
                return;
            }
            if (_remoteJoined)
            {
                _transport.AcceptJoin(room.EncodeForPeer());
                return;
            }

            EnsureRemoteId();
            if (_remoteId == null || !_transport.AcceptJoin(room.EncodeForPeer()))
            {
                _transport.RejectJoin("join_failed");
                return;
            }

            _remoteJoined = true;
            PlayerHasJoinedMatch(_remoteId);
            room.PushUpdatedInfo(true, false);
            DiagnosticLog.Info("FRP_DIRECT authenticated client joined the Broforce room; PID assignment started.");
        }

        private void OnJoinAcceptedReceived(string encodedRoom)
        {
            if (IsHost)
            {
                return;
            }
            if (_remoteJoined && _roomReady)
            {
                return;
            }

            var room = new FrpDirectRoomInfo(this, encodedRoom);
            EnsureRemoteId();
            if (room.invalidInfo || _remoteId == null)
            {
                ResetRoomState();
                base.LeaveMatch(-1);
                OnFailedToJoinGame();
                return;
            }

            _remoteJoined = true;
            SetNewLobbyRoom(room);
            OnJoinedLobby(room, _localId);
            _roomReady = true;
            DiagnosticLog.Info("FRP_DIRECT joined the Broforce room; waiting for host PID assignment.");
        }

        private void OnJoinRejectedReceived(string reason)
        {
            ResetRoomState();
            base.LeaveMatch(-1);
            DiagnosticLog.Warning("FRP_DIRECT room join rejected; reason=" + reason + ".");
            OnFailedToJoinGame();
        }

        private void OnGameDataReceived(byte[] bytes)
        {
            if (Room == null || !_remoteJoined)
            {
                return;
            }

            RecieveBytes(bytes);
        }

        private void OnLeaveNoticeReceived()
        {
            if (IsHost)
            {
                RemoveRemotePlayer();
                var room = Room as FrpDirectRoomInfo;
                if (room != null)
                {
                    room.PushUpdatedInfo(true, false);
                }
                return;
            }

            var hadRoom = Room != null;
            if (hadRoom)
            {
                HarmonyDiagnostics.PrepareFrpDirectRoomExit("host leave notice");
                Connect.OnConnectionDown();
            }
            ResetRoomState();
            base.LeaveMatch(-1);
            if (hadRoom)
            {
                HarmonyDiagnostics.CompleteFrpDirectRemoteRoomExit("host leave notice");
            }
        }

        private void OnRemoteDisconnected()
        {
            if (IsHost)
            {
                RemoveRemotePlayer();
                var room = Room as FrpDirectRoomInfo;
                if (room != null)
                {
                    room.PushUpdatedInfo(true, false);
                }
                return;
            }

            if (Room != null)
            {
                HarmonyDiagnostics.PrepareFrpDirectRoomExit("host transport disconnected");
                Connect.OnConnectionDown();
                ResetRoomState();
                base.LeaveMatch(-1);
                HarmonyDiagnostics.CompleteFrpDirectRemoteRoomExit("host transport disconnected");
            }
        }

        private void OnConfigurationChanging()
        {
            if (Room == null)
            {
                return;
            }

            _transport.SendLeaveNotice();
            HarmonyDiagnostics.PrepareFrpDirectRoomExit("FRP configuration changed");
            ResetRoomState();
            base.LeaveMatch(-1);
            OnGameDestroyed();
            HarmonyDiagnostics.CompleteFrpDirectRemoteRoomExit("FRP configuration changed");
        }

        private bool ShouldSendToRemote(PID target)
        {
            if (Room == null || !_remoteJoined || !_transport.IsHandshakeComplete ||
                target == PID.NoID || target == PID.MatchMakingServer || target == PID.MyID)
            {
                return false;
            }
            if (target == PID.TargetOthers || target == PID.TargetAll)
            {
                return true;
            }
            if (target == PID.TargetServer)
            {
                return !IsHost;
            }

            var wrapper = GetPIDWrapper(target);
            return wrapper != null && IsRemote(wrapper);
        }

        private void EnsureRemoteId()
        {
            var underlyingId = RemoteUnderlyingId;
            if (string.IsNullOrEmpty(underlyingId))
            {
                _remoteId = null;
                return;
            }
            if (_remoteId == null ||
                !string.Equals(_remoteId.UnderlyingID, underlyingId, StringComparison.Ordinal))
            {
                _remoteId = new IDWrapper(underlyingId, PID.NoID, true);
            }
            else
            {
                _remoteId.ProcessConnected();
            }
        }

        private void RemoveRemotePlayer()
        {
            if (_remoteId != null)
            {
                _remoteId.ProcessDisconnected();
                foreach (var pair in PlayerIDPairs)
                {
                    if (pair.Value != null && pair.Value.Equals(_remoteId))
                    {
                        pair.Value.ProcessDisconnected();
                    }
                }
                Connect.ClearDCPlayers();
            }
            _remoteJoined = false;
            _roomReady = IsHost && Room != null;
        }

        private void ResetRoomState()
        {
            _remoteJoined = false;
            _roomReady = false;
            if (_remoteId != null)
            {
                if (_transport.IsHandshakeComplete)
                {
                    _remoteId.ProcessConnected();
                }
                else
                {
                    _remoteId.ProcessDisconnected();
                }
            }
            _roomQueryPending = false;
            PlayerIDPairs.Clear();
            Reset();
            PID.Reset();
        }

        private bool IsRemote(IDWrapper wrapper)
        {
            return _remoteId != null && wrapper.Equals(_remoteId);
        }

        private string RemoteUnderlyingId
        {
            get
            {
                return string.IsNullOrEmpty(_transport.RemoteMachineId)
                    ? string.Empty
                    : FormatMachineId(_transport.RemoteMachineId);
            }
        }

        private static string FormatMachineId(string value)
        {
            return string.IsNullOrEmpty(value) ? string.Empty : "frp-direct:" + value;
        }

        private void Subscribe()
        {
            _transport.ConfigurationChanging += OnConfigurationChanging;
            _transport.HandshakeCompleted += OnHandshakeCompleted;
            _transport.RemoteDisconnected += OnRemoteDisconnected;
            _transport.RoomQueryReceived += OnRoomQueryReceived;
            _transport.RoomStateReceived += OnRoomStateReceived;
            _transport.JoinRequestReceived += OnJoinRequestReceived;
            _transport.JoinAcceptedReceived += OnJoinAcceptedReceived;
            _transport.JoinRejectedReceived += OnJoinRejectedReceived;
            _transport.GameDataReceived += OnGameDataReceived;
            _transport.LeaveNoticeReceived += OnLeaveNoticeReceived;
        }

        private void Unsubscribe()
        {
            _transport.ConfigurationChanging -= OnConfigurationChanging;
            _transport.HandshakeCompleted -= OnHandshakeCompleted;
            _transport.RemoteDisconnected -= OnRemoteDisconnected;
            _transport.RoomQueryReceived -= OnRoomQueryReceived;
            _transport.RoomStateReceived -= OnRoomStateReceived;
            _transport.JoinRequestReceived -= OnJoinRequestReceived;
            _transport.JoinAcceptedReceived -= OnJoinAcceptedReceived;
            _transport.JoinRejectedReceived -= OnJoinRejectedReceived;
            _transport.GameDataReceived -= OnGameDataReceived;
            _transport.LeaveNoticeReceived -= OnLeaveNoticeReceived;
        }
    }
}
