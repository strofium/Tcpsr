using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace StandRiseServer.Models;

[BsonIgnoreExtraElements]
public class AuthToken
{
    [BsonId]
    public ObjectId Id { get; set; }
    
    [BsonElement("token")]
    public string Token { get; set; } = string.Empty;
    
    [BsonElement("telegramUserId")]
    public long TelegramUserId { get; set; }
    
    [BsonElement("telegramUsername")]
    public string TelegramUsername { get; set; } = string.Empty;
    
    [BsonElement("createdAt")]
    public DateTime CreatedAt { get; set; }
    
    [BsonElement("expiresAt")]
    public DateTime ExpiresAt { get; set; }
    
    [BsonElement("isUsed")]
    public bool IsUsed { get; set; }
    
    [BsonElement("usedAt")]
    public DateTime? UsedAt { get; set; }
    
    [BsonElement("playerId")]
    public string? PlayerId { get; set; }

    [BsonElement("hwid")]
    public string? Hwid { get; set; }
}
