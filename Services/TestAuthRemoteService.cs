using System.Net.Sockets;
using Axlebolt.RpcSupport.Protobuf;
using StandRiseServer.Core;
using StandRiseServer.Models;
using MongoDB.Driver;
using MongoDB.Bson;
using Google.Protobuf;

namespace StandRiseServer.Services;

public class TestAuthRemoteService
{
    private readonly ProtobufHandler _handler;
    private readonly DatabaseService _database;
    private readonly SessionManager _sessionManager;
    private static readonly object _keyGenLock = new();

    public TestAuthRemoteService(ProtobufHandler handler, DatabaseService database, SessionManager sessionManager)
    {
        _handler = handler;
        _database = database;
        _sessionManager = sessionManager;

        Console.WriteLine("Registering TestAuthRemoteService handlers...");
        _handler.RegisterHandler("TestAuthRemoteService", "auth", AuthAsync);
        Console.WriteLine("TestAuthRemoteService handlers registered!");
    }

    private async Task AuthAsync(TcpClient client, RpcRequest request)
    {
        string gameId = "";
        string token = "";
        string ipAddress = "unknown";

        try
        {
            Console.WriteLine("=== TestAuth Request ===");

            if (request.Params.Count > 0 && request.Params[0].One != null)
                gameId = Axlebolt.RpcSupport.Protobuf.String.Parser.ParseFrom(request.Params[0].One).Value;
            if (request.Params.Count > 3 && request.Params[3].One != null)
                token = Axlebolt.RpcSupport.Protobuf.String.Parser.ParseFrom(request.Params[3].One).Value;

            try
            {
                if (client.Client.RemoteEndPoint is System.Net.IPEndPoint endpoint)
                    ipAddress = endpoint.Address.ToString();
            }
            catch { }

            Console.WriteLine($"Auth: HWID={gameId}, Token={(token.Length > 20 ? token.Substring(0, 20) + "..." : token)}, IP={ipAddress}");

            var playersCollection = _database.Database.GetCollection<Player>("Players2");
            var hwidsCollection = _database.Database.GetCollection<HwidEntry>("Hwids2");

            Player? player = null;
            HwidEntry? hwidEntry = null;

            // LOGIN token format: LOGIN:username:password_hash
            if (!string.IsNullOrEmpty(token) && token.StartsWith("LOGIN:"))
            {
                Console.WriteLine("Detected LOGIN token format");
                var parts = token.Split(':');
                if (parts.Length >= 3)
                {
                    string username = parts[1];
                    string passwordHash = parts[2];
                    
                    Console.WriteLine($"Login attempt: username={username}");
                    
                    player = await playersCollection.Find(p => p.Username == username).FirstOrDefaultAsync();
                    
                    if (player != null)
                    {
                        if (player.PasswordHash == passwordHash)
                        {
                            Console.WriteLine($"Password match for: {username}");
                            player.Token = Guid.NewGuid().ToString();
                            player.LastLogin = DateTime.UtcNow;
                            player.LastIpAddress = ipAddress;
                            player.DeviceId = gameId;
                            player.LastHwid = gameId;
                            await playersCollection.ReplaceOneAsync(p => p.Id == player.Id, player);
                        }
                        else
                        {
                            Console.WriteLine($"Wrong password for: {username}");
                            await _handler.WriteProtoResponseAsync(client, request.Id, null,
                                new RpcException { Id = request.Id, Code = 1005 });
                            return;
                        }
                    }
                    else
                    {
                        Console.WriteLine($"Creating new player: {username}");
                        var lastPlayer = await playersCollection.Find(_ => true).SortByDescending(p => p.OriginalUid).FirstOrDefaultAsync();
                        int newUid = (lastPlayer?.OriginalUid ?? 10000) + 1;
                        
                        player = new Player
                        {
                            Id = ObjectId.GenerateNewId(),
                            PlayerUid = newUid.ToString(),
                            OriginalUid = newUid,
                            Name = username,
                            Username = username,
                            PasswordHash = passwordHash,
                            Token = Guid.NewGuid().ToString(),
                            DeviceId = gameId,
                            LastHwid = gameId,
                            LastIpAddress = ipAddress,
                            LastLogin = DateTime.UtcNow,
                            RegistrationDate = DateTime.UtcNow,
                            Level = 1,
                            Coins = 10000,
                            Gems = 1000,
                            Inventory = new PlayerInventoryData { Items = new List<PlayerInventoryItem>() },
                            Stats = new PlayerStats { ArrayCust = new List<StatItem>() }
                        };
                        
                        await playersCollection.InsertOneAsync(player);
                        Console.WriteLine($"Created player: {username} (UID: {newUid})");
                    }
                    
                    if (!string.IsNullOrEmpty(gameId))
                    {
                        hwidEntry = await hwidsCollection.Find(h => h.PlayerId == player.Id.ToString()).FirstOrDefaultAsync();
                        if (hwidEntry != null)
                        {
                            await UpdateHwidEntry(hwidsCollection, hwidEntry, gameId, ipAddress, player.Token);
                        }
                        else
                        {
                            hwidEntry = await CreateHwidEntry(hwidsCollection, gameId, ipAddress, player);
                        }
                    }
                    
                    _sessionManager.AddSession(new PlayerSession
                    {
                        PlayerObjectId = player.Id.ToString(),
                        Token = player.Token,
                        Client = client
                    });

                    var loginResultToken = new Axlebolt.RpcSupport.Protobuf.String { Value = player.Token };
                    var loginResult = new BinaryValue
                    {
                        IsNull = false,
                        One = ByteString.CopyFrom(loginResultToken.ToByteArray())
                    };

                    await _handler.WriteProtoResponseAsync(client, request.Id, loginResult, null);
                    Console.WriteLine($"LOGIN Auth completed: {player.Name}");
                    return;
                }
            }

            // Standard token auth
            if (!string.IsNullOrEmpty(token))
            {
                player = await playersCollection
                    .Find(p => p.Token == token || p.AuthToken == token)
                    .FirstOrDefaultAsync();

                if (player != null)
                {
                    Console.WriteLine($"Found player by token: {player.Name}");
                    
                    hwidEntry = await hwidsCollection
                        .Find(h => h.PlayerId == player.Id.ToString())
                        .FirstOrDefaultAsync();

                    if (hwidEntry != null)
                    {
                        await UpdateHwidEntry(hwidsCollection, hwidEntry, gameId, ipAddress, player.Token);
                    }
                    else
                    {
                        hwidEntry = await CreateHwidEntry(hwidsCollection, gameId, ipAddress, player);
                    }
                }
            }

            // Fingerprint fallback
            if (player == null && !string.IsNullOrEmpty(gameId))
            {
                var loginsCollection = _database.Database.GetCollection<PlayerLogin>("PlayerLogins");
                var loginRecord = await loginsCollection
                    .Find(l => l.Hwid == gameId && l.Ip == ipAddress)
                    .FirstOrDefaultAsync();

                if (loginRecord != null)
                {
                    player = await playersCollection
                        .Find(p => p.PlayerUid == loginRecord.PlayerOrig)
                        .FirstOrDefaultAsync();
                    
                    if (player != null)
                    {
                        Console.WriteLine($"Found player by Fingerprint: {player.Name}");
                        hwidEntry = await hwidsCollection
                            .Find(h => h.PlayerId == player.Id.ToString())
                            .FirstOrDefaultAsync();
                    }
                }
            }

            // Create new account
            if (player == null)
            {
                player = await CreateNewPlayer(playersCollection, gameId, ipAddress);
                hwidEntry = await CreateHwidEntry(hwidsCollection, gameId, ipAddress, player);
                Console.WriteLine($"Created new account: {player.Name}");
            }
            else
            {
                await UpdateExistingPlayer(playersCollection, player, gameId, ipAddress, hwidEntry?.CustomKey ?? "");
            }

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
                One = ByteString.CopyFrom(resultToken.ToByteArray())
            };

            await _handler.WriteProtoResponseAsync(client, request.Id, result, null);
            Console.WriteLine($"Auth completed: {player.Name}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"TestAuth error: {ex.Message}");
            await _handler.WriteProtoResponseAsync(client, request.Id, null,
                new RpcException { Id = request.Id, Code = 500 });
        }
    }

    private async Task UpdateHwidEntry(IMongoCollection<HwidEntry> collection, HwidEntry entry, string newHwid, string newIp, string playerToken)
    {
        var updates = new List<UpdateDefinition<HwidEntry>>
        {
            Builders<HwidEntry>.Update.Set(h => h.LastSeen, DateTime.UtcNow),
            Builders<HwidEntry>.Update.Set(h => h.PlayerToken, playerToken)
        };

        if (!string.IsNullOrEmpty(newHwid) && entry.Hwid != newHwid)
            updates.Add(Builders<HwidEntry>.Update.Set(h => h.Hwid, newHwid));

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

    private async Task<Player> CreateNewPlayer(IMongoCollection<Player> collection, string deviceId, string ipAddress)
    {
        var lastPlayer = await collection.Find(_ => true).SortByDescending(p => p.OriginalUid).FirstOrDefaultAsync();
        int newUid = (lastPlayer?.OriginalUid ?? 10000) + 1;

        var player = new Player
        {
            PlayerUid = newUid.ToString(),
            OriginalUid = newUid,
            Name = $"Player_{newUid}",
            Token = Guid.NewGuid().ToString(),
            DeviceId = deviceId,
            LastIpAddress = ipAddress,
            LastLogin = DateTime.UtcNow,
            Level = 1,
            Coins = 10000,
            Gems = 1000,
            RegistrationDate = DateTime.UtcNow,
            Inventory = new PlayerInventoryData { Items = new List<PlayerInventoryItem>() },
            Stats = new PlayerStats { ArrayCust = new List<StatItem>() }
        };

        await collection.InsertOneAsync(player);
        return player;
    }

    private async Task UpdateExistingPlayer(IMongoCollection<Player> collection, Player player, string deviceId, string ipAddress, string customKey)
    {
        player.LastLogin = DateTime.UtcNow;
        player.LastIpAddress = ipAddress;
        if (string.IsNullOrEmpty(player.DeviceId)) player.DeviceId = deviceId;
        if (string.IsNullOrEmpty(player.CustomKey)) player.CustomKey = customKey;
        await collection.ReplaceOneAsync(p => p.Id == player.Id, player);
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
        catch { }
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
}
