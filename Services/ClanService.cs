using System.Net.Sockets;
using Axlebolt.RpcSupport.Protobuf;
using StandRiseServer.Core;
using Google.Protobuf;
using MongoDB.Driver;

namespace StandRiseServer.Services;

public class ClanService
{
    private readonly ProtobufHandler _handler;
    private readonly DatabaseService _database;
    private readonly SessionManager _sessionManager;

    public ClanService(ProtobufHandler handler, DatabaseService database, SessionManager sessionManager)
    {
        _handler = handler;
        _database = database;
        _sessionManager = sessionManager;

        Console.WriteLine("🏰 Registering ClanService handlers...");
        _handler.RegisterHandler("ClanRemoteService", "getPlayerClan", GetPlayerClanAsync);
        _handler.RegisterHandler("ClanRemoteService", "getClan", GetClanAsync);
        _handler.RegisterHandler("ClanRemoteService", "createClan", CreateClanAsync);
        _handler.RegisterHandler("ClanRemoteService", "leaveClan", LeaveClanAsync);
        _handler.RegisterHandler("ClanRemoteService", "findClan", FindClanAsync);
        _handler.RegisterHandler("ClanRemoteService", "getRoles", GetRolesAsync);
        _handler.RegisterHandler("ClanRemoteService", "getLevels", GetLevelsAsync);
        _handler.RegisterHandler("ClanRemoteService", "getAllClanMembers", GetAllClanMembersAsync);
        _handler.RegisterHandler("ClanRemoteService", "requestToJoinClan", RequestToJoinClanAsync);
        _handler.RegisterHandler("ClanRemoteService", "inviteToClan", InviteToClanAsync);
        _handler.RegisterHandler("ClanRemoteService", "cancelRequest", CancelRequestAsync);
        _handler.RegisterHandler("ClanRemoteService", "declineRequest", DeclineRequestAsync);
        _handler.RegisterHandler("ClanRemoteService", "kickMember", KickMemberAsync);
        _handler.RegisterHandler("ClanRemoteService", "assignRoleToMember", AssignRoleToMemberAsync);
        _handler.RegisterHandler("ClanRemoteService", "assignLeaderRole", AssignLeaderRoleAsync);
        _handler.RegisterHandler("ClanRemoteService", "changeClanName", ChangeClanNameAsync);
        _handler.RegisterHandler("ClanRemoteService", "changeClanType", ChangeClanTypeAsync);
        _handler.RegisterHandler("ClanRemoteService", "upgradeClan", UpgradeClanAsync);
        _handler.RegisterHandler("ClanRemoteService", "setClanAvatar", SetClanAvatarAsync);
        _handler.RegisterHandler("ClanRemoteService", "sendMsgToClan", SendMsgToClanAsync);
        _handler.RegisterHandler("ClanRemoteService", "getClanMsgs", GetClanMsgsAsync);
        _handler.RegisterHandler("ClanRemoteService", "readClanMsgs", ReadClanMsgsAsync);
        _handler.RegisterHandler("ClanRemoteService", "deleteClanMsgs", DeleteClanMsgsAsync);
        _handler.RegisterHandler("ClanRemoteService", "getUnreadClanMessagesCount", GetUnreadClanMessagesCountAsync);
        _handler.RegisterHandler("ClanRemoteService", "getPlayerOpenRequests", GetPlayerOpenRequestsAsync);
        _handler.RegisterHandler("ClanRemoteService", "getPlayerClosedRequests", GetPlayerClosedRequestsAsync);
        _handler.RegisterHandler("ClanRemoteService", "getClanOpenRequests", GetClanOpenRequestsAsync);
        _handler.RegisterHandler("ClanRemoteService", "getClanClosedRequests", GetClanClosedRequestsAsync);
        _handler.RegisterHandler("ClanRemoteService", "getAvatars", GetAvatarsAsync);
        Console.WriteLine("🏰 ClanService handlers registered!");
    }

    private async Task GetPlayerClanAsync(TcpClient client, RpcRequest request)
    {
        try
        {
            // Возвращаем пустой клан (игрок не в клане)
            var clan = new Clan();
            var result = new BinaryValue { IsNull = false, One = ByteString.CopyFrom(clan.ToByteArray()) };
            await _handler.WriteProtoResponseAsync(client, request.Id, result, null);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ GetPlayerClan: {ex.Message}");
        }
    }

    private async Task GetClanAsync(TcpClient client, RpcRequest request)
    {
        try
        {
            var clan = new Clan();
            var result = new BinaryValue { IsNull = false, One = ByteString.CopyFrom(clan.ToByteArray()) };
            await _handler.WriteProtoResponseAsync(client, request.Id, result, null);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ GetClan: {ex.Message}");
        }
    }

    private async Task CreateClanAsync(TcpClient client, RpcRequest request)
    {
        try
        {
            Console.WriteLine("🏰 CreateClan Request");
            var clan = new Clan { Id = Guid.NewGuid().ToString() };
            var result = new BinaryValue { IsNull = false, One = ByteString.CopyFrom(clan.ToByteArray()) };
            await _handler.WriteProtoResponseAsync(client, request.Id, result, null);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ CreateClan: {ex.Message}");
        }
    }

    private async Task LeaveClanAsync(TcpClient client, RpcRequest request)
    {
        try
        {
            var result = new BinaryValue { IsNull = true };
            await _handler.WriteProtoResponseAsync(client, request.Id, result, null);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ LeaveClan: {ex.Message}");
        }
    }

    private async Task FindClanAsync(TcpClient client, RpcRequest request)
    {
        try
        {
            var result = new BinaryValue { IsNull = false };
            await _handler.WriteProtoResponseAsync(client, request.Id, result, null);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ FindClan: {ex.Message}");
        }
    }

    private async Task GetRolesAsync(TcpClient client, RpcRequest request)
    {
        try
        {
            var result = new BinaryValue { IsNull = false };
            await _handler.WriteProtoResponseAsync(client, request.Id, result, null);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ GetRoles: {ex.Message}");
        }
    }

    private async Task GetLevelsAsync(TcpClient client, RpcRequest request)
    {
        try
        {
            var result = new BinaryValue { IsNull = false };
            await _handler.WriteProtoResponseAsync(client, request.Id, result, null);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ GetLevels: {ex.Message}");
        }
    }

    private async Task GetAllClanMembersAsync(TcpClient client, RpcRequest request)
    {
        try
        {
            var result = new BinaryValue { IsNull = false };
            await _handler.WriteProtoResponseAsync(client, request.Id, result, null);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ GetAllClanMembers: {ex.Message}");
        }
    }

    private async Task RequestToJoinClanAsync(TcpClient client, RpcRequest request)
    {
        try
        {
            var result = new BinaryValue { IsNull = true };
            await _handler.WriteProtoResponseAsync(client, request.Id, result, null);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ RequestToJoinClan: {ex.Message}");
        }
    }

    private async Task InviteToClanAsync(TcpClient client, RpcRequest request)
    {
        try
        {
            var result = new BinaryValue { IsNull = true };
            await _handler.WriteProtoResponseAsync(client, request.Id, result, null);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ InviteToClan: {ex.Message}");
        }
    }

    private async Task CancelRequestAsync(TcpClient client, RpcRequest request)
    {
        try
        {
            var result = new BinaryValue { IsNull = true };
            await _handler.WriteProtoResponseAsync(client, request.Id, result, null);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ CancelRequest: {ex.Message}");
        }
    }

    private async Task DeclineRequestAsync(TcpClient client, RpcRequest request)
    {
        try
        {
            var result = new BinaryValue { IsNull = true };
            await _handler.WriteProtoResponseAsync(client, request.Id, result, null);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ DeclineRequest: {ex.Message}");
        }
    }

    private async Task KickMemberAsync(TcpClient client, RpcRequest request)
    {
        try
        {
            var result = new BinaryValue { IsNull = true };
            await _handler.WriteProtoResponseAsync(client, request.Id, result, null);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ KickMember: {ex.Message}");
        }
    }

    private async Task AssignRoleToMemberAsync(TcpClient client, RpcRequest request)
    {
        try
        {
            var result = new BinaryValue { IsNull = true };
            await _handler.WriteProtoResponseAsync(client, request.Id, result, null);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ AssignRoleToMember: {ex.Message}");
        }
    }

    private async Task AssignLeaderRoleAsync(TcpClient client, RpcRequest request)
    {
        try
        {
            var result = new BinaryValue { IsNull = true };
            await _handler.WriteProtoResponseAsync(client, request.Id, result, null);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ AssignLeaderRole: {ex.Message}");
        }
    }

    private async Task ChangeClanNameAsync(TcpClient client, RpcRequest request)
    {
        try
        {
            var result = new BinaryValue { IsNull = true };
            await _handler.WriteProtoResponseAsync(client, request.Id, result, null);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ ChangeClanName: {ex.Message}");
        }
    }

    private async Task ChangeClanTypeAsync(TcpClient client, RpcRequest request)
    {
        try
        {
            var result = new BinaryValue { IsNull = true };
            await _handler.WriteProtoResponseAsync(client, request.Id, result, null);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ ChangeClanType: {ex.Message}");
        }
    }

    private async Task UpgradeClanAsync(TcpClient client, RpcRequest request)
    {
        try
        {
            var result = new BinaryValue { IsNull = true };
            await _handler.WriteProtoResponseAsync(client, request.Id, result, null);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ UpgradeClan: {ex.Message}");
        }
    }

    private async Task SetClanAvatarAsync(TcpClient client, RpcRequest request)
    {
        try
        {
            var avatarId = new Axlebolt.RpcSupport.Protobuf.String { Value = Guid.NewGuid().ToString() };
            var result = new BinaryValue { IsNull = false, One = ByteString.CopyFrom(avatarId.ToByteArray()) };
            await _handler.WriteProtoResponseAsync(client, request.Id, result, null);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ SetClanAvatar: {ex.Message}");
        }
    }

    private async Task SendMsgToClanAsync(TcpClient client, RpcRequest request)
    {
        try
        {
            var result = new BinaryValue { IsNull = true };
            await _handler.WriteProtoResponseAsync(client, request.Id, result, null);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ SendMsgToClan: {ex.Message}");
        }
    }

    private async Task GetClanMsgsAsync(TcpClient client, RpcRequest request)
    {
        try
        {
            var result = new BinaryValue { IsNull = false };
            await _handler.WriteProtoResponseAsync(client, request.Id, result, null);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ GetClanMsgs: {ex.Message}");
        }
    }

    private async Task ReadClanMsgsAsync(TcpClient client, RpcRequest request)
    {
        try
        {
            var result = new BinaryValue { IsNull = true };
            await _handler.WriteProtoResponseAsync(client, request.Id, result, null);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ ReadClanMsgs: {ex.Message}");
        }
    }

    private async Task DeleteClanMsgsAsync(TcpClient client, RpcRequest request)
    {
        try
        {
            var result = new BinaryValue { IsNull = true };
            await _handler.WriteProtoResponseAsync(client, request.Id, result, null);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ DeleteClanMsgs: {ex.Message}");
        }
    }

    private async Task GetUnreadClanMessagesCountAsync(TcpClient client, RpcRequest request)
    {
        try
        {
            var count = new Axlebolt.RpcSupport.Protobuf.Integer { Value = 0 };
            var result = new BinaryValue { IsNull = false, One = ByteString.CopyFrom(count.ToByteArray()) };
            await _handler.WriteProtoResponseAsync(client, request.Id, result, null);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ GetUnreadClanMessagesCount: {ex.Message}");
        }
    }

    private async Task GetPlayerOpenRequestsAsync(TcpClient client, RpcRequest request)
    {
        try
        {
            var result = new BinaryValue { IsNull = false };
            await _handler.WriteProtoResponseAsync(client, request.Id, result, null);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ GetPlayerOpenRequests: {ex.Message}");
        }
    }

    private async Task GetPlayerClosedRequestsAsync(TcpClient client, RpcRequest request)
    {
        try
        {
            var result = new BinaryValue { IsNull = false };
            await _handler.WriteProtoResponseAsync(client, request.Id, result, null);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ GetPlayerClosedRequests: {ex.Message}");
        }
    }

    private async Task GetClanOpenRequestsAsync(TcpClient client, RpcRequest request)
    {
        try
        {
            var result = new BinaryValue { IsNull = false };
            await _handler.WriteProtoResponseAsync(client, request.Id, result, null);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ GetClanOpenRequests: {ex.Message}");
        }
    }

    private async Task GetClanClosedRequestsAsync(TcpClient client, RpcRequest request)
    {
        try
        {
            var result = new BinaryValue { IsNull = false };
            await _handler.WriteProtoResponseAsync(client, request.Id, result, null);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ GetClanClosedRequests: {ex.Message}");
        }
    }

    private async Task GetAvatarsAsync(TcpClient client, RpcRequest request)
    {
        try
        {
            var result = new BinaryValue { IsNull = false };
            await _handler.WriteProtoResponseAsync(client, request.Id, result, null);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ GetAvatars: {ex.Message}");
        }
    }
}
