using System.Collections.Concurrent;
using StandRiseServer.Core;

namespace StandRiseServer.GameServer;

public class MatchmakingService
{
    private readonly DatabaseService _database;
    private readonly ConcurrentDictionary<string, MatchmakingRequest> _queue = new();
    private readonly ConcurrentDictionary<string, GameRoomInfo> _activeRooms = new();
    private readonly string _gameServerIp;
    private readonly int _gameServerPort;
    private bool _running;

    public MatchmakingService(DatabaseService database, string gameServerIp = "127.0.0.1", int gameServerPort = 5055)
    {
        _database = database;
        _gameServerIp = gameServerIp;
        _gameServerPort = gameServerPort;
    }

    public void Start()
    {
        _running = true;
        _ = ProcessQueueAsync();
        Console.WriteLine("🎯 Matchmaking service started");
    }

    public void Stop()
    {
        _running = false;
    }

    public MatchmakingResult EnqueuePlayer(string playerId, string playerName, MatchmakingParams parameters)
    {
        var request = new MatchmakingRequest
        {
            PlayerId = playerId,
            PlayerName = playerName,
            GameMode = parameters.GameMode,
            MapName = parameters.MapName,
            Region = parameters.Region,
            Mmr = parameters.Mmr,
            EnqueuedAt = DateTime.UtcNow
        };

        _queue[playerId] = request;
        Console.WriteLine($"🎯 Player {playerName} queued for {parameters.GameMode}");

        return new MatchmakingResult
        {
            Status = MatchmakingStatus.Searching,
            Message = "Searching for match..."
        };
    }

    public void DequeuePlayer(string playerId)
    {
        _queue.TryRemove(playerId, out _);
        Console.WriteLine($"🎯 Player {playerId} removed from queue");
    }

    public MatchmakingResult GetStatus(string playerId)
    {
        if (_queue.TryGetValue(playerId, out var request))
        {
            if (request.FoundRoom != null)
            {
                return new MatchmakingResult
                {
                    Status = MatchmakingStatus.Found,
                    RoomId = request.FoundRoom.RoomId,
                    ServerIp = _gameServerIp,
                    ServerPort = _gameServerPort,
                    Message = "Match found!"
                };
            }

            var waitTime = (DateTime.UtcNow - request.EnqueuedAt).TotalSeconds;
            return new MatchmakingResult
            {
                Status = MatchmakingStatus.Searching,
                Message = $"Searching... ({waitTime:F0}s)"
            };
        }

        return new MatchmakingResult
        {
            Status = MatchmakingStatus.NotInQueue,
            Message = "Not in queue"
        };
    }

    private async Task ProcessQueueAsync()
    {
        while (_running)
        {
            try
            {
                await MatchPlayersAsync();
                await Task.Delay(1000); // Check every second
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Matchmaking error: {ex.Message}");
            }
        }
    }

    private async Task MatchPlayersAsync()
    {
        // Group players by game mode and region
        var groups = _queue.Values
            .Where(r => r.FoundRoom == null)
            .GroupBy(r => new { r.GameMode, r.Region });

        foreach (var group in groups)
        {
            var players = group.OrderBy(p => p.EnqueuedAt).ToList();
            
            // Need at least 2 players for a match (can be configured per mode)
            var minPlayers = GetMinPlayersForMode(group.Key.GameMode);
            
            if (players.Count >= minPlayers)
            {
                var matchPlayers = players.Take(GetMaxPlayersForMode(group.Key.GameMode)).ToList();
                await CreateMatchAsync(matchPlayers, group.Key.GameMode, group.Key.Region);
            }
        }
    }

    private async Task CreateMatchAsync(List<MatchmakingRequest> players, string gameMode, string region)
    {
        var roomId = $"{gameMode}_{region}_{Guid.NewGuid():N}";
        
        var roomInfo = new GameRoomInfo
        {
            RoomId = roomId,
            GameMode = gameMode,
            Region = region,
            ServerIp = _gameServerIp,
            ServerPort = _gameServerPort,
            CreatedAt = DateTime.UtcNow
        };

        _activeRooms[roomId] = roomInfo;

        // Assign room to all matched players
        foreach (var player in players)
        {
            player.FoundRoom = roomInfo;
            roomInfo.PlayerIds.Add(player.PlayerId);
        }

        Console.WriteLine($"🎯 Match created: {roomId} with {players.Count} players");
        await Task.CompletedTask;
    }

    private int GetMinPlayersForMode(string gameMode)
    {
        return gameMode switch
        {
            "deathmatch" => 2,
            "defuse" => 2,
            "ranked" => 2,
            "arms_race" => 2,
            _ => 2
        };
    }

    private int GetMaxPlayersForMode(string gameMode)
    {
        return gameMode switch
        {
            "deathmatch" => 10,
            "defuse" => 10,
            "ranked" => 10,
            "arms_race" => 8,
            _ => 10
        };
    }
}

public class MatchmakingRequest
{
    public string PlayerId { get; set; } = "";
    public string PlayerName { get; set; } = "";
    public string GameMode { get; set; } = "deathmatch";
    public string? MapName { get; set; }
    public string Region { get; set; } = "test";
    public int Mmr { get; set; }
    public DateTime EnqueuedAt { get; set; }
    public GameRoomInfo? FoundRoom { get; set; }
}

public class MatchmakingParams
{
    public string GameMode { get; set; } = "deathmatch";
    public string? MapName { get; set; }
    public string Region { get; set; } = "test";
    public int Mmr { get; set; }
}

public class MatchmakingResult
{
    public MatchmakingStatus Status { get; set; }
    public string? RoomId { get; set; }
    public string? ServerIp { get; set; }
    public int ServerPort { get; set; }
    public string Message { get; set; } = "";
}

public enum MatchmakingStatus
{
    NotInQueue,
    Searching,
    Found,
    Cancelled
}

public class GameRoomInfo
{
    public string RoomId { get; set; } = "";
    public string GameMode { get; set; } = "";
    public string Region { get; set; } = "";
    public string ServerIp { get; set; } = "";
    public int ServerPort { get; set; }
    public DateTime CreatedAt { get; set; }
    public List<string> PlayerIds { get; set; } = new();
}
