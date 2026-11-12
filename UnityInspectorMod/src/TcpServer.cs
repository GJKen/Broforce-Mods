using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;

namespace Unity_Inspector_Mod
{
    public class TcpServer
    {
        private TcpListener listener;
        private Thread listenerThread;
        private volatile bool isRunning;
        private readonly int port;
        private readonly IPAddress listenAddress;
        private volatile string accessToken;
        public const int MaxClients = 8;
        public const int MaxRequestCharacters = 1024 * 1024;
        private readonly List<ClientHandler> clients = new List<ClientHandler>();
        private readonly MessageHandler messageHandler;

        public bool IsRunning => isRunning;
        public int Port => port;
        public string ListenAddress => listenAddress.ToString();
        public int ConnectedClients
        {
            get { lock ( clients ) return clients.Count; }
        }

        public TcpServer( int port, string address = "127.0.0.1", string token = "" )
        {
            this.port = port;
            this.listenAddress = IPAddress.Parse( address );
            this.accessToken = token ?? "";
            if ( !IPAddress.IsLoopback( listenAddress ) && string.IsNullOrEmpty( accessToken ) )
                throw new ArgumentException( "Remote listening requires an access token" );
            this.messageHandler = new MessageHandler();
        }

        public void Start()
        {
            if ( isRunning ) return;

            try
            {
                listener = new TcpListener( listenAddress, port );
                listener.ExclusiveAddressUse = true;
                listener.Start();
                isRunning = true;

                var activeListener = listener;
                listenerThread = new Thread( () => ListenForClients( activeListener ) );
                listenerThread.IsBackground = true;
                listenerThread.Start();
            }
            catch ( Exception ex )
            {
                Stop();
                Main.Log( $"Failed to start TcpListener: {ex}" );
                throw;
            }
        }

        public void Stop()
        {
            isRunning = false;

            if ( listener != null )
            {
                listener.Stop();
                listener = null;
            }

            if ( listenerThread != null )
            {
                if ( listenerThread != Thread.CurrentThread &&
                    (listenerThread.ThreadState & ThreadState.Unstarted) == 0 )
                    listenerThread.Join( 1000 );
                listenerThread = null;
            }

            ClientHandler[] snapshot;
            lock ( clients )
            {
                snapshot = clients.ToArray();
                clients.Clear();
            }
            foreach ( var client in snapshot ) client.Disconnect();
        }

        private void ListenForClients( TcpListener activeListener )
        {
            while ( isRunning )
            {
                TcpClient tcpClient = null;
                ClientHandler client = null;
                try
                {
                    tcpClient = activeListener.AcceptTcpClient();
                    lock ( clients )
                    {
                        if ( !isRunning || clients.Count >= MaxClients )
                        {
                            tcpClient.Close();
                            if ( !isRunning ) return;
                            continue;
                        }
                        client = new ClientHandler( tcpClient, this );
                        clients.Add( client );
                        // Register before starting the receive thread: a client
                        // may disconnect as soon as it is accepted.
                        client.Start();
                    }
                }
                catch ( Exception ex )
                {
                    if ( client != null ) client.Disconnect();
                    else if ( tcpClient != null ) tcpClient.Close();
                    if ( isRunning )
                    {
                        Main.Log( $"Error accepting client: {ex.Message}" );
                        Main.Log( $"Exception details: {ex}" );
                    }
                }
            }
        }

        internal void RemoveClient( ClientHandler client )
        {
            lock ( clients ) clients.Remove( client );
        }

        public void UpdateToken( string token )
        {
            token = token ?? "";
            if ( !IPAddress.IsLoopback( listenAddress ) && string.IsNullOrEmpty( token ) )
                throw new ArgumentException( "Remote listening requires an access token" );
            if ( accessToken == token ) return;
            accessToken = token;
            ClientHandler[] snapshot;
            lock ( clients ) snapshot = clients.ToArray();
            foreach ( var client in snapshot ) client.Disconnect();
        }

        internal string ProcessMessage( string message, Func<bool> cancelled )
        {
            return messageHandler.HandleMessage( message, accessToken, cancelled );
        }
    }

    internal class ClientHandler
    {
        private readonly TcpClient client;
        private readonly TcpServer server;
        private readonly Thread receiveThread;
        private readonly NetworkStream stream;
        private readonly StreamReader reader;
        private readonly StreamWriter writer;
        private int disconnected;

        public ClientHandler( TcpClient client, TcpServer server )
        {
            this.client = client;
            this.server = server;
            client.NoDelay = true;
            client.ReceiveTimeout = 120000;
            client.SendTimeout = 10000;
            this.stream = client.GetStream();
            this.reader = new StreamReader( stream, new UTF8Encoding( false ) ); // false = no BOM
            this.writer = new StreamWriter( stream, new UTF8Encoding( false ) ) { AutoFlush = true };
            receiveThread = new Thread( ReceiveMessages );
            receiveThread.IsBackground = true;
        }

        public void Start()
        {
            receiveThread.Start();
        }

        private void ReceiveMessages()
        {
            while ( Interlocked.CompareExchange( ref disconnected, 0, 0 ) == 0 )
            {
                try
                {
                    string message = ReadBoundedLine();
                    if ( message != null )
                    {
                        // Process message from client
                        if ( Encoding.UTF8.GetByteCount( message ) > TcpServer.MaxRequestCharacters )
                            throw new IOException( "Request exceeds 1 MiB limit" );
                        string response = server.ProcessMessage( message, IsUnavailable );
                        
                        writer.WriteLine( response );
                        
                    }
                    else
                    {
                        break;
                    }
                }
                catch ( Exception ex )
                {
                    if ( Interlocked.CompareExchange( ref disconnected, 0, 0 ) == 0 && server.IsRunning )
                        Main.Log( $"[ClientHandler] Error receiving message: {ex.Message}" );
                    break;
                }
            }

            Disconnect();
        }

        private string ReadBoundedLine()
        {
            var message = new StringBuilder();
            while ( true )
            {
                int character = reader.Read();
                if ( character < 0 ) return null;
                if ( character == '\n' ) return message.ToString().TrimEnd( '\r' );
                if ( message.Length >= TcpServer.MaxRequestCharacters )
                    throw new IOException( "Request exceeds 1 MiB limit" );
                message.Append( (char)character );
            }
        }

        private bool IsUnavailable()
        {
            if ( !server.IsRunning || Interlocked.CompareExchange( ref disconnected, 0, 0 ) != 0 ) return true;
            try { return client.Client.Poll( 0, SelectMode.SelectRead ) && client.Client.Available == 0; }
            catch { return true; }
        }

        public void Disconnect()
        {
            if ( Interlocked.Exchange( ref disconnected, 1 ) != 0 ) return;

            // Close the socket first to unblock ReadLine on the receive thread.
            try { client.Close(); } catch { }
            try { reader.Close(); } catch { }
            try { writer.Close(); } catch { }
            try { stream.Close(); } catch { }

            server.RemoveClient( this );
        }
    }
}
