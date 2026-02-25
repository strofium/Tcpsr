using System.Net.Sockets;
using Axlebolt.RpcSupport.Protobuf;
using Axlebolt.Bolt.Protobuf2;
using StandRiseServer.Core;
using StandRiseServer.Models;
using MongoDB.Driver;
using Google.Protobuf;

namespace StandRiseServer.Services;

/// <summary>
/// Сервис авторизации через Telegram токен.
/// Логика: TelegramUserId = уникальный идентификатор аккаунта.
/// HWID привязывается к аккаунту, IP обновляется динамически.
/// </summary>
public class TokenAuthService
{
    private readonly ProtobufHandler _handler;
    private readonly DatabaseService _database;
    private readonly SessionManager _sessionManager;
    private static readonly object _keyGenLock = new();

    public TokenAuthService(ProtobufHandler handler, DatabaseService database, SessionManager sessionManager)
    {
        _handler = handler;
        _database = database;
        _sessionManager = sessionManager;

        _handler.RegisterHandler("TokenAuthRemoteService", "authWithToken", AuthWithTokenAsync);
    }

    private async Task AuthWithTokenAsync(TcpClient client, RpcRequest request)
    {
        string tokenStr = "";
        string ipAddress = "unknown";
        string gameId = "";

        try
        {
            Console.WriteLine("=== Token Auth Request ===");

            if (request.Params.Count == 0)
            {
                Console.WriteLine("❌ No token provided");
                await SendErrorAsync(client, request.Id, 400, "Token required");
                return;
            }

            if (request.Params.Count > 0 && request.Params[0].One != null)
                tokenStr = Axlebolt.RpcSupport.Protobuf.String.Parser.ParseFrom(request.Params[0].One).Value;

            // Получаем HWID если передан
            if (request.Params.Count > 1 && request.Params[1].One != null)
            {
                try { gameId = Axlebolt.RpcSupport.Protobuf.String.Parser.ParseFrom(request.Params[1].One).Value; }
                catch { }
            }

            try
            {
                if (client.Client.RemoteEndPoint is System.Net.IPEndPoint endpoint)
                    ipAddress = endpoint.Address.ToString();
            }
            catch { }

            Console.WriteLine($"🔍 TokenAuth: Token={(!string.IsNullOrEmpty(tokenStr) ? tokenStr[..Math.Min(8, tokenStr.Length)] : "empty")}..., HWID={gameId}, IP={ipAddress}");

            var playersCollection = _database.Database.GetCollection<Player>("Players2");
            var hwidsCollection = _database.Database.GetCollection<HwidEntry>("Hwids2");
            var tokensCollection = _database.Database.GetCollection<AuthToken>("AuthTokens");

            // ═══════════════════════════════════════════════════════════════
            // ЭТАП 1: Валидация Telegram токена
            // ═══════════════════════════════════════════════════════════════
            var tokenRecord = await tokensCollection
                .Find(t => t.Token == tokenStr)
                .FirstOrDefaultAsync();

            if (tokenRecord == null)
            {
                Console.WriteLine($"❌ Token not found: {tokenStr}");
                await RecordAuthLog(gameId, tokenStr, ipAddress, "", "Error", "Invalid Telegram token");
                await SendErrorAsync(client, request.Id, 404, "Invalid token");
                return;
            }

            if (tokenRecord.ExpiresAt < DateTime.UtcNow)
            {
                Console.WriteLine("❌ Token expired");
                await RecordAuthLog(gameId, tokenStr, ipAddress, "", "Error", "Token expired");
                await SendErrorAsync(client, request.Id, 401, "Token expired");
                return;
            }

            if (tokenRecord.IsUsed)
            {
                Console.WriteLine("❌ Token already used");
                await RecordAuthLog(gameId, tokenStr, ipAddress, "", "Error", "Token already used");
                await SendErrorAsync(client, request.Id, 401, "Token already used");
                return;
            }


            var player = await playersCollection
                .Find(p => p.TelegramUserId == tokenRecord.TelegramUserId)
                .FirstOrDefaultAsync();

            HwidEntry? hwidEntry = null;

            if (player != null)
            {
                Console.WriteLine($"✅ Found existing Telegram account: {player.Name}");

                // Ищем привязку HWID
                hwidEntry = await hwidsCollection
                    .Find(h => h.PlayerId == player.Id.ToString())
                    .FirstOrDefaultAsync();

                if (hwidEntry != null)
                {
                    await UpdateHwidEntry(hwidsCollection, hwidEntry, gameId, ipAddress, player.Token);
                }
                else if (!string.IsNullOrEmpty(gameId))
                {
                    hwidEntry = await CreateHwidEntry(hwidsCollection, gameId, ipAddress, player);
                }

                // Обновляем данные игрока
                player.LastLogin = DateTime.UtcNow;
                player.LastIpAddress = ipAddress;
                player.Token = Guid.NewGuid().ToString();
                player.AuthToken = tokenStr;
                
                if (!string.IsNullOrEmpty(gameId) && string.IsNullOrEmpty(player.DeviceId))
                    player.DeviceId = gameId;

                await playersCollection.ReplaceOneAsync(p => p.Id == player.Id, player);

                await RecordAuthLog(gameId, tokenStr, ipAddress, hwidEntry?.CustomKey ?? "", "Success",
                    $"TokenAuth login: {player.Name}");
            }
            else
            {
                player = await CreateNewPlayer(playersCollection, tokenRecord, gameId, ipAddress);

                if (!string.IsNullOrEmpty(gameId))
                {
                    hwidEntry = await CreateHwidEntry(hwidsCollection, gameId, ipAddress, player);
                }

                Console.WriteLine($"✅ Created new Telegram account: {player.Name}");
                await RecordAuthLog(gameId, tokenStr, ipAddress, hwidEntry?.CustomKey ?? "", "NewAccount",
                    $"Created via Telegram: {player.Name}");
            }

            await tokensCollection.UpdateOneAsync(
                at => at.Id == tokenRecord.Id,
                Builders<AuthToken>.Update
                    .Set(at => at.IsUsed, true)
                    .Set(at => at.UsedAt, DateTime.UtcNow)
                    .Set(at => at.PlayerId, player.Id.ToString())
                    .Set(at => at.Hwid, gameId));

            await UpdatePlayerLogin(player, gameId, ipAddress, hwidEntry?.CustomKey ?? "");

            _sessionManager.AddSession(new PlayerSession
            {
                PlayerObjectId = player.Id.ToString(),
                Token = player.Token,
                Client = client
            });

            var resultToken = new Axlebolt.RpcSupport.Protobuf.String { Value = player.Token };
            var result = new BinaryValue
            {
                IsNull = false,
                One = resultToken.ToByteString()
            };

            await _handler.WriteProtoResponseAsync(client, request.Id, result, null);
            Console.WriteLine($"✅ TokenAuth completed: {player.Name}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ TokenAuth error: {ex.Message}\n{ex.StackTrace}");
            await RecordAuthLog(gameId, tokenStr, ipAddress, "", "Error", ex.Message);
            await SendErrorAsync(client, request.Id, 500, "Internal server error");
        }
    }

    /// <summary>
    /// Обновляет запись HWID
    /// </summary>
    private async Task UpdateHwidEntry(
        IMongoCollection<HwidEntry> collection,
        HwidEntry entry,
        string newHwid,
        string newIp,
        string playerToken)
    {
        var updates = new List<UpdateDefinition<HwidEntry>>
        {
            Builders<HwidEntry>.Update.Set(h => h.LastSeen, DateTime.UtcNow),
            Builders<HwidEntry>.Update.Set(h => h.PlayerToken, playerToken)
        };

        if (!string.IsNullOrEmpty(newHwid) && entry.Hwid != newHwid)
        {
            updates.Add(Builders<HwidEntry>.Update.Set(h => h.Hwid, newHwid));
            Console.WriteLine($"🔄 HWID updated: {entry.Hwid} -> {newHwid}");
        }

        if (entry.LastIp != newIp)
        {
            updates.Add(Builders<HwidEntry>.Update.Set(h => h.LastIp, newIp));
            updates.Add(Builders<HwidEntry>.Update.Push(h => h.IpHistory, new IpHistoryEntry
            {
                Ip = newIp,
                Timestamp = DateTime.UtcNow
            }));
            Console.WriteLine($"🔄 IP updated: {entry.LastIp} -> {newIp}");
        }

        var combinedUpdate = Builders<HwidEntry>.Update.Combine(updates);
        await collection.UpdateOneAsync(h => h.Id == entry.Id, combinedUpdate);
    }

    /// <summary>
    /// Создаёт новую привязку HWID к игроку
    /// </summary>
    private async Task<HwidEntry> CreateHwidEntry(
        IMongoCollection<HwidEntry> collection,
        string hwid,
        string ip,
        Player player)
    {
        var entry = new HwidEntry
        {
            Hwid = hwid,
            CustomKey = GenerateRandomKey(16),
            PlayerToken = player.Token,
            PlayerId = player.Id.ToString(),
            LastIp = ip,
            IpHistory = new List<IpHistoryEntry>
            {
                new() { Ip = ip, Timestamp = DateTime.UtcNow }
            },
            FirstSeen = DateTime.UtcNow,
            LastSeen = DateTime.UtcNow
        };

        await collection.InsertOneAsync(entry);
        Console.WriteLine($"📝 Created HWID binding: {hwid} -> {player.Name}");

        return entry;
    }

    /// <summary>
    /// Сохраняет или обновляет запись о входе для последующего восстановления
    /// </summary>
    private async Task UpdatePlayerLogin(Player player, string hwid, string ip, string customKey)
    {
        try 
        {
            var collection = _database.Database.GetCollection<PlayerLogin>("PlayerLogins");
            var update = Builders<PlayerLogin>.Update
                .Set(l => l.TokenId, player.Token)
                .Set(l => l.PlayerOrig, player.PlayerUid)
                .Set(l => l.CustomKey, customKey)
                .Set(l => l.LastLogin, DateTime.UtcNow);
                
            await collection.UpdateOneAsync(
                l => l.Hwid == hwid && l.Ip == ip,
                update,
                new UpdateOptions { IsUpsert = true }
            );
            Console.WriteLine($"💾 PlayerLogin updated for {player.Name} (IP: {ip})");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"⚠️ Failed to update PlayerLogin: {ex.Message}");
        }
    }

    /// <summary>
    /// Создаёт нового игрока через Telegram
    /// </summary>
    private async Task<Player> CreateNewPlayer(
        IMongoCollection<Player> collection,
        AuthToken authToken,
        string deviceId,
        string ipAddress)
    {
        var lastPlayer = await collection
            .Find(_ => true)
            .SortByDescending(p => p.OriginalUid)
            .FirstOrDefaultAsync();

        int newUid = (lastPlayer?.OriginalUid ?? 10000) + 1;

        var player = new Player
        {
            PlayerUid = newUid.ToString(),
            OriginalUid = newUid,
            Name = !string.IsNullOrEmpty(authToken.TelegramUsername)
                ? authToken.TelegramUsername
                : $"Player_{newUid}",
            TelegramUserId = authToken.TelegramUserId,
            TelegramUsername = authToken.TelegramUsername,
            Token = Guid.NewGuid().ToString(),
            AuthToken = authToken.Token,
            DeviceId = deviceId,
            LastIpAddress = ipAddress,
            LastLogin = DateTime.UtcNow,
            Level = 1,
            Experience = 0,
            Coins = 10000,
            Gems = 1000,
            RegistrationDate = DateTime.UtcNow,
            Inventory = new PlayerInventoryData { Items = new List<PlayerInventoryItem>() },
            Stats = new PlayerStats
            {
                ArrayCust = new List<StatItem>
                {
                    new() { Name = "kills", IntValue = 0, Type = "INT", Window = 0 },
                    new() { Name = "deaths", IntValue = 0, Type = "INT", Window = 0 }
                }
            }
        };

        await collection.InsertOneAsync(player);
        return player;
    }

    private async Task RecordAuthLog(string hwid, string token, string ip, string key, string status, string details)
    {
        try
        {
            var logs = _database.Database.GetCollection<AuthLog>("AuthLogs");
            await logs.InsertOneAsync(new AuthLog
            {
                Hwid = hwid,
                Token = token,
                Ip = ip,
                CustomKey = key,
                Status = status,
                Details = details,
                Timestamp = DateTime.UtcNow
            });
        }
        catch (Exception ex)
        {
            Console.WriteLine($"⚠️ Failed to record auth log: {ex.Message}");
        }
    }

    private string GenerateRandomKey(int length)
    {
        lock (_keyGenLock)
        {
            const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789";
            var random = new Random();
            return new string(Enumerable.Repeat(chars, length)
                .Select(s => s[random.Next(s.Length)]).ToArray());
        }
    }

    private async Task SendErrorAsync(TcpClient client, string guid, int code, string message)
    {
        await _handler.WriteProtoResponseAsync(client, guid, null,
            new RpcException { Id = guid, Code = code, Property = null });
    }
}
