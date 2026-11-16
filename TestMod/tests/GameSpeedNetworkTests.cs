// Real controllers and RPC handlers, with a queued transport and separate peer state.
using System;
using System.Collections.Generic;
using System.Reflection;
using TestMod;
using UnityEngine;

internal static class GameSpeedNetworkTests
{
    private sealed class Peer
    {
        internal PID ID;
        internal TestModSettings Settings = new TestModSettings();
        internal GameSpeedController Controller = new GameSpeedController();
        internal float Scale = 1f;
        internal float FixedDelta = 0.02f;
        internal int Capture;
        internal PauseStatus Pause;
        internal object Host;
        internal object Session = (byte)0;
        internal object NextSync = 0f;

        internal Peer(byte id) { ID = new PID(id); }
        internal void Activate()
        {
            if (_current != null)
            {
                _current.Scale = Time.timeScale;
                _current.FixedDelta = Time.fixedDeltaTime;
                _current.Capture = Time.captureFramerate;
                _current.Pause = PauseController.pauseStatus;
                _current.Host = Field("_speedNetworkHost").GetValue(null);
                _current.Session = Field("_speedNetworkSession").GetValue(null);
                _current.NextSync = Field("_nextSpeedSyncAt").GetValue(null);
            }
            _current = this;
            Plugin.Settings = Settings;
            Plugin.Controller = Controller;
            PID.MyID = ID;
            Time.timeScale = Scale;
            Time.fixedDeltaTime = FixedDelta;
            Time.captureFramerate = Capture;
            PauseController.pauseStatus = Pause;
            Field("_speedNetworkHost").SetValue(null, Host);
            Field("_speedNetworkSession").SetValue(null, Session);
            Field("_nextSpeedSyncAt").SetValue(null, NextSync);
        }
    }

    private sealed class Packet
    {
        internal PID Sender, Target;
        internal Delegate Method;
        internal object[] Args;
    }

    private static Peer _current, _host, _client;
    private static readonly Queue<Packet> Packets = new Queue<Packet>();
    private static int _checks;
    private static FieldInfo Field(string name)
    {
        return typeof(Plugin).GetField(name, BindingFlags.Static | BindingFlags.NonPublic);
    }
    private static void Check(bool value, string message)
    {
        _checks++;
        if (!value) throw new Exception(message);
    }
    private static void Tick(Peer peer, string method)
    {
        peer.Activate();
        typeof(GameSpeedController).GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic)
            .Invoke(peer.Controller, null);
    }
    private static void Command(Peer peer, int command, float value)
    {
        peer.Activate();
        var method = typeof(Plugin).GetMethod("SendGameSpeedCommand", BindingFlags.Static | BindingFlags.NonPublic);
        method.Invoke(null, new object[] { Enum.ToObject(method.GetParameters()[0].ParameterType, command), value });
    }
    internal static void Send(PID target, Delegate method, params object[] args)
    {
        Packets.Enqueue(new Packet { Sender = PID.MyID, Target = target, Method = method, Args = args });
    }
    private static void Drain()
    {
        while (Packets.Count > 0)
        {
            var packet = Packets.Dequeue();
            var target = packet.Target == PID.TargetServer ? _host :
                packet.Sender == _host.ID ? _client : _host;
            target.Activate();
            RPCController.CurrentRPC = new RPCObject { messageInfo = new MessageInfo { Sender = packet.Sender } };
            packet.Method.DynamicInvoke(packet.Args);
            RPCController.CurrentRPC = null;
        }
    }

    internal static void Run()
    {
        Connect.IsOffline = false;
        _host = new Peer(1);
        _client = new Peer(2);
        PID.ServerID = _host.ID;
        Connect.Layer.PlayerIDPairs.Add(_host.ID, new object());
        Connect.Layer.PlayerIDPairs.Add(_client.ID, new object());
        Input.Pressed = Input.Held = KeyCode.None;
        Command(_client, 1, 0.25f);
        Check(_client.Settings.GameSpeed == 1f, "Client waits for host authority");
        Drain();
        Check(_host.Settings.GameSpeed == 0.25f && _client.Settings.GameSpeed == 0.25f,
            "Client slow-motion request reaches both peers");
        Check(Time.timeScale == 0.25f, "Client applies shared time scale");
        Command(_host, 2, 1f);
        Drain();
        Check(_host.Controller.IsFrozen && _client.Controller.IsFrozen, "Host freezes both peers");
        foreach (var type in new[] { typeof(Connect), typeof(RPCController), typeof(RPCBatcher),
            typeof(PingController), typeof(SyncController), typeof(NetworkTimeSync), typeof(SteamManager) })
            Check(FrozenGameFramePatch.IsNetworkService(type), "Keep network callback: " + type.Name);
        Check(!FrozenGameFramePatch.IsNetworkService(typeof(TestVanDammeAnim)), "Keep gameplay frozen");
        var remoteObject = new NetworkObject { IsMine = false };
        var ownedObject = new NetworkObject { IsMine = true };
        Registry.ComponentsToSync.Add(1, remoteObject);
        Registry.ComponentsToSync.Add(2, ownedObject);
        Tick(_client, "LateUpdate");
        Check(remoteObject.SyncCount == 1 && ownedObject.SyncCount == 0,
            "Frozen peer applies remote state without simulating its owned objects");

        _client.Activate();
        RPCController.CurrentRPC = new RPCObject { messageInfo = new MessageInfo { Sender = _client.ID } };
        typeof(Plugin).GetMethod("ReceiveGameSpeedRPC", BindingFlags.Static | BindingFlags.NonPublic)
            .Invoke(null, new object[] { true, 2f, false, false });
        Check(_client.Controller.IsFrozen && _client.Settings.GameSpeed == 0.25f,
            "Non-host cannot publish shared state");
        RPCController.CurrentRPC.messageInfo.Sender = _host.ID;
        typeof(Plugin).GetMethod("ReceiveGameSpeedRPC", BindingFlags.Static | BindingFlags.NonPublic)
            .Invoke(null, new object[] { true, float.NaN, false, false });
        Check(_client.Controller.IsFrozen, "Malformed network speed cannot release the freeze");
        RPCController.CurrentRPC = null;

        // Three reliable packets arrive in one render frame: neither peer may coalesce them.
        for (var i = 0; i < 3; i++) Command(_client, 3, 0f);
        Drain();
        for (var i = 0; i < 4; i++) { Tick(_host, "LateUpdate"); Tick(_client, "LateUpdate"); }
        Check(_host.Controller.SteppedFrames == 3 && _client.Controller.SteppedFrames == 3,
            "Batched network steps advance exactly three frames on both peers");
        Check(_host.Controller.IsFrozen && _client.Controller.IsFrozen, "Both return to freeze");
        Tick(_host, "Update");
        Drain();
        Tick(_client, "LateUpdate");
        Check(_client.Controller.SteppedFrames == 3 && _client.Controller.IsFrozen,
            "Periodic snapshot cannot replay historical steps");
        Command(_client, 4, 0.5f);
        Drain();
        Check(_host.Controller.IsFrozen && _client.Controller.IsFrozen && _client.Settings.GameSpeed == 0.5f,
            "Multiplier slider updates both peers without unpausing");
        Command(_client, 2, 0f);
        Drain();
        Check(!_host.Controller.IsPaused && !_client.Controller.IsPaused && Time.timeScale == 0.5f,
            "Client resume works after a network freeze");

        _client.Settings.FrameStepKey = KeyCode.N;
        _client.Activate();
        Input.Pressed = Input.Held = KeyCode.N;
        Time.realtimeSinceStartup = 100f;
        Tick(_client, "Update");
        Input.Pressed = KeyCode.None;
        Drain();
        Tick(_host, "LateUpdate"); Tick(_host, "LateUpdate");
        Tick(_client, "LateUpdate"); Tick(_client, "LateUpdate");
        Time.realtimeSinceStartup = 100.4f;
        Tick(_client, "Update"); Tick(_client, "LateUpdate");
        Drain();
        Tick(_host, "LateUpdate"); Tick(_host, "LateUpdate");
        Tick(_client, "LateUpdate"); Tick(_client, "LateUpdate");
        Check(_host.Controller.SteppedFrames == 5 && _client.Controller.SteppedFrames == 5,
            "Host acknowledgement preserves the client's held step key");
        Input.Held = KeyCode.None;
        Tick(_client, "Update");
        Command(_client, 0, 0f);
        Drain();
        Check(!_host.Controller.IsPaused && !_client.Controller.IsPaused &&
            !_host.Settings.EnableGameSpeed && !_client.Settings.EnableGameSpeed && Time.timeScale == 1f,
            "Disable from either peer restores both peers");

        Command(_host, 2, 1f); Drain();
        Command(_host, 3, 0f); Drain();
        Connect.SessionID.AsByte++;
        Tick(_host, "Update"); Drain(); Tick(_client, "Update");
        Tick(_host, "LateUpdate"); Tick(_client, "LateUpdate");
        Check(!_host.Controller.IsPaused && !_client.Controller.IsPaused &&
            _host.Controller.SteppedFrames == 5 && _client.Controller.SteppedFrames == 5,
            "New session cancels frozen state and old queued steps");
        _client.Pause = PauseStatus.MenuPause;
        Command(_host, 3, 0f); Drain();
        Tick(_host, "LateUpdate"); Tick(_host, "LateUpdate");
        Tick(_client, "LateUpdate"); Tick(_client, "LateUpdate");
        Check(_host.Controller.SteppedFrames == 6 && _client.Controller.SteppedFrames == 6,
            "Remote native online menu cannot block a shared step");
        _client.Activate(); PauseController.pauseStatus = PauseStatus.UnPaused;
        Command(_host, 2, 1f); Drain();
        PID.ServerID = _client.ID;
        Tick(_client, "Update"); Drain(); Tick(_host, "Update");
        Check(!_host.Controller.IsPaused && !_client.Controller.IsPaused, "Host migration releases the freeze");
        PID.ServerID = _host.ID;
        Command(_host, 2, 1f); Drain();
        _client.Activate(); Connect.IsOffline = true; Tick(_client, "Update");
        Check(!_client.Controller.IsPaused, "Disconnect releases a network freeze");
        Console.WriteLine("PASS: " + _checks + " network game-speed checks");
    }
}

internal class PID
{
    internal byte Value;
    internal PID(byte value) { Value = value; }
    internal static PID MyID, ServerID;
    internal static readonly PID TargetServer = new PID(254), TargetOthers = new PID(253);
    internal static bool IamServer() { return MyID == ServerID; }
}
internal class SID { internal byte AsByte = 1; }
internal class ConnectionLayer { internal Dictionary<PID, object> PlayerIDPairs = new Dictionary<PID, object>(); }
internal class Connect : MonoBehaviour
{
    internal static bool IsOffline = true;
    internal static ConnectionLayer Layer = new ConnectionLayer();
    internal static SID SessionID = new SID();
}
internal class MessageInfo { internal PID Sender; }
internal class RPCObject { internal MessageInfo messageInfo; }
internal class RPCController : MonoBehaviour { internal static RPCObject CurrentRPC; }
internal class RPCBatcher : MonoBehaviour { internal static void FlushQueue() { } }
internal class PingController : MonoBehaviour { }
internal class SyncController : MonoBehaviour { }
internal class NetworkTimeSync : MonoBehaviour { }
internal class SteamManager : MonoBehaviour { }
internal class NetworkObject
{
    internal bool IsMine;
    internal int SyncCount;
    internal void RunSync() { SyncCount++; }
}
internal static class Registry
{
    internal static Dictionary<int, NetworkObject> ComponentsToSync = new Dictionary<int, NetworkObject>();
}
namespace Networking
{
    internal class AllowedRPCAttribute : Attribute { }
    internal delegate void RpcSignature<T1, T2>(T1 a, T2 b);
    internal delegate void RpcSignature<T1, T2, T3, T4>(T1 a, T2 b, T3 c, T4 d);
    internal static class Networking
    {
        internal static void RPC<T1, T2>(PID target, bool immediate, bool ignoreSession,
            RpcSignature<T1, T2> method, T1 a, T2 b)
        { GameSpeedNetworkTests.Send(target, method, a, b); }
        internal static void RPC<T1, T2, T3, T4>(PID target, bool immediate, bool ignoreSession,
            RpcSignature<T1, T2, T3, T4> method, T1 a, T2 b, T3 c, T4 d)
        { GameSpeedNetworkTests.Send(target, method, a, b, c, d); }
    }
}
