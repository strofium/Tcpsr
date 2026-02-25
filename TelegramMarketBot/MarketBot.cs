using MongoDB.Bson;
using MongoDB.Driver;
using StandRiseServer.Models;
using Telegram.Bot;
using Telegram.Bot.Polling;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;
using System.Linq;

namespace StandRiseServer.TelegramMarketBot;

public class MarketBot
{
    private readonly TelegramBotClient _botClient;
    private readonly IMongoDatabase _database;
    private readonly Dictionary<long, bool> _authorizedUsers = new();
    private readonly Dictionary<long, PromoCreationState> _promoCreationStates = new();
    private MarketBotWorker? _worker;
    private readonly CancellationTokenSource _cts = new();
    
    // Пароль для доступа к боту
    private const string BotPassword = "admin123";

    // Состояние создания промокода
    private class PromoCreationState
    {
        public int Step { get; set; } // 1 = ожидание активаций, 2 = ожидание ID предмета
        public int MaxActivations { get; set; }
        public int ItemId { get; set; }
    }

    public MarketBot(string botToken, string mongoConnectionString, string databaseName)
    {
        _botClient = new TelegramBotClient(botToken);
        var mongoClient = new MongoClient(mongoConnectionString);
        _database = mongoClient.GetDatabase(databaseName);
    }

    public async Task StartAsync()
    {
        var receiverOptions = new ReceiverOptions
        {
            AllowedUpdates = Array.Empty<UpdateType>()
        };

        _botClient.StartReceiving(
            updateHandler: HandleUpdateAsync,
            errorHandler: HandleErrorAsync,
            receiverOptions: receiverOptions,
            cancellationToken: _cts.Token
        );

        var me = await _botClient.GetMe();
        Console.WriteLine($"🤖 Market Bot started: @{me.Username}");
    }

    private async Task HandleUpdateAsync(ITelegramBotClient bot, Update update, CancellationToken ct)
    {
        if (update.Message is not { } message) return;
        if (message.Text is not { } text) return;

        var chatId = message.Chat.Id;
        Console.WriteLine($"📩 [{chatId}] {text}");

        try
        {
            // Проверка авторизации
            if (!_authorizedUsers.ContainsKey(chatId) || !_authorizedUsers[chatId])
            {
                if (text == BotPassword)
                {
                    _authorizedUsers[chatId] = true;
                    await bot.SendMessage(chatId, "✅ Авторизация успешна!", cancellationToken: ct);
                    await SendMenuAsync(chatId, ct);
                    return;
                }
                await bot.SendMessage(chatId, "🔐 Введите пароль для доступа:", cancellationToken: ct);
                return;
            }

            await HandleCommandAsync(chatId, text, ct);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Error: {ex.Message}");
            await bot.SendMessage(chatId, $"❌ Ошибка: {ex.Message}", cancellationToken: ct);
        }
    }

    private async Task HandleCommandAsync(long chatId, string text, CancellationToken ct)
    {
        // Проверяем, находимся ли в процессе создания промокода
        if (_promoCreationStates.TryGetValue(chatId, out var state))
        {
            await HandlePromoCreationAsync(chatId, text, state, ct);
            return;
        }

        switch (text.ToLower())
        {
            case "/start":
            case "📋 меню":
                await SendMenuAsync(chatId, ct);
                break;

            case "/status":
            case "📊 статус":
                await SendStatusAsync(chatId, ct);
                break;

            case "/startbot":
            case "▶️ запустить":
                await StartWorkerAsync(chatId, ct);
                break;

            case "/stopbot":
            case "⏹️ остановить":
                await StopWorkerAsync(chatId, ct);
                break;

            case "/clear":
            case "🗑️ очистить рынок":
                await ClearMarketAsync(chatId, ct);
                break;

            case "/createpromo":
            case "🎟️ создать промокод":
                await StartPromoCreationAsync(chatId, ct);
                break;

            case "/listpromos":
            case "📋 список промокодов":
                await ListPromosAsync(chatId, ct);
                break;

            default:
                await _botClient.SendMessage(chatId, "❓ Неизвестная команда", cancellationToken: ct);
                break;
        }
    }

    private async Task SendMenuAsync(long chatId, CancellationToken ct)
    {
        var isRunning = _worker?.IsRunning ?? false;
        var status = isRunning ? "🟢 Бот работает" : "🔴 Бот остановлен";

        var keyboard = new ReplyKeyboardMarkup(new[]
        {
            new KeyboardButton[] { "▶️ Запустить", "⏹️ Остановить" },
            new KeyboardButton[] { "📊 Статус", "🗑️ Очистить рынок" },
            new KeyboardButton[] { "🎟️ Создать промокод", "📋 Список промокодов" },
            new KeyboardButton[] { "📋 Меню" }
        })
        {
            ResizeKeyboard = true
        };

        await _botClient.SendMessage(chatId,
            $"🤖 *Market Fill Bot*\n\n" +
            $"{status}\n\n" +
            "Бот заполняет рынок рандомными скинами от фейковых продавцов.\n" +
            "Скины выставляются по 1 Gold каждую секунду.",
            parseMode: ParseMode.Markdown,
            replyMarkup: keyboard,
            cancellationToken: ct);
    }

    private async Task SendStatusAsync(long chatId, CancellationToken ct)
    {
        var listingsCollection = _database.GetCollection<MarketplaceListing>("marketplace_listings");
        var totalActive = await listingsCollection.CountDocumentsAsync(
            l => l.Status == ListingStatus.Active);
        var botListings = await listingsCollection.CountDocumentsAsync(
            l => l.Status == ListingStatus.Active && l.SellerName.StartsWith("Bot_"));

        var isRunning = _worker?.IsRunning ?? false;
        var listed = _worker?.ListedCount ?? 0;

        await _botClient.SendMessage(chatId,
            $"📊 *Статус*\n\n" +
            $"🤖 Бот: {(isRunning ? "🟢 Работает" : "🔴 Остановлен")}\n" +
            $"📤 Выставлено за сессию: {listed}\n\n" +
            $"🏪 Всего на рынке: {totalActive}\n" +
            $"🤖 От ботов: {botListings}",
            parseMode: ParseMode.Markdown,
            cancellationToken: ct);
    }

    private async Task StartWorkerAsync(long chatId, CancellationToken ct)
    {
        if (_worker?.IsRunning == true)
        {
            await _botClient.SendMessage(chatId, "⚠️ Бот уже запущен!", cancellationToken: ct);
            return;
        }

        _worker = new MarketBotWorker(_database, chatId, _botClient);
        _ = _worker.StartAsync();

        await _botClient.SendMessage(chatId,
            "✅ Бот запущен!\n\n" +
            "Рандомные скины будут выставляться на рынок по 1 Gold каждую секунду.",
            cancellationToken: ct);
    }

    private async Task StopWorkerAsync(long chatId, CancellationToken ct)
    {
        if (_worker?.IsRunning != true)
        {
            await _botClient.SendMessage(chatId, "⚠️ Бот не запущен!", cancellationToken: ct);
            return;
        }

        var listed = _worker.ListedCount;
        _worker.Stop();

        await _botClient.SendMessage(chatId,
            $"⏹️ Бот остановлен!\n📤 Выставлено: {listed} скинов",
            cancellationToken: ct);
    }

    private async Task ClearMarketAsync(long chatId, CancellationToken ct)
    {
        var listingsCollection = _database.GetCollection<MarketplaceListing>("marketplace_listings");
        var result = await listingsCollection.DeleteManyAsync(
            l => l.SellerName.StartsWith("Bot_"), ct);

        await _botClient.SendMessage(chatId,
            $"🗑️ Удалено {result.DeletedCount} листингов от ботов",
            cancellationToken: ct);
    }

    private async Task StartPromoCreationAsync(long chatId, CancellationToken ct)
    {
        _promoCreationStates[chatId] = new PromoCreationState { Step = 1 };
        
        await _botClient.SendMessage(chatId,
            "🎟️ *Создание промокода*\n\n" +
            "Шаг 1/2: Введите максимальное количество активаций промокода:",
            parseMode: ParseMode.Markdown,
            cancellationToken: ct);
    }

    private async Task HandlePromoCreationAsync(long chatId, string text, PromoCreationState state, CancellationToken ct)
    {
        try
        {
            if (state.Step == 1)
            {
                // Парсим количество активаций
                if (!int.TryParse(text, out int maxActivations) || maxActivations <= 0)
                {
                    await _botClient.SendMessage(chatId,
                        "❌ Неверное число! Введите положительное целое число:",
                        cancellationToken: ct);
                    return;
                }

                state.MaxActivations = maxActivations;
                state.Step = 2;

                await _botClient.SendMessage(chatId,
                    $"✅ Количество активаций: {maxActivations}\n\n" +
                    "Шаг 2/2: Введите ID предмета (ItemDefinitionId):",
                    cancellationToken: ct);
            }
            else if (state.Step == 2)
            {
                // Парсим ID предмета
                if (!int.TryParse(text, out int itemId) || itemId <= 0)
                {
                    await _botClient.SendMessage(chatId,
                        "❌ Неверный ID! Введите положительное целое число:",
                        cancellationToken: ct);
                    return;
                }

                state.ItemId = itemId;

                // Создаём промокод
                var promoCode = await CreatePromoCodeAsync(state.MaxActivations, state.ItemId);

                // Удаляем состояние
                _promoCreationStates.Remove(chatId);

                await _botClient.SendMessage(chatId,
                    $"✅ *Промокод создан!*\n\n" +
                    $"🎟️ Код: `{promoCode}`\n" +
                    $"🔢 Активаций: {state.MaxActivations}\n" +
                    $"🎁 Предмет: {state.ItemId}\n\n" +
                    "Игроки могут использовать этот код в игре.",
                    parseMode: ParseMode.Markdown,
                    cancellationToken: ct);
            }
        }
        catch (Exception ex)
        {
            _promoCreationStates.Remove(chatId);
            await _botClient.SendMessage(chatId,
                $"❌ Ошибка создания промокода: {ex.Message}",
                cancellationToken: ct);
        }
    }

    private async Task<string> CreatePromoCodeAsync(int maxActivations, int itemId)
    {
        var couponId = Guid.NewGuid().ToString();
        var couponCode = GeneratePromoCode();

        var couponCollection = _database.GetCollection<Coupon>("coupons");
        var coupon = new Coupon
        {
            CouponId = couponId,
            Code = couponCode,
            CreatorPlayerId = "telegram_bot",
            MaxUses = maxActivations,
            CurrentUses = 0,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            Rewards = new List<RewardDefinition>
            {
                new RewardDefinition
                {
                    Type = "item",
                    ItemDefinitionId = itemId,
                    Amount = 1
                }
            }
        };

        await couponCollection.InsertOneAsync(coupon);
        Console.WriteLine($"🎟️ Created promo code: {couponCode} (activations: {maxActivations}, item: {itemId})");

        return couponCode;
    }

    private async Task ListPromosAsync(long chatId, CancellationToken ct)
    {
        var couponCollection = _database.GetCollection<Coupon>("coupons");
        var coupons = await couponCollection.Find(c => c.IsActive)
            .SortByDescending(c => c.CreatedAt)
            .Limit(10)
            .ToListAsync(ct);

        if (coupons.Count == 0)
        {
            await _botClient.SendMessage(chatId,
                "📋 Нет активных промокодов",
                cancellationToken: ct);
            return;
        }

        var message = "📋 *Активные промокоды (последние 10):*\n\n";
        foreach (var coupon in coupons)
        {
            var itemIds = string.Join(", ", coupon.Rewards.Select(r => r.ItemDefinitionId));
            message += $"🎟️ `{coupon.Code}`\n" +
                      $"   Использовано: {coupon.CurrentUses}/{coupon.MaxUses}\n" +
                      $"   Предметы: {itemIds}\n" +
                      $"   Создан: {coupon.CreatedAt:dd.MM.yyyy HH:mm}\n\n";
        }

        await _botClient.SendMessage(chatId,
            message,
            parseMode: ParseMode.Markdown,
            cancellationToken: ct);
    }

    private string GeneratePromoCode()
    {
        const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
        var random = new Random();
        return new string(Enumerable.Repeat(chars, 12).Select(s => s[random.Next(s.Length)]).ToArray());
    }

    private Task HandleErrorAsync(ITelegramBotClient bot, Exception ex, CancellationToken ct)
    {
        Console.WriteLine($"❌ Telegram error: {ex.Message}");
        return Task.CompletedTask;
    }

    public void Stop()
    {
        _worker?.Stop();
        _cts.Cancel();
    }
}
