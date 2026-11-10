// Offline endpoint simulation using the real compatibility controller; no Unity runtime required.
using System;
using System.Collections.Generic;
using TestMod;
using UnityEngine;

internal static class CameraCompatibilityTests
{
    private static int _checks;
    private static readonly PID Local = new PID(1);
    private static readonly PID Remote = new PID(2);
    private static CameraCompatibility _controller;

    private static void Check(bool condition, string description)
    {
        _checks++;
        if (!condition) throw new Exception(description);
    }

    private static Player AddPlayer(int slot, PID owner, float x)
    {
        var player = new Player { playerNum = slot, Owner = owner,
            character = new TestVanDammeAnim(x) };
        HeroController.players[slot] = player;
        HeroController.PIDS[slot] = owner;
        return player;
    }

    private static void Reset()
    {
        PID.MyID = Local;
        Connect.IsOffline = false;
        Connect.SessionID = new SID(1);
        Connect.Layer = new ConnectionLayer();
        Connect.Layer.PlayerIDPairs[Remote] = new IDWrapper { HaveSentSyncStateToPlayer = true };
        HeroController.Instance = new HeroController();
        HeroController.players = new Player[4];
        HeroController.PIDS = new PID[4];
        AddPlayer(0, Local, 100f);
        AddPlayer(1, Remote, 500f);
        Plugin.IsFocusedCameraEnabled = true;
        Map.MapData = new object();
        Map.isEditing = false;
        GameModeController.LevelFinished = false;
        SortOfFollow.ControlledByTriggerAction = false;
        CutsceneController.InCutscene = false;
        Networking.Networking.PauseStream = false;
        Networking.Networking.Reset();
        _controller = new CameraCompatibility();
    }

    private static Transform Override(PID peer, int slot)
    {
        return Networking.Networking.GetOverride(peer, slot);
    }

    // Vanilla takes the midpoint of the extents, rather than averaging all players.
    private static float CameraX(PID peer)
    {
        var min = float.MaxValue;
        var max = float.MinValue;
        for (var slot = 0; slot < 4; slot++)
        {
            var player = HeroController.players[slot];
            if (player == null || player.character.health <= 0) continue;
            var target = Override(peer, slot) ?? player.playerTemporaryTarget ?? player.character.transform;
            min = Math.Min(min, target.X);
            max = Math.Max(max, target.X);
        }
        return (min + max) / 2f;
    }

    private static void CheckFollowing()
    {
        Check(Override(Remote, 0) == HeroController.players[1].character.transform,
            "The vanilla peer tracks its own character for our camera contribution");
    }

    private static void TestViewsAndLifecycle()
    {
        Reset();
        Check(CameraX(Remote) == 300f, "Baseline vanilla camera includes both players");
        _controller.Update();
        CheckFollowing();
        Check(CameraX(Remote) == 500f, "Vanilla peer keeps its own character on screen");
        Check(HeroController.players[0].playerTemporaryTarget == null,
            "A targeted RPC does not modify the sender's own camera target");
        HeroController.players[0].character.transform.X = -1500f;
        _controller.Update();
        Check(CameraX(Remote) == 500f, "Moving far away does not drag the vanilla peer camera");
        Check(Networking.Networking.Calls == 1, "Stable frames do not resend RPCs");
        Check(HeroController.players[0].character.transform.X == -1500f,
            "The source's gameplay position is preserved");

        HeroController.players[1].character = new TestVanDammeAnim(700f);
        _controller.Update();
        CheckFollowing();
        Check(CameraX(Remote) == 700f, "Recipient respawn or bro swap replaces the old target");
        var remoteSkill = new Transform(1100f) { Projectile = new Projectile() };
        HeroController.players[1].playerTemporaryTarget = remoteSkill;
        _controller.Update();
        Check(Override(Remote, 0) == remoteSkill && CameraX(Remote) == 1100f,
            "The recipient's native remote-control skill remains its camera target");
        HeroController.players[1].playerTemporaryTarget = null;
        _controller.Update();
        CheckFollowing();
        HeroController.players[1].character.health = 0;
        _controller.Update();
        Check(Override(Remote, 0) == null, "Dead recipient returns to native spectating");
        HeroController.players[1].character.health = 1;
        _controller.Update();
        CheckFollowing();
        HeroController.players[0].character.health = 0;
        _controller.Update();
        Check(Override(Remote, 0) == null, "Source death releases the remote override");
        HeroController.players[0].character = null;
        _controller.Update();
        Check(Override(Remote, 0) == null, "Respawn gap leaves native camera behavior intact");
        HeroController.players[0].character = new TestVanDammeAnim(100f);
        _controller.Update();
        CheckFollowing();

        Networking.Networking.ClearOverrides();
        HeroController.players[0].character = new TestVanDammeAnim(120f);
        _controller.Update();
        CheckFollowing();
        var skillTarget = new Transform(900f);
        HeroController.players[0].playerTemporaryTarget = skillTarget;
        _controller.Update();
        Check(Override(Remote, 0) == skillTarget,
            "Yielding to a native skill restores its target instead of erasing it");
        var calls = Networking.Networking.Calls;
        _controller.Update();
        Check(Networking.Networking.Calls == calls, "Native skills retain control without repeated writes");
        HeroController.players[0].playerTemporaryTarget = null;
        Networking.Networking.ClearOverrides();
        _controller.Update();
        CheckFollowing();
        Plugin.IsFocusedCameraEnabled = false;
        _controller.Update();
        Check(Override(Remote, 0) == null, "Disabling focus removes the remote target");
        Plugin.IsFocusedCameraEnabled = true;
        _controller.Update();
        _controller.Release();
        Check(Override(Remote, 0) == null, "Mod stop releases remote targets");
    }

    private static void TestNativeCameraStates()
    {
        for (var state = 0; state < 4; state++)
        {
            Reset();
            _controller.Update();
            switch (state)
            {
                case 0: SortOfFollow.ControlledByTriggerAction = true; break;
                case 1: CutsceneController.InCutscene = true; break;
                case 2: GameModeController.LevelFinished = true; break;
                case 3: Map.isEditing = true; break;
            }
            _controller.Update();
            Check(Override(Remote, 0) == null, "Native camera state releases compatibility: " + state);
        }
        Reset();
        Plugin.IsFocusedCameraEnabled = false;
        _controller.Update();
        Check(Networking.Networking.Calls == 0, "Disabled by default means no remote operations");
        Plugin.IsFocusedCameraEnabled = true;
        Connect.IsOffline = true;
        _controller.Update();
        Check(Networking.Networking.Calls == 0, "Offline play emits no compatibility RPCs");
    }

    private static void TestNetworkBoundaries()
    {
        Reset();
        Connect.Layer.PlayerIDPairs[Remote].HaveSentSyncStateToPlayer = false;
        _controller.Update();
        Check(Networking.Networking.Calls == 0, "Wait until the peer accepts ordinary RPCs");
        Connect.Layer.PlayerIDPairs[Remote].HaveSentSyncStateToPlayer = true;
        HeroController.players[1].character.transform.Registered = false;
        _controller.Update();
        Check(Networking.Networking.Calls == 0, "Unregistered transforms are not transmitted");
        HeroController.players[1].character.transform.Registered = true;
        Networking.Networking.PauseStream = true;
        _controller.Update();
        Check(Networking.Networking.Calls == 0, "Scene loading does not send compatibility RPCs");
        Networking.Networking.PauseStream = false;
        _controller.Update();
        CheckFollowing();
        Connect.Layer.PlayerIDPairs.Remove(Remote);
        var calls = Networking.Networking.Calls;
        _controller.Update();
        Check(Networking.Networking.Calls == calls, "Disconnect does not send to an absent peer");
        Connect.Layer.PlayerIDPairs[Remote] = new IDWrapper { HaveSentSyncStateToPlayer = true };
        _controller.Update();
        Check(Networking.Networking.Calls == calls + 1, "Reconnect establishes a new target");
        Plugin.IsFocusedCameraEnabled = false;
        Connect.SessionID.AsInt++;
        calls = Networking.Networking.Calls;
        _controller.Update();
        Check(Networking.Networking.Calls == calls, "Mutable session ID changes discard old cleanup");

        Reset();
        _controller.Update();
        HeroController.Instance = new HeroController();
        Plugin.IsFocusedCameraEnabled = false;
        calls = Networking.Networking.Calls;
        _controller.Update();
        Check(Networking.Networking.Calls == calls, "Same-name map restart cannot clean up the new scene");
    }

    private static void TestMultiplePlayers()
    {
        Reset();
        AddPlayer(2, Remote, 900f);
        _controller.Update();
        Check(Networking.Networking.Calls == 1, "Multiple local players on one peer share one binding");
        Check(CameraX(Remote) == 700f, "The recipient's local multiplayer framing is preserved");
        HeroController.players[1].character.health = 0;
        _controller.Update();
        Check(Override(Remote, 0) == HeroController.players[2].character.transform,
            "A surviving local teammate replaces the dead anchor");

        Reset();
        var third = new PID(3);
        Connect.Layer.PlayerIDPairs[third] = new IDWrapper { HaveSentSyncStateToPlayer = true };
        AddPlayer(2, third, 900f);
        _controller.Update();
        Check(Override(Remote, 0) == HeroController.players[1].character.transform &&
            Override(third, 0) == HeroController.players[2].character.transform,
            "Each peer receives its own target instead of a room-wide broadcast");
        Check(CameraX(Remote) == 700f && CameraX(third) == 700f,
            "Non-focused teammates retain their group camera when the source moves away");

        Reset();
        // Another enabled endpoint can redirect its slot on us. Never reuse that as its real position.
        HeroController.players[1].playerTemporaryTarget = HeroController.players[0].character.transform;
        _controller.Update();
        CheckFollowing();
    }

    private static void Main()
    {
        TestViewsAndLifecycle();
        TestNativeCameraStates();
        TestNetworkBoundaries();
        TestMultiplePlayers();
        Console.WriteLine("Passed " + _checks + " camera compatibility checks.");
    }
}

namespace UnityEngine
{
    public class Transform
    {
        public float X;
        public bool Registered = true;
        public Projectile Projectile;
        public T GetComponent<T>() where T : class { return Projectile as T; }
        public Transform(float x) { X = x; }
    }
}

public sealed class PID
{
    private readonly int _id;
    public static PID MyID;
    public PID(int id) { _id = id; }
    public bool IsMine { get { return Equals(MyID); } }
    public override bool Equals(object other) { return other is PID && ((PID)other)._id == _id; }
    public override int GetHashCode() { return _id; }
    public static bool operator ==(PID a, PID b) { return object.Equals(a, b); }
    public static bool operator !=(PID a, PID b) { return !object.Equals(a, b); }
}

public sealed class SID
{
    public int AsInt;
    public SID(int id) { AsInt = id; }
}

public sealed class IDWrapper { public bool HaveSentSyncStateToPlayer; }
public sealed class Projectile { }
public sealed class ConnectionLayer
{
    public readonly Dictionary<PID, IDWrapper> PlayerIDPairs = new Dictionary<PID, IDWrapper>();
}
public static class Connect
{
    public static bool IsOffline;
    public static SID SessionID;
    public static ConnectionLayer Layer;
}
public sealed class TestVanDammeAnim
{
    public int health = 1;
    public Transform transform;
    public TestVanDammeAnim(float x) { transform = new Transform(x); }
}
public sealed class Player
{
    public int playerNum;
    public PID Owner;
    public bool IsMine { get { return Owner.IsMine; } }
    public TestVanDammeAnim character;
    public Transform playerTemporaryTarget;
}
public class HeroController
{
    public static HeroController Instance;
    public static Player[] players;
    public static PID[] PIDS;
    public static void AddTemporaryPlayerTarget(int slot, Transform target) { }
    public static void RemoveTemporaryPlayerTarget(int slot) { }
}
public static class Map { public static object MapData; public static bool isEditing; }
public static class GameModeController { public static bool LevelFinished; }
public static class SortOfFollow { public static bool ControlledByTriggerAction; }
public static class CutsceneController { public static bool InCutscene; }
public static class NID { public const int NoID = 0; }
public static class Registry
{
    public static int GetNID(Transform target) { return target.Registered ? 1 : NID.NoID; }
}
namespace TestMod
{
    internal static class Plugin
    {
        internal static bool IsFocusedCameraEnabled;
        internal static int GetCameraFollowPlayer()
        {
            for (var slot = 0; slot < 4; slot++)
                if (HeroController.players[slot] != null && HeroController.PIDS[slot].IsMine) return slot;
            return -1;
        }
    }
}
namespace Networking
{
    public delegate void RpcSignature<T>(T arg);
    public delegate void RpcSignature<T1, T2>(T1 arg1, T2 arg2);
    public static class Networking
    {
        public static bool PauseStream;
        public static int Calls;
        private static readonly Dictionary<PID, Transform[]> Overrides = new Dictionary<PID, Transform[]>();
        public static void Reset() { Calls = 0; ClearOverrides(); }
        public static void ClearOverrides() { Overrides.Clear(); }
        public static Transform GetOverride(PID peer, int slot)
        {
            Transform[] targets;
            return Overrides.TryGetValue(peer, out targets) ? targets[slot] : null;
        }
        public static void RPC<T>(PID peer, RpcSignature<T> method, T arg, bool immediate)
        {
            Calls++;
            if (Overrides.ContainsKey(peer)) Overrides[peer][(int)(object)arg] = null;
        }
        public static void RPC<T1, T2>(PID peer, RpcSignature<T1, T2> method, T1 arg1, T2 arg2, bool immediate)
        {
            Calls++;
            if (!Overrides.ContainsKey(peer)) Overrides[peer] = new Transform[4];
            Overrides[peer][(int)(object)arg1] = (Transform)(object)arg2;
        }
    }
}
