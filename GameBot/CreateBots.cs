// Скрипт для создания бот-аккаунтов в MongoDB
// Запустить: dotnet run -- create-bots 5

using MongoDB.Driver;
using MongoDB.Bson;

namespace GameBot;

public static class BotCreator
{
    public static async Task CreateBotsAsync(string mongoUrl, string dbName, int count)
    {
        Console.WriteLine($"🤖 Creating {count} bot accounts...");
        
        var client = new MongoClient(mongoUrl);
        var database = client.GetDatabase(dbName);
        var playersCollection = database.GetCollection<BsonDocument>("Players2");
        
        var createdTokens = new List<string>();
        
        for (int i = 1; i <= count; i++)
        {
            var token = $"bot_{Guid.NewGuid():N}".Substring(0, 20);
            var botName = $"Bot_{i:D3}";
            var playerUid = $"BOT{i:D6}";
            
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
                { "LastHwid", $"BOT_HWID_{i}" },
                { "RegistrationDate", DateTime.UtcNow },
                { "LastLoginDate", DateTime.UtcNow },
                { "IsBot", true },
                { "Inventory", new BsonDocument
                    {
                        { "Items", new BsonArray() }
                    }
                }
            };
            
            // Проверяем существует ли уже
            var existing = await playersCollection.Find(
                Builders<BsonDocument>.Filter.Eq("Token", token)
            ).FirstOrDefaultAsync();
            
            if (existing == null)
            {
                await playersCollection.InsertOneAsync(botDoc);
                Console.WriteLine($"  ✅ Created: {botName} (token: {token})");
                createdTokens.Add(token);
            }
            else
            {
                Console.WriteLine($"  ⚠️ Already exists: {botName}");
                createdTokens.Add(token);
            }
        }
        
        Console.WriteLine($"\n🎮 Bot tokens for GameBot:");
        foreach (var token in createdTokens)
        {
            Console.WriteLine($"  {token}");
        }
        
        Console.WriteLine($"\n📋 Run bots with:");
        Console.WriteLine($"  dotnet run -- 127.0.0.1 7777 {string.Join(" ", createdTokens)}");
    }
}
