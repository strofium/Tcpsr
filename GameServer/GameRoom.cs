using System.Collections.Concurrent;

namespace StandRiseServer.GameServer;

public class GameRoom
{
    public string Id { get; }
    public string? MapName { get; set; }
    public string? GameMode { get; set; }
    public int MaxPlayers { get; set; } = 10;
    public GameState State { get; private set; } = GameState.Waiting;
    public DateTime CreatedAt { get; } = DateTime.UtcNow;
    
    private readonly ConcurrentDictionary<string, GameClient> _players = new();
    private readonly object _lock = new();

    public int PlayerCount => _players.Count;

    public GameRoom(string id)
    {
        Id = id;
    }

    public void AddPlayer(GameClient client)
    {
        lock (_lock)
        {
            if (_players.Count >= MaxPlayers)
            {
                throw new InvalidOperationException("Room is full");
            }
            
            _players[client.Id] = client;
            
            // Assign team (simple alternating)
            client.Team = _players.Count % 2;
        }
    }

    public void RemovePlayer(GameClient client)
    {
        _players.TryRemove(client.Id, out _);
        
        // If room is empty, mark for cleanup
        if (_players.IsEmpty)
        {
            State = GameState.Finished;
        }
    }

    public bool AllPlayersReady()
    {
        return _players.Values.All(p => p.IsReady) && _players.Count >= 2;
    }

    public async Task StartGameAsync()
    {
        State = GameState.Starting;
        Console.WriteLine($"🎮 Room {Id}: Starting game with {_players.Count} players");
        
        // Send game start message to all players
        var startMessage = CreateGameStartMessage();
        await BroadcastToAllAsync(startMessage);
        
        State = GameState.InProgress;
    }

    public async Task BroadcastAsync(byte[] data, string? excludeClientId = null)
    {
        var tasks = _players.Values
            .Where(p => p.Id != excludeClientId)
            .Select(p => p.SendAsync(data));
        
        await Task.WhenAll(tasks);
    }

    public async Task BroadcastToAllAsync(byte[] data)
    {
        var tasks = _players.Values.Select(p => p.SendAsync(data));
        await Task.WhenAll(tasks);
    }

    private byte[] CreateGameStartMessage()
    {
        // Simple game start message
        var message = new byte[12];
        BitConverter.GetBytes(200).CopyTo(message, 0); // Message type: Game Start
        BitConverter.GetBytes(_players.Count).CopyTo(message, 4);
        BitConverter.GetBytes((int)State).CopyTo(message, 8);
        return message;
    }

    public IEnumerable<GameClient> GetPlayers() => _players.Values;
    
    public GameClient? GetPlayer(string clientId)
    {
        _players.TryGetValue(clientId, out var client);
        return client;
    }
}

public enum GameState
{
    Waiting,
    Starting,
    InProgress,
    Finished
}
