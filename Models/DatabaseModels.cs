using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace StandRiseServer.Models;

[BsonIgnoreExtraElements]
public class HwidDocument
{
    [BsonId]
    public ObjectId Id { get; set; }
    
    public string Hwid { get; set; } = string.Empty;
    public bool IsBanned { get; set; }
}

[BsonIgnoreExtraElements]
public class HashDocument
{
    [BsonId]
    public ObjectId Id { get; set; }
    
    [BsonElement("version")]
    public string Version { get; set; } = string.Empty;
    
    [BsonElement("hash")]
    public string Hash { get; set; } = string.Empty;
    
    [BsonElement("signature")]
    public string Signature { get; set; } = string.Empty;
}

[BsonIgnoreExtraElements]
public class GameSettingDocument
{
    [BsonElement("key")]
    public string Key { get; set; } = string.Empty;
    
    [BsonElement("value")]
    public string Value { get; set; } = string.Empty;
}

[BsonIgnoreExtraElements]
public class InventoryDocument
{
    [BsonId]
    public ObjectId Id { get; set; }
    
    [BsonElement("customskins")]
    public List<CustomSkin> CustomSkins { get; set; } = new();
    
    [BsonElement("skitnsProperties")]
    public List<BsonDocument> SkinProperties { get; set; } = new();
}

public class CustomSkin
{
    [BsonElement("id")]
    public string Id { get; set; } = string.Empty;
    
    [BsonElement("displayName")]
    public string DisplayName { get; set; } = string.Empty;
    
    [BsonElement("properties")]
    public SkinProperties Properties { get; set; } = new();
}

public class SkinProperties
{
    [BsonElement("value")]
    public string Value { get; set; } = string.Empty;
    
    [BsonElement("stattrack")]
    public bool Stattrack { get; set; }
    
    [BsonElement("collection")]
    public string Collection { get; set; } = string.Empty;
}
