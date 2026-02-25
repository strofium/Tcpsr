using System.Net.Sockets;
using Axlebolt.RpcSupport.Protobuf;
using Axlebolt.Bolt.Protobuf2;
using Axlebolt.Bolt.Protobuf;
using StandRiseServer.Core;
using Google.Protobuf;

namespace StandRiseServer.Services;

/// <summary>
/// Сервис для управления статистикой игрока на сервере
/// Обрабатывает сохранение статистики после матча в arrayCust структуру
/// </summary>
public class GameServerStatsRemoteService
{
    private readonly ProtobufHandler _handler;
    private readonly DatabaseService _database;
    private readonly SessionManager _sessionManager;

    public GameServerStatsRemoteService(ProtobufHandler handler, DatabaseService database, SessionManager sessionManager)
    {
        _handler = handler;
        _database = database;
        _sessionManager = sessionManager;
        
        _handler.RegisterHandler("GameServerStatsRemoteService", "saveMatchStats", SaveMatchStatsAsync);
        _handler.RegisterHandler("GameServerStatsRemoteService", "getPlayerMatchHistory", GetPlayerMatchHistoryAsync);
        _handler.RegisterHandler("GameServerStatsRemoteService", "updatePlayerStats", UpdatePlayerStatsAsync);
        _handler.RegisterHandler("GameServerStatsRemoteService", "getLeaderboard", GetLeaderboardAsync);
    }

    /// <summary>
    /// Сохраняет статистику матча в arrayCust после окончания игры
    /// </summary>
    private async Task SaveMatchStatsAsync(TcpClient client, RpcRequest request)
    {
        try
        {
            Console.WriteLine("=== SaveMatchStats Request ===");
            var session = _sessionManager.GetSessionByClient(client);
            if (session == null || request.Params.Count == 0)
            {
                await SendUnauthorizedAsync(client, request.Id);
                return;
            }

            var player = await _database.GetPlayerByTokenAsync(session.Token);
            if (player == null)
            {
                await SendUnauthorizedAsync(client, request.Id);
                return;
            }

            // Парсим статистику матча из параметров
            var matchStatsData = request.Params[0];
            if (matchStatsData.One != null && matchStatsData.One.Length > 0)
            {
                try
                {
                    var matchStats = MatchStatsData.ParseFrom(matchStatsData.One.ToByteArray());
                    
                    // Обновляем основную статистику игрока
                    UpdatePlayerMainStats(player, matchStats);
                    
                    // Сохраняем детальную статистику в arrayCust
                    SaveMatchStatsToArrayCust(player, matchStats);
                    
                    // Обновляем статистику по оружию
                    UpdateWeaponStats(player, matchStats);
                    
                    // Сохраняем в базу данных
                    await _database.UpdatePlayerAsync(player);
                    
                    Console.WriteLine($"✅ SaveMatchStats successful for: {player.Name}");
                    Console.WriteLine($"📊 Updated stats - Kills: {player.Stats.TotalKills}, Deaths: {player.Stats.TotalDeaths}, Matches: {player.Stats.TotalMatches}");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"⚠️ Error parsing match stats: {ex.Message}");
                }
            }

            var result = new BinaryValue { IsNull = true };
            await _handler.WriteProtoResponseAsync(client, request.Id, result, null);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Error in SaveMatchStats: {ex.Message}");
            await SendUnauthorizedAsync(client, request.Id);
        }
    }

    /// <summary>
    /// Получает историю матчей игрока
    /// </summary>
    private async Task GetPlayerMatchHistoryAsync(TcpClient client, RpcRequest request)
    {
        try
        {
            Console.WriteLine("=== GetPlayerMatchHistory Request ===");
            var session = _sessionManager.GetSessionByClient(client);
            if (session == null)
            {
                await SendUnauthorizedAsync(client, request.Id);
                return;
            }

            var player = await _database.GetPlayerByTokenAsync(session.Token);
            if (player == null)
            {
                await SendUnauthorizedAsync(client, request.Id);
                return;
            }

            var result = new BinaryValue { IsNull = false };
            
            // Возвращаем последние 10 матчей из arrayCust
            var recentMatches = player.Stats.ArrayCust
                .Where(s => s.Name.Contains("match_") || s.Name.Contains("game_"))
                .OrderByDescending(s => s.Name)
                .Take(10)
                .ToList();

            foreach (var match in recentMatches)
            {
                var matchInfo = new GameSetting
                {
                    Key = match.Name,
                    Type = SettingType.String,
                    Value = System.Text.Json.JsonSerializer.Serialize(new
                    {
                        name = match.Name,
                        intValue = match.IntValue,
                        floatValue = match.FloatValue
                    })
                };
                result.Array.Add(ByteString.CopyFrom(matchInfo.ToByteArray()));
            }

            Console.WriteLine($"✅ GetPlayerMatchHistory successful - Found {recentMatches.Count} matches");
            await _handler.WriteProtoResponseAsync(client, request.Id, result, null);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Error in GetPlayerMatchHistory: {ex.Message}");
            await SendUnauthorizedAsync(client, request.Id);
        }
    }

    /// <summary>
    /// Обновляет статистику игрока
    /// </summary>
    private async Task UpdatePlayerStatsAsync(TcpClient client, RpcRequest request)
    {
        try
        {
            Console.WriteLine("=== UpdatePlayerStats Request ===");
            var session = _sessionManager.GetSessionByClient(client);
            if (session == null || request.Params.Count == 0)
            {
                await SendUnauthorizedAsync(client, request.Id);
                return;
            }

            var player = await _database.GetPlayerByTokenAsync(session.Token);
            if (player == null)
            {
                await SendUnauthorizedAsync(client, request.Id);
                return;
            }

            // Парсим обновления статистики
            foreach (var statBytes in request.Params[0].Array)
            {
                try
                {
                    var storeStat = StorePlayerStat.Parser.ParseFrom(statBytes);
                    
                    // Ищем существующую статистику в arrayCust
                    var existingStat = player.Stats.ArrayCust.FirstOrDefault(s => s.Name == storeStat.Name);
                    if (existingStat != null)
                    {
                        existingStat.IntValue = storeStat.StoreInt;
                        existingStat.FloatValue = 0;
                    }
                    else
                    {
                        // Добавляем новую статистику
                        player.Stats.ArrayCust.Add(new Models.StatItem
                        {
                            Name = storeStat.Name,
                            IntValue = storeStat.StoreInt,
                            FloatValue = 0,
                            Type = "INT"
                        });
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"⚠️ Error parsing stat: {ex.Message}");
                }
            }

            await _database.UpdatePlayerAsync(player);
            Console.WriteLine($"✅ UpdatePlayerStats successful for: {player.Name}");

            var result = new BinaryValue { IsNull = true };
            await _handler.WriteProtoResponseAsync(client, request.Id, result, null);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Error in UpdatePlayerStats: {ex.Message}");
            await SendUnauthorizedAsync(client, request.Id);
        }
    }

    /// <summary>
    /// Получает таблицу лидеров
    /// </summary>
    private async Task GetLeaderboardAsync(TcpClient client, RpcRequest request)
    {
        try
        {
            Console.WriteLine("=== GetLeaderboard Request ===");
            var session = _sessionManager.GetSessionByClient(client);
            if (session == null)
            {
                await SendUnauthorizedAsync(client, request.Id);
                return;
            }

            var result = new BinaryValue { IsNull = false };
            
            // Получаем топ 100 игроков по количеству убийств
            var topPlayers = await _database.GetTopPlayersByKillsAsync(100);
            
            int rank = 1;
            foreach (var topPlayer in topPlayers)
            {
                var leaderboardEntry = new GameSetting
                {
                    Key = $"rank_{rank}",
                    Type = SettingType.String,
                    Value = System.Text.Json.JsonSerializer.Serialize(new
                    {
                        rank = rank,
                        playerName = topPlayer.Name,
                        playerId = topPlayer.PlayerUid,
                        kills = topPlayer.Stats.TotalKills,
                        deaths = topPlayer.Stats.TotalDeaths,
                        matches = topPlayer.Stats.TotalMatches,
                        winRate = topPlayer.Stats.WinRate,
                        kdr = topPlayer.Stats.KDR,
                        level = topPlayer.Level
                    })
                };
                result.Array.Add(ByteString.CopyFrom(leaderboardEntry.ToByteArray()));
                rank++;
            }

            Console.WriteLine($"✅ GetLeaderboard successful - Found {topPlayers.Count} players");
            await _handler.WriteProtoResponseAsync(client, request.Id, result, null);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Error in GetLeaderboard: {ex.Message}");
            await SendUnauthorizedAsync(client, request.Id);
        }
    }

    /// <summary>
    /// Обновляет основную статистику игрока на основе данных матча
    /// </summary>
    private void UpdatePlayerMainStats(Models.Player player, MatchStatsData matchStats)
    {
        player.Stats.TotalMatches++;
        player.Stats.TotalKills += matchStats.Kills;
        player.Stats.TotalDeaths += matchStats.Deaths;
        player.Stats.Assists += matchStats.Assists;
        player.Stats.Headshots += matchStats.Headshots;
        player.Stats.DamageDealt += matchStats.DamageDealt;
        
        if (matchStats.IsWin)
        {
            player.Stats.TotalWins++;
        }
        else
        {
            player.Stats.TotalLosses++;
        }

        // Обновляем ранговую статистику если это ранговый матч
        if (matchStats.IsRanked)
        {
            player.Stats.RankedMatches++;
            if (matchStats.IsWin)
            {
                player.Stats.RankedWins++;
            }
            else
            {
                player.Stats.RankedLosses++;
            }
        }

        // Обновляем время в игре
        player.TimeInGame += matchStats.MatchDuration;
    }

    /// <summary>
    /// Сохраняет детальную статистику матча в arrayCust
    /// </summary>
    private void SaveMatchStatsToArrayCust(Models.Player player, MatchStatsData matchStats)
    {
        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var matchId = $"match_{timestamp}_{Guid.NewGuid().ToString().Substring(0, 8)}";

        // Сохраняем основные данные матча
        var matchStats_list = new[]
        {
            new Models.StatItem { Name = $"{matchId}_kills", IntValue = matchStats.Kills, Type = "INT" },
            new Models.StatItem { Name = $"{matchId}_deaths", IntValue = matchStats.Deaths, Type = "INT" },
            new Models.StatItem { Name = $"{matchId}_assists", IntValue = matchStats.Assists, Type = "INT" },
            new Models.StatItem { Name = $"{matchId}_headshots", IntValue = matchStats.Headshots, Type = "INT" },
            new Models.StatItem { Name = $"{matchId}_damage", IntValue = (int)matchStats.DamageDealt, Type = "INT" },
            new Models.StatItem { Name = $"{matchId}_duration", IntValue = matchStats.MatchDuration, Type = "INT" },
            new Models.StatItem { Name = $"{matchId}_isWin", IntValue = matchStats.IsWin ? 1 : 0, Type = "INT" },
            new Models.StatItem { Name = $"{matchId}_isRanked", IntValue = matchStats.IsRanked ? 1 : 0, Type = "INT" },
            new Models.StatItem { Name = $"{matchId}_gameMode", IntValue = (int)matchStats.GameMode, Type = "INT" },
            new Models.StatItem { Name = $"{matchId}_timestamp", IntValue = (int)timestamp, Type = "INT" }
        };

        foreach (var stat in matchStats_list)
        {
            // Проверяем, есть ли уже такая статистика
            var existing = player.Stats.ArrayCust.FirstOrDefault(s => s.Name == stat.Name);
            if (existing == null)
            {
                player.Stats.ArrayCust.Add(stat);
            }
            else
            {
                existing.IntValue = stat.IntValue;
            }
        }

        // Обновляем общие счетчики
        UpdateOrAddStat(player, "total_kills", player.Stats.TotalKills);
        UpdateOrAddStat(player, "total_deaths", player.Stats.TotalDeaths);
        UpdateOrAddStat(player, "total_matches", player.Stats.TotalMatches);
        UpdateOrAddStat(player, "total_wins", player.Stats.TotalWins);
        UpdateOrAddStat(player, "total_losses", player.Stats.TotalLosses);
        UpdateOrAddStat(player, "total_assists", player.Stats.Assists);
        UpdateOrAddStat(player, "total_headshots", player.Stats.Headshots);
        UpdateOrAddStat(player, "total_damage", (int)player.Stats.DamageDealt);
    }

    /// <summary>
    /// Обновляет статистику по оружию
    /// </summary>
    private void UpdateWeaponStats(Models.Player player, MatchStatsData matchStats)
    {
        if (matchStats.WeaponStats == null || matchStats.WeaponStats.Count == 0)
            return;

        foreach (var weaponStat in matchStats.WeaponStats)
        {
            var weaponKey = weaponStat.WeaponName.ToLower();
            
            if (!player.Stats.WeaponStats.ContainsKey(weaponKey))
            {
                player.Stats.WeaponStats[weaponKey] = new Models.WeaponStats
                {
                    WeaponName = weaponStat.WeaponName
                };
            }

            var stats = player.Stats.WeaponStats[weaponKey];
            stats.Kills += weaponStat.Kills;
            stats.Deaths += weaponStat.Deaths;
            stats.Headshots += weaponStat.Headshots;
            stats.DamageDealt += weaponStat.DamageDealt;
            stats.ShotsHit += weaponStat.ShotsHit;
            stats.ShotsFired += weaponStat.ShotsFired;

            // Также сохраняем в arrayCust для совместимости
            UpdateOrAddStat(player, $"gun_{weaponKey}_kills", stats.Kills);
            UpdateOrAddStat(player, $"gun_{weaponKey}_deaths", stats.Deaths);
            UpdateOrAddStat(player, $"gun_{weaponKey}_headshots", stats.Headshots);
            UpdateOrAddStat(player, $"gun_{weaponKey}_damage", (int)stats.DamageDealt);
        }
    }

    /// <summary>
    /// Вспомогательный метод для обновления или добавления статистики в arrayCust
    /// </summary>
    private void UpdateOrAddStat(Models.Player player, string statName, int value)
    {
        var existing = player.Stats.ArrayCust.FirstOrDefault(s => s.Name == statName);
        if (existing != null)
        {
            existing.IntValue = value;
        }
        else
        {
            player.Stats.ArrayCust.Add(new Models.StatItem
            {
                Name = statName,
                IntValue = value,
                Type = "INT"
            });
        }
    }

    private async Task SendUnauthorizedAsync(TcpClient client, string guid)
    {
        await _handler.WriteProtoResponseAsync(client, guid, null,
            new RpcException { Id = guid, Code = 401, Property = null });
    }
}

/// <summary>
/// Модель для статистики матча
/// </summary>
public class MatchStatsData
{
    public int Kills { get; set; }
    public int Deaths { get; set; }
    public int Assists { get; set; }
    public int Headshots { get; set; }
    public long DamageDealt { get; set; }
    public int MatchDuration { get; set; }
    public bool IsWin { get; set; }
    public bool IsRanked { get; set; }
    public int GameMode { get; set; }
    public List<WeaponStatData> WeaponStats { get; set; } = new();

    public byte[] ToByteArray() => System.Text.Json.JsonSerializer.SerializeToUtf8Bytes(this);

    public static MatchStatsData ParseFrom(byte[] data)
    {
        return System.Text.Json.JsonSerializer.Deserialize<MatchStatsData>(data) ?? new MatchStatsData();
    }
}

public class WeaponStatData
{
    public string WeaponName { get; set; } = string.Empty;
    public int Kills { get; set; }
    public int Deaths { get; set; }
    public int Headshots { get; set; }
    public long DamageDealt { get; set; }
    public int ShotsHit { get; set; }
    public int ShotsFired { get; set; }
}
