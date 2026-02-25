using MongoDB.Bson.Serialization.Attributes;

namespace StandRiseServer.Models;

public class WeaponLoadout
{
    [BsonElement("primaryWeapon")]
    public string PrimaryWeapon { get; set; } = "AK47";
    
    [BsonElement("secondaryWeapon")]
    public string SecondaryWeapon { get; set; } = "Pistol";
    
    [BsonElement("meleeWeapon")]
    public string MeleeWeapon { get; set; } = "Knife";
    
    [BsonElement("grenade")]
    public string Grenade { get; set; } = "FragGrenade";
    
    [BsonElement("equipment")]
    public List<string> Equipment { get; set; } = new();
}

public class PlayerPosition
{
    [BsonElement("x")]
    public float X { get; set; }
    
    [BsonElement("y")]
    public float Y { get; set; }
    
    [BsonElement("z")]
    public float Z { get; set; }
    
    [BsonElement("rotationY")]
    public float RotationY { get; set; }
}
