using System.Net.Sockets;
using Axlebolt.RpcSupport.Protobuf;
using Axlebolt.Bolt.Protobuf;
using StandRiseServer.Core;
using StandRiseServer.Models;
using StandRiseServer.Utils;
using Google.Protobuf;
using MongoDB.Bson;
using MongoDB.Driver;

namespace StandRiseServer.Services;

/// <summary>
/// PKConnect авторизация - логин/пароль
/// Формат как в KeyAuthService
/// </summary>
public class PKConnectRemoteService
{
    private readonly ProtobufHandler _handler;
    private readonly DatabaseService _database;
    private readonly SessionManager _sessionManager;

    public PKConnectRemoteService(ProtobufHandler handler, DatabaseService database, SessionManager sessionManager)
    {
        _handler = handler;
        _database = database;
        _sessionManager = sessionManager;

        Console.WriteLine("🔐 Registering PKConnectRemoteService handlers...");
        _handler.RegisterHandler("PKConnectRemoteService", "auth", HandleAuthAsync);
        Console.WriteLine("🔐 PKConnectRemoteService handlers registered!");
    }

    private async Task HandleAuthAsync(TcpClient client, RpcRequest request)
    {
        string login = "";
        string password = "";
        string deviceId = "";
        string ipAddress = "unknown";

        try
        {
            Console.WriteLine("=== PKConnect Auth Request ===");
            Console.WriteLine($"Params count: {request.Params.Count}");

            // Логируем все параметры
            for (int i = 0; i < request.Params.Count; i++)
            {
                var param = request.Params[i];
                if (param.One != null)
                {
                    try
                    {
                        var str = Axlebolt.RpcSupport.Protobuf.String.Parser.ParseFrom(param.One).Value;
                        Console.WriteLine($"  Param[{i}] = '{str}'");
                    }
                    catch
                    {
                        Console.WriteLine($"  Param[{i}] = [binary {param.One.Length} bytes]");
                    }
                }
                else
                {
                    Console.WriteLine($"  Param[{i}] = null");
                }
            }

            // Получаем IP
            try
            {
                if (client.Client.RemoteEndPoint is System.Net.IPEndPoint endpoint)
                    ipAddress = endpoint.Address.ToString();
            }
            catch { }

            // Парсим параметры
            if (request.Params.Count >= 2)
            {
                if (request.Params[0].One != null)
                    login = Axlebolt.RpcSupport.Protobuf.String.Parser.ParseFrom(request.Params[0].One).Value;
                if (request.Params[1].One != null)
                    password = Axlebolt.RpcSupport.Protobuf.String.Parser.ParseFrom(request.Params[1].One).Value;
            }

            if (request.Params.Count >= 3 && request.Params[2].One != null)
                deviceId = Axlebolt.RpcSupport.Protobuf.String.Parser.ParseFrom(request.Params[2].One).Value;

            // Если deviceId пустой, пробуем взять из Verification
            if (string.IsNullOrEmpty(deviceId) && request.Params.Count >= 6 && request.Params[5].One != null)
            {
                try
                {
                    var verification = Verification.Parser.ParseFrom(request.Params[5].One);
                    deviceId = verification.DeviceId;
                }
                catch { }
            }

            Console.WriteLine($"🔐 Login='{login}', DeviceId='{deviceId}', IP={ipAddress}");

            // Валидация
            if (string.IsNullOrEmpty(login) || string.IsNullOrEmpty(password))
            {
                Console.WriteLine("❌ Login or password is empty");
                await SendError(client, request.Id, 1001, "Login or password is empty");
                return;
            }

            if (string.IsNullOrEmpty(deviceId))
            {
                deviceId = $"device_{Guid.NewGuid().ToString()[..8]}";
            }

            var playersCollection = _database.Database.GetCollection<Models.Player>("Players2");

            // Ищем игрока по логину
            Models.Player? player = await playersCollection.Find(p => p.Username == login).FirstOrDefaultAsync();

            if (player != null)
            {
                // Проверяем пароль
                var passwordHash = Converters.CalculateMD5(password);
                if (player.PasswordHash != passwordHash)
                {
                    Console.WriteLine($"❌ Invalid password for: {login}");
                    await SendError(client, request.Id, 1005, "Invalid password");
                    return;
                }

                if (player.IsBanned)
                {
                    Console.WriteLine($"❌ Player banned: {login}");
                    await SendError(client, request.Id, 1006, "Account banned");
                    return;
                }

                Console.WriteLine($"✅ Found player: {player.Name} (UID: {player.PlayerUid})");

                // Обновляем
                player.Token = Guid.NewGuid().ToString();
                player.LastLogin = DateTime.UtcNow;
                player.LastIpAddress = ipAddress;
                player.DeviceId = deviceId;
                player.LastHwid = deviceId;

                await playersCollection.ReplaceOneAsync(p => p.Id == player.Id, player);
            }
            else
            {
                // Создаём нового игрока
                Console.WriteLine($"🔐 Creating new player: {login}");

                var lastPlayer = await playersCollection.Find(_ => true).SortByDescending(p => p.OriginalUid).FirstOrDefaultAsync();
                int newUid = (lastPlayer?.OriginalUid ?? 10000) + 1;

                player = new Models.Player
                {
                    Id = ObjectId.GenerateNewId(),
                    PlayerUid = newUid.ToString(),
                    OriginalUid = newUid,
                    Name = login,
                    Username = login,
                    PasswordHash = Converters.CalculateMD5(password),
                    AuthToken = Converters.CalculateMD5(login + Converters.CalculateMD5(password)),
                    Token = Guid.NewGuid().ToString(),
                    DeviceId = deviceId,
                    LastHwid = deviceId,
                    LastIpAddress = ipAddress,
                    LastLogin = DateTime.UtcNow,
                    RegistrationDate = DateTime.UtcNow,
                    Level = 1,
                    Coins = 10000,
                    Gems = 1000,
                    Keys = 10,
                    IsBanned = false,
                    NoDetectRoot = true,
                    Inventory = new PlayerInventoryData { Items = new List<PlayerInventoryItem>() },
                    Stats = new PlayerStats { ArrayCust = new List<StatItem>() },
                    Social = new SocialInfo(),
                    OnlineStatus = Models.OnlineStatus.Online
                };

                await playersCollection.InsertOneAsync(player);
                Console.WriteLine($"✅ Created player: {player.Name} (UID: {newUid})");
            }

            // Создаём сессию
            _sessionManager.AddSession(new PlayerSession
            {
                PlayerObjectId = player.Id.ToString(),
                Token = player.Token,
                Hwid = deviceId,
                TimeInGame = player.TimeInGame,
                Client = client
            });

            // Отправляем токен - ТОЧНО КАК В KeyAuthService
            var resultToken = new Axlebolt.RpcSupport.Protobuf.String { Value = player.Token };
            var result = new BinaryValue
            {
                IsNull = false,
                One = resultToken.ToByteString()  // ToByteString() как в KeyAuthService!
            };

            await _handler.WriteProtoResponseAsync(client, request.Id, result, null);
            Console.WriteLine($"✅ PKConnect Auth success: {player.Name} (Token: {player.Token[..8]}...)");
        }
        catch (System.Exception ex)
        {
            Console.WriteLine($"❌ PKConnect Auth error: {ex.Message}");
            Console.WriteLine($"StackTrace: {ex.StackTrace}");
            await SendError(client, request.Id, 500, ex.Message);
        }
    }

    private async Task SendError(TcpClient client, string guid, int code, string message)
    {
        await _handler.WriteProtoResponseAsync(client, guid, null,
            new RpcException { Id = guid, Code = code, Property = new RpcExceptionProperty { Reason = message } });
    }
}
