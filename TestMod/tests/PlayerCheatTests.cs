using System;
using System.Collections.Generic;
using System.Reflection;
using TestMod;

internal static class PlayerCheatTests
{
    private sealed class Peer
    {
        internal PID ID;
        internal Player[] Players = new Player[4];
        internal object[] State = { 0, 0, 0, null, 0f };
        internal object Deaths = Array.CreateInstance(typeof(Plugin).GetNestedType("PlayerDeath", BindingFlags.NonPublic), 4);
        internal Peer(byte id) { ID = new PID(id); }
        internal void Activate()
        {
            if (_current != null)
            {
                for (var i = 0; i < Fields.Length; i++) _current.State[i] = Field(Fields[i]).GetValue(null);
                Array.Copy((Array)Field("PlayerDeaths").GetValue(null), (Array)_current.Deaths, 4);
            }
            _current = this;
            PID.MyID = ID;
            HeroController.players = Players;
            Array.Copy((Array)Deaths, (Array)Field("PlayerDeaths").GetValue(null), 4);
            for (var i = 0; i < Fields.Length; i++) Field(Fields[i]).SetValue(null, State[i]);
        }
    }

    private sealed class Packet
    {
        internal PID Sender, Target;
        internal Delegate Method;
        internal object[] Args;
    }

    private static readonly string[] Fields = {
        "_invinciblePlayers", "_infiniteSpecialPlayers", "_autoRevivePlayers", "_cheatNetworkHost", "_nextCheatSyncAt" };
    private static readonly Queue<Packet> Packets = new Queue<Packet>();
    private static Peer _current, _host, _client;
    private static int _checks;
    private static FieldInfo Field(string name)
    {
        return typeof(Plugin).GetField(name, BindingFlags.Static | BindingFlags.NonPublic);
    }
    private static void Call(string name, params object[] args)
    {
        typeof(Plugin).GetMethod(name, BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, args);
    }
    private static bool Prefix(Type patch, params object[] args)
    {
        return (bool)patch.GetMethod("Prefix", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, args);
    }
    private static void Check(bool value, string message)
    {
        _checks++;
        if (!value) throw new Exception(message);
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
            var peer = packet.Target == PID.TargetServer || packet.Target == _host.ID ? _host :
                packet.Target == _client.ID ? _client : packet.Sender == _host.ID ? _client : _host;
            peer.Activate();
            RPCController.CurrentRPC = new RPCObject { messageInfo = new MessageInfo { Sender = packet.Sender } };
            packet.Method.DynamicInvoke(packet.Args);
            RPCController.CurrentRPC = null;
        }
    }
    private static Player NewPlayer(int index, bool mine, int ammo)
    {
        return new Player { playerNum = index, IsMine = mine, Lives = 2,
            character = new BroBase { playerNum = index, IsMine = mine, SpecialAmmo = ammo } };
    }

    private static void CheckRevival()
    {
        Connect.IsOffline = true;
        _host.Activate();
        Call("ResetPlayerCheats");
        var player = _host.Players[0];
        var hero = player.character;
        hero.X = 120f;
        hero.Y = 64f;
        hero.heroType = HeroType.Brommando;
        Check(!Plugin.RevivePlayer(0), "Manual revival rejects living players");
        Plugin.CapturePlayerDeath(hero);
        hero.health = 0;
        hero.Y = 10f;
        Plugin.CapturePlayerDeath(hero);
        Check(Plugin.CanRevivePlayer(0), "Death enables manual revival");
        Check(Plugin.RevivePlayer(0) && Plugin.RevivePlayer(0), "Duplicate requests can be queued");
        Plugin.UpdatePlayerCheats();
        Check(player.SpawnCount == 1 && player.character.X == 120f && player.character.Y == 64f,
            "Manual revival spawns once at the first death position, not the moving corpse");
        Check(player.character.heroType == HeroType.Brommando, "Revive the dead hero type");
        Plugin.UpdatePlayerCheats();
        Check(player.SpawnCount == 1 && !Plugin.CanRevivePlayer(0), "A living replacement clears its death record");
        Plugin.SetPlayerAutoRevive(0, true);
        Check(!Prefix(typeof(InfinitePlayerLivesPatch), player), "Auto revive preserves lives");
        Check(Prefix(typeof(InfinitePlayerHealthPatch), player.character, DamageType.Normal),
            "Auto revive allows damage independently of invincibility");
        hero = player.character;
        Plugin.CapturePlayerDeath(hero);
        hero.health = 0;
        hero.destroyed = true;
        player.character = null;
        player.Lives = 0;
        Plugin.UpdatePlayerCheats();
        Check(player.SpawnCount == 2 && player.Lives == 1 && player.character.IsAlive(),
            "Auto revive handles a destroyed corpse and zero lives in the next update");
        Plugin.SetPlayerAutoRevive(0, false);
        Plugin.UpdatePlayerCheats();
        hero = player.character;
        Plugin.CapturePlayerDeath(hero);
        hero.health = 0;
        Plugin.UpdatePlayerCheats();
        Check(player.SpawnCount == 2 && Plugin.CanRevivePlayer(0), "Disabling auto revive leaves manual revival available");
        GameModeController.LevelFinished = true;
        Check(!Plugin.RevivePlayer(0), "Do not revive in a completed level");
        GameModeController.LevelFinished = false;
        Map.Instance = new Map();
        Check(!Plugin.CanRevivePlayer(0), "Do not use a death position from a previous map");
        Call("ResetPlayerRevival");
        player.character = null;
        Check(!Plugin.RevivePlayer(0), "Do not revive without a recorded character");
        player.character = NewPlayer(0, true, 0).character;
        Plugin.CapturePlayerDeath(player.character);
        player.character.health = 0;
        player.awaitingHeroTypeFromServer = true;
        Check(!Plugin.RevivePlayer(0), "Native pending respawn cannot be duplicated");
        player.awaitingHeroTypeFromServer = false;
        player.firstDeployment = true;
        Check(!Plugin.RevivePlayer(0), "Do not bypass the initial deployment");
        player.firstDeployment = false;
        player.character.health = 1;
        Connect.IsOffline = false;
        PID.ServerID = _host.ID;
        Plugin.UpdatePlayerCheats();
        Drain();
        _client.Activate();
        Plugin.UpdatePlayerCheats();
        var remotePlayer = _client.Players[1];
        remotePlayer.character.X = 222f;
        remotePlayer.character.Y = 88f;
        Plugin.CapturePlayerDeath(remotePlayer.character);
        remotePlayer.character.health = 0;
        _host.Activate();
        Plugin.CapturePlayerDeath(_host.Players[1].character);
        _host.Players[1].character.health = 0;
        Check(Plugin.RevivePlayer(1), "Host can request remote player's revival");
        Drain();
        _client.Activate();
        Plugin.UpdatePlayerCheats();
        Check(remotePlayer.SpawnCount == 1 && remotePlayer.character.X == 222f && remotePlayer.character.Y == 88f,
            "Only the remote owner spawns at its authoritative death position");
        _host.Activate();
        Check(_host.Players[1].SpawnCount == 0, "Host does not spawn a remote-owned hero");
        Plugin.CapturePlayerDeath(player.character);
        player.character.health = 0;
        _client.Activate();
        Plugin.CapturePlayerDeath(_client.Players[0].character);
        _client.Players[0].character.health = 0;
        Check(Plugin.RevivePlayer(0), "Client can request host player's revival");
        Drain();
        _host.Activate();
        Plugin.UpdatePlayerCheats();
        Check(player.SpawnCount == 3 && player.character.IsAlive(), "Client-to-host manual revival succeeds");
        Drain();
        _client.Activate();
        Plugin.SetPlayerAutoRevive(1, true);
        Drain();
        _host.Activate();
        Check(Plugin.IsPlayerAutoReviveEnabled(1), "Auto revive toggle synchronized to host");
        _client.Activate();
        Plugin.UpdatePlayerCheats();
        Plugin.CapturePlayerDeath(remotePlayer.character);
        remotePlayer.character.health = 0;
        Plugin.UpdatePlayerCheats();
        Check(remotePlayer.SpawnCount == 2 && remotePlayer.character.IsAlive(), "Remote owner auto revives after synchronized enable");
        Call("StopPlayerCheats");
        Drain();
        _host.Activate();
        Check(!Plugin.IsPlayerAutoReviveEnabled(1), "Disable clears auto revive on every peer");
    }

    private static void Main()
    {
        Plugin._behaviour = new object();
        _host = new Peer(1);
        _client = new Peer(2);
        HeroController.PIDS = new[] { _host.ID, _client.ID, null, null };
        PID.ServerID = _host.ID;
        Connect.Layer.PlayerIDPairs.Add(_host.ID, new object());
        Connect.Layer.PlayerIDPairs.Add(_client.ID, new object());
        _host.Players[0] = NewPlayer(0, true, 0);
        _host.Players[1] = NewPlayer(1, false, 0);
        _client.Players[0] = NewPlayer(0, false, 0);
        _client.Players[1] = NewPlayer(1, true, 0);
        _host.Activate();
        Plugin.UpdatePlayerCheats();
        Check(_host.Players[0].character.SpecialAmmo == 0, "Cheats default off");
        Plugin.SetPlayerInfiniteLives(0, true);
        Check(Plugin.IsPlayerInfiniteLivesEnabled(0) && !Plugin.IsPlayerInfiniteLivesEnabled(1), "Offline selection");
        Check(!Prefix(typeof(InfinitePlayerLivesPatch), _host.Players[0]), "Selected player keeps lives");
        Check(Prefix(typeof(InfinitePlayerLivesPatch), _host.Players[1]), "Other player still spends lives");
        var hero = _host.Players[0].character;
        Check(!Prefix(typeof(InfinitePlayerHealthPatch), hero, DamageType.Normal), "Block normal damage");
        Check(!Prefix(typeof(InfinitePlayerGibPatch), hero, DamageType.Crush), "Block direct crushing gib");
        Check(Prefix(typeof(InfinitePlayerHealthPatch), hero, DamageType.OutOfBounds) &&
            Prefix(typeof(InfinitePlayerGibPatch), hero, DamageType.OutOfBounds), "Allow out-of-bounds respawn");
        hero.health = 0;
        Check(Prefix(typeof(InfinitePlayerGibPatch), hero, DamageType.Normal), "Do not interrupt existing deaths");
        hero.health = 1;
        Check(!Plugin.ProtectPlayerFromDamage(new TestVanDammeAnim { playerNum = -1 }), "Exclude enemies");
        Plugin.SetPlayerInfiniteSpecial(0, true);
        Plugin.UpdatePlayerCheats();
        Check(hero.SpecialAmmo == 3 && _host.Players[1].character.SpecialAmmo == 0, "Refill only selected owned hero");
        var bro = (BroBase)hero;
        bro.pockettedSpecialAmmo.Add(1);
        hero.SpecialAmmo = 1;
        Plugin.UpdatePlayerCheats();
        Check(hero.SpecialAmmo == 1 && !Prefix(typeof(InfinitePocketedSpecialPatch), bro), "Preserve pocketed ammo without refill spam");
        bro.pockettedSpecialAmmo.Clear();
        hero.originalSpecialAmmo = 0;
        hero.SpecialAmmo = 0;
        _host.Players[0].Lives = 0;
        Plugin.UpdatePlayerCheats();
        Check(hero.SpecialAmmo == 1 && _host.Players[0].Lives == 1, "Restore usable ammo and at least one life");
        Plugin.SetPlayerInfiniteLives(0, false);
        Plugin.SetPlayerInfiniteSpecial(0, false);
        hero.SpecialAmmo = 0;
        Plugin.UpdatePlayerCheats();
        Check(hero.SpecialAmmo == 0 && Prefix(typeof(InfinitePlayerHealthPatch), hero, DamageType.Normal), "Disable releases immediately");
        try { Plugin.SetPlayerInfiniteSpecial(4, true); throw new Exception("Missing validation"); }
        catch (ArgumentOutOfRangeException) { _checks++; }

        Connect.IsOffline = false;
        _client.Activate();
        Plugin.SetPlayerInfiniteSpecial(1, true);
        Check(!Plugin.IsPlayerInfiniteSpecialEnabled(1), "Client waits for host approval");
        Drain();
        Check(Plugin.IsPlayerInfiniteSpecialEnabled(1), "Host snapshot reaches client");
        _host.Activate();
        Plugin.UpdatePlayerCheats();
        Check(_host.Players[1].character.SpecialAmmo == 0, "Host never writes remote-owned ammo");
        Drain();
        _client.Activate();
        Plugin.UpdatePlayerCheats();
        Check(_client.Players[1].character.SpecialAmmo == 3, "Remote owner refills after client command");
        Plugin.SetPlayerInfiniteLives(0, true);
        Drain();
        _host.Activate();
        Check(Plugin.IsPlayerInfiniteLivesEnabled(0), "Client can target host player");
        Plugin.SetPlayerInfiniteSpecial(0, true);
        Plugin.SetPlayerInfiniteLives(1, true);
        Drain();
        Check(Plugin.IsPlayerInfiniteSpecialEnabled(0) && Plugin.IsPlayerInfiniteSpecialEnabled(1) &&
            Plugin.IsPlayerInfiniteLivesEnabled(0) && Plugin.IsPlayerInfiniteLivesEnabled(1), "Independent per-player toggles synchronize");
        _client.Players[1].character = new BroBase { playerNum = 1, originalSpecialAmmo = 5 };
        Plugin.UpdatePlayerCheats();
        Check(_client.Players[1].character.SpecialAmmo == 5, "New character retains slot cheat and its native capacity");
        RPCController.CurrentRPC = new RPCObject { messageInfo = new MessageInfo { Sender = _client.ID } };
        Call("ReceivePlayerCheatsRPC", 0, 0, 0);
        Check(Plugin.IsPlayerInfiniteLivesEnabled(0), "Reject non-host snapshots");
        RPCController.CurrentRPC.messageInfo.Sender = _host.ID;
        Call("ReceivePlayerCheatsRPC", 16, 0, 0);
        Check(Plugin.IsPlayerInfiniteLivesEnabled(0), "Reject malformed masks");
        RPCController.CurrentRPC = null;
        Call("ResetPlayerCheats");
        _host.Activate();
        UnityEngine.Time.realtimeSinceStartup = 2f;
        Plugin.UpdatePlayerCheats();
        Drain();
        Check(Plugin.IsPlayerInfiniteSpecialEnabled(1), "Periodic snapshot initializes a late/re-enabled peer");
        _host.Activate();
        RPCController.CurrentRPC = new RPCObject { messageInfo = new MessageInfo { Sender = new PID(9) } };
        Call("RequestPlayerCheatRPC", 0, 2, false);
        Check(Plugin.IsPlayerInfiniteLivesEnabled(0), "Reject unknown request sender");
        RPCController.CurrentRPC.messageInfo.Sender = _client.ID;
        Call("RequestPlayerCheatRPC", 4, 0, true);
        Call("RequestPlayerCheatRPC", 0, 5, true);
        Check(Packets.Count == 0, "Reject invalid player/cheat commands");
        RPCController.CurrentRPC = null;
        _client.Activate();
        Call("StopPlayerCheats");
        Drain();
        _host.Activate();
        Check(!Plugin.IsPlayerInfiniteLivesEnabled(0) && !Plugin.IsPlayerInfiniteSpecialEnabled(1), "Stopping a peer clears shared cheats");
        Plugin.SetPlayerInfiniteSpecial(0, true);
        Drain();
        Connect.IsOffline = true;
        Plugin.UpdatePlayerCheats();
        Check(!Plugin.IsPlayerInfiniteSpecialEnabled(0), "Disconnect clears shared state");
        Connect.IsOffline = false;
        _host.Activate();
        Plugin.SetPlayerInfiniteSpecial(0, true);
        Drain();
        PID.ServerID = _client.ID;
        Plugin.UpdatePlayerCheats();
        Check(!Plugin.IsPlayerInfiniteSpecialEnabled(0), "Host change clears shared state");
        CheckRevival();
        Plugin._behaviour = null;
        Plugin.SetPlayerInfiniteLives(0, true);
        Check(!Plugin.IsPlayerInfiniteLivesEnabled(0), "Inactive mod cannot enable cheats");
        Console.WriteLine("PASS: " + _checks + " player-cheat checks");
    }
}

namespace TestMod { public static partial class Plugin { internal static object _behaviour; } }
namespace UnityEngine
{
    internal static class Time { internal static float realtimeSinceStartup; }
    internal struct Vector3
    {
        internal float x, y, z;
        internal Vector3(float x, float y, float z) { this.x = x; this.y = y; this.z = z; }
    }
}
namespace HarmonyLib { internal class HarmonyPatch : Attribute { internal HarmonyPatch(Type type, string method) { } } }
internal enum DamageType { Normal, Crush, OutOfBounds }
internal class Map { internal static Map Instance = new Map(); }
internal static class GameModeController { internal static bool LevelFinished; }
internal enum HeroType { Rambro, Brommando }
internal class Player
{
    internal int playerNum, Lives, SpawnCount;
    internal bool IsMine, firstDeployment, changingBroFromTrigger, awaitingHeroTypeFromServer;
    internal TestVanDammeAnim character;
    internal UnityEngine.Vector3 playerFollowPos;
    internal bool IsAlive() { return awaitingHeroTypeFromServer || (character != null && character.IsAlive()); }
    internal void SpawnHero(HeroType heroType)
    {
        SpawnCount++;
        character = new BroBase { playerNum = playerNum, heroType = heroType,
            X = playerFollowPos.x, Y = playerFollowPos.y, IsMine = IsMine };
        changingBroFromTrigger = awaitingHeroTypeFromServer = false;
    }
}
internal class TestVanDammeAnim
{
    internal int playerNum, health = 1, originalSpecialAmmo = 3, SpecialAmmo;
    internal bool IsMine = true, IsHero = true, destroyed;
    internal HeroType heroType;
    internal float X, Y;
    internal bool IsAlive() { return health > 0 && !destroyed; }
}
internal class BroBase : TestVanDammeAnim { internal List<int> pockettedSpecialAmmo = new List<int>(); }
internal static class HeroController
{
    internal static Player[] players;
    internal static PID[] PIDS;
    internal static bool IsPlaying(int index) { return players[index] != null; }
}
internal class PID
{
    internal byte Value;
    internal PID(byte value) { Value = value; }
    internal static PID MyID, ServerID;
    internal static readonly PID TargetServer = new PID(254), TargetOthers = new PID(253);
    internal static bool IamServer() { return MyID == ServerID; }
}
internal static class Connect
{
    internal static bool IsOffline = true;
    internal static ConnectionLayer Layer = new ConnectionLayer();
}
internal class ConnectionLayer { internal Dictionary<PID, object> PlayerIDPairs = new Dictionary<PID, object>(); }
internal class MessageInfo { internal PID Sender; }
internal class RPCObject { internal MessageInfo messageInfo; }
internal static class RPCController { internal static RPCObject CurrentRPC; }
internal static class RPCBatcher { internal static void FlushQueue() { } }
namespace Networking
{
    internal class AllowedRPCAttribute : Attribute { }
    internal delegate void RpcSignature<T1>(T1 a);
    internal delegate void RpcSignature<T1, T2>(T1 a, T2 b);
    internal delegate void RpcSignature<T1, T2, T3>(T1 a, T2 b, T3 c);
    internal static class Networking
    {
        internal static void RPC<T1>(PID target, bool immediate, bool ignoreSession,
            RpcSignature<T1> method, T1 a) { PlayerCheatTests.Send(target, method, a); }
        internal static void RPC<T1, T2>(PID target, bool immediate, bool ignoreSession,
            RpcSignature<T1, T2> method, T1 a, T2 b) { PlayerCheatTests.Send(target, method, a, b); }
        internal static void RPC<T1, T2, T3>(PID target, bool immediate, bool ignoreSession,
            RpcSignature<T1, T2, T3> method, T1 a, T2 b, T3 c) { PlayerCheatTests.Send(target, method, a, b, c); }
    }
}
