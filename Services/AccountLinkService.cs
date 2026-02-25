using System.Net.Sockets;
using Axlebolt.RpcSupport.Protobuf;
using Axlebolt.Bolt.Protobuf2;
using StandRiseServer.Core;
using Google.Protobuf;

namespace StandRiseServer.Services;

public class AccountLinkService
{
    private readonly ProtobufHandler _handler;
    private readonly DatabaseService _database;
    private readonly SessionManager _sessionManager;

    public AccountLinkService(ProtobufHandler handler, DatabaseService database, SessionManager sessionManager)
    {
        _handler = handler;
        _database = database;
        _sessionManager = sessionManager;

        Console.WriteLine("🔗 Registering AccountLinkService handlers...");
        _handler.RegisterHandler("AccountLinkRemoteService", "createLinkTicket", CreateLinkTicketAsync);
        _handler.RegisterHandler("AccountLinkRemoteService", "getPlayerByTicket", GetPlayerByTicketAsync);
        _handler.RegisterHandler("AccountLinkRemoteService", "linkAccount", LinkAccountAsync);
        _handler.RegisterHandler("AccountLinkRemoteService", "unlinkAccount", UnlinkAccountAsync);
        Console.WriteLine("🔗 AccountLinkService handlers registered!");
    }

    private async Task CreateLinkTicketAsync(TcpClient client, RpcRequest request)
    {
        try
        {
            Console.WriteLine("🔗 CreateLinkTicket Request");

            var ticket = Guid.NewGuid().ToString("N").Substring(0, 8).ToUpper();
            var ticketStr = new Axlebolt.RpcSupport.Protobuf.String { Value = ticket };

            var result = new BinaryValue
            {
                IsNull = false,
                One = ByteString.CopyFrom(ticketStr.ToByteArray())
            };

            await _handler.WriteProtoResponseAsync(client, request.Id, result, null);
            Console.WriteLine($"🔗 Link ticket created: {ticket}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ CreateLinkTicket: {ex.Message}");
        }
    }

    private async Task GetPlayerByTicketAsync(TcpClient client, RpcRequest request)
    {
        try
        {
            Console.WriteLine("🔗 GetPlayerByTicket Request");

            // Возвращаем null если тикет не найден
            var result = new BinaryValue { IsNull = true };
            await _handler.WriteProtoResponseAsync(client, request.Id, result, null);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ GetPlayerByTicket: {ex.Message}");
        }
    }

    private async Task LinkAccountAsync(TcpClient client, RpcRequest request)
    {
        try
        {
            Console.WriteLine("🔗 LinkAccount Request");
            var result = new BinaryValue { IsNull = true };
            await _handler.WriteProtoResponseAsync(client, request.Id, result, null);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ LinkAccount: {ex.Message}");
        }
    }

    private async Task UnlinkAccountAsync(TcpClient client, RpcRequest request)
    {
        try
        {
            Console.WriteLine("🔗 UnlinkAccount Request");
            var result = new BinaryValue { IsNull = true };
            await _handler.WriteProtoResponseAsync(client, request.Id, result, null);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ UnlinkAccount: {ex.Message}");
        }
    }
}
