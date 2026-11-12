using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Xml.Serialization;
using Unity_Inspector_Mod;
using UnityModManagerNet;
using InspectorMain = Unity_Inspector_Mod.Main;

// Compile the production Main and TcpServer against small UI/UMM substitutes.
// All socket operations below use real loopback TCP connections.
public static class PortSwitchTests
{
    private static readonly BindingFlags PrivateStatic = BindingFlags.NonPublic | BindingFlags.Static;
    private static int checks;

    public static int Main(string[] args)
    {
        try
        {
            Run(args[0]);
            RuntimeChecks();
            DispatcherChecks();
            Console.WriteLine("PASS: {0} connection, persistence, lifecycle, dispatcher and input checks", checks);
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(ex);
            return 1;
        }
        finally
        {
            if (InspectorMain.server != null) InspectorMain.server.Stop();
        }
    }

    private static void Check(bool value, string message)
    {
        if (!value) throw new Exception(message);
        checks++;
    }

    private static void Invoke(string method)
    {
        typeof(InspectorMain).GetMethod(method, PrivateStatic).Invoke(null,
            method == "Load" || method == "OnGUI" ? new object[] { InspectorMain.mod } : null);
    }

    private static bool Apply(string input, bool start)
    {
        typeof(InspectorMain).GetField("serverPortInput", PrivateStatic).SetValue(null, input);
        return (bool)typeof(InspectorMain).GetMethod("ApplyPort", PrivateStatic).Invoke(null, new object[] { start });
    }

    private static int FreePort()
    {
        var reservation = new TcpListener(IPAddress.Loopback, 0);
        reservation.ExclusiveAddressUse = true;
        reservation.Start();
        int port = ((IPEndPoint)reservation.LocalEndpoint).Port;
        reservation.Stop();
        return port;
    }

    private static void Ping(TcpClient client)
    {
        client.ReceiveTimeout = 3000;
        client.SendTimeout = 3000;
        var stream = client.GetStream();
        byte[] message = Encoding.UTF8.GetBytes("ping\n");
        stream.Write(message, 0, message.Length);
        var reader = new StreamReader(stream, new UTF8Encoding(false));
        Check(reader.ReadLine() == "pong", "TCP client must receive a reply");
    }

    private static void Run(string settingsPath)
    {
        InspectorMain.mod = new UnityModManager.ModEntry { SettingsPath = settingsPath };
        new Settings { autoStartServer = false, useChinese = false }.Save(InspectorMain.mod);
        Invoke("Load");
        Check(InspectorMain.server == null, "Loading with auto-start off must not listen");

        string baseline = File.ReadAllText(settingsPath);
        UnityEngine.GUILayout.Input = FreePort().ToString();
        Invoke("OnGUI");
        Check(InspectorMain.server == null && InspectorMain.settings.serverPort == 9999,
            "Typing a port without Apply must not change the configuration");
        Check(File.ReadAllText(settingsPath) == baseline, "Typing must not save settings");

        foreach (string invalid in new[] { null, "", "abc", "0", "-1", "65536", "+9999", "1.5", "999999999999" })
        {
            Check(!Apply(invalid, true), "Invalid input must be rejected: " + invalid);
            Check(InspectorMain.server == null && InspectorMain.settings.serverPort == 9999,
                "Invalid input must leave the server and settings unchanged");
            Check(File.ReadAllText(settingsPath) == baseline, "Invalid input must not overwrite saved settings");
        }

        Check(Apply("1", false) && InspectorMain.settings.serverPort == 1, "Lower port bound must be accepted");
        Check(Apply("65535", false) && InspectorMain.settings.serverPort == 65535, "Upper port bound must be accepted");
        int firstPort = FreePort();
        Check(Apply(firstPort.ToString(), false) && InspectorMain.server == null,
            "Apply while stopped must save without starting a listener");
        Check(UnityModManager.ModSettings.Load<Settings>(InspectorMain.mod).serverPort == firstPort,
            "Saved port must survive loading from XML");
        Invoke("Load");
        Invoke("StartServer");
        TcpServer original = InspectorMain.server;
        Check(original.IsRunning && original.Port == firstPort, "Start must use the saved port");

        var connection = new TcpClient("127.0.0.1", firstPort);
        try
        {
            Ping(connection);
            Check(Apply("  " + firstPort + "  ", false) && ReferenceEquals(original, InspectorMain.server),
                "Applying the active port must not replace the listener");
            Ping(connection);
            Check(!Apply("65536", false) && ReferenceEquals(original, InspectorMain.server),
                "Invalid input while running must preserve the listener");
            Ping(connection);

            var occupied = new TcpListener(IPAddress.Loopback, 0);
            occupied.ExclusiveAddressUse = true;
            occupied.Start();
            int nextPort = ((IPEndPoint)occupied.LocalEndpoint).Port;
            try
            {
                string saved = File.ReadAllText(settingsPath);
                Check(!Apply(nextPort.ToString(), false), "An occupied port must be rejected");
                Check(ReferenceEquals(original, InspectorMain.server) && original.IsRunning,
                    "Failed switch must keep the original listener running");
                Check(InspectorMain.settings.serverPort == firstPort && File.ReadAllText(settingsPath) == saved,
                    "Failed switch must preserve memory and disk settings");
                Ping(connection);
            }
            finally { occupied.Stop(); }

            Check(Apply(nextPort.ToString(), false), "Switching to a free port must succeed");
            Check(!original.IsRunning && original.ConnectedClients == 0,
                "Successful switch must stop the old listener and remove its clients");
            bool disconnected;
            try { disconnected = connection.GetStream().ReadByte() == -1; }
            catch (IOException) { disconnected = true; }
            Check(disconnected, "Successful switch must disconnect existing clients");
            using (var newConnection = new TcpClient("127.0.0.1", nextPort)) Ping(newConnection);
            Check(UnityModManager.ModSettings.Load<Settings>(InspectorMain.mod).serverPort == nextPort,
                "The new port must be persisted immediately");
            var released = new TcpListener(IPAddress.Any, firstPort);
            released.Start();
            released.Stop();
            Check(true, "Old port must be released after switching");

            UnityEngine.GUILayout.Input = firstPort.ToString();
            UnityEngine.GUILayout.Labels.Clear();
            Invoke("OnGUI");
            Check(UnityEngine.GUILayout.Labels.Contains("Listening Port: " + nextPort),
                "UI must show the actual listening port while a different port is being typed");
            UnityEngine.GUILayout.PressedButton = "Apply Settings";
            Invoke("OnGUI");
            Check(InspectorMain.server.Port == firstPort, "The Apply button must invoke the switch");
        }
        finally { connection.Close(); }

        // Disconnecting clients concurrently with Stop must not corrupt the list
        // or leave the accept thread and bound socket behind.
        for (int round = 0; round < 8; round++)
        {
            TcpServer active = InspectorMain.server;
            int port = active.Port;
            var errors = new List<Exception>();
            var workers = new List<Thread>();
            for (int worker = 0; worker < 4; worker++)
            {
                var thread = new Thread(() =>
                {
                    try
                    {
                        for (int i = 0; i < 30; i++)
                            using (var client = new TcpClient("127.0.0.1", port)) { }
                    }
                    catch (SocketException) { } // Expected if Stop wins the race.
                    catch (Exception ex) { lock (errors) errors.Add(ex); }
                });
                thread.IsBackground = true;
                workers.Add(thread);
                thread.Start();
            }
            Thread.Sleep(10);
            Invoke("StopServer");
            foreach (Thread worker in workers) Check(worker.Join(5000), "Connection worker must exit after Stop");
            Check(errors.Count == 0 && !active.IsRunning && active.ConnectedClients == 0,
                "Concurrent shutdown must leave no registered clients");
            Check(Apply(port.ToString(), true), "Stopped port must be reusable immediately");
        }

        UnityEngine.GUILayout.PressedButton = "Stop Server";
        Invoke("OnGUI");
        Check(InspectorMain.server == null, "Stop button must release the listener");
        Invoke("Load");
        UnityEngine.GUILayout.PressedButton = "Start Server";
        Invoke("OnGUI");
        Check(InspectorMain.server.IsRunning && InspectorMain.server.Port == InspectorMain.settings.serverPort,
            "Start button must use the persisted port after reloading settings");
    }

    private static void SetInput(string name, object value)
    {
        typeof(InspectorMain).GetField(name, PrivateStatic).SetValue(null, value);
    }

    private static void Toggle(bool value)
    {
        typeof(InspectorMain).GetMethod("OnToggle", PrivateStatic).Invoke(null, new object[] { InspectorMain.mod, value });
    }

    private static void WaitWaiting(Thread thread)
    {
        DateTime deadline = DateTime.UtcNow.AddSeconds(2);
        while ((thread.ThreadState & ThreadState.WaitSleepJoin) == 0 && thread.IsAlive && DateTime.UtcNow < deadline) Thread.Sleep(1);
        Check(thread.IsAlive && (thread.ThreadState & ThreadState.WaitSleepJoin) != 0, "Worker must enter its wait");
    }

    private static void RuntimeChecks()
    {
        int port = InspectorMain.server.Port;
        foreach (string text in new[] { "ab12中3.-", "１２٣", "+9999", "9999", "", "abc" })
        {
            UnityEngine.GUILayout.Input = text;
            Invoke("OnGUI");
            string value = (string)typeof(InspectorMain).GetField("serverPortInput", PrivateStatic).GetValue(null);
            foreach (char c in value) Check(c >= '0' && c <= '9', "Port editor must retain only ASCII digits");
        }
        UnityEngine.GUILayout.Input = null;
        UnityEngine.GUILayout.PressedButton = "中文";
        Invoke("OnGUI");
        Check(InspectorMain.settings.useChinese, "Language button must switch to Chinese");
        Check(UnityModManager.ModSettings.Load<Settings>(InspectorMain.mod).useChinese, "Language preference must be saved");
        UnityEngine.GUILayout.PressedButton = "English";
        Invoke("OnGUI");
        Check(!InspectorMain.settings.useChinese, "Language button must switch back to English");

        bool refused = false;
        try { new TcpServer(port, "0.0.0.0"); } catch (ArgumentException) { refused = true; }
        Check(refused, "Remote listener must require a token");
        SetInput("allowRemoteInput", true);
        SetInput("serverTokenInput", "");
        Check(!Apply(port.ToString(), false) && InspectorMain.server.ListenAddress == "127.0.0.1",
            "Invalid remote configuration must keep the working local listener");
        SetInput("serverTokenInput", "test-only-token");
        Check(Apply(port.ToString(), false) && InspectorMain.server.ListenAddress == "0.0.0.0",
            "Same-port address change must restart the listener successfully");
        using (var client = new TcpClient("127.0.0.1", port))
        {
            Ping(client);
            SetInput("serverTokenInput", "new-test-token");
            Check(Apply(port.ToString(), false), "Token rotation must succeed on the same endpoint");
            bool closed;
            try { closed = client.GetStream().ReadByte() == -1; } catch (IOException) { closed = true; }
            Check(closed, "Changing a token must disconnect previous clients");
        }
        SetInput("allowRemoteInput", false);
        SetInput("serverTokenInput", "");
        Check(Apply(port.ToString(), false), "Local listener must allow clearing the token");
        var clients = new List<TcpClient>();
        try
        {
            for (int i = 0; i < TcpServer.MaxClients; i++)
            {
                var client = new TcpClient("127.0.0.1", port);
                clients.Add(client);
                Ping(client);
            }
            using (var excess = new TcpClient("127.0.0.1", port))
            {
                excess.ReceiveTimeout = 3000;
                bool closed;
                try { closed = excess.GetStream().ReadByte() == -1; } catch (IOException) { closed = true; }
                Check(closed && InspectorMain.server.ConnectedClients == TcpServer.MaxClients, "Excess connections must be closed");
            }
        }
        finally { foreach (var client in clients) client.Close(); }

        InputSimulator.SimulateInput("fire", 1000);
        Check(InputSimulator.HasAnySimulatedInput(), "Hold input must become active");
        Toggle(false);
        Check(!InspectorMain.enabled && InspectorMain.server == null, "Disabling must stop the server");
        Check(!InputSimulator.HasAnySimulatedInput(), "Disabling must release simulated keys");
        Check(ScriptManager.Cleanups > 0 && CodeExecutor.Cleanups > 0 && ScriptCompiler.Cleanups > 0 && HarmonyLib.Harmony.UnpatchCalls > 0,
            "Disabling must invoke script, evaluator and patch cleanup");
        Check(!Apply(port.ToString(), true) && InspectorMain.server == null, "Disabled mod must reject manual server start");
        Toggle(true);
        Check(InspectorMain.enabled && MainThreadDispatcher.Instance != null, "Re-enabling must recreate the dispatcher");
        Check(InspectorMain.server == null, "Auto-start disabled must remain stopped after enabling");
        Invoke("StartServer");
        Check(InspectorMain.server.IsRunning, "Re-enabled mod must allow starting the server");
    }

    private static void DispatcherChecks()
    {
        var dispatcher = MainThreadDispatcher.Instance;
        bool inline = false;
        MainThreadDispatcher.EnqueueAndWait(() => inline = true);
        Check(inline, "Nested calls on the main thread must execute inline without waiting");
        Exception error = null;
        bool ran = false;
        var worker = new Thread(() => { try { MainThreadDispatcher.EnqueueAndWait(() => ran = true, 40); } catch (Exception ex) { error = ex; } });
        worker.Start();
        Check(worker.Join(1000) && error is TimeoutException, "Unpumped queued task must time out");
        dispatcher.Update();
        Check(!ran, "Timed-out queued task must never run later");

        error = null;
        worker = new Thread(() => { try { MainThreadDispatcher.EnqueueAndWait(() => { throw new ArgumentException("expected"); }); } catch (Exception ex) { error = ex; } });
        worker.Start();
        WaitWaiting(worker);
        dispatcher.Update();
        Check(worker.Join(1000) && error is ArgumentException, "Main-thread exception must return to the correct waiter");

        bool enqueuedDuringAction = false;
        MainThreadDispatcher.Enqueue(() =>
        {
            var producer = new Thread(() => { MainThreadDispatcher.Enqueue(() => { }); enqueuedDuringAction = true; });
            producer.Start();
            Check(producer.Join(500), "Queue lock must not be held during action execution");
        });
        dispatcher.Update();
        Check(enqueuedDuringAction, "Background enqueue must succeed while an action runs");
        dispatcher.Update();

        bool secondRan = false;
        MainThreadDispatcher.Enqueue(() => Thread.Sleep(15));
        MainThreadDispatcher.Enqueue(() => secondRan = true);
        dispatcher.Update();
        Check(!secondRan, "Frame budget must defer additional actions after a slow action");
        dispatcher.Update();
        Check(secondRan, "Deferred task must run on the next update");

        bool disconnected = false;
        ran = false; error = null;
        worker = new Thread(() =>
        {
            MainThreadDispatcher.RequestCancelled = () => disconnected;
            try { MainThreadDispatcher.EnqueueAndWait(() => ran = true); } catch (Exception ex) { error = ex; }
            finally { MainThreadDispatcher.RequestCancelled = null; }
        });
        worker.Start();
        WaitWaiting(worker);
        disconnected = true;
        Check(worker.Join(1000) && error is OperationCanceledException, "Disconnected pending request must be cancelled");
        dispatcher.Update();
        Check(!ran, "Disconnected request must not change the game later");

        error = null;
        worker = new Thread(() => { try { MainThreadDispatcher.EnqueueAndWait(() => Thread.Sleep(90), 40); } catch (Exception ex) { error = ex; } });
        worker.Start();
        WaitWaiting(worker);
        dispatcher.Update();
        Check(worker.Join(1000) && error is TimeoutException && error.Message.Contains("already started"),
            "A running action timeout must say that execution already started");

        error = null;
        worker = new Thread(() => { try { MainThreadDispatcher.EnqueueAndWait(() => { }); } catch (Exception ex) { error = ex; } });
        worker.Start();
        WaitWaiting(worker);
        MainThreadDispatcher.StopAccepting();
        Check(worker.Join(1000) && error is OperationCanceledException, "Shutdown must wake queued waiters immediately");
        MainThreadDispatcher.Initialize();

        InputSimulator.Initialize();
        InputSimulator.SimulateInput("fire", 20);
        InputSimulator.Cleanup();
        InputSimulator.Initialize();
        InputSimulator.SimulateInput("fire", 1000);
        Thread.Sleep(40);
        Check(InputSimulator.IsInputSimulated("fire"), "A disposed old timer must not release a newly held key");
        InputSimulator.Cleanup();
        Check(!InputSimulator.HasAnySimulatedInput(), "Cleanup must release every active key");

        InputSimulator.Initialize();
        worker = new Thread(() => InputSimulator.SimulateInput("jump", null, null, 100, 1000));
        worker.Start();
        DateTime deadline = DateTime.UtcNow.AddSeconds(2);
        while (!InputSimulator.HasAnySimulatedInput() && DateTime.UtcNow < deadline) Thread.Sleep(1);
        InputSimulator.Cleanup();
        InputSimulator.Initialize();
        Check(worker.Join(500), "Cleanup must interrupt a multiple-press request even after re-enabling");
        Check(!InputSimulator.HasAnySimulatedInput(), "Cancelled presses must not restart after re-enabling");
    }
}

namespace Unity_Inspector_Mod
{
    public class MessageHandler { public string HandleMessage(string message, string token = "", Func<bool> cancelled = null) { return "pong"; } }
    public static class ScriptManager { public static int Cleanups; public static void UnloadAll() { Cleanups++; } }
    public static class CodeExecutor { public static int Cleanups; public static void Cleanup() { Cleanups++; } }
    public static class ScriptCompiler { public static int Cleanups; public static void Cleanup() { Cleanups++; } }
    public static class SwapBrosIntegration { public static void Initialize(Action<string> log) { } }
}

namespace UnityEngine
{
    public class Object { public static void DontDestroyOnLoad(Object value) { } public static void Destroy(Object value) { } }
    public class Component : Object { public GameObject gameObject; }
    public class MonoBehaviour : Component { }
    public class GameObject : Object
    {
        public GameObject(string name) { }
        public T AddComponent<T>() where T : Component, new() { return new T { gameObject = this }; }
    }
    public class GUILayoutOption { }
    public static class GUILayout
    {
        public static string Input;
        public static string PressedButton;
        public static readonly List<string> Labels = new List<string>();
        public static void Label(string value, params GUILayoutOption[] options) { Labels.Add(value); }
        public static GUILayoutOption Width(float width) { return new GUILayoutOption(); }
        public static string TextField(string value, params GUILayoutOption[] options) { return Input ?? value; }
        public static string PasswordField(string value, char mask, int maximum, params GUILayoutOption[] options) { return value; }
        public static bool Button(string value, params GUILayoutOption[] options)
        {
            if (PressedButton != value) return false;
            PressedButton = null;
            return true;
        }
        public static void BeginHorizontal() { }
        public static void EndHorizontal() { }
        public static void Space(float value) { }
        public static bool Toggle(bool value, string text) { return value; }
    }
}

namespace HarmonyLib
{
    public class Harmony
    {
        public static int UnpatchCalls;
        public Harmony(string id) { }
        public void PatchAll(Assembly assembly) { }
        public void UnpatchAll(string id) { UnpatchCalls++; }
    }
}

namespace UnityModManagerNet
{
    public class UnityModManager
    {
        public class ModEntry
        {
            public string SettingsPath;
            public Action<ModEntry> OnGUI, OnSaveGUI;
            public Func<ModEntry, bool, bool> OnToggle;
            public Func<ModEntry, bool> OnUnload;
            public readonly ModInfo Info = new ModInfo();
            public readonly ModLogger Logger = new ModLogger();
        }
        public class ModInfo { public string Id = "Unity Inspector Mod"; }
        public class ModLogger { public void Log(string value) { } }
        public class ModSettings
        {
            public virtual void Save(ModEntry entry) { }
            public static T Load<T>(ModEntry entry) where T : new()
            {
                using (var stream = File.OpenRead(entry.SettingsPath))
                    return (T)new XmlSerializer(typeof(T)).Deserialize(stream);
            }
            protected static void Save<T>(T value, ModEntry entry)
            {
                using (var stream = File.Create(entry.SettingsPath))
                    new XmlSerializer(typeof(T)).Serialize(stream, value);
            }
        }
    }
}
