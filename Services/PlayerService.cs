using System.IO;
using System.Net.Sockets;
using Axlebolt.RpcSupport.Protobuf;
using Axlebolt.Bolt.Protobuf2;
using StandRiseServer.Core;
using StandRiseServer.Models;
using Google.Protobuf;

namespace StandRiseServer.Services;

public class PlayerService
{
    private readonly ProtobufHandler _handler;
    private readonly DatabaseService _database;
    private readonly SessionManager _sessionManager;

    public PlayerService(ProtobufHandler handler, DatabaseService database, SessionManager sessionManager)
    {
        _handler = handler;
        _database = database;
        _sessionManager = sessionManager;
        
        _handler.RegisterHandler("PlayerRemoteService", "getPlayer", GetPlayerAsync);
        _handler.RegisterHandler("PlayerRemoteService", "setPlayerName", SetPlayerNameAsync);
        _handler.RegisterHandler("PlayerRemoteService", "banMe", BanMeAsync);
        _handler.RegisterHandler("PlayerRemoteService", "setOnlineStatus", SetOnlineStatusAsync);
        _handler.RegisterHandler("PlayerRemoteService", "setAwayStatus", SetAwayStatusAsync);
        _handler.RegisterHandler("PlayerRemoteService", "setPlayerAvatar", SetPlayerAvatarAsync);
        _handler.RegisterHandler("PlayerRemoteService", "setPlayerFirebaseToken", SetPlayerFirebaseTokenAsync);
        _handler.RegisterHandler("PlayerRemoteService", "report", ReportAsync);
        _handler.RegisterHandler("PlayerRemoteService", "getReport", GetReportAsync);
    }

    private async Task GetPlayerAsync(TcpClient client, RpcRequest request)
    {
        try
        {
            Console.WriteLine("=== GetPlayer Request ===");
            var session = _sessionManager.GetSessionByClient(client);
            Console.WriteLine($"Session found by client: {session != null}");
            
            // Если сессия не найдена по клиенту, попробуем найти любую активную сессию
            if (session == null)
            {
                Console.WriteLine("Trying to find any active session...");
                session = _sessionManager.GetAllSessions().FirstOrDefault();
                if (session != null)
                {
                    Console.WriteLine($"Using active session: {session.Token}");
                    session.Client = client; // Обновляем клиента
                }
            }
            
            if (session == null)
            {
                Console.WriteLine("❌ No session found for client in GetPlayer");
                await SendUnauthorizedAsync(client, request.Id);
                return;
            }

            Console.WriteLine($"Session token: {session.Token}");
            var player = await _database.GetPlayerByTokenAsync(session.Token);
            Console.WriteLine($"Player found: {player != null}");
            if (player == null)
            {
                Console.WriteLine("❌ No player found for token in GetPlayer");
                await SendUnauthorizedAsync(client, request.Id);
                return;
            }

            Console.WriteLine($"✅ GetPlayer successful for: {player.Name}");
            Console.WriteLine($"   - Player.Id (MongoDB): {player.Id}");
            Console.WriteLine($"   - Player.PlayerUid: {player.PlayerUid}");
            Console.WriteLine($"   - Sending Id to client: {player.PlayerUid}");

            var playerProto = new Axlebolt.Bolt.Protobuf.Player
            {
                Id = player.PlayerUid,  // Используем PlayerUid как основной ID
                Uid = player.PlayerUid,
                Name = player.Name,
                AvatarId = player.AvatarId,
                TimeInGame = player.TimeInGame,
                PlayerStatus = null,
                LogoutDate = 0,
                RegistrationDate = new DateTimeOffset(player.RegistrationDate).ToUnixTimeSeconds()
            };

            var result = new BinaryValue 
            { 
                IsNull = false, 
                One = ByteString.CopyFrom(playerProto.ToByteArray()) 
            };
            
            await _handler.WriteProtoResponseAsync(client, request.Id, result, null);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error in GetPlayer: {ex.Message}");
        }
    }

    private async Task SetPlayerNameAsync(TcpClient client, RpcRequest request)
    {
        try
        {
            Console.WriteLine("=== SetPlayerName Request ===");
            
            var session = _sessionManager.GetSessionByClient(client);
            
            // Если сессия не найдена по клиенту, попробуем найти любую активную сессию
            if (session == null)
            {
                Console.WriteLine("Session not found by client, trying to find any active session...");
                session = _sessionManager.GetAllSessions().FirstOrDefault();
                if (session != null)
                {
                    Console.WriteLine($"Using active session: {session.Token}");
                    session.Client = client;
                }
            }
            
            if (session == null || request.Params.Count == 0)
            {
                Console.WriteLine($"❌ SetPlayerName failed: session={session != null}, params={request.Params.Count}");
                await SendUnauthorizedAsync(client, request.Id);
                return;
            }

            var nameString = Axlebolt.RpcSupport.Protobuf.String.Parser.ParseFrom(request.Params[0].One);
            var newName = nameString.Value;
            Console.WriteLine($"New name requested: {newName}");

            var player = await _database.GetPlayerByTokenAsync(session.Token);
            if (player == null)
            {
                Console.WriteLine("❌ Player not found by token");
                await SendUnauthorizedAsync(client, request.Id);
                return;
            }

            Console.WriteLine($"Updating player name from '{player.Name}' to '{newName}'");
            player.Name = newName;
            await _database.UpdatePlayerAsync(player);

            var result = new BinaryValue { IsNull = true };
            await _handler.WriteProtoResponseAsync(client, request.Id, result, null);
            
            Console.WriteLine($"✅ Player renamed to: {newName}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Error in SetPlayerName: {ex.Message}");
            Console.WriteLine($"Stack trace: {ex.StackTrace}");
        }
    }

    private async Task BanMeAsync(TcpClient client, RpcRequest request)
    {
        try
        {
            var session = _sessionManager.GetSessionByClient(client);
            if (session == null || request.Params.Count < 2)
            {
                await SendUnauthorizedAsync(client, request.Id);
                return;
            }

            var banCode = Integer.Parser.ParseFrom(request.Params[0].One);
            var banReason = Axlebolt.RpcSupport.Protobuf.String.Parser.ParseFrom(request.Params[1].One);

            var player = await _database.GetPlayerByTokenAsync(session.Token);
            if (player == null)
            {
                await SendUnauthorizedAsync(client, request.Id);
                return;
            }

            player.IsBanned = true;
            player.BanCode = banCode.Value.ToString();
            player.BanReason = banReason.Value;
            await _database.UpdatePlayerAsync(player);

            var result = new BinaryValue { IsNull = true };
            await _handler.WriteProtoResponseAsync(client, request.Id, result, null);
            
            Console.WriteLine($"Player banned: {player.Name}, Reason: {banReason.Value}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error in BanMe: {ex.Message}");
        }
    }

    private async Task SetOnlineStatusAsync(TcpClient client, RpcRequest request)
    {
        var result = new BinaryValue { IsNull = true };
        await _handler.WriteProtoResponseAsync(client, request.Id, result, null);
    }

    private async Task SetAwayStatusAsync(TcpClient client, RpcRequest request)
    {
        var result = new BinaryValue { IsNull = true };
        await _handler.WriteProtoResponseAsync(client, request.Id, result, null);
    }

    private async Task SetPlayerAvatarAsync(TcpClient client, RpcRequest request)
    {
        try
        {
            Console.WriteLine("=== SetPlayerAvatar Request ===");
            
            var session = _sessionManager.GetSessionByClient(client);
            
            // Если сессия не найдена по клиенту, попробуем найти любую активную сессию
            if (session == null)
            {
                Console.WriteLine("Session not found by client, trying to find any active session...");
                session = _sessionManager.GetAllSessions().FirstOrDefault();
                if (session != null)
                {
                    Console.WriteLine($"Using active session: {session.Token}");
                    session.Client = client;
                }
            }
            
            if (session == null)
            {
                Console.WriteLine("❌ SetPlayerAvatar failed: no session found");
                await SendUnauthorizedAsync(client, request.Id);
                return;
            }

            var player = await _database.GetPlayerByTokenAsync(session.Token);
            if (player == null)
            {
                Console.WriteLine("❌ Player not found by token");
                await SendUnauthorizedAsync(client, request.Id);
                return;
            }

            // Парсим аватар из параметров
            if (request.Params.Count > 0)
            {
                var avatarBytes = request.Params[0].One.ToByteArray();
                Console.WriteLine($"Avatar bytes received: {avatarBytes.Length} bytes");
                
                // Генерируем уникальный ID для аватара
                var avatarId = $"avatar_{player.PlayerUid}_{DateTime.UtcNow.Ticks}";
                
                // Сохраняем аватар как base64 строку
                player.Avatar = Convert.ToBase64String(avatarBytes);
                player.AvatarId = avatarId;
                
                await _database.UpdatePlayerAsync(player);
                Console.WriteLine($"✅ Avatar saved for player {player.Name}, AvatarId: {avatarId}");
                
                // Возвращаем ID аватара
                var avatarIdString = new Axlebolt.RpcSupport.Protobuf.String { Value = avatarId };
                using var stream = new MemoryStream();
                avatarIdString.WriteTo(stream);
                
                var result = new BinaryValue 
                { 
                    IsNull = false, 
                    One = ByteString.CopyFrom(stream.ToArray()) 
                };
                await _handler.WriteProtoResponseAsync(client, request.Id, result, null);
            }
            else
            {
                Console.WriteLine("No avatar data in request");
                var result = new BinaryValue { IsNull = true };
                await _handler.WriteProtoResponseAsync(client, request.Id, result, null);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Error in SetPlayerAvatar: {ex.Message}");
            Console.WriteLine($"Stack trace: {ex.StackTrace}");
        }
    }

    private async Task SetPlayerFirebaseTokenAsync(TcpClient client, RpcRequest request)
    {
        try
        {
            Console.WriteLine("=== SetPlayerFirebaseToken Request ===");
            var result = new BinaryValue { IsNull = true };
            await _handler.WriteProtoResponseAsync(client, request.Id, result, null);
            Console.WriteLine("✅ SetPlayerFirebaseToken successful");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error in SetPlayerFirebaseToken: {ex.Message}");
        }
    }

    private async Task ReportAsync(TcpClient client, RpcRequest request)
    {
        try
        {
            Console.WriteLine("=== Report Request ===");
            var result = new BinaryValue { IsNull = false, One = Google.Protobuf.ByteString.CopyFrom(new byte[] { 0 }) };
            await _handler.WriteProtoResponseAsync(client, request.Id, result, null);
            Console.WriteLine("✅ Report successful");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error in Report: {ex.Message}");
        }
    }

    private async Task GetReportAsync(TcpClient client, RpcRequest request)
    {
        try
        {
            Console.WriteLine("=== GetReport Request ===");
            var result = new BinaryValue { IsNull = false, One = Google.Protobuf.ByteString.CopyFrom(new byte[] { 0 }) };
            await _handler.WriteProtoResponseAsync(client, request.Id, result, null);
            Console.WriteLine("✅ GetReport successful");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error in GetReport: {ex.Message}");
        }
    }

    private async Task SendUnauthorizedAsync(TcpClient client, string guid)
    {
        await _handler.WriteProtoResponseAsync(client, guid, null,
            new RpcException { Id = guid, Code = 401, Property = null });
    }
}
