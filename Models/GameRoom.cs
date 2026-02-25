using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace StandRiseServer.Models;

[BsonIgnoreExtraElements]
public class GameRoom
{
    [BsonId]
    public ObjectId Id { get; set; }
    
    [BsonElement("roomId")]
    public string RoomId { get; set; } = Guid.NewGuid().ToString();
    
    [BsonElement("name")]
    public string Name { get; set; } = string.Empty;
    
    [BsonElement("map")]
    public string Map { get; set; } = "DefaultMap";
    
    [BsonElement("gameMode")]
    public string GameMode { get; set; } = "TeamDeathmatch";
    
    [BsonElement("maxPlayers")]
    public int MaxPlayers { get; set; } = 10;
    
    [BsonElement("currentPlayers")]
    public int CurrentPlayers { get; set; } = 0;
    
    [BsonElement("hostPlayerId")]
    public string HostPlayerId { get; set; } = string.Empty;
    
    [BsonElement("players")]
    public List<RoomPlayer> Players { get; set; } = new();
    
    [BsonElement("isStarted")]
    public bool IsStarted { get; set; } = false;
    
    [BsonElement("isPublic")]
    public bool IsPublic { get; set; } = true;
    
    [BsonElement("password")]
    public string Password { get; set; } = string.Empty;
    
    [BsonElement("createdAt")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    
    [BsonElement("teamAScore")]
    public int TeamAScore { get; set; } = 0;
    
    [BsonElement("teamBScore")]
    public int TeamBScore { get; set; } = 0;
}

public class RoomPlayer
{
    [BsonElement("playerId")]
    public string PlayerId { get; set; } = string.Empty;
    
    [BsonElement("playerName")]
    public string PlayerName { get; set; } = string.Empty;
    
    [BsonElement("team")]
    public int Team { get; set; } = 0; // 0 = TeamA, 1 = TeamB
    
    [BsonElement("kills")]
    public int Kills { get; set; } = 0;
    
    [BsonElement("deaths")]
    public int Deaths { get; set; } = 0;
    
    [BsonElement("score")]
    public int Score { get; set; } = 0;
    
    [BsonElement("isReady")]
    public bool IsReady { get; set; } = false;
    
    [BsonElement("joinedAt")]
    public DateTime JoinedAt { get; set; } = DateTime.UtcNow;
}
