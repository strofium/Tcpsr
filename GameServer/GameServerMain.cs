using System.Net;
using System.Net.Sockets;
using System.Collections.Concurrent;

namespace StandRiseServer.GameServer;

public class GameServerMain
{
    private readonly int _port;
    private readonly string _region;
    private TcpListener? _listener;
    private bool _running;
    
    private readonly ConcurrentDictionary<string, GameRoom> _rooms = new();
    private readonly ConcurrentDictionary<string, GameClient> _clients = new();

    public GameServerMain(int port, string region = "test")
    {
        _port = port;
        _region = region;
    }

    public async Task StartAsync()
    {
        _listener = new TcpListener(IPAddress.Any, _port);
        _listener.Start();
        _running = true;

        Console.WriteLine($"🎮 Game Server started on port {_port} (Region: {_region})");
        Console.WriteLine($"🎮 Waiting for game connections...");

        while (_running)
        {
            try
            {
                var client = await _listener.AcceptTcpClientAsync();
                _ = HandleClientAsync(client);
            }
            catch (Exception ex) when (_running)
            {
                Console.WriteLine($"❌ Accept error: {ex.Message}");
            }
        }
    }

    private async Task HandleClientAsync(TcpClient tcpClient)
    {
        var clientId = Guid.NewGuid().ToString();
        var gameClient = new GameClient(clientId, tcpClient);
        _clients[clientId] = gameClient;

        Console.WriteLine($"🎮 Client connected: {clientId}");

        try
        {
            var stream = tcpClient.GetStream();
            var buffer = new byte[4096];

            while (tcpClient.Connected && _running)
            {
                var bytesRead = await stream.ReadAsync(buffer);
                if (bytesRead == 0) break;

                await ProcessMessageAsync(gameClient, buffer.Take(bytesRead).ToArray());
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Client error: {ex.Message}");
        }
        finally
        {
            _clients.TryRemove(clientId, out _);
            gameClient.Dispose();
            Console.WriteLine($"🎮 Client disconnected: {clientId}");
        }
    }

    private async Task ProcessMessageAsync(GameClient client, byte[] data)
    {
        // Parse game protocol messages
        // This is a simplified implementation
        if (data.Length < 4) return;

        var messageType = BitConverter.ToInt32(data, 0);
        var payload = data.Skip(4).ToArray();

        switch (messageType)
        {
            case 1: // Join Room
                await HandleJoinRoomAsync(client, payload);
                break;
            case 2: // Leave Room
                await HandleLeaveRoomAsync(client);
                break;
            case 3: // Game Event
                await HandleGameEventAsync(client, payload);
                break;
            case 4: // Player Ready
                await HandlePlayerReadyAsync(client);
                break;
            default:
                Console.WriteLine($"🎮 Unknown message type: {messageType}");
                break;
        }
    }

    private async Task HandleJoinRoomAsync(GameClient client, byte[] payload)
    {
        var roomId = System.Text.Encoding.UTF8.GetString(payload);
        
        if (!_rooms.TryGetValue(roomId, out var room))
        {
            room = new GameRoom(roomId);
            _rooms[roomId] = room;
            Console.WriteLine($"🎮 Created room: {roomId}");
        }

        room.AddPlayer(client);
        client.CurrentRoom = room;
        
        Console.WriteLine($"🎮 Player {client.Id} joined room {roomId}");
        await SendJoinConfirmationAsync(client, room);
    }

    private async Task HandleLeaveRoomAsync(GameClient client)
    {
        if (client.CurrentRoom != null)
        {
            client.CurrentRoom.RemovePlayer(client);
            Console.WriteLine($"🎮 Player {client.Id} left room {client.CurrentRoom.Id}");
            client.CurrentRoom = null;
        }
        await Task.CompletedTask;
    }

    private async Task HandleGameEventAsync(GameClient client, byte[] payload)
    {
        if (client.CurrentRoom == null) return;
        
        // Broadcast event to all players in room
        await client.CurrentRoom.BroadcastAsync(payload, client.Id);
    }

    private async Task HandlePlayerReadyAsync(GameClient client)
    {
        if (client.CurrentRoom == null) return;
        
        client.IsReady = true;
        Console.WriteLine($"🎮 Player {client.Id} is ready");
        
        // Check if all players are ready
        if (client.CurrentRoom.AllPlayersReady())
        {
            await client.CurrentRoom.StartGameAsync();
        }
    }

    private async Task SendJoinConfirmationAsync(GameClient client, GameRoom room)
    {
        var response = new byte[8];
        BitConverter.GetBytes(100).CopyTo(response, 0); // Message type: Join OK
        BitConverter.GetBytes(room.PlayerCount).CopyTo(response, 4);
        
        await client.SendAsync(response);
    }

    public void Stop()
    {
        _running = false;
        _listener?.Stop();
        
        foreach (var client in _clients.Values)
        {
            client.Dispose();
        }
        _clients.Clear();
        _rooms.Clear();
    }
}
