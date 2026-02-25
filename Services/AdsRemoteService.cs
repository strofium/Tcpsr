using System.Net.Sockets;
using Axlebolt.RpcSupport.Protobuf;
using StandRiseServer.Core;
using Google.Protobuf;

namespace StandRiseServer.Services;

public class AdsRemoteService
{
    private readonly ProtobufHandler _handler;
    private readonly DatabaseService _database;
    private readonly SessionManager _sessionManager;

    public AdsRemoteService(ProtobufHandler handler, DatabaseService database, SessionManager sessionManager)
    {
        _handler = handler;
        _database = database;
        _sessionManager = sessionManager;

        Console.WriteLine("📺 Registering AdsRemoteService handlers...");
        _handler.RegisterHandler("AdsRemoteService", "giveAdReward", GiveAdRewardAsync);
        Console.WriteLine("📺 AdsRemoteService handlers registered!");
    }

    private async Task GiveAdRewardAsync(TcpClient client, RpcRequest request)
    {
        try
        {
            Console.WriteLine("📺 GiveAdReward Request");

            var session = _sessionManager.GetSessionByClient(client);
            if (session == null)
            {
                session = _sessionManager.GetAllSessions().FirstOrDefault();
                if (session != null) session.Client = client;
            }

            string conditions = "";
            if (request.Params.Count > 0 && request.Params[0].One != null)
            {
                var condStr = Axlebolt.RpcSupport.Protobuf.String.Parser.ParseFrom(request.Params[0].One);
                conditions = condStr.Value;
            }

            Console.WriteLine($"📺 Ad reward conditions: {conditions}");

            // Даем награду игроку
            if (session != null)
            {
                var player = await _database.GetPlayerByTokenAsync(session.Token);
                if (player != null)
                {
                    // Награда за просмотр рекламы - 100 монет
                    player.Coins += 100;
                    await _database.UpdatePlayerAsync(player);
                    Console.WriteLine($"📺 Player {player.Name} received ad reward: +100 coins");
                }
            }

            // void метод - возвращаем null
            var result = new BinaryValue { IsNull = true };
            await _handler.WriteProtoResponseAsync(client, request.Id, result, null);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ GiveAdReward: {ex.Message}");
        }
    }
}
