using System;
using System.Globalization;
using System.Reflection;
using HarmonyLib;
using UnityEngine;
using UnityModManagerNet;

namespace Unity_Inspector_Mod
{
    static class Main
    {
        public static UnityModManager.ModEntry mod;
        public static volatile bool enabled;
        public static Settings settings;
        public static TcpServer server;
        private static string serverPortInput;
        private static string serverTokenInput;
        private static bool allowRemoteInput;
        private static Harmony harmony;
        private static string serverMessageChinese;
        private static string serverMessageEnglish;
        private static string settingsSaveError;

        static bool Load( UnityModManager.ModEntry modEntry )
        {
            modEntry.OnGUI = OnGUI;
            modEntry.OnSaveGUI = OnSaveGUI;
            modEntry.OnToggle = OnToggle;
            modEntry.OnUnload = OnUnload;
            settings = Settings.Load<Settings>( modEntry );
            mod = modEntry;
            serverPortInput = settings.serverPort.ToString( CultureInfo.InvariantCulture );
            serverTokenInput = settings.accessToken ?? "";
            allowRemoteInput = settings.allowRemoteConnections;
            Activate();
            return true;
        }

        private static void Activate()
        {
            enabled = true;
            MainThreadDispatcher.Initialize();
            InputSimulator.Initialize();
            SwapBrosIntegration.Initialize( Log );
            try
            {
                harmony = new Harmony(mod.Info.Id);
                var assembly = Assembly.GetExecutingAssembly();
                harmony.PatchAll(assembly);
                Log("Harmony patches applied successfully");
            }
            catch (Exception ex)
            {
                Log($"Failed to apply Harmony patches: {ex.Message}");
            }

            if ( settings.autoStartServer ) StartServer();
        }

        static void OnGUI( UnityModManager.ModEntry modEntry )
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label( Text( "界面语言：中文", "Language: English" ), GUILayout.Width( 180 ) );
            if ( GUILayout.Button( settings.useChinese ? "English" : "中文", GUILayout.Width( 100 ) ) )
            {
                settings.useChinese = !settings.useChinese;
                SaveSettings();
            }
            GUILayout.EndHorizontal();

            bool running = server != null && server.IsRunning;
            GUILayout.Label( Text( "TCP 服务状态：", "TCP Server Status: " ) +
                (running ? Text( "运行中", "Running" ) : Text( "已停止", "Stopped" )) );

            if ( server != null && server.IsRunning )
            {
                GUILayout.Label( Text( $"监听端口：{server.Port}", $"Listening Port: {server.Port}" ) );
                GUILayout.Label( Text( $"监听地址：{server.ListenAddress}", $"Listening Address: {server.ListenAddress}" ) );
                GUILayout.Label( Text( $"已连接客户端：{server.ConnectedClients}", $"Connected Clients: {server.ConnectedClients}" ) );
            }

            allowRemoteInput = GUILayout.Toggle( allowRemoteInput,
                Text( "允许局域网/穿透连接（需要口令）", "Allow LAN/tunnel connections (token required)" ) );
            GUILayout.BeginHorizontal();
            GUILayout.Label( Text( "连接口令：", "Access token:" ), GUILayout.Width( 150 ) );
            serverTokenInput = GUILayout.PasswordField( serverTokenInput ?? "", '*', 256, GUILayout.Width( 280 ) );
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            GUILayout.Label( Text( "端口（1-65535）：", "Port (1-65535):" ), GUILayout.Width( 150 ) );
            serverPortInput = GUILayout.TextField( serverPortInput, GUILayout.Width( 100 ) );
            for ( int i = serverPortInput.Length - 1; i >= 0; i-- )
            {
                if ( serverPortInput[i] < '0' || serverPortInput[i] > '9' )
                    serverPortInput = serverPortInput.Remove( i, 1 );
            }
            if ( GUILayout.Button( Text( "应用连接设置", "Apply Settings" ), GUILayout.Width( 140 ) ) )
            {
                ApplyPort( false );
            }
            GUILayout.EndHorizontal();
            GUILayout.Label( Text( "默认仅本机连接。应用后，在 MCP 调用 configure_connection 设置相同的地址、端口和口令。",
                "Local connections only by default. After applying, use MCP configure_connection with the matching host, port and token." ) );
            string serverMessage = Text( serverMessageChinese, serverMessageEnglish );
            if ( !string.IsNullOrEmpty( serverMessage ) )
            {
                GUILayout.Label( serverMessage );
            }
            if ( !string.IsNullOrEmpty( settingsSaveError ) )
            {
                GUILayout.Label( Text( "无法保存设置：", "Could not save settings: " ) + settingsSaveError );
            }

            if ( server != null && server.IsRunning )
            {
                if ( GUILayout.Button( Text( "停止服务", "Stop Server" ), GUILayout.Width( 200 ) ) )
                {
                    StopServer();
                }
            }
            else
            {
                if ( GUILayout.Button( Text( "启动服务", "Start Server" ), GUILayout.Width( 200 ) ) )
                {
                    StartServer();
                }
            }

            GUILayout.Space( 10 );
            settings.autoStartServer = GUILayout.Toggle( settings.autoStartServer,
                Text( "加载 Mod 时自动启动服务", "Auto-start server on load" ) );
        }

        private static string Text( string chinese, string english )
        {
            return settings.useChinese ? chinese : english;
        }

        private static void SetServerMessage( string chinese, string english )
        {
            serverMessageChinese = chinese;
            serverMessageEnglish = english;
        }

        private static void SaveSettings()
        {
            try
            {
                settings.Save( mod );
                settingsSaveError = null;
            }
            catch ( Exception ex )
            {
                settingsSaveError = ex.Message;
                Log( $"Could not save settings: {ex.Message}" );
            }
        }

        static void OnSaveGUI( UnityModManager.ModEntry modEntry )
        {
            SaveSettings();
        }

        static bool OnToggle( UnityModManager.ModEntry modEntry, bool value )
        {
            if ( value == enabled ) return true;
            if ( value ) Activate();
            else Deactivate();
            return true;
        }

        static bool OnUnload( UnityModManager.ModEntry modEntry )
        {
            Deactivate();
            return true;
        }

        private static void Cleanup( Action action )
        {
            try { action(); }
            catch ( Exception ex ) { Log( "Cleanup failed: " + ex ); }
        }

        private static void Deactivate()
        {
            enabled = false;
            StopServer();
            MainThreadDispatcher.StopAccepting();
            Cleanup( InputSimulator.Cleanup );
            Cleanup( ScriptManager.UnloadAll );
            Cleanup( CodeExecutor.Cleanup );
            Cleanup( ScriptCompiler.Cleanup );
            Cleanup( () => { if ( harmony != null ) harmony.UnpatchAll( mod.Info.Id ); } );
            harmony = null;
            MainThreadDispatcher.Shutdown();
        }

        static void StartServer()
        {
            ApplyPort( true );
        }

        // Bind the replacement first so an occupied port leaves the current
        // listener, its clients and the saved configuration untouched.
        static bool ApplyPort( bool startIfStopped )
        {
            if ( !enabled )
            {
                SetServerMessage( "请先启用 Mod。", "Enable the mod first." );
                return false;
            }
            int port;
            if ( !int.TryParse( (serverPortInput ?? "").Trim(), NumberStyles.None,
                CultureInfo.InvariantCulture, out port ) || port < 1 || port > 65535 )
            {
                SetServerMessage( "请输入 1 到 65535 之间的整数端口，当前端口未改变。",
                    "Enter a whole port number from 1 to 65535. The current port has not changed." );
                return false;
            }

            string address = allowRemoteInput ? "0.0.0.0" : "127.0.0.1";
            string token = serverTokenInput ?? "";
            if ( token.Length > 256 || (allowRemoteInput && string.IsNullOrEmpty( token )) )
            {
                SetServerMessage( "远程监听需要连接口令，长度不得超过 256 个字符。",
                    "Remote listening requires an access token, at most 256 characters." );
                return false;
            }

            bool running = server != null && server.IsRunning;
            if ( running && server.Port == port && server.ListenAddress == address )
            {
                server.UpdateToken( token );
            }
            else if ( running || startIfStopped )
            {
                var previous = server;
                var replacement = new TcpServer( port, address, token );
                bool stoppedPrevious = running && previous.Port == port;
                try
                {
                    // A different address on the same port cannot be pre-bound.
                    if ( stoppedPrevious ) previous.Stop();
                    replacement.Start();
                }
                catch ( Exception ex )
                {
                    if ( stoppedPrevious )
                    {
                        try { previous.Start(); }
                        catch ( Exception rollback ) { Log( "Could not restore listener: " + rollback ); }
                    }
                    SetServerMessage( $"无法监听端口 {port}：{ex.Message}",
                        $"Could not listen on port {port}: {ex.Message}" );
                    Log( serverMessageEnglish );
                    return false;
                }

                server = replacement;
                if ( previous != null ) previous.Stop();
                Log( $"TCP server listening on port {port}." );
            }

            settings.serverPort = port;
            settings.allowRemoteConnections = allowRemoteInput;
            settings.accessToken = token;
            serverPortInput = port.ToString( CultureInfo.InvariantCulture );
            if ( server != null && server.IsRunning )
            {
                SetServerMessage( $"正在监听 {address}:{port}，可在 MCP 使用 configure_connection 更新连接。",
                    $"Listening at {address}:{port}. Use configure_connection in MCP to update the connection." );
            }
            else
            {
                SetServerMessage( $"已选择端口 {port}，点击“启动服务”开始监听。",
                    $"Port {port} selected. Click Start Server to listen." );
            }
            SaveSettings();
            return true;
        }

        static void StopServer()
        {
            if ( server != null )
            {
                server.Stop();
                server = null;
            }
            SetServerMessage( "服务已停止。", "Server stopped." );
        }

        public static void Log( String str )
        {
            mod.Logger.Log( str );
        }

    }

    public class Settings : UnityModManager.ModSettings
    {
        public int serverPort = 9999;
        public bool autoStartServer = true;
        public bool useChinese = true;
        public bool allowRemoteConnections = false;
        public string accessToken = "";

        public override void Save( UnityModManager.ModEntry modEntry )
        {
            Save( this, modEntry );
        }
    }

}
