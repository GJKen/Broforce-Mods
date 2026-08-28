using System;
using System.Collections.Generic;
using System.Globalization;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using Lidgren.Network;

namespace CustomMapMultiplayer
{
    internal sealed class FrpDirectTransport : IDisposable
    {
        private const string ApplicationIdentifier = "CustomMapMultiplayer.FrpDirect.v1";
        private const string ProtocolMagic = "BFOD-FRP";
        private const int ProtocolVersion = 4;
        private const int MaxRemoteConnections = 3;
        private const int HeartbeatIntervalSeconds = 5;
        private const int LatencySnapshotIntervalSeconds = 1;
        private const int HandshakeTimeoutSeconds = 15;
        private const int HeartbeatTimeoutSeconds = 60;
        private const int MainThreadStallThresholdSeconds = 10;
        private const int ReconnectDelaySeconds = 5;
        private const int MaxRoomStateBytes = 32768;
        private const int MaxGameDataBytes = 2097152;

        private readonly List<FrpPeer> _peers = new List<FrpPeer>();
        private readonly Dictionary<string, int> _hostReportedLatencies =
            new Dictionary<string, int>(StringComparer.Ordinal);
        private readonly string _localMachineId;
        private NetPeer _peer;
        private NetPeer _retiringPeer;
        private IPEndPoint _remoteEndpoint;
        private FrpRole _role;
        private string _roomPassword = string.Empty;
        private string _configurationKey = string.Empty;
        private DateTime _nextConnectAtUtc;
        private DateTime _lastUpdateAtUtc;
        private DateTime _nextLatencySnapshotAtUtc;
        private bool _fatalConnectionError;
        private bool _disposed;
        private int _suppressedInvalidPackets;
        private DateTime _nextInvalidPacketLogAtUtc;
        private FrpDirectConfiguration _pendingConfiguration;

        internal string Status { get; private set; }
        internal bool IsEnabled { get; private set; }
        internal bool IsHost { get { return _role == FrpRole.Host; } }
        internal int PlayerLimit { get; private set; }
        internal bool IsHandshakeComplete
        {
            get { return _peers.Exists(item => item.HandshakeComplete); }
        }
        internal string LocalMachineId { get { return _localMachineId; } }
        internal string RemoteMachineId
        {
            get
            {
                var remote = _peers.Find(item => item.HandshakeComplete);
                return remote == null ? string.Empty : remote.MachineId;
            }
        }
        internal IList<string> ConnectedRemoteMachineIds
        {
            get
            {
                var machineIds = new List<string>();
                foreach (var remote in _peers)
                {
                    if (remote.HandshakeComplete && !string.IsNullOrEmpty(remote.MachineId))
                    {
                        machineIds.Add(remote.MachineId);
                    }
                }
                return machineIds;
            }
        }

        internal event Action<string> HandshakeCompleted;
        internal event Action ConfigurationChanging;
        internal event Action<int> PlayerLimitChanged;
        internal event Action<string> RemoteDisconnected;
        internal event Action<string> RoomQueryReceived;
        internal event Action<string, bool, string> RoomStateReceived;
        internal event Action<string> JoinRequestReceived;
        internal event Action<string, string> JoinAcceptedReceived;
        internal event Action<string, string> JoinRejectedReceived;
        internal event Action<string, string, byte[]> GameDataReceived;
        internal event Action<string> LeaveNoticeReceived;
        internal event Action<string> MemberLeftReceived;

        internal FrpDirectTransport()
        {
            _localMachineId = Guid.NewGuid().ToString("N");
            PlayerLimit = 4;
            Status = "Disabled";
        }

        internal bool IsMachineConnected(string machineId)
        {
            var remote = FindPeer(machineId);
            return remote != null && remote.HandshakeComplete && !remote.DisconnectRequested;
        }

        internal int GetRoundTripTimeMilliseconds(string machineId)
        {
            machineId = NormalizeMachineId(machineId);
            if (string.IsNullOrEmpty(machineId))
            {
                return -1;
            }

            if (_role == FrpRole.Host)
            {
                return GetConnectionRoundTripTimeMilliseconds(FindPeer(machineId));
            }

            if (string.Equals(machineId, _localMachineId, StringComparison.Ordinal) ||
                string.Equals(machineId, RemoteMachineId, StringComparison.Ordinal))
            {
                return GetConnectionRoundTripTimeMilliseconds(FindPeer(string.Empty));
            }

            int latencyMilliseconds;
            return _hostReportedLatencies.TryGetValue(machineId, out latencyMilliseconds)
                ? latencyMilliseconds
                : -1;
        }

        internal bool RequestRoomState()
        {
            return SendEmptyControlMessage(ControlMessageKind.RoomQuery, null);
        }

        internal bool SendRoomState(bool hasRoom, string encodedRoom, string targetMachineId)
        {
            encodedRoom = hasRoom ? encodedRoom ?? string.Empty : string.Empty;
            if (Encoding.UTF8.GetByteCount(encodedRoom) > MaxRoomStateBytes)
            {
                DiagnosticLog.Warning("FRP_DIRECT room state was not sent because it is too large.");
                return false;
            }

            var sent = false;
            foreach (var remote in SelectPeers(targetMachineId, false))
            {
                if (!CanSendApplicationMessage(remote))
                {
                    continue;
                }

                var outgoing = CreateControlMessage(ControlMessageKind.RoomState);
                outgoing.Write(hasRoom);
                outgoing.Write(encodedRoom);
                SendReliable(remote, outgoing);
                sent = true;
            }
            return sent;
        }

        internal bool RequestJoin()
        {
            return SendEmptyControlMessage(ControlMessageKind.JoinRequest, null);
        }

        internal bool AcceptJoin(string encodedRoom, string targetMachineId)
        {
            encodedRoom = encodedRoom ?? string.Empty;
            var remote = FindPeer(targetMachineId);
            if (!CanSendApplicationMessage(remote) ||
                Encoding.UTF8.GetByteCount(encodedRoom) > MaxRoomStateBytes)
            {
                return false;
            }

            var outgoing = CreateControlMessage(ControlMessageKind.JoinAccepted);
            outgoing.Write(encodedRoom);
            SendReliable(remote, outgoing);
            return true;
        }

        internal bool RejectJoin(string reason, string targetMachineId)
        {
            var remote = FindPeer(targetMachineId);
            if (!CanSendApplicationMessage(remote))
            {
                return false;
            }

            var outgoing = CreateControlMessage(ControlMessageKind.JoinRejected);
            outgoing.Write(SafeReason(reason));
            SendReliable(remote, outgoing);
            return true;
        }

        internal bool SendGameData(string route, byte[] bytes, string excludeMachineId)
        {
            if (bytes == null || bytes.Length == 0 || bytes.Length > MaxGameDataBytes)
            {
                return false;
            }

            var sent = false;
            var targetMachineId = _role == FrpRole.Client || string.Equals(route, "*", StringComparison.Ordinal)
                ? null
                : route;
            foreach (var remote in SelectPeers(targetMachineId, string.Equals(route, "*", StringComparison.Ordinal)))
            {
                if (!CanSendApplicationMessage(remote) ||
                    string.Equals(remote.MachineId, excludeMachineId, StringComparison.Ordinal))
                {
                    continue;
                }

                var outgoing = CreateControlMessage(ControlMessageKind.GameData);
                outgoing.Write(route ?? string.Empty);
                outgoing.Write(bytes.Length);
                outgoing.Write(bytes);
                SendReliable(remote, outgoing);
                sent = true;
            }
            return sent;
        }

        internal bool SendLeaveNotice()
        {
            return SendEmptyControlMessage(ControlMessageKind.LeaveNotice, null);
        }

        internal bool SendMemberLeft(string departedMachineId, string excludeMachineId)
        {
            departedMachineId = NormalizeMachineId(departedMachineId);
            if (_role != FrpRole.Host || string.IsNullOrEmpty(departedMachineId))
            {
                return false;
            }

            var sent = false;
            foreach (var remote in SelectPeers(null, true))
            {
                if (!CanSendApplicationMessage(remote) ||
                    string.Equals(remote.MachineId, excludeMachineId, StringComparison.Ordinal))
                {
                    continue;
                }

                var outgoing = CreateControlMessage(ControlMessageKind.MemberLeft);
                outgoing.Write(departedMachineId);
                SendReliable(remote, outgoing);
                sent = true;
            }
            return sent;
        }

        internal void Apply(DiagnosticSettings settings, bool forceRestart)
        {
            if (_disposed)
            {
                return;
            }

            var configuration = FrpDirectConfiguration.FromSettings(settings);
            if (!forceRestart &&
                string.Equals(_configurationKey, configuration.ConfigurationKey, StringComparison.Ordinal))
            {
                UpdatePlayerLimit(configuration.PlayerLimit);
                return;
            }

            Raise(ConfigurationChanging, "configuration change");
            Stop("configuration changed");
            _configurationKey = configuration.ConfigurationKey;
            PlayerLimit = configuration.PlayerLimit;
            IsEnabled = configuration.Enabled;
            if (!configuration.Enabled)
            {
                Status = "Disabled";
                return;
            }

            _pendingConfiguration = configuration;
            Status = "Restarting";
            ContinuePendingStart();
        }

        internal void UpdatePlayerLimit(int playerLimit)
        {
            if (_disposed)
            {
                return;
            }

            var normalizedPlayerLimit = NormalizePlayerLimit(playerLimit);
            if (PlayerLimit == normalizedPlayerLimit)
            {
                return;
            }

            PlayerLimit = normalizedPlayerLimit;
            DiagnosticLog.Info(
                "FRP_DIRECT room player limit changed without restarting transport; playerLimit=" +
                PlayerLimit + ".");
            Raise(PlayerLimitChanged, PlayerLimit, "player limit change");
        }

        internal void Update()
        {
            if (_disposed)
            {
                return;
            }

            ContinuePendingStart();
            if (_peer == null)
            {
                return;
            }

            try
            {
                var now = DateTime.UtcNow;
                HandleMainThreadStall(now);
                NetIncomingMessage message;
                while ((message = _peer.ReadMessage()) != null)
                {
                    try
                    {
                        ProcessMessage(message);
                    }
                    catch (Exception exception)
                    {
                        LogInvalidControlPacket(exception);
                    }
                    finally
                    {
                        _peer.Recycle(message);
                    }
                }

                UpdateTimers(now);
                _lastUpdateAtUtc = now;
            }
            catch (Exception exception)
            {
                var errorName = exception.GetType().Name;
                DiagnosticLog.Error(
                    "FRP_DIRECT update failed; the prototype is stopping; error=" + errorName + ".");
                Stop("update failed");
                Status = "Transport error: " + errorName;
            }
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            Stop("disposed");
            IsEnabled = false;
            _disposed = true;
        }

        private void Start(FrpDirectConfiguration settings)
        {
            _role = settings.Role;
            _roomPassword = settings.RoomPassword;
            PlayerLimit = settings.PlayerLimit;
            _fatalConnectionError = false;
            _nextLatencySnapshotAtUtc = DateTime.UtcNow;

            try
            {
                if (_role == FrpRole.Client)
                {
                    if (string.IsNullOrEmpty(settings.ServerAddress) || settings.ServerPort == 0)
                    {
                        Status = "Server endpoint must use host:port";
                        DiagnosticLog.Warning(
                            "FRP_DIRECT client did not start because the server endpoint is invalid.");
                        return;
                    }

                    _remoteEndpoint = NetUtility.Resolve(settings.ServerAddress, settings.ServerPort);
                    if (_remoteEndpoint == null)
                    {
                        Status = "Server address could not be resolved";
                        DiagnosticLog.Warning(
                            "FRP_DIRECT client could not resolve the configured server address.");
                        return;
                    }
                }

                var configuration = new NetPeerConfiguration(ApplicationIdentifier);
                configuration.AcceptIncomingConnections = _role == FrpRole.Host;
                configuration.MaximumConnections = _role == FrpRole.Host ? MaxRemoteConnections : 1;
                configuration.Port = _role == FrpRole.Host ? settings.LocalPort : 0;
                configuration.PingInterval = 4f;
                configuration.ConnectionTimeout = 25f;
                configuration.ResendHandshakeInterval = 2f;
                configuration.MaximumHandshakeAttempts = 6;
                configuration.EnableMessageType(NetIncomingMessageType.Data);
                configuration.EnableMessageType(NetIncomingMessageType.StatusChanged);

                _peer = new NetPeer(configuration);
                _peer.Start();

                if (_role == FrpRole.Host)
                {
                    Status = "Listening on UDP " + _peer.Port;
                    DiagnosticLog.Info(
                        "FRP_DIRECT host started; protocol=" + ProtocolVersion +
                        "; maxRemoteConnections=" + MaxRemoteConnections +
                        "; playerLimit=" + PlayerLimit +
                        "; localUdpPort=" + _peer.Port +
                        "; buildHash=" + BuildMetadata.BuildHash + ".");
                }
                else
                {
                    Status = "Connecting";
                    DiagnosticLog.Info(
                        "FRP_DIRECT client started; protocol=" + ProtocolVersion +
                        "; remoteUdpPort=" + settings.ServerPort +
                        "; buildHash=" + BuildMetadata.BuildHash + ".");
                    ConnectClient();
                }
            }
            catch (Exception exception)
            {
                Stop("start failed");
                Status = "Start failed: " + exception.GetType().Name;
                DiagnosticLog.Error(
                    "FRP_DIRECT failed to start; role=" + RoleName(_role) +
                    "; error=" + exception.GetType().Name + ".");
            }
        }

        private void Stop(string reason)
        {
            var disconnectedMachineIds = new List<string>();
            foreach (var remote in _peers)
            {
                if (remote.HandshakeComplete && !string.IsNullOrEmpty(remote.MachineId))
                {
                    disconnectedMachineIds.Add(remote.MachineId);
                }
            }

            _pendingConfiguration = null;
            if (_peer != null)
            {
                try
                {
                    _peer.Shutdown("FRP Direct stopped");
                }
                catch (Exception exception)
                {
                    DiagnosticLog.Warning(
                        "FRP_DIRECT shutdown failed; error=" + exception.GetType().Name + ".");
                }

                DiagnosticLog.Info("FRP_DIRECT stopped; reason=" + SafeReason(reason) + ".");
                _retiringPeer = _peer;
            }

            _peer = null;
            _peers.Clear();
            _hostReportedLatencies.Clear();
            _remoteEndpoint = null;
            _roomPassword = string.Empty;
            _fatalConnectionError = false;
            _suppressedInvalidPackets = 0;
            _nextInvalidPacketLogAtUtc = DateTime.MinValue;
            _nextLatencySnapshotAtUtc = DateTime.MinValue;
            foreach (var machineId in disconnectedMachineIds)
            {
                Raise(RemoteDisconnected, machineId, "remote disconnect");
            }
        }

        private void ContinuePendingStart()
        {
            if (_retiringPeer != null)
            {
                if (_retiringPeer.Status != NetPeerStatus.NotRunning)
                {
                    return;
                }
                _retiringPeer = null;
            }

            if (_pendingConfiguration == null)
            {
                return;
            }

            var configuration = _pendingConfiguration;
            _pendingConfiguration = null;
            Start(configuration);
        }

        private void ProcessMessage(NetIncomingMessage message)
        {
            switch (message.MessageType)
            {
                case NetIncomingMessageType.StatusChanged:
                    HandleStatusChanged(message);
                    break;
                case NetIncomingMessageType.Data:
                    HandleControlMessage(message);
                    break;
            }
        }

        private void HandleStatusChanged(NetIncomingMessage message)
        {
            var status = (NetConnectionStatus)message.ReadByte();
            var connection = message.SenderConnection;
            if (status == NetConnectionStatus.Connected)
            {
                HandleConnected(connection);
            }
            else if (status == NetConnectionStatus.Disconnected)
            {
                HandleDisconnected(connection);
            }
        }

        private void HandleConnected(NetConnection connection)
        {
            if (connection == null)
            {
                return;
            }

            var remote = FindPeer(connection);
            if (remote == null)
            {
                if (_role == FrpRole.Host && _peers.Count >= MaxRemoteConnections)
                {
                    connection.Disconnect("FRP Direct room is full");
                    return;
                }

                remote = new FrpPeer(connection);
                _peers.Add(remote);
            }

            remote.ConnectedAtUtc = DateTime.UtcNow;
            remote.LastHeartbeatAtUtc = remote.ConnectedAtUtc;
            remote.DisconnectRequested = false;
            if (_role == FrpRole.Host)
            {
                remote.Challenge = Guid.NewGuid().ToString("N");
                Status = "Peer connected; authenticating";
                DiagnosticLog.Info(
                    "FRP_DIRECT UDP peer connected to host; activeConnections=" + _peers.Count + ".");
                SendServerHello(remote);
            }
            else
            {
                Status = "Connected; waiting for host handshake";
                DiagnosticLog.Info(
                    "FRP_DIRECT UDP connection established; waiting for host handshake.");
            }
        }

        private void HandleDisconnected(NetConnection connection)
        {
            var remote = FindPeer(connection);
            if (remote == null)
            {
                return;
            }

            var wasReady = remote.HandshakeComplete;
            var remoteMachineId = remote.MachineId;
            _peers.Remove(remote);
            if (_role == FrpRole.Host)
            {
                UpdateHostStatus();
            }
            else if (!_fatalConnectionError)
            {
                Status = "Disconnected; retrying";
                _nextConnectAtUtc = DateTime.UtcNow.AddSeconds(ReconnectDelaySeconds);
            }

            DiagnosticLog.Warning(
                "FRP_DIRECT transport disconnected; role=" + RoleName(_role) +
                "; remoteMachineId=" + SafeMachineId(remoteMachineId) +
                "; handshakeCompleted=" + wasReady +
                "; willRetry=" + (_role == FrpRole.Client && !_fatalConnectionError) + ".");
            if (wasReady)
            {
                Raise(RemoteDisconnected, remoteMachineId, "remote disconnect");
            }
        }

        private void HandleControlMessage(NetIncomingMessage message)
        {
            var remote = FindPeer(message.SenderConnection);
            if (remote == null ||
                !string.Equals(message.ReadString(), ProtocolMagic, StringComparison.Ordinal))
            {
                return;
            }

            var kind = (ControlMessageKind)message.ReadByte();
            switch (kind)
            {
                case ControlMessageKind.ServerHello:
                    if (_role == FrpRole.Client)
                    {
                        ReceiveServerHello(remote, message);
                    }
                    break;
                case ControlMessageKind.ClientHello:
                    if (_role == FrpRole.Host)
                    {
                        ReceiveClientHello(remote, message);
                    }
                    break;
                case ControlMessageKind.HandshakeResult:
                    if (_role == FrpRole.Client)
                    {
                        ReceiveHandshakeResult(remote, message);
                    }
                    break;
                case ControlMessageKind.Heartbeat:
                    if (_role == FrpRole.Host && remote.HandshakeComplete)
                    {
                        ReceiveHeartbeat(remote, message);
                    }
                    break;
                case ControlMessageKind.HeartbeatAck:
                    if (_role == FrpRole.Client && remote.HandshakeComplete)
                    {
                        ReceiveHeartbeatAck(remote, message);
                    }
                    break;
                case ControlMessageKind.RoomQuery:
                    if (_role == FrpRole.Host && remote.HandshakeComplete)
                    {
                        Raise(RoomQueryReceived, remote.MachineId, "room query");
                    }
                    break;
                case ControlMessageKind.RoomState:
                    if (_role == FrpRole.Client && remote.HandshakeComplete)
                    {
                        ReceiveRoomState(remote, message);
                    }
                    break;
                case ControlMessageKind.JoinRequest:
                    if (_role == FrpRole.Host && remote.HandshakeComplete)
                    {
                        Raise(JoinRequestReceived, remote.MachineId, "join request");
                    }
                    break;
                case ControlMessageKind.JoinAccepted:
                    if (_role == FrpRole.Client && remote.HandshakeComplete)
                    {
                        ReceiveJoinAccepted(remote, message);
                    }
                    break;
                case ControlMessageKind.JoinRejected:
                    if (_role == FrpRole.Client && remote.HandshakeComplete)
                    {
                        Raise(
                            JoinRejectedReceived,
                            remote.MachineId,
                            SafeReason(message.ReadString()),
                            "join rejected");
                    }
                    break;
                case ControlMessageKind.GameData:
                    if (remote.HandshakeComplete)
                    {
                        ReceiveGameData(remote, message);
                    }
                    break;
                case ControlMessageKind.LeaveNotice:
                    if (remote.HandshakeComplete)
                    {
                        Raise(LeaveNoticeReceived, remote.MachineId, "leave notice");
                    }
                    break;
                case ControlMessageKind.MemberLeft:
                    if (_role == FrpRole.Client && remote.HandshakeComplete)
                    {
                        ReceiveMemberLeft(message);
                    }
                    break;
                case ControlMessageKind.LatencySnapshot:
                    if (_role == FrpRole.Client && remote.HandshakeComplete)
                    {
                        ReceiveLatencySnapshot(message);
                    }
                    break;
            }
        }

        private void SendServerHello(FrpPeer remote)
        {
            var outgoing = CreateControlMessage(ControlMessageKind.ServerHello);
            outgoing.Write(ProtocolVersion);
            outgoing.Write(BuildMetadata.BuildHash);
            outgoing.Write(remote.Challenge);
            outgoing.Write(_localMachineId);
            SendReliable(remote, outgoing);
        }

        private void ReceiveServerHello(FrpPeer remote, NetIncomingMessage message)
        {
            var serverProtocol = message.ReadInt32();
            var serverBuildHash = message.ReadString();
            var challenge = message.ReadString();
            remote.MachineId = NormalizeMachineId(message.ReadString());
            remote.ClientNonce = Guid.NewGuid().ToString("N");

            var proof = ComputePasswordProof(
                _roomPassword,
                challenge,
                remote.ClientNonce,
                _localMachineId,
                remote.MachineId,
                ProtocolVersion,
                BuildMetadata.BuildHash,
                serverProtocol,
                serverBuildHash);
            var outgoing = CreateControlMessage(ControlMessageKind.ClientHello);
            outgoing.Write(ProtocolVersion);
            outgoing.Write(BuildMetadata.BuildHash);
            outgoing.Write(remote.ClientNonce);
            outgoing.Write(proof);
            outgoing.Write(_localMachineId);
            SendReliable(remote, outgoing);
            Status = serverProtocol == ProtocolVersion &&
                     BuildHashesMatch(serverBuildHash, BuildMetadata.BuildHash)
                ? "Authenticating"
                : "Version mismatch; waiting for rejection";
        }

        private void ReceiveClientHello(FrpPeer remote, NetIncomingMessage message)
        {
            var clientProtocol = message.ReadInt32();
            var clientBuildHash = message.ReadString();
            var clientNonce = message.ReadString();
            var suppliedProof = message.ReadString();
            var clientMachineId = NormalizeMachineId(message.ReadString());
            var expectedProof = ComputePasswordProof(
                _roomPassword,
                remote.Challenge,
                clientNonce,
                clientMachineId,
                _localMachineId,
                clientProtocol,
                clientBuildHash,
                ProtocolVersion,
                BuildMetadata.BuildHash);
            var duplicateMachineId = _peers.Exists(
                item => item != remote && item.HandshakeComplete &&
                        string.Equals(item.MachineId, clientMachineId, StringComparison.Ordinal));
            var accepted = clientProtocol == ProtocolVersion &&
                           BuildHashesMatch(clientBuildHash, BuildMetadata.BuildHash) &&
                           FixedTimeEquals(suppliedProof, expectedProof) &&
                           !string.IsNullOrEmpty(clientMachineId) &&
                           !duplicateMachineId;
            var reason = accepted
                ? "accepted"
                : (clientProtocol != ProtocolVersion
                    ? "protocol_mismatch"
                    : (!BuildHashesMatch(clientBuildHash, BuildMetadata.BuildHash)
                        ? "build_hash_mismatch"
                        : (!FixedTimeEquals(suppliedProof, expectedProof)
                            ? "authentication_failed"
                            : (duplicateMachineId ? "duplicate_machine_id" : "invalid_machine_id"))));

            SendHandshakeResult(remote, accepted, reason);
            if (!accepted)
            {
                DiagnosticLog.Warning(
                    "FRP_DIRECT host rejected the client handshake; reason=" + reason + ".");
                return;
            }

            remote.MachineId = clientMachineId;
            remote.HandshakeComplete = true;
            remote.LastHeartbeatAtUtc = DateTime.UtcNow;
            UpdateHostStatus();
            DiagnosticLog.Info(
                "FRP_DIRECT host accepted a client handshake; remoteMachineId=" +
                SafeMachineId(clientMachineId) + ".");
            Raise(HandshakeCompleted, remote.MachineId, "handshake completion");
        }

        private void SendHandshakeResult(FrpPeer remote, bool accepted, string reason)
        {
            var outgoing = CreateControlMessage(ControlMessageKind.HandshakeResult);
            outgoing.Write(accepted);
            outgoing.Write(reason);
            outgoing.Write(ProtocolVersion);
            outgoing.Write(BuildMetadata.BuildHash);
            SendReliable(remote, outgoing);
        }

        private void ReceiveHandshakeResult(FrpPeer remote, NetIncomingMessage message)
        {
            var accepted = message.ReadBoolean();
            var reason = SafeReason(message.ReadString());
            var serverProtocol = message.ReadInt32();
            var serverBuildHash = message.ReadString();
            if (!accepted || serverProtocol != ProtocolVersion ||
                !BuildHashesMatch(serverBuildHash, BuildMetadata.BuildHash) ||
                string.IsNullOrEmpty(remote.MachineId))
            {
                _fatalConnectionError = true;
                Status = "Handshake rejected: " + reason;
                DiagnosticLog.Warning(
                    "FRP_DIRECT client handshake rejected; reason=" + reason + ".");
                Disconnect(remote, "FRP Direct handshake rejected");
                return;
            }

            remote.HandshakeComplete = true;
            remote.LastHeartbeatAtUtc = DateTime.UtcNow;
            remote.NextHeartbeatAtUtc = DateTime.UtcNow;
            Status = "Handshake complete; heartbeat active";
            DiagnosticLog.Info("FRP_DIRECT client handshake accepted; heartbeat monitoring started.");
            Raise(HandshakeCompleted, remote.MachineId, "handshake completion");
        }

        private void ReceiveRoomState(FrpPeer remote, NetIncomingMessage message)
        {
            var hasRoom = message.ReadBoolean();
            var encodedRoom = message.ReadString() ?? string.Empty;
            if (Encoding.UTF8.GetByteCount(encodedRoom) > MaxRoomStateBytes)
            {
                throw new InvalidOperationException("Room state exceeds the protocol limit.");
            }
            Raise(RoomStateReceived, remote.MachineId, hasRoom, encodedRoom, "room state");
        }

        private void ReceiveJoinAccepted(FrpPeer remote, NetIncomingMessage message)
        {
            var encodedRoom = message.ReadString() ?? string.Empty;
            if (Encoding.UTF8.GetByteCount(encodedRoom) > MaxRoomStateBytes)
            {
                throw new InvalidOperationException("Join room state exceeds the protocol limit.");
            }
            Raise(JoinAcceptedReceived, remote.MachineId, encodedRoom, "join accepted");
        }

        private void ReceiveGameData(FrpPeer remote, NetIncomingMessage message)
        {
            var route = NormalizeRoute(message.ReadString());
            var length = message.ReadInt32();
            if (route == null || length <= 0 || length > MaxGameDataBytes)
            {
                throw new InvalidOperationException("Game data envelope is invalid.");
            }
            Raise(
                GameDataReceived,
                remote.MachineId,
                route,
                message.ReadBytes(length),
                "game data");
        }

        private void ReceiveMemberLeft(NetIncomingMessage message)
        {
            var departedMachineId = NormalizeMachineId(message.ReadString());
            if (string.IsNullOrEmpty(departedMachineId))
            {
                throw new InvalidOperationException("Departed machine ID is invalid.");
            }
            Raise(MemberLeftReceived, departedMachineId, "member left");
        }

        private void SendLatencySnapshot(DateTime now)
        {
            var latencies = new Dictionary<string, int>(StringComparer.Ordinal);
            foreach (var remote in _peers)
            {
                if (!remote.HandshakeComplete || remote.DisconnectRequested ||
                    string.IsNullOrEmpty(remote.MachineId))
                {
                    continue;
                }

                latencies[remote.MachineId] =
                    GetConnectionRoundTripTimeMilliseconds(remote);
            }

            foreach (var remote in _peers)
            {
                if (!CanSendApplicationMessage(remote))
                {
                    continue;
                }

                var outgoing = CreateControlMessage(ControlMessageKind.LatencySnapshot);
                outgoing.Write(latencies.Count);
                foreach (var pair in latencies)
                {
                    outgoing.Write(pair.Key);
                    outgoing.Write(pair.Value);
                }
                SendReliable(remote, outgoing);
            }

            _nextLatencySnapshotAtUtc = now.AddSeconds(LatencySnapshotIntervalSeconds);
        }

        private void ReceiveLatencySnapshot(NetIncomingMessage message)
        {
            var count = message.ReadInt32();
            if (count < 0 || count > MaxRemoteConnections)
            {
                throw new InvalidOperationException("Latency snapshot entry count is invalid.");
            }

            var latencies = new Dictionary<string, int>(StringComparer.Ordinal);
            for (var index = 0; index < count; index++)
            {
                var machineId = NormalizeMachineId(message.ReadString());
                var latencyMilliseconds = message.ReadInt32();
                if (string.IsNullOrEmpty(machineId) ||
                    latencyMilliseconds < -1 || latencyMilliseconds > 9999)
                {
                    throw new InvalidOperationException("Latency snapshot entry is invalid.");
                }
                latencies[machineId] = latencyMilliseconds;
            }

            _hostReportedLatencies.Clear();
            foreach (var pair in latencies)
            {
                _hostReportedLatencies.Add(pair.Key, pair.Value);
            }
        }

        private void SendHeartbeat(FrpPeer remote, DateTime now)
        {
            remote.HeartbeatSequence++;
            var outgoing = CreateControlMessage(ControlMessageKind.Heartbeat);
            outgoing.Write(remote.HeartbeatSequence);
            SendReliable(remote, outgoing);
            remote.NextHeartbeatAtUtc = now.AddSeconds(HeartbeatIntervalSeconds);
        }

        private void ReceiveHeartbeat(FrpPeer remote, NetIncomingMessage message)
        {
            var sequence = message.ReadInt32();
            remote.LastHeartbeatAtUtc = DateTime.UtcNow;
            var outgoing = CreateControlMessage(ControlMessageKind.HeartbeatAck);
            outgoing.Write(sequence);
            SendReliable(remote, outgoing);
        }

        private void ReceiveHeartbeatAck(FrpPeer remote, NetIncomingMessage message)
        {
            var sequence = message.ReadInt32();
            if (sequence <= 0 || sequence > remote.HeartbeatSequence)
            {
                return;
            }

            remote.LastHeartbeatAtUtc = DateTime.UtcNow;
            Status = "Connected; heartbeat " + sequence;
        }

        private void UpdateTimers(DateTime now)
        {
            if (_role == FrpRole.Client && _peers.Count == 0 && !_fatalConnectionError &&
                _remoteEndpoint != null && now >= _nextConnectAtUtc)
            {
                ConnectClient();
                return;
            }

            foreach (var remote in new List<FrpPeer>(_peers))
            {
                if (remote.DisconnectRequested)
                {
                    continue;
                }

                if (!remote.HandshakeComplete)
                {
                    if ((now - remote.ConnectedAtUtc).TotalSeconds >= HandshakeTimeoutSeconds)
                    {
                        DiagnosticLog.Warning("FRP_DIRECT application handshake timed out.");
                        Disconnect(remote, "FRP Direct handshake timeout");
                    }
                    continue;
                }

                if (_role == FrpRole.Client && now >= remote.NextHeartbeatAtUtc)
                {
                    SendHeartbeat(remote, now);
                }

                if ((now - remote.LastHeartbeatAtUtc).TotalSeconds >= HeartbeatTimeoutSeconds)
                {
                    DiagnosticLog.Warning(
                        "FRP_DIRECT heartbeat timed out; remoteMachineId=" +
                        SafeMachineId(remote.MachineId) + ".");
                    Disconnect(remote, "FRP Direct heartbeat timeout");
                }
            }

            if (_role == FrpRole.Host && now >= _nextLatencySnapshotAtUtc)
            {
                SendLatencySnapshot(now);
            }
        }

        private void HandleMainThreadStall(DateTime now)
        {
            if (_lastUpdateAtUtc == DateTime.MinValue)
            {
                return;
            }

            var stalledSeconds = (now - _lastUpdateAtUtc).TotalSeconds;
            if (stalledSeconds < MainThreadStallThresholdSeconds)
            {
                return;
            }

            foreach (var remote in _peers)
            {
                if (!remote.HandshakeComplete)
                {
                    continue;
                }

                remote.LastHeartbeatAtUtc = now;
                if (_role == FrpRole.Client)
                {
                    remote.NextHeartbeatAtUtc = now;
                }
            }
            DiagnosticLog.Warning(
                "FRP_DIRECT main-thread update paused for " + (int)stalledSeconds +
                " seconds; heartbeat timeout windows resumed.");
        }

        private void Disconnect(FrpPeer remote, string reason)
        {
            if (remote == null || remote.DisconnectRequested)
            {
                return;
            }

            remote.DisconnectRequested = true;
            remote.Connection.Disconnect(reason);
        }

        private void ConnectClient()
        {
            if (_peer == null || _remoteEndpoint == null || _fatalConnectionError)
            {
                return;
            }

            try
            {
                var connection = _peer.Connect(_remoteEndpoint);
                var remote = new FrpPeer(connection);
                remote.ConnectedAtUtc = DateTime.UtcNow;
                remote.LastHeartbeatAtUtc = remote.ConnectedAtUtc;
                _peers.Add(remote);
                _nextConnectAtUtc = remote.ConnectedAtUtc.AddSeconds(ReconnectDelaySeconds);
                Status = "Connecting";
                DiagnosticLog.Info("FRP_DIRECT client connection attempt started.");
            }
            catch (Exception exception)
            {
                _nextConnectAtUtc = DateTime.UtcNow.AddSeconds(ReconnectDelaySeconds);
                Status = "Connect failed; retrying";
                DiagnosticLog.Warning(
                    "FRP_DIRECT client connection attempt failed; error=" +
                    exception.GetType().Name + ".");
            }
        }

        private void UpdateHostStatus()
        {
            var readyCount = 0;
            foreach (var remote in _peers)
            {
                if (remote.HandshakeComplete)
                {
                    readyCount++;
                }
            }
            Status = readyCount == 0
                ? "Listening on UDP " + (_peer == null ? 0 : _peer.Port)
                : "Authenticated clients: " + readyCount + "/" + MaxRemoteConnections;
        }

        private void LogInvalidControlPacket(Exception exception)
        {
            var now = DateTime.UtcNow;
            if (now < _nextInvalidPacketLogAtUtc)
            {
                _suppressedInvalidPackets++;
                return;
            }

            if (_suppressedInvalidPackets > 0)
            {
                DiagnosticLog.Warning(
                    "FRP_DIRECT suppressed " + _suppressedInvalidPackets +
                    " additional invalid control packets.");
            }
            _suppressedInvalidPackets = 0;
            _nextInvalidPacketLogAtUtc = now.AddSeconds(5);
            DiagnosticLog.Warning(
                "FRP_DIRECT ignored an invalid control packet; error=" +
                exception.GetType().Name + ".");
        }

        private FrpPeer FindPeer(NetConnection connection)
        {
            return _peers.Find(item => item.Connection == connection);
        }

        private FrpPeer FindPeer(string machineId)
        {
            if (string.IsNullOrEmpty(machineId))
            {
                return _role == FrpRole.Client && _peers.Count == 1 ? _peers[0] : null;
            }
            return _peers.Find(
                item => string.Equals(item.MachineId, machineId, StringComparison.Ordinal));
        }

        private static int GetConnectionRoundTripTimeMilliseconds(FrpPeer remote)
        {
            if (remote == null || remote.Connection == null ||
                !remote.HandshakeComplete || remote.DisconnectRequested)
            {
                return -1;
            }

            return OnlinePlayerListFormatter.SecondsToMilliseconds(
                remote.Connection.AverageRoundtripTime);
        }

        private IEnumerable<FrpPeer> SelectPeers(string targetMachineId, bool broadcast)
        {
            foreach (var remote in _peers)
            {
                if (!broadcast && !string.IsNullOrEmpty(targetMachineId) &&
                    !string.Equals(remote.MachineId, targetMachineId, StringComparison.Ordinal))
                {
                    continue;
                }
                yield return remote;
            }
        }

        private NetOutgoingMessage CreateControlMessage(ControlMessageKind kind)
        {
            var outgoing = _peer.CreateMessage();
            outgoing.Write(ProtocolMagic);
            outgoing.Write((byte)kind);
            return outgoing;
        }

        private bool SendEmptyControlMessage(ControlMessageKind kind, string targetMachineId)
        {
            var sent = false;
            foreach (var remote in SelectPeers(targetMachineId, false))
            {
                if (!CanSendApplicationMessage(remote))
                {
                    continue;
                }
                SendReliable(remote, CreateControlMessage(kind));
                sent = true;
            }
            return sent;
        }

        private bool CanSendApplicationMessage(FrpPeer remote)
        {
            return _peer != null && remote != null && remote.Connection != null &&
                   remote.HandshakeComplete && !remote.DisconnectRequested;
        }

        private static void SendReliable(FrpPeer remote, NetOutgoingMessage message)
        {
            if (remote == null || remote.Connection == null)
            {
                return;
            }
            remote.Connection.SendMessage(message, NetDeliveryMethod.ReliableOrdered, 0);
        }

        private static string NormalizeRoute(string value)
        {
            value = value ?? string.Empty;
            if (value.Length == 0 || string.Equals(value, "*", StringComparison.Ordinal))
            {
                return value;
            }
            var machineId = NormalizeMachineId(value);
            return string.IsNullOrEmpty(machineId) ? null : machineId;
        }

        private static string ComputePasswordProof(
            string password,
            string challenge,
            string clientNonce,
            string clientMachineId,
            string serverMachineId,
            int clientProtocol,
            string clientBuildHash,
            int serverProtocol,
            string serverBuildHash)
        {
            var key = Encoding.UTF8.GetBytes("BFOD-FRP-PASSWORD\n" + (password ?? string.Empty));
            var value = string.Join(
                "\n",
                new[]
                {
                    ProtocolMagic,
                    challenge ?? string.Empty,
                    clientNonce ?? string.Empty,
                    clientMachineId ?? string.Empty,
                    serverMachineId ?? string.Empty,
                    clientProtocol.ToString(CultureInfo.InvariantCulture),
                    clientBuildHash ?? string.Empty,
                    serverProtocol.ToString(CultureInfo.InvariantCulture),
                    serverBuildHash ?? string.Empty
                });
            using (var hmac = new HMACSHA256(key))
            {
                var bytes = hmac.ComputeHash(Encoding.UTF8.GetBytes(value));
                var builder = new StringBuilder(bytes.Length * 2);
                for (var index = 0; index < bytes.Length; index++)
                {
                    builder.Append(bytes[index].ToString("x2", CultureInfo.InvariantCulture));
                }
                return builder.ToString();
            }
        }

        private static bool FixedTimeEquals(string left, string right)
        {
            left = left ?? string.Empty;
            right = right ?? string.Empty;
            var difference = left.Length ^ right.Length;
            var length = global::System.Math.Max(left.Length, right.Length);
            for (var index = 0; index < length; index++)
            {
                var leftValue = index < left.Length ? left[index] : (char)0;
                var rightValue = index < right.Length ? right[index] : (char)0;
                difference |= leftValue ^ rightValue;
            }
            return difference == 0;
        }

        private static bool BuildHashesMatch(string left, string right)
        {
            return !string.IsNullOrEmpty(left) && !string.IsNullOrEmpty(right) &&
                   string.Equals(left, right, StringComparison.OrdinalIgnoreCase);
        }

        private static string HashConfigurationKey(string value)
        {
            using (var sha256 = SHA256.Create())
            {
                var bytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(value ?? string.Empty));
                var builder = new StringBuilder(bytes.Length * 2);
                for (var index = 0; index < bytes.Length; index++)
                {
                    builder.Append(bytes[index].ToString("x2", CultureInfo.InvariantCulture));
                }
                return builder.ToString();
            }
        }

        private static FrpRole ParseRole(string value)
        {
            return string.Equals(
                (value ?? string.Empty).Trim(),
                "client",
                StringComparison.OrdinalIgnoreCase)
                ? FrpRole.Client
                : FrpRole.Host;
        }

        private static string RoleName(FrpRole role)
        {
            return role == FrpRole.Client ? "client" : "host";
        }

        private static string SafeReason(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return "unknown";
            }

            var builder = new StringBuilder();
            for (var index = 0; index < value.Length && index < 64; index++)
            {
                var current = value[index];
                if ((current >= 'a' && current <= 'z') || current == '_')
                {
                    builder.Append(current);
                }
            }
            return builder.Length == 0 ? "unknown" : builder.ToString();
        }

        private static string SafeMachineId(string value)
        {
            var normalized = NormalizeMachineId(value);
            return string.IsNullOrEmpty(normalized) ? "unknown" : normalized;
        }

        private static string NormalizeMachineId(string value)
        {
            value = (value ?? string.Empty).Trim();
            if (value.Length != 32)
            {
                return string.Empty;
            }

            for (var index = 0; index < value.Length; index++)
            {
                var current = value[index];
                if (!((current >= '0' && current <= '9') ||
                      (current >= 'a' && current <= 'f') ||
                      (current >= 'A' && current <= 'F')))
                {
                    return string.Empty;
                }
            }
            return value.ToLowerInvariant();
        }

        private static void Raise(Action callback, string operation)
        {
            if (callback == null)
            {
                return;
            }
            try
            {
                callback();
            }
            catch (Exception exception)
            {
                LogCallbackFailure(operation, exception);
            }
        }

        private static void Raise<T>(Action<T> callback, T value, string operation)
        {
            if (callback == null)
            {
                return;
            }
            try
            {
                callback(value);
            }
            catch (Exception exception)
            {
                LogCallbackFailure(operation, exception);
            }
        }

        private static void Raise<T1, T2>(
            Action<T1, T2> callback,
            T1 first,
            T2 second,
            string operation)
        {
            if (callback == null)
            {
                return;
            }
            try
            {
                callback(first, second);
            }
            catch (Exception exception)
            {
                LogCallbackFailure(operation, exception);
            }
        }

        private static void Raise<T1, T2, T3>(
            Action<T1, T2, T3> callback,
            T1 first,
            T2 second,
            T3 third,
            string operation)
        {
            if (callback == null)
            {
                return;
            }
            try
            {
                callback(first, second, third);
            }
            catch (Exception exception)
            {
                LogCallbackFailure(operation, exception);
            }
        }

        private static void LogCallbackFailure(string operation, Exception exception)
        {
            DiagnosticLog.Error(
                "FRP_DIRECT " + operation + " callback failed; error=" +
                exception.GetType().Name + ".");
        }

        private sealed class FrpPeer
        {
            internal FrpPeer(NetConnection connection)
            {
                Connection = connection;
            }

            internal readonly NetConnection Connection;
            internal string MachineId = string.Empty;
            internal string Challenge = string.Empty;
            internal string ClientNonce = string.Empty;
            internal DateTime ConnectedAtUtc;
            internal DateTime LastHeartbeatAtUtc;
            internal DateTime NextHeartbeatAtUtc;
            internal bool HandshakeComplete;
            internal bool DisconnectRequested;
            internal int HeartbeatSequence;
        }

        private enum FrpRole
        {
            Host,
            Client
        }

        private enum ControlMessageKind : byte
        {
            ServerHello = 1,
            ClientHello = 2,
            HandshakeResult = 3,
            Heartbeat = 4,
            HeartbeatAck = 5,
            RoomQuery = 6,
            RoomState = 7,
            JoinRequest = 8,
            JoinAccepted = 9,
            JoinRejected = 10,
            GameData = 11,
            LeaveNotice = 12,
            MemberLeft = 13,
            LatencySnapshot = 14
        }

        private sealed class FrpDirectConfiguration
        {
            private FrpDirectConfiguration()
            {
            }

            internal bool Enabled { get; private set; }
            internal FrpRole Role { get; private set; }
            internal int LocalPort { get; private set; }
            internal string ServerAddress { get; private set; }
            internal int ServerPort { get; private set; }
            internal string RoomPassword { get; private set; }
            internal int PlayerLimit { get; private set; }
            internal string ConfigurationKey { get; private set; }

            internal static FrpDirectConfiguration FromSettings(DiagnosticSettings settings)
            {
                var configuration = new FrpDirectConfiguration();
                configuration.Enabled = settings != null && settings.EnableFrpDirect;
                configuration.Role = ParseRole(settings == null ? null : settings.FrpDirectRole);
                configuration.LocalPort = NormalizePort(
                    settings == null ? 27045 : settings.FrpDirectLocalPort);
                var endpoint = settings == null
                    ? string.Empty
                    : (settings.FrpDirectServerEndpoint ?? string.Empty).Trim();
                if (string.IsNullOrEmpty(endpoint) && settings != null &&
                    !string.IsNullOrEmpty((settings.FrpDirectServerAddress ?? string.Empty).Trim()))
                {
                    endpoint = FormatLegacyEndpoint(
                        settings.FrpDirectServerAddress.Trim(),
                        NormalizePort(settings.FrpDirectServerPort));
                }

                string serverAddress;
                int serverPort;
                if (TryParseServerEndpoint(endpoint, out serverAddress, out serverPort))
                {
                    configuration.ServerAddress = serverAddress;
                    configuration.ServerPort = serverPort;
                }
                else
                {
                    configuration.ServerAddress = string.Empty;
                    configuration.ServerPort = 0;
                }
                configuration.RoomPassword = settings == null
                    ? string.Empty
                    : settings.FrpDirectRoomPassword ?? string.Empty;
                configuration.PlayerLimit = NormalizePlayerLimit(
                    settings == null ? 4 : settings.FrpDirectPlayerLimit);
                var roleEndpoint = configuration.Role == FrpRole.Host
                    ? configuration.LocalPort.ToString(CultureInfo.InvariantCulture)
                    : configuration.ServerAddress + "|" +
                      configuration.ServerPort.ToString(CultureInfo.InvariantCulture);
                configuration.ConfigurationKey = HashConfigurationKey(
                    string.Join(
                        "|",
                        new[]
                        {
                            configuration.Enabled.ToString(),
                            RoleName(configuration.Role),
                            roleEndpoint,
                            configuration.RoomPassword
                        }));
                return configuration;
            }

            private static bool TryParseServerEndpoint(
                string value,
                out string address,
                out int port)
            {
                address = string.Empty;
                port = 0;
                value = (value ?? string.Empty).Trim();
                if (value.Length == 0)
                {
                    return false;
                }

                string portText;
                if (value[0] == '[')
                {
                    var closingBracket = value.IndexOf(']');
                    if (closingBracket <= 1 || closingBracket + 1 >= value.Length ||
                        value[closingBracket + 1] != ':')
                    {
                        return false;
                    }
                    address = value.Substring(1, closingBracket - 1).Trim();
                    portText = value.Substring(closingBracket + 2).Trim();
                }
                else
                {
                    var separator = value.LastIndexOf(':');
                    if (separator <= 0 || separator == value.Length - 1 ||
                        value.IndexOf(':') != separator)
                    {
                        return false;
                    }
                    address = value.Substring(0, separator).Trim();
                    portText = value.Substring(separator + 1).Trim();
                }

                int parsedPort;
                if (address.Length == 0 || !int.TryParse(portText, out parsedPort) ||
                    parsedPort < 1 || parsedPort > 65535)
                {
                    address = string.Empty;
                    return false;
                }
                port = parsedPort;
                return true;
            }

            private static string FormatLegacyEndpoint(string address, int port)
            {
                if (address.IndexOf(':') >= 0 && !address.StartsWith("[", StringComparison.Ordinal))
                {
                    address = "[" + address + "]";
                }
                return address + ":" + port;
            }

            private static int NormalizePort(int value)
            {
                return value >= 1 && value <= 65535 ? value : 27045;
            }
        }

        private static int NormalizePlayerLimit(int value)
        {
            return global::System.Math.Max(1, global::System.Math.Min(4, value));
        }
    }
}
