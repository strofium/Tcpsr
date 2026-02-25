// AuthService.cs
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Threading.RateLimiting;
using Axlebolt.RpcSupport.Protobuf;
using Google.Protobuf;
using Microsoft.Extensions.Caching.Memory;
using StandRiseServer.Core;
using StandRiseServer.Models;
using StandRiseServer.Utils;

namespace StandRiseServer.Services;

public class AuthService
{
    private readonly ProtobufHandler _handler;
    private readonly DatabaseService _database;
    private readonly SessionManager _sessionManager;
    private readonly IMemoryCache _rateLimitCache;
    private readonly ILogger<AuthService> _logger;
    private const int BcryptWorkFactor = 12;

    public AuthService(
        ProtobufHandler handler, 
        DatabaseService database, 
        SessionManager sessionManager,
        IMemoryCache rateLimitCache,
        ILogger<AuthService> logger)
    {
        _handler = handler;
        _database = database;
        _sessionManager = sessionManager;
        _rateLimitCache = rateLimitCache;
        _logger = logger;

        _logger.LogInformation("🔐 Registering AuthService handlers...");
        _handler.RegisterHandler("AuthRemoteService", "register", RegisterAsync);
        _handler.RegisterHandler("AuthRemoteService", "login", LoginAsync);
        _handler.RegisterHandler("AuthRemoteService", "logout", LogoutAsync);
        _handler.RegisterHandler("AuthRemoteService", "changePassword", ChangePasswordAsync);
        _logger.LogInformation("🔐 AuthService handlers registered!");
    }

    private bool CheckRateLimit(string ip, string action, int maxAttempts, TimeSpan window)
    {
        var key = $"rate_limit:{action}:{ip}";
        if (_rateLimitCache.TryGetValue<int>(key, out var attempts))
        {
            if (attempts >= maxAttempts)
                return false;
            _rateLimitCache.Set(key, attempts + 1, window);
        }
        else
        {
            _rateLimitCache.Set(key, 1, window);
        }
        return true;
    }

    private string HashPassword(string password)
    {
        return BCrypt.Net.BCrypt.HashPassword(password, BcryptWorkFactor);
    }

    private bool VerifyPassword(string password, string hash)
    {
        // Проверка, не MD5 ли это (для миграции)
        if (hash.Length == 32 && System.Text.RegularExpressions.Regex.IsMatch(hash, "^[a-fA-F0-9]{32}$"))
        {
            var md5Hash = Converters.CalculateMD5(password);
            return md5Hash.Equals(hash, StringComparison.OrdinalIgnoreCase);
        }
        
        return BCrypt.Net.BCrypt.Verify(password, hash);
    }

    private async Task RegisterAsync(TcpClient client, RpcRequest request)
    {
        var clientIp = (client.Client.RemoteEndPoint as System.Net.IPEndPoint)?.Address.ToString() ?? "unknown";
        
        try
        {
            _logger.LogInformation("🔐 Register Request from {Ip}", clientIp);

            if (!CheckRateLimit(clientIp, "register", 5, TimeSpan.FromHours(1)))
            {
                _logger.LogWarning("Rate limit exceeded for register from {Ip}", clientIp);
                await SendErrorAsync(client, request.Id, 429); // Too Many Requests
                return;
            }

            string username = "", password = "", email = "", deviceId = "";

            if (request.Params.Count > 0 && request.Params[0].One != null)
                username = Axlebolt.RpcSupport.Protobuf.String.Parser.ParseFrom(request.Params[0].One).Value;
            if (request.Params.Count > 1 && request.Params[1].One != null)
                password = Axlebolt.RpcSupport.Protobuf.String.Parser.ParseFrom(request.Params[1].One).Value;
            if (request.Params.Count > 2 && request.Params[2].One != null)
                email = Axlebolt.RpcSupport.Protobuf.String.Parser.ParseFrom(request.Params[2].One).Value;
            if (request.Params.Count > 3 && request.Params[3].One != null)
                deviceId = Axlebolt.RpcSupport.Protobuf.String.Parser.ParseFrom(request.Params[3].One).Value;

            _logger.LogInformation("Register: username='{Username}', deviceId='{DeviceId}'", username, deviceId);

            // Валидация
            if (string.IsNullOrWhiteSpace(username) || username.Length < 3)
            {
                _logger.LogWarning("Username too short: {Username}", username);
                await SendErrorAsync(client, request.Id, 1001);
                return;
            }

            if (string.IsNullOrWhiteSpace(password) || password.Length < 4)
            {
                _logger.LogWarning("Password too short for user {Username}", username);
                await SendErrorAsync(client, request.Id, 1002);
                return;
            }

            if (!string.IsNullOrEmpty(email) && !IsValidEmail(email))
            {
                _logger.LogWarning("Invalid email for user {Username}: {Email}", username, email);
                await SendErrorAsync(client, request.Id, 1007);
                return;
            }

            var playersCollection = _database.GetCollection<Player>("Players2");

            // Проверяем существует ли пользователь
            var existingPlayer = await playersCollection.Find(p => p.Username == username).FirstOrDefaultAsync();
            if (existingPlayer != null)
            {
                _logger.LogWarning("Username already exists: {Username}", username);
                await SendErrorAsync(client, request.Id, 1003);
                return;
            }

            // Проверяем email если указан
            if (!string.IsNullOrEmpty(email))
            {
                var existingEmail = await playersCollection.Find(p => p.Email == email).FirstOrDefaultAsync();
                if (existingEmail != null)
                {
                    _logger.LogWarning("Email already in use: {Email}", email);
                    await SendErrorAsync(client, request.Id, 1008);
                    return;
                }
            }

            // Создаем нового игрока
            var lastPlayer = await playersCollection.Find(_ => true).SortByDescending(p => p.OriginalUid).FirstOrDefaultAsync();
            int newUid = (lastPlayer?.OriginalUid ?? 10000) + 1;

            var token = Guid.NewGuid().ToString();
            var passwordHash = HashPassword(password);

            var newPlayer = new Player
            {
                Id = MongoDB.Bson.ObjectId.GenerateNewId(),
                PlayerUid = newUid.ToString(),
                OriginalUid = newUid,
                Name = username,
                Username = username,
                PasswordHash = passwordHash,
                Email = email,
                Token = token,
                DeviceId = deviceId,
                LastHwid = deviceId,
                LastIpAddress = clientIp,
                RegistrationDate = DateTime.UtcNow,
                LastLogin = DateTime.UtcNow,
                Level = 1,
                Coins = 10000,
                Gems = 100000000,
                IsBanned = false,
                NoDetectRoot = true,
                Inventory = new PlayerInventoryData { Items = new List<PlayerInventoryItem>() },
                Stats = new PlayerStats { ArrayCust = new List<StatItem>() },
                Social = new SocialInfo(),
                OnlineStatus = OnlineStatus.Online,
                LastStatusUpdate = DateTime.UtcNow
            };

            await playersCollection.InsertOneAsync(newPlayer);
            _logger.LogInformation("Created player: {Username} (UID: {Uid})", username, newUid);

            // Удаляем старые сессии этого игрока (если были)
            var oldSessions = _sessionManager.GetSessionsByPlayerId(newPlayer.Id.ToString()).ToList();
            foreach (var oldSession in oldSessions)
            {
                if (oldSession != null)
                    _sessionManager.RemoveSession(oldSession.Token);
            }

            // Создаем сессию
            _sessionManager.AddSession(new PlayerSession
            {
                PlayerObjectId = newPlayer.Id.ToString(),
                Token = token,
                Hwid = deviceId,
                Client = client,
                LastActivityTime = DateTime.UtcNow
            });

            await SendTokenAsync(client, request.Id, token);
            _logger.LogInformation("Register success: {Username}", username);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Register error from {Ip}", clientIp);
            await SendErrorAsync(client, request.Id, 5000);
        }
    }

    private async Task LoginAsync(TcpClient client, RpcRequest request)
    {
        var clientIp = (client.Client.RemoteEndPoint as System.Net.IPEndPoint)?.Address.ToString() ?? "unknown";
        
        try
        {
            _logger.LogInformation("🔐 Login Request from {Ip}", clientIp);

            if (!CheckRateLimit(clientIp, "login", 10, TimeSpan.FromMinutes(1)))
            {
                _logger.LogWarning("Rate limit exceeded for login from {Ip}", clientIp);
                await SendErrorAsync(client, request.Id, 429);
                return;
            }

            string username = "", password = "", deviceId = "";

            if (request.Params.Count > 0 && request.Params[0].One != null)
                username = Axlebolt.RpcSupport.Protobuf.String.Parser.ParseFrom(request.Params[0].One).Value;
            if (request.Params.Count > 1 && request.Params[1].One != null)
                password = Axlebolt.RpcSupport.Protobuf.String.Parser.ParseFrom(request.Params[1].One).Value;
            if (request.Params.Count > 2 && request.Params[2].One != null)
                deviceId = Axlebolt.RpcSupport.Protobuf.String.Parser.ParseFrom(request.Params[2].One).Value;

            _logger.LogInformation("Login: username='{Username}', deviceId='{DeviceId}'", username, deviceId);

            if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
            {
                _logger.LogWarning("Empty username or password from {Ip}", clientIp);
                await SendErrorAsync(client, request.Id, 1001);
                return;
            }

            var playersCollection = _database.GetCollection<Player>("Players2");
            var player = await playersCollection.Find(p => p.Username == username).FirstOrDefaultAsync();

            if (player == null)
            {
                _logger.LogWarning("User not found: {Username} from {Ip}", username, clientIp);
                await SendErrorAsync(client, request.Id, 1004);
                return;
            }

            // Проверяем пароль
            if (!VerifyPassword(password, player.PasswordHash))
            {
                _logger.LogWarning("Wrong password for: {Username} from {Ip}", username, clientIp);
                await SendErrorAsync(client, request.Id, 1005);
                return;
            }

            // Проверяем бан
            if (player.IsBanned)
            {
                _logger.LogWarning("Player banned: {Username} from {Ip}", username, clientIp);
                await SendErrorAsync(client, request.Id, 1006);
                return;
            }

            // Если пароль был MD5, обновляем на bcrypt
            if (player.PasswordHash.Length == 32 && 
                System.Text.RegularExpressions.Regex.IsMatch(player.PasswordHash, "^[a-fA-F0-9]{32}$"))
            {
                var update = Builders<Player>.Update.Set(p => p.PasswordHash, HashPassword(password));
                await playersCollection.UpdateOneAsync(p => p.Id == player.Id, update);
                _logger.LogInformation("Migrated password for {Username} to bcrypt", username);
            }

            // Генерируем новый токен
            var token = Guid.NewGuid().ToString();
            
            var updates = new List<UpdateDefinition<Player>>
            {
                Builders<Player>.Update.Set(p => p.Token, token),
                Builders<Player>.Update.Set(p => p.LastLogin, DateTime.UtcNow),
                Builders<Player>.Update.Set(p => p.LastHwid, deviceId),
                Builders<Player>.Update.Set(p => p.LastIpAddress, clientIp),
                Builders<Player>.Update.Set(p => p.DeviceId, deviceId),
                Builders<Player>.Update.Set(p => p.OnlineStatus, OnlineStatus.Online),
                Builders<Player>.Update.Set(p => p.LastStatusUpdate, DateTime.UtcNow)
            };

            var combinedUpdate = Builders<Player>.Update.Combine(updates);
            await playersCollection.UpdateOneAsync(p => p.Id == player.Id, combinedUpdate);

            // Удаляем старые сессии этого игрока
            var oldSessions = _sessionManager.GetSessionsByPlayerId(player.Id.ToString()).ToList();
            foreach (var oldSession in oldSessions)
            {
                if (oldSession != null)
                    _sessionManager.RemoveSession(oldSession.Token);
            }

            // Создаем сессию
            _sessionManager.AddSession(new PlayerSession
            {
                PlayerObjectId = player.Id.ToString(),
                Token = token,
                Hwid = deviceId,
                TimeInGame = player.TimeInGame,
                Client = client,
                LastActivityTime = DateTime.UtcNow
            });

            // Оповещаем друзей о смене статуса
            await NotifyFriendsStatusChanged(player.Id.ToString(), player.PlayerUid, OnlineStatus.Online, null);

            await SendTokenAsync(client, request.Id, token);
            _logger.LogInformation("Login success: {Username} (UID: {Uid})", username, player.PlayerUid);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Login error from {Ip}", clientIp);
            await SendErrorAsync(client, request.Id, 5000);
        }
    }

    private async Task LogoutAsync(TcpClient client, RpcRequest request)
    {
        try
        {
            var session = _sessionManager.GetSessionByClient(client);
            if (session != null)
            {
                await UpdatePlayerStatus(session.PlayerObjectId, OnlineStatus.Offline, null);
                _sessionManager.RemoveSession(session.Token);
                _logger.LogInformation("Logout: {Token}", session.Token);
            }

            var result = new BinaryValue { IsNull = true };
            await _handler.WriteProtoResponseAsync(client, request.Id, result, null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Logout error");
        }
    }

    private async Task ChangePasswordAsync(TcpClient client, RpcRequest request)
    {
        try
        {
            var session = _sessionManager.GetSessionByClient(client);
            if (session == null)
            {
                await SendErrorAsync(client, request.Id, 401); // Unauthorized
                return;
            }

            string oldPassword = "", newPassword = "";

            if (request.Params.Count > 0 && request.Params[0].One != null)
                oldPassword = Axlebolt.RpcSupport.Protobuf.String.Parser.ParseFrom(request.Params[0].One).Value;
            if (request.Params.Count > 1 && request.Params[1].One != null)
                newPassword = Axlebolt.RpcSupport.Protobuf.String.Parser.ParseFrom(request.Params[1].One).Value;

            if (string.IsNullOrWhiteSpace(oldPassword) || string.IsNullOrWhiteSpace(newPassword) || newPassword.Length < 4)
            {
                await SendErrorAsync(client, request.Id, 1002);
                return;
            }

            var player = await _database.GetPlayerByObjectIdAsync(session.PlayerObjectId);
            if (player == null)
            {
                await SendErrorAsync(client, request.Id, 1004);
                return;
            }

            if (!VerifyPassword(oldPassword, player.PasswordHash))
            {
                await SendErrorAsync(client, request.Id, 1005);
                return;
            }

            var update = Builders<Player>.Update.Set(p => p.PasswordHash, HashPassword(newPassword));
            await _database.GetCollection<Player>("Players2").UpdateOneAsync(p => p.Id == player.Id, update);

            await SendTokenAsync(client, request.Id, "OK");
            _logger.LogInformation("Password changed for player {PlayerId}", player.PlayerUid);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "ChangePassword error");
            await SendErrorAsync(client, request.Id, 5000);
        }
    }

    private async Task UpdatePlayerStatus(string playerObjectId, OnlineStatus status, CurrentGameInfo? gameInfo)
    {
        var playersCollection = _database.GetCollection<Player>("Players2");
        var objectId = MongoDB.Bson.ObjectId.Parse(playerObjectId);
        
        var updates = new List<UpdateDefinition<Player>>
        {
            Builders<Player>.Update.Set(p => p.OnlineStatus, status),
            Builders<Player>.Update.Set(p => p.LastStatusUpdate, DateTime.UtcNow)
        };

        if (gameInfo != null)
        {
            updates.Add(Builders<Player>.Update.Set(p => p.CurrentGameInfo, gameInfo));
        }
        else if (status != OnlineStatus.InGame)
        {
            updates.Add(Builders<Player>.Update.Unset(p => p.CurrentGameInfo));
        }

        var combinedUpdate = Builders<Player>.Update.Combine(updates);
        await playersCollection.UpdateOneAsync(p => p.Id == objectId, combinedUpdate);

        // Получаем игрока для оповещения друзей
        var player = await playersCollection.Find(p => p.Id == objectId).FirstOrDefaultAsync();
        if (player != null)
        {
            await NotifyFriendsStatusChanged(playerObjectId, player.PlayerUid, status, gameInfo);
        }
    }

    private async Task NotifyFriendsStatusChanged(string playerObjectId, string playerUid, OnlineStatus status, CurrentGameInfo? gameInfo)
    {
        var player = await _database.GetPlayerByObjectIdAsync(playerObjectId);
        if (player?.Social?.Friends == null) return;

        foreach (var friendId in player.Social.Friends)
        {
            var friend = await _database.GetPlayerByUidAsync(friendId);
            if (friend == null) continue;

            var friendSession = _sessionManager.GetSessionByPlayerId(friend.Id.ToString());
            if (friendSession?.Client?.Connected == true)
            {
                var statusEvent = new FriendStatusChangedEvent
                {
                    FriendUid = playerUid,
                    Status = (int)status,
                    GameMode = gameInfo?.GameMode,
                    RoomId = gameInfo?.RoomId
                };

                await _handler.SendEventAsync(friendSession.Client, "FriendsRemoteService", "OnFriendStatusChanged",
                    ByteString.CopyFrom(statusEvent.ToByteArray()));
            }
        }
    }

    private bool IsValidEmail(string email)
    {
        try
        {
            var addr = new System.Net.Mail.MailAddress(email);
            return addr.Address == email;
        }
        catch
        {
            return false;
        }
    }

    private async Task SendTokenAsync(TcpClient client, string requestId, string token)
    {
        var tokenString = new Axlebolt.RpcSupport.Protobuf.String { Value = token };
        var result = new BinaryValue
        {
            IsNull = false,
            One = ByteString.CopyFrom(tokenString.ToByteArray())
        };
        await _handler.WriteProtoResponseAsync(client, requestId, result, null);
    }

    private async Task SendErrorAsync(TcpClient client, string requestId, int code)
    {
        await _handler.WriteProtoResponseAsync(client, requestId, null,
            new RpcException { Id = requestId, Code = code });
    }
}
