using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace StandRiseServer.Models;

[BsonIgnoreExtraElements]
public class Player
{
    [BsonId]
    public ObjectId Id { get; set; }
    
    // Основная информация (соответствует Unity PlayerStructure)
    public string PlayerUid { get; set; } = string.Empty; // Uid в Unity
    public int OriginalUid { get; set; }
    public string Name { get; set; } = string.Empty;

    public CurrentGameInfo? CurrentGameInfo { get; set; }


    public DateTime LastStatusUpdat { get; set; } = DateTime.UtcNow;
    
    // Авторизация через логин/пароль
    public string Username { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    
    public int Level { get; set; } = 1;
    public long Experience { get; set; } = 0;
    
    // Валюты (соответствует Unity: Coins, Gems, Keys)
    public int Coins { get; set; } = 100;
    public int Gems { get; set; } = 1000;
    public int Keys { get; set; } = 10;
    
    // Временные данные
    public DateTime LastLogin { get; set; } = DateTime.UtcNow;
    public DateTime RegistrationDate { get; set; } = DateTime.UtcNow;
    public long TimeInGame { get; set; } = 0;
    
    // Статистика (соответствует Unity PlayerStats)
    public PlayerStats Stats { get; set; } = new();
    
    // Ранговая система (соответствует Unity RankedInfo)
    public RankedInfo Ranked { get; set; } = new();
    
    // Инвентарь (соответствует Unity PlayerInventory)
    public PlayerInventoryData Inventory { get; set; } = new();
    
    // Настройки (соответствует Unity PlayerSettings)
    public PlayerSettings Settings { get; set; } = new();
    
    // Социальные функции (соответствует Unity SocialInfo)
    public SocialInfo Social { get; set; } = new();
    
    // Статус онлайн
    public OnlineStatus OnlineStatus { get; set; } = OnlineStatus.Online;
    public PlayInGame PlayInGame { get; set; } = new();
    
    // Дополнительные поля сервера (не из Unity)
    public string Avatar { get; set; } = string.Empty;
    public string AvatarId { get; set; } = string.Empty;
    public string AuthToken { get; set; } = string.Empty;
    public string Token { get; set; } = string.Empty;
    public string DeviceId { get; set; } = string.Empty;
    public string LastHwid { get; set; } = string.Empty;
    public string LastIpAddress { get; set; } = string.Empty;
    public long TelegramUserId { get; set; }
    public string TelegramUsername { get; set; } = string.Empty;
    public bool NoDetectRoot { get; set; }
    public bool IsBanned { get; set; }
    public string BanCode { get; set; } = string.Empty;
    public string BanReason { get; set; } = string.Empty;
    
    public string CustomKey { get; set; } = string.Empty;
    public List<FileStorageItem> FileStorage { get; set; } = new();
}

// Инвентарь игрока (соответствует Unity PlayerInventory)
[BsonIgnoreExtraElements]
public class PlayerInventoryData
{
    // Убираем BsonElement чтобы использовать имя свойства "Items" как есть
    public List<PlayerInventoryItem> Items { get; set; } = new();
    
    [BsonElement("loadout")]
    public Dictionary<int, LoadoutItem> Loadout { get; set; } = new(); // weaponId -> itemId
    
    [BsonElement("equippedItems")]
    public Dictionary<string, int> EquippedItems { get; set; } = new();
    
    [BsonElement("favoriteItems")]
    public List<int> FavoriteItems { get; set; } = new();
    
    [BsonElement("newItems")]
    public List<int> NewItems { get; set; } = new();
    
    [BsonElement("currencies")]
    public Dictionary<int, float> Currencies { get; set; } = new();
}

public class PlayerInventoryItem
{
    [BsonElement("id")]
    public int Id { get; set; }
    
    [BsonElement("definitionId")]
    public int DefinitionId { get; set; }
    
    [BsonElement("quantity")]
    public int Quantity { get; set; } = 1;
    
    [BsonElement("flags")]
    public int Flags { get; set; } = 0;
    
    [BsonElement("date")]
    public long Date { get; set; }
    
    [BsonElement("properties")]
    public Dictionary<string, string> Properties { get; set; } = new();
    
    [BsonElement("condition")]
    public float Condition { get; set; } = 1.0f;
    
    [BsonElement("expiryDate")]
    public long? ExpiryDate { get; set; }
    
    public bool IsExpired => ExpiryDate.HasValue && ExpiryDate.Value < DateTimeOffset.UtcNow.ToUnixTimeSeconds();
}

// Предмет в лодауте (соответствует Unity LoadoutItem)
public class LoadoutItem
{
    [BsonElement("weaponId")]
    public int WeaponId { get; set; }
    
    [BsonElement("skinId")]
    public int? SkinId { get; set; }
    
    [BsonElement("stickers")]
    public Dictionary<int, int> Stickers { get; set; } = new(); // position -> stickerId
    
    [BsonElement("nameTag")]
    public string? NameTag { get; set; }
}

// Статистика игрока (соответствует Unity PlayerStats)
public class PlayerStats
{
    // Общая статистика
    [BsonElement("totalKills")]
    public int TotalKills { get; set; } = 0;
    
    [BsonElement("totalDeaths")]
    public int TotalDeaths { get; set; } = 0;
    
    [BsonElement("totalMatches")]
    public int TotalMatches { get; set; } = 0;
    
    [BsonElement("totalWins")]
    public int TotalWins { get; set; } = 0;
    
    [BsonElement("totalLosses")]
    public int TotalLosses { get; set; } = 0;
    
    // Ранговая статистика
    [BsonElement("rankedMatches")]
    public int RankedMatches { get; set; } = 0;
    
    [BsonElement("rankedWins")]
    public int RankedWins { get; set; } = 0;
    
    [BsonElement("rankedLosses")]
    public int RankedLosses { get; set; } = 0;
    
    // Детальная статистика
    [BsonElement("headshots")]
    public int Headshots { get; set; } = 0;
    
    [BsonElement("assists")]
    public int Assists { get; set; } = 0;
    
    [BsonElement("damageDealt")]
    public long DamageDealt { get; set; } = 0;
    
    [BsonElement("bombsPlanted")]
    public int BombsPlanted { get; set; } = 0;
    
    [BsonElement("bombsDefused")]
    public int BombsDefused { get; set; } = 0;
    
    // Статистика по оружию
    [BsonElement("weaponStats")]
    public Dictionary<string, WeaponStats> WeaponStats { get; set; } = new();
    
    // Достижения
    [BsonElement("achievements")]
    public List<Achievement> Achievements { get; set; } = new();
    
    // Старая структура для совместимости
    [BsonElement("arrayCust")]
    public List<StatItem> ArrayCust { get; set; } = new();
    
    // Вычисляемые показатели
    public float KDR => TotalDeaths > 0 ? (float)TotalKills / TotalDeaths : TotalKills;
    public float WinRate => TotalMatches > 0 ? (float)TotalWins / TotalMatches * 100 : 0;
    public float HeadshotRate => TotalKills > 0 ? (float)Headshots / TotalKills * 100 : 0;
}

// Ранговая информация (соответствует Unity RankedInfo)
public class RankedInfo
{
    [BsonElement("currentRank")]
    public int CurrentRank { get; set; } = 0;
    
    [BsonElement("currentMMR")]
    public int CurrentMMR { get; set; } = 0;
    
    [BsonElement("calibrationMatches")]
    public int CalibrationMatches { get; set; } = 0;
    
    public bool IsCalibrated => CalibrationMatches >= 10;
    
    // Бан информация
    [BsonElement("banCode")]
    public int BanCode { get; set; } = 0;
    
    [BsonElement("banDuration")]
    public long BanDuration { get; set; } = 0;
    
    public bool IsBanned => BanDuration > DateTimeOffset.UtcNow.ToUnixTimeSeconds();
    
    // История рангов
    [BsonElement("rankHistory")]
    public List<RankHistory> RankHistory { get; set; } = new();
    
    // Сезонная статистика
    [BsonElement("seasonId")]
    public int SeasonId { get; set; } = 1;
    
    [BsonElement("seasonMMR")]
    public int SeasonMMR { get; set; } = 0;
    
    [BsonElement("seasonMatches")]
    public int SeasonMatches { get; set; } = 0;
    
    [BsonElement("seasonWins")]
    public int SeasonWins { get; set; } = 0;
}

// Настройки игрока (соответствует Unity PlayerSettings)
public class PlayerSettings
{
    // Игровые настройки
    [BsonElement("mouseSensitivity")]
    public float MouseSensitivity { get; set; } = 1.0f;
    
    [BsonElement("crosshair")]
    public int Crosshair { get; set; } = 0;
    
    [BsonElement("preferredRegion")]
    public string PreferredRegion { get; set; } = "auto";
    
    // Аудио настройки
    [BsonElement("masterVolume")]
    public float MasterVolume { get; set; } = 1.0f;
    
    [BsonElement("musicVolume")]
    public float MusicVolume { get; set; } = 0.5f;
    
    [BsonElement("effectsVolume")]
    public float EffectsVolume { get; set; } = 1.0f;
    
    // Видео настройки
    [BsonElement("graphicsQuality")]
    public int GraphicsQuality { get; set; } = 2; // 0-низкое, 1-среднее, 2-высокое
    
    [BsonElement("resolution")]
    public int Resolution { get; set; } = 0;
    
    [BsonElement("fullscreen")]
    public bool Fullscreen { get; set; } = true;
    
    // Приватность
    [BsonElement("showOnlineStatus")]
    public bool ShowOnlineStatus { get; set; } = true;
    
    [BsonElement("allowFriendRequests")]
    public bool AllowFriendRequests { get; set; } = true;
    
    [BsonElement("showMatchHistory")]
    public bool ShowMatchHistory { get; set; } = true;
    
    // Уведомления
    [BsonElement("enableNotifications")]
    public bool EnableNotifications { get; set; } = true;
    
    [BsonElement("enableSoundNotifications")]
    public bool EnableSoundNotifications { get; set; } = true;
    
    // Язык и локализация
    [BsonElement("language")]
    public string Language { get; set; } = "en";
    
    [BsonElement("timeZone")]
    public string TimeZone { get; set; } = "UTC";
}

// Социальная информация (соответствует Unity SocialInfo)
public class SocialInfo
{
    [BsonElement("friends")]
    public List<string> Friends { get; set; } = new();
    
    [BsonElement("blockedPlayers")]
    public List<string> BlockedPlayers { get; set; } = new();
    
    [BsonElement("incomingRequests")]
    public List<FriendRequest> IncomingRequests { get; set; } = new();
    
    [BsonElement("outgoingRequests")]
    public List<FriendRequest> OutgoingRequests { get; set; } = new();

    [BsonElement("rejectedRequest")]
    public List<RejectedRequest> RejectedRequests { get; set; } = new();
    
    // Клан
    [BsonElement("clanId")]
    public string? ClanId { get; set; }
    
    [BsonElement("clanRole")]
    public string? ClanRole { get; set; }
    
    [BsonElement("clanJoinDate")]
    public DateTime? ClanJoinDate { get; set; }
    
    // Репутация
    [BsonElement("reputation")]
    public int Reputation { get; set; } = 0;
    
    [BsonElement("commendations")]
    public int Commendations { get; set; } = 0;
    
    [BsonElement("reports")]
    public int Reports { get; set; } = 0;
}

// Статистика по оружию (соответствует Unity WeaponStats)
public class WeaponStats
{
    [BsonElement("weaponName")]
    public string WeaponName { get; set; } = "";
    
    [BsonElement("kills")]
    public int Kills { get; set; } = 0;
    
    [BsonElement("deaths")]
    public int Deaths { get; set; } = 0;
    
    [BsonElement("headshots")]
    public int Headshots { get; set; } = 0;
    
    [BsonElement("damageDealt")]
    public long DamageDealt { get; set; } = 0;
    
    [BsonElement("shotsHit")]
    public int ShotsHit { get; set; } = 0;
    
    [BsonElement("shotsFired")]
    public int ShotsFired { get; set; } = 0;
    
    public float Accuracy => ShotsFired > 0 ? (float)ShotsHit / ShotsFired * 100 : 0;
    public float HeadshotRate => Kills > 0 ? (float)Headshots / Kills * 100 : 0;
}

// Достижение (соответствует Unity Achievement)
public class Achievement
{
    [BsonElement("id")]
    public string Id { get; set; } = "";
    
    [BsonElement("name")]
    public string Name { get; set; } = "";
    
    [BsonElement("description")]
    public string Description { get; set; } = "";
    
    [BsonElement("unlockedDate")]
    public DateTime UnlockedDate { get; set; }
    
    [BsonElement("progress")]
    public int Progress { get; set; } = 0;
    
    [BsonElement("maxProgress")]
    public int MaxProgress { get; set; } = 100;
    
    public bool IsUnlocked => Progress >= MaxProgress;
}

// История рангов (соответствует Unity RankHistory)
public class RankHistory
{
    [BsonElement("rank")]
    public int Rank { get; set; }
    
    [BsonElement("mmr")]
    public int MMR { get; set; }
    
    [BsonElement("date")]
    public DateTime Date { get; set; }
    
    [BsonElement("reason")]
    public string Reason { get; set; } = ""; // "promotion", "demotion", "calibration"
}

// Запрос в друзья (соответствует Unity FriendRequest)
public class FriendRequest
{
    [BsonElement("playerId")]
    public string PlayerId { get; set; } = "";
    
    [BsonElement("playerName")]
    public string PlayerName { get; set; } = "";
    
    [BsonElement("requestDate")]
    public DateTime RequestDate { get; set; }
    
    [BsonElement("message")]
    public string? Message { get; set; }
}

// Статус онлайн (соответствует Unity OnlineStatus)
public enum OnlineStatus
{
    Offline = 0,
    Online = 1,
    Away = 2,
    Busy = 3,
    InGame = 4
}

// Информация об игре (соответствует Unity PlayInGame)
public class PlayInGame
{
    [BsonElement("isInGame")]
    public bool IsInGame { get; set; } = false;
    
    [BsonElement("gameMode")]
    public string? GameMode { get; set; }
    
    [BsonElement("mapName")]
    public string? MapName { get; set; }
    
    [BsonElement("serverRegion")]
    public string? ServerRegion { get; set; }
    
    [BsonElement("gameStartTime")]
    public DateTime? GameStartTime { get; set; }
    
    [BsonElement("playersCount")]
    public int? PlayersCount { get; set; }
    
    [BsonElement("maxPlayers")]
    public int? MaxPlayers { get; set; }
}

// Старая структура для совместимости
public class StatItem
{
    [BsonElement("name")]
    public string Name { get; set; } = string.Empty;
    
    [BsonElement("intValue")]
    public int IntValue { get; set; }
    
    [BsonElement("floatValue")]
    public float FloatValue { get; set; }
    
    [BsonElement("window")]
    public float Window { get; set; }
    
    [BsonElement("type")]
    public string Type { get; set; } = "INT";
}

public class FileStorageItem
{
    [BsonElement("filename")]
    public string Filename { get; set; } = string.Empty;
    
    [BsonElement("file")]
    public List<int> File { get; set; } = new();
}
