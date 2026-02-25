using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Polling;
using MongoDB.Driver;
using MongoDB.Bson;
using StandRiseServer.Models;

namespace RyzenBot;

class Program
{
    private static ITelegramBotClient botClient;
    private static IMongoCollection<KeyEntry> keysCollection;
    private static IMongoCollection<Player> playersCollection;

    static async Task Main(string[] args)
    {
        // Settings
        string botToken = "8477828914:AAHH12QQeJITI22yO0TvlDS06rHGpu9Zg6k";
        string mongoUrl = "mongodb://127.0.0.1:27017";
        string dbName = "Ryzen";

        // DB Setup
        var client = new MongoClient(mongoUrl);
        var database = client.GetDatabase(dbName);
        keysCollection = database.GetCollection<KeyEntry>("RegistrationKeys");
        playersCollection = database.GetCollection<Player>("Players2");

        botClient = new TelegramBotClient(botToken);

        using var cts = new CancellationTokenSource();

        Console.WriteLine("🤖 Ryzen Bot starting...");

        var receiverOptions = new ReceiverOptions
        {
            AllowedUpdates = Array.Empty<UpdateType>()
        };

        var handler = new BotUpdateHandler(keysCollection, playersCollection);

        botClient.StartReceiving(
            updateHandler: handler,
            receiverOptions: receiverOptions,
            cancellationToken: cts.Token
        );

        Console.WriteLine("🤖 Ryzen Bot is running!");

        // Keep running
        await Task.Delay(-1);
    }
}

class BotUpdateHandler : IUpdateHandler
{
    private readonly IMongoCollection<KeyEntry> _keys;
    private readonly IMongoCollection<Player> _players;

    public BotUpdateHandler(IMongoCollection<KeyEntry> keys, IMongoCollection<Player> players)
    {
        _keys = keys;
        _players = players;
    }

    public async Task HandleUpdateAsync(ITelegramBotClient botClient, Update update, CancellationToken cancellationToken)
    {
        if (update.Message is not { Text: { } messageText } message)
            return;

        var chatId = message.Chat.Id;
        var userId = message.From?.Id ?? 0;
        var username = message.From?.Username ?? "Unknown";

        Console.WriteLine($"📩 Received '{messageText}' from {username} ({userId})");

        if (messageText == "/start")
        {
            await botClient.SendMessage(chatId, "👋 Welcome to Ryzen Promo Bot!\n\nCommands:\n/getkey - Get your unique registration key\n/profile - View your linked game profile", cancellationToken: cancellationToken);
        }
        else if (messageText == "/getkey")
        {
            await HandleGetKey(botClient, chatId, userId, username, cancellationToken);
        }
        else if (messageText == "/profile")
        {
            await HandleProfile(botClient, chatId, userId, cancellationToken);
        }
    }

    private async Task HandleGetKey(ITelegramBotClient botClient, long chatId, long userId, string username, CancellationToken ct)
    {
        var existingKey = await _keys.Find(k => k.TelegramUserId == userId).FirstOrDefaultAsync();

        if (existingKey != null)
        {
            await botClient.SendMessage(chatId, $"❌ You already received a key:\n\n`{existingKey.Key}`", parseMode: ParseMode.Markdown, cancellationToken: ct);
            return;
        }

        string newKey = GenerateRandomKey();
        var keyEntry = new KeyEntry
        {
            Key = newKey,
            TelegramUserId = userId,
            TelegramUsername = username,
            IsUsed = false,
            CreatedAt = DateTime.UtcNow
        };

        await _keys.InsertOneAsync(keyEntry);

        await botClient.SendMessage(chatId, $"✅ Your unique registration key generated!\n\nKey: `{newKey}`\n\nUse it in the game to register.", parseMode: ParseMode.Markdown, cancellationToken: ct);
    }

    private async Task HandleProfile(ITelegramBotClient botClient, long chatId, long userId, CancellationToken ct)
    {
        var keyEntry = await _keys.Find(k => k.TelegramUserId == userId).FirstOrDefaultAsync();

        if (keyEntry == null)
        {
            await botClient.SendMessage(chatId, "❌ You don't have a key yet. Use /getkey first.", cancellationToken: ct);
            return;
        }

        if (!keyEntry.IsUsed || string.IsNullOrEmpty(keyEntry.PlayerId))
        {
            await botClient.SendMessage(chatId, $"ℹ️ Your key: `{keyEntry.Key}`\n\nStatus: *Not used yet*", parseMode: ParseMode.Markdown, cancellationToken: ct);
            return;
        }

        try
        {
            var player = await _players.Find(p => p.Id == ObjectId.Parse(keyEntry.PlayerId)).FirstOrDefaultAsync();
            if (player == null)
            {
                await botClient.SendMessage(chatId, "❌ Linked player profile not found in database.", cancellationToken: ct);
                return;
            }

            string profileInfo = $"👤 *Game Profile*\n\n" +
                                 $"Name: {player.Name}\n" +
                                 $"UID: {player.PlayerUid}\n" +
                                 $"Level: {player.Level}\n" +
                                 $"Coins: {player.Coins}\n" +
                                 $"Gems: {player.Gems}\n" +
                                 $"Registered: {player.RegistrationDate:dd.MM.yyyy}";

            await botClient.SendMessage(chatId, profileInfo, parseMode: ParseMode.Markdown, cancellationToken: ct);
        }
        catch (Exception ex)
        {
            await botClient.SendMessage(chatId, $"❌ Error loading profile: {ex.Message}", cancellationToken: ct);
        }
    }

    private string GenerateRandomKey()
    {
        const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
        var random = new Random();
        return new string(Enumerable.Repeat(chars, 12)
            .Select(s => s[random.Next(s.Length)]).ToArray());
    }

    // Fixed member name and parameters for Telegram.Bot v22+
    public Task HandleErrorAsync(ITelegramBotClient botClient, Exception exception, HandleErrorSource source, CancellationToken cancellationToken)
    {
        Console.WriteLine($"📛 Bot Error ({source}): {exception.Message}");
        return Task.CompletedTask;
    }
}
