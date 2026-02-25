using MongoDB.Bson;
using MongoDB.Driver;
using StandRiseServer.Models;
using Telegram.Bot;

namespace StandRiseServer.TelegramMarketBot;

public class MarketBotWorker
{
    private readonly IMongoDatabase _database;
    private readonly long _chatId;
    private readonly TelegramBotClient _botClient;
    private CancellationTokenSource? _cts;
    private readonly Random _random = new();

    public int ListedCount { get; private set; }
    public bool IsRunning { get; private set; }

    // Список всех скинов (без кейсов 301-304, 401-404)
    private static readonly int[] AllSkinIds = new[]
    {
        // Origin
        15001, 45002, 51002, 62001, 13001, 32001, 35002, 45001, 46002, 47002,
        11002, 44002, 71001, 71002, 71003, 71004, 71005,
        // StatTrack Origin
        1015001, 1045002, 1051002, 1013001, 1032001, 1035002, 1045001, 1046002, 1047002, 1011002, 1044002,
        // Furious
        15002, 48001, 32003, 44004, 52001, 51003, 13003, 48002, 46006, 51004, 32004, 72002, 72006, 72004, 72007,
        // Rival
        13004, 44006, 48003, 52003, 15006, 34001, 46007, 11008, 34002, 44005, 32005, 51007, 73002, 73003, 73004, 73006,
        // Fable
        41701, 44601, 41102, 41502, 41703, 45301, 41212, 43202, 44903, 41605, 43402, 44603, 47504, 47502, 47503, 47505
    };

    // Рандомные аватарки
    private static readonly string[] Avatars = new[]
    {
        "avatar_1", "avatar_2", "avatar_3", "avatar_4", "avatar_5",
        "avatar_6", "avatar_7", "avatar_8", "avatar_9", "avatar_10",
        "default", "soldier", "sniper", "medic", "assault"
    };

    // Рандомные части ника
    private static readonly string[] NameParts = new[]
    {
        "Pro", "Killer", "Sniper", "Shadow", "Ghost", "Ninja", "Hunter",
        "Wolf", "Tiger", "Eagle", "Hawk", "Viper", "Cobra", "Dragon",
        "Fire", "Ice", "Storm", "Thunder", "Dark", "Light", "Steel"
    };

    public MarketBotWorker(IMongoDatabase database, long chatId, TelegramBotClient botClient)
    {
        _database = database;
        _chatId = chatId;
        _botClient = botClient;
    }

    public async Task StartAsync()
    {
        _cts = new CancellationTokenSource();
        IsRunning = true;
        ListedCount = 0;

        Console.WriteLine("🤖 MarketBotWorker started - filling market with random skins");

        try
        {
            while (IsRunning && !_cts.Token.IsCancellationRequested)
            {
                await ListRandomSkinAsync();
                await Task.Delay(1000, _cts.Token); // 1 секунда
            }
        }
        catch (OperationCanceledException)
        {
            Console.WriteLine("🤖 MarketBotWorker stopped");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ MarketBotWorker error: {ex.Message}");
            try
            {
                await _botClient.SendMessage(_chatId, $"❌ Ошибка бота: {ex.Message}");
            }
            catch { }
        }
        finally
        {
            IsRunning = false;
        }
    }

    private async Task ListRandomSkinAsync()
    {
        // Генерируем рандомного бота-продавца
        var botId = ObjectId.GenerateNewId().ToString();
        var botName = $"Bot_{NameParts[_random.Next(NameParts.Length)]}{_random.Next(100, 999)}";
        var botAvatar = Avatars[_random.Next(Avatars.Length)];
        var botUid = $"BOT{_random.Next(100000, 999999)}";

        // Рандомный скин
        var skinId = AllSkinIds[_random.Next(AllSkinIds.Length)];
        var itemInstanceId = _random.Next(1000000, 9999999);

        // Получаем конфиг рынка
        var configCollection = _database.GetCollection<MarketplaceConfig>("marketplace_config");
        var config = await configCollection.Find(c => c.ConfigId == "default").FirstOrDefaultAsync()
            ?? new MarketplaceConfig();

        // Создаём листинг
        var listingCollection = _database.GetCollection<MarketplaceListing>("marketplace_listings");
        var listing = new MarketplaceListing
        {
            Id = ObjectId.GenerateNewId(),
            ListingId = Guid.NewGuid().ToString(),
            SellerId = botId,
            SellerName = botName,
            ItemDefinitionId = skinId,
            InventoryItemId = itemInstanceId,
            Price = 1.0f, // 1 Gold
            CurrencyId = config.CurrencyId,
            Quantity = 1,
            Status = ListingStatus.Active,
            CreatedAt = DateTime.UtcNow,
            ExpiresAt = DateTime.UtcNow.AddHours(config.ListingDurationHours)
        };

        await listingCollection.InsertOneAsync(listing);
        ListedCount++;

        Console.WriteLine($"📤 Bot listed skin {skinId} by {botName} (Total: {ListedCount})");

        // Уведомляем каждые 10 выставлений
        if (ListedCount % 10 == 0)
        {
            try
            {
                await _botClient.SendMessage(_chatId, $"📤 Выставлено {ListedCount} скинов");
            }
            catch { }
        }
    }

    public void Stop()
    {
        IsRunning = false;
        _cts?.Cancel();
    }
}
