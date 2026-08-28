using System;
using System.Collections.Generic;
using UnityEngine;

namespace BroforceCustomMapMultiplayer
{
    internal sealed class FrpDirectLayer : ConnectionLayer, IDisposable
    {
        internal const string RoomFullNotice =
            "房主设置的房间人数已达上限，暂时无法加入。";

        private const string MachineIdPrefix = "frp-direct:";

        private readonly FrpDirectTransport _transport;
        private readonly IDWrapper _localId;
        private readonly Dictionary<string, IDWrapper> _remoteIds =
            new Dictionary<string, IDWrapper>(StringComparer.Ordinal);
        private readonly HashSet<string> _joinedRemoteMachineIds =
            new HashSet<string>(StringComparer.Ordinal);
        private readonly HashSet<string> _departedRemoteMachineIds =
            new HashSet<string>(StringComparer.Ordinal);
        private bool _clientJoined;
        private bool _roomReady;
        private bool _roomQueryPending;
        private bool _disposed;
        private float _nextOnlinePlayerListUpdateAt;

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
            get { return !IsHost && _transport.IsHandshakeComplete; }
        }
        public override bool CanInviteFriends { get { return false; } }
        public override bool CanEditLobbyName { get { return true; } }
        public override LayerType ConnectionType { get { return LayerType.Steam; } }
        public override string Host
        {
            get
            {
                return IsHost
                    ? _localId.UnderlyingID
                    : FormatMachineId(_transport.RemoteMachineId);
            }
        }
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

        internal int RoomMemberCount
        {
            get
            {
                if (IsHost)
                {
                    return 1 + _joinedRemoteMachineIds.Count;
                }
                return _clientJoined ? global::System.Math.Max(2, PlayerIDPairs.Count) : 1;
            }
        }

        private int PlayerLimit
        {
            get { return NormalizePlayerLimit(_transport.PlayerLimit); }
        }

        private int RemoteMemberLimit
        {
            get { return PlayerLimit - 1; }
        }

        public override void CreateMatch()
        {
            if (!IsHost)
            {
                DiagnosticLog.Warning(
                    "FRP_DIRECT client cannot create a room while configured as Client.");
                OnMatchingError();
                return;
            }

            base.CreateMatch();
            ResetRoomState();
            Connect.PlayerLimit = PlayerLimit;
            var room = new FrpDirectRoomInfo(this);
            room.HostName = Connect.PlayerName;
            room.GameName = Connect.GameName;
            OnCreatedLobby(room, _localId);
            _roomReady = true;
            DiagnosticLog.Info(
                "FRP_DIRECT Broforce room created; playerLimit=" + PlayerLimit +
                "; remoteMemberLimit=" + RemoteMemberLimit + ".");
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
                    DiagnosticLog.Warning(
                        "FRP_DIRECT room query is waiting for the transport handshake.");
                }
            }
        }

        public override void JoinLobby(
            RoomInfo room,
            int controllerId,
            string password,
            Action completed)
        {
            Plugin.ClearFrpDirectNotice();
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
            Plugin.ClearFrpDirectNotice();
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
            var now = Time.unscaledTime;
            if (now < _nextOnlinePlayerListUpdateAt)
            {
                return;
            }
            _nextOnlinePlayerListUpdateAt = now + OnlinePlayerListFormatter.RefreshSeconds;
            UpdateOnlinePlayerList();
        }

        protected override string[] GetAllOnlinePlayerNames()
        {
            var names = new List<string>();
            var localName = Connect.PlayerName;
            localName = string.IsNullOrEmpty(localName) ? "Local Player" : localName;
            names.Add(IsHost
                ? OnlinePlayerListFormatter.FormatHost(localName)
                : FormatLatencyPlayerName(localName, _transport.LocalMachineId));

            var remotePids = new List<PID>();
            foreach (var pair in PlayerIDPairs)
            {
                if (pair.Value == null || pair.Key == PID.MyID ||
                    !IsWrapperAvailable(pair.Value))
                {
                    continue;
                }
                remotePids.Add(pair.Key);
            }
            remotePids.Sort(delegate(PID left, PID right)
            {
                return left.AsByte.CompareTo(right.AsByte);
            });
            foreach (var remotePid in remotePids)
            {
                var remoteName = remotePid.PlayerName;
                remoteName = string.IsNullOrEmpty(remoteName)
                    ? "FRP Direct Player"
                    : remoteName;
                var machineId = GetMachineId(GetPIDWrapper(remotePid));
                names.Add(!IsHost && string.Equals(
                        machineId,
                        _transport.RemoteMachineId,
                        StringComparison.Ordinal)
                    ? OnlinePlayerListFormatter.FormatHost(remoteName)
                    : FormatLatencyPlayerName(remoteName, machineId));
            }
            return names.ToArray();
        }

        private string FormatLatencyPlayerName(string playerName, string machineId)
        {
            var latencyMilliseconds = _transport.GetRoundTripTimeMilliseconds(machineId);
            return OnlinePlayerListFormatter.FormatLatency(playerName, latencyMilliseconds);
        }

        public override void SendData(PID target, byte[] bytes)
        {
            if (Room == null || bytes == null || bytes.Length == 0 ||
                target == PID.NoID || target == PID.MatchMakingServer || target == PID.MyID)
            {
                return;
            }

            if (target == PID.TargetAll || target == PID.TargetOthers)
            {
                if (IsHost)
                {
                    BroadcastToJoinedRemotes(bytes, null);
                }
                else
                {
                    _transport.SendGameData("*", bytes, null);
                }
                return;
            }
            if (target == PID.TargetServer)
            {
                if (!IsHost)
                {
                    _transport.SendGameData(string.Empty, bytes, null);
                }
                return;
            }

            var wrapper = GetPIDWrapper(target);
            var targetMachineId = GetMachineId(wrapper);
            if (string.IsNullOrEmpty(targetMachineId) ||
                string.Equals(targetMachineId, _transport.LocalMachineId, StringComparison.Ordinal))
            {
                return;
            }

            if (!IsHost &&
                string.Equals(targetMachineId, _transport.RemoteMachineId, StringComparison.Ordinal))
            {
                targetMachineId = string.Empty;
            }
            _transport.SendGameData(targetMachineId, bytes, null);
        }

        public override IDWrapper GetPIDWrapper(PID pid)
        {
            IDWrapper wrapper;
            return PlayerIDPairs.TryGetValue(pid, out wrapper) ? wrapper : null;
        }

        public override bool IsPlayerDisconnected(PID pid)
        {
            var wrapper = GetPIDWrapper(pid);
            return wrapper == null || !wrapper.Connected || !IsWrapperAvailable(wrapper);
        }

        public override bool IsReadyToRecieveRPCs(PID pid)
        {
            if (pid == PID.MyID)
            {
                return true;
            }
            if (pid == PID.TargetOthers || pid == PID.TargetAll)
            {
                return Room != null && (IsHost
                    ? _joinedRemoteMachineIds.Count > 0
                    : _clientJoined && _transport.IsHandshakeComplete);
            }
            if (pid == PID.TargetServer)
            {
                return IsHost || (_clientJoined && _transport.IsHandshakeComplete);
            }

            var wrapper = GetPIDWrapper(pid);
            return wrapper != null && wrapper.Connected && IsWrapperAvailable(wrapper);
        }

        protected override void RegisterNewPlayer(string underlyingId, PID allocatedID)
        {
            IDWrapper wrapper;
            if (string.Equals(underlyingId, _localId.UnderlyingID, StringComparison.Ordinal))
            {
                wrapper = _localId;
            }
            else
            {
                var machineId = ParseMachineId(underlyingId);
                if (string.IsNullOrEmpty(machineId))
                {
                    base.RegisterNewPlayer(underlyingId, allocatedID);
                    return;
                }

                wrapper = GetOrCreateRemoteId(machineId);
                wrapper.ProcessConnected();
                _departedRemoteMachineIds.Remove(machineId);
            }

            if (_localId.Equals(wrapper) && !Connect.IsOffline)
            {
                PID.SetMyID(allocatedID.AsByte);
            }
            SetPIDPair(allocatedID, wrapper);
        }

        internal void PublishRoomState(FrpDirectRoomInfo room)
        {
            if (IsHost && room != null && Room == room)
            {
                _transport.SendRoomState(true, room.EncodeForPeer(), null);
            }
        }

        internal string GetRoomMetadata(string key)
        {
            var room = Room as FrpDirectRoomInfo;
            if (room == null)
            {
                return string.Empty;
            }
            if (string.Equals(
                key,
                "GJKen_BroforceOnline_WorkshopReady",
                StringComparison.Ordinal))
            {
                return room.WorkshopReady ? "1" : "0";
            }
            if (string.Equals(
                key,
                "GJKen_BroforceOnline_WorkshopPhase",
                StringComparison.Ordinal))
            {
                return room.WorkshopPhase ?? string.Empty;
            }
            if (string.Equals(key, "GJKen_BroforceOnline_WorkshopId", StringComparison.Ordinal))
            {
                return room.WorkshopId ?? string.Empty;
            }
            if (string.Equals(key, "GJKen_BroforceOnline_WorkshopScene", StringComparison.Ordinal))
            {
                return room.WorkshopScene ?? string.Empty;
            }
            if (string.Equals(key, "GJKen_BroforceOnline_WorkshopCampaign", StringComparison.Ordinal))
            {
                return room.WorkshopCampaign ?? string.Empty;
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
            if (string.Equals(
                key,
                "GJKen_BroforceOnline_WorkshopReady",
                StringComparison.Ordinal))
            {
                room.WorkshopReady = string.Equals(value, "1", StringComparison.Ordinal);
            }
            else if (string.Equals(
                key,
                "GJKen_BroforceOnline_WorkshopPhase",
                StringComparison.Ordinal))
            {
                room.WorkshopPhase = value ?? string.Empty;
            }
            else if (string.Equals(key, "GJKen_BroforceOnline_WorkshopId", StringComparison.Ordinal))
            {
                room.WorkshopId = value ?? string.Empty;
            }
            else if (string.Equals(key, "GJKen_BroforceOnline_WorkshopScene", StringComparison.Ordinal))
            {
                room.WorkshopScene = value ?? string.Empty;
            }
            else if (string.Equals(key, "GJKen_BroforceOnline_WorkshopCampaign", StringComparison.Ordinal))
            {
                room.WorkshopCampaign = value ?? string.Empty;
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

        private void OnHandshakeCompleted(string machineId)
        {
            GetOrCreateRemoteId(machineId).ProcessConnected();
            _departedRemoteMachineIds.Remove(machineId);
            if (!IsHost && _roomQueryPending)
            {
                _transport.RequestRoomState();
            }
            if (IsHost && Room != null)
            {
                var room = Room as FrpDirectRoomInfo;
                if (room != null)
                {
                    _transport.SendRoomState(true, room.EncodeForPeer(), machineId);
                }
            }
        }

        private void OnRoomQueryReceived(string machineId)
        {
            var room = Room as FrpDirectRoomInfo;
            if (!IsHost || room == null)
            {
                _transport.SendRoomState(false, string.Empty, machineId);
                return;
            }
            _transport.SendRoomState(true, room.EncodeForPeer(), machineId);
        }

        private void OnRoomStateReceived(
            string machineId,
            bool hasRoom,
            string encodedRoom)
        {
            var joinedRoom = Room as FrpDirectRoomInfo;
            if (_clientJoined && joinedRoom != null)
            {
                if (hasRoom)
                {
                    joinedRoom.ApplyEncodedRoom(encodedRoom);
                    if (!joinedRoom.invalidInfo)
                    {
                        Connect.PlayerLimit = NormalizePlayerLimit(joinedRoom.Capacity);
                    }
                }
                return;
            }

            if (!_roomQueryPending)
            {
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

        private void OnJoinRequestReceived(string machineId)
        {
            var room = Room as FrpDirectRoomInfo;
            if (!IsHost || room == null)
            {
                _transport.RejectJoin("room_not_available", machineId);
                return;
            }
            if (_joinedRemoteMachineIds.Contains(machineId))
            {
                _transport.AcceptJoin(room.EncodeForPeer(), machineId);
                return;
            }
            if (_joinedRemoteMachineIds.Count >= RemoteMemberLimit)
            {
                _transport.RejectJoin("room_full", machineId);
                return;
            }

            var remoteId = GetOrCreateRemoteId(machineId);
            remoteId.ProcessConnected();
            if (!_transport.AcceptJoin(room.EncodeForPeer(), machineId))
            {
                _transport.RejectJoin("join_failed", machineId);
                return;
            }

            _joinedRemoteMachineIds.Add(machineId);
            _departedRemoteMachineIds.Remove(machineId);
            PlayerHasJoinedMatch(remoteId);
            room.PushUpdatedInfo(true, false);
            DiagnosticLog.Info(
                "FRP_DIRECT authenticated client joined the Broforce room; " +
                "remoteMembers=" + _joinedRemoteMachineIds.Count + ".");
        }

        private void OnJoinAcceptedReceived(string machineId, string encodedRoom)
        {
            if (IsHost || (_clientJoined && _roomReady))
            {
                return;
            }

            var room = new FrpDirectRoomInfo(this, encodedRoom);
            var hostId = GetOrCreateRemoteId(machineId);
            if (room.invalidInfo || hostId == null)
            {
                ResetRoomState();
                base.LeaveMatch(-1);
                OnFailedToJoinGame();
                return;
            }

            hostId.ProcessConnected();
            Plugin.ClearFrpDirectNotice();
            _clientJoined = true;
            Connect.PlayerLimit = NormalizePlayerLimit(room.Capacity);
            SetNewLobbyRoom(room);
            OnJoinedLobby(room, _localId);
            _roomReady = true;
            DiagnosticLog.Info(
                "FRP_DIRECT joined the Broforce room; waiting for host PID assignment.");
        }

        private void OnJoinRejectedReceived(string machineId, string reason)
        {
            var roomFull = string.Equals(reason, "room_full", StringComparison.Ordinal);
            ResetRoomState();
            base.LeaveMatch(-1);
            DiagnosticLog.Warning("FRP_DIRECT room join rejected; reason=" + reason + ".");
            OnFailedToJoinGame();
            if (roomFull)
            {
                Plugin.ShowFrpDirectNotice(RoomFullNotice);
            }
        }

        private void OnGameDataReceived(
            string sourceMachineId,
            string route,
            byte[] bytes)
        {
            if (Room == null)
            {
                return;
            }

            if (IsHost)
            {
                if (!_joinedRemoteMachineIds.Contains(sourceMachineId))
                {
                    return;
                }
                if (string.IsNullOrEmpty(route))
                {
                    RecieveBytes(bytes);
                    return;
                }
                if (string.Equals(route, "*", StringComparison.Ordinal))
                {
                    RecieveBytes(bytes);
                    BroadcastToJoinedRemotes(bytes, sourceMachineId);
                    return;
                }
                if (_joinedRemoteMachineIds.Contains(route))
                {
                    _transport.SendGameData(route, bytes, sourceMachineId);
                }
                return;
            }

            if (_clientJoined &&
                string.Equals(sourceMachineId, _transport.RemoteMachineId, StringComparison.Ordinal))
            {
                RecieveBytes(bytes);
            }
        }

        private void OnLeaveNoticeReceived(string machineId)
        {
            if (IsHost)
            {
                RemoveRemotePlayer(machineId, true);
                return;
            }

            LeaveRemoteRoom("host leave notice");
        }

        private void OnRemoteDisconnected(string machineId)
        {
            if (IsHost)
            {
                RemoveRemotePlayer(machineId, true);
                return;
            }

            if (Room != null)
            {
                LeaveRemoteRoom("host transport disconnected");
            }
        }

        private void OnMemberLeftReceived(string machineId)
        {
            if (IsHost || !_clientJoined)
            {
                return;
            }
            MarkRemotePlayerDisconnected(machineId);
        }

        private void OnConfigurationChanging()
        {
            Plugin.ClearFrpDirectNotice();
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

        private void OnPlayerLimitChanged(int playerLimit)
        {
            if (!IsHost)
            {
                return;
            }

            Connect.PlayerLimit = NormalizePlayerLimit(playerLimit);
            var room = Room as FrpDirectRoomInfo;
            if (room != null)
            {
                room.PushUpdatedInfo(true, false);
            }
            DiagnosticLog.Info(
                "FRP_DIRECT active room player limit updated; playerLimit=" +
                Connect.PlayerLimit +
                "; currentMembers=" + RoomMemberCount +
                "; existingMembersRetained=true.");
        }

        private void LeaveRemoteRoom(string reason)
        {
            var hadRoom = Room != null;
            if (hadRoom)
            {
                HarmonyDiagnostics.PrepareFrpDirectRoomExit(reason);
                Connect.OnConnectionDown();
            }
            ResetRoomState();
            base.LeaveMatch(-1);
            if (hadRoom)
            {
                HarmonyDiagnostics.CompleteFrpDirectRemoteRoomExit(reason);
            }
        }

        private void RemoveRemotePlayer(string machineId, bool notifyOthers)
        {
            if (string.IsNullOrEmpty(machineId))
            {
                return;
            }

            var wasJoined = _joinedRemoteMachineIds.Remove(machineId);
            if (!wasJoined)
            {
                _remoteIds.Remove(machineId);
                _departedRemoteMachineIds.Remove(machineId);
                return;
            }

            MarkRemotePlayerDisconnected(machineId);
            if (notifyOthers)
            {
                _transport.SendMemberLeft(machineId, machineId);
            }
            var room = Room as FrpDirectRoomInfo;
            if (room != null)
            {
                room.PushUpdatedInfo(true, false);
            }
            _roomReady = Room != null;
        }

        private void BroadcastToJoinedRemotes(byte[] bytes, string excludeMachineId)
        {
            foreach (var machineId in _joinedRemoteMachineIds)
            {
                if (string.Equals(machineId, excludeMachineId, StringComparison.Ordinal))
                {
                    continue;
                }
                _transport.SendGameData(machineId, bytes, null);
            }
        }

        private void MarkRemotePlayerDisconnected(string machineId)
        {
            _departedRemoteMachineIds.Add(machineId);
            IDWrapper remoteId;
            if (_remoteIds.TryGetValue(machineId, out remoteId))
            {
                remoteId.ProcessDisconnected();
            }
            foreach (var pair in PlayerIDPairs)
            {
                if (string.Equals(
                    GetMachineId(pair.Value),
                    machineId,
                    StringComparison.Ordinal))
                {
                    pair.Value.ProcessDisconnected();
                }
            }
            Connect.ClearDCPlayers();
            _remoteIds.Remove(machineId);
        }

        private void ResetRoomState()
        {
            _nextOnlinePlayerListUpdateAt = 0f;
            _clientJoined = false;
            _roomReady = false;
            _roomQueryPending = false;
            _joinedRemoteMachineIds.Clear();
            _departedRemoteMachineIds.Clear();
            foreach (var remoteId in _remoteIds.Values)
            {
                remoteId.ProcessDisconnected();
            }
            _remoteIds.Clear();
            PlayerIDPairs.Clear();
            Reset();
            PID.Reset();
            Connect.PlayerLimit = 4;
        }

        private static int NormalizePlayerLimit(int value)
        {
            return global::System.Math.Max(1, global::System.Math.Min(4, value));
        }

        private bool IsWrapperAvailable(IDWrapper wrapper)
        {
            if (wrapper == null || !wrapper.Connected)
            {
                return false;
            }
            var machineId = GetMachineId(wrapper);
            if (string.IsNullOrEmpty(machineId) ||
                string.Equals(machineId, _transport.LocalMachineId, StringComparison.Ordinal))
            {
                return true;
            }
            if (_departedRemoteMachineIds.Contains(machineId))
            {
                return false;
            }
            if (IsHost)
            {
                return _joinedRemoteMachineIds.Contains(machineId) &&
                       _transport.IsMachineConnected(machineId);
            }
            return _clientJoined && _transport.IsHandshakeComplete;
        }

        private IDWrapper GetOrCreateRemoteId(string machineId)
        {
            if (string.IsNullOrEmpty(machineId))
            {
                return null;
            }

            IDWrapper remoteId;
            if (!_remoteIds.TryGetValue(machineId, out remoteId))
            {
                remoteId = new IDWrapper(FormatMachineId(machineId), PID.NoID, true);
                _remoteIds.Add(machineId, remoteId);
            }
            return remoteId;
        }

        private static string GetMachineId(IDWrapper wrapper)
        {
            return wrapper == null ? string.Empty : ParseMachineId(wrapper.UnderlyingID);
        }

        private static string ParseMachineId(string underlyingId)
        {
            if (string.IsNullOrEmpty(underlyingId) ||
                !underlyingId.StartsWith(MachineIdPrefix, StringComparison.Ordinal))
            {
                return string.Empty;
            }
            return underlyingId.Substring(MachineIdPrefix.Length);
        }

        private static string FormatMachineId(string value)
        {
            return string.IsNullOrEmpty(value) ? string.Empty : MachineIdPrefix + value;
        }

        private void Subscribe()
        {
            _transport.ConfigurationChanging += OnConfigurationChanging;
            _transport.PlayerLimitChanged += OnPlayerLimitChanged;
            _transport.HandshakeCompleted += OnHandshakeCompleted;
            _transport.RemoteDisconnected += OnRemoteDisconnected;
            _transport.RoomQueryReceived += OnRoomQueryReceived;
            _transport.RoomStateReceived += OnRoomStateReceived;
            _transport.JoinRequestReceived += OnJoinRequestReceived;
            _transport.JoinAcceptedReceived += OnJoinAcceptedReceived;
            _transport.JoinRejectedReceived += OnJoinRejectedReceived;
            _transport.GameDataReceived += OnGameDataReceived;
            _transport.LeaveNoticeReceived += OnLeaveNoticeReceived;
            _transport.MemberLeftReceived += OnMemberLeftReceived;
        }

        private void Unsubscribe()
        {
            _transport.ConfigurationChanging -= OnConfigurationChanging;
            _transport.PlayerLimitChanged -= OnPlayerLimitChanged;
            _transport.HandshakeCompleted -= OnHandshakeCompleted;
            _transport.RemoteDisconnected -= OnRemoteDisconnected;
            _transport.RoomQueryReceived -= OnRoomQueryReceived;
            _transport.RoomStateReceived -= OnRoomStateReceived;
            _transport.JoinRequestReceived -= OnJoinRequestReceived;
            _transport.JoinAcceptedReceived -= OnJoinAcceptedReceived;
            _transport.JoinRejectedReceived -= OnJoinRejectedReceived;
            _transport.GameDataReceived -= OnGameDataReceived;
            _transport.LeaveNoticeReceived -= OnLeaveNoticeReceived;
            _transport.MemberLeftReceived -= OnMemberLeftReceived;
        }
    }
}
