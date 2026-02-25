using System.Net.Sockets;
using Axlebolt.RpcSupport.Protobuf;
using Axlebolt.Bolt.Protobuf;
using StandRiseServer.Core;
using Google.Protobuf;

namespace StandRiseServer.Services;

public class GroupService
{
    private readonly ProtobufHandler _handler;
    private readonly DatabaseService _database;
    private readonly SessionManager _sessionManager;

    public GroupService(ProtobufHandler handler, DatabaseService database, SessionManager sessionManager)
    {
        _handler = handler;
        _database = database;
        _sessionManager = sessionManager;

        Console.WriteLine("👥 Registering GroupService handlers...");
        _handler.RegisterHandler("GroupRemoteService", "createGroup", CreateGroupAsync);
        _handler.RegisterHandler("GroupRemoteService", "joinGroup", JoinGroupAsync);
        _handler.RegisterHandler("GroupRemoteService", "leaveGroup", LeaveGroupAsync);
        Console.WriteLine("👥 GroupService handlers registered!");
    }

    private async Task CreateGroupAsync(TcpClient client, RpcRequest request)
    {
        try
        {
            Console.WriteLine("👥 CreateGroup Request");

            var groupId = Guid.NewGuid().ToString();
            var group = new Group
            {
                Id = groupId
            };

            var result = new BinaryValue
            {
                IsNull = false,
                One = ByteString.CopyFrom(group.ToByteArray())
            };

            await _handler.WriteProtoResponseAsync(client, request.Id, result, null);
            Console.WriteLine($"👥 Group created: {groupId}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ CreateGroup: {ex.Message}");
        }
    }

    private async Task JoinGroupAsync(TcpClient client, RpcRequest request)
    {
        try
        {
            Console.WriteLine("👥 JoinGroup Request");

            string groupId = "";
            if (request.Params.Count > 0 && request.Params[0].One != null)
            {
                var str = Axlebolt.RpcSupport.Protobuf.String.Parser.ParseFrom(request.Params[0].One);
                groupId = str.Value;
            }

            var group = new Group
            {
                Id = groupId
            };

            var result = new BinaryValue
            {
                IsNull = false,
                One = ByteString.CopyFrom(group.ToByteArray())
            };

            await _handler.WriteProtoResponseAsync(client, request.Id, result, null);
            Console.WriteLine($"👥 Joined group: {groupId}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ JoinGroup: {ex.Message}");
        }
    }

    private async Task LeaveGroupAsync(TcpClient client, RpcRequest request)
    {
        try
        {
            Console.WriteLine("👥 LeaveGroup Request");
            var result = new BinaryValue { IsNull = true };
            await _handler.WriteProtoResponseAsync(client, request.Id, result, null);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ LeaveGroup: {ex.Message}");
        }
    }
}
