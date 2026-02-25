using System.Net.Sockets;
using Axlebolt.RpcSupport.Protobuf;
using Axlebolt.Bolt.Protobuf;
using StandRiseServer.Core;
using StandRiseServer.Utils;

namespace StandRiseServer.Services;

public class HandshakeService
{
    private readonly ProtobufHandler _handler;
    private readonly DatabaseService _database;
    private readonly SessionManager _sessionManager;

    public HandshakeService(ProtobufHandler handler, DatabaseService database, SessionManager sessionManager)
    {
        _handler = handler;
        _database = database;
        _sessionManager = sessionManager;
        
        _handler.RegisterHandler("HandshakeRemoteService", "protoHandshake", HandleHandshakeAsync);
    }

    private async Task HandleHandshakeAsync(TcpClient client, RpcRequest request)
    {
        try
        {
            Console.WriteLine("=== Handshake Request ===");
            if (request.Params.Count == 0) 
            {
                Console.WriteLine("No parameters in handshake request");
                return;
            }

            var handshake = Handshake.Parser.ParseFrom(request.Params[0].One);
            var token = handshake.Ticket;
            Console.WriteLine($"Handshake token: '{token}'");

            // If token is empty, try to find existing session by client
            if (string.IsNullOrEmpty(token))
            {
                Console.WriteLine("Token is empty, looking for existing session by client...");
                var existingSession = _sessionManager.GetSessionByClient(client);
                if (existingSession != null)
                {
                    Console.WriteLine($"Found existing session by client: {existingSession.Token}");
                    
                    // Обновляем токен в handshake для дальнейшего использования
                    token = existingSession.Token;
                    
                    // Send success response
                    var result = new BinaryValue { IsNull = true };
                    await _handler.WriteProtoResponseAsync(client, request.Id, result, null);
                    Console.WriteLine("✅ Handshake successful (found session by client)");
                    return;
                }
                else
                {
                    Console.WriteLine("❌ No existing session found for this client");
                    Console.WriteLine("This might be normal if Unity uses different connections");
                    
                    // Попробуем найти последнюю созданную сессию
                    // REMOVED INSECURE FALLBACK
                    // Do not allow logging in as 'latest session'
                    /*
                     var latestSession = _sessionManager.GetAllSessions().OrderByDescending(s => s.TimeInGame).FirstOrDefault();
                     if (latestSession != null)
                     {
                         // ... logic removed ...
                     }
                    */
                    
                    Console.WriteLine("❌ No session found. Connection rejected.");
                    
                    await _handler.WriteProtoResponseAsync(client, request.Id, null,
                        new RpcException { Id = request.Id, Code = 2003, Property = null });
                    return;
                }
            }

            // Check if session already exists
            var existingSessionByToken = _sessionManager.GetSessionByToken(token);
            if (existingSessionByToken != null)
            {
                Console.WriteLine($"Session already exists for token: {token}");
                // Update the client reference in existing session
                existingSessionByToken.Client = client;
                
                // Send success response
                var result = new BinaryValue { IsNull = true };
                await _handler.WriteProtoResponseAsync(client, request.Id, result, null);
                Console.WriteLine("✅ Handshake successful (existing session)");
                return;
            }

            // Check if player with this token exists
            Console.WriteLine($"Looking for player with token: {token}");
            var player = await _database.GetPlayerByTokenAsync(token);
            
            if (player == null)
            {
                Console.WriteLine($"❌ Player not found for token: {token}");
                await _handler.WriteProtoResponseAsync(client, request.Id, null,
                    new RpcException { Id = request.Id, Code = 2003, Property = null });
                return;
            }

            Console.WriteLine($"Found player: {player.Name} ({player.PlayerUid})");

            // Create new session
            var session = new Models.PlayerSession
            {
                PlayerObjectId = player.Id.ToString(),
                Token = token,
                Hwid = player.LastHwid,
                TimeInGame = player.TimeInGame,
                Client = client
            };

            _sessionManager.AddSession(session);
            Console.WriteLine($"New session created for handshake");

            // Send success response
            var result2 = new BinaryValue { IsNull = true };
            await _handler.WriteProtoResponseAsync(client, request.Id, result2, null);
            
            Console.WriteLine($"✅ Handshake successful: {player.Name} ({player.PlayerUid})");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Error in Handshake: {ex.Message}");
            Console.WriteLine($"Stack trace: {ex.StackTrace}");
            await _handler.WriteProtoResponseAsync(client, request.Id, null,
                new RpcException { Id = request.Id, Code = 5000, Property = null });
        }
    }
}
