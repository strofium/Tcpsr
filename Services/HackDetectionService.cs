using System.Net.Sockets;
using Axlebolt.RpcSupport.Protobuf;
using StandRiseServer.Core;
using Google.Protobuf;

namespace StandRiseServer.Services;

public class HackDetectionService
{
    private readonly ProtobufHandler _handler;

    public HackDetectionService(ProtobufHandler handler)
    {
        _handler = handler;

        Console.WriteLine("🛡️ Registering HackDetectionService handlers...");
        _handler.RegisterHandler("HackDetectionRemoteService", "hackDetection", HackDetectionAsync);
        _handler.RegisterHandler("HackDetectionRemoteService", "systemTime", SystemTimeAsync);
        Console.WriteLine("🛡️ HackDetectionService handlers registered!");
    }

    private async Task HackDetectionAsync(TcpClient client, RpcRequest request)
    {
        try
        {
            Console.WriteLine("🛡️ HackDetection Request");
            // void метод
            var result = new BinaryValue { IsNull = true };
            await _handler.WriteProtoResponseAsync(client, request.Id, result, null);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ HackDetection: {ex.Message}");
        }
    }

    private async Task SystemTimeAsync(TcpClient client, RpcRequest request)
    {
        try
        {
            Console.WriteLine("🕐 SystemTime Request");
            
            var timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            var longValue = new Axlebolt.RpcSupport.Protobuf.Long { Value = timestamp };
            
            var result = new BinaryValue
            {
                IsNull = false,
                One = ByteString.CopyFrom(longValue.ToByteArray())
            };
            
            await _handler.WriteProtoResponseAsync(client, request.Id, result, null);
            Console.WriteLine($"🕐 SystemTime: {timestamp}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ SystemTime: {ex.Message}");
        }
    }
}
