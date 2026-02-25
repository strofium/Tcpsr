using System.Net.Sockets;
using Axlebolt.RpcSupport.Protobuf;
using Axlebolt.Bolt.Protobuf2;
using StandRiseServer.Core;
using Google.Protobuf;

namespace StandRiseServer.Services;

public class GameServerService
{
    private readonly ProtobufHandler _handler;
    private readonly DatabaseService _database;
    private readonly SessionManager _sessionManager;

    public GameServerService(ProtobufHandler handler, DatabaseService database, SessionManager sessionManager)
    {
        _handler = handler;
        _database = database;
        _sessionManager = sessionManager;

        Console.WriteLine("🎮 Registering GameServerService handlers...");
        _handler.RegisterHandler("GameServerRemoteService", "serverHandshake", ServerHandshakeAsync);
        _handler.RegisterHandler("GameServerRemoteService", "logout", LogoutAsync);
        _handler.RegisterHandler("GameServerPlayerRemoteService", "setPhotonGame", SetPhotonGameAsync);
        _handler.RegisterHandler("GameServerStatsRemoteService", "getStats", GetGameServerStatsAsync);
        _handler.RegisterHandler("GameServerStatsRemoteService", "storeStats", StoreGameServerStatsAsync);
        _handler.RegisterHandler("GameServerStatsRemoteService", "getPlayersStats", GetPlayersStatsAsync);
        Console.WriteLine("🎮 GameServerService handlers registered!");
    }

    private async Task ServerHandshakeAsync(TcpClient client, RpcRequest request)
    {
        try
        {
            Console.WriteLine("🎮 ServerHandshake Request");
            var result = new BinaryValue { IsNull = true };
            await _handler.WriteProtoResponseAsync(client, request.Id, result, null);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ ServerHandshake: {ex.Message}");
        }
    }

    private async Task LogoutAsync(TcpClient client, RpcRequest request)
    {
        try
        {
            Console.WriteLine("🎮 Logout Request");
            var result = new BinaryValue { IsNull = true };
            await _handler.WriteProtoResponseAsync(client, request.Id, result, null);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Logout: {ex.Message}");
        }
    }

    private async Task SetPhotonGameAsync(TcpClient client, RpcRequest request)
    {
        try
        {
            Console.WriteLine("🎮 SetPhotonGame Request");
            var result = new BinaryValue { IsNull = true };
            await _handler.WriteProtoResponseAsync(client, request.Id, result, null);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ SetPhotonGame: {ex.Message}");
        }
    }

    private async Task GetGameServerStatsAsync(TcpClient client, RpcRequest request)
    {
        try
        {
            Console.WriteLine("🎮 GetGameServerStats Request");
            
            var stats = new Stats();
            var result = new BinaryValue
            {
                IsNull = false,
                One = ByteString.CopyFrom(stats.ToByteArray())
            };
            
            await _handler.WriteProtoResponseAsync(client, request.Id, result, null);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ GetGameServerStats: {ex.Message}");
        }
    }

    private async Task StoreGameServerStatsAsync(TcpClient client, RpcRequest request)
    {
        try
        {
            Console.WriteLine("🎮 StoreGameServerStats Request");
            var result = new BinaryValue { IsNull = true };
            await _handler.WriteProtoResponseAsync(client, request.Id, result, null);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ StoreGameServerStats: {ex.Message}");
        }
    }

    private async Task GetPlayersStatsAsync(TcpClient client, RpcRequest request)
    {
        try
        {
            Console.WriteLine("🎮 GetPlayersStats Request");
            var result = new BinaryValue { IsNull = false };
            await _handler.WriteProtoResponseAsync(client, request.Id, result, null);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ GetPlayersStats: {ex.Message}");
        }
    }
}
