using System.Net.Sockets;
using Axlebolt.RpcSupport.Protobuf;
using Axlebolt.Bolt.Protobuf;
using StandRiseServer.Core;
using Google.Protobuf;

namespace StandRiseServer.Services;

public class InAppService
{
    private readonly ProtobufHandler _handler;
    private readonly DatabaseService _database;
    private readonly SessionManager _sessionManager;

    public InAppService(ProtobufHandler handler, DatabaseService database, SessionManager sessionManager)
    {
        _handler = handler;
        _database = database;
        _sessionManager = sessionManager;

        Console.WriteLine("💳 Registering InAppService handlers...");
        _handler.RegisterHandler("GoogleInAppRemoteService", "buyInApp", GoogleBuyInAppAsync);
        _handler.RegisterHandler("AppStoreInAppRemoteService", "buyInApp", AppStoreBuyInAppAsync);
        _handler.RegisterHandler("AmazonInAppRemoteService", "buyInApp", AmazonBuyInAppAsync);
        Console.WriteLine("💳 InAppService handlers registered!");
    }

    private async Task GoogleBuyInAppAsync(TcpClient client, RpcRequest request)
    {
        try
        {
            Console.WriteLine("💳 Google BuyInApp Request");
            await ProcessInAppPurchaseAsync(client, request);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ GoogleBuyInApp: {ex.Message}");
        }
    }

    private async Task AppStoreBuyInAppAsync(TcpClient client, RpcRequest request)
    {
        try
        {
            Console.WriteLine("💳 AppStore BuyInApp Request");
            await ProcessInAppPurchaseAsync(client, request);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ AppStoreBuyInApp: {ex.Message}");
        }
    }

    private async Task AmazonBuyInAppAsync(TcpClient client, RpcRequest request)
    {
        try
        {
            Console.WriteLine("💳 Amazon BuyInApp Request");
            await ProcessInAppPurchaseAsync(client, request);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ AmazonBuyInApp: {ex.Message}");
        }
    }

    private async Task ProcessInAppPurchaseAsync(TcpClient client, RpcRequest request)
    {
        var session = _sessionManager.GetSessionByClient(client);
        if (session == null)
        {
            session = _sessionManager.GetAllSessions().FirstOrDefault();
            if (session != null) session.Client = client;
        }

        var playerInventory = new PlayerInventory();

        if (session != null)
        {
            var player = await _database.GetPlayerByTokenAsync(session.Token);
            if (player != null)
            {
                // Добавляем валюту за покупку (для тестирования)
                player.Gems += 1000;
                await _database.UpdatePlayerAsync(player);

                // Возвращаем обновленный инвентарь
                playerInventory.Currencies.Add(new CurrencyAmount { CurrencyId = 101, Value = player.Coins });
                playerInventory.Currencies.Add(new CurrencyAmount { CurrencyId = 102, Value = player.Gems });

                foreach (var item in player.Inventory.Items)
                {
                    playerInventory.InventoryItems.Add(new InventoryItem
                    {
                        Id = item.Id,
                        ItemDefinitionId = item.DefinitionId,
                        Quantity = item.Quantity,
                        Flags = item.Flags,
                        Date = item.Date
                    });
                }

                Console.WriteLine($"💳 Player {player.Name} purchased: +1000 gems");
            }
        }

        var result = new BinaryValue
        {
            IsNull = false,
            One = ByteString.CopyFrom(playerInventory.ToByteArray())
        };

        await _handler.WriteProtoResponseAsync(client, request.Id, result, null);
    }
}
