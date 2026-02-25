using System.Net.Sockets;
using Axlebolt.RpcSupport.Protobuf;
using Axlebolt.Bolt.Protobuf2;
using StandRiseServer.Core;
using Google.Protobuf;
using MongoDB.Driver;

namespace StandRiseServer.Services;

public class AvatarService
{
    private readonly ProtobufHandler _handler;
    private readonly DatabaseService _database;
    private readonly SessionManager _sessionManager;

    public AvatarService(ProtobufHandler handler, DatabaseService database, SessionManager sessionManager)
    {
        _handler = handler;
        _database = database;
        _sessionManager = sessionManager;

        Console.WriteLine("🖼️ Registering AvatarService handlers...");
        _handler.RegisterHandler("AvatarRemoteService", "getAvatars", GetAvatarsAsync);
        Console.WriteLine("🖼️ AvatarService handlers registered!");
    }

    private async Task GetAvatarsAsync(TcpClient client, RpcRequest request)
    {
        try
        {
            Console.WriteLine("🖼️ GetAvatars Request");

            string[] avatarIds = Array.Empty<string>();
            if (request.Params.Count > 0 && request.Params[0].Array.Count > 0)
            {
                avatarIds = request.Params[0].Array
                    .Select(b => Axlebolt.RpcSupport.Protobuf.String.Parser.ParseFrom(b).Value)
                    .ToArray();
            }

            var result = new BinaryValue { IsNull = false };

            // Возвращаем пустые аватары для каждого запрошенного ID
            foreach (var avatarId in avatarIds)
            {
                var avatar = new Axlebolt.Bolt.Protobuf2.AvatarBinary
                {
                    Id = avatarId,
                    Data = ByteString.Empty
                };
                result.Array.Add(ByteString.CopyFrom(avatar.ToByteArray()));
            }

            await _handler.WriteProtoResponseAsync(client, request.Id, result, null);
            Console.WriteLine($"🖼️ Returned {avatarIds.Length} avatars");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ GetAvatars: {ex.Message}");
        }
    }
}
