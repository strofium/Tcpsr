using System.Collections.Concurrent;
using StandRiseServer.Core;

namespace StandRiseServer.GameServer;

/// <summary>
/// Отдельный сервер для ранговых игр
/// </summary>
public class RankedGameServer
{
    private readonly DatabaseService _database;
    private readonly ConcurrentDictionary<string, RankedMatch> _activeMatches = new();
    private readonly ConcurrentDictionary<string, RankedQueue> _playerQueue = new();
    private readonly string _serverIp;
    private readonly int _serverPort;
    private bool _running;

    public RankedGameServer(DatabaseService database, string serverIp = "127.0.0.1", int serverPort = 5056)
    {
        _database = database;
        _serverIp = serverIp;
        _serverPort = serverPort;
    }

    public void Start()
    {
        _running = true;
        _ = ProcessRankedQueueAsync();
        Console.WriteLine($"🏆 Ranked Game Server started on {_serverIp}:{_serverPort}");
    }

    public void Stop()
    {
        _running = false;
        Console.WriteLine("🏆 Ranked Game Server stopped");
    }

    /// <summary>
    /// Добавить игрока в очередь ранговых игр
    /// </summary>
    public RankedMatchmakingResult EnqueuePlayer(string playerId, string playerName, int mmr, string region)
    {
        var queueEntry = new RankedQueue
        {
            PlayerId = playerId,
            PlayerName = playerName,
            Mmr = mmr,
            Region = region,
            EnqueuedAt = DateTime.UtcNow
        };

        _playerQueue[playerId] = queueEntry;
        Console.WriteLine($"🏆 Player {playerName} (MMR: {mmr}) queued for ranked in {region}");

        return new RankedMatchmakingResult
        {
            Status = RankedMatchStatus.Searching,
            Message = "Searching for ranked match...",
            EstimatedWaitTime = CalculateEstimatedWaitTime(mmr, region)
        };
    }

    /// <summary>
    /// Убрать игрока из очереди
    /// </summary>
    public void DequeuePlayer(string playerId)
    {
        _playerQueue.TryRemove(playerId, out _);
        Console.WriteLine($"🏆 Player {playerId} removed from ranked queue");
    }

    /// <summary>
    /// Получить статус игрока в очереди
    /// </summary>
    public RankedMatchmakingResult GetPlayerStatus(string playerId)
    {
        if (_playerQueue.TryGetValue(playerId, out var queueEntry))
        {
            var waitTime = (DateTime.UtcNow - queueEntry.EnqueuedAt).TotalSeconds;
            return new RankedMatchmakingResult
            {
                Status = RankedMatchStatus.Searching,
                Message = $"Searching... ({waitTime:F0}s)",
                EstimatedWaitTime = Math.Max(0, queueEntry.EstimatedWaitTime - (int)waitTime)
            };
        }

        // Проверяем активные матчи
        foreach (var match in _activeMatches.Values)
        {
            if (match.Players.Any(p => p.PlayerId == playerId))
            {
                return new RankedMatchmakingResult
                {
                    Status = RankedMatchStatus.MatchFound,
                    MatchId = match.MatchId,
                    ServerIp = _serverIp,
                    ServerPort = _serverPort,
                    Message = "Match found!"
                };
            }
        }

        return new RankedMatchmakingResult
        {
            Status = RankedMatchStatus.NotInQueue,
            Message = "Not in ranked queue"
        };
    }

    /// <summary>
    /// Обработка очереди ранговых игр
    /// </summary>
    private async Task ProcessRankedQueueAsync()
    {
        while (_running)
        {
            try
            {
                await MatchRankedPlayersAsync();
                await Task.Delay(2000); // Проверяем каждые 2 секунды
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Ranked matchmaking error: {ex.Message}");
            }
        }
    }

    /// <summary>
    /// Поиск матчей для ранговых игроков
    /// </summary>
    private async Task MatchRankedPlayersAsync()
    {
        // Группируем игроков по регионам
        var regionGroups = _playerQueue.Values
            .GroupBy(p => p.Region)
            .Where(g => g.Count() >= 2); // Минимум 2 игрока для матча

        foreach (var regionGroup in regionGroups)
        {
            var players = regionGroup.OrderBy(p => p.EnqueuedAt).ToList();
            
            // Пытаемся создать матчи с учетом MMR
            await CreateRankedMatchesForRegion(players, regionGroup.Key);
        }
    }

    /// <summary>
    /// Создание ранковых матчей для региона
    /// </summary>
    private async Task CreateRankedMatchesForRegion(List<RankedQueue> players, string region)
    {
        // Сортируем по MMR для лучшего матчмейкинга
        players = players.OrderBy(p => p.Mmr).ToList();

        for (int i = 0; i < players.Count - 1; i += 10) // Матчи по 10 игроков
        {
            var matchPlayers = players.Skip(i).Take(10).ToList();
            
            if (matchPlayers.Count >= 2) // Минимум 2 игрока
            {
                // Проверяем совместимость MMR
                var minMmr = matchPlayers.Min(p => p.Mmr);
                var maxMmr = matchPlayers.Max(p => p.Mmr);
                var mmrDifference = maxMmr - minMmr;

                // Разрешаем матч если разница MMR не слишком большая
                if (mmrDifference <= GetMaxMmrDifference(matchPlayers.Average(p => p.Mmr)))
                {
                    await CreateRankedMatchAsync(matchPlayers, region);
                }
            }
        }
    }

    /// <summary>
    /// Создание рангового матча
    /// </summary>
    private async Task CreateRankedMatchAsync(List<RankedQueue> queuePlayers, string region)
    {
        var matchId = $"ranked_{region}_{Guid.NewGuid():N}";
        
        var match = new RankedMatch
        {
            MatchId = matchId,
            Region = region,
            ServerIp = _serverIp,
            ServerPort = _serverPort,
            CreatedAt = DateTime.UtcNow,
            Status = RankedMatchStatus.MatchFound
        };

        // Добавляем игроков в матч
        foreach (var queuePlayer in queuePlayers)
        {
            var player = new RankedPlayer
            {
                PlayerId = queuePlayer.PlayerId,
                PlayerName = queuePlayer.PlayerName,
                Mmr = queuePlayer.Mmr,
                Team = match.Players.Count < 5 ? "CT" : "T" // Первые 5 в CT, остальные в T
            };
            
            match.Players.Add(player);
            
            // Убираем из очереди
            _playerQueue.TryRemove(queuePlayer.PlayerId, out _);
        }

        _activeMatches[matchId] = match;

        Console.WriteLine($"🏆 Ranked match created: {matchId} with {match.Players.Count} players in {region}");
        Console.WriteLine($"🏆 Average MMR: {match.Players.Average(p => p.Mmr):F0}");
        
        await Task.CompletedTask;
    }

    /// <summary>
    /// Рассчитать максимальную разницу MMR для матча
    /// </summary>
    private int GetMaxMmrDifference(double averageMmr)
    {
        return averageMmr switch
        {
            < 1000 => 200,   // Новички - больше разброс
            < 2000 => 150,   // Средний уровень
            < 3000 => 100,   // Высокий уровень
            _ => 50          // Профессиональный уровень - строгий матчмейкинг
        };
    }

    /// <summary>
    /// Рассчитать примерное время ожидания
    /// </summary>
    private int CalculateEstimatedWaitTime(int mmr, string region)
    {
        var playersInRegion = _playerQueue.Values.Count(p => p.Region == region);
        var baseTime = 30; // Базовое время 30 секунд
        
        // Чем выше MMR, тем дольше ожидание
        var mmrMultiplier = mmr > 2500 ? 2.0 : 1.0;
        
        // Чем меньше игроков в регионе, тем дольше ожидание
        var regionMultiplier = playersInRegion < 5 ? 1.5 : 1.0;
        
        return (int)(baseTime * mmrMultiplier * regionMultiplier);
    }
}

/// <summary>
/// Игрок в очереди ранговых игр
/// </summary>
public class RankedQueue
{
    public string PlayerId { get; set; } = "";
    public string PlayerName { get; set; } = "";
    public int Mmr { get; set; }
    public string Region { get; set; } = "";
    public DateTime EnqueuedAt { get; set; }
    public int EstimatedWaitTime { get; set; }
}

/// <summary>
/// Ранговый матч
/// </summary>
public class RankedMatch
{
    public string MatchId { get; set; } = "";
    public string Region { get; set; } = "";
    public string ServerIp { get; set; } = "";
    public int ServerPort { get; set; }
    public DateTime CreatedAt { get; set; }
    public RankedMatchStatus Status { get; set; }
    public List<RankedPlayer> Players { get; set; } = new();
}

/// <summary>
/// Игрок в ранговом матче
/// </summary>
public class RankedPlayer
{
    public string PlayerId { get; set; } = "";
    public string PlayerName { get; set; } = "";
    public int Mmr { get; set; }
    public string Team { get; set; } = ""; // CT или T
}

/// <summary>
/// Результат рангового матчмейкинга
/// </summary>
public class RankedMatchmakingResult
{
    public RankedMatchStatus Status { get; set; }
    public string? MatchId { get; set; }
    public string? ServerIp { get; set; }
    public int ServerPort { get; set; }
    public string Message { get; set; } = "";
    public int EstimatedWaitTime { get; set; }
}

/// <summary>
/// Статус рангового матча
/// </summary>
public enum RankedMatchStatus
{
    NotInQueue,
    Searching,
    MatchFound,
    InGame,
    Completed,
    Cancelled
}