using Google.Protobuf;
using Axlebolt.RpcSupport.Protobuf;
using StandRiseServer.Utils;
using System.Net.Sockets;

namespace StandRiseServer.Core;

public class ProtobufHandler
{
    private readonly SessionManager _sessionManager;
    private readonly DatabaseService _database;
    private readonly Dictionary<string, Func<TcpClient, RpcRequest, Task>> _serviceHandlers = new();

    public ProtobufHandler(SessionManager sessionManager, DatabaseService database)
    {
        _sessionManager = sessionManager;
        _database = database;
        RegisterServiceHandlers();
    }

    private void RegisterServiceHandlers()
    {
        // Services will be registered here by individual service classes
    }

    public async Task HandleRequestAsync(TcpClient client, byte[] data)
    {
        try
        {
            // Read length prefix (4 bytes, big endian)
            if (data.Length < 4) return;
            
            var lengthBytes = data.Take(4).ToArray();
            var length = Converters.BytesToInt32BigEndian(lengthBytes);

            // Special case: ping/heartbeat
            if (length == 1)
            {
                await SendHeartbeatResponseAsync(client);
                return;
            }

            // Validate message length
            if (data.Length < 4 + length) return;

            // Read protobuf message
            var messageBytes = data.Skip(4).Take(length).ToArray();
            var request = RpcRequest.Parser.ParseFrom(messageBytes);
            
            Console.WriteLine($"🔧 {request.ServiceName}.{request.MethodName}");

            // Route to appropriate handler
            var serviceKey = $"{request.ServiceName}.{request.MethodName}";
            
            if (_serviceHandlers.TryGetValue(serviceKey, out var handler))
            {
                await handler(client, request);
            }
            else
            {
                Console.WriteLine($"❌ Unknown service: {serviceKey}");
                await WriteErrorResponseAsync(client, request.Id, 404, "Service not found");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Error: {ex.Message}");
        }
    }

    public void RegisterHandler(string serviceName, string methodName, Func<TcpClient, RpcRequest, Task> handler)
    {
        var key = $"{serviceName}.{methodName}";
        _serviceHandlers[key] = handler;
        Console.WriteLine($"🔧 Registered handler: {key}");
    }

    public async Task WriteProtoResponseAsync(TcpClient client, string guid, BinaryValue? result, RpcException? exception)
    {
        try
        {
            var response = new ResponseMessage
            {
                RpcResponse = new RpcResponse
                {
                    Id = guid,
                    Exception = exception,
                    Return = result ?? new BinaryValue { IsNull = true }
                },
                EventResponse = null
            };

            var messageBytes = response.ToByteArray();
            var lengthBytes = BitConverter.GetBytes(messageBytes.Length);
            
            // Reverse to big endian
            if (BitConverter.IsLittleEndian)
                Array.Reverse(lengthBytes);

            var fullMessage = lengthBytes.Concat(messageBytes).ToArray();
            
            var stream = client.GetStream();
            await stream.WriteAsync(fullMessage);
            await stream.FlushAsync();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error writing response: {ex.Message}");
        }
    }

    /// <summary>
    /// Отправляет событие клиенту (для pub/sub системы)
    /// </summary>
    public async Task SendEventAsync(TcpClient client, string listenerName, string eventName, params ByteString[] eventParams)
    {
        try
        {
            var eventResponse = new EventResponse
            {
                ListenerName = listenerName,
                EventName = eventName
            };
            
            foreach (var param in eventParams)
            {
                eventResponse.Params.Add(new BinaryValue { IsNull = false, One = param });
            }

            var response = new ResponseMessage
            {
                RpcResponse = null,
                EventResponse = eventResponse
            };

            var messageBytes = response.ToByteArray();
            var lengthBytes = BitConverter.GetBytes(messageBytes.Length);
            
            if (BitConverter.IsLittleEndian)
                Array.Reverse(lengthBytes);

            var fullMessage = lengthBytes.Concat(messageBytes).ToArray();
            
            Console.WriteLine($"📤 Sending event: {eventName}, size={fullMessage.Length} bytes, params={eventParams.Length}");
            Console.WriteLine($"📤 Event hex (first 50): {BitConverter.ToString(fullMessage.Take(50).ToArray())}");
            
            var stream = client.GetStream();
            await stream.WriteAsync(fullMessage);
            await stream.FlushAsync();
            
            Console.WriteLine($"📤 Event sent to client: {eventName}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Error sending event to client: {ex.Message}");
        }
    }

    /// <summary>
    /// Отправляет событие всем подписчикам топика
    /// </summary>
    public async Task BroadcastEventAsync(string topic, string listenerName, string eventName, params ByteString[] eventParams)
    {
        try
        {
            var subscribers = _sessionManager.GetSubscribers(topic);
            var subscribersList = subscribers.ToList();
            
            if (subscribersList.Count == 0)
            {
                Console.WriteLine($"📤 No subscribers for topic: {topic}");
                return;
            }

            Console.WriteLine($"📤 Broadcasting event {eventName} to {subscribersList.Count} subscribers of topic: {topic}");
            
            var tasks = subscribersList.Select(client => SendEventAsync(client, listenerName, eventName, eventParams));
            await Task.WhenAll(tasks);
            
            Console.WriteLine($"📤 Event {eventName} broadcasted to {subscribersList.Count} clients");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error broadcasting event: {ex.Message}");
        }
    }

    private async Task SendHeartbeatResponseAsync(TcpClient client)
    {
        try
        {
            var lengthBytes = BitConverter.GetBytes(1);
            if (BitConverter.IsLittleEndian)
                Array.Reverse(lengthBytes);
            
            var response = lengthBytes.Concat(new byte[] { 0x01 }).ToArray();
            
            var stream = client.GetStream();
            await stream.WriteAsync(response);
            await stream.FlushAsync();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error sending heartbeat: {ex.Message}");
        }
    }

    private async Task WriteErrorResponseAsync(TcpClient client, string guid, int code, string message)
    {
        var exception = new RpcException
        {
            Id = guid,
            Code = code,
            Property = null
        };

        await WriteProtoResponseAsync(client, guid, null, exception);
    }
}
