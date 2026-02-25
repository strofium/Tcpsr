using System.Net.Sockets;
using Axlebolt.RpcSupport.Protobuf;
using Axlebolt.Bolt.Protobuf;
using StandRiseServer.Core;
using StandRiseServer.Models;
using StandRiseServer.Config;
using Google.Protobuf;
using MongoDB.Bson;
using MongoDB.Driver;

namespace StandRiseServer.Services;

public class GameEventService
{
    private readonly ProtobufHandler _handler;
    private readonly DatabaseService _database;
    private readonly SessionManager _sessionManager;
    private readonly Random _random = new();

    // Конфигурация текущего ивента - NEW YEAR MADNESS 2020
    private const string CURRENT_EVENT_CODE = "new_year_madness_2020";
    private const int EVENT_DURATION_DAYS = 60;
    private const int GOLD_PASS_ITEM_ID = 602; // New Year Madness 2020 Gold Pass

    // ID валют (как в клиенте)
    private const int CURRENCY_SILVER = 101; // Серебро/монеты
    private const int CURRENCY_GOLD = 102;   // Золото/гемы
    private const int CURRENCY_KEYS = 103;   // Ключи

    // Рецепты для наград (из Unity asset)
    private const string RECIPE_SKINS = "NEW_YEAR_2020_SKINS";
    private const string RECIPE_SKINS_ST = "NEW_YEAR_2020_SKINS_STATTRACK";
    private const string RECIPE_STICKERS = "NEW_YEAR_2020_STICKERS";
    private const string RECIPE_KNIVES = "NEW_YEAR_2020_KNIVES";
    private const string RECIPE_CASES = "NEW_YEAR_2020_CASES";
    private const string RECIPE_BOXES = "NEW_YEAR_2020_BOXES";

    // Медали NewYear2020 (ItemReward в Unity asset)
    private const int MEDAL_BRONZE = 120;
    private const int MEDAL_SILVER = 121;
    private const int MEDAL_GOLD = 122;
    private const int MEDAL_PLATINUM = 123;
    private const int MEDAL_BRILLIANT = 124;

    public GameEventService(ProtobufHandler handler, DatabaseService database, SessionManager sessionManager)
    {
        _handler = handler;
        _database = database;
        _sessionManager = sessionManager;

        Console.WriteLine("🎮 Registering GameEventService handlers...");
        _handler.RegisterHandler("GameEventRemoteService", "getCurrentGameEvents", GetCurrentGameEventsAsync);
        _handler.RegisterHandler("GameEventRemoteService", "getCurrentChallenges", GetCurrentChallengesAsync);
        _handler.RegisterHandler("GameEventRemoteService", "setChallengeProgress", SetChallengeProgressAsync);
        _handler.RegisterHandler("GameEventRemoteService", "progressGameEvent", ProgressGameEventAsync);
        _handler.RegisterHandler("GameEventRemoteService", "saveChallenge", SaveChallengeAsync);
        _handler.RegisterHandler("GameEventRemoteService", "claimReward", ClaimRewardAsync);
        Console.WriteLine("🎮 GameEventService handlers registered!");
        
        InitializeGameEventAsync().Wait();
    }

    private Models.Player? GetCurrentPlayer(TcpClient client)
    {
        var session = _sessionManager.GetSessionByClient(client);
        if (session == null)
        {
            session = _sessionManager.GetAllSessions().FirstOrDefault();
            if (session != null) session.Client = client;
        }
        if (session == null) return null;
        return _database.GetPlayerByTokenAsync(session.Token).Result;
    }

    private async Task InitializeGameEventAsync()
    {
        try
        {
            var eventsCollection = _database.GetCollection<GameEventDefinition>("GameEvents");
            
            // Удаляем старый ивент и создаём новый
            await eventsCollection.DeleteManyAsync(e => e.Code == CURRENT_EVENT_CODE);
            
            Console.WriteLine("🎮 Creating New Year Madness 2020 Battle Pass...");
            var newEvent = CreateNewYearMadness2020Event();
            await eventsCollection.InsertOneAsync(newEvent);
            Console.WriteLine("🎮 Battle Pass event created!");
            
            // Создаём миссии
            var challengesCollection = _database.GetCollection<GameChallenge>("GameChallenges");
            await challengesCollection.DeleteManyAsync(c => c.GameEventId == CURRENT_EVENT_CODE);
            
            Console.WriteLine("🎮 Creating challenges...");
            var challenges = CreateNewYear2020Challenges();
            await challengesCollection.InsertManyAsync(challenges);
            Console.WriteLine($"🎮 Created {challenges.Count} challenges!");
            
            // Очищаем старый прогресс игроков (для тестирования)
            var progressCollection = _database.GetCollection<PlayerGameEventProgress>("PlayerGameEventProgress");
            await progressCollection.DeleteManyAsync(p => p.EventId == CURRENT_EVENT_CODE);
            Console.WriteLine("🎮 Cleared old player progress!");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Error initializing game event: {ex.Message}");
        }
    }

    private GameEventDefinition CreateNewYearMadness2020Event()
    {
        var now = DateTimeOffset.UtcNow;
        return new GameEventDefinition
        {
            EventId = ObjectId.GenerateNewId().ToString(),
            Code = CURRENT_EVENT_CODE,
            DateSince = now.ToUnixTimeSeconds(),
            DateUntil = now.AddDays(EVENT_DURATION_DAYS).ToUnixTimeSeconds(),
            DurationDays = EVENT_DURATION_DAYS,
            IsEnabled = true,
            GamePasses = new List<GamePassDefinition>
            {
                CreateFreePass(),
                CreateGoldPass()
            }
        };
    }

    private GamePassDefinition CreateFreePass()
    {
        var levels = new List<GamePassLevelDefinition>();
        
        // 28 уровней как в Unity asset (_levelsPerCycle: 28)
        // Уровни начинаются с 0 (индекс в массиве)
        for (int i = 0; i < 28; i++)
        {
            var rewards = new List<RewardDefinition>();
            
            // Распределение наград Free Pass - больше разнообразия:
            // Уровень 0 - серебро 25
            // Уровни 3, 10, 17, 24 - кейсы
            // Уровни 6, 13, 20, 27 - стикеры
            // Уровни 4, 9, 14, 19 - скины
            // Уровни 7, 21 - ключи
            // Остальные - серебро (разные суммы)
            
            if (i == 3 || i == 10 || i == 17 || i == 24)
            {
                // Кейсы
                rewards.Add(new RewardDefinition
                {
                    Type = "recipe",
                    Recipe = RECIPE_CASES,
                    Amount = 1
                });
            }
            else if (i == 6 || i == 13 || i == 20 || i == 27)
            {
                // Стикеры
                rewards.Add(new RewardDefinition
                {
                    Type = "recipe",
                    Recipe = RECIPE_STICKERS,
                    Amount = 1
                });
            }
            else if (i == 4 || i == 9 || i == 14 || i == 19)
            {
                // Скины
                rewards.Add(new RewardDefinition
                {
                    Type = "recipe",
                    Recipe = RECIPE_SKINS,
                    Amount = 1
                });
            }
            else if (i == 7 || i == 21)
            {
                // Ключи
                rewards.Add(new RewardDefinition
                {
                    Type = "currency",
                    CurrencyId = CURRENCY_KEYS,
                    Amount = 1
                });
            }
            else
            {
                // Серебро - разные суммы для разных спрайтов
                // 0-25 = первый спрайт, 26-50 = второй, 51+ = третий
                int silverAmount;
                if (i == 0) silverAmount = 25; // Первый уровень - 25 серебра
                else if (i < 5) silverAmount = 15 + i * 3; // 18-27 (первый спрайт)
                else if (i < 12) silverAmount = 30 + i * 2; // 40-52 (второй спрайт)
                else silverAmount = 55 + i * 3; // 91+ (третий спрайт)
                
                rewards.Add(new RewardDefinition
                {
                    Type = "currency",
                    CurrencyId = CURRENCY_SILVER,
                    Amount = silverAmount
                });
            }
            
            levels.Add(new GamePassLevelDefinition
            {
                Level = i, // Уровень начинается с 0
                RequiredPoints = i * 1000, // 0, 1000, 2000... (1000 очков на уровень)
                Rewards = rewards
            });
        }
        
        return new GamePassDefinition
        {
            PassId = "free_pass",
            Code = "FreePass",
            KeyItemDefinitionId = 0,
            Levels = levels
        };
    }

    private GamePassDefinition CreateGoldPass()
    {
        var levels = new List<GamePassLevelDefinition>();
        
        // 28 уровней как в Unity asset
        for (int i = 0; i < 28; i++)
        {
            var rewards = new List<RewardDefinition>();
            
            // Распределение наград Gold Pass:
            // Уровень 0 - Bronze медаль (120)
            // Уровень 6 - Silver медаль (121)
            // Уровень 13 - Gold медаль (122)
            // Уровень 20 - Platinum медаль (123)
            // Уровень 27 - Brilliant медаль (124)
            // Уровни 4, 9, 14, 19, 24 - ножи
            // Уровни 2, 5, 8, 11, 17, 23 - StatTrack скины
            // Остальные - обычные скины
            
            if (i == 0)
            {
                rewards.Add(new RewardDefinition
                {
                    Type = "item",
                    ItemDefinitionId = MEDAL_BRONZE,
                    Amount = 1
                });
            }
            else if (i == 6)
            {
                rewards.Add(new RewardDefinition
                {
                    Type = "item",
                    ItemDefinitionId = MEDAL_SILVER,
                    Amount = 1
                });
            }
            else if (i == 13)
            {
                rewards.Add(new RewardDefinition
                {
                    Type = "item",
                    ItemDefinitionId = MEDAL_GOLD,
                    Amount = 1
                });
            }
            else if (i == 20)
            {
                rewards.Add(new RewardDefinition
                {
                    Type = "item",
                    ItemDefinitionId = MEDAL_PLATINUM,
                    Amount = 1
                });
            }
            else if (i == 27)
            {
                rewards.Add(new RewardDefinition
                {
                    Type = "item",
                    ItemDefinitionId = MEDAL_BRILLIANT,
                    Amount = 1
                });
            }
            else if (i == 4 || i == 9 || i == 14 || i == 19 || i == 24)
            {
                // Ножи
                rewards.Add(new RewardDefinition
                {
                    Type = "recipe",
                    Recipe = RECIPE_KNIVES,
                    Amount = 1
                });
            }
            else if (i == 2 || i == 5 || i == 8 || i == 11 || i == 17 || i == 23)
            {
                // StatTrack скины
                rewards.Add(new RewardDefinition
                {
                    Type = "recipe",
                    Recipe = RECIPE_SKINS_ST,
                    Amount = 1
                });
            }
            else
            {
                // Обычные скины
                rewards.Add(new RewardDefinition
                {
                    Type = "recipe",
                    Recipe = RECIPE_SKINS,
                    Amount = 1
                });
            }
            
            levels.Add(new GamePassLevelDefinition
            {
                Level = i, // Уровень начинается с 0
                RequiredPoints = i * 1000, // 0, 1000, 2000... (1000 очков на уровень)
                Rewards = rewards
            });
        }
        
        return new GamePassDefinition
        {
            PassId = "gold_pass",
            Code = "GoldPass",
            KeyItemDefinitionId = GOLD_PASS_ITEM_ID,
            Levels = levels
        };
    }

    private List<GameChallenge> CreateNewYear2020Challenges()
    {
        var challenges = new List<GameChallenge>();
        
        // Ежедневные задания (Daily) - Type = "D"
        challenges.Add(CreateDailyChallenge("daily_kills_10", "{\"type\":\"kills\",\"count\":10}", 10, 500, "Убейте 10 врагов", "Убейте 10 врагов сегодня"));
        challenges.Add(CreateDailyChallenge("daily_wins_3", "{\"type\":\"wins\",\"count\":3}", 3, 750, "Выиграйте 3 матча", "Выиграйте 3 матча сегодня"));
        challenges.Add(CreateDailyChallenge("daily_headshots_5", "{\"type\":\"headshots\",\"count\":5}", 5, 500, "5 хедшотов", "Сделайте 5 хедшотов сегодня"));
        challenges.Add(CreateDailyChallenge("daily_matches_5", "{\"type\":\"matches\",\"count\":5}", 5, 400, "Сыграйте 5 матчей", "Сыграйте 5 матчей сегодня"));
        challenges.Add(CreateDailyChallenge("daily_open_case", "{\"type\":\"open_case\",\"count\":1}", 1, 5000, "Открыть кейс", "Откройте любой кейс"));
        
        // Еженедельные задания (Weekly) - Type = "W", создаём для каждой недели
        for (int week = 1; week <= 8; week++)
        {
            int dayFrom = 7 * week - 6; // 1, 8, 15, 22...
            int dayTo = 7 * week;       // 7, 14, 21, 28...
            
            challenges.Add(CreateWeeklyChallenge($"weekly_kills_100_w{week}", "{\"type\":\"kills\",\"count\":100}", 100, 2000, "Убейте 100 врагов", $"Убейте 100 врагов за неделю {week}", dayFrom, dayTo));
            challenges.Add(CreateWeeklyChallenge($"weekly_wins_15_w{week}", "{\"type\":\"wins\",\"count\":15}", 15, 2500, "Выиграйте 15 матчей", $"Выиграйте 15 матчей за неделю {week}", dayFrom, dayTo));
            challenges.Add(CreateWeeklyChallenge($"weekly_headshots_50_w{week}", "{\"type\":\"headshots\",\"count\":50}", 50, 2000, "50 хедшотов", $"Сделайте 50 хедшотов за неделю {week}", dayFrom, dayTo));
            challenges.Add(CreateWeeklyChallenge($"weekly_playtime_300_w{week}", "{\"type\":\"playtime\",\"minutes\":300}", 300, 1500, "Время в игре", $"Проведите 300 минут в игре за неделю {week}", dayFrom, dayTo));
        }
        
        return challenges;
    }

    private GameChallenge CreateDailyChallenge(string code, string action, int targetPoints, int eventPoints, string name, string description)
    {
        return new GameChallenge
        {
            ChallengeId = ObjectId.GenerateNewId().ToString(),
            GameEventId = CURRENT_EVENT_CODE,
            Code = code,
            Type = "D", // Daily - клиент ищет по "D"
            Action = action,
            TargetPoints = targetPoints,
            EventPoints = eventPoints,
            Name = name,
            Description = description,
            DayRange = new DayRangeModel { From = 1, To = EVENT_DURATION_DAYS },
            IsEnabled = true
        };
    }

    private GameChallenge CreateWeeklyChallenge(string code, string action, int targetPoints, int eventPoints, string name, string description, int dayFrom, int dayTo)
    {
        return new GameChallenge
        {
            ChallengeId = ObjectId.GenerateNewId().ToString(),
            GameEventId = CURRENT_EVENT_CODE,
            Code = code,
            Type = "W", // Weekly - клиент ищет по "W" и DayRange.From == 7 * week - 6
            Action = action,
            TargetPoints = targetPoints,
            EventPoints = eventPoints,
            Name = name,
            Description = description,
            DayRange = new DayRangeModel { From = dayFrom, To = dayTo },
            IsEnabled = true
        };
    }

    private GameChallenge CreateChallenge(string code, string type, string action, int targetPoints, int rewardPoints)
    {
        return new GameChallenge
        {
            ChallengeId = ObjectId.GenerateNewId().ToString(),
            GameEventId = CURRENT_EVENT_CODE,
            Code = code,
            Type = type,
            Action = action,
            TargetPoints = targetPoints,
            DayRange = new DayRangeModel { From = 1, To = EVENT_DURATION_DAYS },
            IsEnabled = true
        };
    }

    private async Task<PlayerGameEventProgress> GetOrCreatePlayerProgressAsync(string playerId)
    {
        var progressCollection = _database.GetCollection<PlayerGameEventProgress>("PlayerGameEventProgress");
        var progress = await progressCollection.Find(p => p.PlayerId == playerId && p.EventId == CURRENT_EVENT_CODE).FirstOrDefaultAsync();
        
        if (progress == null)
        {
            progress = new PlayerGameEventProgress
            {
                PlayerId = playerId,
                EventId = CURRENT_EVENT_CODE,
                Points = 0,
                PassLevels = new Dictionary<string, int>
                {
                    { "FreePass", 0 },
                    { "GoldPass", 0 }
                },
                ChallengeProgress = new Dictionary<string, int>()
            };
            await progressCollection.InsertOneAsync(progress);
        }
        
        return progress;
    }

    private async Task UpdatePlayerProgressAsync(PlayerGameEventProgress progress)
    {
        var progressCollection = _database.GetCollection<PlayerGameEventProgress>("PlayerGameEventProgress");
        await progressCollection.ReplaceOneAsync(p => p.Id == progress.Id, progress);
    }

    private int CalculateCurrentDay(long dateSince)
    {
        var since = DateTimeOffset.FromUnixTimeSeconds(dateSince).UtcDateTime;
        var currentDay = (int)(DateTime.UtcNow - since).TotalDays + 1;
        return Math.Max(1, Math.Min(currentDay, EVENT_DURATION_DAYS));
    }

    private int CalculateLevelFromPoints(int points, List<GamePassLevelDefinition> levels)
    {
        int level = 0;
        foreach (var lvl in levels.OrderBy(l => l.Level))
        {
            if (points >= lvl.RequiredPoints)
                level = lvl.Level;
            else
                break;
        }
        return level;
    }

    private async Task GetCurrentGameEventsAsync(TcpClient client, RpcRequest request)
    {
        try
        {
            Console.WriteLine("🎮 GetCurrentGameEvents");
            var player = GetCurrentPlayer(client);
            
            var response = new GetCurrentGameEventsResponse();
            
            if (player == null)
            {
                Console.WriteLine("🎮 GetCurrentGameEvents: No player found");
                var result = new BinaryValue { IsNull = false, One = ByteString.CopyFrom(response.ToByteArray()) };
                await _handler.WriteProtoResponseAsync(client, request.Id, result, null);
                return;
            }

            var eventsCollection = _database.GetCollection<GameEventDefinition>("GameEvents");
            var gameEvent = await eventsCollection.Find(e => e.Code == CURRENT_EVENT_CODE && e.IsEnabled).FirstOrDefaultAsync();
            
            if (gameEvent == null)
            {
                Console.WriteLine("🎮 GetCurrentGameEvents: No active event");
                var result = new BinaryValue { IsNull = false, One = ByteString.CopyFrom(response.ToByteArray()) };
                await _handler.WriteProtoResponseAsync(client, request.Id, result, null);
                return;
            }

            var playerProgress = await GetOrCreatePlayerProgressAsync(player.PlayerUid);
            var currentDay = CalculateCurrentDay(gameEvent.DateSince);

            // Проверяем есть ли у игрока Gold Pass
            bool hasGoldPass = player.Inventory.Items.Any(i => i.DefinitionId == GOLD_PASS_ITEM_ID);

            var currentGameEvent = new CurrentGameEvent
            {
                Id = gameEvent.EventId,
                Code = gameEvent.Code,
                DateSince = gameEvent.DateSince,
                DateUntil = gameEvent.DateUntil,
                DurationDays = gameEvent.DurationDays,
                CurrentDay = currentDay,
                Points = playerProgress.Points
            };

            // Добавляем Free Pass
            var freePassDef = gameEvent.GamePasses.FirstOrDefault(p => p.Code == "FreePass");
            if (freePassDef != null)
            {
                var freePass = new GamePass
                {
                    Id = freePassDef.PassId,
                    Code = freePassDef.Code,
                    KeyItemDefinitionId = freePassDef.KeyItemDefinitionId,
                    CurrentLevel = CalculateLevelFromPoints(playerProgress.Points, freePassDef.Levels)
                };
                
                foreach (var lvl in freePassDef.Levels)
                {
                    var passLevel = new GamePassLevel
                    {
                        Level = lvl.Level,
                        MinPoints = lvl.RequiredPoints,
                        Reward = ConvertRewards(lvl.Rewards)
                    };
                    freePass.Levels.Add(passLevel);
                }
                currentGameEvent.GamePasses.Add(freePass);
            }

            // Добавляем Gold Pass
            var goldPassDef = gameEvent.GamePasses.FirstOrDefault(p => p.Code == "GoldPass");
            if (goldPassDef != null)
            {
                var goldPass = new GamePass
                {
                    Id = goldPassDef.PassId,
                    Code = goldPassDef.Code,
                    KeyItemDefinitionId = goldPassDef.KeyItemDefinitionId,
                    CurrentLevel = hasGoldPass ? CalculateLevelFromPoints(playerProgress.Points, goldPassDef.Levels) : 0
                };
                
                foreach (var lvl in goldPassDef.Levels)
                {
                    var passLevel = new GamePassLevel
                    {
                        Level = lvl.Level,
                        MinPoints = lvl.RequiredPoints,
                        Reward = ConvertRewards(lvl.Rewards)
                    };
                    goldPass.Levels.Add(passLevel);
                }
                currentGameEvent.GamePasses.Add(goldPass);
            }

            response.GameEvents.Add(currentGameEvent);
            
            var resultBytes = new BinaryValue { IsNull = false, One = ByteString.CopyFrom(response.ToByteArray()) };
            await _handler.WriteProtoResponseAsync(client, request.Id, resultBytes, null);
            Console.WriteLine($"🎮 GetCurrentGameEvents: Sent event {gameEvent.Code}, day {currentDay}, points {playerProgress.Points}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ GetCurrentGameEvents: {ex.Message}");
            var response = new GetCurrentGameEventsResponse();
            var result = new BinaryValue { IsNull = false, One = ByteString.CopyFrom(response.ToByteArray()) };
            await _handler.WriteProtoResponseAsync(client, request.Id, result, null);
        }
    }

    private RewardInfo ConvertRewards(List<RewardDefinition> rewards)
    {
        var rewardInfo = new RewardInfo();
        foreach (var reward in rewards)
        {
            if (reward.Type == "currency")
            {
                rewardInfo.Currencies.Add(new CurrencyAmount
                {
                    CurrencyId = reward.CurrencyId,
                    Value = reward.Amount
                });
            }
            else if (reward.Type == "item")
            {
                rewardInfo.Items.Add(new InventoryItemAmount
                {
                    ItemDefinitionId = reward.ItemDefinitionId,
                    Quantity = reward.Amount
                });
            }
            else if (reward.Type == "recipe")
            {
                // Для рецептов добавляем Entities с возможными предметами
                var recipeInfo = new RecipeInfo
                {
                    Recipe = reward.Recipe,
                    Quantity = reward.Amount
                };
                
                // Добавляем предметы которые можно получить из рецепта
                var entity = new ExchangeEntity();
                var itemIds = GetItemsForRecipe(reward.Recipe);
                Console.WriteLine($"🎮 Recipe {reward.Recipe}: adding {itemIds.Length} items to entities");
                foreach (var itemId in itemIds)
                {
                    entity.InventoryItems.Add(new InventoryItemAmount
                    {
                        ItemDefinitionId = itemId,
                        Quantity = 1
                    });
                }
                recipeInfo.Entities.Add(entity);
                Console.WriteLine($"🎮 RecipeInfo entities count: {recipeInfo.Entities.Count}, items in entity: {entity.InventoryItems.Count}");
                
                rewardInfo.Recipes.Add(recipeInfo);
            }
        }
        return rewardInfo;
    }

    private int[] GetItemsForRecipe(string recipe)
    {
        return recipe switch
        {
            RECIPE_SKINS => NewYear2020Skins,
            RECIPE_SKINS_ST => NewYear2020SkinsST,
            RECIPE_STICKERS => NewYear2020Stickers,
            RECIPE_KNIVES => NewYear2020Knives,
            RECIPE_CASES => new[] { 301, 302, 303, 304 }, // Origin, Furious, Rival, Fable Cases
            RECIPE_BOXES => new[] { 401, 402, 403, 404 }, // Origin, Furious, Rival, Fable Boxes
            _ => Array.Empty<int>()
        };
    }

    private async Task GetCurrentChallengesAsync(TcpClient client, RpcRequest request)
    {
        try
        {
            Console.WriteLine("🎮 GetCurrentChallenges");
            var player = GetCurrentPlayer(client);
            
            var response = new GetCurrentChallengesResponse();
            
            if (player == null)
            {
                var result = new BinaryValue { IsNull = false, One = ByteString.CopyFrom(response.ToByteArray()) };
                await _handler.WriteProtoResponseAsync(client, request.Id, result, null);
                return;
            }

            var playerProgress = await GetOrCreatePlayerProgressAsync(player.PlayerUid);
            var challengesCollection = _database.GetCollection<GameChallenge>("GameChallenges");
            var challenges = await challengesCollection.Find(c => c.GameEventId == CURRENT_EVENT_CODE && c.IsEnabled).ToListAsync();

            var eventsCollection = _database.GetCollection<GameEventDefinition>("GameEvents");
            var gameEvent = await eventsCollection.Find(e => e.Code == CURRENT_EVENT_CODE).FirstOrDefaultAsync();
            var currentDay = gameEvent != null ? CalculateCurrentDay(gameEvent.DateSince) : 1;

            foreach (var challenge in challenges)
            {
                // Для еженедельных миссий НЕ фильтруем по текущему дню - клиент сам фильтрует по DayRange.From
                // Для ежедневных миссий тоже не фильтруем - они доступны всегда
                
                var currentPoints = playerProgress.ChallengeProgress.GetValueOrDefault(challenge.ChallengeId, 0);
                
                var currentChallenge = new CurrentChallenge
                {
                    GameEventChallengeId = challenge.ChallengeId,
                    Code = challenge.Code,
                    KeyItemDefinitionId = 0,
                    LocalizedTitle = new LocalizedTitle
                    {
                        Name = !string.IsNullOrEmpty(challenge.Name) ? challenge.Name : GetChallengeName(challenge.Code),
                        Description = !string.IsNullOrEmpty(challenge.Description) ? challenge.Description : GetChallengeDescription(challenge.Code, challenge.TargetPoints)
                    },
                    Action = challenge.Action,
                    DayRange = challenge.DayRange != null ? new DayRange
                    {
                        From = challenge.DayRange.From,
                        To = challenge.DayRange.To
                    } : new DayRange { From = 1, To = EVENT_DURATION_DAYS },
                    Type = challenge.Type,
                    EventPoints = challenge.EventPoints > 0 ? challenge.EventPoints : GetChallengeRewardPoints(challenge.Type),
                    TargetPoints = challenge.TargetPoints,
                    CurrentPoints = currentPoints
                };
                
                response.Challenges.Add(currentChallenge);
            }

            var resultBytes = new BinaryValue { IsNull = false, One = ByteString.CopyFrom(response.ToByteArray()) };
            await _handler.WriteProtoResponseAsync(client, request.Id, resultBytes, null);
            Console.WriteLine($"🎮 GetCurrentChallenges: Sent {response.Challenges.Count} challenges (day {currentDay})");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ GetCurrentChallenges: {ex.Message}");
            var response = new GetCurrentChallengesResponse();
            var result = new BinaryValue { IsNull = false, One = ByteString.CopyFrom(response.ToByteArray()) };
            await _handler.WriteProtoResponseAsync(client, request.Id, result, null);
        }
    }

    private string GetChallengeName(string code) => code switch
    {
        "daily_kills_10" => "Ежедневные убийства",
        "daily_wins_3" => "Ежедневные победы",
        "daily_headshots_5" => "Хедшоты дня",
        "daily_matches_5" => "Матчи дня",
        "weekly_kills_100" => "Недельные убийства",
        "weekly_wins_15" => "Недельные победы",
        "weekly_headshots_50" => "Хедшоты недели",
        "weekly_playtime_300" => "Время в игре",
        "season_kills_1000" => "Убийства сезона",
        "season_wins_100" => "Победы сезона",
        "season_headshots_500" => "Хедшоты сезона",
        "season_mvp_50" => "MVP сезона",
        _ => code
    };

    private string GetChallengeDescription(string code, int target) => code switch
    {
        "daily_kills_10" => $"Убейте {target} врагов сегодня",
        "daily_wins_3" => $"Выиграйте {target} матча сегодня",
        "daily_headshots_5" => $"Сделайте {target} хедшотов сегодня",
        "daily_matches_5" => $"Сыграйте {target} матчей сегодня",
        "weekly_kills_100" => $"Убейте {target} врагов за неделю",
        "weekly_wins_15" => $"Выиграйте {target} матчей за неделю",
        "weekly_headshots_50" => $"Сделайте {target} хедшотов за неделю",
        "weekly_playtime_300" => $"Проведите {target} минут в игре",
        "season_kills_1000" => $"Убейте {target} врагов за сезон",
        "season_wins_100" => $"Выиграйте {target} матчей за сезон",
        "season_headshots_500" => $"Сделайте {target} хедшотов за сезон",
        "season_mvp_50" => $"Станьте MVP {target} раз",
        _ => $"Выполните {target} действий"
    };

    private int GetChallengeRewardPoints(string type) => type switch
    {
        "daily" => 50,
        "weekly" => 200,
        "season" => 500,
        _ => 100
    };

    private async Task SetChallengeProgressAsync(TcpClient client, RpcRequest request)
    {
        try
        {
            Console.WriteLine("🎮 SetChallengeProgress");
            var player = GetCurrentPlayer(client);
            
            if (player == null)
            {
                var emptyResult = new BinaryValue { IsNull = true };
                await _handler.WriteProtoResponseAsync(client, request.Id, emptyResult, null);
                return;
            }

            ProgressChallengeRequest? progressRequest = null;
            if (request.Params.Count > 0 && request.Params[0].One != null)
            {
                progressRequest = ProgressChallengeRequest.Parser.ParseFrom(request.Params[0].One);
            }

            if (progressRequest == null)
            {
                var emptyResult = new BinaryValue { IsNull = true };
                await _handler.WriteProtoResponseAsync(client, request.Id, emptyResult, null);
                return;
            }

            var playerProgress = await GetOrCreatePlayerProgressAsync(player.PlayerUid);
            var challengesCollection = _database.GetCollection<GameChallenge>("GameChallenges");
            var challenge = await challengesCollection.Find(c => c.ChallengeId == progressRequest.GameEventChallengeId).FirstOrDefaultAsync();

            if (challenge == null)
            {
                var emptyResult = new BinaryValue { IsNull = true };
                await _handler.WriteProtoResponseAsync(client, request.Id, emptyResult, null);
                return;
            }

            int prevPoints = playerProgress.ChallengeProgress.GetValueOrDefault(challenge.ChallengeId, 0);
            bool wasCompleted = prevPoints >= challenge.TargetPoints;

            playerProgress.ChallengeProgress[challenge.ChallengeId] = progressRequest.Points;
            int newPoints = progressRequest.Points;
            bool isCompleted = newPoints >= challenge.TargetPoints;

            if (isCompleted && !wasCompleted)
            {
                int rewardPoints = GetChallengeRewardPoints(challenge.Type);
                playerProgress.Points += rewardPoints;
                Console.WriteLine($"🎮 Challenge {challenge.Code} completed! +{rewardPoints} event points");
            }

            await UpdatePlayerProgressAsync(playerProgress);

            var eventsCollection = _database.GetCollection<GameEventDefinition>("GameEvents");
            var gameEvent = await eventsCollection.Find(e => e.Code == CURRENT_EVENT_CODE).FirstOrDefaultAsync();
            
            var response = new ProgressChallengeResponse
            {
                ChallengePoints = newPoints,
                EventPoints = playerProgress.Points,
                Completed = isCompleted
            };

            if (gameEvent != null)
            {
                foreach (var pass in gameEvent.GamePasses)
                {
                    int level = CalculateLevelFromPoints(playerProgress.Points, pass.Levels);
                    response.EventGamePassLevels[pass.Code] = level;
                }
            }

            var resultBytes = new BinaryValue { IsNull = false, One = ByteString.CopyFrom(response.ToByteArray()) };
            await _handler.WriteProtoResponseAsync(client, request.Id, resultBytes, null);
            Console.WriteLine($"🎮 SetChallengeProgress: {challenge.Code} = {newPoints}/{challenge.TargetPoints}, completed={isCompleted}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ SetChallengeProgress: {ex.Message}");
            var emptyResult = new BinaryValue { IsNull = true };
            await _handler.WriteProtoResponseAsync(client, request.Id, emptyResult, null);
        }
    }

    private async Task ProgressGameEventAsync(TcpClient client, RpcRequest request)
    {
        try
        {
            Console.WriteLine("🎮 ProgressGameEvent");
            var player = GetCurrentPlayer(client);
            
            if (player == null)
            {
                var emptyResult = new BinaryValue { IsNull = true };
                await _handler.WriteProtoResponseAsync(client, request.Id, emptyResult, null);
                return;
            }

            ProgressGameEventRequest? progressRequest = null;
            if (request.Params.Count > 0 && request.Params[0].One != null)
            {
                progressRequest = ProgressGameEventRequest.Parser.ParseFrom(request.Params[0].One);
            }

            var playerProgress = await GetOrCreatePlayerProgressAsync(player.PlayerUid);
            
            if (progressRequest != null && progressRequest.Points > 0)
            {
                playerProgress.Points += progressRequest.Points;
                await UpdatePlayerProgressAsync(playerProgress);
            }

            var eventsCollection = _database.GetCollection<GameEventDefinition>("GameEvents");
            var gameEvent = await eventsCollection.Find(e => e.Code == CURRENT_EVENT_CODE).FirstOrDefaultAsync();
            
            var response = new ProgressGameEventResponse
            {
                EventPoints = playerProgress.Points
            };

            if (gameEvent != null)
            {
                foreach (var pass in gameEvent.GamePasses)
                {
                    int level = CalculateLevelFromPoints(playerProgress.Points, pass.Levels);
                    response.EventGamePassLevels[pass.Code] = level;
                }
            }

            var resultBytes = new BinaryValue { IsNull = false, One = ByteString.CopyFrom(response.ToByteArray()) };
            await _handler.WriteProtoResponseAsync(client, request.Id, resultBytes, null);
            Console.WriteLine($"🎮 ProgressGameEvent: points={playerProgress.Points}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ ProgressGameEvent: {ex.Message}");
            var emptyResult = new BinaryValue { IsNull = true };
            await _handler.WriteProtoResponseAsync(client, request.Id, emptyResult, null);
        }
    }

    private async Task SaveChallengeAsync(TcpClient client, RpcRequest request)
    {
        try
        {
            Console.WriteLine("🎮 SaveChallenge");
            var player = GetCurrentPlayer(client);
            
            if (player == null)
            {
                var emptyResult = new BinaryValue { IsNull = true };
                await _handler.WriteProtoResponseAsync(client, request.Id, emptyResult, null);
                return;
            }

            string code = "";
            int targetPoints = 0;

            if (request.Params.Count > 0 && request.Params[0].One != null)
                code = Axlebolt.RpcSupport.Protobuf.String.Parser.ParseFrom(request.Params[0].One).Value;
            if (request.Params.Count > 2 && request.Params[2].One != null)
                targetPoints = Integer.Parser.ParseFrom(request.Params[2].One).Value;

            var playerProgress = await GetOrCreatePlayerProgressAsync(player.PlayerUid);
            var challengesCollection = _database.GetCollection<GameChallenge>("GameChallenges");
            var challenge = await challengesCollection.Find(c => c.Code == code && c.GameEventId == CURRENT_EVENT_CODE).FirstOrDefaultAsync();
            
            if (challenge != null)
            {
                playerProgress.ChallengeProgress[challenge.ChallengeId] = targetPoints;
                await UpdatePlayerProgressAsync(playerProgress);
            }

            var emptyResultOk = new BinaryValue { IsNull = true };
            await _handler.WriteProtoResponseAsync(client, request.Id, emptyResultOk, null);
            Console.WriteLine($"🎮 SaveChallenge: {code} = {targetPoints}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ SaveChallenge: {ex.Message}");
            var emptyResult = new BinaryValue { IsNull = true };
            await _handler.WriteProtoResponseAsync(client, request.Id, emptyResult, null);
        }
    }

    private async Task ClaimRewardAsync(TcpClient client, RpcRequest request)
    {
        try
        {
            Console.WriteLine("🎮 ClaimReward");
            var player = GetCurrentPlayer(client);
            
            if (player == null)
            {
                var emptyResult = new BinaryValue { IsNull = true };
                await _handler.WriteProtoResponseAsync(client, request.Id, emptyResult, null);
                return;
            }

            string passCode = "";
            int level = 0;

            if (request.Params.Count > 0 && request.Params[0].One != null)
                passCode = Axlebolt.RpcSupport.Protobuf.String.Parser.ParseFrom(request.Params[0].One).Value;
            if (request.Params.Count > 1 && request.Params[1].One != null)
                level = Integer.Parser.ParseFrom(request.Params[1].One).Value;

            var playerProgress = await GetOrCreatePlayerProgressAsync(player.PlayerUid);
            var eventsCollection = _database.GetCollection<GameEventDefinition>("GameEvents");
            var gameEvent = await eventsCollection.Find(e => e.Code == CURRENT_EVENT_CODE).FirstOrDefaultAsync();

            if (gameEvent == null)
            {
                var emptyResult = new BinaryValue { IsNull = true };
                await _handler.WriteProtoResponseAsync(client, request.Id, emptyResult, null);
                return;
            }

            var pass = gameEvent.GamePasses.FirstOrDefault(p => p.Code == passCode);
            if (pass == null)
            {
                var emptyResult = new BinaryValue { IsNull = true };
                await _handler.WriteProtoResponseAsync(client, request.Id, emptyResult, null);
                return;
            }

            int currentLevel = CalculateLevelFromPoints(playerProgress.Points, pass.Levels);
            if (level > currentLevel)
            {
                Console.WriteLine($"🎮 ClaimReward: Level {level} not reached (current: {currentLevel})");
                var emptyResult = new BinaryValue { IsNull = true };
                await _handler.WriteProtoResponseAsync(client, request.Id, emptyResult, null);
                return;
            }

            if (passCode == "GoldPass")
            {
                bool hasGoldPass = player.Inventory.Items.Any(i => i.DefinitionId == GOLD_PASS_ITEM_ID);
                if (!hasGoldPass)
                {
                    Console.WriteLine("🎮 ClaimReward: No Gold Pass");
                    var emptyResult = new BinaryValue { IsNull = true };
                    await _handler.WriteProtoResponseAsync(client, request.Id, emptyResult, null);
                    return;
                }
            }

            var levelDef = pass.Levels.FirstOrDefault(l => l.Level == level);
            if (levelDef == null)
            {
                var emptyResult = new BinaryValue { IsNull = true };
                await _handler.WriteProtoResponseAsync(client, request.Id, emptyResult, null);
                return;
            }

            // Выдаём награды
            foreach (var reward in levelDef.Rewards)
            {
                if (reward.Type == "currency")
                {
                    switch (reward.CurrencyId)
                    {
                        case CURRENCY_SILVER: // 101 - серебро/монеты
                        case 1: 
                            player.Coins += reward.Amount; 
                            break;
                        case CURRENCY_GOLD: // 102 - золото/гемы
                        case 2: 
                            player.Gems += reward.Amount; 
                            break;
                        case CURRENCY_KEYS: // 103 - ключи
                        case 3: 
                            player.Keys += reward.Amount; 
                            break;
                    }
                    Console.WriteLine($"🎮 ClaimReward: +{reward.Amount} currency[{reward.CurrencyId}]");
                }
                else if (reward.Type == "item")
                {
                    var newItem = new PlayerInventoryItem
                    {
                        Id = player.Inventory.Items.Count > 0 ? player.Inventory.Items.Max(i => i.Id) + 1 : 1,
                        DefinitionId = reward.ItemDefinitionId,
                        Quantity = reward.Amount,
                        Date = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                        Flags = 0
                    };
                    player.Inventory.Items.Add(newItem);
                    Console.WriteLine($"🎮 ClaimReward: +item {reward.ItemDefinitionId}");
                }
                else if (reward.Type == "recipe")
                {
                    // Для рецептов выдаём случайный предмет из соответствующей коллекции
                    int itemId = GetRandomItemFromRecipe(reward.Recipe);
                    if (itemId > 0)
                    {
                        var newItem = new PlayerInventoryItem
                        {
                            Id = player.Inventory.Items.Count > 0 ? player.Inventory.Items.Max(i => i.Id) + 1 : 1,
                            DefinitionId = itemId,
                            Quantity = reward.Amount,
                            Date = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                            Flags = 0
                        };
                        player.Inventory.Items.Add(newItem);
                        Console.WriteLine($"🎮 ClaimReward: +recipe item {itemId} from {reward.Recipe}");
                    }
                }
            }

            await _database.UpdatePlayerAsync(player);

            var resultOk = new BinaryValue { IsNull = true };
            await _handler.WriteProtoResponseAsync(client, request.Id, resultOk, null);
            Console.WriteLine($"🎮 ClaimReward: {passCode} level {level} claimed!");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ ClaimReward: {ex.Message}");
            var emptyResult = new BinaryValue { IsNull = true };
            await _handler.WriteProtoResponseAsync(client, request.Id, emptyResult, null);
        }
    }

    // Скины NewYear2020 для рецептов
    private static readonly int[] NewYear2020Skins = { 65201, 66201, 63401, 65202, 61201, 61601, 65101, 61101 };
    private static readonly int[] NewYear2020SkinsST = { 1065101, 1061101, 1065201, 1065202, 1063401, 1066201, 1061601, 1061201 };
    private static readonly int[] NewYear2020Stickers = { 1121, 1122, 1123, 1124, 1125, 1126, 1127, 1128, 1129, 1130, 1131, 1132 };
    private static readonly int[] NewYear2020Knives = { 67701, 67702, 67703, 67704, 67705 };
    
    private int GetRandomItemFromRecipe(string recipe)
    {
        var random = new Random();
        return recipe switch
        {
            RECIPE_SKINS => NewYear2020Skins[random.Next(NewYear2020Skins.Length)],
            RECIPE_SKINS_ST => NewYear2020SkinsST[random.Next(NewYear2020SkinsST.Length)],
            RECIPE_STICKERS => NewYear2020Stickers[random.Next(NewYear2020Stickers.Length)],
            RECIPE_KNIVES => NewYear2020Knives[random.Next(NewYear2020Knives.Length)],
            RECIPE_CASES => new[] { 301, 302, 303, 304 }[random.Next(4)],
            RECIPE_BOXES => new[] { 401, 402, 403, 404 }[random.Next(4)],
            _ => 0
        };
    }
}
