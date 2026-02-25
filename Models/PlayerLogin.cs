using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace StandRiseServer.Models;

[BsonIgnoreExtraElements]
public class PlayerLogin
{
    [BsonId]
    public ObjectId Id { get; set; }
    
    [BsonElement("tokenId")]
    public string TokenId { get; set; } = string.Empty; // player.Token
    
    [BsonElement("playerOrig")]
    public string PlayerOrig { get; set; } = string.Empty; // player.PlayerUid
    
    [BsonElement("hwid")]
    public string Hwid { get; set; } = string.Empty;
    
    [BsonElement("ip")]
    public string Ip { get; set; } = string.Empty;
    
    [BsonElement("customKey")]
    public string CustomKey { get; set; } = string.Empty;
    
    [BsonElement("lastLogin")]
    public DateTime LastLogin { get; set; } = DateTime.UtcNow;
}
