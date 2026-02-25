using System.Net.Sockets;
using Axlebolt.RpcSupport.Protobuf;
using Axlebolt.Bolt.Protobuf;
using StandRiseServer.Core;
using Google.Protobuf;
using MongoDB.Driver;

namespace StandRiseServer.Services;

public class MarketplaceRemoteService
{
    private readonly ProtobufHandler _handler;
    private readonly DatabaseService _database;
    private readonly SessionManager _sessionManager;

    public MarketplaceRemoteService(ProtobufHandler handler, DatabaseService database, SessionManager sessionManager)
    {
        _handler = handler;
        _database = database;
        _sessionManager = sessionManager;

        _handler.RegisterHandler("MarketplaceRemoteService", "getMarketplaceSettings", GetMarketplaceSettingsAsync);
        _handler.RegisterHandler("MarketplaceRemoteService", "getPlayerOpenRequests", GetPlayerOpenRequestsAsync);
        _handler.RegisterHandler("MarketplaceRemoteService", "getPlayerProcessingRequest", GetPlayerProcessingRequestsAsync);
        _handler.RegisterHandler("MarketplaceRemoteService", "getPlayerClosedRequests", GetPlayerClosedRequestsAsync);
        _handler.RegisterHandler("MarketplaceRemoteService", "getPlayerClosedRequestsCount", GetPlayerClosedRequestsCountAsync);
        _handler.RegisterHandler("MarketplaceRemoteService", "createSaleRequest", CreateSaleRequestAsync);
        _handler.RegisterHandler("MarketplaceRemoteService", "createPurchaseRequest", CreatePurchaseRequestAsync);
        _handler.RegisterHandler("MarketplaceRemoteService", "createPurchaseRequestBySale", CreatePurchaseRequestBySaleAsync);
        _handler.RegisterHandler("MarketplaceRemoteService", "cancelRequest", CancelRequestAsync);
        _handler.RegisterHandler("MarketplaceRemoteService", "getTrade", GetTradeAsync);
        _handler.RegisterHandler("MarketplaceRemoteService", "getTrades", GetTradesAsync);
        _handler.RegisterHandler("MarketplaceRemoteService", "getTradeOpenSaleRequests", GetTradeOpenSaleRequestsAsync);
        _handler.RegisterHandler("MarketplaceRemoteService", "getTradeOpenPurchaseRequests", GetTradeOpenPurchaseRequestsAsync);
    }

    private async Task<Models.MarketplaceConfig> GetConfigAsync()
    {
        var configCollection = _database.GetCollection<Models.MarketplaceConfig>("marketplace_config");
        return await configCollection.Find(x => x.ConfigId == "default").FirstOrDefaultAsync() 
            ?? new Models.MarketplaceConfig();
    }

    private async Task GetMarketplaceSettingsAsync(TcpClient client, RpcRequest request)
    {
        try
        {
            var config = await GetConfigAsync();

            var settings = new MarketplaceSettings
            {
                CommissionPercent = config.CommissionPercent,
                MinCommission = config.MinCommission,
                CurrencyId = config.CurrencyId,
                Enabled = config.Enabled
            };

            var result = new BinaryValue
            {
                IsNull = false,
                One = ByteString.CopyFrom(settings.ToByteArray())
            };

            await _handler.WriteProtoResponseAsync(client, request.Id, result, null);
            Console.WriteLine($"🛒 MarketplaceSettings: Enabled={config.Enabled}, Commission={config.CommissionPercent * 100}%");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ GetMarketplaceSettings: {ex.Message}");
            await SendErrorAsync(client, request.Id, 500);
        }
    }

    private async Task GetPlayerOpenRequestsAsync(TcpClient client, RpcRequest request)
    {
        try
        {
            var session = _sessionManager.GetSessionByClient(client);
            var playerId = await GetPlayerObjectIdByClientAsync(client) ?? "";

            var listingCollection = _database.GetCollection<Models.MarketplaceListing>("marketplace_listings");
            var listings = await listingCollection
                .Find(x => x.SellerId == playerId && x.Status == Models.ListingStatus.Active)
                .ToListAsync();

            var purchaseCollection = _database.GetCollection<Models.MarketplacePurchaseRequest>("marketplace_purchases");
            var purchases = await purchaseCollection
                .Find(x => x.BuyerId == playerId && x.Status == Models.ListingStatus.Active)
                .ToListAsync();

            Console.WriteLine($"🛒 GetPlayerOpenRequests: {listings.Count} sales, {purchases.Count} purchases");

            var result = new BinaryValue { IsNull = false };

            // Sale requests
            foreach (var listing in listings)
            {
                var seller = await _database.GetPlayerByObjectIdAsync(listing.SellerId);
                var openRequest = new OpenRequest
                {
                    Id = listing.ListingId,
                    ItemDefinitionId = listing.ItemDefinitionId,
                    Price = listing.Price,
                    CreateDate = new DateTimeOffset(listing.CreatedAt).ToUnixTimeMilliseconds(),
                    Type = MarketRequestType.SaleRequest,
                    Quantity = listing.Quantity,
                    Creator = new Player
                    {
                        Id = seller?.Id.ToString() ?? listing.SellerId,
                        Uid = seller?.PlayerUid ?? listing.SellerId,
                        Name = seller?.Name ?? listing.SellerName ?? "Unknown",
                        AvatarId = seller?.AvatarId ?? seller?.Avatar ?? "",
                        TimeInGame = seller?.TimeInGame ?? 0,
                        PlayerStatus = null,
                        LogoutDate = 0,
                        RegistrationDate = seller != null ? new DateTimeOffset(seller.RegistrationDate).ToUnixTimeSeconds() : 0
                    }
                };
                result.Array.Add(ByteString.CopyFrom(openRequest.ToByteArray()));
            }

            // Purchase requests
            foreach (var purchase in purchases)
            {
                var buyer = await _database.GetPlayerByObjectIdAsync(purchase.BuyerId);
                var openRequest = new OpenRequest
                {
                    Id = purchase.RequestId,
                    ItemDefinitionId = purchase.ItemDefinitionId,
                    Price = purchase.MaxPrice,
                    CreateDate = new DateTimeOffset(purchase.CreatedAt).ToUnixTimeMilliseconds(),
                    Type = MarketRequestType.PurchaseRequest,
                    Quantity = purchase.Quantity,
                    Creator = new Player
                    {
                        Id = buyer?.Id.ToString() ?? purchase.BuyerId,
                        Uid = buyer?.PlayerUid ?? purchase.BuyerId,
                        Name = buyer?.Name ?? purchase.BuyerName ?? "Unknown",
                        AvatarId = buyer?.AvatarId ?? buyer?.Avatar ?? "",
                        TimeInGame = buyer?.TimeInGame ?? 0,
                        PlayerStatus = null,
                        LogoutDate = 0,
                        RegistrationDate = buyer != null ? new DateTimeOffset(buyer.RegistrationDate).ToUnixTimeSeconds() : 0
                    }
                };
                result.Array.Add(ByteString.CopyFrom(openRequest.ToByteArray()));
            }

            await _handler.WriteProtoResponseAsync(client, request.Id, result, null);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ GetPlayerOpenRequests: {ex.Message}");
            await SendErrorAsync(client, request.Id, 500);
        }
    }

    private async Task GetPlayerProcessingRequestsAsync(TcpClient client, RpcRequest request)
    {
        try
        {
            // Processing requests - временные запросы в процессе создания
            var result = new BinaryValue { IsNull = false };
            await _handler.WriteProtoResponseAsync(client, request.Id, result, null);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ GetPlayerProcessingRequests: {ex.Message}");
            await SendErrorAsync(client, request.Id, 500);
        }
    }

    private async Task GetPlayerClosedRequestsAsync(TcpClient client, RpcRequest request)
    {
        try
        {
            var session = _sessionManager.GetSessionByClient(client);
            var playerId = await GetPlayerObjectIdByClientAsync(client) ?? "";

            // Парсим параметры пагинации
            int page = 0, size = 20;
            MarketRequestType type = MarketRequestType.SaleRequest;
            if (request.Params.Count > 0 && request.Params[0].One != null)
            {
                var args = GetClosedRequestsArgs.Parser.ParseFrom(request.Params[0].One);
                page = args.Page;
                size = args.Size;
                type = args.Type;
            }

            var transactionCollection = _database.GetCollection<Models.MarketplaceTransaction>("marketplace_transactions");
            var transactions = await transactionCollection
                .Find(x => type == MarketRequestType.SaleRequest && x.SellerId == playerId || type == MarketRequestType.PurchaseRequest && x.BuyerId == playerId)
                .SortByDescending(x => x.CompletedAt)
                .Skip(page * size)
                .Limit(size)
                .ToListAsync();

            Console.WriteLine($"🛒 GetPlayerClosedRequests: {transactions.Count} transactions");

            var result = new BinaryValue { IsNull = false };
            foreach (var tx in transactions)
            {
                var seller = await _database.GetPlayerByObjectIdAsync(tx.SellerId);
                var buyer = await _database.GetPlayerByObjectIdAsync(tx.BuyerId);
                var listingCollection = _database.GetCollection<Models.MarketplaceListing>("marketplace_listings");
                var listing = await listingCollection.Find(x => x.ListingId == tx.ListingId && x.Status == Models.ListingStatus.Sold).FirstOrDefaultAsync();
                
                var closedRequest = new ClosedRequest
                {
                    Id = tx.TransactionId,
                    OriginId = tx.TransactionId,
                    ItemDefinitionId = tx.ItemDefinitionId,
                    Price = tx.SellerId == playerId ? tx.SellerReceived : tx.Price,
                    CreateDate = new DateTimeOffset(listing.CreatedAt).ToUnixTimeMilliseconds(),
                    CloseDate = new DateTimeOffset(tx.CompletedAt).ToUnixTimeMilliseconds(),
                    Type = tx.SellerId == playerId ? MarketRequestType.SaleRequest : MarketRequestType.PurchaseRequest,
                    Reason = ClosingReason.SuccessTransaction,
                    Quantity = 1,
                    Creator = new Player
                    {
                        Id = tx.BuyerId == playerId ? seller?.Id.ToString() ?? listing.SellerId : buyer?.Id.ToString() ?? listing.BuyerId,
                        Uid = tx.BuyerId == playerId ? seller?.PlayerUid ?? listing.SellerId : buyer?.PlayerUid ?? listing.BuyerId,
                        Name = tx.BuyerId == playerId ? seller?.Name ?? listing.SellerName ?? "Unknown" : buyer?.Name ?? listing.BuyerName ?? "Unknown",
                        AvatarId = tx.BuyerId == playerId ? seller?.AvatarId ?? seller?.Avatar ?? "" : buyer?.AvatarId ?? buyer?.Avatar ?? "",
                        TimeInGame = tx.BuyerId == playerId ? seller?.TimeInGame ?? 0 : buyer?.TimeInGame ?? 0,
                        PlayerStatus = null,
                        LogoutDate = 0,
                        RegistrationDate = tx.BuyerId == playerId ? seller != null ? new DateTimeOffset(seller.RegistrationDate).ToUnixTimeSeconds() : 0 : buyer != null ? new DateTimeOffset(buyer.RegistrationDate).ToUnixTimeSeconds() : 0
                    },
                    PartnerRequestId = tx.TransactionId,
                    Partner = new Player
                    {
                        Id = tx.BuyerId == playerId ? seller?.Id.ToString() ?? listing.SellerId : buyer?.Id.ToString() ?? listing.BuyerId,
                        Uid = tx.BuyerId == playerId ? seller?.PlayerUid ?? listing.SellerId : buyer?.PlayerUid ?? listing.BuyerId,
                        Name = tx.BuyerId == playerId ? seller?.Name ?? listing.SellerName ?? "Unknown" : buyer?.Name ?? listing.BuyerName ?? "Unknown",
                        AvatarId = tx.BuyerId == playerId ? seller?.AvatarId ?? seller?.Avatar ?? "" : buyer?.AvatarId ?? buyer?.Avatar ?? "",
                        TimeInGame = tx.BuyerId == playerId ? seller?.TimeInGame ?? 0 : buyer?.TimeInGame ?? 0,
                        PlayerStatus = null,
                        LogoutDate = 0,
                        RegistrationDate = tx.BuyerId == playerId ? seller != null ? new DateTimeOffset(seller.RegistrationDate).ToUnixTimeSeconds() : 0 : buyer != null ? new DateTimeOffset(buyer.RegistrationDate).ToUnixTimeSeconds() : 0
                    }
                };
                
                result.Array.Add(ByteString.CopyFrom(closedRequest.ToByteArray()));
            }

            await _handler.WriteProtoResponseAsync(client, request.Id, result, null);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ GetPlayerClosedRequests: {ex.Message}");
            await SendErrorAsync(client, request.Id, 500);
        }
    }

    private async Task GetPlayerClosedRequestsCountAsync(TcpClient client, RpcRequest request)
    {
        try
        {
            var args = GetClosedRequestsCountArgs.Parser.ParseFrom(request.Params[0].One);
            
            var session = _sessionManager.GetSessionByClient(client);
            var playerId = await GetPlayerObjectIdByClientAsync(client) ?? "";

            var transactionCollection = _database.GetCollection<Models.MarketplaceTransaction>("marketplace_transactions");
            var count = await transactionCollection.CountDocumentsAsync(
                x => args.Type == MarketRequestType.SaleRequest && x.SellerId == playerId || args.Type == MarketRequestType.PurchaseRequest && x.BuyerId == playerId);

            var countProto = new Axlebolt.RpcSupport.Protobuf.Integer { Value = (int)count };
            var result = new BinaryValue
            {
                IsNull = false,
                One = ByteString.CopyFrom(countProto.ToByteArray())
            };

            await _handler.WriteProtoResponseAsync(client, request.Id, result, null);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ GetPlayerClosedRequestsCount: {ex.Message}");
            await SendErrorAsync(client, request.Id, 500);
        }
    }


    private async Task CreateSaleRequestAsync(TcpClient client, RpcRequest request)
    {
        try
        {
            Console.WriteLine("💰 [CreateSaleRequest] STARTED");

            int itemId = 0;
            float price = 0;

            if (request.Params.Count > 0 && request.Params[0].One != null)
            {
                var args = CreateSaleRequestArgs.Parser.ParseFrom(request.Params[0].One);
                itemId = args.ItemId;
                price = args.Price;
                Console.WriteLine($"💰 [CreateSaleRequest] Received Args: ItemId(Instance)={itemId}, Price={price}");
            }
            else
            {
                Console.WriteLine("❌ [CreateSaleRequest] No arguments received!");
            }

            var config = await GetConfigAsync();
            Console.WriteLine($"💰 [CreateSaleRequest] Config: MinPrice={config.MinPrice}, MaxPrice={config.MaxPrice}, Commission={config.CommissionPercent}");
            
            float originalPrice = price;
            price = Math.Clamp(price, config.MinPrice, config.MaxPrice);
            if (price != originalPrice) Console.WriteLine($"⚠️ [CreateSaleRequest] Price clamped from {originalPrice} to {price}");

            var session = _sessionManager.GetSessionByClient(client);
            if (session == null)
            {
                Console.WriteLine("❌ [CreateSaleRequest] Session not found for client");
                await SendErrorAsync(client, request.Id, 401);
                return;
            }
            Console.WriteLine($"💰 [CreateSaleRequest] Session found: Token={session.Token}");

            var player = await _database.GetPlayerByTokenAsync(session.Token);
            if (player == null)
            {
                Console.WriteLine($"❌ [CreateSaleRequest] Player not found for token: {session.Token}");
                await SendErrorAsync(client, request.Id, 404);
                return;
            }
            Console.WriteLine($"💰 [CreateSaleRequest] Player found: Name={player.Name}, UID={player.PlayerUid}");
            Console.WriteLine($"💰 [CreateSaleRequest] Scanning inventory for ItemId={itemId}...");

            int itemDefinitionId = 0;
            Models.PlayerInventoryItem? inventoryItem = null;
            
            if (player.Inventory != null && player.Inventory.Items != null)
            {
                inventoryItem = player.Inventory.Items.FirstOrDefault(x => x.Id == itemId);
                if (inventoryItem != null)
                {
                    itemDefinitionId = inventoryItem.DefinitionId;
                    Console.WriteLine($"✅ [CreateSaleRequest] Item Found in Inventory! InstanceId={inventoryItem.Id}, DefinitionId={inventoryItem.DefinitionId}, Quantity={inventoryItem.Quantity}");
                }
                else
                {
                    Console.WriteLine($"❌ [CreateSaleRequest] Item {itemId} NOT found in player inventory!");
                    // Dump first 5 items for debug
                    Console.WriteLine($"💰 [CreateSaleRequest] First 5 items in inventory: {string.Join(", ", player.Inventory.Items.Take(5).Select(x => $"[Id={x.Id}, Def={x.DefinitionId}]"))}");
                }
            }
            else
            {
                Console.WriteLine("❌ [CreateSaleRequest] Player inventory is null or empty!");
            }

            // Проверяем что предмет найден и имеет валидный definition
            if (itemDefinitionId == 0)
            {
                Console.WriteLine($"❌ [CreateSaleRequest] Validation Failed: ItemDefinitionId is 0 or Item not found.");
                await SendErrorAsync(client, request.Id, 404);
                return;
            }

            // Запрещаем выставлять кейсы на рынок (ID 301-304 Cases, 401-404 Boxes)
            if ((itemDefinitionId >= 301 && itemDefinitionId <= 304) || (itemDefinitionId >= 401 && itemDefinitionId <= 404))
            {
                Console.WriteLine($"❌ [CreateSaleRequest] Cases cannot be sold on marketplace: ItemDefId={itemDefinitionId}");
                await SendErrorAsync(client, request.Id, 403); // Forbidden
                return;
            }

            // Удаляем предмет из инвентаря (он теперь на рынке)
            if (inventoryItem != null)
            {
                Console.WriteLine($"💰 [CreateSaleRequest] Removing item {inventoryItem.Id} from inventory...");
                player.Inventory.Items.Remove(inventoryItem);
                await _database.UpdatePlayerAsync(player);
                Console.WriteLine($"✅ [CreateSaleRequest] Item removed and player saved.");
            }

            var listingId = Guid.NewGuid().ToString();
            var listingCollection = _database.GetCollection<Models.MarketplaceListing>("marketplace_listings");

            var listing = new Models.MarketplaceListing
            {
                ListingId = listingId,
                SellerId = await GetPlayerObjectIdByClientAsync(client) ?? "unknown",
                SellerName = player?.Name ?? "Unknown",
                InventoryItemId = itemId,
                ItemDefinitionId = itemDefinitionId,
                Price = price,
                CurrencyId = config.CurrencyId,
                Status = Models.ListingStatus.Active,
                CreatedAt = DateTime.UtcNow,
                ExpiresAt = DateTime.UtcNow.AddHours(config.ListingDurationHours)
            };

            await listingCollection.InsertOneAsync(listing);
            Console.WriteLine($"💰 [CreateSaleRequest] Listing saved to DB: ListingId={listingId}, ItemDefId={itemDefinitionId}, Price={price}");

            // Создаем Creator для события
            // Создаем Creator для события (Correct Protobuf Player)
            /*var creatorPlayer = new Axlebolt.Bolt.Protobuf.Player
            {
                Id = player?.PlayerUid ?? await GetPlayerObjectIdByClientAsync(client) ?? "",
                Uid = player?.PlayerUid ?? await GetPlayerObjectIdByClientAsync(client) ?? "",
                Name = player?.Name ?? "Unknown",
                AvatarId = player?.AvatarId ?? player?.Avatar ?? "",
                PlayerStatus = new PlayerStatus 
                { 
                    OnlineStatus = Axlebolt.Bolt.Protobuf.OnlineStatus.StateOnline 
                }
            };*/
            
            var creatorPlayer = new Player
            {
                Id = player?.Id.ToString() ?? listing.SellerId,
                Uid = player?.PlayerUid ?? listing.SellerId,
                Name = player?.Name ?? listing.SellerName ?? "Unknown",
                AvatarId = player?.AvatarId ?? player?.Avatar ?? "",
                TimeInGame = player?.TimeInGame ?? 0,
                PlayerStatus = null,
                LogoutDate = 0,
                RegistrationDate = player != null ? new DateTimeOffset(player.RegistrationDate).ToUnixTimeSeconds() : 0
            };
            
            // Создаем OpenRequest для события
            var openRequest = new OpenRequest
            {
                Id = listingId,
                Creator = creatorPlayer,
                ItemDefinitionId = itemDefinitionId,
                Price = price,
                CreateDate = new DateTimeOffset(listing.CreatedAt).ToUnixTimeMilliseconds(),
                Type = MarketRequestType.SaleRequest,
                Quantity = listing.Quantity
            };
            
            Console.WriteLine($"📤 [CreateSaleRequest] Prepping Events. OpenRequest: Id={openRequest.Id}, ItemDefId={openRequest.ItemDefinitionId}, Price={openRequest.Price}, Creator={creatorPlayer.Name}");

            // Сначала отправляем ответ с ID заявки
            var requestIdStr = new Axlebolt.RpcSupport.Protobuf.String { Value = listingId };
            var result = new BinaryValue
            {
                IsNull = false,
                One = ByteString.CopyFrom(requestIdStr.ToByteArray())
            };
            await _handler.WriteProtoResponseAsync(client, request.Id, result, null);
            Console.WriteLine($"📤 [CreateSaleRequest] RPC response sent with listingId: {listingId}");

            // Задержка чтобы клиент успел обработать ответ RPC и установить _requestId
            //await Task.Delay(200);

            // Затем отправляем событие OnPlayerRequestOpened
            var openEvent = new OnPlayerRequestOpenedEvent { Request = openRequest };
            var eventBytes = openEvent.ToByteArray();
            Console.WriteLine($"📤 [CreateSaleRequest] Sending onPlayerRequestOpened (size: {eventBytes.Length} bytes)");
            
            // Отправляем событие создателю заявки
            await _handler.SendEventAsync(client, "MarketplaceRemoteEventListener", "onPlayerRequestOpened", 
                ByteString.CopyFrom(eventBytes));
            
            // Также отправляем событие всем подписчикам на торги этого предмета
            var tradeOpenEvent = new OnTradeRequestOpenedEvent { Request = openRequest };
            
            Console.WriteLine($"📤 [CreateSaleRequest] Broadcasting onTradeRequestOpened for topic: marketplace_trade_{itemDefinitionId}");
            await _handler.BroadcastEventAsync($"marketplace_trade_{itemDefinitionId}", "MarketplaceRemoteEventListener", "onTradeRequestOpened",
                ByteString.CopyFrom(tradeOpenEvent.ToByteArray()));
            
            // Обновляем торговую информацию
            Console.WriteLine($"📤 [CreateSaleRequest] Sending Trade Updated Event for ItemDefId={itemDefinitionId}...");
            await SendTradeUpdatedEventAsync(itemDefinitionId);
            
            Console.WriteLine($"✅ [CreateSaleRequest] COMPLETED for {listingId}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ [CreateSaleRequest] EXCEPTION: {ex.Message}");
            Console.WriteLine(ex.StackTrace);
            await SendErrorAsync(client, request.Id, 500);
        }
    }

    private async Task CreatePurchaseRequestAsync(TcpClient client, RpcRequest request)
    {
        try
        {
            Console.WriteLine("💰 CreatePurchaseRequest");

            int itemDefinitionId = 0;
            float price = 0;
            int quantity = 1;

            if (request.Params.Count > 0 && request.Params[0].One != null)
            {
                var args = CreatePurchaseRequestArgs.Parser.ParseFrom(request.Params[0].One);
                itemDefinitionId = args.ItemDefinitionId;
                price = args.Price;
                quantity = args.Quantity;
            }

            var config = await GetConfigAsync();
            var session = _sessionManager.GetSessionByClient(client);
            var player = session != null ? await _database.GetPlayerByTokenAsync(session.Token) : null;

            var requestId = Guid.NewGuid().ToString();
            var purchaseCollection = _database.GetCollection<Models.MarketplacePurchaseRequest>("marketplace_purchases");

            var purchaseRequest = new Models.MarketplacePurchaseRequest
            {
                RequestId = requestId,
                BuyerId = await GetPlayerObjectIdByClientAsync(client) ?? "unknown",
                BuyerName = player?.Name ?? "Unknown",
                ItemDefinitionId = itemDefinitionId,
                MaxPrice = price,
                CurrencyId = config.CurrencyId,
                Quantity = quantity,
                Status = Models.ListingStatus.Active,
                CreatedAt = DateTime.UtcNow,
                ExpiresAt = DateTime.UtcNow.AddHours(config.ListingDurationHours)
            };

            await purchaseCollection.InsertOneAsync(purchaseRequest);
            Console.WriteLine($"💰 Created purchase request: {requestId}, Item={itemDefinitionId}, MaxPrice={price}");

            // Создаем Creator для события
            // Создаем Creator для события (Correct Protobuf Player)
            /*var creatorPlayer = new Axlebolt.Bolt.Protobuf.Player
            {
                Id = player?.PlayerUid ?? await GetPlayerObjectIdByClientAsync(client) ?? "",
                Uid = player?.PlayerUid ?? await GetPlayerObjectIdByClientAsync(client) ?? "",
                Name = player?.Name ?? "Unknown",
                AvatarId = player?.AvatarId ?? player?.Avatar ?? "",
                PlayerStatus = new PlayerStatus 
                { 
                    OnlineStatus = Axlebolt.Bolt.Protobuf.OnlineStatus.StateOnline 
                }
            };*/
            
            var creatorPlayer = new Player
            {
                Id = player?.Id.ToString() ?? await GetPlayerObjectIdByClientAsync(client) ?? "",
                Uid = player?.PlayerUid ?? await GetPlayerObjectIdByClientAsync(client) ?? "",
                Name = player?.Name ?? "Unknown",
                AvatarId = player?.AvatarId ?? player?.Avatar ?? "",
                TimeInGame = player?.TimeInGame ?? 0,
                PlayerStatus = null,
                LogoutDate = 0,
                RegistrationDate = player != null ? new DateTimeOffset(player.RegistrationDate).ToUnixTimeSeconds() : 0
            };
            
            // Отправляем событие о создании заявки на покупку
            var openRequest = new OpenRequest
            {
                Id = requestId,
                Creator = creatorPlayer,
                ItemDefinitionId = itemDefinitionId,
                Price = price,
                CreateDate = new DateTimeOffset(purchaseRequest.CreatedAt).ToUnixTimeMilliseconds(),
                Type = MarketRequestType.PurchaseRequest,
                Quantity = quantity
            };

            var openEvent = new OnPlayerRequestOpenedEvent { Request = openRequest };
            await _handler.SendEventAsync(client, "MarketplaceRemoteEventListener", "onPlayerRequestOpened", 
                ByteString.CopyFrom(openEvent.ToByteArray()));

            var tradeOpenEvent = new OnTradeRequestOpenedEvent { Request = openRequest };
            await _handler.BroadcastEventAsync($"marketplace_trade_{itemDefinitionId}", "MarketplaceRemoteEventListener", "onTradeRequestOpened",
                ByteString.CopyFrom(tradeOpenEvent.ToByteArray()));

            await SendTradeUpdatedEventAsync(itemDefinitionId);

            var requestIdStr = new Axlebolt.RpcSupport.Protobuf.String { Value = requestId };
            var result = new BinaryValue
            {
                IsNull = false,
                One = ByteString.CopyFrom(requestIdStr.ToByteArray())
            };

            await _handler.WriteProtoResponseAsync(client, request.Id, result, null);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ CreatePurchaseRequest: {ex.Message}");
            await SendErrorAsync(client, request.Id, 500);
        }
    }

    private async Task CreatePurchaseRequestBySaleAsync(TcpClient client, RpcRequest request)
    {
        try
        {
            Console.WriteLine("💰 CreatePurchaseRequestBySale");

            string saleId = "";
            if (request.Params.Count > 0 && request.Params[0].One != null)
            {
                var args = CreatePurchaseBySaleArgs.Parser.ParseFrom(request.Params[0].One);
                saleId = args.SaleId;
            }

            var config = await GetConfigAsync();
            var session = _sessionManager.GetSessionByClient(client);
            var player = session != null ? await _database.GetPlayerByTokenAsync(session.Token) : null;

            // Находим листинг продажи
            var listingCollection = _database.GetCollection<Models.MarketplaceListing>("marketplace_listings");
            var listing = await listingCollection.Find(x => x.ListingId == saleId && x.Status == Models.ListingStatus.Active).FirstOrDefaultAsync();

            if (listing == null)
            {
                Console.WriteLine($"❌ Sale {saleId} not found");
                await SendErrorAsync(client, request.Id, 404);
                return;
            }

            // Проверяем что покупатель не является продавцом (нельзя купить свой же товар)
            var buyerId = await GetPlayerObjectIdByClientAsync(client);
            if (listing.SellerId == buyerId)
            {
                Console.WriteLine($"❌ Cannot buy own listing: {saleId}");
                await SendErrorAsync(client, request.Id, 403); // Forbidden
                return;
            }

            // Проверяем баланс покупателя
            if (player != null && player.Gems < listing.Price)
            {
                Console.WriteLine($"❌ Not enough balance: {player.Gems} < {listing.Price}");
                await SendErrorAsync(client, request.Id, 402);
                return;
            }

            // Выполняем транзакцию
            var transactionId = Guid.NewGuid().ToString();
            float commission = listing.Price * config.CommissionPercent;
            if (commission < config.MinCommission) commission = config.MinCommission;
            float sellerReceives = listing.Price - commission;

            // Обновляем листинг
            listing.Status = Models.ListingStatus.Sold;
            listing.SoldAt = DateTime.UtcNow;
            listing.BuyerId = await GetPlayerObjectIdByClientAsync(client);
            listing.BuyerName = player?.Name;
            await listingCollection.ReplaceOneAsync(x => x.ListingId == saleId, listing);

            // Списываем деньги у покупателя и добавляем предмет
            if (player != null)
            {
                player.Gems -= (int)listing.Price;
                var random = new Random();
                player.Inventory.Items.Add(new Models.PlayerInventoryItem
                {
                    Id = listing.InventoryItemId,
                    DefinitionId = listing.ItemDefinitionId,
                    Quantity = listing.Quantity,
                    Date = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
                });
                await _database.UpdatePlayerAsync(player);
            }

            // Начисляем деньги продавцу
            var seller = await _database.GetPlayerByObjectIdAsync(listing.SellerId);
            if (seller != null)
            {
                seller.Gems += (int)sellerReceives;
                await _database.UpdatePlayerAsync(seller);
            }

            // Сохраняем транзакцию
            var transactionCollection = _database.GetCollection<Models.MarketplaceTransaction>("marketplace_transactions");
            await transactionCollection.InsertOneAsync(new Models.MarketplaceTransaction
            {
                TransactionId = transactionId,
                ListingId = saleId,
                SellerId = listing.SellerId,
                BuyerId = await GetPlayerObjectIdByClientAsync(client) ?? "",
                ItemDefinitionId = listing.ItemDefinitionId,
                Price = listing.Price,
                Commission = commission,
                SellerReceived = sellerReceives,
                CurrencyId = listing.CurrencyId,
                CompletedAt = DateTime.UtcNow
            });

            Console.WriteLine($"💰 Transaction complete: {transactionId}, Seller gets {sellerReceives}");

            // Отправляем события о закрытии заявки
            var sellerClosedRequest = new ClosedRequest
            {
                Id = saleId,
                OriginId = saleId,
                ItemDefinitionId = listing.ItemDefinitionId,
                Price = sellerReceives,
                CreateDate = new DateTimeOffset(listing.CreatedAt).ToUnixTimeMilliseconds(),
                CloseDate = new DateTimeOffset(listing.SoldAt ?? DateTime.UtcNow).ToUnixTimeMilliseconds(),
                Type = MarketRequestType.SaleRequest,
                Reason = ClosingReason.SuccessTransaction,
                Quantity = listing.Quantity,
                /*Creator = new Axlebolt.Bolt.Protobuf.Player 
                {
                    Id = listing.SellerId,
                    Uid = listing.SellerId,
                    Name = listing.SellerName,
                    PlayerStatus = new PlayerStatus { OnlineStatus = Axlebolt.Bolt.Protobuf.OnlineStatus.StateOnline }
                }*/
                Creator = new Player
                {
                    Id = seller?.Id.ToString() ?? listing.SellerId,
                    Uid = seller?.PlayerUid ?? listing.SellerId,
                    Name = seller?.Name ?? listing.SellerName ?? "Unknown",
                    AvatarId = seller?.AvatarId ?? seller?.Avatar ?? "",
                    TimeInGame = seller?.TimeInGame ?? 0,
                    PlayerStatus = null,
                    LogoutDate = 0,
                    RegistrationDate = seller != null ? new DateTimeOffset(seller.RegistrationDate).ToUnixTimeSeconds() : 0
                },
                PartnerRequestId = transactionId,
                Partner = new Player
                {
                    Id = player?.Id.ToString() ?? listing.BuyerId,
                    Uid = player?.PlayerUid ?? listing.BuyerId,
                    Name = player?.Name ?? listing.BuyerName ?? "Unknown",
                    AvatarId = player?.AvatarId ?? player?.Avatar ?? "",
                    TimeInGame = player?.TimeInGame ?? 0,
                    PlayerStatus = null,
                    LogoutDate = 0,
                    RegistrationDate = player != null ? new DateTimeOffset(player.RegistrationDate).ToUnixTimeSeconds() : 0
                }
            };
            
            var tradeSaleClosedRequest = new ClosedRequest
            {
                Id = saleId,
                OriginId = saleId,
                ItemDefinitionId = listing.ItemDefinitionId,
                Price = listing.Price,
                CreateDate = new DateTimeOffset(listing.CreatedAt).ToUnixTimeMilliseconds(),
                CloseDate = new DateTimeOffset(listing.SoldAt ?? DateTime.UtcNow).ToUnixTimeMilliseconds(),
                Type = MarketRequestType.SaleRequest,
                Reason = ClosingReason.SuccessTransaction,
                Quantity = listing.Quantity,
                /*Creator = new Axlebolt.Bolt.Protobuf.Player 
                {
                    Id = await GetPlayerObjectIdByClientAsync(client) ?? "",
                    Uid = await GetPlayerObjectIdByClientAsync(client) ?? "",
                    Name = player?.Name ?? "Unknown",
                    PlayerStatus = new PlayerStatus { OnlineStatus = Axlebolt.Bolt.Protobuf.OnlineStatus.StateOnline }
                }*/
                Creator = new Player
                {
                    Id = player?.Id.ToString() ?? listing.BuyerId,
                    Uid = player?.PlayerUid ?? listing.BuyerId,
                    Name = player?.Name ?? listing.BuyerName ?? "Unknown",
                    AvatarId = player?.AvatarId ?? player?.Avatar ?? "",
                    TimeInGame = player?.TimeInGame ?? 0,
                    PlayerStatus = null,
                    LogoutDate = 0,
                    RegistrationDate = player != null ? new DateTimeOffset(player.RegistrationDate).ToUnixTimeSeconds() : 0
                },
                PartnerRequestId = saleId,
                Partner = new Player
                {
                    Id = seller?.Id.ToString() ?? listing.SellerId,
                    Uid = seller?.PlayerUid ?? listing.SellerId,
                    Name = seller?.Name ?? listing.SellerName ?? "Unknown",
                    AvatarId = seller?.AvatarId ?? seller?.Avatar ?? "",
                    TimeInGame = seller?.TimeInGame ?? 0,
                    PlayerStatus = null,
                    LogoutDate = 0,
                    RegistrationDate = seller != null ? new DateTimeOffset(seller.RegistrationDate).ToUnixTimeSeconds() : 0
                }
            };
            
            var tradePurchaseClosedRequest = new ClosedRequest
            {
                Id = saleId,
                OriginId = saleId,
                ItemDefinitionId = listing.ItemDefinitionId,
                Price = listing.Price,
                CreateDate = new DateTimeOffset(listing.CreatedAt).ToUnixTimeMilliseconds(),
                CloseDate = new DateTimeOffset(listing.SoldAt ?? DateTime.UtcNow).ToUnixTimeMilliseconds(),
                Type = MarketRequestType.PurchaseRequest,
                Reason = ClosingReason.SuccessTransaction,
                Quantity = listing.Quantity,
                /*Creator = new Axlebolt.Bolt.Protobuf.Player 
                {
                    Id = await GetPlayerObjectIdByClientAsync(client) ?? "",
                    Uid = await GetPlayerObjectIdByClientAsync(client) ?? "",
                    Name = player?.Name ?? "Unknown",
                    PlayerStatus = new PlayerStatus { OnlineStatus = Axlebolt.Bolt.Protobuf.OnlineStatus.StateOnline }
                }*/
                Creator = new Player
                {
                    Id = player?.Id.ToString() ?? listing.BuyerId,
                    Uid = player?.PlayerUid ?? listing.BuyerId,
                    Name = player?.Name ?? listing.BuyerName ?? "Unknown",
                    AvatarId = player?.AvatarId ?? player?.Avatar ?? "",
                    TimeInGame = player?.TimeInGame ?? 0,
                    PlayerStatus = null,
                    LogoutDate = 0,
                    RegistrationDate = player != null ? new DateTimeOffset(player.RegistrationDate).ToUnixTimeSeconds() : 0
                },
                PartnerRequestId = saleId,
                Partner = new Player
                {
                    Id = seller?.Id.ToString() ?? listing.SellerId,
                    Uid = seller?.PlayerUid ?? listing.SellerId,
                    Name = seller?.Name ?? listing.SellerName ?? "Unknown",
                    AvatarId = seller?.AvatarId ?? seller?.Avatar ?? "",
                    TimeInGame = seller?.TimeInGame ?? 0,
                    PlayerStatus = null,
                    LogoutDate = 0,
                    RegistrationDate = seller != null ? new DateTimeOffset(seller.RegistrationDate).ToUnixTimeSeconds() : 0
                }
            };

            // Отправляем событие продавцу
            var sellerSession = _sessionManager.GetSessionByPlayerObjectId(listing.SellerId);
            if (sellerSession != null)
            {
                var sellerClosedEvent = new OnPlayerRequestClosedEvent 
                { 
                    Request = sellerClosedRequest,
                    Item = new InventoryItem()
                    {
                        Id = listing.InventoryItemId,
                        ItemDefinitionId = listing.ItemDefinitionId,
                        Quantity = listing.Quantity,
                        Flags = 0,
                        Date = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
                    }
                };
                await _handler.SendEventAsync(sellerSession.Client, "MarketplaceRemoteEventListener", "onPlayerRequestClosed", 
                    ByteString.CopyFrom(sellerClosedEvent.ToByteArray()));
            }

            // Отправляем событие покупателю
            var buyerClosedRequest = new ClosedRequest
            {
                Id = saleId,
                OriginId = transactionId,
                ItemDefinitionId = listing.ItemDefinitionId,
                Price = listing.Price,
                CreateDate = new DateTimeOffset(listing.CreatedAt).ToUnixTimeMilliseconds(),
                CloseDate = new DateTimeOffset(listing.SoldAt ?? DateTime.UtcNow).ToUnixTimeMilliseconds(),
                Type = MarketRequestType.PurchaseRequest,
                Reason = ClosingReason.SuccessTransaction,
                Quantity = listing.Quantity,
                /*Creator = new Axlebolt.Bolt.Protobuf.Player 
                {
                    Id = await GetPlayerObjectIdByClientAsync(client) ?? "",
                    Uid = await GetPlayerObjectIdByClientAsync(client) ?? "",
                    Name = player?.Name ?? "Unknown",
                    PlayerStatus = new PlayerStatus { OnlineStatus = Axlebolt.Bolt.Protobuf.OnlineStatus.StateOnline }
                }*/
                Creator = new Player
                {
                    Id = player?.Id.ToString() ?? listing.BuyerId,
                    Uid = player?.PlayerUid ?? listing.BuyerId,
                    Name = player?.Name ?? listing.BuyerName ?? "Unknown",
                    AvatarId = player?.AvatarId ?? player?.Avatar ?? "",
                    TimeInGame = player?.TimeInGame ?? 0,
                    PlayerStatus = null,
                    LogoutDate = 0,
                    RegistrationDate = player != null ? new DateTimeOffset(player.RegistrationDate).ToUnixTimeSeconds() : 0
                },
                PartnerRequestId = saleId,
                Partner = new Player
                {
                    Id = seller?.Id.ToString() ?? listing.SellerId,
                    Uid = seller?.PlayerUid ?? listing.SellerId,
                    Name = seller?.Name ?? listing.SellerName ?? "Unknown",
                    AvatarId = seller?.AvatarId ?? seller?.Avatar ?? "",
                    TimeInGame = seller?.TimeInGame ?? 0,
                    PlayerStatus = null,
                    LogoutDate = 0,
                    RegistrationDate = seller != null ? new DateTimeOffset(seller.RegistrationDate).ToUnixTimeSeconds() : 0
                }
            };

            var buyerClosedEvent = new OnPlayerRequestClosedEvent 
            { 
                Request = buyerClosedRequest,
                Item = new InventoryItem()
                {
                    Id = listing.InventoryItemId,
                    ItemDefinitionId = listing.ItemDefinitionId,
                    Quantity = listing.Quantity,
                    Flags = 0,
                    Date = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
                }
            };
            await _handler.SendEventAsync(client, "MarketplaceRemoteEventListener", "onPlayerRequestClosed", 
                ByteString.CopyFrom(buyerClosedEvent.ToByteArray()));

            // Отправляем событие о закрытии торговой заявки всем подписчикам
            var tradeSaleClosedEvent = new OnTradeRequestClosedEvent { Request = tradeSaleClosedRequest };
            var tradePurchaseClosedEvent = new OnTradeRequestClosedEvent { Request = tradePurchaseClosedRequest };
            await _handler.BroadcastEventAsync($"marketplace_trade_{listing.ItemDefinitionId}", "MarketplaceRemoteEventListener", "onTradeRequestClosed",
                ByteString.CopyFrom(tradeSaleClosedEvent.ToByteArray()));
            await _handler.BroadcastEventAsync($"marketplace_trade_{listing.ItemDefinitionId}", "MarketplaceRemoteEventListener", "onTradeRequestClosed",
                ByteString.CopyFrom(tradePurchaseClosedEvent.ToByteArray()));

            // Обновляем торговую информацию
            await SendTradeUpdatedEventAsync(listing.ItemDefinitionId);

            var requestIdStr = new Axlebolt.RpcSupport.Protobuf.String { Value = transactionId };
            var result = new BinaryValue
            {
                IsNull = false,
                One = ByteString.CopyFrom(requestIdStr.ToByteArray())
            };

            await _handler.WriteProtoResponseAsync(client, request.Id, result, null);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ CreatePurchaseRequestBySale: {ex.Message}");
            await SendErrorAsync(client, request.Id, 500);
        }
    }

    private async Task CancelRequestAsync(TcpClient client, RpcRequest request)
    {
        try
        {
            Console.WriteLine("💰 CancelRequest");

            string requestId = "";
            if (request.Params.Count > 0 && request.Params[0].One != null)
            {
                var args = CancelRequestArgs.Parser.ParseFrom(request.Params[0].One);
                requestId = args.Id;
            }

            var session = _sessionManager.GetSessionByClient(client);
            var player = session != null ? await _database.GetPlayerByTokenAsync(session.Token) : null;

            // Ищем в листингах продаж
            var listingCollection = _database.GetCollection<Models.MarketplaceListing>("marketplace_listings");
            var listing = await listingCollection.Find(x => x.ListingId == requestId).FirstOrDefaultAsync();

            if (listing != null && listing.SellerId == await GetPlayerObjectIdByClientAsync(client))
            {
                listing.Status = Models.ListingStatus.Cancelled;
                await listingCollection.ReplaceOneAsync(x => x.ListingId == requestId, listing);

                // Возвращаем предмет в инвентарь
                if (player != null)
                {
                    var random = new Random();
                    player.Inventory.Items.Add(new Models.PlayerInventoryItem
                    {
                        Id = listing.InventoryItemId,
                        DefinitionId = listing.ItemDefinitionId,
                        Quantity = listing.Quantity,
                        Date = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
                    });
                    await _database.UpdatePlayerAsync(player);
                }

                // Отправляем событие об отмене
                var closedRequest = new ClosedRequest
                {
                    Id = requestId,
                    OriginId = requestId,
                    ItemDefinitionId = listing.ItemDefinitionId,
                    Price = listing.Price,
                    CreateDate = new DateTimeOffset(listing.CreatedAt).ToUnixTimeMilliseconds(),
                    CloseDate = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                    Type = MarketRequestType.SaleRequest,
                    Reason = ClosingReason.Cancelled,
                    Quantity = listing.Quantity,
                    /*Creator = new Axlebolt.Bolt.Protobuf.Player 
                    {
                        Id = await GetPlayerObjectIdByClientAsync(client) ?? listing.SellerId,
                        Uid = await GetPlayerObjectIdByClientAsync(client) ?? listing.SellerId,
                        Name = player?.Name ?? listing.SellerName,
                        PlayerStatus = new PlayerStatus { OnlineStatus = Axlebolt.Bolt.Protobuf.OnlineStatus.StateOnline }
                    }*/
                    Creator = new Player
                    {
                        Id = player?.Id.ToString() ?? listing.SellerId,
                        Uid = player?.PlayerUid ?? listing.SellerId,
                        Name = player?.Name ?? listing.SellerName ?? "Unknown",
                        AvatarId = player?.AvatarId ?? player?.Avatar ?? "",
                        TimeInGame = player?.TimeInGame ?? 0,
                        PlayerStatus = null,
                        LogoutDate = 0,
                        RegistrationDate = player != null ? new DateTimeOffset(player.RegistrationDate).ToUnixTimeSeconds() : 0
                    }
                };

                var closedEvent = new OnPlayerRequestClosedEvent 
                { 
                    Request = closedRequest,
                    Item = new InventoryItem()
                    {
                        Id = listing.InventoryItemId,
                        ItemDefinitionId = listing.ItemDefinitionId,
                        Quantity = listing.Quantity,
                        Flags = 0,
                        Date = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
                    }
                };
                await _handler.SendEventAsync(client, "MarketplaceRemoteEventListener", "onPlayerRequestClosed", 
                    ByteString.CopyFrom(closedEvent.ToByteArray()));

                var tradeClosedEvent = new OnTradeRequestClosedEvent { Request = closedRequest };
                await _handler.BroadcastEventAsync($"marketplace_trade_{listing.ItemDefinitionId}", "MarketplaceRemoteEventListener", "onTradeRequestClosed",
                    ByteString.CopyFrom(tradeClosedEvent.ToByteArray()));

                await SendTradeUpdatedEventAsync(listing.ItemDefinitionId);
                Console.WriteLine($"💰 Cancelled sale: {requestId}");
            }

            // Ищем в запросах на покупку
            var purchaseCollection = _database.GetCollection<Models.MarketplacePurchaseRequest>("marketplace_purchases");
            var purchase = await purchaseCollection.Find(x => x.RequestId == requestId).FirstOrDefaultAsync();

            if (purchase != null && purchase.BuyerId == await GetPlayerObjectIdByClientAsync(client))
            {
                purchase.Status = Models.ListingStatus.Cancelled;
                await purchaseCollection.ReplaceOneAsync(x => x.RequestId == requestId, purchase);

                // Отправляем событие об отмене
                var closedRequest = new ClosedRequest
                {
                    Id = requestId,
                    OriginId = requestId,
                    ItemDefinitionId = purchase.ItemDefinitionId,
                    Price = purchase.MaxPrice,
                    CreateDate = new DateTimeOffset(purchase.CreatedAt).ToUnixTimeMilliseconds(),
                    CloseDate = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                    Type = MarketRequestType.PurchaseRequest,
                    Reason = ClosingReason.Cancelled,
                    Quantity = purchase.Quantity,
                    /*Creator = new Axlebolt.Bolt.Protobuf.Player 
                    {
                        Id = await GetPlayerObjectIdByClientAsync(client) ?? purchase.BuyerId,
                        Uid = await GetPlayerObjectIdByClientAsync(client) ?? purchase.BuyerId,
                        Name = player?.Name ?? purchase.BuyerName,
                        PlayerStatus = new PlayerStatus { OnlineStatus = Axlebolt.Bolt.Protobuf.OnlineStatus.StateOnline }
                    }*/
                    Creator = new Player
                    {
                        Id = player?.Id.ToString() ?? purchase.BuyerId,
                        Uid = player?.PlayerUid ?? purchase.BuyerId,
                        Name = player?.Name ?? purchase.BuyerName ?? "Unknown",
                        AvatarId = player?.AvatarId ?? player?.Avatar ?? "",
                        TimeInGame = player?.TimeInGame ?? 0,
                        PlayerStatus = null,
                        LogoutDate = 0,
                        RegistrationDate = player != null ? new DateTimeOffset(player.RegistrationDate).ToUnixTimeSeconds() : 0
                    }
                };

                var closedEvent = new OnPlayerRequestClosedEvent 
                { 
                    Request = closedRequest,
                    Item = new InventoryItem()
                    {
                        //Id = 0,//listing.InventoryItemId,
                        ItemDefinitionId = purchase.ItemDefinitionId,
                        Quantity = purchase.Quantity,
                        Flags = 0,
                        Date = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
                    }
                };
                await _handler.SendEventAsync(client, "MarketplaceRemoteEventListener", "onPlayerRequestClosed", 
                    ByteString.CopyFrom(closedEvent.ToByteArray()));

                var tradeClosedEvent = new OnTradeRequestClosedEvent { Request = closedRequest };
                await _handler.BroadcastEventAsync($"marketplace_trade_{purchase.ItemDefinitionId}", "MarketplaceRemoteEventListener", "onTradeRequestClosed",
                    ByteString.CopyFrom(tradeClosedEvent.ToByteArray()));

                await SendTradeUpdatedEventAsync(purchase.ItemDefinitionId);
                Console.WriteLine($"💰 Cancelled purchase: {requestId}");
            }

            var result = new BinaryValue { IsNull = true };
            await _handler.WriteProtoResponseAsync(client, request.Id, result, null);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ CancelRequest: {ex.Message}");
            await SendErrorAsync(client, request.Id, 500);
        }
    }


    private async Task GetTradeAsync(TcpClient client, RpcRequest request)
    {
        try
        {
            int itemDefinitionId = 0;
            if (request.Params.Count > 0 && request.Params[0].One != null)
            {
                var args = GetTradeArgs.Parser.ParseFrom(request.Params[0].One);
                itemDefinitionId = args.Id;
            }

            Console.WriteLine($"🛒 GetTrade: ItemDefId={itemDefinitionId}");

            // Подписываем клиента на обновления торгов этого предмета
            _sessionManager.Subscribe(client, $"marketplace_trade_{itemDefinitionId}");

            var listingCollection = _database.GetCollection<Models.MarketplaceListing>("marketplace_listings");
            var salesFilter = Builders<Models.MarketplaceListing>.Filter.And(
                Builders<Models.MarketplaceListing>.Filter.Eq(x => x.ItemDefinitionId, itemDefinitionId),
                Builders<Models.MarketplaceListing>.Filter.Eq(x => x.Status, Models.ListingStatus.Active),
                Builders<Models.MarketplaceListing>.Filter.Gt(x => x.ExpiresAt, DateTime.UtcNow)
            );

            var sales = await listingCollection.Find(salesFilter).ToListAsync();

            var purchaseCollection = _database.GetCollection<Models.MarketplacePurchaseRequest>("marketplace_purchases");
            var purchasesFilter = Builders<Models.MarketplacePurchaseRequest>.Filter.And(
                Builders<Models.MarketplacePurchaseRequest>.Filter.Eq(x => x.ItemDefinitionId, itemDefinitionId),
                Builders<Models.MarketplacePurchaseRequest>.Filter.Eq(x => x.Status, Models.ListingStatus.Active)
            );
            var purchases = await purchaseCollection.Find(purchasesFilter).ToListAsync();

            if (sales.Count == 0 && purchases.Count == 0)
            {
                var result = new BinaryValue { IsNull = true };
                await _handler.WriteProtoResponseAsync(client, request.Id, result, null);
                return;
            }

            var trade = new Trade
            {
                Id = itemDefinitionId,
                SalesPrice = sales.Count > 0 ? sales.Min(x => x.Price) : 0,
                PurchasesPrice = purchases.Count > 0 ? purchases.Max(x => x.MaxPrice) : 0,
                SalesCount = sales.Count,
                PurchasesCount = purchases.Count
            };

            var resultWithTrade = new BinaryValue
            {
                IsNull = false,
                One = ByteString.CopyFrom(trade.ToByteArray())
            };

            await _handler.WriteProtoResponseAsync(client, request.Id, resultWithTrade, null);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ GetTrade: {ex.Message}");
            await SendErrorAsync(client, request.Id, 500);
        }
    }

    private async Task GetTradesAsync(TcpClient client, RpcRequest request)
    {
        try
        {
            Console.WriteLine("🛒 GetTrades");

            var itemDefinitionIds = new List<int>();
            if (request.Params.Count > 0 && request.Params[0].One != null)
            {
                var args = GetTradesArgs.Parser.ParseFrom(request.Params[0].One);
                itemDefinitionIds.AddRange(args.ItemDefinitionIds);
            }

            var listingCollection = _database.GetCollection<Models.MarketplaceListing>("marketplace_listings");
            var purchaseCollection = _database.GetCollection<Models.MarketplacePurchaseRequest>("marketplace_purchases");

            var result = new BinaryValue { IsNull = false };

            foreach (var itemDefId in itemDefinitionIds)
            {
                var salesFilter = Builders<Models.MarketplaceListing>.Filter.And(
                    Builders<Models.MarketplaceListing>.Filter.Eq(x => x.ItemDefinitionId, itemDefId),
                    Builders<Models.MarketplaceListing>.Filter.Eq(x => x.Status, Models.ListingStatus.Active),
                    Builders<Models.MarketplaceListing>.Filter.Gt(x => x.ExpiresAt, DateTime.UtcNow)
                );
                var sales = await listingCollection.Find(salesFilter).ToListAsync();

                var purchasesFilter = Builders<Models.MarketplacePurchaseRequest>.Filter.And(
                    Builders<Models.MarketplacePurchaseRequest>.Filter.Eq(x => x.ItemDefinitionId, itemDefId),
                    Builders<Models.MarketplacePurchaseRequest>.Filter.Eq(x => x.Status, Models.ListingStatus.Active)
                );
                var purchases = await purchaseCollection.Find(purchasesFilter).ToListAsync();

                var trade = new Trade
                {
                    Id = itemDefId,
                    SalesPrice = sales.Count > 0 ? sales.Min(x => x.Price) : 0,
                    PurchasesPrice = purchases.Count > 0 ? purchases.Max(x => x.MaxPrice) : 0,
                    SalesCount = sales.Count,
                    PurchasesCount = purchases.Count
                };

                result.Array.Add(ByteString.CopyFrom(trade.ToByteArray()));
            }

            Console.WriteLine($"🛒 Returning {result.Array.Count} trades");
            await _handler.WriteProtoResponseAsync(client, request.Id, result, null);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ GetTrades: {ex.Message}");
            await SendErrorAsync(client, request.Id, 500);
        }
    }

    private async Task GetTradeOpenSaleRequestsAsync(TcpClient client, RpcRequest request)
    {
        try
        {
            int itemDefinitionId = 0;
            int page = 0, size = 1000; // Убрали лимит - показываем все

            if (request.Params.Count > 0 && request.Params[0].One != null)
            {
                var args = GetTradeOpenSaleRequestsArgs.Parser.ParseFrom(request.Params[0].One);
                itemDefinitionId = args.Id;
                page = args.Page;
                size = 1000; // Игнорируем size от клиента
            }

            Console.WriteLine($"🛒 GetTradeOpenSaleRequests: ItemDefId={itemDefinitionId}, Page={page}, Size={size}");

            // Подписываем клиента на обновления торгов этого предмета
            _sessionManager.Subscribe(client, $"marketplace_trade_{itemDefinitionId}");
            
            // Логируем все активные листинги для отладки
            var allListings = _database.GetCollection<Models.MarketplaceListing>("marketplace_listings");
            var allActive = await allListings.Find(x => x.Status == Models.ListingStatus.Active).ToListAsync();
            Console.WriteLine($"🛒 Total active listings in DB: {allActive.Count}");
            foreach (var l in allActive.Take(5))
            {
                Console.WriteLine($"   - ListingId={l.ListingId}, ItemDefId={l.ItemDefinitionId}, Price={l.Price}, Seller={l.SellerName}");
            }

            var listingCollection = _database.GetCollection<Models.MarketplaceListing>("marketplace_listings");
            var filter = Builders<Models.MarketplaceListing>.Filter.And(
                Builders<Models.MarketplaceListing>.Filter.Eq(x => x.ItemDefinitionId, itemDefinitionId),
                Builders<Models.MarketplaceListing>.Filter.Eq(x => x.Status, Models.ListingStatus.Active),
                Builders<Models.MarketplaceListing>.Filter.Gt(x => x.ExpiresAt, DateTime.UtcNow)
            );

            var listings = await listingCollection
                .Find(filter)
                .SortBy(x => x.Price)
                .ThenBy(x => x.CreatedAt)
                .Skip(page * size)
                .Limit(size)
                .ToListAsync();

            Console.WriteLine($"🛒 Found {listings.Count} sale requests");

            var result = new BinaryValue { IsNull = false };
            foreach (var listing in listings)
            {
                // Получаем информацию о продавце
                var seller = await _database.GetPlayerByObjectIdAsync(listing.SellerId);
                
                /*var creatorPlayer = new Player
                {
                    Id = seller?.PlayerUid ?? listing.SellerId,
                    Uid = seller?.PlayerUid ?? listing.SellerId,
                    Name = seller?.Name ?? listing.SellerName ?? "Unknown",
                    AvatarId = seller?.AvatarId ?? seller?.Avatar ?? "",
                    TimeInGame = seller?.TimeInGame ?? 0,
                    PlayerStatus = new PlayerStatus { OnlineStatus = seller?.OnlineStatus == Models.OnlineStatus.Online ? OnlineStatus.StateOnline : OnlineStatus.StateOffline },
                    LogoutDate = seller != null ? new DateTimeOffset(seller.LastLogin).ToUnixTimeSeconds() : 0,
                    RegistrationDate = seller != null ? new DateTimeOffset(seller.RegistrationDate).ToUnixTimeSeconds() : 0
                };*/
                
                var creatorPlayer = new Player
                {
                    Id = seller?.Id.ToString() ?? listing.SellerId,
                    Uid = seller?.PlayerUid ?? listing.SellerId,
                    Name = seller?.Name ?? listing.SellerName ?? "Unknown",
                    AvatarId = seller?.AvatarId ?? seller?.Avatar ?? "",
                    TimeInGame = seller?.TimeInGame ?? 0,
                    PlayerStatus = null,
                    LogoutDate = 0,
                    RegistrationDate = seller != null ? new DateTimeOffset(seller.RegistrationDate).ToUnixTimeSeconds() : 0
                };
                
                var openRequest = new OpenRequest
                {
                    Id = listing.ListingId,
                    Creator = creatorPlayer,
                    ItemDefinitionId = listing.ItemDefinitionId,
                    Price = listing.Price,
                    CreateDate = new DateTimeOffset(listing.CreatedAt).ToUnixTimeMilliseconds(),
                    Type = MarketRequestType.SaleRequest,
                    Quantity = listing.Quantity
                };
                
                Console.WriteLine($"🛒 Sale: Id={listing.ListingId}, Item={listing.ItemDefinitionId}, Price={listing.Price}, Seller={creatorPlayer.Name}");
                result.Array.Add(ByteString.CopyFrom(openRequest.ToByteArray()));
            }

            await _handler.WriteProtoResponseAsync(client, request.Id, result, null);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ GetTradeOpenSaleRequests: {ex.Message}");
            await SendErrorAsync(client, request.Id, 500);
        }
    }

    private async Task GetTradeOpenPurchaseRequestsAsync(TcpClient client, RpcRequest request)
    {
        try
        {
            int itemDefinitionId = 0;
            int page = 0, size = 1000; // Убрали лимит - показываем все

            if (request.Params.Count > 0 && request.Params[0].One != null)
            {
                var args = GetTradeOpenPurchaseRequestsArgs.Parser.ParseFrom(request.Params[0].One);
                itemDefinitionId = args.Id;
                page = args.Page;
                size = 1000; // Игнорируем size от клиента
            }

            Console.WriteLine($"🛒 GetTradeOpenPurchaseRequests: ItemDefId={itemDefinitionId}");

            // Подписываем клиента на обновления торгов этого предмета
            _sessionManager.Subscribe(client, $"marketplace_trade_{itemDefinitionId}");

            var purchaseCollection = _database.GetCollection<Models.MarketplacePurchaseRequest>("marketplace_purchases");
            var filter = Builders<Models.MarketplacePurchaseRequest>.Filter.And(
                Builders<Models.MarketplacePurchaseRequest>.Filter.Eq(x => x.ItemDefinitionId, itemDefinitionId),
                Builders<Models.MarketplacePurchaseRequest>.Filter.Eq(x => x.Status, Models.ListingStatus.Active),
                Builders<Models.MarketplacePurchaseRequest>.Filter.Gt(x => x.ExpiresAt, DateTime.UtcNow)
            );

            var purchases = await purchaseCollection
                .Find(filter)
                .SortByDescending(x => x.MaxPrice)
                .Skip(page * size)
                .Limit(size)
                .ToListAsync();

            Console.WriteLine($"🛒 Found {purchases.Count} purchase requests");

            var result = new BinaryValue { IsNull = false };
            foreach (var purchase in purchases)
            {
                // Получаем информацию о покупателе
                var buyer = await _database.GetPlayerByObjectIdAsync(purchase.BuyerId);
                
                /*var creatorPlayer = new Player
                {
                    Id = buyer?.PlayerUid ?? purchase.BuyerId,
                    Uid = buyer?.PlayerUid ?? purchase.BuyerId,
                    Name = buyer?.Name ?? purchase.BuyerName ?? "Unknown",
                    AvatarId = buyer?.AvatarId ?? buyer?.Avatar ?? "",
                    TimeInGame = buyer?.TimeInGame ?? 0,
                    PlayerStatus = new PlayerStatus { OnlineStatus = buyer?.OnlineStatus == Models.OnlineStatus.Online ? OnlineStatus.StateOnline : OnlineStatus.StateOffline },
                    LogoutDate = buyer != null ? new DateTimeOffset(buyer.LastLogin).ToUnixTimeSeconds() : 0,
                    RegistrationDate = buyer != null ? new DateTimeOffset(buyer.RegistrationDate).ToUnixTimeSeconds() : 0
                };*/
                
                var creatorPlayer = new Player
                {
                    Id = buyer?.Id.ToString() ?? purchase.BuyerId,
                    Uid = buyer?.PlayerUid ?? purchase.BuyerId,
                    Name = buyer?.Name ?? purchase.BuyerName ?? "Unknown",
                    AvatarId = buyer?.AvatarId ?? buyer?.Avatar ?? "",
                    TimeInGame = buyer?.TimeInGame ?? 0,
                    PlayerStatus = null,
                    LogoutDate = 0,
                    RegistrationDate = buyer != null ? new DateTimeOffset(buyer.RegistrationDate).ToUnixTimeSeconds() : 0
                };
                
                var openRequest = new OpenRequest
                {
                    Id = purchase.RequestId,
                    Creator = creatorPlayer,
                    ItemDefinitionId = purchase.ItemDefinitionId,
                    Price = purchase.MaxPrice,
                    CreateDate = new DateTimeOffset(purchase.CreatedAt).ToUnixTimeMilliseconds(),
                    Type = MarketRequestType.PurchaseRequest,
                    Quantity = purchase.Quantity
                };
                
                Console.WriteLine($"🛒 Purchase: Id={purchase.RequestId}, Item={purchase.ItemDefinitionId}, Price={purchase.MaxPrice}, Buyer={creatorPlayer.Name}");
                result.Array.Add(ByteString.CopyFrom(openRequest.ToByteArray()));
            }

            await _handler.WriteProtoResponseAsync(client, request.Id, result, null);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ GetTradeOpenPurchaseRequests: {ex.Message}");
            await SendErrorAsync(client, request.Id, 500);
        }
    }

    private async Task SendTradeUpdatedEventAsync(int itemDefinitionId)
    {
        try
        {
            var listingCollection = _database.GetCollection<Models.MarketplaceListing>("marketplace_listings");
            var salesFilter = Builders<Models.MarketplaceListing>.Filter.And(
                Builders<Models.MarketplaceListing>.Filter.Eq(x => x.ItemDefinitionId, itemDefinitionId),
                Builders<Models.MarketplaceListing>.Filter.Eq(x => x.Status, Models.ListingStatus.Active),
                Builders<Models.MarketplaceListing>.Filter.Gt(x => x.ExpiresAt, DateTime.UtcNow)
            );
            var sales = await listingCollection.Find(salesFilter).ToListAsync();

            var purchaseCollection = _database.GetCollection<Models.MarketplacePurchaseRequest>("marketplace_purchases");
            var purchasesFilter = Builders<Models.MarketplacePurchaseRequest>.Filter.And(
                Builders<Models.MarketplacePurchaseRequest>.Filter.Eq(x => x.ItemDefinitionId, itemDefinitionId),
                Builders<Models.MarketplacePurchaseRequest>.Filter.Eq(x => x.Status, Models.ListingStatus.Active)
            );
            var purchases = await purchaseCollection.Find(purchasesFilter).ToListAsync();

            var trade = new Trade
            {
                Id = itemDefinitionId,
                SalesPrice = sales.Count > 0 ? sales.Min(x => x.Price) : 0,
                PurchasesPrice = purchases.Count > 0 ? purchases.Max(x => x.MaxPrice) : 0,
                SalesCount = sales.Count,
                PurchasesCount = purchases.Count
            };

            var tradeUpdatedEvent = new OnTradeUpdatedEvent { Trade = trade };
            /*await _handler.BroadcastEventAsync($"marketplace_trade_{itemDefinitionId}", "MarketplaceRemoteEventListener", "onTradeUpdated",
                ByteString.CopyFrom(tradeUpdatedEvent.ToByteArray()));*/
            
            var clients = _sessionManager
                .GetAllSessions()
                .Select(s => s.Client)
                .Where(c => c != null && c.Connected)
                .Distinct()
                .ToList();

            foreach (var client in clients)
            {
                try
                {
                    await _handler.SendEventAsync(client, "MarketplaceRemoteEventListener", "onTradeUpdated",
                        ByteString.CopyFrom(tradeUpdatedEvent.ToByteArray()));
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"❌ BroadcastEvent error: {ex.Message}");
                }
            }
            
            Console.WriteLine($"📤 Sent onTradeUpdated event for item {itemDefinitionId}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ SendTradeUpdatedEvent: {ex.Message}");
        }
    }

    private async Task SendErrorAsync(TcpClient client, string guid, int code)
    {
        await _handler.WriteProtoResponseAsync(client, guid, null,
            new RpcException { Id = guid, Code = code });
    }

    private async Task<string?> GetPlayerObjectIdByClientAsync(TcpClient client)
    {
        var session = _sessionManager.GetSessionByClient(client);
        if (session == null) return null;

        var player = await _database.GetPlayerByTokenAsync(session.Token);
        return player?.Id.ToString();
    }
}
