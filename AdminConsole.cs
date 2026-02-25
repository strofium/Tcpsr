using System.Diagnostics;
using MongoDB.Driver;
using StandRiseServer.Core;
using StandRiseServer.Models;

namespace StandRiseServer;

public class AdminConsole
{
    private readonly DatabaseService _database;
    private readonly SessionManager _sessionManager;
    private bool _running = true;

    public AdminConsole(DatabaseService database, SessionManager sessionManager)
    {
        _database = database;
        _sessionManager = sessionManager;
    }

    public async Task RunAsync()
    {
        Console.WriteLine("\n🎮 Admin Console Started. Type 'help' for commands.\n");

        while (_running)
        {
            Console.Write("admin> ");
            var input = Console.ReadLine()?.Trim();
            if (string.IsNullOrEmpty(input)) continue;

            var parts = input.Split(' ');
            var command = parts[0].ToLower();

            try
            {
                switch (command)
                {
                    case "help":
                        ShowHelp();
                        break;

                    // Server Commands
                    case "status":
                        ShowStatus();
                        break;
                    
                    case "players":
                        ShowPlayers();
                        break;
                    
                    case "kick":
                        if (parts.Length > 1)
                            KickPlayer(parts[1]);
                        else
                            Console.WriteLine("Usage: kick <token>");
                        break;

                    // Battle Pass Commands
                    case "bp":
                        await HandleBattlePassCommand(parts);
                        break;

                    // Mission Commands
                    case "mission":
                        await HandleMissionCommand(parts);
                        break;

                    // Player Commands
                    case "player":
                        await HandlePlayerCommand(parts);
                        break;

                    case "exit":
                    case "quit":
                    case "stop":
                        _running = false;
                        Console.WriteLine("🛑 Stopping server...");
                        Environment.Exit(0);
                        break;

                    case "admin":
                        Console.WriteLine("✅ You are already in Admin Console! Type 'help' for commands.");
                        break;

                    default:
                        Console.WriteLine($"Unknown command: {command}. Type 'help' for available commands.");
                        break;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Error: {ex.Message}");
            }
        }
    }

    private void ShowStatus()
    {
        var sessions = _sessionManager.GetAllSessions().ToList();
        Console.WriteLine($"📊 Online: {sessions.Count} players");
        var uptime = DateTime.Now - Process.GetCurrentProcess().StartTime;
        Console.WriteLine($"⏱️  Uptime: {uptime.Hours}h {uptime.Minutes}m {uptime.Seconds}s");
    }
    
    private void ShowPlayers()
    {
        var sessions = _sessionManager.GetAllSessions().ToList();
        Console.WriteLine($"\n=== Connected Players ({sessions.Count}) ===");
        
        if (sessions.Count == 0)
        {
            Console.WriteLine("  No players connected");
        }
        else
        {
            foreach (var session in sessions)
            {
                var tokenPreview = session.Token.Length > 8 ? session.Token.Substring(0, 8) + "..." : session.Token;
                Console.WriteLine($"  Token: {tokenPreview}");
            }
        }
    }
    
    private void KickPlayer(string token)
    {
        var session = _sessionManager.GetSessionByToken(token);
        if (session != null)
        {
            session.Client?.Close();
            Console.WriteLine($"✅ Kicked player: {token}");
        }
        else
        {
            // Попробуем найти по частичному совпадению
            var sessions = _sessionManager.GetAllSessions()
                .Where(s => s.Token.StartsWith(token))
                .ToList();
            
            if (sessions.Count == 1)
            {
                sessions[0].Client?.Close();
                Console.WriteLine($"✅ Kicked player: {sessions[0].Token}");
            }
            else if (sessions.Count > 1)
            {
                Console.WriteLine($"❌ Multiple matches found. Be more specific.");
            }
            else
            {
                Console.WriteLine($"❌ Player not found: {token}");
            }
        }
    }

    private void ShowHelp()
    {
        Console.WriteLine(@"
=== Admin Console Commands ===

Server:
  status                                      - Show server status
  players                                     - List connected players
  kick <token>                                - Kick a player

Battle Pass:
  bp create <eventId> <name> <durationDays>  - Create new Battle Pass event
  bp addlevel <eventId> <level> <type> <rewardId> <amount>
      type: free/premium
      rewardId: item definition ID or currency ID
  bp list                                     - List all BP events
  bp levels <eventId>                         - Show levels for event
  bp setplayer <playerUid> <eventId> <level> <points> - Set player BP progress

Missions:
  mission create <eventId> <code> <type> <action> <target> <dayFrom> <dayTo>
      type: daily/weekly/season
      action: kills/wins/headshots/etc
  mission list <eventId>                      - List missions for event
  mission delete <missionId>                  - Delete mission

Player:
  player find <name/uid>                      - Find player
  player setlevel <uid> <level>               - Set player level
  player addcoins <uid> <amount>              - Add coins
  player addgems <uid> <amount>               - Add gems
  player setbp <uid> <eventId> <level>        - Set BP level

exit/quit/stop - Stop server
");
    }

    private async Task HandleBattlePassCommand(string[] parts)
    {
        if (parts.Length < 2)
        {
            Console.WriteLine("Usage: bp <create|addlevel|list|levels|setplayer> ...");
            return;
        }

        var subCommand = parts[1].ToLower();
        var eventsCollection = _database.GetCollection<GameEvent>("game_events");
        var levelsCollection = _database.GetCollection<GamePassLevelData>("game_pass_levels");

        switch (subCommand)
        {
            case "create":
                if (parts.Length < 5)
                {
                    Console.WriteLine("Usage: bp create <eventId> <name> <durationDays>");
                    return;
                }
                var eventId = parts[2];
                var eventName = parts[3];
                var duration = int.Parse(parts[4]);

                var newEvent = new GameEvent
                {
                    EventId = eventId,
                    Code = eventId,
                    Name = eventName,
                    DurationDays = duration,
                    DateSince = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                    DateUntil = DateTimeOffset.UtcNow.AddDays(duration).ToUnixTimeMilliseconds(),
                    IsEnabled = true
                };

                await eventsCollection.InsertOneAsync(newEvent);
                Console.WriteLine($"✅ Battle Pass '{eventName}' created with ID: {eventId}");
                break;

            case "addlevel":
                if (parts.Length < 7)
                {
                    Console.WriteLine("Usage: bp addlevel <eventId> <level> <type> <rewardId> <amount>");
                    Console.WriteLine("  type: free/premium");
                    return;
                }
                var evId = parts[2];
                var level = int.Parse(parts[3]);
                var passType = parts[4].ToLower(); // free or premium
                var rewardId = int.Parse(parts[5]);
                var amount = int.Parse(parts[6]);

                var levelData = new GamePassLevelData
                {
                    EventId = evId,
                    Level = level,
                    PassType = passType,
                    MinPoints = (level - 1) * 100,
                    RewardType = rewardId > 1000 ? "item" : "currency",
                    RewardId = rewardId,
                    RewardAmount = amount
                };

                await levelsCollection.InsertOneAsync(levelData);
                Console.WriteLine($"✅ Level {level} ({passType}) added to event {evId}");
                Console.WriteLine($"   Reward: {(rewardId > 1000 ? "Item" : "Currency")} #{rewardId} x{amount}");
                break;

            case "list":
                var events = await eventsCollection.Find(_ => true).ToListAsync();
                Console.WriteLine($"\n=== Battle Pass Events ({events.Count}) ===");
                foreach (var ev in events)
                {
                    var status = ev.IsEnabled ? "✅" : "❌";
                    Console.WriteLine($"  {status} {ev.EventId}: {ev.Name} ({ev.DurationDays} days)");
                }
                break;

            case "levels":
                if (parts.Length < 3)
                {
                    Console.WriteLine("Usage: bp levels <eventId>");
                    return;
                }
                var showEvId = parts[2];
                var levels = await levelsCollection.Find(l => l.EventId == showEvId)
                    .SortBy(l => l.Level)
                    .ThenBy(l => l.PassType)
                    .ToListAsync();

                Console.WriteLine($"\n=== Levels for {showEvId} ({levels.Count}) ===");
                foreach (var lvl in levels)
                {
                    Console.WriteLine($"  Level {lvl.Level} ({lvl.PassType}): {lvl.RewardType} #{lvl.RewardId} x{lvl.RewardAmount}");
                }
                break;

            case "setplayer":
                if (parts.Length < 6)
                {
                    Console.WriteLine("Usage: bp setplayer <playerUid> <eventId> <level> <points>");
                    return;
                }
                var playerUid = parts[2];
                var bpEventId = parts[3];
                var bpLevel = int.Parse(parts[4]);
                var bpPoints = int.Parse(parts[5]);

                var progressCollection = _database.GetCollection<PlayerBattlePassProgress>("player_bp_progress");
                var filter = Builders<PlayerBattlePassProgress>.Filter.And(
                    Builders<PlayerBattlePassProgress>.Filter.Eq(p => p.PlayerUid, playerUid),
                    Builders<PlayerBattlePassProgress>.Filter.Eq(p => p.EventId, bpEventId)
                );

                var progress = new PlayerBattlePassProgress
                {
                    PlayerUid = playerUid,
                    EventId = bpEventId,
                    CurrentLevel = bpLevel,
                    Points = bpPoints,
                    HasPremium = false
                };

                await progressCollection.ReplaceOneAsync(filter, progress, new ReplaceOptions { IsUpsert = true });
                Console.WriteLine($"✅ Player {playerUid} BP progress set: Level {bpLevel}, Points {bpPoints}");
                break;

            default:
                Console.WriteLine($"Unknown bp subcommand: {subCommand}");
                break;
        }
    }

    private async Task HandleMissionCommand(string[] parts)
    {
        if (parts.Length < 2)
        {
            Console.WriteLine("Usage: mission <create|list|delete> ...");
            return;
        }

        var subCommand = parts[1].ToLower();
        var missionsCollection = _database.GetCollection<GameChallenge>("game_challenges");

        switch (subCommand)
        {
            case "create":
                if (parts.Length < 9)
                {
                    Console.WriteLine("Usage: mission create <eventId> <code> <type> <action> <target> <dayFrom> <dayTo>");
                    Console.WriteLine("  type: daily/weekly/season");
                    Console.WriteLine("  action: kills/wins/headshots/damage/matches/etc");
                    return;
                }
                var eventId = parts[2];
                var code = parts[3];
                var missionType = parts[4];
                var action = parts[5];
                var target = int.Parse(parts[6]);
                var dayFrom = int.Parse(parts[7]);
                var dayTo = int.Parse(parts[8]);

                var mission = new GameChallenge
                {
                    ChallengeId = Guid.NewGuid().ToString(),
                    GameEventId = eventId,
                    Code = code,
                    Type = missionType,
                    Action = action,
                    TargetPoints = target,
                    DayRange = new DayRangeModel { From = dayFrom, To = dayTo },
                    IsEnabled = true
                };

                await missionsCollection.InsertOneAsync(mission);
                Console.WriteLine($"✅ Mission '{code}' created for event {eventId}");
                Console.WriteLine($"   Action: {action}, Target: {target}, Days: {dayFrom}-{dayTo}");
                break;

            case "list":
                if (parts.Length < 3)
                {
                    Console.WriteLine("Usage: mission list <eventId>");
                    return;
                }
                var listEventId = parts[2];
                var missions = await missionsCollection.Find(m => m.GameEventId == listEventId).ToListAsync();

                Console.WriteLine($"\n=== Missions for {listEventId} ({missions.Count}) ===");
                foreach (var m in missions)
                {
                    var status = m.IsEnabled ? "✅" : "❌";
                    Console.WriteLine($"  {status} {m.Code}: {m.Action} x{m.TargetPoints} ({m.Type})");
                    if (m.DayRange != null)
                        Console.WriteLine($"      Days: {m.DayRange.From}-{m.DayRange.To}");
                }
                break;

            case "delete":
                if (parts.Length < 3)
                {
                    Console.WriteLine("Usage: mission delete <missionId>");
                    return;
                }
                var deleteId = parts[2];
                var deleteResult = await missionsCollection.DeleteOneAsync(m => m.ChallengeId == deleteId);
                if (deleteResult.DeletedCount > 0)
                    Console.WriteLine($"✅ Mission {deleteId} deleted");
                else
                    Console.WriteLine($"❌ Mission {deleteId} not found");
                break;

            default:
                Console.WriteLine($"Unknown mission subcommand: {subCommand}");
                break;
        }
    }

    private async Task HandlePlayerCommand(string[] parts)
    {
        if (parts.Length < 2)
        {
            Console.WriteLine("Usage: player <find|setlevel|addcoins|addgems|setbp> ...");
            return;
        }

        var subCommand = parts[1].ToLower();
        var playersCollection = _database.GetCollection<Player>("Players2");

        switch (subCommand)
        {
            case "find":
                if (parts.Length < 3)
                {
                    Console.WriteLine("Usage: player find <name/uid>");
                    return;
                }
                var search = parts[2];
                var filter = Builders<Player>.Filter.Or(
                    Builders<Player>.Filter.Regex(p => p.Name, new MongoDB.Bson.BsonRegularExpression(search, "i")),
                    Builders<Player>.Filter.Eq(p => p.PlayerUid, search)
                );
                var players = await playersCollection.Find(filter).Limit(10).ToListAsync();

                Console.WriteLine($"\n=== Players found ({players.Count}) ===");
                foreach (var p in players)
                {
                    Console.WriteLine($"  UID: {p.PlayerUid}, Name: {p.Name}, Level: {p.Level}");
                    Console.WriteLine($"      Coins: {p.Coins}, Gems: {p.Gems}");
                }
                break;

            case "setlevel":
                if (parts.Length < 4)
                {
                    Console.WriteLine("Usage: player setlevel <uid> <level>");
                    return;
                }
                var uid = parts[2];
                var newLevel = int.Parse(parts[3]);
                var updateLevel = Builders<Player>.Update.Set(p => p.Level, newLevel);
                var levelResult = await playersCollection.UpdateOneAsync(p => p.PlayerUid == uid, updateLevel);
                if (levelResult.ModifiedCount > 0)
                    Console.WriteLine($"✅ Player {uid} level set to {newLevel}");
                else
                    Console.WriteLine($"❌ Player {uid} not found");
                break;

            case "addcoins":
                if (parts.Length < 4)
                {
                    Console.WriteLine("Usage: player addcoins <uid> <amount>");
                    return;
                }
                var coinsUid = parts[2];
                var coinsAmount = int.Parse(parts[3]);
                var updateCoins = Builders<Player>.Update.Inc(p => p.Coins, coinsAmount);
                var coinsResult = await playersCollection.UpdateOneAsync(p => p.PlayerUid == coinsUid, updateCoins);
                if (coinsResult.ModifiedCount > 0)
                    Console.WriteLine($"✅ Added {coinsAmount} coins to player {coinsUid}");
                else
                    Console.WriteLine($"❌ Player {coinsUid} not found");
                break;

            case "addgems":
                if (parts.Length < 4)
                {
                    Console.WriteLine("Usage: player addgems <uid> <amount>");
                    return;
                }
                var gemsUid = parts[2];
                var gemsAmount = int.Parse(parts[3]);
                var updateGems = Builders<Player>.Update.Inc(p => p.Gems, gemsAmount);
                var gemsResult = await playersCollection.UpdateOneAsync(p => p.PlayerUid == gemsUid, updateGems);
                if (gemsResult.ModifiedCount > 0)
                    Console.WriteLine($"✅ Added {gemsAmount} gems to player {gemsUid}");
                else
                    Console.WriteLine($"❌ Player {gemsUid} not found");
                break;

            case "setbp":
                if (parts.Length < 5)
                {
                    Console.WriteLine("Usage: player setbp <uid> <eventId> <level>");
                    return;
                }
                var bpUid = parts[2];
                var bpEventId = parts[3];
                var bpLevel = int.Parse(parts[4]);

                var progressCollection = _database.GetCollection<PlayerBattlePassProgress>("player_bp_progress");
                var bpFilter = Builders<PlayerBattlePassProgress>.Filter.And(
                    Builders<PlayerBattlePassProgress>.Filter.Eq(p => p.PlayerUid, bpUid),
                    Builders<PlayerBattlePassProgress>.Filter.Eq(p => p.EventId, bpEventId)
                );

                var bpProgress = new PlayerBattlePassProgress
                {
                    PlayerUid = bpUid,
                    EventId = bpEventId,
                    CurrentLevel = bpLevel,
                    Points = bpLevel * 100,
                    HasPremium = false
                };

                await progressCollection.ReplaceOneAsync(bpFilter, bpProgress, new ReplaceOptions { IsUpsert = true });
                Console.WriteLine($"✅ Player {bpUid} BP level set to {bpLevel} for event {bpEventId}");
                break;

            default:
                Console.WriteLine($"Unknown player subcommand: {subCommand}");
                break;
        }
    }
}
