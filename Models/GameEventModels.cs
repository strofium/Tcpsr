using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace StandRiseServer.Models;

public class GameEventDefinition
{
    [BsonId]
    public ObjectId Id { get; set; }

    [BsonElement("eventId")]
    public string EventId { get; set; } = string.Empty;

    [BsonElement("code")]
    public string Code { get; set; } = string.Empty;

    [BsonElement("dateSince")]
    public long DateSince { get; set; }

    [BsonElement("dateUntil")]
    public long DateUntil { get; set; }

    [BsonElement("durationDays")]
    public int DurationDays { get; set; }

    [BsonElement("gamePasses")]
    public List<GamePassDefinition> GamePasses { get; set; } = new();

    [BsonElement("isEnabled")]
    public bool IsEnabled { get; set; } = true;
}

public class GamePassDefinition
{
    [BsonElement("passId")]
    public string PassId { get; set; } = string.Empty;

    [BsonElement("code")]
    public string Code { get; set; } = string.Empty;

    [BsonElement("keyItemDefinitionId")]
    public int KeyItemDefinitionId { get; set; }

    [BsonElement("levels")]
    public List<GamePassLevelDefinition> Levels { get; set; } = new();
}

public class GamePassLevelDefinition
{
    [BsonElement("level")]
    public int Level { get; set; }

    [BsonElement("requiredPoints")]
    public int RequiredPoints { get; set; }

    [BsonElement("rewards")]
    public List<RewardDefinition> Rewards { get; set; } = new();
}

public class RewardDefinition
{
    [BsonElement("type")]
    public string Type { get; set; } = string.Empty; // "currency", "item", or "recipe"

    [BsonElement("currencyId")]
    public int CurrencyId { get; set; }

    [BsonElement("itemDefinitionId")]
    public int ItemDefinitionId { get; set; }

    [BsonElement("recipe")]
    public string Recipe { get; set; } = string.Empty;

    [BsonElement("amount")]
    public int Amount { get; set; }
}

public class GameChallenge
{
    [BsonId]
    public ObjectId Id { get; set; }

    [BsonElement("challengeId")]
    public string ChallengeId { get; set; } = string.Empty;

    [BsonElement("gameEventId")]
    public string GameEventId { get; set; } = string.Empty;

    [BsonElement("code")]
    public string Code { get; set; } = string.Empty;

    [BsonElement("type")]
    public string Type { get; set; } = string.Empty;

    [BsonElement("action")]
    public string Action { get; set; } = string.Empty;

    [BsonElement("targetPoints")]
    public int TargetPoints { get; set; }

    [BsonElement("eventPoints")]
    public int EventPoints { get; set; }

    [BsonElement("name")]
    public string Name { get; set; } = string.Empty;

    [BsonElement("description")]
    public string Description { get; set; } = string.Empty;

    [BsonElement("dayRange")]
    public DayRangeModel? DayRange { get; set; }

    [BsonElement("isEnabled")]
    public bool IsEnabled { get; set; } = true;
}

public class DayRangeModel
{
    [BsonElement("from")]
    public int From { get; set; }

    [BsonElement("to")]
    public int To { get; set; }
}

public class PlayerGameEventProgress
{
    [BsonId]
    public ObjectId Id { get; set; }

    [BsonElement("playerId")]
    public string PlayerId { get; set; } = string.Empty;

    [BsonElement("eventId")]
    public string EventId { get; set; } = string.Empty;

    [BsonElement("points")]
    public int Points { get; set; }

    [BsonElement("passLevels")]
    public Dictionary<string, int> PassLevels { get; set; } = new();

    [BsonElement("challengeProgress")]
    public Dictionary<string, int> ChallengeProgress { get; set; } = new();
}

public class Coupon
{
    [BsonId]
    public ObjectId Id { get; set; }

    [BsonElement("couponId")]
    public string CouponId { get; set; } = string.Empty;

    [BsonElement("code")]
    public string Code { get; set; } = string.Empty;

    [BsonElement("creatorPlayerId")]
    public string CreatorPlayerId { get; set; } = string.Empty;

    [BsonElement("rewards")]
    public List<RewardDefinition> Rewards { get; set; } = new();

    [BsonElement("maxUses")]
    public int MaxUses { get; set; } = 1;

    [BsonElement("currentUses")]
    public int CurrentUses { get; set; }

    [BsonElement("expiresAt")]
    public DateTime? ExpiresAt { get; set; }

    [BsonElement("isActive")]
    public bool IsActive { get; set; } = true;

    [BsonElement("createdAt")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public class PlayerCoupon
{
    [BsonId]
    public ObjectId Id { get; set; }

    [BsonElement("playerId")]
    public string PlayerId { get; set; } = string.Empty;

    [BsonElement("couponId")]
    public string CouponId { get; set; } = string.Empty;

    [BsonElement("activatedAt")]
    public DateTime ActivatedAt { get; set; } = DateTime.UtcNow;
}
