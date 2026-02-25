using System;
using Google.Protobuf;

namespace StandRiseServer.Models
{
    /// <summary>
    /// Отклонённая заявка в друзья
    /// </summary>
    public class RejectedRequest
    {
        public string PlayerId { get; set; } = "";
        public string PlayerName { get; set; } = "";
        public DateTime RequestDate { get; set; }
    }

    /// <summary>
    /// Информация о текущей игре игрока
    /// </summary>
    public class CurrentGameInfo
    {
        public string GameMode { get; set; } = "";
        public string RoomId { get; set; } = "";
        public DateTime EnterTime { get; set; }
    }

    /// <summary>
    /// Событие изменения статуса друга
    /// </summary>
    public class FriendStatusChangedEvent
    {
        public string FriendUid { get; set; } = "";
        public int Status { get; set; }
        public string? GameMode { get; set; }
        public string? RoomId { get; set; }
        
        public byte[] ToByteArray()
        {
            // Создаём protobuf-совместимый BinaryValue
            var message = new Axlebolt.RpcSupport.Protobuf.BinaryValue();
            
            // Здесь нужно добавить реальную сериализацию полей
            // В зависимости от вашего protobuf-контракта на клиенте
            // Например, можно сериализовать как JSON:
            var json = System.Text.Json.JsonSerializer.Serialize(this);
            message.One = ByteString.CopyFromUtf8(json);
            
            return message.ToByteArray();
        }
    }

    /// <summary>
    /// Уведомление о заявке в друзья
    /// </summary>
    public class FriendRequestNotification
    {
        public string FromUid { get; set; } = "";
        public string FromName { get; set; } = "";
        public string Type { get; set; } = ""; // "received", "accepted", "rejected", "removed", "blocked"
        
        public byte[] ToByteArray()
        {
            var message = new Axlebolt.RpcSupport.Protobuf.BinaryValue();
            var json = System.Text.Json.JsonSerializer.Serialize(this);
            message.One = ByteString.CopyFromUtf8(json);
            return message.ToByteArray();
        }
    }
}