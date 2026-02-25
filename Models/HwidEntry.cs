using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace StandRiseServer.Models;

/// <summary>
/// Запись привязки устройства к аккаунту.
/// Ключевая логика: HWID привязывается к PlayerToken, IP обновляется динамически.
/// </summary>
[BsonIgnoreExtraElements]
public class HwidEntry
{
    [BsonId]
    public ObjectId Id { get; set; }
    
    /// <summary>
    /// Уникальный идентификатор железа (gameId от клиента)
    /// </summary>
    [BsonElement("hwid")]
    public string Hwid { get; set; } = string.Empty;
    
    /// <summary>
    /// Внутренний ключ для изоляции аккаунтов на одном железе
    /// </summary>
    [BsonElement("customKey")]
    public string CustomKey { get; set; } = string.Empty;
    
    /// <summary>
    /// Токен игрока для привязки устройства к аккаунту.
    /// Позволяет восстановить сессию при смене IP.
    /// </summary>
    [BsonElement("playerToken")]
    public string PlayerToken { get; set; } = string.Empty;
    
    /// <summary>
    /// ObjectId игрока в коллекции Players2
    /// </summary>
    [BsonElement("playerId")]
    public string PlayerId { get; set; } = string.Empty;
    
    /// <summary>
    /// Последний известный IP адрес (обновляется при каждом входе)
    /// </summary>
    [BsonElement("lastIp")]
    public string LastIp { get; set; } = string.Empty;
    
    /// <summary>
    /// История IP адресов для аудита
    /// </summary>
    [BsonElement("ipHistory")]
    public List<IpHistoryEntry> IpHistory { get; set; } = new();
    
    [BsonElement("firstSeen")]
    public DateTime FirstSeen { get; set; } = DateTime.UtcNow;
    
    [BsonElement("lastSeen")]
    public DateTime LastSeen { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Запись истории IP для аудита
/// </summary>
public class IpHistoryEntry
{
    [BsonElement("ip")]
    public string Ip { get; set; } = string.Empty;
    
    [BsonElement("timestamp")]
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}
