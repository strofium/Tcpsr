using System.Collections.Concurrent;
using StandRiseServer.Core;

namespace StandRiseServer.GameServer;

/// <summary>
/// Сервер для обычных (казуальных) игр
/// </summary>
public class CasualGameServer
{
    private readonly DatabaseService _database;
    private readonly ConcurrentDictionary<string, CasualMatch> _activeMatches = new();
    private readonly ConcurrentDictionary<string, CasualQueue> _playerQueue = new();
    private readonly string _serverIp;
    private readonly int _serverPort;
    private bool _running;

    public CasualGameServer(DatabaseService database, string serverIp = "127.0.0.1", int serverPort = 5055)
    {
        _database = database;
        _serverIp = serverIp;
        _serverPort = serverPort;
    }

    public void Start()
    {
        _running = true;
        _ = ProcessCasualQueueAsync();
        Console.WriteLine($"🎯 Casual Game Server started on {_serverIp}:{_serverPort}");
    }

    public void Stop()
    {
        _running = false;
        Console.WriteLine("🎯 Casual Game Server stopped");
    }

    /// <summary>
    /// Добавить игрока в очередь обычных игр
    /// </summary>
    public CasualMatchmakingResult EnqueuePlayer(string playerId, string playerName, string gameMode, string region, string? mapName = null)
    {
        var queueEntry = new CasualQueue
        {
            PlayerId = playerId,
            PlayerName = playerName,
            GameMode = gameMode,
            Region = region,
            MapName = mapName,
            EnqueuedAt = DateTime.UtcNow
        };

        _playerQueue[playerId] = queueEntry;
        Console.WriteLine($"🎯 Player {playerName} queued for {gameMode} in {region}");

        return new CasualMatchmakingResult
        {
            Status = CasualMatchStatus.Searching,
            Message = "Searching for match...",
            EstimatedWaitTime = CalculateEstimatedWaitTime(gameMode, region)
        };
    }

    /// <summary>
    /// Убрать игрока из очереди
    /// </summary>
    public void DequeuePlayer(string playerId)
    {
        _playerQueue.TryRemove(playerId, out _);
        Console.WriteLine($"🎯 Player {playerId} removed from casual queue");
    }

    /// <summary>
    /// Получить статус игрока в очереди
    /// </summary>
    public CasualMatchmakingResult GetPlayerStatus(string playerId)
    {
        if (_playerQueue.TryGetValue(playerId, out var queueEntry))
        {
            var waitTime = (DateTime.UtcNow - queueEntry.EnqueuedAt).TotalSeconds;
            return new CasualMatchmakingResult
            {
                Status = CasualMatchStatus.Searching,
                Message = $"Searching... ({waitTime:F0}s)",
                EstimatedWaitTime = Math.Max(0, queueEntry.EstimatedWaitTime - (int)waitTime)
            };
        }

        // Проверяем активные матчи
        foreach (var match in _activeMatches.Values)
        {
            if (match.Players.Any(p => p.PlayerId == playerId))
            {
                return new CasualMatchmakingResult
                {
                    Status = CasualMatchStatus.MatchFound,
                    MatchId = match.MatchId,
                    ServerIp = _serverIp,
                    ServerPort = _serverPort,
                    Message = "Match found!"
                };
            }
        }

        return new CasualMatchmakingResult
        {
            Status = CasualMatchStatus.NotInQueue,
            Message = "Not in queue"
        };
    }

    /// <summary>
    /// Обработка очереди обычных игр
    /// </summary>
    private async Task ProcessCasualQueueAsync()
    {
        while (_running)
        {
            try
            {
                await MatchCasualPlayersAsync();
                await Task.Delay(1000); // Проверяем каждую секунду (быстрее чем ранговые)
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Casual matchmaking error: {ex.Message}");
            }
        }
    }

    /// <summary>
    /// Поиск матчей для обычных игроков
    /// </summary>
    private async Task MatchCasualPlayersAsync()
    {
        // Группируем игроков по режиму игры и региону
        var groups = _playerQueue.Values
            .GroupBy(p => new { p.GameMode, p.Region })
            .Where(g => g.Count() >= GetMinPlayersForMode(g.Key.GameMode));

        foreach (var group in groups)
        {
            var players = group.OrderBy(p => p.EnqueuedAt).ToList();
            var maxPlayers = GetMaxPlayersForMode(group.Key.GameMode);
            
            // Создаем матчи
            for (int i = 0; i < players.Count; i += maxPlayers)
            {
                var matchPlayers = players.Skip(i).Take(maxPlayers).ToList();
                if (matchPlayers.Count >= GetMinPlayersForMode(group.Key.GameMode))
                {
                    await CreateCasualMatchAsync(matchPlayers, group.Key.GameMode, group.Key.Region);
                }
            }
        }
    }

    /// <summary>
    /// Создание обычного матча
    /// </summary>
    private async Task CreateCasualMatchAsync(List<CasualQueue> queuePlayers, string gameMode, string region)
    {
        var matchId = $"{gameMode}_{region}_{Guid.NewGuid():N}";
        
        var match = new CasualMatch
        {
            MatchId = matchId,
            GameMode = gameMode,
            Region = region,
            ServerIp = _serverIp,
            ServerPort = _serverPort,
            CreatedAt = DateTime.UtcNow,
            Status = CasualMatchStatus.MatchFound,
            MapName = SelectMapForMode(gameMode, queuePlayers.FirstOrDefault()?.MapName)
        };

        // Добавляем игроков в матч
        foreach (var queuePlayer in queuePlayers)
        {
            var player = new CasualPlayer
            {
                PlayerId = queuePlayer.PlayerId,
                PlayerName = queuePlayer.PlayerName,
                Team = match.Players.Count % 2 == 0 ? "CT" : "T" // Чередуем команды
            };
            
            match.Players.Add(player);
            
            // Убираем из очереди
            _playerQueue.TryRemove(queuePlayer.PlayerId, out _);
        }

        _activeMatches[matchId] = match;

        Console.WriteLine($"🎯 Casual match created: {matchId} ({gameMode}) with {match.Players.Count} players in {region}");
        Console.WriteLine($"🎯 Map: {match.MapName}");
        
        await Task.CompletedTask;
    }

    /// <summary>
    /// Выбрать карту для режима игры
    /// </summary>
    private string SelectMapForMode(string gameMode, string? preferredMap = null)
    {
        if (!string.IsNullOrEmpty(preferredMap))
            return preferredMap;

        return gameMode switch
        {
            "deathmatch" => GetRandomMap(new[] { "de_dust2", "de_mirage", "de_inferno", "de_cache" }),
            "defuse" => GetRandomMap(new[] { "de_dust2", "de_mirage", "de_inferno", "de_cache", "de_train" }),
            "arms_race" => GetRandomMap(new[] { "ar_shoots", "ar_baggage", "ar_monastery" }),
            _ => "de_dust2"
        };
    }

    /// <summary>
    /// Получить случайную карту из списка
    /// </summary>
    private string GetRandomMap(string[] maps)
    {
        var random = new Random();
        return maps[random.Next(maps.Length)];
    }

    /// <summary>
    /// Минимальное количество игроков для режима
    /// </summary>
    private int GetMinPlayersForMode(string gameMode)
    {
        return gameMode switch
        {
            "deathmatch" => 2,
            "defuse" => 2,
            "arms_race" => 2,
            _ => 2
        };
    }

    /// <summary>
    /// Максимальное количество игроков для режима
    /// </summary>
    private int GetMaxPlayersForMode(string gameMode)
    {
        return gameMode switch
        {
            "deathmatch" => 10,
            "defuse" => 10,
            "arms_race" => 8,
            _ => 10
        };
    }

    /// <summary>
    /// Рассчитать примерное время ожидания
    /// </summary>
    private int CalculateEstimatedWaitTime(string gameMode, string region)
    {
        var playersInQueue = _playerQueue.Values.Count(p => p.GameMode == gameMode && p.Region == region);
        var minPlayers = GetMinPlayersForMode(gameMode);
        
        if (playersInQueue >= minPlayers)
            return 5; // Быстрый матчмейкинг если достаточно игроков
        
        return 15 + (minPlayers - playersInQueue) * 5; // Базовое время + время на недостающих игроков
    }
}

/// <summary>
/// Игрок в очереди обычных игр
/// </summary>
public class CasualQueue
{
    public string PlayerId { get; set; } = "";
    public string PlayerName { get; set; } = "";
    public string GameMode { get; set; } = "";
    public string Region { get; set; } = "";
    public string? MapName { get; set; }
    public DateTime EnqueuedAt { get; set; }
    public int EstimatedWaitTime { get; set; }
}

/// <summary>
/// Обычный матч
/// </summary>
public class CasualMatch
{
    public string MatchId { get; set; } = "";
    public string GameMode { get; set; } = "";
    public string Region { get; set; } = "";
    public string ServerIp { get; set; } = "";
    public int ServerPort { get; set; }
    public string MapName { get; set; } = "";
    public DateTime CreatedAt { get; set; }
    public CasualMatchStatus Status { get; set; }
    public List<CasualPlayer> Players { get; set; } = new();
}

/// <summary>
/// Игрок в обычном матче
/// </summary>
public class CasualPlayer
{
    public string PlayerId { get; set; } = "";
    public string PlayerName { get; set; } = "";
    public string Team { get; set; } = ""; // CT или T
}

/// <summary>
/// Результат обычного матчмейкинга
/// </summary>
public class CasualMatchmakingResult
{
    public CasualMatchStatus Status { get; set; }
    public string? MatchId { get; set; }
    public string? ServerIp { get; set; }
    public int ServerPort { get; set; }
    public string Message { get; set; } = "";
    public int EstimatedWaitTime { get; set; }
}

/// <summary>
/// Статус обычного матча
/// </summary>
public enum CasualMatchStatus
{
    NotInQueue,
    Searching,
    MatchFound,
    InGame,
    Completed,
    Cancelled
}