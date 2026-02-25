using System.Net.Sockets;
using Axlebolt.RpcSupport.Protobuf;
using Axlebolt.Bolt.Protobuf2;
using StandRiseServer.Core;
using Google.Protobuf;

namespace StandRiseServer.Services;

public class StatsService
{
    private readonly ProtobufHandler _handler;
    private readonly DatabaseService _database;
    private readonly SessionManager _sessionManager;

    public StatsService(ProtobufHandler handler, DatabaseService database, SessionManager sessionManager)
    {
        _handler = handler;
        _database = database;
        _sessionManager = sessionManager;
        
        _handler.RegisterHandler("PlayerStatsRemoteService", "getStats", GetStatsAsync);
        _handler.RegisterHandler("PlayerStatsRemoteService", "resetAllStats", ResetAllStatsAsync);
        _handler.RegisterHandler("PlayerStatsRemoteService", "getPlayerStats", GetPlayerStatsAsync);
        _handler.RegisterHandler("PlayerStatsRemoteService", "storeStats", StoreStatsAsync);
        _handler.RegisterHandler("PlayerStatsRemoteService", "getGlobalStats", GetGlobalStatsAsync);
    }

    private async Task GetStatsAsync(TcpClient client, RpcRequest request)
    {
        try
        {
            Console.WriteLine("=== GetStats Request ===");
            var session = _sessionManager.GetSessionByClient(client);
            Console.WriteLine($"Session found by client: {session != null}");
            
            // Если сессия не найдена по клиенту, попробуем найти любую активную сессию
            if (session == null)
            {
                Console.WriteLine("Trying to find any active session...");
                session = _sessionManager.GetAllSessions().FirstOrDefault();
                if (session != null)
                {
                    Console.WriteLine($"Using active session: {session.Token}");
                    session.Client = client; // Обновляем клиента
                }
            }
            
            if (session == null)
            {
                Console.WriteLine("❌ No session found for client in GetStats");
                await SendUnauthorizedAsync(client, request.Id);
                return;
            }

            Console.WriteLine($"Session token: {session.Token}");
            var player = await _database.GetPlayerByTokenAsync(session.Token);
            Console.WriteLine($"Player found: {player != null}");
            if (player == null)
            {
                Console.WriteLine($"❌ No player found for token in GetStats: {session.Token}");
                
                // Попробуем найти игрока по authToken
                Console.WriteLine("Trying to find player by authToken...");
                var allPlayers = await _database.GetAllPlayersAsync();
                Console.WriteLine($"Total players in database: {allPlayers?.Count ?? 0}");
                
                if (allPlayers != null)
                {
                    foreach (var p in allPlayers.Take(3)) // Показываем первых 3 игроков
                    {
                        Console.WriteLine($"Player: {p.Name}, Token: {p.Token}, AuthToken: {p.AuthToken}");
                    }
                }
                
                await SendUnauthorizedAsync(client, request.Id);
                return;
            }

            // Добавляем недостающую статистику оружия если её нет
            var statsUpdated = EnsureWeaponStats(player);
            if (statsUpdated)
            {
                await _database.UpdatePlayerAsync(player);
                Console.WriteLine("📊 Added missing weapon stats to player");
            }

            Console.WriteLine($"✅ GetStats successful for: {player.Name}");
            Console.WriteLine($"📊 Player has {player.Stats.ArrayCust.Count} stats");

            var stats = new Stats();
            foreach (var stat in player.Stats.ArrayCust)
            {
                // Convert string type to StatDefType enum
                var statDefType = stat.Type.ToUpper() switch
                {
                    "INT" => StatDefType.Int,
                    "FLOAT" => StatDefType.Float,
                    "AVGRATE" => StatDefType.Avgrate,
                    _ => StatDefType.Int // Default to INT
                };

                stats.Stat.Add(new PlayerStat
                {
                    Name = stat.Name,
                    IntValue = stat.IntValue,
                    FloatValue = stat.FloatValue,
                    Window = stat.Window,
                    Type = statDefType
                });
            }
            
            Console.WriteLine($"📤 Sending {stats.Stat.Count} stats to client");

            var result = new BinaryValue 
            { 
                IsNull = false, 
                One = ByteString.CopyFrom(stats.ToByteArray()) 
            };
            
            await _handler.WriteProtoResponseAsync(client, request.Id, result, null);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error in GetStats: {ex.Message}");
        }
    }

    private bool EnsureWeaponStats(StandRiseServer.Models.Player player)
    {
        var existingStatNames = player.Stats.ArrayCust.Select(s => s.Name).ToHashSet();
        var statsAdded = false;

        // Список всех режимов и оружия (из proto.js)
        var gameModes = new[] { "defuse", "deathmatch" };
        var weapons = new[] 
        { 
            "g22", "usp", "p350", "deagle", "tec9", "fiveseven", // Pistols
            "ump45", "mp7", "p90", // SMGs
            "akr", "akr12", "m4", "m16", "famas", "fnfal", // Rifles
            "awm", "m40", "m110", // Snipers
            "sm1014", // Shotguns
            "knife", "knifebayonet", "knifekarambit", "knifebutterfly", "jkommando", "flipknife" // Knives
        };

        // Типы статистики для каждого оружия
        var statTypes = new[] { "kills", "damage", "headshots", "hits", "shots" };

        foreach (var mode in gameModes)
        {
            foreach (var weapon in weapons)
            {
                foreach (var statType in statTypes)
                {
                    var statName = $"gun_{mode}_{weapon}_{statType}";
                    if (!existingStatNames.Contains(statName))
                    {
                        player.Stats.ArrayCust.Add(new StandRiseServer.Models.StatItem 
                        { 
                            Name = statName, 
                            IntValue = 0, 
                            FloatValue = 0, 
                            Window = 0, 
                            Type = "INT" 
                        });
                        statsAdded = true;
                    }
                }
            }
        }

        // Добавляем базовые статистики режимов если их нет
        var baseModeStats = new[]
        {
            "defuse_kills", "defuse_deaths", "defuse_assists", "defuse_shots", "defuse_hits", 
            "defuse_headshots", "defuse_damage", "defuse_games_played",
            "deathmatch_kills", "deathmatch_deaths", "deathmatch_assists", "deathmatch_shots", 
            "deathmatch_hits", "deathmatch_headshots", "deathmatch_damage", "deathmatch_games_played",
            // Sniper Duel mode
            "sniperduel_kills", "sniperduel_deaths", "sniperduel_assists", "sniperduel_shots",
            "sniperduel_hits", "sniperduel_headshots", "sniperduel_damage", "sniperduel_games_played",
            // Arms Race mode
            "armsrace_kills", "armsrace_deaths", "armsrace_assists", "armsrace_shots",
            "armsrace_hits", "armsrace_headshots", "armsrace_damage", "armsrace_games_played",
            // Rampage mode
            "rampage_kills", "rampage_deaths", "rampage_assists", "rampage_shots",
            "rampage_hits", "rampage_headshots", "rampage_damage", "rampage_games_played",
            // Knives Only mode
            "knivesonly_kills", "knivesonly_deaths", "knivesonly_assists", "knivesonly_games_played",
            // Escalation mode
            "escalation_kills", "escalation_deaths", "escalation_assists", "escalation_games_played",
            // Capture mode
            "capture_kills", "capture_deaths", "capture_assists", "capture_games_played",
            // Capture the Flag mode
            "ctf_kills", "ctf_deaths", "ctf_assists", "ctf_games_played", "ctf_flags_captured",
            // General stats
            "level_xp", "level_id", "total_kills", "total_deaths", "total_matches", "event_2years_score",
            "ranked_rank", "ranked_current_mmr", "ranked_ban_code", "ranked_ban_duration",
            "ranked_calibration_match_count", "ranked_last_match_status",
            "ranked_last_activity_time1", "ranked_last_activity_time2",
            "ranked_matches", "ranked_wins", "ranked_losses",
            // Additional stats
            "total_wins", "total_losses", "total_assists", "total_headshots",
            "total_damage", "total_shots", "total_hits", "total_playtime",
            "mvp_count", "bomb_plants", "bomb_defuses", "hostages_rescued"
        };

        foreach (var statName in baseModeStats)
        {
            if (!existingStatNames.Contains(statName))
            {
                var defaultValue = statName switch
                {
                    "level_id" => 0,
                    "ranked_current_mmr" => 0,
                    "ranked_last_match_status" => 0,
                    _ => 0
                };
                
                player.Stats.ArrayCust.Add(new StandRiseServer.Models.StatItem 
                { 
                    Name = statName, 
                    IntValue = defaultValue, 
                    FloatValue = 0, 
                    Window = 0, 
                    Type = "INT" 
                });
                statsAdded = true;
            }
        }

        return statsAdded;
    }

    private async Task StoreStatsAsync(TcpClient client, RpcRequest request)
    {
        try
        {
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

            // Parse stats from array
            foreach (var statBytes in request.Params[0].Array)
            {
                var storeStat = StorePlayerStat.Parser.ParseFrom(statBytes);
                
                // Find and update stat
                var existingStat = player.Stats.ArrayCust.FirstOrDefault(s => s.Name == storeStat.Name);
                if (existingStat != null)
                {
                    existingStat.IntValue = storeStat.StoreInt;
                }
            }

            await _database.UpdatePlayerAsync(player);

            var result = new BinaryValue { IsNull = true };
            await _handler.WriteProtoResponseAsync(client, request.Id, result, null);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error in StoreStats: {ex.Message}");
        }
    }

    private async Task ResetAllStatsAsync(TcpClient client, RpcRequest request)
    {
        try
        {
            Console.WriteLine("=== ResetAllStats Request ===");
            var result = new BinaryValue { IsNull = true };
            await _handler.WriteProtoResponseAsync(client, request.Id, result, null);
            Console.WriteLine("✅ ResetAllStats successful");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error in ResetAllStats: {ex.Message}");
        }
    }

    private async Task GetPlayerStatsAsync(TcpClient client, RpcRequest request)
    {
        try
        {
            Console.WriteLine("=== GetPlayerStats Request ===");
            // Возвращаем те же статистики что и в GetStats
            await GetStatsAsync(client, request);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error in GetPlayerStats: {ex.Message}");
        }
    }



    private async Task GetGlobalStatsAsync(TcpClient client, RpcRequest request)
    {
        try
        {
            Console.WriteLine("=== GetGlobalStats Request ===");
            var result = new BinaryValue { IsNull = false };
            // Пустой массив глобальной статистики
            await _handler.WriteProtoResponseAsync(client, request.Id, result, null);
            Console.WriteLine("✅ GetGlobalStats successful");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error in GetGlobalStats: {ex.Message}");
        }
    }

    private async Task SendUnauthorizedAsync(TcpClient client, string guid)
    {
        await _handler.WriteProtoResponseAsync(client, guid, null,
            new RpcException { Id = guid, Code = 401, Property = null });
    }
}
