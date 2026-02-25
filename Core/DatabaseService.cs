using System.Collections.Concurrent;
using Microsoft.Extensions.Caching.Memory;
using MongoDB.Driver;
using StandRiseServer.Models;

namespace StandRiseServer.Core;

public class DatabaseService
{
    private readonly IMongoDatabase _database;
    private readonly IMongoCollection<Player> _players;
    private readonly IMongoCollection<HwidDocument> _hwids;
    private readonly IMongoCollection<HashDocument> _hashes;
    private readonly IMongoCollection<GameSettingDocument> _gameSettings;
    private readonly IMongoCollection<InventoryDocument> _inventory;
    private readonly IMemoryCache _cache;
    private readonly ILogger<DatabaseService> _logger;
    private static readonly ConcurrentDictionary<string, SemaphoreSlim> _playerLocks = new();

    public IMongoDatabase Database => _database;
    
    public IMongoCollection<T> GetCollection<T>(string name)
    {
        return _database.GetCollection<T>(name);
    }

    public IMongoCollection<Player> Players => _players;

    public DatabaseService(
        string connectionString, 
        IMemoryCache cache,
        ILogger<DatabaseService> logger,
        string databaseName = "Ryzen")
    {
        _cache = cache;
        _logger = logger;
        
        _logger.LogInformation("🔗 Connecting to MongoDB: {ConnectionString}", connectionString);
        _logger.LogInformation("📊 Database name: {DatabaseName}", databaseName);
        
        try
        {
            var settings = MongoClientSettings.FromConnectionString(connectionString);
            settings.MaxConnectionPoolSize = 1000;
            settings.MinConnectionPoolSize = 10;
            settings.ConnectTimeout = TimeSpan.FromSeconds(10);
            settings.SocketTimeout = TimeSpan.FromSeconds(30);
            settings.ServerSelectionTimeout = TimeSpan.FromSeconds(30);
            
            var client = new MongoClient(settings);
            _database = client.GetDatabase(databaseName);
            
            // Test connection
            var collections = _database.ListCollectionNames().ToList();
            _logger.LogInformation("📋 Found {Count} collections in database", collections.Count);
            
            _players = _database.GetCollection<Player>("Players2");
            _hwids = _database.GetCollection<HwidDocument>("Hwids2");
            _hashes = _database.GetCollection<HashDocument>("Hashes");
            _gameSettings = _database.GetCollection<GameSettingDocument>("GameSettings");
            _inventory = _database.GetCollection<InventoryDocument>("Inventory");

            // Создаем индексы асинхронно (не блокируем конструктор)
            Task.Run(CreateIndexesAsync).ContinueWith(t =>
            {
                if (t.IsFaulted)
                    _logger.LogError(t.Exception, "Failed to create indexes");
            });

            _logger.LogInformation("✅ Connected to MongoDB: {DatabaseName}", databaseName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Failed to connect to MongoDB");
            throw;
        }
    }

    private async Task CreateIndexesAsync()
    {
        try
        {
            _logger.LogInformation("🔧 Creating database indexes...");

            // Индексы для Players
            var tokenIndex = new CreateIndexModel<Player>(
                Builders<Player>.IndexKeys.Ascending(p => p.Token),
                new CreateIndexOptions { Unique = true, Name = "Token_Unique" });
            
            var uidIndex = new CreateIndexModel<Player>(
                Builders<Player>.IndexKeys.Ascending(p => p.PlayerUid),
                new CreateIndexOptions { Unique = true, Name = "PlayerUid_Unique" });
            
            var usernameIndex = new CreateIndexModel<Player>(
                Builders<Player>.IndexKeys.Ascending(p => p.Username),
                new CreateIndexOptions { Unique = true, Name = "Username_Unique" });
            
            var emailIndex = new CreateIndexModel<Player>(
                Builders<Player>.IndexKeys.Ascending(p => p.Email),
                new CreateIndexOptions { Unique = true, Sparse = true, Name = "Email_Unique" });

            var nameTextIndex = new CreateIndexModel<Player>(
                Builders<Player>.IndexKeys.Text(p => p.Name),
                new CreateIndexOptions { Name = "Name_Text" });

            var objectIdIndex = new CreateIndexModel<Player>(
                Builders<Player>.IndexKeys.Ascending(p => p.Id),
                new CreateIndexOptions { Name = "Id_Index" });

            await _players.Indexes.CreateManyAsync(new[]
            {
                tokenIndex, uidIndex, usernameIndex, emailIndex, nameTextIndex, objectIdIndex
            });

            // Индексы для Hashes
            var versionIndex = new CreateIndexModel<HashDocument>(
                Builders<HashDocument>.IndexKeys.Ascending(h => h.Version),
                new CreateIndexOptions { Unique = true, Name = "Version_Unique" });

            await _hashes.Indexes.CreateOneAsync(versionIndex);

            // Индексы для GameSettings
            var keyIndex = new CreateIndexModel<GameSettingDocument>(
                Builders<GameSettingDocument>.IndexKeys.Ascending(s => s.Key),
                new CreateIndexOptions { Unique = true, Name = "Key_Unique" });

            await _gameSettings.Indexes.CreateOneAsync(keyIndex);

            _logger.LogInformation("✅ Database indexes created successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Error creating indexes");
        }
    }

    // ==================== PLAYER OPERATIONS ====================

    public async Task<Player?> GetPlayerByTokenAsync(string token)
    {
        if (string.IsNullOrEmpty(token))
            return null;

        var cacheKey = $"player_token_{token}";
        if (_cache.TryGetValue<Player>(cacheKey, out var cachedPlayer))
            return cachedPlayer;

        var player = await _players.Find(p => p.Token == token).FirstOrDefaultAsync();
        
        if (player != null)
        {
            _cache.Set(cacheKey, player, TimeSpan.FromMinutes(5));
            CachePlayer(player);
        }
        
        return player;
    }

    public async Task<Player?> GetPlayerByUidAsync(string uid)
    {
        if (string.IsNullOrEmpty(uid))
            return null;

        var cacheKey = $"player_uid_{uid}";
        if (_cache.TryGetValue<Player>(cacheKey, out var cachedPlayer))
            return cachedPlayer;

        var player = await _players.Find(p => p.PlayerUid == uid).FirstOrDefaultAsync();
        
        if (player != null)
        {
            _cache.Set(cacheKey, player, TimeSpan.FromMinutes(5));
            CachePlayer(player);
        }
        
        return player;
    }

    public async Task<Player?> GetPlayerByUsernameAsync(string username)
    {
        if (string.IsNullOrEmpty(username))
            return null;

        var cacheKey = $"player_username_{username}";
        if (_cache.TryGetValue<Player>(cacheKey, out var cachedPlayer))
            return cachedPlayer;

        var player = await _players.Find(p => p.Username == username).FirstOrDefaultAsync();
        
        if (player != null)
        {
            _cache.Set(cacheKey, player, TimeSpan.FromMinutes(5));
            CachePlayer(player);
        }
        
        return player;
    }

    public async Task<Player?> GetPlayerByObjectIdAsync(string objectId)
    {
        if (string.IsNullOrEmpty(objectId))
            return null;

        var cacheKey = $"player_objectid_{objectId}";
        if (_cache.TryGetValue<Player>(cacheKey, out var cachedPlayer))
            return cachedPlayer;

        if (!MongoDB.Bson.ObjectId.TryParse(objectId, out var objId))
            return null;

        var player = await _players.Find(p => p.Id == objId).FirstOrDefaultAsync();
        
        if (player != null)
        {
            _cache.Set(cacheKey, player, TimeSpan.FromMinutes(5));
            CachePlayer(player);
        }
        
        return player;
    }

    public async Task<Player?> GetPlayerByIdAsync(string playerId)
    {
        try
        {
            if (MongoDB.Bson.ObjectId.TryParse(playerId, out var objectId))
            {
                return await GetPlayerByObjectIdAsync(playerId);
            }
            return await GetPlayerByUidAsync(playerId);
        }
        catch
        {
            return null;
        }
    }

    public async Task<Player?> GetPlayerByAuthTokenAsync(string authToken)
    {
        return await _players.Find(p => p.AuthToken == authToken).FirstOrDefaultAsync();
    }

    public async Task<long> CountPlayersByAuthTokenAsync(string authToken)
    {
        return await _players.CountDocumentsAsync(p => p.AuthToken == authToken);
    }

    public async Task<int> GetNextPlayerUidAsync()
    {
        var lastPlayer = await _players.Find(_ => true)
            .SortByDescending(p => p.OriginalUid)
            .FirstOrDefaultAsync();
        
        return (lastPlayer?.OriginalUid ?? 10000) + 1;
    }

    public async Task InsertPlayerAsync(Player player)
    {
        await _players.InsertOneAsync(player);
        CachePlayer(player);
    }

    public async Task UpdatePlayerFieldsAsync(string playerId, UpdateDefinition<Player> update)
    {
        if (!MongoDB.Bson.ObjectId.TryParse(playerId, out var objId))
            return;

        var lockKey = $"player_lock_{playerId}";
        var semaphore = _playerLocks.GetOrAdd(lockKey, _ => new SemaphoreSlim(1, 1));
        
        await semaphore.WaitAsync();
        try
        {
            var result = await _players.UpdateOneAsync(p => p.Id == objId, update);
            
            if (result.ModifiedCount > 0 || result.MatchedCount > 0)
            {
                await InvalidatePlayerCacheAsync(playerId);
            }
        }
        finally
        {
            semaphore.Release();
        }
    }

    public async Task UpdateTwoPlayersAsync(string player1Id, string player2Id, 
        UpdateDefinition<Player> update1, UpdateDefinition<Player> update2)
    {
        var lockKey1 = $"player_lock_{player1Id}";
        var lockKey2 = $"player_lock_{player2Id}";
        
        var semaphore1 = _playerLocks.GetOrAdd(lockKey1, _ => new SemaphoreSlim(1, 1));
        var semaphore2 = _playerLocks.GetOrAdd(lockKey2, _ => new SemaphoreSlim(1, 1));

        // Всегда блокируем в одном порядке, чтобы избежать deadlock
        var firstLock = string.CompareOrdinal(player1Id, player2Id) < 0 ? semaphore1 : semaphore2;
        var secondLock = string.CompareOrdinal(player1Id, player2Id) < 0 ? semaphore2 : semaphore1;

        await firstLock.WaitAsync();
        try
        {
            await secondLock.WaitAsync();
            try
            {
                using var session = await _database.Client.StartSessionAsync();
                session.StartTransaction();

                try
                {
                    var objId1 = MongoDB.Bson.ObjectId.Parse(player1Id);
                    var objId2 = MongoDB.Bson.ObjectId.Parse(player2Id);

                    await _players.UpdateOneAsync(session, p => p.Id == objId1, update1);
                    await _players.UpdateOneAsync(session, p => p.Id == objId2, update2);

                    await session.CommitTransactionAsync();

                    await InvalidatePlayerCacheAsync(player1Id);
                    await InvalidatePlayerCacheAsync(player2Id);
                }
                catch
                {
                    await session.AbortTransactionAsync();
                    throw;
                }
            }
            finally
            {
                secondLock.Release();
            }
        }
        finally
        {
            firstLock.Release();
        }
    }

    public async Task SaveTimeInGameAsync(string token, long timeInGame)
    {
        var update = Builders<Player>.Update.Set(p => p.TimeInGame, timeInGame);
        await _players.UpdateOneAsync(p => p.Token == token, update);
        
        // Инвалидируем кэш для этого токена
        _cache.Remove($"player_token_{token}");
    }

    // ==================== FRIENDS SYSTEM METHODS ====================

    public async Task<List<Player>> SearchPlayersAsync(string searchValue, int page, int size)
    {
        if (string.IsNullOrWhiteSpace(searchValue))
            return new List<Player>();
        
        var filter = Builders<Player>.Filter.Or(
            Builders<Player>.Filter.Text(searchValue),
            Builders<Player>.Filter.Eq(p => p.PlayerUid, searchValue)
        );
        
        return await _players.Find(filter)
            .Skip(page * size)
            .Limit(size)
            .ToListAsync();
    }

    public async Task<long> GetPlayersCountAsync(string searchValue)
    {
        if (string.IsNullOrWhiteSpace(searchValue))
            return 0;
        
        var filter = Builders<Player>.Filter.Or(
            Builders<Player>.Filter.Text(searchValue),
            Builders<Player>.Filter.Eq(p => p.PlayerUid, searchValue)
        );
        
        return await _players.CountDocumentsAsync(filter);
    }

    public async Task<List<Player>> GetPlayersByUidsAsync(List<string> uids)
    {
        if (uids == null || uids.Count == 0)
            return new List<Player>();

        return await _players.Find(p => uids.Contains(p.PlayerUid)).ToListAsync();
    }

    // ==================== HWID OPERATIONS ====================

    public async Task<HwidDocument?> GetHwidAsync(string hwid)
    {
        return await _hwids.Find(h => h.Hwid == hwid).FirstOrDefaultAsync();
    }

    public async Task<long> CountHwidAsync(string hwid)
    {
        return await _hwids.CountDocumentsAsync(h => h.Hwid == hwid);
    }

    public async Task InsertHwidAsync(string hwid)
    {
        await _hwids.InsertOneAsync(new HwidDocument { Hwid = hwid, IsBanned = false });
    }

    // ==================== HASH OPERATIONS ====================

    public async Task<HashDocument?> GetHashByVersionAsync(string version)
    {
        return await _hashes.Find(h => h.Version == version).FirstOrDefaultAsync();
    }

    public async Task<long> CountHashByVersionAsync(string version)
    {
        return await _hashes.CountDocumentsAsync(h => h.Version == version);
    }

    public async Task InsertHashAsync(string version, string hash, string signature)
    {
        var hashDoc = new HashDocument
        {
            Version = version,
            Hash = hash,
            Signature = signature
        };
        await _hashes.InsertOneAsync(hashDoc);
        _logger.LogInformation("Hash added for version: {Version}", version);
    }

    public async Task<List<HashDocument>> GetAllHashesAsync()
    {
        return await _hashes.Find(_ => true).ToListAsync();
    }

    // ==================== GAME SETTINGS OPERATIONS ====================

    public async Task<List<GameSettingDocument>> GetGameSettingsAsync()
    {
        return await _gameSettings.Find(_ => true).ToListAsync();
    }

    public async Task InsertGameSettingAsync(string key, string value)
    {
        var setting = new GameSettingDocument
        {
            Key = key,
            Value = value
        };
        await _gameSettings.InsertOneAsync(setting);
        _logger.LogInformation("Game setting added: {Key} = {Value}", key, value);
    }

    public async Task<GameSettingDocument?> GetGameSettingAsync(string key)
    {
        return await _gameSettings.Find(s => s.Key == key).FirstOrDefaultAsync();
    }

    public async Task UpdateGameSettingAsync(string key, string value)
    {
        var filter = Builders<GameSettingDocument>.Filter.Eq(s => s.Key, key);
        var update = Builders<GameSettingDocument>.Update.Set(s => s.Value, value);
        await _gameSettings.UpdateOneAsync(filter, update);
        _logger.LogInformation("Game setting updated: {Key} = {Value}", key, value);
    }

    public async Task UpsertGameSettingAsync(string key, string value)
    {
        var existing = await GetGameSettingAsync(key);
        if (existing == null)
        {
            await InsertGameSettingAsync(key, value);
        }
        else
        {
            await UpdateGameSettingAsync(key, value);
        }
    }

    // ==================== INVENTORY OPERATIONS ====================

    public async Task<InventoryDocument?> GetInventoryAsync()
    {
        return await _inventory.Find(_ => true).FirstOrDefaultAsync();
    }

    // ==================== STATISTICS & LEADERBOARDS ====================

    public async Task<List<Player>> GetTopPlayersByKillsAsync(int limit = 100)
    {
        return await _players.Find(_ => true)
            .SortByDescending(p => p.Stats.TotalKills)
            .Limit(limit)
            .ToListAsync();
    }

    public async Task<List<Player>> GetTopPlayersByWinRateAsync(int limit = 100)
    {
        return await _players.Find(_ => true)
            .SortByDescending(p => p.Stats.TotalWins)
            .Limit(limit)
            .ToListAsync();
    }

    public async Task<List<Player>> GetBannedPlayersAsync()
    {
        return await _players.Find(p => p.IsBanned || (p.Ranked != null && p.Ranked.IsBanned)).ToListAsync();
    }

    public async Task<List<Player>> GetActivePlayers()
    {
        return await _players.Find(p => p.OnlineStatus == OnlineStatus.InGame).ToListAsync();
    }

    public async Task<List<Player>> GetAllPlayersAsync()
    {
        return await _players.Find(_ => true).ToListAsync();
    }

    // ==================== CACHE HELPERS ====================

    private void CachePlayer(Player player)
    {
        if (player == null) return;

        _cache.Set($"player_token_{player.Token}", player, TimeSpan.FromMinutes(5));
        _cache.Set($"player_uid_{player.PlayerUid}", player, TimeSpan.FromMinutes(5));
        _cache.Set($"player_objectid_{player.Id}", player, TimeSpan.FromMinutes(5));
        
        if (!string.IsNullOrEmpty(player.Username))
            _cache.Set($"player_username_{player.Username}", player, TimeSpan.FromMinutes(5));
    }

    private async Task InvalidatePlayerCacheAsync(string playerId)
    {
        var player = await GetPlayerByObjectIdAsync(playerId);
        if (player != null)
        {
            _cache.Remove($"player_token_{player.Token}");
            _cache.Remove($"player_uid_{player.PlayerUid}");
            _cache.Remove($"player_objectid_{playerId}");
            
            if (!string.IsNullOrEmpty(player.Username))
                _cache.Remove($"player_username_{player.Username}");
        }
    }

    // ==================== INITIALIZATION METHODS ====================

    public async Task InitializeDatabaseAsync()
    {
        _logger.LogInformation("🔧 Initializing StandRaid Database v0.37.0 (RaidTeam)");
        
        await InitializeGameVersionsAsync();
        await InitializeGameSettingsAsync();
        await EnsureDefaultPlayerStatsAsync();
        await InitializeInventoryAsync();
        await InitializeMarketplaceConfigAsync();
        await InitializeGameEventsAsync();
        
        _logger.LogInformation("✅ Database initialization completed");
    }

    private async Task InitializeGameVersionsAsync()
    {
        _logger.LogInformation("📦 Initializing game versions...");
        
        var versions = new[]
        {
            new { Version = "0.37.0", Hash = "StandRaid_0370", Signature = "raidteam_signature" },
            new { Version = "0.36.0", Hash = "StandRaid_0360", Signature = "raidteam_signature" },
            new { Version = "0.35.0", Hash = "StandRaid_0350", Signature = "raidteam_signature" }
        };

        foreach (var version in versions)
        {
            var count = await CountHashByVersionAsync(version.Version);
            if (count == 0)
            {
                await InsertHashAsync(version.Version, version.Hash, version.Signature);
                _logger.LogInformation("✅ Added game version: {Version}", version.Version);
            }
        }
    }

    private async Task InitializeGameSettingsAsync()
    {
        _logger.LogInformation("⚙️ Initializing game settings...");
        
        var settings = new Dictionary<string, string>
        {
            ["game_version"] = "0.37.0",
            ["game_name"] = "StandRaid",
            ["company_name"] = "RaidTeam",
            ["server_version"] = "1.0.0",
            ["maintenance_mode"] = "false",
            ["max_players"] = "1000",
            ["update_required"] = "false",
            ["regions"] = @"{""Servers"":[" +
                @"{""Location"":""eu"",""DisplayName"":""Europe"",""Dns"":""92.38.154.54"",""Ip"":""92.38.154.54"",""Online"":true,""Enabled"":true}," +
                @"{""Location"":""msk"",""DisplayName"":""Russia"",""Dns"":""92.38.154.54"",""Ip"":""92.38.154.54"",""Online"":true,""Enabled"":true}," +
                @"{""Location"":""fra"",""DisplayName"":""Europe"",""Dns"":""92.38.154.54"",""Ip"":""92.38.154.54"",""Online"":true,""Enabled"":true}," +
                @"{""Location"":""nyc"",""DisplayName"":""USA East"",""Dns"":""92.38.154.54"",""Ip"":""92.38.154.54"",""Online"":true,""Enabled"":true}," +
                @"{""Location"":""sfo"",""DisplayName"":""USA West"",""Dns"":""92.38.154.54"",""Ip"":""92.38.154.54"",""Online"":true,""Enabled"":true}," +
                @"{""Location"":""sgp"",""DisplayName"":""Asia South"",""Dns"":""92.38.154.54"",""Ip"":""92.38.154.54"",""Online"":true,""Enabled"":true}," +
                @"{""Location"":""tok"",""DisplayName"":""Asia East"",""Dns"":""92.38.154.54"",""Ip"":""92.38.154.54"",""Online"":true,""Enabled"":true}" +
                @"]}",
            ["event_2years_capture_the_flag_k"] = "1.0",
            ["event_2years_sniper_duel_k"] = "1.0",
            ["event_2years_rampage_k"] = "1.0",
            ["event_2years_end_date_android"] = "30",
            ["ranked_client_matchmaking_config"] = @"{""SearchRange"":[0,20,40,60,100,150,200],""CalibrationMathces"":10,""PlayerTTL"":100000,""BanDurations"":[900,3600,43200,86400],""SearchRoomCreateInterval"":4,""SearchRoomSizeStep"":2,""SearchStepDelay"":1000}"
        };

        foreach (var setting in settings)
        {
            await UpsertGameSettingAsync(setting.Key, setting.Value);
        }
        
        _logger.LogInformation("✅ Initialized {Count} game settings", settings.Count);
    }

    public async Task EnsureDefaultPlayerStatsAsync()
    {
        _logger.LogInformation("🔧 Ensuring default player stats...");
        var defaultStats = new[]
        {
            // Ranked matchmaking stats
            new { Name = "ranked_rank", IntValue = 0, Type = "INT" },
            new { Name = "ranked_current_mmr", IntValue = 0, Type = "INT" },
            new { Name = "ranked_ban_code", IntValue = 0, Type = "INT" },
            new { Name = "ranked_ban_duration", IntValue = 0, Type = "INT" },
            new { Name = "ranked_calibration_match_count", IntValue = 0, Type = "INT" },
            new { Name = "ranked_last_activity_time1", IntValue = 0, Type = "INT" },
            new { Name = "ranked_last_activity_time2", IntValue = 0, Type = "INT" },
            new { Name = "ranked_last_match_status", IntValue = 0, Type = "INT" },
            new { Name = "ranked_matches", IntValue = 0, Type = "INT" },
            new { Name = "ranked_wins", IntValue = 0, Type = "INT" },
            new { Name = "ranked_losses", IntValue = 0, Type = "INT" },
            
            // General stats
            new { Name = "total_kills", IntValue = 0, Type = "INT" },
            new { Name = "total_deaths", IntValue = 0, Type = "INT" },
            new { Name = "total_matches", IntValue = 0, Type = "INT" },
            new { Name = "event_2years_score", IntValue = 0, Type = "INT" }
        };

        var players = await _players.Find(_ => true).ToListAsync();
        _logger.LogInformation("📊 Found {Count} players to check for default stats", players.Count);
        
        foreach (var player in players)
        {
            bool needsUpdate = false;
            
            foreach (var defaultStat in defaultStats)
            {
                if (!player.Stats.ArrayCust.Any(s => s.Name == defaultStat.Name))
                {
                    player.Stats.ArrayCust.Add(new StatItem
                    {
                        Name = defaultStat.Name,
                        IntValue = defaultStat.IntValue,
                        FloatValue = 0f,
                        Window = 0f,
                        Type = defaultStat.Type
                    });
                    needsUpdate = true;
                }
            }
            
            if (needsUpdate)
            {
                await _players.ReplaceOneAsync(p => p.Id == player.Id, player);
                _logger.LogDebug("Updated player {Name} with new stats", player.Name);
            }
        }
        
        _logger.LogInformation("✅ Default player stats initialization completed");
    }

    private async Task InitializeInventoryAsync()
    {
        _logger.LogInformation("🎒 Initializing inventory system...");
        await InventoryInitializer.InitializeAsync(this);
    }

    private async Task InitializeMarketplaceConfigAsync()
    {
        _logger.LogInformation("🛒 Initializing marketplace config...");
        await MarketplaceInitializer.InitializeAsync(this);
    }

    private async Task InitializeGameEventsAsync()
    {
        _logger.LogInformation("🎮 Initializing game events...");
        
        var eventsCollection = _database.GetCollection<GameEventDefinition>("game_events");
        var count = await eventsCollection.CountDocumentsAsync(_ => true);
        
        if (count == 0)
        {
            var now = DateTimeOffset.UtcNow;
            var eventStart = now.AddDays(-1).ToUnixTimeMilliseconds();
            var eventEnd = now.AddDays(365).ToUnixTimeMilliseconds();
            
            var defaultEvent = new GameEventDefinition
            {
                EventId = "battlepass_2024",
                Code = "battlepass",
                DateSince = eventStart,
                DateUntil = eventEnd,
                DurationDays = 365,
                IsEnabled = true,
                GamePasses = new List<GamePassDefinition>
                {
                    new GamePassDefinition
                    {
                        PassId = "free_pass",
                        Code = "free",
                        KeyItemDefinitionId = 0,
                        Levels = GeneratePassLevels(50, 100)
                    },
                    new GamePassDefinition
                    {
                        PassId = "gold_pass",
                        Code = "gold",
                        KeyItemDefinitionId = 601,
                        Levels = GeneratePassLevels(50, 100)
                    }
                }
            };
            
            await eventsCollection.InsertOneAsync(defaultEvent);
            _logger.LogInformation("✅ Created default game event: {EventId}", defaultEvent.EventId);
        }
    }

    private List<GamePassLevelDefinition> GeneratePassLevels(int maxLevel, int pointsPerLevel)
    {
        var levels = new List<GamePassLevelDefinition>();
        for (int i = 1; i <= maxLevel; i++)
        {
            levels.Add(new GamePassLevelDefinition
            {
                Level = i,
                RequiredPoints = i * pointsPerLevel,
                Rewards = new List<RewardDefinition>
                {
                    new RewardDefinition
                    {
                        Type = "currency",
                        CurrencyId = 101,
                        Amount = i * 50
                    }
                }
            });
        }
        return levels;
    }

    // ==================== INTERACTIVE METHODS ====================

    public async Task AddGameVersionInteractiveAsync()
    {
        _logger.LogInformation("\n=== Add New Game Version ===");
        
        Console.Write("Enter game version (e.g., 0.37.0): ");
        var version = Console.ReadLine()?.Trim();
        if (string.IsNullOrEmpty(version))
        {
            Console.WriteLine("Invalid version");
            return;
        }

        var existingCount = await CountHashByVersionAsync(version);
        if (existingCount > 0)
        {
            Console.WriteLine($"Version {version} already exists in database");
            return;
        }

        Console.Write("Enter APK hash: ");
        var hash = Console.ReadLine()?.Trim();
        if (string.IsNullOrEmpty(hash))
        {
            Console.WriteLine("Invalid hash");
            return;
        }

        Console.Write("Enter APK signature: ");
        var signature = Console.ReadLine()?.Trim();
        if (string.IsNullOrEmpty(signature))
        {
            Console.WriteLine("Invalid signature");
            return;
        }

        try
        {
            await InsertHashAsync(version, hash, signature);
            Console.WriteLine($"✅ Successfully added game version: {version}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Error adding version: {ex.Message}");
        }
    }

    public async Task ListGameVersionsAsync()
    {
        _logger.LogInformation("\n=== Current Game Versions (StandRaid v0.37.0) ===");
        
        try
        {
            var hashes = await GetAllHashesAsync();
            if (hashes.Count == 0)
            {
                Console.WriteLine("No game versions found in database");
                return;
            }

            Console.WriteLine($"{"Version",-15} {"Hash",-25} {"Signature",-25}");
            Console.WriteLine(new string('-', 70));
            
            foreach (var hash in hashes)
            {
                Console.WriteLine($"{hash.Version,-15} {hash.Hash,-25} {hash.Signature,-25}");
            }
            
            Console.WriteLine($"\nTotal versions: {hashes.Count}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Error listing versions: {ex.Message}");
        }
    }
}