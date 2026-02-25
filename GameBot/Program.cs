using System.Net.Sockets;
using Google.Protobuf;
using Axlebolt.RpcSupport.Protobuf;
using Axlebolt.Bolt.Protobuf;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Polling;
using MongoDB.Driver;
using MongoDB.Bson;

namespace GameBot;

class Program
{
    static async Task Main(string[] args)
    {
        Console.WriteLine("🤖 Game Bot Manager with Telegram Control");
        Console.WriteLine("==========================================");
        
        // Команда создания ботов
        if (args.Length >= 2 && args[0] == "create-bots")
        {
            int count = int.Parse(args[1]);
            string mongoUrl = args.Length >= 3 ? args[2] : "mongodb://mongo:vvsHryKyEkNaiIgYtMBtkIqdQIOSZPPE@maglev.proxy.rlwy.net:26476";
            string dbName = args.Length >= 4 ? args[3] : "Ryzen";
            await BotCreator.CreateBotsAsync(mongoUrl, dbName, count);
            return;
        }
        
        // Настройки
        var config = new BotManagerConfig
        {
            TelegramToken = "7976917656:AAEX5xogpHrBZJ3qpAOsic4P_6b_-1OnKSo", // Замени на свой токен
            ServerHost = "centerbeam.proxy.rlwy.net",
            ServerPort = 34146,
            MongoUrl = "mongodb://mongo:vvsHryKyEkNaiIgYtMBtkIqdQIOSZPPE@maglev.proxy.rlwy.net:26476",
            DbName = "Ryzen",
            AdminChatIds = new List<long> { 8079964001 } // Замени на свой Telegram ID
        };
        
        // Парсим аргументы
        if (args.Length >= 1) config.TelegramToken = args[0];
        if (args.Length >= 2) config.ServerHost = args[1];
        if (args.Length >= 3)
        {
            if (int.TryParse(args[2], out int port))
                config.ServerPort = port;
        }
        
        var manager = new BotManager(config);
        await manager.StartAsync();
    }
}

public class BotManagerConfig
{
    public string TelegramToken { get; set; } = "";
    public string ServerHost { get; set; } = "centerbeam.proxy.rlwy.net";
    public int ServerPort { get; set; } = 34146;
    public string MongoUrl { get; set; } = "mongodb://mongo:vvsHryKyEkNaiIgYtMBtkIqdQIOSZPPE@maglev.proxy.rlwy.net:26476";
    public string DbName { get; set; } = "Ryzen";
    public List<long> AdminChatIds { get; set; } = new();
}

public class BotManager : IUpdateHandler
{
    private readonly BotManagerConfig _config;
    private readonly Dictionary<string, GameBotClient> _bots = new();
    private ITelegramBotClient? _telegram;
    private IMongoCollection<BsonDocument>? _playersCollection;
    
    public BotManager(BotManagerConfig config)
    {
        _config = config;
    }
    
    public async Task StartAsync()
    {
        // MongoDB
        var mongoClient = new MongoClient(_config.MongoUrl);
        var database = mongoClient.GetDatabase(_config.DbName);
        _playersCollection = database.GetCollection<BsonDocument>("Players2");
        
        // Telegram Bot
        _telegram = new TelegramBotClient(_config.TelegramToken);
        
        Console.WriteLine("🤖 Starting Telegram bot...");
        
        var receiverOptions = new ReceiverOptions
        {
            AllowedUpdates = Array.Empty<UpdateType>()
        };
        
        _telegram.StartReceiving(
            updateHandler: this,
            receiverOptions: receiverOptions
        );
        
        var me = await _telegram.GetMe();
        Console.WriteLine($"🤖 Telegram bot started: @{me.Username}");
        Console.WriteLine("\n📱 Commands:");
        Console.WriteLine("  /start - Show help");
        Console.WriteLine("  /status - Bot status");
        Console.WriteLine("  /create <count> - Create bot accounts");
        Console.WriteLine("  /connect <token> - Connect bot to server");
        Console.WriteLine("  /connectall - Connect all bots");
        Console.WriteLine("  /disconnect <token> - Disconnect bot");
        Console.WriteLine("  /disconnectall - Disconnect all bots");
        Console.WriteLine("  /list - List all bot accounts");
        Console.WriteLine("  /join <token> <lobbyId> - Join lobby");
        Console.WriteLine("  /leave <token> - Leave lobby");
        Console.WriteLine("  /autojoin <token> <on|off> - Enable/disable auto-join");
        
        await Task.Delay(-1);
    }
    
    public async Task HandleUpdateAsync(ITelegramBotClient bot, Update update, CancellationToken ct)
    {
        if (update.Message is not { Text: { } text } message) return;
        
        var chatId = message.Chat.Id;
        
        // Проверка админа (опционально)
        // if (!_config.AdminChatIds.Contains(chatId)) return;
        
        var parts = text.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var command = parts[0].ToLower();
        
        try
        {
            switch (command)
            {
                case "/start":
                case "/help":
                    await SendHelpAsync(chatId, ct);
                    break;
                    
                case "/status":
                    await SendStatusAsync(chatId, ct);
                    break;
                    
                case "/create":
                    int count = parts.Length > 1 ? int.Parse(parts[1]) : 5;
                    await CreateBotsAsync(chatId, count, ct);
                    break;
                    
                case "/connect":
                    if (parts.Length > 1)
                        await ConnectBotAsync(chatId, parts[1], ct);
                    else
                        await bot.SendMessage(chatId, "❌ Usage: /connect <token>", cancellationToken: ct);
                    break;
                    
                case "/connectall":
                    await ConnectAllBotsAsync(chatId, ct);
                    break;
                    
                case "/disconnect":
                    if (parts.Length > 1)
                        await DisconnectBotAsync(chatId, parts[1], ct);
                    else
                        await bot.SendMessage(chatId, "❌ Usage: /disconnect <token>", cancellationToken: ct);
                    break;
                    
                case "/disconnectall":
                    await DisconnectAllBotsAsync(chatId, ct);
                    break;
                    
                case "/list":
                    await ListBotsAsync(chatId, ct);
                    break;
                    
                case "/join":
                    if (parts.Length > 2)
                        await JoinLobbyAsync(chatId, parts[1], parts[2], ct);
                    else
                        await bot.SendMessage(chatId, "❌ Usage: /join <token> <lobbyId>", cancellationToken: ct);
                    break;
                    
                case "/leave":
                    if (parts.Length > 1)
                        await LeaveLobbyAsync(chatId, parts[1], ct);
                    else
                        await bot.SendMessage(chatId, "❌ Usage: /leave <token>", cancellationToken: ct);
                    break;
                    
                case "/autojoin":
                    if (parts.Length > 2)
                        await SetAutoJoinAsync(chatId, parts[1], parts[2].ToLower() == "on", ct);
                    else
                        await bot.SendMessage(chatId, "❌ Usage: /autojoin <token> <on|off>", cancellationToken: ct);
                    break;
            }
        }
        catch (Exception ex)
        {
            await bot.SendMessage(chatId, $"❌ Error: {ex.Message}", cancellationToken: ct);
        }
    }
    
    private async Task SendHelpAsync(long chatId, CancellationToken ct)
    {
        var help = @"🤖 *Game Bot Manager*

*Commands:*
/status - Show connected bots
/create <count> - Create bot accounts
/list - List all bot accounts

*Connection:*
/connect <token> - Connect bot
/connectall - Connect all bots
/disconnect <token> - Disconnect bot
/disconnectall - Disconnect all

*Matchmaking:*
/join <token> <lobbyId> - Join lobby
/leave <token> - Leave lobby
/autojoin <token> <on|off> - Auto-join lobbies";
        
        await _telegram!.SendMessage(chatId, help, parseMode: ParseMode.Markdown, cancellationToken: ct);
    }
    
    private async Task SendStatusAsync(long chatId, CancellationToken ct)
    {
        if (_bots.Count == 0)
        {
            await _telegram!.SendMessage(chatId, "📊 No bots connected", cancellationToken: ct);
            return;
        }
        
        var status = "📊 *Bot Status:*\n\n";
        foreach (var (token, bot) in _bots)
        {
            var icon = bot.IsConnected ? "🟢" : "🔴";
            var lobbyInfo = !string.IsNullOrEmpty(bot.CurrentLobbyId) ? $" (in lobby)" : "";
            status += $"{icon} `{token}`{lobbyInfo}\n";
        }
        
        await _telegram!.SendMessage(chatId, status, parseMode: ParseMode.Markdown, cancellationToken: ct);
    }
    
    private async Task CreateBotsAsync(long chatId, int count, CancellationToken ct)
    {
        await _telegram!.SendMessage(chatId, $"🔄 Creating {count} bots...", cancellationToken: ct);
        
        var tokens = new List<string>();
        
        for (int i = 1; i <= count; i++)
        {
            var token = $"bot_{Guid.NewGuid():N}"[..20];
            var botName = $"Bot_{DateTime.Now:HHmmss}_{i}";
            var playerUid = $"BOT{DateTime.Now:yyyyMMddHHmmss}{i:D3}";
            
            var botDoc = new BsonDocument
            {
                { "Name", botName },
                { "PlayerUid", playerUid },
                { "Token", token },
                { "Level", 50 },
                { "Coins", 10000 },
                { "Gems", 1000 },
                { "Keys", 100 },
                { "Experience", 50000 },
                { "TimeInGame", 0 },
                { "LastHwid", $"BOT_HWID_{playerUid}" },
                { "RegistrationDate", DateTime.UtcNow },
                { "LastLoginDate", DateTime.UtcNow },
                { "IsBot", true },
                { "Inventory", new BsonDocument { { "Items", new BsonArray() } } }
            };
            
            await _playersCollection!.InsertOneAsync(botDoc, cancellationToken: ct);
            tokens.Add(token);
        }
        
        var msg = $"✅ Created {count} bots:\n\n";
        foreach (var t in tokens)
            msg += $"`{t}`\n";
        
        await _telegram!.SendMessage(chatId, msg, parseMode: ParseMode.Markdown, cancellationToken: ct);
    }
    
    private async Task ConnectBotAsync(long chatId, string token, CancellationToken ct)
    {
        if (_bots.ContainsKey(token))
        {
            await _telegram!.SendMessage(chatId, $"⚠️ Bot `{token}` already connected", parseMode: ParseMode.Markdown, cancellationToken: ct);
            return;
        }
        
        await _telegram!.SendMessage(chatId, $"🔄 Connecting `{token}`...", parseMode: ParseMode.Markdown, cancellationToken: ct);
        
        var bot = new GameBotClient(_config.ServerHost, _config.ServerPort, token);
        _bots[token] = bot;
        
        _ = Task.Run(async () =>
        {
            await bot.StartAsync();
            _bots.Remove(token);
        });
        
        await Task.Delay(2000, ct);
        
        if (bot.IsConnected)
            await _telegram!.SendMessage(chatId, $"✅ Bot `{token}` connected!", parseMode: ParseMode.Markdown, cancellationToken: ct);
        else
            await _telegram!.SendMessage(chatId, $"❌ Bot `{token}` failed to connect", parseMode: ParseMode.Markdown, cancellationToken: ct);
    }
    
    private async Task ConnectAllBotsAsync(long chatId, CancellationToken ct)
    {
        var bots = await _playersCollection!
            .Find(Builders<BsonDocument>.Filter.Eq("IsBot", true))
            .ToListAsync(ct);
        
        if (bots.Count == 0)
        {
            await _telegram!.SendMessage(chatId, "❌ No bot accounts found. Use /create first.", cancellationToken: ct);
            return;
        }
        
        await _telegram!.SendMessage(chatId, $"🔄 Connecting {bots.Count} bots...", cancellationToken: ct);
        
        int connected = 0;
        foreach (var botDoc in bots)
        {
            var token = botDoc["Token"].AsString;
            if (_bots.ContainsKey(token)) continue;
            
            var bot = new GameBotClient(_config.ServerHost, _config.ServerPort, token);
            _bots[token] = bot;
            
            _ = Task.Run(async () =>
            {
                await bot.StartAsync();
                _bots.Remove(token);
            });
            
            connected++;
            await Task.Delay(300, ct);
        }
        
        await Task.Delay(2000, ct);
        
        var online = _bots.Values.Count(b => b.IsConnected);
        await _telegram!.SendMessage(chatId, $"✅ {online}/{connected} bots connected", cancellationToken: ct);
    }
    
    private async Task DisconnectBotAsync(long chatId, string token, CancellationToken ct)
    {
        if (!_bots.TryGetValue(token, out var bot))
        {
            await _telegram!.SendMessage(chatId, $"❌ Bot `{token}` not found", parseMode: ParseMode.Markdown, cancellationToken: ct);
            return;
        }
        
        bot.Stop();
        _bots.Remove(token);
        await _telegram!.SendMessage(chatId, $"✅ Bot `{token}` disconnected", parseMode: ParseMode.Markdown, cancellationToken: ct);
    }
    
    private async Task DisconnectAllBotsAsync(long chatId, CancellationToken ct)
    {
        var count = _bots.Count;
        foreach (var bot in _bots.Values)
            bot.Stop();
        _bots.Clear();
        
        await _telegram!.SendMessage(chatId, $"✅ Disconnected {count} bots", cancellationToken: ct);
    }
    
    private async Task ListBotsAsync(long chatId, CancellationToken ct)
    {
        var bots = await _playersCollection!
            .Find(Builders<BsonDocument>.Filter.Eq("IsBot", true))
            .ToListAsync(ct);
        
        if (bots.Count == 0)
        {
            await _telegram!.SendMessage(chatId, "📋 No bot accounts. Use /create to make some.", cancellationToken: ct);
            return;
        }
        
        var msg = $"📋 *Bot Accounts ({bots.Count}):*\n\n";
        foreach (var bot in bots.Take(20))
        {
            var token = bot["Token"].AsString;
            var name = bot["Name"].AsString;
            var isOnline = _bots.ContainsKey(token) && _bots[token].IsConnected;
            var icon = isOnline ? "🟢" : "⚪";
            msg += $"{icon} {name}\n`{token}`\n\n";
        }
        
        if (bots.Count > 20)
            msg += $"... and {bots.Count - 20} more";
        
        await _telegram!.SendMessage(chatId, msg, parseMode: ParseMode.Markdown, cancellationToken: ct);
    }
    
    private async Task JoinLobbyAsync(long chatId, string token, string lobbyId, CancellationToken ct)
    {
        if (!_bots.TryGetValue(token, out var bot) || !bot.IsConnected)
        {
            await _telegram!.SendMessage(chatId, $"❌ Bot `{token}` not connected", parseMode: ParseMode.Markdown, cancellationToken: ct);
            return;
        }
        
        await bot.JoinLobbyAsync(lobbyId);
        await _telegram!.SendMessage(chatId, $"✅ Bot `{token}` joining lobby `{lobbyId}`", parseMode: ParseMode.Markdown, cancellationToken: ct);
    }
    
    private async Task LeaveLobbyAsync(long chatId, string token, CancellationToken ct)
    {
        if (!_bots.TryGetValue(token, out var bot) || !bot.IsConnected)
        {
            await _telegram!.SendMessage(chatId, $"❌ Bot `{token}` not connected", parseMode: ParseMode.Markdown, cancellationToken: ct);
            return;
        }
        
        await bot.LeaveLobbyAsync();
        await _telegram!.SendMessage(chatId, $"✅ Bot `{token}` left lobby", parseMode: ParseMode.Markdown, cancellationToken: ct);
    }
    
    private async Task SetAutoJoinAsync(long chatId, string token, bool enabled, CancellationToken ct)
    {
        if (!_bots.TryGetValue(token, out var bot))
        {
            await _telegram!.SendMessage(chatId, $"❌ Bot `{token}` not found", parseMode: ParseMode.Markdown, cancellationToken: ct);
            return;
        }
        
        bot.AutoJoinLobbies = enabled;
        var status = enabled ? "enabled" : "disabled";
        await _telegram!.SendMessage(chatId, $"✅ Auto-join {status} for `{token}`", parseMode: ParseMode.Markdown, cancellationToken: ct);
    }
    
    public Task HandleErrorAsync(ITelegramBotClient bot, Exception ex, HandleErrorSource source, CancellationToken ct)
    {
        Console.WriteLine($"❌ Telegram error: {ex.Message}");
        return Task.CompletedTask;
    }
}
