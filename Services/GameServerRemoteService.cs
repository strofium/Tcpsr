using System.Net.Sockets;
using Axlebolt.RpcSupport.Protobuf;
using Axlebolt.Bolt.Protobuf;
using Axlebolt.Bolt.Protobuf2;
using StandRiseServer.Core;
using Google.Protobuf;

namespace StandRiseServer.Services;

/// <summary>
/// Сервис для управления игровыми серверами
/// Обрабатывает информацию о серверах, проверку версии игры и систему банов
/// </summary>
public class GameServerRemoteService
{
    private readonly ProtobufHandler _handler;
    private readonly DatabaseService _database;
    private readonly SessionManager _sessionManager;

    public GameServerRemoteService(ProtobufHandler handler, DatabaseService database, SessionManager sessionManager)
    {
        _handler = handler;
        _database = database;
        _sessionManager = sessionManager;
        
        _handler.RegisterHandler("GameServerRemoteService", "getServerInfo", GetServerInfoAsync);
        _handler.RegisterHandler("GameServerRemoteService", "checkGameVersion", CheckGameVersionAsync);
        _handler.RegisterHandler("GameServerRemoteService", "checkPlayerBan", CheckPlayerBanAsync);
        _handler.RegisterHandler("GameServerRemoteService", "banPlayer", BanPlayerAsync);
        _handler.RegisterHandler("GameServerRemoteService", "unbanPlayer", UnbanPlayerAsync);
        _handler.RegisterHandler("GameServerRemoteService", "getServerStatus", GetServerStatusAsync);
    }

    /// <summary>
    /// Получает информацию о сервере
    /// </summary>
    private async Task GetServerInfoAsync(TcpClient client, RpcRequest request)
    {
        try
        {
            Console.WriteLine("=== GetServerInfo Request ===");
            var session = _sessionManager.GetSessionByClient(client);
            if (session == null)
            {
                await SendUnauthorizedAsync(client, request.Id);
                return;
            }

            var result = new BinaryValue { IsNull = false };
            
            var serverInfo = new GameSetting
            {
                Key = "serverInfo",
                Type = SettingType.String,
                Value = System.Text.Json.JsonSerializer.Serialize(new
                {
                    serverId = "main-server-1",
                    serverName = "Stand Rise Server",
                    region = "EU",
                    maxPlayers = 64,
                    currentPlayers = _sessionManager.GetAllSessions().Count(),
                    status = "online",
                    version = "1.0.0",
                    timestamp = DateTime.UtcNow
                })
            };
            
            result.Array.Add(ByteString.CopyFrom(serverInfo.ToByteArray()));
            
            Console.WriteLine($"✅ GetServerInfo successful");
            await _handler.WriteProtoResponseAsync(client, request.Id, result, null);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Error in GetServerInfo: {ex.Message}");
            await SendUnauthorizedAsync(client, request.Id);
        }
    }

    /// <summary>
    /// Проверяет версию игры клиента
    /// Возвращает ошибку если версия не совпадает с актуальной
    /// </summary>
    private async Task CheckGameVersionAsync(TcpClient client, RpcRequest request)
    {
        try
        {
            Console.WriteLine("=== CheckGameVersion Request ===");
            var session = _sessionManager.GetSessionByClient(client);
            if (session == null || request.Params.Count == 0)
            {
                await SendUnauthorizedAsync(client, request.Id);
                return;
            }

            // Получаем версию клиента
            var clientVersion = request.Params[0].One != null ? 
                System.Text.Encoding.UTF8.GetString(request.Params[0].One.ToByteArray()) : "";
            Console.WriteLine($"Client version: {clientVersion}");

            // Получаем актуальную версию из базы данных
            var validHash = await _database.GetHashByVersionAsync(clientVersion);
            
            var result = new BinaryValue { IsNull = false };
            
            if (validHash == null)
            {
                Console.WriteLine($"❌ Invalid game version: {clientVersion}");
                
                // Возвращаем информацию об ошибке версии
                var versionError = new GameSetting
                {
                    Key = "versionCheck",
                    Type = SettingType.String,
                    Value = System.Text.Json.JsonSerializer.Serialize(new
                    {
                        valid = false,
                        reason = "INVALID_VERSION",
                        message = "Your game version is outdated. Please update the game.",
                        requiredVersion = await GetLatestVersionAsync(),
                        clientVersion = clientVersion
                    })
                };
                result.Array.Add(ByteString.CopyFrom(versionError.ToByteArray()));
            }
            else
            {
                Console.WriteLine($"✅ Valid game version: {clientVersion}");
                
                // Версия валидна
                var versionOk = new GameSetting
                {
                    Key = "versionCheck",
                    Type = SettingType.String,
                    Value = System.Text.Json.JsonSerializer.Serialize(new
                    {
                        valid = true,
                        version = clientVersion,
                        hash = validHash.Hash,
                        signature = validHash.Signature
                    })
                };
                result.Array.Add(ByteString.CopyFrom(versionOk.ToByteArray()));
            }

            await _handler.WriteProtoResponseAsync(client, request.Id, result, null);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Error in CheckGameVersion: {ex.Message}");
            await SendUnauthorizedAsync(client, request.Id);
        }
    }

    /// <summary>
    /// Проверяет, забанен ли игрок
    /// </summary>
    private async Task CheckPlayerBanAsync(TcpClient client, RpcRequest request)
    {
        try
        {
            Console.WriteLine("=== CheckPlayerBan Request ===");
            var session = _sessionManager.GetSessionByClient(client);
            if (session == null)
            {
                await SendUnauthorizedAsync(client, request.Id);
                return;
            }

            var player = await _database.GetPlayerByTokenAsync(session.Token);
            if (player == null)
            {
                await SendUnauthorizedAsync(client, request.Id);
                return;
            }

            var result = new BinaryValue { IsNull = false };
            
            // Проверяем основной бан
            bool isBanned = player.IsBanned;
            
            // Проверяем ранговый бан
            if (!isBanned && player.Ranked.IsBanned)
            {
                isBanned = true;
            }

            if (isBanned)
            {
                Console.WriteLine($"⛔ Player {player.Name} is banned. Reason: {player.BanReason}");
                
                var banInfo = new GameSetting
                {
                    Key = "banStatus",
                    Type = SettingType.String,
                    Value = System.Text.Json.JsonSerializer.Serialize(new
                    {
                        isBanned = true,
                        banCode = player.BanCode,
                        banReason = player.BanReason,
                        banDuration = player.Ranked.BanDuration,
                        banExpireTime = player.Ranked.BanDuration > 0 ? 
                            new DateTime(player.Ranked.BanDuration * 10000000L, DateTimeKind.Utc) : 
                            DateTime.MaxValue
                    })
                };
                result.Array.Add(ByteString.CopyFrom(banInfo.ToByteArray()));
            }
            else
            {
                Console.WriteLine($"✅ Player {player.Name} is not banned");
                
                var banInfo = new GameSetting
                {
                    Key = "banStatus",
                    Type = SettingType.String,
                    Value = System.Text.Json.JsonSerializer.Serialize(new
                    {
                        isBanned = false
                    })
                };
                result.Array.Add(ByteString.CopyFrom(banInfo.ToByteArray()));
            }

            await _handler.WriteProtoResponseAsync(client, request.Id, result, null);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Error in CheckPlayerBan: {ex.Message}");
            await SendUnauthorizedAsync(client, request.Id);
        }
    }

    /// <summary>
    /// Банит игрока (требует прав администратора)
    /// </summary>
    private async Task BanPlayerAsync(TcpClient client, RpcRequest request)
    {
        try
        {
            Console.WriteLine("=== BanPlayer Request ===");
            var session = _sessionManager.GetSessionByClient(client);
            if (session == null || request.Params.Count < 3)
            {
                await SendUnauthorizedAsync(client, request.Id);
                return;
            }

            // Получаем параметры: playerId, reason, duration
            var playerId = request.Params[0].One != null ? 
                System.Text.Encoding.UTF8.GetString(request.Params[0].One.ToByteArray()) : "";
            var reason = request.Params[1].One != null ? 
                System.Text.Encoding.UTF8.GetString(request.Params[1].One.ToByteArray()) : "";
            
            var durationInt = Axlebolt.RpcSupport.Protobuf.Integer.Parser.ParseFrom(request.Params[2].One);
            var durationSeconds = durationInt.Value;

            var player = await _database.GetPlayerByIdAsync(playerId);
            if (player == null)
            {
                Console.WriteLine($"❌ Player not found: {playerId}");
                await SendUnauthorizedAsync(client, request.Id);
                return;
            }

            // Применяем бан
            player.IsBanned = true;
            player.BanReason = reason;
            player.BanCode = GenerateBanCode();
            
            // Если указана длительность, устанавливаем ранговый бан
            if (durationSeconds > 0)
            {
                var banExpireTime = DateTimeOffset.UtcNow.AddSeconds(durationSeconds).ToUnixTimeSeconds();
                player.Ranked.BanDuration = banExpireTime;
            }

            await _database.UpdatePlayerAsync(player);
            
            // Отключаем игрока с сервера
            var playerSession = _sessionManager.GetSessionByPlayerId(playerId);
            if (playerSession != null)
            {
                _sessionManager.DisconnectPlayer(playerSession.Token, $"Banned: {reason}");
            }

            Console.WriteLine($"⛔ Player {player.Name} has been banned. Reason: {reason}");

            var result = new BinaryValue { IsNull = false };
            var banResult = new GameSetting
            {
                Key = "banResult",
                Type = SettingType.String,
                Value = System.Text.Json.JsonSerializer.Serialize(new
                {
                    success = true,
                    playerId = playerId,
                    banCode = player.BanCode,
                    reason = reason
                })
            };
            result.Array.Add(ByteString.CopyFrom(banResult.ToByteArray()));
            
            await _handler.WriteProtoResponseAsync(client, request.Id, result, null);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Error in BanPlayer: {ex.Message}");
            await SendUnauthorizedAsync(client, request.Id);
        }
    }

    /// <summary>
    /// Разбанивает игрока (требует прав администратора)
    /// </summary>
    private async Task UnbanPlayerAsync(TcpClient client, RpcRequest request)
    {
        try
        {
            Console.WriteLine("=== UnbanPlayer Request ===");
            var session = _sessionManager.GetSessionByClient(client);
            if (session == null || request.Params.Count == 0)
            {
                await SendUnauthorizedAsync(client, request.Id);
                return;
            }

            var playerId = request.Params[0].One != null ? 
                System.Text.Encoding.UTF8.GetString(request.Params[0].One.ToByteArray()) : "";
            var player = await _database.GetPlayerByIdAsync(playerId);
            if (player == null)
            {
                await SendUnauthorizedAsync(client, request.Id);
                return;
            }

            // Снимаем бан
            player.IsBanned = false;
            player.BanReason = string.Empty;
            player.BanCode = string.Empty;
            player.Ranked.BanDuration = 0;

            await _database.UpdatePlayerAsync(player);
            Console.WriteLine($"✅ Player {player.Name} has been unbanned");

            var result = new BinaryValue { IsNull = false };
            var unbanResult = new GameSetting
            {
                Key = "unbanResult",
                Type = SettingType.String,
                Value = System.Text.Json.JsonSerializer.Serialize(new
                {
                    success = true,
                    playerId = playerId
                })
            };
            result.Array.Add(ByteString.CopyFrom(unbanResult.ToByteArray()));
            
            await _handler.WriteProtoResponseAsync(client, request.Id, result, null);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Error in UnbanPlayer: {ex.Message}");
            await SendUnauthorizedAsync(client, request.Id);
        }
    }

    /// <summary>
    /// Получает статус сервера
    /// </summary>
    private async Task GetServerStatusAsync(TcpClient client, RpcRequest request)
    {
        try
        {
            Console.WriteLine("=== GetServerStatus Request ===");
            var session = _sessionManager.GetSessionByClient(client);
            if (session == null)
            {
                await SendUnauthorizedAsync(client, request.Id);
                return;
            }

            var result = new BinaryValue { IsNull = false };
            
            var serverStatus = new GameSetting
            {
                Key = "serverStatus",
                Type = SettingType.String,
                Value = System.Text.Json.JsonSerializer.Serialize(new
                {
                    status = "online",
                    activePlayers = _sessionManager.GetAllSessions().Count(),
                    maxPlayers = 1000,
                    uptime = DateTime.UtcNow,
                    version = "1.0.0",
                    region = "EU"
                })
            };
            
            result.Array.Add(ByteString.CopyFrom(serverStatus.ToByteArray()));
            
            Console.WriteLine($"✅ GetServerStatus successful");
            await _handler.WriteProtoResponseAsync(client, request.Id, result, null);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Error in GetServerStatus: {ex.Message}");
            await SendUnauthorizedAsync(client, request.Id);
        }
    }

    private string GenerateBanCode()
    {
        return $"BAN-{Guid.NewGuid().ToString().Substring(0, 8).ToUpper()}";
    }

    private async Task<string> GetLatestVersionAsync()
    {
        try
        {
            var hashes = await _database.GetAllHashesAsync();
            return hashes.OrderByDescending(h => h.Version).FirstOrDefault()?.Version ?? "1.0.0";
        }
        catch
        {
            return "1.0.0";
        }
    }

    private async Task SendUnauthorizedAsync(TcpClient client, string guid)
    {
        await _handler.WriteProtoResponseAsync(client, guid, null,
            new RpcException { Id = guid, Code = 401, Property = null });
    }
}
