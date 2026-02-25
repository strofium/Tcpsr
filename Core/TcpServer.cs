using System.Net;
using System.Net.Sockets;
using StandRiseServer.Core;

namespace StandRiseServer;

public class TcpServer
{
    private readonly int _port;
    private readonly ProtobufHandler _handler;
    private readonly SessionManager _sessionManager;
    private readonly DatabaseService _database;
    private TcpListener? _listener;
    private readonly List<TcpClient> _clients = new();

    public int Port => _port;

    public TcpServer(int port, ProtobufHandler handler, SessionManager sessionManager, DatabaseService database)
    {
        _port = port;
        _handler = handler;
        _sessionManager = sessionManager;
        _database = database;
    }

    public async Task StartAsync()
    {
        _listener = new TcpListener(IPAddress.Any, _port);
        _listener.Start();
        
        Console.WriteLine($"🚀 Server listening on port {_port}...");
        Console.WriteLine($"🌐 Server endpoint: {_listener.LocalEndpoint}");

        while (true)
        {
            try
            {
                Console.WriteLine("⏳ Waiting for client connections...");
                var client = await _listener.AcceptTcpClientAsync();
                _clients.Add(client);
                
                var clientEndpoint = client.Client.RemoteEndPoint?.ToString() ?? "Unknown";
                Console.WriteLine($"🎯 Client connected: {clientEndpoint}");
                Console.WriteLine($"📊 Total clients: {_clients.Count}");
                
                // Handle client in background
                _ = Task.Run(() => HandleClientAsync(client));
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Error accepting client: {ex.Message}");
                Console.WriteLine($"📍 Stack trace: {ex.StackTrace}");
            }
        }
    }

    private async Task HandleClientAsync(TcpClient client)
    {
        var clientEndpoint = client.Client.RemoteEndPoint?.ToString() ?? "Unknown";
        Console.WriteLine($"🔗 Starting to handle client: {clientEndpoint}");
        
        try
        {
            var stream = client.GetStream();
            var buffer = new byte[8192];
            var messageBuffer = new List<byte>(); // Buffer to accumulate partial messages

            while (client.Connected)
            {
                    var bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length);
                
                if (bytesRead == 0)
                {
                    Console.WriteLine($"📡 Client {clientEndpoint} disconnected");
                    break;
                }

                Console.WriteLine($"📡 Received {bytesRead} bytes");
                
                // Add received data to message buffer
                for (int i = 0; i < bytesRead; i++)
                {
                    messageBuffer.Add(buffer[i]);
                }

                // Process all complete messages in the buffer
                while (messageBuffer.Count >= 4)
                {
                    // Read message length (first 4 bytes, big endian)
                    var lengthBytes = messageBuffer.Take(4).ToArray();
                    if (BitConverter.IsLittleEndian)
                        Array.Reverse(lengthBytes);
                    var messageLength = BitConverter.ToInt32(lengthBytes, 0);

                    // Check if we have the complete message
                    var totalMessageSize = 4 + messageLength;
                    if (messageBuffer.Count < totalMessageSize)
                    {
                        Console.WriteLine($"📦 Partial message: have {messageBuffer.Count} bytes, need {totalMessageSize} bytes");
                        break; // Wait for more data
                    }

                    // Extract complete message
                    var messageData = messageBuffer.Take(totalMessageSize).ToArray();
                    messageBuffer.RemoveRange(0, totalMessageSize);

                    // Process message silently

                    // Handle the message
                    await _handler.HandleRequestAsync(client, messageData);
                }

                // Buffer management handled silently
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Error handling client {clientEndpoint}: {ex.Message}");
            Console.WriteLine($"📍 Stack trace: {ex.StackTrace}");
        }
        finally
        {
            Console.WriteLine($"🧹 Cleaning up client {clientEndpoint}...");
            
            // Clean up session
            var session = _sessionManager.GetSessionByClient(client);
            if (session != null)
            {
                Console.WriteLine($"💾 Saving session data for {session.Token}...");
                // Save time in game
                try
                {
                    await _database.SaveTimeInGameAsync(session.Token, session.TimeInGame);
                    Console.WriteLine($"✅ Session data saved for {session.Token}");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"❌ Error saving time in game: {ex.Message}");
                }

                _sessionManager.RemoveSession(session.Token);
            }

            // Очищаем подписки клиента
            _sessionManager.CleanupClientSubscriptions(client);

            _clients.Remove(client);
            client.Close();
            client.Dispose();
            
            Console.WriteLine($"🔌 Client {clientEndpoint} disconnected and cleaned up");
        }
    }

    public void Stop()
    {
        _listener?.Stop();
        
        foreach (var client in _clients.ToList())
        {
            client.Close();
        }
        
        _clients.Clear();
    }
}
