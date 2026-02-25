using System.Net.Sockets;
using Axlebolt.RpcSupport.Protobuf;
using Axlebolt.Bolt.Protobuf2;
using StandRiseServer.Core;
using StandRiseServer.Models;
using Google.Protobuf;
using MongoDB.Driver;

namespace StandRiseServer.Services;

public class ChatRemoteService
{
    private readonly ProtobufHandler _handler;
    private readonly DatabaseService _database;
    private readonly SessionManager _sessionManager;
    
    // In-memory chat storage (можно заменить на MongoDB)
    private static readonly Dictionary<string, List<ChatMessage>> _friendChats = new();
    private static readonly List<ChatMessage> _globalChat = new();

    public ChatRemoteService(ProtobufHandler handler, DatabaseService database, SessionManager sessionManager)
    {
        _handler = handler;
        _database = database;
        _sessionManager = sessionManager;
        
        // Register all chat-related handlers
        _handler.RegisterHandler("ChatRemoteService", "getUnreadChatUsersCount", GetUnreadChatUsersCountAsync);
        _handler.RegisterHandler("ChatRemoteService", "getChatUsers", GetChatUsersAsync);
        _handler.RegisterHandler("ChatRemoteService", "getFriendMsgs", GetFriendMsgsAsync);
        _handler.RegisterHandler("ChatRemoteService", "getFriendMsgsByPage", GetFriendMsgsByPageAsync);
        _handler.RegisterHandler("ChatRemoteService", "getFriendMsgsByOffset", GetFriendMsgsByOffsetAsync);
        _handler.RegisterHandler("ChatRemoteService", "sendFriendMsg", SendFriendMsgAsync);
        _handler.RegisterHandler("ChatRemoteService", "readFriendMsgs", ReadFriendMsgsAsync);
        _handler.RegisterHandler("ChatRemoteService", "deleteFriendMsgs", DeleteFriendMsgsAsync);
        _handler.RegisterHandler("ChatRemoteService", "getGroupMsgs", GetGroupMsgsAsync);
        _handler.RegisterHandler("ChatRemoteService", "sendGroupMsg", SendGroupMsgAsync);
        _handler.RegisterHandler("ChatRemoteService", "readGroupMsgs", ReadGroupMsgsAsync);
        _handler.RegisterHandler("ChatRemoteService", "deleteGroupMsgs", DeleteGroupMsgsAsync);
        _handler.RegisterHandler("ChatRemoteService", "sendGlobalChatMessage", SendGlobalChatMessageAsync);
        
        Console.WriteLine("💬 ChatRemoteService handlers registered!");
    }
    
    private string GetChatKey(string player1, string player2)
    {
        var sorted = new[] { player1, player2 }.OrderBy(x => x).ToArray();
        return $"{sorted[0]}_{sorted[1]}";
    }

    private async Task GetUnreadChatUsersCountAsync(TcpClient client, RpcRequest request)
    {
        try
        {
            var count = new Axlebolt.RpcSupport.Protobuf.Integer { Value = 0 };
            var result = new BinaryValue { IsNull = false, One = ByteString.CopyFrom(count.ToByteArray()) };
            await _handler.WriteProtoResponseAsync(client, request.Id, result, null);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ GetUnreadChatUsersCount: {ex.Message}");
        }
    }

    private async Task GetChatUsersAsync(TcpClient client, RpcRequest request)
    {
        try
        {
            var result = new BinaryValue { IsNull = false };
            await _handler.WriteProtoResponseAsync(client, request.Id, result, null);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ GetChatUsers: {ex.Message}");
        }
    }

    private async Task GetFriendMsgsAsync(TcpClient client, RpcRequest request)
    {
        try
        {
            var result = new BinaryValue { IsNull = false };
            await _handler.WriteProtoResponseAsync(client, request.Id, result, null);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ GetFriendMsgs: {ex.Message}");
        }
    }

    private async Task GetFriendMsgsByPageAsync(TcpClient client, RpcRequest request)
    {
        try
        {
            var result = new BinaryValue { IsNull = false };
            await _handler.WriteProtoResponseAsync(client, request.Id, result, null);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ GetFriendMsgsByPage: {ex.Message}");
        }
    }

    private async Task GetFriendMsgsByOffsetAsync(TcpClient client, RpcRequest request)
    {
        try
        {
            var result = new BinaryValue { IsNull = false };
            await _handler.WriteProtoResponseAsync(client, request.Id, result, null);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ GetFriendMsgsByOffset: {ex.Message}");
        }
    }

    private async Task SendFriendMsgAsync(TcpClient client, RpcRequest request)
    {
        try
        {
            Console.WriteLine("💬 SendFriendMsg Request");
            
            var session = _sessionManager.GetSessionByClient(client);
            if (session == null)
            {
                session = _sessionManager.GetAllSessions().FirstOrDefault();
                if (session != null) session.Client = client;
            }
            
            var player = await _database.GetPlayerByTokenAsync(session?.Token ?? "");
            var senderId = player?.PlayerUid ?? session?.Token ?? "";
            var senderName = player?.Name ?? "Player";
            
            string receiverId = "";
            string messageText = "";
            
            // Парсим параметры: receiverId, message
            if (request.Params.Count > 0 && request.Params[0].One != null)
            {
                receiverId = Axlebolt.RpcSupport.Protobuf.String.Parser.ParseFrom(request.Params[0].One).Value;
            }
            if (request.Params.Count > 1 && request.Params[1].One != null)
            {
                messageText = Axlebolt.RpcSupport.Protobuf.String.Parser.ParseFrom(request.Params[1].One).Value;
            }
            
            Console.WriteLine($"💬 Message from {senderName} to {receiverId}: {messageText}");
            
            // Сохраняем сообщение
            var chatKey = GetChatKey(senderId, receiverId);
            if (!_friendChats.ContainsKey(chatKey))
                _friendChats[chatKey] = new List<ChatMessage>();
            
            var msg = new ChatMessage
            {
                Id = Guid.NewGuid().ToString(),
                SenderId = senderId,
                SenderName = senderName,
                ReceiverId = receiverId,
                Text = messageText,
                Timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                IsRead = false
            };
            _friendChats[chatKey].Add(msg);
            
            // Возвращаем ID сообщения
            var msgId = new Axlebolt.RpcSupport.Protobuf.String { Value = msg.Id };
            var result = new BinaryValue 
            { 
                IsNull = false, 
                One = ByteString.CopyFrom(msgId.ToByteArray()) 
            };
            await _handler.WriteProtoResponseAsync(client, request.Id, result, null);
            Console.WriteLine($"✅ Message sent: {msg.Id}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ SendFriendMsg: {ex.Message}");
            var result = new BinaryValue { IsNull = true };
            await _handler.WriteProtoResponseAsync(client, request.Id, result, null);
        }
    }

    private async Task ReadFriendMsgsAsync(TcpClient client, RpcRequest request)
    {
        try
        {
            var result = new BinaryValue { IsNull = true };
            await _handler.WriteProtoResponseAsync(client, request.Id, result, null);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ ReadFriendMsgs: {ex.Message}");
        }
    }

    private async Task DeleteFriendMsgsAsync(TcpClient client, RpcRequest request)
    {
        try
        {
            var result = new BinaryValue { IsNull = true };
            await _handler.WriteProtoResponseAsync(client, request.Id, result, null);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ DeleteFriendMsgs: {ex.Message}");
        }
    }

    private async Task GetGroupMsgsAsync(TcpClient client, RpcRequest request)
    {
        try
        {
            var result = new BinaryValue { IsNull = false };
            await _handler.WriteProtoResponseAsync(client, request.Id, result, null);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ GetGroupMsgs: {ex.Message}");
        }
    }

    private async Task SendGroupMsgAsync(TcpClient client, RpcRequest request)
    {
        try
        {
            var result = new BinaryValue { IsNull = true };
            await _handler.WriteProtoResponseAsync(client, request.Id, result, null);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ SendGroupMsg: {ex.Message}");
        }
    }

    private async Task ReadGroupMsgsAsync(TcpClient client, RpcRequest request)
    {
        try
        {
            var result = new BinaryValue { IsNull = true };
            await _handler.WriteProtoResponseAsync(client, request.Id, result, null);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ ReadGroupMsgs: {ex.Message}");
        }
    }

    private async Task DeleteGroupMsgsAsync(TcpClient client, RpcRequest request)
    {
        try
        {
            var result = new BinaryValue { IsNull = true };
            await _handler.WriteProtoResponseAsync(client, request.Id, result, null);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ DeleteGroupMsgs: {ex.Message}");
        }
    }

    private async Task SendGlobalChatMessageAsync(TcpClient client, RpcRequest request)
    {
        try
        {
            Console.WriteLine("💬 SendGlobalChatMessage Request");
            
            var session = _sessionManager.GetSessionByClient(client);
            if (session == null)
            {
                session = _sessionManager.GetAllSessions().FirstOrDefault();
                if (session != null) session.Client = client;
            }
            
            var player = await _database.GetPlayerByTokenAsync(session?.Token ?? "");
            var senderId = player?.PlayerUid ?? session?.Token ?? "";
            var senderName = player?.Name ?? "Player";
            
            string messageText = "";
            
            if (request.Params.Count > 0 && request.Params[0].One != null)
            {
                messageText = Axlebolt.RpcSupport.Protobuf.String.Parser.ParseFrom(request.Params[0].One).Value;
            }
            
            Console.WriteLine($"💬 Global message from {senderName}: {messageText}");
            
            var msg = new ChatMessage
            {
                Id = Guid.NewGuid().ToString(),
                SenderId = senderId,
                SenderName = senderName,
                ReceiverId = "global",
                Text = messageText,
                Timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                IsRead = false
            };
            _globalChat.Add(msg);
            
            // Ограничиваем размер глобального чата
            if (_globalChat.Count > 100)
                _globalChat.RemoveAt(0);
            
            var result = new BinaryValue { IsNull = true };
            await _handler.WriteProtoResponseAsync(client, request.Id, result, null);
            Console.WriteLine($"✅ Global message sent");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ SendGlobalChatMessage: {ex.Message}");
            var result = new BinaryValue { IsNull = true };
            await _handler.WriteProtoResponseAsync(client, request.Id, result, null);
        }
    }
}

// Модель сообщения чата
public class ChatMessage
{
    public string Id { get; set; } = string.Empty;
    public string SenderId { get; set; } = string.Empty;
    public string SenderName { get; set; } = string.Empty;
    public string ReceiverId { get; set; } = string.Empty;
    public string Text { get; set; } = string.Empty;
    public long Timestamp { get; set; }
    public bool IsRead { get; set; }
}
