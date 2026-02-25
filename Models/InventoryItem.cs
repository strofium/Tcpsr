using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace StandRiseServer.Models;

public class InventoryItemDefinition
{
    [BsonId]
    public ObjectId Id { get; set; }
    
    [BsonElement("itemId")]
    public int ItemId { get; set; }
    
    [BsonElement("displayName")]
    public string DisplayName { get; set; } = string.Empty;
    
    [BsonElement("description")]
    public string Description { get; set; } = string.Empty;
    
    [BsonElement("category")]
    public string Category { get; set; } = string.Empty; // weapon, case, box, medal, etc.
    
    [BsonElement("subcategory")]
    public string Subcategory { get; set; } = string.Empty; // pistol, rifle, knife, etc.
    
    [BsonElement("rarity")]
    public string Rarity { get; set; } = string.Empty; // common, rare, epic, legendary
    
    [BsonElement("buyPrice")]
    public List<CurrencyPrice> BuyPrice { get; set; } = new();
    
    [BsonElement("sellPrice")]
    public List<CurrencyPrice> SellPrice { get; set; } = new();
    
    [BsonElement("properties")]
    public Dictionary<string, string> Properties { get; set; } = new();
    
    [BsonElement("canBeTraded")]
    public bool CanBeTraded { get; set; } = true;
    
    [BsonElement("canBeSold")]
    public bool CanBeSold { get; set; } = true;
    
    [BsonElement("isEnabled")]
    public bool IsEnabled { get; set; } = true;
    
    [BsonElement("weaponId")]
    public int? WeaponId { get; set; } // Для скинов оружия
    
    [BsonElement("skinName")]
    public string SkinName { get; set; } = string.Empty;
    
    [BsonElement("collection")]
    public string Collection { get; set; } = string.Empty;
    
    [BsonElement("createdAt")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    
    [BsonElement("updatedAt")]
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

public class CurrencyPrice
{
    [BsonElement("currencyId")]
    public int CurrencyId { get; set; }
    
    [BsonElement("value")]
    public float Value { get; set; }
}

// PlayerInventoryItem moved to Player.cs


// Отдельная коллекция для инвентаря (для купонов и т.д.)
public class InventoryItem
{
    [BsonId]
    public ObjectId Id { get; set; }
    
    [BsonElement("playerId")]
    public string PlayerId { get; set; } = string.Empty;
    
    [BsonElement("itemDefinitionId")]
    public int ItemDefinitionId { get; set; }
    
    [BsonElement("quantity")]
    public int Quantity { get; set; } = 1;
    
    [BsonElement("acquiredDate")]
    public DateTime AcquiredDate { get; set; } = DateTime.UtcNow;
    
    [BsonElement("properties")]
    public Dictionary<string, string> Properties { get; set; } = new();
}
