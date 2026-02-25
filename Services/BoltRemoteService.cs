using System.Net.Sockets;
using Axlebolt.RpcSupport.Protobuf;
using StandRiseServer.Core;
using StandRiseServer.Utils;
using Google.Protobuf;

namespace StandRiseServer.Services;

public class BoltRemoteService
{
    private readonly ProtobufHandler _handler;
    private readonly SessionManager _sessionManager;

    public BoltRemoteService(ProtobufHandler handler, SessionManager sessionManager)
    {
        _handler = handler;
        _sessionManager = sessionManager;
        
        _handler.RegisterHandler("BoltRemoteService", "systemTime", GetSystemTimeAsync);
        _handler.RegisterHandler("BoltRemoteService", "subscribe", SubscribeAsync);
        _handler.RegisterHandler("BoltRemoteService", "unsubscribe", UnsubscribeAsync);
    }

    private async Task GetSystemTimeAsync(TcpClient client, RpcRequest request)
    {
        try
        {
            Console.WriteLine("=== SystemTime Request ===");
            
            var currentTime = Converters.GetCurrentTimestamp();
            Console.WriteLine($"Returning system time: {currentTime}");
            
            var timeValue = new Axlebolt.RpcSupport.Protobuf.Integer { Value = (int)currentTime };
            using var stream = new MemoryStream();
            using var output = new Google.Protobuf.CodedOutputStream(stream);
            timeValue.WriteTo(output);
            output.Flush();
            
            var result = new BinaryValue { IsNull = false, One = Google.Protobuf.ByteString.CopyFrom(stream.ToArray()) };
            await _handler.WriteProtoResponseAsync(client, request.Id, result, null);
            
            Console.WriteLine("✅ SystemTime response sent");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error in SystemTime: {ex.Message}");
        }
    }

    private async Task SubscribeAsync(TcpClient client, RpcRequest request)
    {
        try
        {
            Console.WriteLine("=== Subscribe Request ===");
            string topic = "";
            if (request.Params.Count > 0)
            {
                var topicParam = Axlebolt.RpcSupport.Protobuf.String.Parser.ParseFrom(request.Params[0].One);
                topic = topicParam.Value;
                Console.WriteLine($"Subscribe to topic: {topic}");
                
                // Добавляем подписку
                _sessionManager.Subscribe(client, topic);
            }
            
            var result = new BinaryValue { IsNull = true };
            await _handler.WriteProtoResponseAsync(client, request.Id, result, null);
            Console.WriteLine("✅ Subscribe successful");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error in Subscribe: {ex.Message}");
        }
    }

    private async Task UnsubscribeAsync(TcpClient client, RpcRequest request)
    {
        try
        {
            Console.WriteLine("=== Unsubscribe Request ===");
            string topic = "";
            if (request.Params.Count > 0)
            {
                var topicParam = Axlebolt.RpcSupport.Protobuf.String.Parser.ParseFrom(request.Params[0].One);
                topic = topicParam.Value;
                Console.WriteLine($"Unsubscribe from topic: {topic}");
                
                // Удаляем подписку
                _sessionManager.Unsubscribe(client, topic);
            }
            
            var result = new BinaryValue { IsNull = true };
            await _handler.WriteProtoResponseAsync(client, request.Id, result, null);
            Console.WriteLine("✅ Unsubscribe successful");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error in Unsubscribe: {ex.Message}");
        }
    }
}