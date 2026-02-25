using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace StandRiseServer.Models;

[BsonIgnoreExtraElements]
public class GameEvent
{
    [BsonId]
    public ObjectId Id { get; set; }
    
    [BsonElement("eventId")]
    public string EventId { get; set; } = string.Empty;
    
    [BsonElement("code")]
    public string Code { get; set; } = string.Empty;
    
    [BsonElement("name")]
    public string Name { get; set; } = string.Empty;
    
    [BsonElement("durationDays")]
    public int DurationDays { get; set; }
    
    [BsonElement("dateSince")]
    public long DateSince { get; set; }
    
    [BsonElement("dateUntil")]
    public long DateUntil { get; set; }
    
    [BsonElement("isEnabled")]
    public bool IsEnabled { get; set; } = true;
}

[BsonIgnoreExtraElements]
public class GamePassLevelData
{
    [BsonId]
    public ObjectId Id { get; set; }
    
    [BsonElement("eventId")]
    public string EventId { get; set; } = string.Empty;
    
    [BsonElement("level")]
    public int Level { get; set; }
    
    [BsonElement("passType")]
    public string PassType { get; set; } = "free"; // free or premium
    
    [BsonElement("minPoints")]
    public int MinPoints { get; set; }
    
    [BsonElement("rewardType")]
    public string RewardType { get; set; } = "currency"; // currency or item
    
    [BsonElement("rewardId")]
    public int RewardId { get; set; }
    
    [BsonElement("rewardAmount")]
    public int RewardAmount { get; set; }
}

// GameChallenge и DayRangeData определены в GameEventModels.cs

[BsonIgnoreExtraElements]
public class PlayerBattlePassProgress
{
    [BsonId]
    public ObjectId Id { get; set; }
    
    [BsonElement("playerUid")]
    public string PlayerUid { get; set; } = string.Empty;
    
    [BsonElement("eventId")]
    public string EventId { get; set; } = string.Empty;
    
    [BsonElement("currentLevel")]
    public int CurrentLevel { get; set; } = 1;
    
    [BsonElement("points")]
    public int Points { get; set; }
    
    [BsonElement("hasPremium")]
    public bool HasPremium { get; set; }
    
    [BsonElement("claimedLevels")]
    public List<int> ClaimedLevels { get; set; } = new();
    
    [BsonElement("claimedPremiumLevels")]
    public List<int> ClaimedPremiumLevels { get; set; } = new();
}

[BsonIgnoreExtraElements]
public class PlayerChallengeProgress
{
    [BsonId]
    public ObjectId Id { get; set; }
    
    [BsonElement("playerUid")]
    public string PlayerUid { get; set; } = string.Empty;
    
    [BsonElement("challengeId")]
    public string ChallengeId { get; set; } = string.Empty;
    
    [BsonElement("currentPoints")]
    public int CurrentPoints { get; set; }
    
    [BsonElement("isCompleted")]
    public bool IsCompleted { get; set; }
    
    [BsonElement("completedAt")]
    public long CompletedAt { get; set; }
}
