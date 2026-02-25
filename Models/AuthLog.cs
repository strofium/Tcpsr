using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace StandRiseServer.Models;

[BsonIgnoreExtraElements]
public class AuthLog
{
    [BsonId]
    public ObjectId Id { get; set; }
    
    public string Hwid { get; set; } = string.Empty;
    public string Token { get; set; } = string.Empty;
    public string Ip { get; set; } = string.Empty;
    public string CustomKey { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty; // Success, NewAccount, IntruderBlocked
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    public string Details { get; set; } = string.Empty;
}
