using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using System.Net.Sockets;

namespace StandRiseServer.Models;

public class PlayerSession
{
    public string Token { get; set; } = string.Empty;
    public string Hwid { get; set; } = string.Empty;
    public long TimeInGame { get; set; }
    
    [BsonIgnore]
    public TcpClient? Client { get; set; }
    
    public string PlayerObjectId { get; set; } = string.Empty;
}
