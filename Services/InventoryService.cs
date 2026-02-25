using System.Net.Sockets;
using Axlebolt.RpcSupport.Protobuf;
using Axlebolt.Bolt.Protobuf;
using Axlebolt.Bolt.Protobuf2;
using StandRiseServer.Core;
using Google.Protobuf;
using MongoDB.Bson;
using MongoDB.Driver;

namespace StandRiseServer.Services;

public class InventoryService
{
    private readonly ProtobufHandler _handler;
    private readonly DatabaseService _database;
    private readonly SessionManager _sessionManager;
    private const string ServiceName = "InventoryRemoteService";

    public InventoryService(ProtobufHandler handler, DatabaseService database, SessionManager sessionManager)
    {
        _handler = handler;
        _database = database;
        _sessionManager = sessionManager;
        
        _handler.RegisterHandler(ServiceName, "getInventoryItemDefinitions", GetItemDefinitionsAsync);
        _handler.RegisterHandler(ServiceName, "getInventoryItemPropertyDefinitions", GetItemPropertyDefinitionsAsync);
        _handler.RegisterHandler(ServiceName, "getPlayerInventory", GetPlayerInventoryAsync);
        _handler.RegisterHandler(ServiceName, "getInventory", GetPlayerInventoryAsync);
        _handler.RegisterHandler(ServiceName, "getOtherPlayerItems", GetOtherPlayerItemsAsync);
        _handler.RegisterHandler(ServiceName, "buyInventoryItem", BuyInventoryItemAsync);
        _handler.RegisterHandler(ServiceName, "sellInventoryItem", SellInventoryItemAsync);
        _handler.RegisterHandler(ServiceName, "exchangeInventoryItems", ExchangeInventoryItemsAsync);
        _handler.RegisterHandler(ServiceName, "consumeInventoryItem", ConsumeInventoryItemAsync);
        _handler.RegisterHandler(ServiceName, "transferInventoryItems", TransferInventoryItemsAsync);
        _handler.RegisterHandler(ServiceName, "tradeInventoryItems", TradeInventoryItemsAsync);
        _handler.RegisterHandler(ServiceName, "setInventoryItemFlags", SetInventoryItemFlagsAsync);
        _handler.RegisterHandler(ServiceName, "setInventoryItemsProperties", SetInventoryItemsPropertiesAsync);
        _handler.RegisterHandler(ServiceName, "generateCoupon", GenerateCouponAsync);
        _handler.RegisterHandler(ServiceName, "getPlayerCoupons", GetPlayerCouponsAsync);
        _handler.RegisterHandler(ServiceName, "activateCoupon", ActivateCouponAsync);
        _handler.RegisterHandler(ServiceName, "applyInventoryItem", ApplyInventoryItemAsync);
        _handler.RegisterHandler(ServiceName, "removeInventoryItemProperty", RemoveInventoryItemPropertyAsync);
    }
    
    private string GetPlayerUid(TcpClient client)
    {
        var session = _sessionManager.GetSessionByClient(client);
        return session?.Token[..8] ?? "unknown";
    }

    private async Task GetItemDefinitionsAsync(TcpClient client, RpcRequest request)
    {
        try
        {
            Console.WriteLine("📦 GetItemDefinitions request received!");
            Console.WriteLine($"📦 Request ID: {request.Id}");
            
            // Получаем определения из базы данных
            var collection = _database.GetCollection<Models.InventoryItemDefinition>("inventory_definitions");
            var dbDefinitions = await collection.Find(x => x.IsEnabled).ToListAsync();
            
            Console.WriteLine($"📦 Found {dbDefinitions.Count} definitions in database");
            
            // Конвертируем в protobuf формат (используем Unity protobuf классы)
            var definitionsList = new List<Axlebolt.Bolt.Protobuf.InventoryItemDefinition>();
            
            foreach (var dbDef in dbDefinitions)
            {
                var definition = new Axlebolt.Bolt.Protobuf.InventoryItemDefinition
                {
                    Id = dbDef.ItemId,
                    DisplayName = dbDef.DisplayName,
                    CanBeTraded = dbDef.CanBeTraded
                };
                
                // Конвертируем цены
                foreach (var price in dbDef.BuyPrice)
                {
                    definition.BuyPrice.Add(new Axlebolt.Bolt.Protobuf.CurrencyAmount 
                    { 
                        CurrencyId = price.CurrencyId, 
                        Value = price.Value 
                    });
                }
                
                foreach (var price in dbDef.SellPrice)
                {
                    definition.SellPrice.Add(new Axlebolt.Bolt.Protobuf.CurrencyAmount 
                    { 
                        CurrencyId = price.CurrencyId, 
                        Value = price.Value 
                    });
                }
                
                // Конвертируем свойства
                foreach (var prop in dbDef.Properties)
                {
                    definition.Properties.Add(prop.Key, prop.Value);
                }
                
                // ВАЖНО: Добавляем "value" (редкость) если его нет в Properties
                // SkinValue: None=0, Common=1, Uncommon=2, Rare=3, Epic=4, Legendary=5, Arcane=6
                if (!definition.Properties.ContainsKey("value"))
                {
                    var skinValue = dbDef.Rarity switch
                    {
                        "Common" => "1",
                        "Uncommon" => "2",
                        "Rare" => "3",
                        "Epic" => "4",
                        "Legendary" => "5",
                        "Arcane" => "6",
                        _ => "1"
                    };
                    definition.Properties.Add("value", skinValue);
                }
                
                // Добавляем collection если это скин и нет collection
                // ВАЖНО: Убираем пробелы из названий коллекций!
                // Клиент ожидает "DigitalCollection", а не "Digital Collection"
                if (!definition.Properties.ContainsKey("collection") && 
                    (dbDef.Category == "weapon" || !string.IsNullOrEmpty(dbDef.Collection)))
                {
                    var collectionName = !string.IsNullOrEmpty(dbDef.Collection) ? dbDef.Collection : "Origin";
                    // Убираем пробелы и приводим к формату enum
                    collectionName = NormalizeCollectionName(collectionName);
                    definition.Properties.Add("collection", collectionName);
                }
                
                // Также проверяем существующее свойство collection и нормализуем его
                if (definition.Properties.ContainsKey("collection"))
                {
                    definition.Properties["collection"] = NormalizeCollectionName(definition.Properties["collection"]);
                }
                
                definitionsList.Add(definition);
            }
            
            // Создаем массив для RPC ответа (как в GameSettingsService)
            var result = new BinaryValue { IsNull = false };
            
            // Добавляем каждый элемент в массив
            foreach (var def in definitionsList)
            {
                var bytes = def.ToByteArray();
                Console.WriteLine($"📦 Serializing item {def.Id}: {bytes.Length} bytes");
                result.Array.Add(ByteString.CopyFrom(bytes));
            }
            
            Console.WriteLine($"📦 Sending {definitionsList.Count} inventory definitions");
            Console.WriteLine($"📦 Array elements: {result.Array.Count}");
            Console.WriteLine($"📦 Total response size: {result.Array.Sum(x => x.Length)} bytes");
            
            // Отладочная информация о первых нескольких предметах
            foreach (var def in definitionsList.Take(3))
            {
                Console.WriteLine($"📦 Item: ID={def.Id}, Name={def.DisplayName}, BuyPrice={def.BuyPrice.Count}, SellPrice={def.SellPrice.Count}");
                // Показываем hex первого элемента для отладки
                var itemBytes = def.ToByteArray();
                Console.WriteLine($"📦 Item {def.Id} hex: {BitConverter.ToString(itemBytes.Take(50).ToArray())}");
            }
            
            // Показываем полный BinaryValue
            var resultBytes = result.ToByteArray();
            Console.WriteLine($"📦 Full BinaryValue size: {resultBytes.Length} bytes");
            Console.WriteLine($"📦 BinaryValue hex (first 100): {BitConverter.ToString(resultBytes.Take(100).ToArray())}");
            
            await _handler.WriteProtoResponseAsync(client, request.Id, result, null);
            Console.WriteLine("📦 Response sent successfully!");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ GetItemDefinitions: {ex.Message}");
            Console.WriteLine($"❌ Stack trace: {ex.StackTrace}");
        }
    }

    private async Task GetItemPropertyDefinitionsAsync(TcpClient client, RpcRequest request)
    {
        try
        {
            // Всегда возвращаем пустой массив, а не null
            var result = new BinaryValue { IsNull = false };
            await _handler.WriteProtoResponseAsync(client, request.Id, result, null);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ GetItemPropertyDefinitions: {ex.Message}");
        }
    }

    private async Task GetPlayerInventoryAsync(TcpClient client, RpcRequest request)
    {
        try
        {
            Console.WriteLine("🎒 GetPlayerInventory Request");
            
            // Получаем игрока из сессии
            var session = _sessionManager.GetSessionByClient(client);
            if (session == null)
            {
                session = _sessionManager.GetAllSessions().FirstOrDefault();
                if (session != null) session.Client = client;
            }

            var playerInventory = new Axlebolt.Bolt.Protobuf.PlayerInventory();
            
            if (session != null)
            {
                var player = await _database.GetPlayerByTokenAsync(session.Token);
                if (player != null)
                {
                    // Берем валюту из базы данных игрока
                    // Silver = 101, Gold = 102 (как в оригинальном клиенте)
                    playerInventory.Currencies.Add(new Axlebolt.Bolt.Protobuf.CurrencyAmount 
                    { 
                        CurrencyId = 101, // Silver
                        Value = player.Coins 
                    });
                    playerInventory.Currencies.Add(new Axlebolt.Bolt.Protobuf.CurrencyAmount 
                    { 
                        CurrencyId = 102, // Gold
                        Value = player.Gems 
                    });
                    
                    // Получаем все валидные definition IDs
                    var defCollection = _database.GetCollection<Models.InventoryItemDefinition>("inventory_definitions");
                    var validDefIds = await defCollection.Find(_ => true)
                        .Project(x => x.ItemId)
                        .ToListAsync();
                    var validDefIdsSet = validDefIds.ToHashSet();
                    
                    // Добавляем предметы из инвентаря игрока (только с валидными definitions)
                    var invalidItems = new List<Models.PlayerInventoryItem>();
                    foreach (var item in player.Inventory.Items)
                    {
                        // Проверяем что definition существует
                        if (!validDefIdsSet.Contains(item.DefinitionId))
                        {
                            Console.WriteLine($"⚠️ Skipping invalid item: Id={item.Id}, DefId={item.DefinitionId} (definition not found)");
                            invalidItems.Add(item);
                            continue;
                        }
                        
                        playerInventory.InventoryItems.Add(new Axlebolt.Bolt.Protobuf.InventoryItem
                        {
                            Id = item.Id,  // int, не string!
                            ItemDefinitionId = item.DefinitionId,
                            Quantity = item.Quantity,
                            Flags = item.Flags,
                            Date = item.Date
                        });
                    }
                    
                    // Удаляем невалидные предметы из инвентаря игрока
                    if (invalidItems.Count > 0)
                    {
                        foreach (var invalidItem in invalidItems)
                        {
                            player.Inventory.Items.Remove(invalidItem);
                        }
                        await _database.UpdatePlayerAsync(player);
                        Console.WriteLine($"🗑️ Removed {invalidItems.Count} invalid items from player inventory");
                    }
                    
                    Console.WriteLine($"🎒 Player {player.Name}: Silver={player.Coins}, Gold={player.Gems}, Keys={player.Keys}, Items={player.Inventory.Items.Count}");
                }
                else
                {
                    // Дефолтные значения если игрок не найден
                    AddDefaultCurrencies(playerInventory);
                }
            }
            else
            {
                AddDefaultCurrencies(playerInventory);
            }
            
            Console.WriteLine($"🎒 Sending inventory with {playerInventory.Currencies.Count} currencies");
            
            var result = new BinaryValue 
            { 
                IsNull = false, 
                One = ByteString.CopyFrom(playerInventory.ToByteArray()) 
            };
            
            await _handler.WriteProtoResponseAsync(client, request.Id, result, null);
            Console.WriteLine("🎒 GetPlayerInventory response sent");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ GetPlayerInventory: {ex.Message}");
        }
    }

    private void AddDefaultCurrencies(Axlebolt.Bolt.Protobuf.PlayerInventory inventory)
    {
        // Silver = 101, Gold = 102 (как в оригинальном клиенте)
        inventory.Currencies.Add(new Axlebolt.Bolt.Protobuf.CurrencyAmount { CurrencyId = 101, Value = 0 });
        inventory.Currencies.Add(new Axlebolt.Bolt.Protobuf.CurrencyAmount { CurrencyId = 102, Value = 0 });
    }

    private async Task BuyInventoryItemAsync(TcpClient client, RpcRequest request)
    {
        try
        {
            var session = _sessionManager.GetSessionByClient(client);
            if (session == null)
            {
                session = _sessionManager.GetAllSessions().FirstOrDefault();
                if (session != null) session.Client = client;
            }
            
            int definitionId = 0;
            int quantity = 1;
            int currencyId = 101;
            
            if (request.Params.Count > 0 && request.Params[0].One != null)
            {
                var defIdInt = Axlebolt.RpcSupport.Protobuf.Integer.Parser.ParseFrom(request.Params[0].One);
                definitionId = defIdInt.Value;
            }
            if (request.Params.Count > 1 && request.Params[1].One != null)
            {
                var qtyInt = Axlebolt.RpcSupport.Protobuf.Integer.Parser.ParseFrom(request.Params[1].One);
                quantity = qtyInt.Value;
            }
            if (request.Params.Count > 2 && request.Params[2].One != null)
            {
                var currInt = Axlebolt.RpcSupport.Protobuf.Integer.Parser.ParseFrom(request.Params[2].One);
                currencyId = currInt.Value;
            }
            
            // Ограничиваем максимум 50 кейсов за раз
            quantity = Math.Min(quantity, 50);
            
            var playerUid = GetPlayerUid(client);
            Console.WriteLine($"🛒 BuyInventoryItem: defId={definitionId}, qty={quantity}, currency={currencyId}");
            Logger.Service(ServiceName, "1.0", "android", $"buyItem defId={definitionId} qty={quantity}", playerUid);
            
            // Получаем игрока
            var player = session != null ? await _database.GetPlayerByTokenAsync(session.Token) : null;
            
            // Получаем определение предмета
            var defCollection = _database.GetCollection<Models.InventoryItemDefinition>("inventory_definitions");
            var definition = await defCollection.Find(x => x.ItemId == definitionId).FirstOrDefaultAsync();
            
            if (definition == null)
            {
                Console.WriteLine($"❌ Definition {definitionId} not found, creating default");
                definition = new Models.InventoryItemDefinition
                {
                    ItemId = definitionId,
                    DisplayName = $"Item_{definitionId}",
                    BuyPrice = new List<Models.CurrencyPrice> 
                    { 
                        new Models.CurrencyPrice { CurrencyId = 102, Value = 100 }
                    }
                };
            }
            
            // Проверяем цену
            var price = definition.BuyPrice.FirstOrDefault(p => p.CurrencyId == currencyId);
            float totalCost = (price?.Value ?? 0) * quantity;
            
            Console.WriteLine($"🛒 Price per item: {price?.Value ?? 0}, Total: {totalCost} (currency {currencyId})");
            
            // Проверяем достаточно ли валюты
            if (player != null)
            {
                float currentBalance = currencyId switch
                {
                    101 => player.Coins,
                    102 => player.Gems,
                    103 => player.Keys,
                    _ => 0
                };
                
                if (currentBalance < totalCost)
                {
                    Console.WriteLine($"❌ Not enough currency! Have: {currentBalance}, Need: {totalCost}");
                    await SendError(client, request.Id, 403, "Insufficient funds");
                    return;
                }
            }
            
            var random = new Random();
            var resultItems = new List<Axlebolt.Bolt.Protobuf.InventoryItem>();
            
            if (player != null)
            {
                // Создаем отдельные предметы для каждой единицы (как просил юзер, чтобы не было стаков)
                for (int i = 0; i < quantity; i++)
                {
                    var newItemId = random.Next(100000, 999999);
                    
                    var itemModel = new Models.PlayerInventoryItem
                    {
                        Id = newItemId,
                        DefinitionId = definitionId,
                        Quantity = 1,
                        Date = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
                    };
                    
                    player.Inventory.Items.Add(itemModel);
                    
                    resultItems.Add(new Axlebolt.Bolt.Protobuf.InventoryItem
                    {
                        Id = newItemId,
                        ItemDefinitionId = definitionId,
                        Quantity = 1,
                        Flags = 0,
                        Date = itemModel.Date
                    });
                }

                Console.WriteLine($"📦 Created {quantity} individual items for definition {definitionId}");
                
                // Списываем валюту
                int cost = (int)totalCost;
                switch (currencyId)
                {
                    case 101:
                        if (player.Coins >= cost)
                        {
                            player.Coins -= cost;
                            Console.WriteLine($"💰 [BUY] Player {player.Name} spent {cost} Silver. {player.Coins + cost} -> {player.Coins}");
                        }
                        else
                        {
                            Console.WriteLine($"⚠️ [BUY] Player {player.Name} insufficient Silver! Have: {player.Coins}, Need: {cost}");
                            player.Coins = 0; // Prevent massive negative if somehow skipped check
                        }
                        break;
                    case 102:
                        if (player.Gems >= cost)
                        {
                            player.Gems -= cost;
                            Console.WriteLine($"💎 [BUY] Player {player.Name} spent {cost} Gold. {player.Gems + cost} -> {player.Gems}");
                        }
                        else
                        {
                            Console.WriteLine($"⚠️ [BUY] Player {player.Name} insufficient Gold! Have: {player.Gems}, Need: {cost}");
                            player.Gems = 0;
                        }
                        break;
                    case 103:
                        if (player.Keys >= cost)
                        {
                            player.Keys -= cost;
                            Console.WriteLine($"🔑 [BUY] Player {player.Name} spent {cost} Keys. {player.Keys + cost} -> {player.Keys}");
                        }
                        else
                        {
                            Console.WriteLine($"⚠️ [BUY] Player {player.Name} insufficient Keys! Have: {player.Keys}, Need: {cost}");
                            player.Keys = 0;
                        }
                        break;
                }
                
                await _database.UpdatePlayerAsync(player);
                Console.WriteLine($"🛒 Player {player.Name} bought {quantity}x item {definitionId}");
                
                // Возвращаем массив купленных предметов
                var result = new BinaryValue { IsNull = false };
                foreach (var item in resultItems)
                {
                    result.Array.Add(ByteString.CopyFrom(item.ToByteArray()));
                }
                
                await _handler.WriteProtoResponseAsync(client, request.Id, result, null);
                Console.WriteLine("🛒 BuyInventoryItem response sent");
            }
            else
            {
                // Нет игрока - возвращаем пустой результат
                var result = new BinaryValue { IsNull = false };
                await _handler.WriteProtoResponseAsync(client, request.Id, result, null);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ BuyInventoryItem: {ex.Message}");
            Console.WriteLine($"❌ Stack: {ex.StackTrace}");
        }
    }

    private async Task SellInventoryItemAsync(TcpClient client, RpcRequest request)
    {
        try
        {
            Console.WriteLine("💰 SellInventoryItem Request");
            
            var result = new BinaryValue { IsNull = true };
            await _handler.WriteProtoResponseAsync(client, request.Id, result, null);
            Console.WriteLine("💰 SellInventoryItem response sent");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ SellInventoryItem: {ex.Message}");
        }
    }
    
    private async Task ExchangeInventoryItemsAsync(TcpClient client, RpcRequest request)
    {
        try
        {
            Console.WriteLine("🔄 ExchangeInventoryItems Request");
            
            var session = _sessionManager.GetSessionByClient(client);
            if (session == null)
            {
                session = _sessionManager.GetAllSessions().FirstOrDefault();
                if (session != null) session.Client = client;
            }
            
            // Парсим параметры: recipeCode, currencies[], inventoryItemIds[]
            string recipeCode = "";
            var currenciesToSpend = new List<Axlebolt.Bolt.Protobuf.CurrencyAmount>();
            var itemIdsToConsume = new List<int>();
            
            // Param 0: recipeCode (string)
            if (request.Params.Count > 0 && request.Params[0].One != null)
            {
                var recipeStr = Axlebolt.RpcSupport.Protobuf.String.Parser.ParseFrom(request.Params[0].One);
                recipeCode = recipeStr.Value;
            }
            
            // Param 1: currencies[] (CurrencyAmount array)
            if (request.Params.Count > 1 && request.Params[1].Array != null)
            {
                foreach (var currBytes in request.Params[1].Array)
                {
                    var curr = Axlebolt.Bolt.Protobuf.CurrencyAmount.Parser.ParseFrom(currBytes);
                    currenciesToSpend.Add(curr);
                    Console.WriteLine($"🔄 Currency to spend: {curr.CurrencyId} = {curr.Value}");
                }
            }
            
            // Param 2: inventoryItemIds[] (int array)
            if (request.Params.Count > 2 && request.Params[2].Array != null)
            {
                foreach (var itemBytes in request.Params[2].Array)
                {
                    var itemId = Axlebolt.RpcSupport.Protobuf.Integer.Parser.ParseFrom(itemBytes);
                    itemIdsToConsume.Add(itemId.Value);
                    Console.WriteLine($"🔄 Item to consume (inventory ID): {itemId.Value}");
                }
            }
            
            Console.WriteLine($"🔄 Recipe code: {recipeCode}, Currencies: {currenciesToSpend.Count}, Items to consume: {itemIdsToConsume.Count}");
            
            // Создаем результат обмена
            var exchangeResult = new Axlebolt.Bolt.Protobuf.ExchangeResult();
            
            // Получаем игрока
            var player = session != null ? await _database.GetPlayerByTokenAsync(session.Token) : null;
            
            // Проверяем специальные рецепты дропа после матча
            bool isMatchDropRecipe = !string.IsNullOrEmpty(recipeCode) && (
                recipeCode.StartsWith("RECIPE_DROP_IN_GAME") ||
                recipeCode.StartsWith("RECIPE_DROP_ON_LVL") ||
                recipeCode.StartsWith("RECIPE_GOOD_GAME_") ||
                recipeCode.StartsWith("RECIPE_DROP_ON_BONUS")
            );
            
            if (isMatchDropRecipe)
            {
                Console.WriteLine($"🎁 Processing match drop recipe: {recipeCode}");
                var random = new Random();
                
                // RECIPE_DROP_ON_LVL - награда за повышение уровня
                if (recipeCode.StartsWith("RECIPE_DROP_ON_LVL"))
                {
                    // Даем голду 100-1000 и серебро 100-2000
                    int goldReward = random.Next(100, 1001);
                    int silverReward = random.Next(100, 2001);
                    
                    // Добавляем валюту в результат
                    exchangeResult.Currencies.Add(new Axlebolt.Bolt.Protobuf.CurrencyAmount 
                    { 
                        CurrencyId = 102, // Gold
                        Value = goldReward 
                    });
                    exchangeResult.Currencies.Add(new Axlebolt.Bolt.Protobuf.CurrencyAmount 
                    { 
                        CurrencyId = 101, // Silver
                        Value = silverReward 
                    });
                    
                    // Обновляем баланс игрока
                    if (player != null)
                    {
                        player.Gems += goldReward;
                        player.Coins += silverReward;
                        await _database.UpdatePlayerAsync(player);
                        Console.WriteLine($"🎁 Level Up reward: +{goldReward} Gold, +{silverReward} Silver for {player.Name}");
                    }
                    
                    // Супер редкий шанс на скин (1%)
                    if (random.Next(100) < 1)
                    {
                        var defCollection = _database.GetCollection<Models.InventoryItemDefinition>("inventory_definitions");
                        var weaponSkins = await defCollection.Find(x => x.Category == "weapon" && x.IsEnabled).ToListAsync();
                        
                        if (weaponSkins.Count > 0)
                        {
                            var randomSkin = weaponSkins[random.Next(weaponSkins.Count)];
                            var newItemId = random.Next(100000, 999999);
                            
                            exchangeResult.InventoryItems.Add(new Axlebolt.Bolt.Protobuf.InventoryItem
                            {
                                Id = newItemId,
                                ItemDefinitionId = randomSkin.ItemId,
                                Quantity = 1,
                                Flags = 0,
                                Date = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
                            });
                            
                            if (player != null)
                            {
                                player.Inventory.Items.Add(new Models.PlayerInventoryItem
                                {
                                    Id = newItemId,
                                    DefinitionId = randomSkin.ItemId,
                                    Quantity = 1,
                                    Date = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
                                });
                                await _database.UpdatePlayerAsync(player);
                            }
                            
                            Console.WriteLine($"🎁 SUPER LUCKY! Level Up skin drop: {randomSkin.DisplayName}");
                        }
                    }
                }
                // RECIPE_DROP_IN_GAME - дроп во время игры (очень редко)
                else if (recipeCode.StartsWith("RECIPE_DROP_IN_GAME"))
                {
                    // Даем немного серебра
                    int silverReward = random.Next(10, 101);
                    exchangeResult.Currencies.Add(new Axlebolt.Bolt.Protobuf.CurrencyAmount 
                    { 
                        CurrencyId = 101, 
                        Value = silverReward 
                    });
                    
                    if (player != null)
                    {
                        player.Coins += silverReward;
                        await _database.UpdatePlayerAsync(player);
                    }
                    
                    Console.WriteLine($"🎁 In-game drop: +{silverReward} Silver");
                }
                // RECIPE_GOOD_GAME - награда за хорошую игру (топ места)
                else if (recipeCode.StartsWith("RECIPE_GOOD_GAME_"))
                {
                    // Даем серебро в зависимости от места
                    int place = 1;
                    if (int.TryParse(recipeCode.Replace("RECIPE_GOOD_GAME_", ""), out int parsedPlace))
                    {
                        place = parsedPlace;
                    }
                    
                    int silverReward = place switch
                    {
                        1 => random.Next(200, 501),
                        2 => random.Next(100, 301),
                        3 => random.Next(50, 151),
                        _ => random.Next(20, 51)
                    };
                    
                    exchangeResult.Currencies.Add(new Axlebolt.Bolt.Protobuf.CurrencyAmount 
                    { 
                        CurrencyId = 101, 
                        Value = silverReward 
                    });
                    
                    if (player != null)
                    {
                        player.Coins += silverReward;
                        await _database.UpdatePlayerAsync(player);
                    }
                    
                    Console.WriteLine($"🎁 Good game (place {place}): +{silverReward} Silver");
                }
                // RECIPE_DROP_ON_BONUS - бонусный дроп
                else
                {
                    // Пустой результат
                    Console.WriteLine($"🎁 Bonus drop - no reward");
                }
                
                var dropResult = new BinaryValue 
                { 
                    IsNull = false, 
                    One = ByteString.CopyFrom(exchangeResult.ToByteArray()) 
                };
                
                await _handler.WriteProtoResponseAsync(client, request.Id, dropResult, null);
                Console.WriteLine($"🎁 Match drop recipe {recipeCode} processed");
                return;
            }
            
            // Проверяем рецепт крафта (CRAFT_{RARITY}_{COLLECTION} или CRAFT_{RARITY}_{COLLECTION}_STATTRACK)
            bool isCraftRecipe = !string.IsNullOrEmpty(recipeCode) && recipeCode.StartsWith("CRAFT_");
            
            if (isCraftRecipe)
            {
                Console.WriteLine($"🔨 Processing craft recipe: {recipeCode}");
                
                // Парсим рецепт: CRAFT_RARE_FABLE или CRAFT_EPIC_RIVAL_STATTRACK
                var parts = recipeCode.Replace("CRAFT_", "").Split('_');
                string rarityStr = parts.Length > 0 ? parts[0].ToUpper() : "";
                string collectionStr = parts.Length > 1 ? parts[1].ToUpper() : "";
                bool isStatTrack = recipeCode.EndsWith("_STATTRACK");
                
                Console.WriteLine($"🔨 Craft: Rarity={rarityStr}, Collection={collectionStr}, StatTrack={isStatTrack}");
                
                // Разрешаем крафт только для Fable и Rival коллекций
                var allowedCollections = new[] { "FABLE", "RIVAL" };
                
                if (!allowedCollections.Contains(collectionStr))
                {
                    Console.WriteLine($"❌ Craft denied: Collection {collectionStr} is not allowed. Only Fable and Rival allowed.");
                    await SendError(client, request.Id, 403, $"Craft is only available for Fable and Rival collections");
                    return;
                }
                
                // Проверяем что передано 10 предметов
                if (itemIdsToConsume.Count != 10)
                {
                    Console.WriteLine($"❌ Craft denied: Need 10 items, got {itemIdsToConsume.Count}");
                    await SendError(client, request.Id, 400, "Craft requires exactly 10 items");
                    return;
                }
                
                // Получаем следующую редкость
                var nextRarity = rarityStr switch
                {
                    "COMMON" => "Uncommon",
                    "UNCOMMON" => "Rare",
                    "RARE" => "Epic",
                    "EPIC" => "Legendary",
                    "LEGENDARY" => "Arcane",
                    _ => "Rare"
                };
                
                Console.WriteLine($"🔨 Crafting to next rarity: {nextRarity}");
                
                // Ищем скины следующей редкости из той же коллекции
                var defCollection = _database.GetCollection<Models.InventoryItemDefinition>("inventory_definitions");
                var targetSkins = await defCollection.Find(x => 
                    x.Category == "weapon" && 
                    x.IsEnabled && 
                    x.Rarity == nextRarity &&
                    x.Collection != null &&
                    x.Collection.ToUpper() == collectionStr
                ).ToListAsync();
                
                Console.WriteLine($"🔨 Found {targetSkins.Count} skins of rarity {nextRarity} in collection {collectionStr}");
                
                if (targetSkins.Count == 0)
                {
                    // Fallback - берем любой скин следующей редкости
                    targetSkins = await defCollection.Find(x => 
                        x.Category == "weapon" && 
                        x.IsEnabled && 
                        x.Rarity == nextRarity
                    ).ToListAsync();
                    Console.WriteLine($"🔨 Fallback: Found {targetSkins.Count} skins of rarity {nextRarity}");
                }
                
                if (targetSkins.Count == 0)
                {
                    Console.WriteLine($"❌ No skins found for craft result");
                    await SendError(client, request.Id, 500, "No skins available for craft");
                    return;
                }
                
                // Удаляем 10 предметов из инвентаря игрока
                if (player != null)
                {
                    foreach (var itemId in itemIdsToConsume)
                    {
                        var itemToRemove = player.Inventory.Items.FirstOrDefault(x => x.Id == itemId);
                        if (itemToRemove != null)
                        {
                            player.Inventory.Items.Remove(itemToRemove);
                            Console.WriteLine($"🔨 Removed item {itemId} from inventory");
                        }
                    }
                }
                
                // Выбираем случайный скин
                var random = new Random();
                var resultSkin = targetSkins[random.Next(targetSkins.Count)];
                var newItemId = random.Next(100000, 999999);
                
                // Добавляем новый скин в результат
                var craftedItem = new Axlebolt.Bolt.Protobuf.InventoryItem
                {
                    Id = newItemId,
                    ItemDefinitionId = resultSkin.ItemId,
                    Quantity = 1,
                    Flags = 0,
                    Date = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
                };
                exchangeResult.InventoryItems.Add(craftedItem);
                
                // Добавляем в инвентарь игрока
                if (player != null)
                {
                    player.Inventory.Items.Add(new Models.PlayerInventoryItem
                    {
                        Id = newItemId,
                        DefinitionId = resultSkin.ItemId,
                        Quantity = 1,
                        Date = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
                    });
                    await _database.UpdatePlayerAsync(player);
                }
                
                Console.WriteLine($"🔨 Craft successful! Created: {resultSkin.DisplayName} (ID: {resultSkin.ItemId})");
                
                var craftResult = new BinaryValue 
                { 
                    IsNull = false, 
                    One = ByteString.CopyFrom(exchangeResult.ToByteArray()) 
                };
                
                await _handler.WriteProtoResponseAsync(client, request.Id, craftResult, null);
                Console.WriteLine($"🔨 Craft recipe {recipeCode} processed");
                return;
            }
            
            // Парсим ID кейса из recipeCode СРАЗУ (до работы с инвентарём)
            int caseDefinitionId = 0;
            if (!string.IsNullOrEmpty(recipeCode))
            {
                var caseIdStr = recipeCode
                    .Replace("RECIPE_V2_", "")
                    .Replace("RECIPE_OPEN_GIFT_", "")
                    .Replace("case_", "")
                    .Replace("box_", "");
                
                if (int.TryParse(caseIdStr, out int parsedCaseId))
                {
                    caseDefinitionId = parsedCaseId;
                    Console.WriteLine($"📦 Parsed case definition ID from recipe: {caseDefinitionId}");
                }
            }
            
            if (player != null)
            {
                Console.WriteLine($"🔄 Player {player.Name} inventory before: {player.Inventory.Items.Count} items");
                
                // СПИСЫВАЕМ ВАЛЮТУ
                foreach (var curr in currenciesToSpend)
                {
                    int amount = (int)curr.Value;
                    if (amount <= 0) continue;

                    switch (curr.CurrencyId)
                    {
                        case 101: // Silver/Coins
                            int oldCoins = player.Coins;
                            player.Coins = Math.Max(0, player.Coins - amount);
                            Console.WriteLine($"💰 [EXCHANGE] Recipe={recipeCode}, Spent {amount} Silver. {oldCoins} -> {player.Coins}");
                            break;
                        case 102: // Gold/Gems
                            int oldGems = player.Gems;
                            player.Gems = Math.Max(0, player.Gems - amount);
                            Console.WriteLine($"💎 [EXCHANGE] Recipe={recipeCode}, Spent {amount} Gold. {oldGems} -> {player.Gems}");
                            break;
                        case 103: // Keys
                            int oldKeys = player.Keys;
                            player.Keys = Math.Max(0, player.Keys - amount);
                            Console.WriteLine($"🔑 [EXCHANGE] Recipe={recipeCode}, Spent {amount} Keys. {oldKeys} -> {player.Keys}");
                            break;
                        default:
                            Console.WriteLine($"⚠️ [EXCHANGE] Unknown currency {curr.CurrencyId} (amount={amount})");
                            break;
                    }
                }
                
                // УМЕНЬШАЕМ QUANTITY кейсов (НЕ удаляем!)
                var itemsToUpdate = new List<Models.PlayerInventoryItem>();
                
                // Сначала проверяем itemIdsToConsume (если клиент передал конкретные ID)
                foreach (var itemId in itemIdsToConsume)
                {
                    Console.WriteLine($"🔄 Looking for item with ID: {itemId} in {player.Inventory.Items.Count} items");
                    
                    var itemToUpdate = player.Inventory.Items.FirstOrDefault(x => x.Id == itemId);
                    
                    if (itemToUpdate != null)
                    {
                        itemsToUpdate.Add(itemToUpdate);
                        if (caseDefinitionId == 0)
                            caseDefinitionId = itemToUpdate.DefinitionId;
                        Console.WriteLine($"📦 Found item to update: ID={itemId}, DefId={itemToUpdate.DefinitionId}, Qty={itemToUpdate.Quantity}");
                    }
                    else
                    {
                        itemToUpdate = player.Inventory.Items.FirstOrDefault(x => x.DefinitionId == itemId);
                        if (itemToUpdate != null)
                        {
                            itemsToUpdate.Add(itemToUpdate);
                            if (caseDefinitionId == 0)
                                caseDefinitionId = itemToUpdate.DefinitionId;
                            Console.WriteLine($"📦 Found item by DefId: ID={itemToUpdate.Id}, DefId={itemId}, Qty={itemToUpdate.Quantity}");
                        }
                    }
                }
                
                // ЕСЛИ itemIdsToConsume пустой, но есть caseDefinitionId - ищем кейс по DefinitionId
                if (itemsToUpdate.Count == 0 && caseDefinitionId > 0)
                {
                    Console.WriteLine($"📦 No items in itemIdsToConsume, looking for case by DefinitionId: {caseDefinitionId}");
                    var caseItem = player.Inventory.Items.FirstOrDefault(x => x.DefinitionId == caseDefinitionId);
                    if (caseItem != null)
                    {
                        itemsToUpdate.Add(caseItem);
                        Console.WriteLine($"📦 Found case: ID={caseItem.Id}, DefId={caseItem.DefinitionId}, Qty={caseItem.Quantity}");
                    }
                    else
                    {
                        Console.WriteLine($"⚠️ Case with DefinitionId={caseDefinitionId} not found in inventory!");
                    }
                }
                
                // Уменьшаем quantity или удаляем если quantity <= 1
                // НЕ добавляем кейс в ответ - только удаляем из базы!
                foreach (var item in itemsToUpdate)
                {
                    if (item.Quantity > 1)
                    {
                        // Уменьшаем quantity на 1
                        item.Quantity -= 1;
                        Console.WriteLine($"📦 Decreased quantity: ID={item.Id}, DefId={item.DefinitionId}, NewQty={item.Quantity}");
                        // НЕ добавляем в ответ - клиент сам обновит
                    }
                    else
                    {
                        // Quantity = 1, удаляем из инвентаря
                        Console.WriteLine($"📦 Removing case from inventory: ID={item.Id}, DefId={item.DefinitionId}");
                        player.Inventory.Items.Remove(item);
                        // НЕ добавляем в ответ - клиент сам обновит
                    }
                }
                
                Console.WriteLine($"🔄 Player {player.Name} inventory after: {player.Inventory.Items.Count} items");
            }
            
            // Обрабатываем разные типы рецептов (кейсы, обмен и т.д.)
            if (!string.IsNullOrEmpty(recipeCode) || itemIdsToConsume.Count > 0)
            {
                var random = new Random();
                int randomItemId = 0;
                
                // Пробуем получить определение кейса из базы
                var caseCollection = _database.GetCollection<Models.CaseDefinition>("case_definitions");
                Models.CaseDefinition? caseDefinition = null;
                
                // Ищем кейс по уже распарсенному caseDefinitionId
                if (caseDefinitionId > 0)
                {
                    caseDefinition = await caseCollection.Find(x => x.CaseId == caseDefinitionId).FirstOrDefaultAsync();
                    Console.WriteLine($"📦 Looking for case by definition ID: {caseDefinitionId}");
                }
                
                if (caseDefinition != null && caseDefinition.SkinIds.Count > 0)
                {
                    Console.WriteLine($"📦 Opening case: {caseDefinition.DisplayName} (ID={caseDefinition.CaseId})");
                    Console.WriteLine($"📦 Available skins: {string.Join(", ", caseDefinition.SkinIds)}");
                    Console.WriteLine($"📦 StatTrack chance: {caseDefinition.StatTrackChance * 100}%");
                    
                    int selectedIndex = 0;
                    
                    // Выбираем скин с учетом весов
                    if (caseDefinition.SkinWeights.Count == caseDefinition.SkinIds.Count)
                    {
                        float totalWeight = caseDefinition.SkinWeights.Sum();
                        float randomValue = (float)random.NextDouble() * totalWeight;
                        float currentWeight = 0;
                        
                        for (int i = 0; i < caseDefinition.SkinIds.Count; i++)
                        {
                            currentWeight += caseDefinition.SkinWeights[i];
                            if (randomValue <= currentWeight)
                            {
                                selectedIndex = i;
                                randomItemId = caseDefinition.SkinIds[i];
                                break;
                            }
                        }
                    }
                    else
                    {
                        // Равные шансы если веса не заданы
                        selectedIndex = random.Next(caseDefinition.SkinIds.Count);
                        randomItemId = caseDefinition.SkinIds[selectedIndex];
                    }
                    
                    // Проверяем шанс на StatTrack версию
                    if (caseDefinition.StatTrackSkinIds.Count > selectedIndex && 
                        caseDefinition.StatTrackChance > 0 &&
                        random.NextDouble() < caseDefinition.StatTrackChance)
                    {
                        int statTrackId = caseDefinition.StatTrackSkinIds[selectedIndex];
                        Console.WriteLine($"🎯 StatTrack roll SUCCESS! Upgrading {randomItemId} -> {statTrackId}");
                        randomItemId = statTrackId;
                    }
                    else
                    {
                        Console.WriteLine($"📦 Selected skin: {randomItemId}");
                    }
                }
                else
                {
                    // Fallback - получаем все скины из базы
                    Console.WriteLine("📦 Case definition not found, using fallback");
                    var defCollection = _database.GetCollection<Models.InventoryItemDefinition>("inventory_definitions");
                    var weaponSkins = await defCollection.Find(x => x.Category == "weapon" && x.IsEnabled).ToListAsync();
                    
                    if (weaponSkins.Count > 0)
                    {
                        var randomSkin = weaponSkins[random.Next(weaponSkins.Count)];
                        randomItemId = randomSkin.ItemId;
                    }
                    else
                    {
                        var possibleItems = new[] { 11001, 11002, 12001, 12002, 12003, 12004, 12005, 13001, 15001, 32001, 44002, 46001, 51001, 30101, 30102, 30201, 30301, 30302 };
                        randomItemId = possibleItems[random.Next(possibleItems.Length)];
                    }
                }
                
                if (randomItemId > 0)
                {
                    var newItemId = random.Next(100000, 999999);
                    var newItem = new Axlebolt.Bolt.Protobuf.InventoryItem
                    {
                        Id = newItemId,  // int, не string!
                        ItemDefinitionId = randomItemId,
                        Quantity = 1,
                        Flags = 0,
                        Date = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
                    };
                    exchangeResult.InventoryItems.Add(newItem);
                    
                    // Сохраняем предмет в инвентарь игрока
                    if (player != null)
                    {
                        player.Inventory.Items.Add(new Models.PlayerInventoryItem
                        {
                            Id = newItemId,
                            DefinitionId = randomItemId,
                            Quantity = 1,
                            Date = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
                        });
                    }
                    
                    Console.WriteLine($"🎁 Case opened! Got item: {randomItemId}");
                }
            }
            
            // Сохраняем изменения игрока
            if (player != null)
            {
                Console.WriteLine($"💾 Saving player {player.Name} to database...");
                Console.WriteLine($"💾 Inventory items count before save: {player.Inventory.Items.Count}");
                
                // Логируем все предметы для отладки
                foreach (var item in player.Inventory.Items.Take(10))
                {
                    Console.WriteLine($"   📦 Item in inventory: ID={item.Id}, DefId={item.DefinitionId}");
                }
                
                await _database.UpdatePlayerAsync(player);
                
                // Проверяем что сохранилось правильно
                var savedPlayer = await _database.GetPlayerByTokenAsync(session!.Token);
                if (savedPlayer != null)
                {
                    Console.WriteLine($"✅ Verified save: {savedPlayer.Inventory.Items.Count} items in database");
                }
                
                await _database.UpdatePlayerAsync(player);
                
                // ВАЖНО: НЕ отправляем текущий баланс в ExchangeResult!
                // Клиент (Bolt) прибавляет значения из ExchangeResult.Currencies к текущему балансу.
                // Если отправить 1000 голды (весь баланс), у игрока станет 2000.
                // Сюда нужно добавлять только то, что игрок ПОЛУЧИЛ в результате обмена (если рецепт дает валюту).
                
                Console.WriteLine($"💰 [EXCHANGE] Final balance in DB: Coins={player.Coins}, Gems={player.Gems}, Keys={player.Keys}");
            }
            
            var result = new BinaryValue 
            { 
                IsNull = false, 
                One = ByteString.CopyFrom(exchangeResult.ToByteArray()) 
            };
            
            await _handler.WriteProtoResponseAsync(client, request.Id, result, null);
            Console.WriteLine("🔄 ExchangeInventoryItems response sent");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ ExchangeInventoryItems: {ex.Message}");
            Console.WriteLine($"❌ Stack: {ex.StackTrace}");
        }
    }
    
    private async Task ConsumeInventoryItemAsync(TcpClient client, RpcRequest request)
    {
        try
        {
            Console.WriteLine("🍴 ConsumeInventoryItem Request");
            
            // Возвращаем обновленный предмет (с уменьшенным количеством)
            var item = new Axlebolt.Bolt.Protobuf.InventoryItem
            {
                Id = 0,  // int, не string!
                ItemDefinitionId = 0,
                Quantity = 0,
                Flags = 0,
                Date = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
            };
            
            var result = new BinaryValue 
            { 
                IsNull = false, 
                One = ByteString.CopyFrom(item.ToByteArray()) 
            };
            
            await _handler.WriteProtoResponseAsync(client, request.Id, result, null);
            Console.WriteLine("🍴 ConsumeInventoryItem response sent");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ ConsumeInventoryItem: {ex.Message}");
        }
    }
    
    private async Task TransferInventoryItemsAsync(TcpClient client, RpcRequest request)
    {
        try
        {
            Console.WriteLine("📦 TransferInventoryItems Request");
            
            var result = new BinaryValue { IsNull = false };
            await _handler.WriteProtoResponseAsync(client, request.Id, result, null);
            Console.WriteLine("📦 TransferInventoryItems response sent");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ TransferInventoryItems: {ex.Message}");
        }
    }
    
    private async Task TradeInventoryItemsAsync(TcpClient client, RpcRequest request)
    {
        try
        {
            Console.WriteLine("🤝 TradeInventoryItems Request");
            
            var result = new BinaryValue { IsNull = false };
            await _handler.WriteProtoResponseAsync(client, request.Id, result, null);
            Console.WriteLine("🤝 TradeInventoryItems response sent");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ TradeInventoryItems: {ex.Message}");
        }
    }
    
    private async Task SetInventoryItemFlagsAsync(TcpClient client, RpcRequest request)
    {
        try
        {
            Console.WriteLine("🏳️ SetInventoryItemFlags Request");
            
            var session = _sessionManager.GetSessionByClient(client);
            if (session == null)
            {
                var result = new BinaryValue { IsNull = true };
                await _handler.WriteProtoResponseAsync(client, request.Id, result, null);
                return;
            }
            
            var player = await _database.GetPlayerByTokenAsync(session.Token);
            if (player == null)
            {
                var result = new BinaryValue { IsNull = true };
                await _handler.WriteProtoResponseAsync(client, request.Id, result, null);
                return;
            }
            
            // Парсим параметры: ItemFlags protobuf message
            // Param 0: ItemFlags - содержит MapField<int, int> Flags (itemId -> newFlags)
            if (request.Params.Count > 0 && request.Params[0].One != null && request.Params[0].One.Length > 0)
            {
                var itemFlags = Axlebolt.Bolt.Protobuf.ItemFlags.Parser.ParseFrom(request.Params[0].One);
                
                foreach (var kvp in itemFlags.Flags)
                {
                    var itemId = kvp.Key;
                    var newFlags = kvp.Value;
                    
                    // Находим предмет в инвентаре
                    var item = player.Inventory.Items.FirstOrDefault(x => x.Id == itemId);
                    if (item != null)
                    {
                        Console.WriteLine($"🏳️ Setting flags for item {itemId}: {item.Flags} -> {newFlags}");
                        item.Flags = newFlags;
                    }
                    else
                    {
                        Console.WriteLine($"⚠️ Item {itemId} not found in inventory");
                    }
                }
                
                // Сохраняем изменения
                await _database.UpdatePlayerAsync(player);
                Console.WriteLine($"🏳️ Flags updated for player {player.Name}");
            }
            
            var resultOk = new BinaryValue { IsNull = true };
            await _handler.WriteProtoResponseAsync(client, request.Id, resultOk, null);
            Console.WriteLine("🏳️ SetInventoryItemFlags response sent");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ SetInventoryItemFlags: {ex.Message}");
            Console.WriteLine($"❌ Stack: {ex.StackTrace}");
        }
    }
    
    private async Task SetInventoryItemsPropertiesAsync(TcpClient client, RpcRequest request)
    {
        try
        {
            Console.WriteLine("📝 SetInventoryItemsProperties Request");
            
            var session = _sessionManager.GetSessionByClient(client);
            if (session == null)
            {
                var result = new BinaryValue { IsNull = true };
                await _handler.WriteProtoResponseAsync(client, request.Id, result, null);
                return;
            }
            
            var player = await _database.GetPlayerByTokenAsync(session.Token);
            if (player == null)
            {
                var result = new BinaryValue { IsNull = true };
                await _handler.WriteProtoResponseAsync(client, request.Id, result, null);
                return;
            }
            
            // Парсим параметры
            // Param 0: itemId (int)
            // Param 1: propertyKey (string)
            // Param 2: propertyValue (varies)
            int itemId = 0;
            string propertyKey = "";
            string propertyValue = "";
            
            if (request.Params.Count > 0 && request.Params[0].One != null)
            {
                itemId = Axlebolt.RpcSupport.Protobuf.Integer.Parser.ParseFrom(request.Params[0].One).Value;
            }
            if (request.Params.Count > 1 && request.Params[1].One != null)
            {
                propertyKey = Axlebolt.RpcSupport.Protobuf.String.Parser.ParseFrom(request.Params[1].One).Value;
            }
            if (request.Params.Count > 2 && request.Params[2].One != null)
            {
                // Может быть int или string
                try
                {
                    var intVal = Axlebolt.RpcSupport.Protobuf.Integer.Parser.ParseFrom(request.Params[2].One);
                    propertyValue = intVal.Value.ToString();
                }
                catch
                {
                    try
                    {
                        var strVal = Axlebolt.RpcSupport.Protobuf.String.Parser.ParseFrom(request.Params[2].One);
                        propertyValue = strVal.Value;
                    }
                    catch { }
                }
            }
            
            if (itemId > 0 && !string.IsNullOrEmpty(propertyKey))
            {
                var item = player.Inventory.Items.FirstOrDefault(x => x.Id == itemId);
                if (item != null)
                {
                    item.Properties ??= new Dictionary<string, string>();
                    item.Properties[propertyKey] = propertyValue;
                    Console.WriteLine($"📝 Set property {propertyKey}={propertyValue} for item {itemId}");
                    
                    await _database.UpdatePlayerAsync(player);
                }
            }
            
            var resultOk = new BinaryValue { IsNull = true };
            await _handler.WriteProtoResponseAsync(client, request.Id, resultOk, null);
            Console.WriteLine("📝 SetInventoryItemsProperties response sent");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ SetInventoryItemsProperties: {ex.Message}");
            Console.WriteLine($"❌ Stack: {ex.StackTrace}");
        }
    }
    
    private async Task GetOtherPlayerItemsAsync(TcpClient client, RpcRequest request)
    {
        try
        {
            Console.WriteLine("👤 GetOtherPlayerItems Request");
            
            // Возвращаем пустой массив предметов другого игрока
            var result = new BinaryValue { IsNull = false };
            await _handler.WriteProtoResponseAsync(client, request.Id, result, null);
            Console.WriteLine("👤 GetOtherPlayerItems response sent");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ GetOtherPlayerItems: {ex.Message}");
        }
    }

    private async Task GenerateCouponAsync(TcpClient client, RpcRequest request)
    {
        try
        {
            Console.WriteLine("🎟️ GenerateCoupon Request");
            Console.WriteLine($"🎟️ Params count: {request.Params.Count}");

            var session = _sessionManager.GetSessionByClient(client);
            var couponId = Guid.NewGuid().ToString();
            var couponCode = GenerateCouponCode();

            // Парсим награды из запроса
            var rewards = new List<Models.RewardDefinition>();
            if (request.Params.Count > 0 && request.Params[0].One != null)
            {
                var req = Axlebolt.Bolt.Protobuf.GenerateCouponRequest.Parser.ParseFrom(request.Params[0].One);
                Console.WriteLine($"🎟️ ItemDefinitionIds count: {req.ItemDefinitionIds.Count}");
                Console.WriteLine($"🎟️ Currencies count: {req.Currencies.Count}");
                
                // Добавляем предметы
                foreach (var itemId in req.ItemDefinitionIds)
                {
                    Console.WriteLine($"🎟️ Adding item reward: {itemId}");
                    rewards.Add(new Models.RewardDefinition
                    {
                        Type = "item",
                        ItemDefinitionId = itemId,
                        Amount = 1
                    });
                }
                
                // Добавляем валюту
                foreach (var currency in req.Currencies)
                {
                    Console.WriteLine($"🎟️ Adding currency reward: {currency.CurrencyId} x {currency.Value}");
                    rewards.Add(new Models.RewardDefinition
                    {
                        Type = "currency",
                        CurrencyId = currency.CurrencyId,
                        Amount = (int)currency.Value
                    });
                }
            }
            else
            {
                Console.WriteLine("🎟️ No params or params[0].One is null");
            }

            // Сохраняем купон в базу
            var couponCollection = _database.GetCollection<Models.Coupon>("coupons");
            var coupon = new Models.Coupon
            {
                CouponId = couponId,
                Code = couponCode,
                CreatorPlayerId = session?.Token ?? "unknown",
                Rewards = rewards,
                MaxUses = 1,
                IsActive = true,
                ExpiresAt = DateTime.UtcNow.AddDays(30) // Купон действует 30 дней
            };
            await couponCollection.InsertOneAsync(coupon);

            var response = new Axlebolt.Bolt.Protobuf.GenerateCouponResponse
            {
                CouponId = couponCode // Возвращаем код, а не ID
            };

            var result = new BinaryValue
            {
                IsNull = false,
                One = ByteString.CopyFrom(response.ToByteArray())
            };

            await _handler.WriteProtoResponseAsync(client, request.Id, result, null);
            Console.WriteLine($"🎟️ Generated coupon: {couponCode} with {rewards.Count} rewards");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ GenerateCoupon: {ex.Message}");
            Console.WriteLine($"❌ StackTrace: {ex.StackTrace}");
            var errorResult = new BinaryValue { IsNull = true };
            await _handler.WriteProtoResponseAsync(client, request.Id, errorResult, null);
        }
    }

    private async Task GetPlayerCouponsAsync(TcpClient client, RpcRequest request)
    {
        try
        {
            Console.WriteLine("🎟️ GetPlayerCoupons Request");

            var session = _sessionManager.GetSessionByClient(client);
            var response = new Axlebolt.Bolt.Protobuf.GetPlayerCouponsResponse();

            if (session != null)
            {
                // Получаем купоны созданные игроком
                var couponCollection = _database.GetCollection<Models.Coupon>("coupons");
                var playerCoupons = await couponCollection
                    .Find(c => c.CreatorPlayerId == session.Token && c.IsActive)
                    .ToListAsync();

                foreach (var coupon in playerCoupons)
                {
                    var protoCoupon = new Axlebolt.Bolt.Protobuf.Coupon
                    {
                        Id = coupon.CouponId,
                        Code = coupon.Code,
                        CreatedAt = new DateTimeOffset(coupon.CreatedAt).ToUnixTimeSeconds(),
                        ExpiresAt = coupon.ExpiresAt.HasValue 
                            ? new DateTimeOffset(coupon.ExpiresAt.Value).ToUnixTimeSeconds() 
                            : 0,
                        IsActive = coupon.IsActive && coupon.CurrentUses < coupon.MaxUses
                    };

                    // Добавляем предметы
                    foreach (var reward in coupon.Rewards.Where(r => r.Type == "item"))
                    {
                        protoCoupon.ItemDefinitionIds.Add(reward.ItemDefinitionId);
                    }

                    // Добавляем валюту
                    foreach (var reward in coupon.Rewards.Where(r => r.Type == "currency"))
                    {
                        protoCoupon.Currencies.Add(new Axlebolt.Bolt.Protobuf.CurrencyAmountCoupon
                        {
                            CurrencyId = reward.CurrencyId,
                            Value = reward.Amount
                        });
                    }

                    response.Coupons.Add(protoCoupon);
                }

                response.TotalCount = response.Coupons.Count;
                Console.WriteLine($"🎟️ Found {response.TotalCount} coupons for player");
            }

            var result = new BinaryValue
            {
                IsNull = false,
                One = ByteString.CopyFrom(response.ToByteArray())
            };

            await _handler.WriteProtoResponseAsync(client, request.Id, result, null);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ GetPlayerCoupons: {ex.Message}");
            var response = new Axlebolt.Bolt.Protobuf.GetPlayerCouponsResponse();
            var result = new BinaryValue
            {
                IsNull = false,
                One = ByteString.CopyFrom(response.ToByteArray())
            };
            await _handler.WriteProtoResponseAsync(client, request.Id, result, null);
        }
    }

    private async Task ActivateCouponAsync(TcpClient client, RpcRequest request)
    {
        try
        {
            Console.WriteLine("🎟️ ActivateCoupon Request");

            string couponCode = "";
            if (request.Params.Count > 0 && request.Params[0].One != null)
            {
                var req = Axlebolt.Bolt.Protobuf.ActivateCouponRequest.Parser.ParseFrom(request.Params[0].One);
                couponCode = req.CouponId; // На самом деле это код купона, не ID
            }

            var session = _sessionManager.GetSessionByClient(client);
            if (session == null)
            {
                var errorResponse = new Axlebolt.Bolt.Protobuf.ActivateCouponResponse
                {
                    Success = false,
                    ErrorMessage = "Session not found"
                };
                var errorResult = new BinaryValue
                {
                    IsNull = false,
                    One = ByteString.CopyFrom(errorResponse.ToByteArray())
                };
                await _handler.WriteProtoResponseAsync(client, request.Id, errorResult, null);
                return;
            }

            // Ищем купон по коду
            var couponCollection = _database.GetCollection<Models.Coupon>("coupons");
            var coupon = await couponCollection.Find(c => c.Code == couponCode).FirstOrDefaultAsync();

            if (coupon == null)
            {
                Console.WriteLine($"❌ Coupon not found: {couponCode}");
                var errorResponse = new Axlebolt.Bolt.Protobuf.ActivateCouponResponse
                {
                    Success = false,
                    ErrorMessage = "Coupon not found"
                };
                var errorResult = new BinaryValue
                {
                    IsNull = false,
                    One = ByteString.CopyFrom(errorResponse.ToByteArray())
                };
                await _handler.WriteProtoResponseAsync(client, request.Id, errorResult, null);
                return;
            }

            // Проверяем активность купона
            if (!coupon.IsActive)
            {
                Console.WriteLine($"❌ Coupon is not active: {couponCode}");
                var errorResponse = new Axlebolt.Bolt.Protobuf.ActivateCouponResponse
                {
                    Success = false,
                    ErrorMessage = "Coupon is not active"
                };
                var errorResult = new BinaryValue
                {
                    IsNull = false,
                    One = ByteString.CopyFrom(errorResponse.ToByteArray())
                };
                await _handler.WriteProtoResponseAsync(client, request.Id, errorResult, null);
                return;
            }

            // Проверяем срок действия
            if (coupon.ExpiresAt.HasValue && coupon.ExpiresAt.Value < DateTime.UtcNow)
            {
                Console.WriteLine($"❌ Coupon expired: {couponCode}");
                var errorResponse = new Axlebolt.Bolt.Protobuf.ActivateCouponResponse
                {
                    Success = false,
                    ErrorMessage = "Coupon has expired"
                };
                var errorResult = new BinaryValue
                {
                    IsNull = false,
                    One = ByteString.CopyFrom(errorResponse.ToByteArray())
                };
                await _handler.WriteProtoResponseAsync(client, request.Id, errorResult, null);
                return;
            }

            // Проверяем количество использований
            if (coupon.CurrentUses >= coupon.MaxUses)
            {
                Console.WriteLine($"❌ Coupon usage limit reached: {couponCode}");
                var errorResponse = new Axlebolt.Bolt.Protobuf.ActivateCouponResponse
                {
                    Success = false,
                    ErrorMessage = "Coupon usage limit reached"
                };
                var errorResult = new BinaryValue
                {
                    IsNull = false,
                    One = ByteString.CopyFrom(errorResponse.ToByteArray())
                };
                await _handler.WriteProtoResponseAsync(client, request.Id, errorResult, null);
                return;
            }

            // Проверяем, не использовал ли игрок уже этот купон
            var playerCouponCollection = _database.GetCollection<Models.PlayerCoupon>("player_coupons");
            var existingUse = await playerCouponCollection.Find(pc => 
                pc.PlayerId == session.Token && pc.CouponId == coupon.CouponId).FirstOrDefaultAsync();

            if (existingUse != null)
            {
                Console.WriteLine($"❌ Player already used this coupon: {couponCode}");
                var errorResponse = new Axlebolt.Bolt.Protobuf.ActivateCouponResponse
                {
                    Success = false,
                    ErrorMessage = "You have already used this coupon"
                };
                var errorResult = new BinaryValue
                {
                    IsNull = false,
                    One = ByteString.CopyFrom(errorResponse.ToByteArray())
                };
                await _handler.WriteProtoResponseAsync(client, request.Id, errorResult, null);
                return;
            }

            // Применяем награды
            var player = await _database.GetPlayerByTokenAsync(session.Token);
            if (player == null)
            {
                var errorResponse = new Axlebolt.Bolt.Protobuf.ActivateCouponResponse
                {
                    Success = false,
                    ErrorMessage = "Player not found"
                };
                var errorResult = new BinaryValue
                {
                    IsNull = false,
                    One = ByteString.CopyFrom(errorResponse.ToByteArray())
                };
                await _handler.WriteProtoResponseAsync(client, request.Id, errorResult, null);
                return;
            }

            Console.WriteLine($"🎟️ Coupon has {coupon.Rewards.Count} rewards");
            Console.WriteLine($"🎟️ Player {player.Name} has {player.Inventory.Items.Count} items before");

            foreach (var reward in coupon.Rewards)
            {
                Console.WriteLine($"🎟️ Processing reward: Type={reward.Type}, ItemId={reward.ItemDefinitionId}, CurrencyId={reward.CurrencyId}, Amount={reward.Amount}");
                
                if (reward.Type == "item" && reward.ItemDefinitionId > 0)
                {
                    // Добавляем предмет в инвентарь игрока
                    var newItem = new Models.PlayerInventoryItem
                    {
                        Id = player.Inventory.Items.Count > 0 ? player.Inventory.Items.Max(i => i.Id) + 1 : 1,
                        DefinitionId = reward.ItemDefinitionId,
                        Quantity = reward.Amount,
                        Date = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                        Flags = 0
                    };
                    player.Inventory.Items.Add(newItem);
                    Console.WriteLine($"✅ Added item {reward.ItemDefinitionId} x{reward.Amount} to player {player.Name}, new count: {player.Inventory.Items.Count}");
                }
                else if (reward.Type == "currency")
                {
                    // Добавляем валюту
                    switch (reward.CurrencyId)
                    {
                        case 1: // Coins
                            player.Coins += reward.Amount;
                            Console.WriteLine($"✅ Added {reward.Amount} coins to player {player.Name}");
                            break;
                        case 2: // Gems
                            player.Gems += reward.Amount;
                            Console.WriteLine($"✅ Added {reward.Amount} gems to player {player.Name}");
                            break;
                        case 3: // Keys
                            player.Keys += reward.Amount;
                            Console.WriteLine($"✅ Added {reward.Amount} keys to player {player.Name}");
                            break;
                        default:
                            // Другие валюты через словарь
                            if (!player.Inventory.Currencies.ContainsKey(reward.CurrencyId))
                                player.Inventory.Currencies[reward.CurrencyId] = 0;
                            player.Inventory.Currencies[reward.CurrencyId] += reward.Amount;
                            Console.WriteLine($"✅ Added {reward.Amount} currency[{reward.CurrencyId}] to player {player.Name}");
                            break;
                    }
                }
            }

            // Сохраняем игрока
            await _database.UpdatePlayerAsync(player);

            // Записываем использование купона
            var playerCoupon = new Models.PlayerCoupon
            {
                PlayerId = session.Token,
                CouponId = coupon.CouponId,
                ActivatedAt = DateTime.UtcNow
            };
            await playerCouponCollection.InsertOneAsync(playerCoupon);

            // Увеличиваем счетчик использований
            var update = Builders<Models.Coupon>.Update.Inc(c => c.CurrentUses, 1);
            await couponCollection.UpdateOneAsync(c => c.CouponId == coupon.CouponId, update);

            var response = new Axlebolt.Bolt.Protobuf.ActivateCouponResponse
            {
                Success = true
            };

            var result = new BinaryValue
            {
                IsNull = false,
                One = ByteString.CopyFrom(response.ToByteArray())
            };

            await _handler.WriteProtoResponseAsync(client, request.Id, result, null);
            Console.WriteLine($"✅ Activated coupon: {couponCode} for player {session.Token}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ ActivateCoupon: {ex.Message}");
            var errorResponse = new Axlebolt.Bolt.Protobuf.ActivateCouponResponse
            {
                Success = false,
                ErrorMessage = ex.Message
            };
            var errorResult = new BinaryValue
            {
                IsNull = false,
                One = ByteString.CopyFrom(errorResponse.ToByteArray())
            };
            await _handler.WriteProtoResponseAsync(client, request.Id, errorResult, null);
        }
    }

    private async Task ApplyInventoryItemAsync(TcpClient client, RpcRequest request)
    {
        try
        {
            Console.WriteLine("🔧 ApplyInventoryItem Request");

            int consumedItemId = 0;
            int appliedItemId = 0;
            string propertyName = "";
            bool isRemovable = false;

            if (request.Params.Count >= 4)
            {
                if (request.Params[0].One != null)
                    consumedItemId = Axlebolt.RpcSupport.Protobuf.Integer.Parser.ParseFrom(request.Params[0].One).Value;
                if (request.Params[1].One != null)
                    appliedItemId = Axlebolt.RpcSupport.Protobuf.Integer.Parser.ParseFrom(request.Params[1].One).Value;
                if (request.Params[2].One != null)
                    propertyName = Axlebolt.RpcSupport.Protobuf.String.Parser.ParseFrom(request.Params[2].One).Value;
                if (request.Params[3].One != null)
                    isRemovable = Axlebolt.RpcSupport.Protobuf.Boolean.Parser.ParseFrom(request.Params[3].One).Value;
            }

            Console.WriteLine($"🔧 Apply: consumed={consumedItemId}, applied={appliedItemId}, prop={propertyName}");

            // Возвращаем обновленный предмет
            var item = new Axlebolt.Bolt.Protobuf.InventoryItem
            {
                Id = appliedItemId,
                ItemDefinitionId = 0,
                Quantity = 1,
                Flags = 0,
                Date = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
            };

            var result = new BinaryValue
            {
                IsNull = false,
                One = ByteString.CopyFrom(item.ToByteArray())
            };

            await _handler.WriteProtoResponseAsync(client, request.Id, result, null);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ ApplyInventoryItem: {ex.Message}");
        }
    }

    private async Task RemoveInventoryItemPropertyAsync(TcpClient client, RpcRequest request)
    {
        try
        {
            Console.WriteLine("🔧 RemoveInventoryItemProperty Request");

            int itemId = 0;
            string propertyName = "";

            if (request.Params.Count >= 2)
            {
                if (request.Params[0].One != null)
                    itemId = Axlebolt.RpcSupport.Protobuf.Integer.Parser.ParseFrom(request.Params[0].One).Value;
                if (request.Params[1].One != null)
                    propertyName = Axlebolt.RpcSupport.Protobuf.String.Parser.ParseFrom(request.Params[1].One).Value;
            }

            var item = new Axlebolt.Bolt.Protobuf.InventoryItem
            {
                Id = itemId,
                ItemDefinitionId = 0,
                Quantity = 1,
                Flags = 0,
                Date = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
            };

            var result = new BinaryValue
            {
                IsNull = false,
                One = ByteString.CopyFrom(item.ToByteArray())
            };

            await _handler.WriteProtoResponseAsync(client, request.Id, result, null);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ RemoveInventoryItemProperty: {ex.Message}");
        }
    }

    private string GenerateCouponCode()
    {
        const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
        var random = new Random();
        return new string(Enumerable.Repeat(chars, 12).Select(s => s[random.Next(s.Length)]).ToArray());
    }
    
    /// <summary>
    /// Нормализует название коллекции для соответствия enum на клиенте.
    /// Клиент ожидает названия без пробелов: "DigitalCollection" вместо "Digital Collection"
    /// </summary>
    private string NormalizeCollectionName(string collection)
    {
        if (string.IsNullOrEmpty(collection))
            return "Origin";
        
        // Маппинг названий коллекций с пробелами на enum значения
        var collectionMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            { "Digital Collection", "DigitalCollection" },
            { "Nature Collection", "NatureCollection" },
            { "Genesis Collection", "GenesisCollection" },
            { "Anniversary Collection", "AnniversaryCollection" },
            { "Cyber Collection", "CyberCollection" },
            { "Pirate Collection", "PirateCollection" },
            { "Adventure Collection", "AdventureCollection" },
            { "Hunter Collection", "HunterCollection" },
            { "Sport Collection", "SportCollection" },
            { "Blood Collection", "BloodCollection" },
            { "Neon Collection", "NeonCollection" },
            { "Origin Collection", "Origin" },
            { "Standard Collection", "Standard" },
            // Добавляем варианты без слова Collection
            { "Digital", "DigitalCollection" },
            { "Nature", "NatureCollection" },
            { "Genesis", "GenesisCollection" },
            { "Anniversary", "AnniversaryCollection" },
            { "Cyber", "CyberCollection" },
            { "Pirate", "PirateCollection" },
            { "Adventure", "AdventureCollection" },
            { "Hunter", "HunterCollection" },
            { "Sport", "SportCollection" },
            { "Blood", "BloodCollection" },
            { "Neon", "NeonCollection" },
        };
        
        // Если есть в маппинге - возвращаем нормализованное значение
        if (collectionMap.TryGetValue(collection, out var normalized))
            return normalized;
        
        // Если уже в правильном формате (без пробелов) - возвращаем как есть
        if (!collection.Contains(' '))
            return collection;
        
        // Иначе убираем пробелы
        return collection.Replace(" ", "");
    }

    private async Task SendError(TcpClient client, string guid, int code, string message)
    {
        await _handler.WriteProtoResponseAsync(client, guid, null, 
            new RpcException { Id = guid, Code = code, Property = new RpcExceptionProperty { Reason = message } });
    }
}
