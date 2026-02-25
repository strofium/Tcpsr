using System.Net.Sockets;
using Axlebolt.RpcSupport.Protobuf;
using StandRiseServer.Core;

namespace StandRiseServer.Services;

public class AnalyticsRemoteService
{
    private readonly ProtobufHandler _handler;
    private readonly SessionManager _sessionManager;

    public AnalyticsRemoteService(ProtobufHandler handler, SessionManager sessionManager)
    {
        _handler = handler;
        _sessionManager = sessionManager;
        
        _handler.RegisterHandler("AnalyticsRemoteService", "event", HandleEventAsync);
    }

    private async Task HandleEventAsync(TcpClient client, RpcRequest request)
    {
        try
        {
            Console.WriteLine("📊 AnalyticsRemoteService.event");
            
            var session = _sessionManager.GetSessionByClient(client);
            if (session == null)
            {
                Console.WriteLine("❌ No session found for Analytics");
                await SendUnauthorizedAsync(client, request.Id);
                return;
            }

            // Parse the analytics events from request
            // The request contains UserEvent[] array
            // For now, we just log that we received analytics events
            Console.WriteLine($"✅ Analytics events received from: {session.Token.Substring(0, 8)}...");
            
            // Send empty response (analytics doesn't need to return data)
            await _handler.WriteProtoResponseAsync(client, request.Id, new BinaryValue { IsNull = true }, null);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Error in HandleEvent: {ex.Message}");
            await _handler.WriteProtoResponseAsync(client, request.Id, null,
                new RpcException { Id = request.Id, Code = 500, Property = null });
        }
    }

    private async Task SendUnauthorizedAsync(TcpClient client, string guid)
    {
        await _handler.WriteProtoResponseAsync(client, guid, null,
            new RpcException { Id = guid, Code = 401, Property = null });
    }
}
