using System.Net.Sockets;
using Axlebolt.RpcSupport.Protobuf;
using StandRiseServer.Core;
using StandRiseServer.Models;
using MongoDB.Driver;
using MongoDB.Bson;
using Google.Protobuf;

namespace StandRiseServer.Services;

public class KeyAuthService
{
    private readonly ProtobufHandler _handler;
    private readonly DatabaseService _database;
    private readonly SessionManager _sessionManager;

    private static readonly object _keyGenLock = new();

    public KeyAuthService(ProtobufHandler handler, DatabaseService database, SessionManager sessionManager)
    {
        _handler = handler;
        _database = database;
        _sessionManager = sessionManager;

        Console.WriteLine("🔑 Registering KeyAuthRemoteService handlers...");
        _handler.RegisterHandler("KeyAuthRemoteService", "auth", AuthWithKeyAsync);
    }

    private async Task AuthWithKeyAsync(TcpClient client, RpcRequest request)
    {
        string key = "";
        string gameId = "";
        string version = "";
        string ipAddress = "unknown";

        try
        {
            if (request.Params.Count > 0 && request.Params[0].One != null) 
                key = Axlebolt.RpcSupport.Protobuf.String.Parser.ParseFrom(request.Params[0].One).Value;
            if (request.Params.Count > 1 && request.Params[1].One != null) 
                gameId = Axlebolt.RpcSupport.Protobuf.String.Parser.ParseFrom(request.Params[1].One).Value;
            if (request.Params.Count > 2 && request.Params[2].One != null) 
                version = Axlebolt.RpcSupport.Protobuf.String.Parser.ParseFrom(request.Params[2].One).Value;

            try
            {
                if (client.Client.RemoteEndPoint is System.Net.IPEndPoint endpoint)
                    ipAddress = endpoint.Address.ToString();
            }
            catch { }

            Console.WriteLine($"🔑 KeyAuth Request: Key={key}, GameId={gameId}, Version={version}, IP={ipAddress}");

            var keysCollection = _database.Database.GetCollection<KeyEntry>("RegistrationKeys");
            var playersCollection = _database.Database.GetCollection<Player>("Players2");
            var hwidsCollection = _database.Database.GetCollection<HwidEntry>("Hwids2");

            // ========== АВТО-ВХОД: Проверяем есть ли уже привязанный аккаунт к этому HWID ==========
            if (!string.IsNullOrEmpty(gameId))
            {
                var existingHwid = await hwidsCollection.Find(h => h.Hwid == gameId).FirstOrDefaultAsync();
                
                if (existingHwid != null && !string.IsNullOrEmpty(existingHwid.PlayerId))
                {
                    // Есть привязанный аккаунт - пробуем авто-вход
                    var existingPlayer = await playersCollection.Find(p => p.Id == ObjectId.Parse(existingHwid.PlayerId)).FirstOrDefaultAsync();
                    
                    if (existingPlayer != null)
                    {
                        // Проверяем что ключ все еще активен
                        var linkedKey = await keysCollection.Find(k => k.PlayerId == existingHwid.PlayerId && k.IsUsed).FirstOrDefaultAsync();
                        
                        if (linkedKey != null)
                        {
                            // Ключ активен - делаем авто-вход!
                            Console.WriteLine($"🔑 AutoLogin: Found existing account {existingPlayer.Name} for HWID {gameId}");
                            
                            await UpdateHwidEntry(hwidsCollection, existingHwid, gameId, ipAddress, existingPlayer.Token);
                            await UpdatePlayerLogin(existingPlayer, gameId, ipAddress, existingHwid.CustomKey ?? "");

                            _sessionManager.AddSession(new PlayerSession
                            {
                                PlayerObjectId = existingPlayer.Id.ToString(),
                                Token = existingPlayer.Token,
                                Client = client
                            });

                            var autoResultToken = new Axlebolt.RpcSupport.Protobuf.String { Value = existingPlayer.Token };
                            var autoResult = new BinaryValue
                            {
                                IsNull = false,
                                One = autoResultToken.ToByteString()
                            };

                            await _handler.WriteProtoResponseAsync(client, request.Id, autoResult, null);
                            Console.WriteLine($"✅ KeyAuth AutoLogin success: {existingPlayer.Name}");
                            await RecordAuthLog(gameId, linkedKey.Key, ipAddress, existingHwid.CustomKey ?? "", "AutoLogin", $"Auto-login: {existingPlayer.Name}");
                            return;
                        }
                        else
                        {
                            // Ключ удален - показываем сообщение и требуем новый ключ
                            Console.WriteLine($"🔑 Key deleted for player {existingPlayer.Name}, requiring new key");
                            // Удаляем старую привязку HWID
                            await hwidsCollection.DeleteOneAsync(h => h.Id == existingHwid.Id);
                        }
                    }
                    else
                    {
                        // Игрок удален - удаляем привязку HWID
                        Console.WriteLine($"🔑 Player deleted for HWID {gameId}, clearing binding");
                        await hwidsCollection.DeleteOneAsync(h => h.Id == existingHwid.Id);
                    }
                }
            }
            // ========== КОНЕЦ АВТО-ВХОДА ==========

            // Стандартная авторизация по ключу
            var keyEntry = await keysCollection.Find(k => k.Key == key).FirstOrDefaultAsync();

            if (keyEntry == null)
            {
                Console.WriteLine($"❌ KeyAuth failed: Invalid Key {key}");
                await SendError(client, request.Id, 401, "Invalid Key");
                await RecordAuthLog(gameId, key, ipAddress, "", "Error", "Invalid registration key");
                return;
            }

            Player? player = null;
            HwidEntry? hwidEntry = null;

            if (keyEntry.IsUsed && !string.IsNullOrEmpty(keyEntry.PlayerId))
            {
                player = await playersCollection.Find(p => p.Id == ObjectId.Parse(keyEntry.PlayerId)).FirstOrDefaultAsync();
                
                if (player != null)
                {
                    hwidEntry = await hwidsCollection.Find(h => h.PlayerId == player.Id.ToString()).FirstOrDefaultAsync();
                    if (hwidEntry != null)
                    {
                        await UpdateHwidEntry(hwidsCollection, hwidEntry, gameId, ipAddress, player.Token);
                    }
                    else if (!string.IsNullOrEmpty(gameId))
                    {
                        hwidEntry = await CreateHwidEntry(hwidsCollection, gameId, ipAddress, player);
                    }
                }
            }
            else
            {
                // Create new player
                player = await CreateNewPlayer(gameId, ipAddress);
                
                if (!string.IsNullOrEmpty(gameId))
                {
                    hwidEntry = await CreateHwidEntry(hwidsCollection, gameId, ipAddress, player);
                }

                keyEntry.IsUsed = true;
                keyEntry.PlayerId = player.Id.ToString();
                await keysCollection.ReplaceOneAsync(k => k.Id == keyEntry.Id, keyEntry);
                
                Console.WriteLine($"✅ Created new player {player.Name} via Key");
                await RecordAuthLog(gameId, key, ipAddress, hwidEntry?.CustomKey ?? "", "NewAccount", $"Created via Key: {player.Name}");
            }

            if (player == null)
            {
                await SendError(client, request.Id, 500, "Player creation failed");
                return;
            }

            // Update session and login tracking
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
            Console.WriteLine($"✅ KeyAuth success: {player.Name}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ KeyAuth error: {ex.Message}");
            await SendError(client, request.Id, 500, ex.Message);
            await RecordAuthLog(gameId, key, ipAddress, "", "Error", ex.Message);
        }
    }

    private async Task<Player> CreateNewPlayer(string deviceId, string ipAddress)
    {
        var playersCollection = _database.Database.GetCollection<Player>("Players2");
        var lastPlayer = await playersCollection.Find(_ => true).SortByDescending(p => p.OriginalUid).FirstOrDefaultAsync();
        int newUid = (lastPlayer?.OriginalUid ?? 10000) + 1;

        var player = new Player
        {
            PlayerUid = newUid.ToString(),
            OriginalUid = newUid,
            Name = $"Key_{newUid}",
            Token = Guid.NewGuid().ToString(),
            DeviceId = deviceId,
            LastIpAddress = ipAddress,
            RegistrationDate = DateTime.UtcNow,
            LastLogin = DateTime.UtcNow,
            Level = 1,
            Coins = 10000,
            Gems = 1000,
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

        await playersCollection.InsertOneAsync(player);
        return player;
    }

    private async Task UpdateHwidEntry(IMongoCollection<HwidEntry> collection, HwidEntry entry, string newHwid, string newIp, string playerToken)
    {
        var updates = new List<UpdateDefinition<HwidEntry>>
        {
            Builders<HwidEntry>.Update.Set(h => h.LastSeen, DateTime.UtcNow),
            Builders<HwidEntry>.Update.Set(h => h.PlayerToken, playerToken)
        };

        if (!string.IsNullOrEmpty(newHwid) && entry.Hwid != newHwid)
        {
            updates.Add(Builders<HwidEntry>.Update.Set(h => h.Hwid, newHwid));
        }

        if (entry.LastIp != newIp)
        {
            updates.Add(Builders<HwidEntry>.Update.Set(h => h.LastIp, newIp));
            updates.Add(Builders<HwidEntry>.Update.Push(h => h.IpHistory, new IpHistoryEntry { Ip = newIp, Timestamp = DateTime.UtcNow }));
        }

        await collection.UpdateOneAsync(h => h.Id == entry.Id, Builders<HwidEntry>.Update.Combine(updates));
    }

    private async Task<HwidEntry> CreateHwidEntry(IMongoCollection<HwidEntry> collection, string hwid, string ip, Player player)
    {
        var entry = new HwidEntry
        {
            Hwid = hwid,
            CustomKey = GenerateRandomKey(16),
            PlayerToken = player.Token,
            PlayerId = player.Id.ToString(),
            LastIp = ip,
            IpHistory = new List<IpHistoryEntry> { new() { Ip = ip, Timestamp = DateTime.UtcNow } },
            FirstSeen = DateTime.UtcNow,
            LastSeen = DateTime.UtcNow
        };
        await collection.InsertOneAsync(entry);
        return entry;
    }

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
                
            await collection.UpdateOneAsync(l => l.Hwid == hwid && l.Ip == ip, update, new UpdateOptions { IsUpsert = true });
        }
        catch (Exception ex) { Console.WriteLine($"⚠️ KeyAuth failed to update PlayerLogin: {ex.Message}"); }
    }

    private async Task RecordAuthLog(string hwid, string token, string ip, string key, string status, string details)
    {
        try
        {
            var logs = _database.Database.GetCollection<AuthLog>("AuthLogs");
            await logs.InsertOneAsync(new AuthLog { Hwid = hwid, Token = token, Ip = ip, CustomKey = key, Status = status, Details = details, Timestamp = DateTime.UtcNow });
        }
        catch (Exception ex) { Console.WriteLine($"⚠️ KeyAuth failed to record auth log: {ex.Message}"); }
    }

    private string GenerateRandomKey(int length)
    {
        lock (_keyGenLock)
        {
            const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789";
            var random = new Random();
            return new string(Enumerable.Repeat(chars, length).Select(s => s[random.Next(s.Length)]).ToArray());
        }
    }

    private async Task SendError(TcpClient client, string guid, int code, string message)
    {
        await _handler.WriteProtoResponseAsync(client, guid, null, 
            new RpcException { Id = guid, Code = code, Property = new RpcExceptionProperty { Reason = message } });
    }
}
