using StandRiseServer.Core;
using StandRiseServer.Models;
using MongoDB.Driver;
using Axlebolt.Bolt.Protobuf;
using Google.Protobuf;
// Подключаем твой скрипт с ID
//using Axlebolt.Standoff.Main.Inventory; 

namespace StandRiseServer.Services;

public class MarketplaceCleanupService
{
    private readonly DatabaseService _database;
    private readonly ProtobufHandler _handler;
    private readonly SessionManager _sessionManager;
    private readonly Timer _cleanupTimer;

    public MarketplaceCleanupService(DatabaseService database, ProtobufHandler handler, SessionManager sessionManager)
    {
        _database = database;
        _handler = handler;
        _sessionManager = sessionManager;
        
        _cleanupTimer = new Timer(CleanupExpiredListings, null, TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(5));
        Logger.Startup("MarketplaceCleanupService started");
    }

    private async void CleanupExpiredListings(object? state)
    {
        try
        {
            await CleanupExpiredSalesAsync();
            await CleanupExpiredPurchasesAsync();
        }
        catch (Exception ex)
        {
            Logger.Error($"MarketplaceCleanup error: {ex.Message}");
        }
    }

    private async Task CleanupExpiredSalesAsync()
    {
        var listingCollection = _database.GetCollection<MarketplaceListing>("marketplace_listings");
        var expiredFilter = Builders<MarketplaceListing>.Filter.And(
            Builders<MarketplaceListing>.Filter.Eq(x => x.Status, ListingStatus.Active),
            Builders<MarketplaceListing>.Filter.Lt(x => x.ExpiresAt, DateTime.UtcNow)
        );

        var expiredListings = await listingCollection.Find(expiredFilter).ToListAsync();
        
        foreach (var listing in expiredListings)
        {
            listing.Status = ListingStatus.Expired;
            await listingCollection.ReplaceOneAsync(x => x.ListingId == listing.ListingId, listing);

            await ReturnItemToSellerAsync(listing);
            await SendExpiredEventAsync(listing);
            
            Logger.Info($"Expired sale listing: {listing.ListingId}");
        }
    }

    private async Task CleanupExpiredPurchasesAsync()
    {
        var purchaseCollection = _database.GetCollection<MarketplacePurchaseRequest>("marketplace_purchases");
        var expiredFilter = Builders<MarketplacePurchaseRequest>.Filter.And(
            Builders<MarketplacePurchaseRequest>.Filter.Eq(x => x.Status, ListingStatus.Active),
            Builders<MarketplacePurchaseRequest>.Filter.Lt(x => x.ExpiresAt, DateTime.UtcNow)
        );

        var expiredPurchases = await purchaseCollection.Find(expiredFilter).ToListAsync();
        
        foreach (var purchase in expiredPurchases)
        {
            purchase.Status = ListingStatus.Expired;
            await purchaseCollection.ReplaceOneAsync(x => x.RequestId == purchase.RequestId, purchase);
            await SendExpiredPurchaseEventAsync(purchase);
            
            Logger.Info($"Expired purchase request: {purchase.RequestId}");
        }
    }

    // ТУТ ИСПРАВЛЕНО
    private async Task ReturnItemToSellerAsync(MarketplaceListing listing)
    {
        try
        {
            var player = await _database.GetPlayerByTokenAsync(listing.SellerId);
            if (player != null)
            {
                // Удаляем старый предмет с таким же ID, если он вдруг забагался (опционально)
                player.Inventory.Items.RemoveAll(x => x.Id == listing.ItemDefinitionId);

                // Добавляем предмет, где Id берется строго из ItemDefinitionId (который соответствует твоему Enum)
                player.Inventory.Items.Add(new PlayerInventoryItem
                {
                    // Теперь ID предмета в инвентаре = ID из InventoryID.cs (например 44002)
                    Id = listing.ItemDefinitionId, 
                    DefinitionId = listing.ItemDefinitionId,
                    Quantity = 1,
                    Date = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
                });

                await _database.UpdatePlayerAsync(player);
                Logger.Info($"Item {listing.ItemDefinitionId} returned to player {player.Name}");
            }
        }
        catch (Exception ex)
        {
            Logger.Error($"Failed to return item to seller {listing.SellerId}: {ex.Message}");
        }
    }

    private async Task SendExpiredEventAsync(MarketplaceListing listing)
    {
        try
        {
            var closedRequest = new ClosedRequest
            {
                Id = listing.ListingId,
                ItemDefinitionId = listing.ItemDefinitionId,
                Price = listing.Price,
                CreateDate = new DateTimeOffset(listing.CreatedAt).ToUnixTimeMilliseconds(),
                CloseDate = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                Type = MarketRequestType.SaleRequest,
                Reason = ClosingReason.Cancelled,
                Quantity = 1
            };

            var sellerSession = _sessionManager.GetSessionByToken(listing.SellerId);
            if (sellerSession != null)
            {
                var closedEvent = new OnPlayerRequestClosedEvent 
                { 
                    Request = closedRequest/*,
                    Item = ByteString.Empty*/
                };
                await _handler.SendEventAsync(sellerSession.Client, "MarketplaceRemoteEventListener", "onPlayerRequestClosed", 
                    ByteString.CopyFrom(closedEvent.ToByteArray()));
            }

            var tradeClosedEvent = new OnTradeRequestClosedEvent { Request = closedRequest };
            await _handler.BroadcastEventAsync($"marketplace_trade_{listing.ItemDefinitionId}", "MarketplaceRemoteEventListener", "onTradeRequestClosed",
                ByteString.CopyFrom(tradeClosedEvent.ToByteArray()));
        }
        catch (Exception ex)
        {
            Logger.Error($"Failed to send expired event for {listing.ListingId}: {ex.Message}");
        }
    }

    private async Task SendExpiredPurchaseEventAsync(MarketplacePurchaseRequest purchase)
    {
        try
        {
            var closedRequest = new ClosedRequest
            {
                Id = purchase.RequestId,
                ItemDefinitionId = purchase.ItemDefinitionId,
                Price = purchase.MaxPrice,
                CreateDate = new DateTimeOffset(purchase.CreatedAt).ToUnixTimeMilliseconds(),
                CloseDate = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                Type = MarketRequestType.PurchaseRequest,
                Reason = ClosingReason.Cancelled,
                Quantity = purchase.Quantity
            };

            var buyerSession = _sessionManager.GetSessionByToken(purchase.BuyerId);
            if (buyerSession != null)
            {
                var closedEvent = new OnPlayerRequestClosedEvent 
                { 
                    Request = closedRequest/*,
                    Item = ByteString.Empty*/
                };
                await _handler.SendEventAsync(buyerSession.Client, "MarketplaceRemoteEventListener", "onPlayerRequestClosed", 
                    ByteString.CopyFrom(closedEvent.ToByteArray()));
            }
        }
        catch (Exception ex)
        {
            Logger.Error($"Failed to send expired purchase event for {purchase.RequestId}: {ex.Message}");
        }
    }

    public void Dispose()
    {
        _cleanupTimer?.Dispose();
    }
}