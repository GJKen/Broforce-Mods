using System;
using System.Globalization;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using Lidgren.Network;

namespace BroforceOnlineDiagnostics
{
    internal sealed class FrpDirectTransport : IDisposable
    {
        private const string ApplicationIdentifier = "BroforceOnlineDiagnostics.FrpDirect.v1";
        private const string ProtocolMagic = "BFOD-FRP";
        private const int ProtocolVersion = 2;
        private const int HeartbeatIntervalSeconds = 5;
        private const int HandshakeTimeoutSeconds = 15;
        private const int HeartbeatTimeoutSeconds = 60;
        private const int MainThreadStallThresholdSeconds = 10;
        private const int ReconnectDelaySeconds = 5;
        private const int MaxRoomStateBytes = 32768;
        private const int MaxGameDataBytes = 2097152;

        private NetPeer _peer;
        private NetPeer _retiringPeer;
        private NetConnection _connection;
        private IPEndPoint _remoteEndpoint;
        private FrpRole _role;
        private string _roomPassword = string.Empty;
        private string _configurationKey = string.Empty;
        private string _challenge = string.Empty;
        private string _clientNonce = string.Empty;
        private readonly string _localMachineId;
        private string _remoteMachineId = string.Empty;
        private DateTime _connectedAtUtc;
        private DateTime _lastHeartbeatAtUtc;
        private DateTime _nextHeartbeatAtUtc;
        private DateTime _nextConnectAtUtc;
        private DateTime _lastUpdateAtUtc;
        private bool _handshakeComplete;
        private bool _fatalConnectionError;
        private bool _disconnectRequested;
        private bool _disposed;
        private int _heartbeatSequence;
        private int _suppressedInvalidPackets;
        private DateTime _nextInvalidPacketLogAtUtc;
        private FrpDirectConfiguration _pendingConfiguration;

        internal string Status { get; private set; }
        internal bool IsEnabled { get; private set; }
        internal bool IsHost { get { return _role == FrpRole.Host; } }
        internal bool IsHandshakeComplete { get { return _handshakeComplete; } }
        internal string LocalMachineId { get { return _localMachineId; } }
        internal string RemoteMachineId { get { return _remoteMachineId; } }

        internal event Action HandshakeCompleted;
        internal event Action ConfigurationChanging;
        internal event Action RemoteDisconnected;
        internal event Action RoomQueryReceived;
        internal event Action<bool, string> RoomStateReceived;
        internal event Action JoinRequestReceived;
        internal event Action<string> JoinAcceptedReceived;
        internal event Action<string> JoinRejectedReceived;
        internal event Action<byte[]> GameDataReceived;
        internal event Action LeaveNoticeReceived;

        internal FrpDirectTransport()
        {
            _localMachineId = Guid.NewGuid().ToString("N");
            Status = "Disabled";
        }

        internal bool RequestRoomState()
        {
            return SendEmptyControlMessage(ControlMessageKind.RoomQuery);
        }

        internal bool SendRoomState(bool hasRoom, string encodedRoom)
        {
            if (!CanSendApplicationMessage())
            {
                return false;
            }

            encodedRoom = hasRoom ? encodedRoom ?? string.Empty : string.Empty;
            if (Encoding.UTF8.GetByteCount(encodedRoom) > MaxRoomStateBytes)
            {
                DiagnosticLog.Warning("FRP_DIRECT room state was not sent because it is too large.");
                return false;
            }

            var outgoing = CreateControlMessage(ControlMessageKind.RoomState);
            outgoing.Write(hasRoom);
            outgoing.Write(encodedRoom);
            SendReliable(outgoing);
            return true;
        }

        internal bool RequestJoin()
        {
            return SendEmptyControlMessage(ControlMessageKind.JoinRequest);
        }

        internal bool AcceptJoin(string encodedRoom)
        {
            if (!CanSendApplicationMessage())
            {
                return false;
            }

            encodedRoom = encodedRoom ?? string.Empty;
            if (Encoding.UTF8.GetByteCount(encodedRoom) > MaxRoomStateBytes)
            {
                return false;
            }

            var outgoing = CreateControlMessage(ControlMessageKind.JoinAccepted);
            outgoing.Write(encodedRoom);
            SendReliable(outgoing);
            return true;
        }

        internal bool RejectJoin(string reason)
        {
            if (!CanSendApplicationMessage())
            {
                return false;
            }

            var outgoing = CreateControlMessage(ControlMessageKind.JoinRejected);
            outgoing.Write(SafeReason(reason));
            SendReliable(outgoing);
            return true;
        }

        internal bool SendGameData(byte[] bytes)
        {
            if (!CanSendApplicationMessage() || bytes == null || bytes.Length == 0 ||
                bytes.Length > MaxGameDataBytes)
            {
                return false;
            }

            var outgoing = CreateControlMessage(ControlMessageKind.GameData);
            outgoing.Write(bytes.Length);
            outgoing.Write(bytes);
            SendReliable(outgoing);
            return true;
        }

        internal bool SendLeaveNotice()
        {
            return SendEmptyControlMessage(ControlMessageKind.LeaveNotice);
        }

        internal void Apply(DiagnosticSettings settings, bool forceRestart)
        {
            if (_disposed)
            {
                return;
            }

            var configuration = FrpDirectConfiguration.FromSettings(settings);
            var configurationKey = configuration.ConfigurationKey;
            if (!forceRestart && string.Equals(_configurationKey, configurationKey, StringComparison.Ordinal))
            {
                return;
            }

            Raise(ConfigurationChanging, "configuration change");
            Stop("configuration changed");
            _configurationKey = configurationKey;
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
            _fatalConnectionError = false;

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
                        DiagnosticLog.Warning("FRP_DIRECT client could not resolve the configured server address.");
                        return;
                    }
                }

                var configuration = new NetPeerConfiguration(ApplicationIdentifier);
                configuration.AcceptIncomingConnections = _role == FrpRole.Host;
                configuration.MaximumConnections = 1;
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
                        "; localUdpPort=" + _peer.Port +
                        "; buildHash=" + BuildMetadata.BuildHash +
                        "; passwordConfigured=" + (!string.IsNullOrEmpty(_roomPassword)) + ".");
                }
                else
                {
                    Status = "Connecting";
                    DiagnosticLog.Info(
                        "FRP_DIRECT client started; protocol=" + ProtocolVersion +
                        "; remoteUdpPort=" + settings.ServerPort +
                        "; buildHash=" + BuildMetadata.BuildHash +
                        "; passwordConfigured=" + (!string.IsNullOrEmpty(_roomPassword)) + ".");
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
            var notifyDisconnect = _handshakeComplete;
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
            _connection = null;
            _remoteEndpoint = null;
            _challenge = string.Empty;
            _clientNonce = string.Empty;
            _remoteMachineId = string.Empty;
            _roomPassword = string.Empty;
            _handshakeComplete = false;
            _fatalConnectionError = false;
            _disconnectRequested = false;
            _heartbeatSequence = 0;
            _suppressedInvalidPackets = 0;
            _nextInvalidPacketLogAtUtc = DateTime.MinValue;
            if (notifyDisconnect)
            {
                Raise(RemoteDisconnected, "remote disconnect");
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
            else if (status == NetConnectionStatus.Disconnected && connection == _connection)
            {
                HandleDisconnected();
            }
        }

        private void HandleConnected(NetConnection connection)
        {
            if (connection == null)
            {
                return;
            }

            if (_connection != null && _connection != connection)
            {
                connection.Disconnect("FRP Direct room is full");
                return;
            }

            _connection = connection;
            _connectedAtUtc = DateTime.UtcNow;
            _lastHeartbeatAtUtc = _connectedAtUtc;
            _lastUpdateAtUtc = _connectedAtUtc;
            _handshakeComplete = false;
            _disconnectRequested = false;
            _heartbeatSequence = 0;
            if (_role == FrpRole.Host)
            {
                _challenge = Guid.NewGuid().ToString("N");
                Status = "Peer connected; authenticating";
                DiagnosticLog.Info("FRP_DIRECT UDP peer connected to host; starting application handshake.");
                SendServerHello();
            }
            else
            {
                Status = "Connected; waiting for host handshake";
                DiagnosticLog.Info("FRP_DIRECT UDP connection established; waiting for host handshake.");
            }
        }

        private void HandleDisconnected()
        {
            var wasReady = _handshakeComplete;
            _connection = null;
            _challenge = string.Empty;
            _clientNonce = string.Empty;
            _remoteMachineId = string.Empty;
            _handshakeComplete = false;
            _disconnectRequested = false;
            if (_role == FrpRole.Host)
            {
                Status = "Listening on UDP " + (_peer == null ? 0 : _peer.Port);
            }
            else if (_fatalConnectionError)
            {
                // Keep the rejection visible until the user changes or reapplies the settings.
            }
            else
            {
                Status = "Disconnected; retrying";
                _nextConnectAtUtc = DateTime.UtcNow.AddSeconds(ReconnectDelaySeconds);
            }

            DiagnosticLog.Warning(
                "FRP_DIRECT transport disconnected; role=" + RoleName(_role) +
                "; handshakeCompleted=" + wasReady +
                "; willRetry=" + (_role == FrpRole.Client && !_fatalConnectionError) + ".");
            if (wasReady)
            {
                Raise(RemoteDisconnected, "remote disconnect");
            }
        }

        private void HandleControlMessage(NetIncomingMessage message)
        {
            if (message.SenderConnection == null || message.SenderConnection != _connection)
            {
                return;
            }

            var magic = message.ReadString();
            if (!string.Equals(magic, ProtocolMagic, StringComparison.Ordinal))
            {
                return;
            }

            var kind = (ControlMessageKind)message.ReadByte();
            switch (kind)
            {
                case ControlMessageKind.ServerHello:
                    if (_role == FrpRole.Client)
                    {
                        ReceiveServerHello(message);
                    }
                    break;
                case ControlMessageKind.ClientHello:
                    if (_role == FrpRole.Host)
                    {
                        ReceiveClientHello(message);
                    }
                    break;
                case ControlMessageKind.HandshakeResult:
                    if (_role == FrpRole.Client)
                    {
                        ReceiveHandshakeResult(message);
                    }
                    break;
                case ControlMessageKind.Heartbeat:
                    if (_role == FrpRole.Host && _handshakeComplete)
                    {
                        ReceiveHeartbeat(message);
                    }
                    break;
                case ControlMessageKind.HeartbeatAck:
                    if (_role == FrpRole.Client && _handshakeComplete)
                    {
                        ReceiveHeartbeatAck(message);
                    }
                    break;
                case ControlMessageKind.RoomQuery:
                    if (_role == FrpRole.Host && _handshakeComplete)
                    {
                        Raise(RoomQueryReceived, "room query");
                    }
                    break;
                case ControlMessageKind.RoomState:
                    if (_role == FrpRole.Client && _handshakeComplete)
                    {
                        ReceiveRoomState(message);
                    }
                    break;
                case ControlMessageKind.JoinRequest:
                    if (_role == FrpRole.Host && _handshakeComplete)
                    {
                        Raise(JoinRequestReceived, "join request");
                    }
                    break;
                case ControlMessageKind.JoinAccepted:
                    if (_role == FrpRole.Client && _handshakeComplete)
                    {
                        ReceiveJoinAccepted(message);
                    }
                    break;
                case ControlMessageKind.JoinRejected:
                    if (_role == FrpRole.Client && _handshakeComplete)
                    {
                        Raise(JoinRejectedReceived, SafeReason(message.ReadString()), "join rejected");
                    }
                    break;
                case ControlMessageKind.GameData:
                    if (_handshakeComplete)
                    {
                        ReceiveGameData(message);
                    }
                    break;
                case ControlMessageKind.LeaveNotice:
                    if (_handshakeComplete)
                    {
                        Raise(LeaveNoticeReceived, "leave notice");
                    }
                    break;
            }
        }

        private void SendServerHello()
        {
            var outgoing = CreateControlMessage(ControlMessageKind.ServerHello);
            outgoing.Write(ProtocolVersion);
            outgoing.Write(BuildMetadata.BuildHash);
            outgoing.Write(_challenge);
            outgoing.Write(_localMachineId);
            SendReliable(outgoing);
        }

        private void ReceiveServerHello(NetIncomingMessage message)
        {
            var serverProtocol = message.ReadInt32();
            var serverBuildHash = message.ReadString();
            var challenge = message.ReadString();
            _remoteMachineId = NormalizeMachineId(message.ReadString());
            _clientNonce = Guid.NewGuid().ToString("N");

            var protocolMatches = serverProtocol == ProtocolVersion;
            var buildMatches = BuildHashesMatch(serverBuildHash, BuildMetadata.BuildHash);
            DiagnosticLog.Info(
                "FRP_DIRECT received host handshake; localProtocol=" + ProtocolVersion +
                "; remoteProtocol=" + serverProtocol +
                "; protocolMatch=" + protocolMatches +
                "; localBuildHash=" + BuildMetadata.BuildHash +
                "; remoteBuildHash=" + SafeBuildHash(serverBuildHash) +
                "; buildHashMatch=" + buildMatches + ".");

            var proof = ComputePasswordProof(
                _roomPassword,
                challenge,
                _clientNonce,
                _localMachineId,
                _remoteMachineId,
                ProtocolVersion,
                BuildMetadata.BuildHash,
                serverProtocol,
                serverBuildHash);
            var outgoing = CreateControlMessage(ControlMessageKind.ClientHello);
            outgoing.Write(ProtocolVersion);
            outgoing.Write(BuildMetadata.BuildHash);
            outgoing.Write(_clientNonce);
            outgoing.Write(proof);
            outgoing.Write(_localMachineId);
            SendReliable(outgoing);
            Status = protocolMatches && buildMatches
                ? "Authenticating"
                : "Version mismatch; waiting for rejection";
        }

        private void ReceiveClientHello(NetIncomingMessage message)
        {
            var clientProtocol = message.ReadInt32();
            var clientBuildHash = message.ReadString();
            var clientNonce = message.ReadString();
            var suppliedProof = message.ReadString();
            var clientMachineId = NormalizeMachineId(message.ReadString());
            var protocolMatches = clientProtocol == ProtocolVersion;
            var buildMatches = BuildHashesMatch(clientBuildHash, BuildMetadata.BuildHash);
            var expectedProof = ComputePasswordProof(
                _roomPassword,
                _challenge,
                clientNonce,
                clientMachineId,
                _localMachineId,
                clientProtocol,
                clientBuildHash,
                ProtocolVersion,
                BuildMetadata.BuildHash);
            var authenticationMatches = FixedTimeEquals(suppliedProof, expectedProof);

            DiagnosticLog.Info(
                "FRP_DIRECT received client handshake; localProtocol=" + ProtocolVersion +
                "; remoteProtocol=" + clientProtocol +
                "; protocolMatch=" + protocolMatches +
                "; localBuildHash=" + BuildMetadata.BuildHash +
                "; remoteBuildHash=" + SafeBuildHash(clientBuildHash) +
                "; buildHashMatch=" + buildMatches +
                "; authenticationMatch=" + authenticationMatches + ".");

            var accepted = protocolMatches && buildMatches && authenticationMatches &&
                           !string.IsNullOrEmpty(clientMachineId);
            var reason = accepted
                ? "accepted"
                : (!protocolMatches
                    ? "protocol_mismatch"
                    : (!buildMatches
                        ? "build_hash_mismatch"
                        : (!authenticationMatches ? "authentication_failed" : "invalid_machine_id")));
            SendHandshakeResult(accepted, reason);
            if (accepted)
            {
                _remoteMachineId = clientMachineId;
                _handshakeComplete = true;
                _lastHeartbeatAtUtc = DateTime.UtcNow;
                Status = "Handshake complete; heartbeat active";
                DiagnosticLog.Info("FRP_DIRECT host accepted the client handshake; heartbeat monitoring started.");
                Raise(HandshakeCompleted, "handshake completion");
            }
            else
            {
                Status = "Client rejected: " + reason;
                DiagnosticLog.Warning("FRP_DIRECT host rejected the client handshake; reason=" + reason + ".");
            }
        }

        private void SendHandshakeResult(bool accepted, string reason)
        {
            var outgoing = CreateControlMessage(ControlMessageKind.HandshakeResult);
            outgoing.Write(accepted);
            outgoing.Write(reason);
            outgoing.Write(ProtocolVersion);
            outgoing.Write(BuildMetadata.BuildHash);
            SendReliable(outgoing);
        }

        private void ReceiveHandshakeResult(NetIncomingMessage message)
        {
            var accepted = message.ReadBoolean();
            var reason = message.ReadString();
            var serverProtocol = message.ReadInt32();
            var serverBuildHash = message.ReadString();
            if (!accepted)
            {
                _fatalConnectionError = true;
                Status = "Handshake rejected: " + SafeReason(reason);
                DiagnosticLog.Warning(
                    "FRP_DIRECT client handshake rejected; reason=" + SafeReason(reason) +
                    "; localBuildHash=" + BuildMetadata.BuildHash +
                    "; remoteBuildHash=" + SafeBuildHash(serverBuildHash) + ".");
                if (_connection != null)
                {
                    DisconnectCurrent("FRP Direct handshake rejected");
                }
                return;
            }

            if (serverProtocol != ProtocolVersion || !BuildHashesMatch(serverBuildHash, BuildMetadata.BuildHash))
            {
                _fatalConnectionError = true;
                Status = "Invalid host handshake result";
                DiagnosticLog.Error("FRP_DIRECT host accepted a handshake with inconsistent version metadata.");
                if (_connection != null)
                {
                    DisconnectCurrent("FRP Direct inconsistent handshake");
                }
                return;
            }

            _handshakeComplete = true;
            _lastHeartbeatAtUtc = DateTime.UtcNow;
            _nextHeartbeatAtUtc = DateTime.UtcNow;
            Status = "Handshake complete; heartbeat active";
            DiagnosticLog.Info("FRP_DIRECT client handshake accepted; heartbeat monitoring started.");
            if (string.IsNullOrEmpty(_remoteMachineId))
            {
                _fatalConnectionError = true;
                DisconnectCurrent("FRP Direct invalid host identity");
                return;
            }
            Raise(HandshakeCompleted, "handshake completion");
        }

        private void ReceiveRoomState(NetIncomingMessage message)
        {
            var hasRoom = message.ReadBoolean();
            var encodedRoom = message.ReadString();
            if (Encoding.UTF8.GetByteCount(encodedRoom ?? string.Empty) > MaxRoomStateBytes)
            {
                throw new InvalidOperationException("Room state exceeds the protocol limit.");
            }
            Raise(RoomStateReceived, hasRoom, encodedRoom ?? string.Empty, "room state");
        }

        private void ReceiveGameData(NetIncomingMessage message)
        {
            var length = message.ReadInt32();
            if (length <= 0 || length > MaxGameDataBytes)
            {
                throw new InvalidOperationException("Game data length is invalid.");
            }
            Raise(GameDataReceived, message.ReadBytes(length), "game data");
        }

        private void ReceiveJoinAccepted(NetIncomingMessage message)
        {
            var encodedRoom = message.ReadString();
            if (Encoding.UTF8.GetByteCount(encodedRoom ?? string.Empty) > MaxRoomStateBytes)
            {
                throw new InvalidOperationException("Join room state exceeds the protocol limit.");
            }
            Raise(JoinAcceptedReceived, encodedRoom ?? string.Empty, "join accepted");
        }

        private void SendHeartbeat(DateTime now)
        {
            _heartbeatSequence++;
            var outgoing = CreateControlMessage(ControlMessageKind.Heartbeat);
            outgoing.Write(_heartbeatSequence);
            SendReliable(outgoing);
            _nextHeartbeatAtUtc = now.AddSeconds(HeartbeatIntervalSeconds);
        }

        private void ReceiveHeartbeat(NetIncomingMessage message)
        {
            var sequence = message.ReadInt32();
            _lastHeartbeatAtUtc = DateTime.UtcNow;
            var outgoing = CreateControlMessage(ControlMessageKind.HeartbeatAck);
            outgoing.Write(sequence);
            SendReliable(outgoing);
        }

        private void ReceiveHeartbeatAck(NetIncomingMessage message)
        {
            var sequence = message.ReadInt32();
            if (sequence <= 0 || sequence > _heartbeatSequence)
            {
                return;
            }

            _lastHeartbeatAtUtc = DateTime.UtcNow;
            Status = "Connected; heartbeat " + sequence;
        }

        private void UpdateTimers(DateTime now)
        {
            if (_role == FrpRole.Client && _connection == null && !_fatalConnectionError &&
                _remoteEndpoint != null && now >= _nextConnectAtUtc)
            {
                ConnectClient();
                return;
            }

            if (_connection == null)
            {
                return;
            }

            if (_disconnectRequested)
            {
                return;
            }

            if (!_handshakeComplete)
            {
                if ((now - _connectedAtUtc).TotalSeconds >= HandshakeTimeoutSeconds)
                {
                    DiagnosticLog.Warning("FRP_DIRECT application handshake timed out.");
                    DisconnectCurrent("FRP Direct handshake timeout");
                }
                return;
            }

            if (_role == FrpRole.Client && now >= _nextHeartbeatAtUtc)
            {
                SendHeartbeat(now);
            }

            if ((now - _lastHeartbeatAtUtc).TotalSeconds >= HeartbeatTimeoutSeconds)
            {
                DiagnosticLog.Warning(
                    "FRP_DIRECT heartbeat timed out; role=" + RoleName(_role) + ".");
                Status = "Heartbeat timed out";
                DisconnectCurrent("FRP Direct heartbeat timeout");
            }
        }

        private void HandleMainThreadStall(DateTime now)
        {
            if (!_handshakeComplete || _lastUpdateAtUtc == DateTime.MinValue)
            {
                return;
            }

            var stalledSeconds = (now - _lastUpdateAtUtc).TotalSeconds;
            if (stalledSeconds < MainThreadStallThresholdSeconds)
            {
                return;
            }

            // Unity does not update this component while a blocking scene load is in progress.
            // Resume the liveness window instead of treating that local pause as peer failure.
            _lastHeartbeatAtUtc = now;
            if (_role == FrpRole.Client)
            {
                _nextHeartbeatAtUtc = now;
            }
            DiagnosticLog.Warning(
                "FRP_DIRECT main-thread update paused for " +
                ((int)stalledSeconds) +
                " seconds; heartbeat timeout window resumed after scene loading.");
        }

        private void DisconnectCurrent(string reason)
        {
            if (_connection == null || _disconnectRequested)
            {
                return;
            }

            _disconnectRequested = true;
            _connection.Disconnect(reason);
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

        private void ConnectClient()
        {
            if (_peer == null || _remoteEndpoint == null || _fatalConnectionError)
            {
                return;
            }

            try
            {
                _connection = _peer.Connect(_remoteEndpoint);
                _connectedAtUtc = DateTime.UtcNow;
                _nextConnectAtUtc = _connectedAtUtc.AddSeconds(ReconnectDelaySeconds);
                Status = "Connecting";
                DiagnosticLog.Info("FRP_DIRECT client connection attempt started.");
            }
            catch (Exception exception)
            {
                _connection = null;
                _nextConnectAtUtc = DateTime.UtcNow.AddSeconds(ReconnectDelaySeconds);
                Status = "Connect failed; retrying";
                DiagnosticLog.Warning(
                    "FRP_DIRECT client connection attempt failed; error=" +
                    exception.GetType().Name + ".");
            }
        }

        private NetOutgoingMessage CreateControlMessage(ControlMessageKind kind)
        {
            var outgoing = _peer.CreateMessage();
            outgoing.Write(ProtocolMagic);
            outgoing.Write((byte)kind);
            return outgoing;
        }

        private bool SendEmptyControlMessage(ControlMessageKind kind)
        {
            if (!CanSendApplicationMessage())
            {
                return false;
            }

            SendReliable(CreateControlMessage(kind));
            return true;
        }

        private bool CanSendApplicationMessage()
        {
            return _peer != null && _connection != null && _handshakeComplete &&
                   !_disconnectRequested;
        }

        private void SendReliable(NetOutgoingMessage message)
        {
            if (_connection == null)
            {
                return;
            }

            _connection.SendMessage(message, NetDeliveryMethod.ReliableOrdered, 0);
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
            return !string.IsNullOrEmpty(left) &&
                   !string.IsNullOrEmpty(right) &&
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
            return string.Equals((value ?? string.Empty).Trim(), "client", StringComparison.OrdinalIgnoreCase)
                ? FrpRole.Client
                : FrpRole.Host;
        }

        private static string RoleName(FrpRole role)
        {
            return role == FrpRole.Client ? "client" : "host";
        }

        private static string SafeBuildHash(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return "missing";
            }

            var builder = new StringBuilder();
            for (var index = 0; index < value.Length && index < 128; index++)
            {
                var current = value[index];
                if ((current >= 'a' && current <= 'z') ||
                    (current >= 'A' && current <= 'Z') ||
                    (current >= '0' && current <= '9') ||
                    current == '-' || current == '_')
                {
                    builder.Append(current);
                }
            }
            return builder.Length == 0 ? "invalid" : builder.ToString();
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

        private static void LogCallbackFailure(string operation, Exception exception)
        {
            DiagnosticLog.Error(
                "FRP_DIRECT " + operation + " callback failed; error=" +
                exception.GetType().Name + ".");
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
            LeaveNotice = 12
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
            internal string ConfigurationKey { get; private set; }

            internal static FrpDirectConfiguration FromSettings(DiagnosticSettings settings)
            {
                var configuration = new FrpDirectConfiguration();
                configuration.Enabled = settings != null && settings.EnableFrpDirectPrototype;
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
                configuration.ConfigurationKey = HashConfigurationKey(
                    string.Join(
                        "|",
                        new[]
                        {
                            configuration.Enabled.ToString(),
                            RoleName(configuration.Role),
                            configuration.LocalPort.ToString(CultureInfo.InvariantCulture),
                            configuration.ServerAddress,
                            configuration.ServerPort.ToString(CultureInfo.InvariantCulture),
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
    }
}
