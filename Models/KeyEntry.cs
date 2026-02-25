using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace StandRiseServer.Models;

public class KeyEntry {
    [BsonId] public ObjectId Id { get; set; }
    public string Key { get; set; } = string.Empty;
    public string PlayerId { get; set; } = string.Empty; // Linked player ID
    public bool IsUsed { get; set; }
    public long TelegramUserId { get; set; }
    public string TelegramUsername { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
