using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace StandRiseServer.Models;

// Настройки рынка
[BsonIgnoreExtraElements]
public class MarketplaceConfig
{
    [BsonId]
    public ObjectId Id { get; set; }
    
    [BsonElement("configId")]
    public string ConfigId { get; set; } = "default";
    
    [BsonElement("enabled")]
    public bool Enabled { get; set; } = true;
    
    [BsonElement("commissionPercent")]
    public float CommissionPercent { get; set; } = 0.05f;  // 5% = 0.05 (клиент умножает на 100)
    
    [BsonElement("minCommission")]
    public float MinCommission { get; set; } = 1.0f;
    
    [BsonElement("currencyId")]
    public int CurrencyId { get; set; } = 101; // Silver
    
    [BsonElement("minPrice")]
    public float MinPrice { get; set; } = 0.03f;
    
    [BsonElement("maxPrice")]
    public float MaxPrice { get; set; } = 32000000.0f;
    
    [BsonElement("maxActiveListings")]
    public int MaxActiveListings { get; set; } = 10;
    
    [BsonElement("listingDurationHours")]
    public int ListingDurationHours { get; set; } = 168; // 7 days
}

// Определение кейса с содержимым
[BsonIgnoreExtraElements]
public class CaseDefinition
{
    [BsonId]
    public ObjectId Id { get; set; }
    
    [BsonElement("caseId")]
    public int CaseId { get; set; }  // ID предмета кейса (301, 302, 303 и т.д.)
    
    [BsonElement("displayName")]
    public string DisplayName { get; set; } = string.Empty;
    
    [BsonElement("description")]
    public string Description { get; set; } = string.Empty;
    
    [BsonElement("collection")]
    public string Collection { get; set; } = "Origin";
    
    [BsonElement("skinIds")]
    public List<int> SkinIds { get; set; } = new();  // Список ID скинов которые могут выпасть
    
    [BsonElement("skinWeights")]
    public List<float> SkinWeights { get; set; } = new();  // Веса для каждого скина (шанс выпадения)
    
    [BsonElement("statTrackSkinIds")]
    public List<int> StatTrackSkinIds { get; set; } = new();  // StatTrack версии скинов
    
    [BsonElement("statTrackChance")]
    public float StatTrackChance { get; set; } = 0.10f;  // 10% шанс StatTrack
    
    [BsonElement("keyRequired")]
    public bool KeyRequired { get; set; } = true;  // Нужен ли ключ для открытия
    
    [BsonElement("keyCurrencyId")]
    public int KeyCurrencyId { get; set; } = 103;  // ID валюты ключа
    
    [BsonElement("keyPrice")]
    public float KeyPrice { get; set; } = 1;  // Цена ключа
    
    [BsonElement("isEnabled")]
    public bool IsEnabled { get; set; } = true;
}

// Листинг на рынке (продажа)
[BsonIgnoreExtraElements]
public class MarketplaceListing
{
    [BsonId]
    public ObjectId Id { get; set; }
    
    [BsonElement("listingId")]
    public string ListingId { get; set; } = string.Empty;
    
    [BsonElement("sellerId")]
    public string SellerId { get; set; } = string.Empty;
    
    [BsonElement("sellerName")]
    public string SellerName { get; set; } = string.Empty;
    
    [BsonElement("itemDefinitionId")]
    public int ItemDefinitionId { get; set; }
    
    [BsonElement("inventoryItemId")]
    public int InventoryItemId { get; set; }
    
    [BsonElement("price")]
    public float Price { get; set; }
    
    [BsonElement("currencyId")]
    public int CurrencyId { get; set; } = 101;
    
    [BsonElement("quantity")]
    public int Quantity { get; set; } = 1;
    
    [BsonElement("status")]
    public ListingStatus Status { get; set; } = ListingStatus.Active;
    
    [BsonElement("createdAt")]
    public DateTime CreatedAt { get; set; }
    
    [BsonElement("expiresAt")]
    public DateTime ExpiresAt { get; set; }
    
    [BsonElement("soldAt")]
    public DateTime? SoldAt { get; set; }
    
    [BsonElement("buyerId")]
    public string? BuyerId { get; set; }
    
    [BsonElement("buyerName")]
    public string? BuyerName { get; set; }
}

public enum ListingStatus
{
    Active = 0,
    Sold = 1,
    Cancelled = 2,
    Expired = 3
}

// Запрос на покупку
[BsonIgnoreExtraElements]
public class MarketplacePurchaseRequest
{
    [BsonId]
    public ObjectId Id { get; set; }
    
    [BsonElement("requestId")]
    public string RequestId { get; set; } = string.Empty;
    
    [BsonElement("buyerId")]
    public string BuyerId { get; set; } = string.Empty;
    
    [BsonElement("buyerName")]
    public string BuyerName { get; set; } = string.Empty;
    
    [BsonElement("itemDefinitionId")]
    public int ItemDefinitionId { get; set; }
    
    [BsonElement("maxPrice")]
    public float MaxPrice { get; set; }
    
    [BsonElement("currencyId")]
    public int CurrencyId { get; set; } = 101;
    
    [BsonElement("quantity")]
    public int Quantity { get; set; } = 1;
    
    [BsonElement("status")]
    public ListingStatus Status { get; set; } = ListingStatus.Active;
    
    [BsonElement("createdAt")]
    public DateTime CreatedAt { get; set; }
    
    [BsonElement("expiresAt")]
    public DateTime ExpiresAt { get; set; }
}

// История транзакций
[BsonIgnoreExtraElements]
public class MarketplaceTransaction
{
    [BsonId]
    public ObjectId Id { get; set; }
    
    [BsonElement("transactionId")]
    public string TransactionId { get; set; } = string.Empty;
    
    [BsonElement("listingId")]
    public string ListingId { get; set; } = string.Empty;
    
    [BsonElement("sellerId")]
    public string SellerId { get; set; } = string.Empty;
    
    [BsonElement("buyerId")]
    public string BuyerId { get; set; } = string.Empty;
    
    [BsonElement("itemDefinitionId")]
    public int ItemDefinitionId { get; set; }
    
    [BsonElement("price")]
    public float Price { get; set; }
    
    [BsonElement("commission")]
    public float Commission { get; set; }
    
    [BsonElement("sellerReceived")]
    public float SellerReceived { get; set; }
    
    [BsonElement("currencyId")]
    public int CurrencyId { get; set; }
    
    [BsonElement("completedAt")]
    public DateTime CompletedAt { get; set; }
}
