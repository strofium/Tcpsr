
using System.Net.Sockets;
using Axlebolt.RpcSupport.Protobuf;
using Axlebolt.Bolt.Protobuf;
using Google.Protobuf;
using StandRiseServer.Core;
using StandRiseServer.Models;

namespace StandRiseServer.Services;

public class FriendsRemoteService
{
    private readonly ProtobufHandler _handler;
    private readonly DatabaseService _database;
    private readonly SessionManager _sessionManager;
    private readonly ILogger<FriendsRemoteService> _logger;
    private const string ServiceName = "FriendsRemoteService";

    public FriendsRemoteService(
        ProtobufHandler handler, 
        DatabaseService database, 
        SessionManager sessionManager,
        ILogger<FriendsRemoteService> logger)
    {
        _handler = handler;
        _database = database;
        _sessionManager = sessionManager;
        _logger = logger;

        _logger.LogInformation("👥 Registering FriendsRemoteService handlers...");
        
        // Основные методы
        _handler.RegisterHandler(ServiceName, "getFriends", GetFriendsAsync);
        _handler.RegisterHandler(ServiceName, "getIncomingRequests", GetIncomingRequestsAsync);
        _handler.RegisterHandler(ServiceName, "getOutgoingRequests", GetOutgoingRequestsAsync);
        _handler.RegisterHandler(ServiceName, "getBlockedPlayers", GetBlockedPlayersAsync);
        _handler.RegisterHandler(ServiceName, "getRejectedRequests", GetRejectedRequestsAsync);
        _handler.RegisterHandler(ServiceName, "getPlayerFriendsIds", GetPlayerFriendsIdsAsync);
        _handler.RegisterHandler(ServiceName, "getPlayerFriendsCount", GetPlayerFriendsCountAsync);
        
        // Поиск
        _handler.RegisterHandler(ServiceName, "searchPlayers", SearchPlayersAsync);
        _handler.RegisterHandler(ServiceName, "getPlayersCount", GetPlayersCountAsync);
        
        // Действия с заявками
        _handler.RegisterHandler(ServiceName, "sendFriendRequest", SendFriendRequestAsync);
        _handler.RegisterHandler(ServiceName, "acceptFriendRequest", AcceptFriendRequestAsync);
        _handler.RegisterHandler(ServiceName, "rejectFriendRequest", RejectFriendRequestAsync);
        _handler.RegisterHandler(ServiceName, "ignoreFriendRequest", IgnoreFriendRequestAsync);
        _handler.RegisterHandler(ServiceName, "revokeFriendRequest", RevokeFriendRequestAsync);
        
        // Действия с друзьями
        _handler.RegisterHandler(ServiceName, "removeFriend", RemoveFriendAsync);
        _handler.RegisterHandler(ServiceName, "blockFriend", BlockFriendAsync);
        _handler.RegisterHandler(ServiceName, "unblockFriend", UnblockFriendAsync);
        
        // Статусы
        _handler.RegisterHandler(ServiceName, "getOnlineStatus", GetOnlineStatusAsync);
        _handler.RegisterHandler(ServiceName, "subscribeToFriendStatus", SubscribeToFriendStatusAsync);
        _handler.RegisterHandler(ServiceName, "unsubscribeFromFriendStatus", UnsubscribeFromFriendStatusAsync);
        
        // Прочее
        _handler.RegisterHandler(ServiceName, "getAvatars", GetAvatarsAsync);
        _handler.RegisterHandler(ServiceName, "getPlayerById", GetPlayerByIdAsync);
        _handler.RegisterHandler(ServiceName, "getPlayerFriendById", GetPlayerFriendByIdAsync);
        
        _logger.LogInformation("👥 FriendsRemoteService handlers registered!");
    }

    private async Task<Models.Player?> GetCurrentPlayerAsync(TcpClient client)
    {
        var session = _sessionManager.GetSessionByClient(client);
        if (session == null)
        {
            _logger.LogWarning("No session found for client");
            return null;
        }

        var player = await _database.GetPlayerByObjectIdAsync(session.PlayerObjectId);
        if (player == null)
        {
            _logger.LogWarning("No player found for session {Token}", session.Token);
            return null;
        }

        return player;
    }

    private PlayerFriend CreatePlayerFriend(Models.Player player, RelationshipStatus status)
    {
        return new PlayerFriend
        {
            Player = new Player
            {
                Id = player.PlayerUid,
                Uid = player.PlayerUid,
                Name = player.Name,
                AvatarId = player.AvatarId ?? "",
                TimeInGame = player.TimeInGame,
                RegistrationDate = new DateTimeOffset(player.RegistrationDate).ToUnixTimeSeconds(),
                LogoutDate = new DateTimeOffset(player.LastLogin).ToUnixTimeSeconds()
            },
            RelationshipStatus = status,
            LastRelationshipUpdate = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
        };
    }

    private string GetStringParam(RpcRequest request, int index)
    {
        if (request.Params.Count > index && request.Params[index].One != null)
            return Axlebolt.RpcSupport.Protobuf.String.Parser.ParseFrom(request.Params[index].One).Value;
        return "";
    }

    private int GetIntParam(RpcRequest request, int index)
    {
        if (request.Params.Count > index && request.Params[index].One != null)
            return Integer.Parser.ParseFrom(request.Params[index].One).Value;
        return 0;
    }

    // ==================== GET FRIENDS (ONLY FRIENDS) ====================

    private async Task GetFriendsAsync(TcpClient client, RpcRequest request)
    {
        try
        {
            _logger.LogInformation("👥 GetFriends");
            var player = await GetCurrentPlayerAsync(client);
            if (player?.Social?.Friends == null)
            {
                await SendEmptyArrayAsync(client, request.Id);
                return;
            }

            int page = GetIntParam(request, 0);
            int size = GetIntParam(request, 1);
            if (size <= 0) size = 50;

            var friendUids = player.Social.Friends
                .Skip(page * size)
                .Take(size)
                .ToList();

            var friends = await GetPlayersWithStatus(friendUids, RelationshipStatus.Friend);
            await SendPlayerFriendListAsync(client, request.Id, friends);
            
            _logger.LogInformation("👥 GetFriends: {Count} friends", friends.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ GetFriends error");
            await SendEmptyArrayAsync(client, request.Id);
        }
    }

    // ==================== GET INCOMING REQUESTS ====================

    private async Task GetIncomingRequestsAsync(TcpClient client, RpcRequest request)
    {
        try
        {
            _logger.LogInformation("👥 GetIncomingRequests");
            var player = await GetCurrentPlayerAsync(client);
            if (player?.Social?.IncomingRequests == null)
            {
                await SendEmptyArrayAsync(client, request.Id);
                return;
            }

            int page = GetIntParam(request, 0);
            int size = GetIntParam(request, 1);
            if (size <= 0) size = 50;

            var requestUids = player.Social.IncomingRequests
                .Skip(page * size)
                .Take(size)
                .Select(r => r.PlayerId)
                .ToList();

            var friends = await GetPlayersWithStatus(requestUids, RelationshipStatus.RequestInitiator);
            await SendPlayerFriendListAsync(client, request.Id, friends);
            
            _logger.LogInformation("👥 GetIncomingRequests: {Count} requests", friends.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ GetIncomingRequests error");
            await SendEmptyArrayAsync(client, request.Id);
        }
    }

    // ==================== GET OUTGOING REQUESTS ====================

    private async Task GetOutgoingRequestsAsync(TcpClient client, RpcRequest request)
    {
        try
        {
            _logger.LogInformation("👥 GetOutgoingRequests");
            var player = await GetCurrentPlayerAsync(client);
            if (player?.Social?.OutgoingRequests == null)
            {
                await SendEmptyArrayAsync(client, request.Id);
                return;
            }

            int page = GetIntParam(request, 0);
            int size = GetIntParam(request, 1);
            if (size <= 0) size = 50;

            var requestUids = player.Social.OutgoingRequests
                .Skip(page * size)
                .Take(size)
                .Select(r => r.PlayerId)
                .ToList();

            var friends = await GetPlayersWithStatus(requestUids, RelationshipStatus.RequestRecipient);
            await SendPlayerFriendListAsync(client, request.Id, friends);
            
            _logger.LogInformation("👥 GetOutgoingRequests: {Count} requests", friends.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ GetOutgoingRequests error");
            await SendEmptyArrayAsync(client, request.Id);
        }
    }

    // ==================== GET BLOCKED PLAYERS ====================

    private async Task GetBlockedPlayersAsync(TcpClient client, RpcRequest request)
    {
        try
        {
            _logger.LogInformation("👥 GetBlockedPlayers");
            var player = await GetCurrentPlayerAsync(client);
            if (player?.Social?.BlockedPlayers == null)
            {
                await SendEmptyArrayAsync(client, request.Id);
                return;
            }

            int page = GetIntParam(request, 0);
            int size = GetIntParam(request, 1);
            if (size <= 0) size = 50;

            var blockedUids = player.Social.BlockedPlayers
                .Skip(page * size)
                .Take(size)
                .ToList();

            var friends = await GetPlayersWithStatus(blockedUids, RelationshipStatus.Blocked);
            await SendPlayerFriendListAsync(client, request.Id, friends);
            
            _logger.LogInformation("👥 GetBlockedPlayers: {Count} players", friends.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ GetBlockedPlayers error");
            await SendEmptyArrayAsync(client, request.Id);
        }
    }

    // ==================== GET REJECTED REQUESTS ====================

    private async Task GetRejectedRequestsAsync(TcpClient client, RpcRequest request)
    {
        try
        {
            _logger.LogInformation("👥 GetRejectedRequests");
            var player = await GetCurrentPlayerAsync(client);
            if (player?.Social?.RejectedRequests == null)
            {
                await SendEmptyArrayAsync(client, request.Id);
                return;
            }

            int page = GetIntParam(request, 0);
            int size = GetIntParam(request, 1);
            if (size <= 0) size = 50;

            var rejectedUids = player.Social.RejectedRequests
                .Skip(page * size)
                .Take(size)
                .Select(r => r.PlayerId)
                .ToList();

            var friends = await GetPlayersWithStatus(rejectedUids, RelationshipStatus.Rejected);
            await SendPlayerFriendListAsync(client, request.Id, friends);
            
            _logger.LogInformation("👥 GetRejectedRequests: {Count} players", friends.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ GetRejectedRequests error");
            await SendEmptyArrayAsync(client, request.Id);
        }
    }

    // ==================== GET PLAYER FRIENDS IDS (для совместимости) ====================

    private async Task GetPlayerFriendsIdsAsync(TcpClient client, RpcRequest request)
    {
        try
        {
            _logger.LogInformation("👥 GetPlayerFriendsIds");
            var player = await GetCurrentPlayerAsync(client);
            if (player == null)
            {
                await SendEmptyArrayAsync(client, request.Id);
                return;
            }

            var friendIds = new List<string>();
            friendIds.AddRange(player.Social.Friends);
            friendIds.AddRange(player.Social.IncomingRequests.Select(r => r.PlayerId));
            friendIds.AddRange(player.Social.OutgoingRequests.Select(r => r.PlayerId));
            friendIds.AddRange(player.Social.BlockedPlayers);
            friendIds.AddRange(player.Social.RejectedRequests.Select(r => r.PlayerId));

            var result = new BinaryValue { IsNull = false };
            foreach (var id in friendIds.Distinct())
            {
                var strProto = new Axlebolt.RpcSupport.Protobuf.String { Value = id };
                result.Array.Add(ByteString.CopyFrom(strProto.ToByteArray()));
            }
            
            await _handler.WriteProtoResponseAsync(client, request.Id, result, null);
            _logger.LogInformation("👥 GetPlayerFriendsIds: {Count} ids", friendIds.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ GetPlayerFriendsIds error");
            await SendEmptyArrayAsync(client, request.Id);
        }
    }

    // ==================== GET PLAYER FRIENDS COUNT ====================

    private async Task GetPlayerFriendsCountAsync(TcpClient client, RpcRequest request)
    {
        try
        {
            _logger.LogInformation("👥 GetPlayerFriendsCount");
            var player = await GetCurrentPlayerAsync(client);
            
            long count = 0;
            if (player != null)
            {
                count = player.Social.Friends.Count + 
                        player.Social.IncomingRequests.Count + 
                        player.Social.OutgoingRequests.Count + 
                        player.Social.BlockedPlayers.Count +
                        player.Social.RejectedRequests.Count;
            }

            await SendLongAsync(client, request.Id, count);
            _logger.LogInformation("👥 GetPlayerFriendsCount: {Count}", count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ GetPlayerFriendsCount error");
            await SendLongAsync(client, request.Id, 0);
        }
    }

    // ==================== SEARCH PLAYERS ====================

    private async Task SearchPlayersAsync(TcpClient client, RpcRequest request)
    {
        try
        {
            _logger.LogInformation("👥 SearchPlayers");
            
            string searchValue = GetStringParam(request, 0);
            int page = GetIntParam(request, 1);
            int size = GetIntParam(request, 2);
            if (size <= 0) size = 20;

            var currentPlayer = await GetCurrentPlayerAsync(client);
            var players = await _database.SearchPlayersAsync(searchValue, page, size);
            
            var result = new BinaryValue { IsNull = false };
            foreach (var p in players)
            {
                if (currentPlayer != null && p.PlayerUid == currentPlayer.PlayerUid) continue;
                
                var status = GetRelationshipStatus(currentPlayer, p);
                var friend = CreatePlayerFriend(p, status);
                result.Array.Add(ByteString.CopyFrom(friend.ToByteArray()));
            }
            
            await _handler.WriteProtoResponseAsync(client, request.Id, result, null);
            _logger.LogInformation("👥 SearchPlayers '{SearchValue}': {Count} results", 
                searchValue, result.Array.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ SearchPlayers error");
            await SendEmptyArrayAsync(client, request.Id);
        }
    }

    private RelationshipStatus GetRelationshipStatus(Models.Player? currentPlayer, Models.Player targetPlayer)
    {
        if (currentPlayer == null) return RelationshipStatus.None;

        if (currentPlayer.Social.Friends.Contains(targetPlayer.PlayerUid))
            return RelationshipStatus.Friend;
            
        if (currentPlayer.Social.BlockedPlayers.Contains(targetPlayer.PlayerUid))
            return RelationshipStatus.Blocked;
            
        if (currentPlayer.Social.OutgoingRequests.Any(r => r.PlayerId == targetPlayer.PlayerUid))
            return RelationshipStatus.RequestRecipient;
            
        if (currentPlayer.Social.IncomingRequests.Any(r => r.PlayerId == targetPlayer.PlayerUid))
            return RelationshipStatus.RequestInitiator;
            
        if (currentPlayer.Social.RejectedRequests.Any(r => r.PlayerId == targetPlayer.PlayerUid))
            return RelationshipStatus.Rejected;

        return RelationshipStatus.None;
    }

    // ==================== GET PLAYERS COUNT ====================

    private async Task GetPlayersCountAsync(TcpClient client, RpcRequest request)
    {
        try
        {
            _logger.LogInformation("👥 GetPlayersCount");
            string searchValue = GetStringParam(request, 0);
            var count = await _database.GetPlayersCountAsync(searchValue);
            await SendLongAsync(client, request.Id, count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ GetPlayersCount error");
            await SendLongAsync(client, request.Id, 0);
        }
    }

    // ==================== SEND FRIEND REQUEST ====================

    private async Task SendFriendRequestAsync(TcpClient client, RpcRequest request)
    {
        try
        {
            _logger.LogInformation("👥 SendFriendRequest");
            var player = await GetCurrentPlayerAsync(client);
            string friendId = GetStringParam(request, 0);
            
            _logger.LogInformation("SendFriendRequest: player={PlayerUid}, friendId={FriendId}", 
                player?.PlayerUid, friendId);
            
            if (player == null || string.IsNullOrEmpty(friendId))
            {
                await SendStatusAsync(client, request.Id, RelationshipStatus.None);
                return;
            }

            // Нельзя отправить заявку самому себе
            if (player.PlayerUid == friendId)
            {
                _logger.LogWarning("Player {PlayerUid} tried to send friend request to self", player.PlayerUid);
                await SendStatusAsync(client, request.Id, RelationshipStatus.None);
                return;
            }

            var targetPlayer = await _database.GetPlayerByUidAsync(friendId);
            if (targetPlayer == null)
            {
                _logger.LogWarning("Target player {FriendId} not found", friendId);
                await SendStatusAsync(client, request.Id, RelationshipStatus.None);
                return;
            }

            // Проверяем блокировки
            if (player.Social.BlockedPlayers.Contains(friendId))
            {
                _logger.LogWarning("Player {PlayerUid} tried to send request to blocked player {FriendId}", 
                    player.PlayerUid, friendId);
                await SendStatusAsync(client, request.Id, RelationshipStatus.Blocked);
                return;
            }

            if (targetPlayer.Social.BlockedPlayers.Contains(player.PlayerUid))
            {
                _logger.LogWarning("Player {PlayerUid} is blocked by target {FriendId}", 
                    player.PlayerUid, friendId);
                await SendStatusAsync(client, request.Id, RelationshipStatus.Blocked);
                return;
            }

            // Проверяем, не друзья ли уже
            if (player.Social.Friends.Contains(friendId))
            {
                _logger.LogInformation("Already friends with {FriendId}", friendId);
                await SendStatusAsync(client, request.Id, RelationshipStatus.Friend);
                return;
            }

            // Проверяем, нет ли уже активной заявки
            if (player.Social.OutgoingRequests.Any(r => r.PlayerId == friendId))
            {
                _logger.LogInformation("Already have outgoing request to {FriendId}", friendId);
                await SendStatusAsync(client, request.Id, RelationshipStatus.RequestRecipient);
                return;
            }

            // Проверяем, не отклоняли ли заявку недавно
            var rejectedRequest = player.Social.RejectedRequests?.FirstOrDefault(r => r.PlayerId == friendId);
            if (rejectedRequest != null)
            {
                var daysSinceRejection = (DateTime.UtcNow - rejectedRequest.RequestDate).TotalDays;
                if (daysSinceRejection < 7) // Нельзя отправить заявку повторно в течение 7 дней после отклонения
                {
                    _logger.LogInformation("Cannot send request to {FriendId}, was rejected {Days:F1} days ago", 
                        friendId, daysSinceRejection);
                    await SendStatusAsync(client, request.Id, RelationshipStatus.Rejected);
                    return;
                }
            }

            // Если у нас есть входящая заявка от этого игрока - автоматически принимаем
            if (player.Social.IncomingRequests.Any(r => r.PlayerId == friendId))
            {
                _logger.LogInformation("Auto-accepting incoming request from {FriendId}", friendId);
                
                var updatePlayer = Builders<Player>.Update
                    .PullFilter(p => p.Social.IncomingRequests, r => r.PlayerId == friendId)
                    .AddToSet(p => p.Social.Friends, friendId);

                var updateTarget = Builders<Player>.Update
                    .PullFilter(p => p.Social.OutgoingRequests, r => r.PlayerId == player.PlayerUid)
                    .AddToSet(p => p.Social.Friends, player.PlayerUid);

                await _database.UpdateTwoPlayersAsync(
                    player.Id.ToString(),
                    targetPlayer.Id.ToString(),
                    updatePlayer,
                    updateTarget);

                // Оповещаем о принятии заявки
                await NotifyFriendRequestAccepted(player, targetPlayer);
                
                await SendStatusAsync(client, request.Id, RelationshipStatus.Friend);
                return;
            }

            // Отправляем новую заявку
            var now = DateTime.UtcNow;
            
            var updatePlayerOutgoing = Builders<Player>.Update
                .Push(p => p.Social.OutgoingRequests, new FriendRequest
                {
                    PlayerId = friendId,
                    PlayerName = targetPlayer.Name,
                    RequestDate = now
                });

            var updateTargetIncoming = Builders<Player>.Update
                .Push(p => p.Social.IncomingRequests, new FriendRequest
                {
                    PlayerId = player.PlayerUid,
                    PlayerName = player.Name,
                    RequestDate = now
                });

            await _database.UpdateTwoPlayersAsync(
                player.Id.ToString(),
                targetPlayer.Id.ToString(),
                updatePlayerOutgoing,
                updateTargetIncoming);

            // Оповещаем о новой заявке
            await NotifyFriendRequestReceived(player, targetPlayer);

            await SendStatusAsync(client, request.Id, RelationshipStatus.RequestRecipient);
            _logger.LogInformation("Friend request sent from {PlayerUid} to {FriendId}", 
                player.PlayerUid, friendId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "SendFriendRequest error");
            await SendStatusAsync(client, request.Id, RelationshipStatus.None);
        }
    }

    // ==================== ACCEPT FRIEND REQUEST ====================

    private async Task AcceptFriendRequestAsync(TcpClient client, RpcRequest request)
    {
        try
        {
            _logger.LogInformation("👥 AcceptFriendRequest");
            var player = await GetCurrentPlayerAsync(client);
            string friendId = GetStringParam(request, 0);
            
            _logger.LogInformation("AcceptFriendRequest: player={PlayerUid}, friendId={FriendId}", 
                player?.PlayerUid, friendId);
            
            if (player == null || string.IsNullOrEmpty(friendId))
            {
                await SendStatusAsync(client, request.Id, RelationshipStatus.None);
                return;
            }

            var targetPlayer = await _database.GetPlayerByUidAsync(friendId);
            if (targetPlayer == null)
            {
                await SendStatusAsync(client, request.Id, RelationshipStatus.None);
                return;
            }

            // Проверяем, что заявка действительно существует
            if (!player.Social.IncomingRequests.Any(r => r.PlayerId == friendId))
            {
                _logger.LogWarning("No incoming request from {FriendId} for player {PlayerUid}", 
                    friendId, player.PlayerUid);
                await SendStatusAsync(client, request.Id, RelationshipStatus.None);
                return;
            }

            // Удаляем заявки и добавляем в друзья
            var updatePlayer = Builders<Player>.Update
                .PullFilter(p => p.Social.IncomingRequests, r => r.PlayerId == friendId)
                .AddToSet(p => p.Social.Friends, friendId);

            var updateTarget = Builders<Player>.Update
                .PullFilter(p => p.Social.OutgoingRequests, r => r.PlayerId == player.PlayerUid)
                .AddToSet(p => p.Social.Friends, player.PlayerUid);

            await _database.UpdateTwoPlayersAsync(
                player.Id.ToString(),
                targetPlayer.Id.ToString(),
                updatePlayer,
                updateTarget);

            // Оповещаем о принятии заявки
            await NotifyFriendRequestAccepted(player, targetPlayer);

            await SendStatusAsync(client, request.Id, RelationshipStatus.Friend);
            _logger.LogInformation("Accepted friend request from {FriendId} for player {PlayerUid}", 
                friendId, player.PlayerUid);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "AcceptFriendRequest error");
            await SendStatusAsync(client, request.Id, RelationshipStatus.None);
        }
    }

    // ==================== REJECT FRIEND REQUEST ====================

    private async Task RejectFriendRequestAsync(TcpClient client, RpcRequest request)
    {
        try
        {
            _logger.LogInformation("👥 RejectFriendRequest");
            var player = await GetCurrentPlayerAsync(client);
            string friendId = GetStringParam(request, 0);
            
            if (player == null || string.IsNullOrEmpty(friendId))
            {
                await SendStatusAsync(client, request.Id, RelationshipStatus.None);
                return;
            }

            var targetPlayer = await _database.GetPlayerByUidAsync(friendId);
            if (targetPlayer == null)
            {
                await SendStatusAsync(client, request.Id, RelationshipStatus.None);
                return;
            }

            // Проверяем, что заявка существует
            if (!player.Social.IncomingRequests.Any(r => r.PlayerId == friendId))
            {
                await SendStatusAsync(client, request.Id, RelationshipStatus.None);
                return;
            }

            // Удаляем заявку у получателя
            var updatePlayer = Builders<Player>.Update
                .PullFilter(p => p.Social.IncomingRequests, r => r.PlayerId == friendId);

            // Добавляем в RejectedRequests у отправителя
            var updateTarget = Builders<Player>.Update
                .PullFilter(p => p.Social.OutgoingRequests, r => r.PlayerId == player.PlayerUid)
                .Push(p => p.Social.RejectedRequests, new RejectedRequest
                {
                    PlayerId = player.PlayerUid,
                    PlayerName = player.Name,
                    RequestDate = DateTime.UtcNow
                });

            await _database.UpdateTwoPlayersAsync(
                player.Id.ToString(),
                targetPlayer.Id.ToString(),
                updatePlayer,
                updateTarget);

            // Оповещаем об отклонении
            await NotifyFriendRequestRejected(player, targetPlayer);

            await SendStatusAsync(client, request.Id, RelationshipStatus.None);
            _logger.LogInformation("Rejected friend request from {FriendId} for player {PlayerUid}", 
                friendId, player.PlayerUid);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "RejectFriendRequest error");
            await SendStatusAsync(client, request.Id, RelationshipStatus.None);
        }
    }

    // ==================== IGNORE FRIEND REQUEST (удалить без уведомления) ====================

    private async Task IgnoreFriendRequestAsync(TcpClient client, RpcRequest request)
    {
        try
        {
            _logger.LogInformation("👥 IgnoreFriendRequest");
            var player = await GetCurrentPlayerAsync(client);
            string friendId = GetStringParam(request, 0);
            
            if (player == null || string.IsNullOrEmpty(friendId))
            {
                await SendStatusAsync(client, request.Id, RelationshipStatus.None);
                return;
            }

            var targetPlayer = await _database.GetPlayerByUidAsync(friendId);
            if (targetPlayer == null)
            {
                await SendStatusAsync(client, request.Id, RelationshipStatus.None);
                return;
            }

            // Удаляем заявку у получателя
            var updatePlayer = Builders<Player>.Update
                .PullFilter(p => p.Social.IncomingRequests, r => r.PlayerId == friendId);

            // Удаляем заявку у отправителя (без добавления в Rejected)
            var updateTarget = Builders<Player>.Update
                .PullFilter(p => p.Social.OutgoingRequests, r => r.PlayerId == player.PlayerUid);

            await _database.UpdateTwoPlayersAsync(
                player.Id.ToString(),
                targetPlayer.Id.ToString(),
                updatePlayer,
                updateTarget);

            await SendStatusAsync(client, request.Id, RelationshipStatus.None);
            _logger.LogInformation("Ignored friend request from {FriendId}", friendId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "IgnoreFriendRequest error");
            await SendStatusAsync(client, request.Id, RelationshipStatus.None);
        }
    }

    // ==================== REVOKE FRIEND REQUEST (отменить свою заявку) ====================

    private async Task RevokeFriendRequestAsync(TcpClient client, RpcRequest request)
    {
        try
        {
            _logger.LogInformation("👥 RevokeFriendRequest");
            var player = await GetCurrentPlayerAsync(client);
            string friendId = GetStringParam(request, 0);
            
            if (player == null || string.IsNullOrEmpty(friendId))
            {
                await SendStatusAsync(client, request.Id, RelationshipStatus.None);
                return;
            }

            var targetPlayer = await _database.GetPlayerByUidAsync(friendId);
            if (targetPlayer == null)
            {
                await SendStatusAsync(client, request.Id, RelationshipStatus.None);
                return;
            }

            var updatePlayer = Builders<Player>.Update
                .PullFilter(p => p.Social.OutgoingRequests, r => r.PlayerId == friendId);

            var updateTarget = Builders<Player>.Update
                .PullFilter(p => p.Social.IncomingRequests, r => r.PlayerId == player.PlayerUid);

            await _database.UpdateTwoPlayersAsync(
                player.Id.ToString(),
                targetPlayer.Id.ToString(),
                updatePlayer,
                updateTarget);

            await SendStatusAsync(client, request.Id, RelationshipStatus.None);
            _logger.LogInformation("Revoked friend request to {FriendId}", friendId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "RevokeFriendRequest error");
            await SendStatusAsync(client, request.Id, RelationshipStatus.None);
        }
    }

    // ==================== REMOVE FRIEND ====================

    private async Task RemoveFriendAsync(TcpClient client, RpcRequest request)
    {
        try
        {
            _logger.LogInformation("👥 RemoveFriend");
            var player = await GetCurrentPlayerAsync(client);
            string friendId = GetStringParam(request, 0);
            
            if (player == null || string.IsNullOrEmpty(friendId))
            {
                await SendStatusAsync(client, request.Id, RelationshipStatus.None);
                return;
            }

            var targetPlayer = await _database.GetPlayerByUidAsync(friendId);
            
            var updatePlayer = Builders<Player>.Update
                .Pull(p => p.Social.Friends, friendId);

            var updateTarget = Builders<Player>.Update
                .Pull(p => p.Social.Friends, player.PlayerUid);

            if (targetPlayer != null)
            {
                await _database.UpdateTwoPlayersAsync(
                    player.Id.ToString(),
                    targetPlayer.Id.ToString(),
                    updatePlayer,
                    updateTarget);
            }
            else
            {
                await _database.UpdatePlayerFieldsAsync(player.Id.ToString(), updatePlayer);
            }

            // Оповещаем об удалении
            await NotifyFriendRemoved(player, friendId);

            await SendStatusAsync(client, request.Id, RelationshipStatus.None);
            _logger.LogInformation("Removed friend {FriendId}", friendId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "RemoveFriend error");
            await SendStatusAsync(client, request.Id, RelationshipStatus.None);
        }
    }

    // ==================== BLOCK FRIEND ====================

    private async Task BlockFriendAsync(TcpClient client, RpcRequest request)
    {
        try
        {
            _logger.LogInformation("👥 BlockFriend");
            var player = await GetCurrentPlayerAsync(client);
            string friendId = GetStringParam(request, 0);
            
            if (player == null || string.IsNullOrEmpty(friendId))
            {
                await SendStatusAsync(client, request.Id, RelationshipStatus.None);
                return;
            }

            var targetPlayer = await _database.GetPlayerByUidAsync(friendId);
            if (targetPlayer == null)
            {
                await SendStatusAsync(client, request.Id, RelationshipStatus.None);
                return;
            }

            var updatePlayer = Builders<Player>.Update
                .Pull(p => p.Social.Friends, friendId)
                .PullFilter(p => p.Social.IncomingRequests, r => r.PlayerId == friendId)
                .PullFilter(p => p.Social.OutgoingRequests, r => r.PlayerId == friendId)
                .AddToSet(p => p.Social.BlockedPlayers, friendId);

            var updateTarget = Builders<Player>.Update
                .Pull(p => p.Social.Friends, player.PlayerUid)
                .PullFilter(p => p.Social.IncomingRequests, r => r.PlayerId == player.PlayerUid)
                .PullFilter(p => p.Social.OutgoingRequests, r => r.PlayerId == player.PlayerUid);

            await _database.UpdateTwoPlayersAsync(
                player.Id.ToString(),
                targetPlayer.Id.ToString(),
                updatePlayer,
                updateTarget);

            // Оповещаем о блокировке
            await NotifyFriendBlocked(player, targetPlayer);

            await SendStatusAsync(client, request.Id, RelationshipStatus.Blocked);
            _logger.LogInformation("Blocked {FriendId}", friendId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "BlockFriend error");
            await SendStatusAsync(client, request.Id, RelationshipStatus.None);
        }
    }

    // ==================== UNBLOCK FRIEND ====================

    private async Task UnblockFriendAsync(TcpClient client, RpcRequest request)
    {
        try
        {
            _logger.LogInformation("👥 UnblockFriend");
            var player = await GetCurrentPlayerAsync(client);
            string friendId = GetStringParam(request, 0);
            
            if (player == null || string.IsNullOrEmpty(friendId))
            {
                await SendStatusAsync(client, request.Id, RelationshipStatus.None);
                return;
            }

            var updatePlayer = Builders<Player>.Update
                .Pull(p => p.Social.BlockedPlayers, friendId);

            await _database.UpdatePlayerFieldsAsync(player.Id.ToString(), updatePlayer);

            await SendStatusAsync(client, request.Id, RelationshipStatus.None);
            _logger.LogInformation("Unblocked {FriendId}", friendId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "UnblockFriend error");
            await SendStatusAsync(client, request.Id, RelationshipStatus.None);
        }
    }

    // ==================== GET ONLINE STATUS ====================

    private async Task GetOnlineStatusAsync(TcpClient client, RpcRequest request)
    {
        try
        {
            _logger.LogInformation("👥 GetOnlineStatus");
            string playerId = GetStringParam(request, 0);
            
            int status = 0;
            var targetPlayer = await _database.GetPlayerByUidAsync(playerId);
            if (targetPlayer != null)
            {
                status = (int)targetPlayer.OnlineStatus;
            }

            var intValue = new Integer { Value = status };
            var result = new BinaryValue 
            { 
                IsNull = false, 
                One = ByteString.CopyFrom(intValue.ToByteArray()) 
            };
            
            await _handler.WriteProtoResponseAsync(client, request.Id, result, null);
            _logger.LogInformation("👥 GetOnlineStatus {PlayerId}: {Status}", playerId, status);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ GetOnlineStatus error");
            var intValue = new Integer { Value = 0 };
            var result = new BinaryValue 
            { 
                IsNull = false, 
                One = ByteString.CopyFrom(intValue.ToByteArray()) 
            };
            await _handler.WriteProtoResponseAsync(client, request.Id, result, null);
        }
    }

    // ==================== SUBSCRIBE TO FRIEND STATUS ====================

    private async Task SubscribeToFriendStatusAsync(TcpClient client, RpcRequest request)
    {
        try
        {
            var session = _sessionManager.GetSessionByClient(client);
            if (session == null) 
            {
                await SendEmptyArrayAsync(client, request.Id);
                return;
            }

            string friendId = GetStringParam(request, 0);
            if (!string.IsNullOrEmpty(friendId))
            {
                _sessionManager.SubscribeToFriendStatus(session.Token, friendId);
                _logger.LogDebug("Client {Token} subscribed to friend {FriendId}", session.Token, friendId);
            }

            await SendEmptyArrayAsync(client, request.Id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "SubscribeToFriendStatus error");
            await SendEmptyArrayAsync(client, request.Id);
        }
    }

    // ==================== UNSUBSCRIBE FROM FRIEND STATUS ====================

    private async Task UnsubscribeFromFriendStatusAsync(TcpClient client, RpcRequest request)
    {
        try
        {
            var session = _sessionManager.GetSessionByClient(client);
            if (session == null) 
            {
                await SendEmptyArrayAsync(client, request.Id);
                return;
            }

            string friendId = GetStringParam(request, 0);
            if (!string.IsNullOrEmpty(friendId))
            {
                _sessionManager.UnsubscribeFromFriendStatus(session.Token, friendId);
                _logger.LogDebug("Client {Token} unsubscribed from friend {FriendId}", session.Token, friendId);
            }

            await SendEmptyArrayAsync(client, request.Id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "UnsubscribeFromFriendStatus error");
            await SendEmptyArrayAsync(client, request.Id);
        }
    }

    // ==================== GET AVATARS (заглушка) ====================

    private async Task GetAvatarsAsync(TcpClient client, RpcRequest request)
    {
        try
        {
            _logger.LogInformation("👥 GetAvatars (stub)");
            await SendEmptyArrayAsync(client, request.Id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ GetAvatars error");
            await SendEmptyArrayAsync(client, request.Id);
        }
    }

    // ==================== GET PLAYER BY ID ====================

    private async Task GetPlayerByIdAsync(TcpClient client, RpcRequest request)
    {
        try
        {
            _logger.LogInformation("👥 GetPlayerById");
            string playerId = GetStringParam(request, 0);
            
            var targetPlayer = await _database.GetPlayerByUidAsync(playerId);
            if (targetPlayer == null)
            {
                var result = new BinaryValue { IsNull = true };
                await _handler.WriteProtoResponseAsync(client, request.Id, result, null);
                return;
            }

            var player = new Player
            {
                Id = targetPlayer.PlayerUid,
                Uid = targetPlayer.PlayerUid,
                Name = targetPlayer.Name,
                AvatarId = targetPlayer.AvatarId ?? "",
                TimeInGame = targetPlayer.TimeInGame,
                RegistrationDate = new DateTimeOffset(targetPlayer.RegistrationDate).ToUnixTimeSeconds(),
                LogoutDate = new DateTimeOffset(targetPlayer.LastLogin).ToUnixTimeSeconds()
            };

            var result2 = new BinaryValue 
            { 
                IsNull = false, 
                One = ByteString.CopyFrom(player.ToByteArray()) 
            };
            
            await _handler.WriteProtoResponseAsync(client, request.Id, result2, null);
            _logger.LogInformation("👥 GetPlayerById {PlayerId}: {Name}", playerId, targetPlayer.Name);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ GetPlayerById error");
            var result = new BinaryValue { IsNull = true };
            await _handler.WriteProtoResponseAsync(client, request.Id, result, null);
        }
    }

    // ==================== GET PLAYER FRIEND BY ID ====================

    private async Task GetPlayerFriendByIdAsync(TcpClient client, RpcRequest request)
    {
        try
        {
            _logger.LogInformation("👥 GetPlayerFriendById");
            var currentPlayer = await GetCurrentPlayerAsync(client);
            string playerId = GetStringParam(request, 0);
            
            var targetPlayer = await _database.GetPlayerByUidAsync(playerId);
            if (targetPlayer == null)
            {
                var result = new BinaryValue { IsNull = true };
                await _handler.WriteProtoResponseAsync(client, request.Id, result, null);
                return;
            }

            var status = RelationshipStatus.None;
            if (currentPlayer != null)
            {
                status = GetRelationshipStatus(currentPlayer, targetPlayer);
            }

            var friend = CreatePlayerFriend(targetPlayer, status);
            var result2 = new BinaryValue 
            { 
                IsNull = false, 
                One = ByteString.CopyFrom(friend.ToByteArray()) 
            };
            
            await _handler.WriteProtoResponseAsync(client, request.Id, result2, null);
            _logger.LogInformation("👥 GetPlayerFriendById {PlayerId}: {Name}, status={Status}", 
                playerId, targetPlayer.Name, status);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ GetPlayerFriendById error");
            var result = new BinaryValue { IsNull = true };
            await _handler.WriteProtoResponseAsync(client, request.Id, result, null);
        }
    }

    // ==================== NOTIFICATION HELPERS ====================

    private async Task NotifyFriendRequestReceived(Models.Player fromPlayer, Models.Player toPlayer)
    {
        var toSession = _sessionManager.GetSessionByPlayerId(toPlayer.Id.ToString());
        if (toSession?.Client?.Connected == true)
        {
            var notification = new FriendRequestNotification
            {
                FromUid = fromPlayer.PlayerUid,
                FromName = fromPlayer.Name,
                Type = "received"
            };

            await _handler.SendEventAsync(toSession.Client, ServiceName, "OnFriendRequestReceived",
                ByteString.CopyFrom(notification.ToByteArray()));
        }
    }

    private async Task NotifyFriendRequestAccepted(Models.Player player, Models.Player friend)
    {
        // Оповещаем обоих игроков
        var sessions = new[]
        {
            _sessionManager.GetSessionByPlayerId(player.Id.ToString()),
            _sessionManager.GetSessionByPlayerId(friend.Id.ToString())
        };

        foreach (var session in sessions.Where(s => s?.Client?.Connected == true))
        {
            var notification = new FriendRequestNotification
            {
                FromUid = session!.PlayerObjectId == player.Id.ToString() ? friend.PlayerUid : player.PlayerUid,
                FromName = session.PlayerObjectId == player.Id.ToString() ? friend.Name : player.Name,
                Type = "accepted"
            };

            await _handler.SendEventAsync(session.Client, ServiceName, "OnFriendRequestAccepted",
                ByteString.CopyFrom(notification.ToByteArray()));
        }
    }

    private async Task NotifyFriendRequestRejected(Models.Player player, Models.Player friend)
    {
        var friendSession = _sessionManager.GetSessionByPlayerId(friend.Id.ToString());
        if (friendSession?.Client?.Connected == true)
        {
            var notification = new FriendRequestNotification
            {
                FromUid = player.PlayerUid,
                FromName = player.Name,
                Type = "rejected"
            };

            await _handler.SendEventAsync(friendSession.Client, ServiceName, "OnFriendRequestRejected",
                ByteString.CopyFrom(notification.ToByteArray()));
        }
    }

    private async Task NotifyFriendRemoved(Models.Player player, string friendUid)
    {
        var friend = await _database.GetPlayerByUidAsync(friendUid);
        if (friend == null) return;

        var friendSession = _sessionManager.GetSessionByPlayerId(friend.Id.ToString());
        if (friendSession?.Client?.Connected == true)
        {
            var notification = new FriendRequestNotification
            {
                FromUid = player.PlayerUid,
                FromName = player.Name,
                Type = "removed"
            };

            await _handler.SendEventAsync(friendSession.Client, ServiceName, "OnFriendRemoved",
                ByteString.CopyFrom(notification.ToByteArray()));
        }
    }

    private async Task NotifyFriendBlocked(Models.Player player, Models.Player friend)
    {
        var friendSession = _sessionManager.GetSessionByPlayerId(friend.Id.ToString());
        if (friendSession?.Client?.Connected == true)
        {
            var notification = new FriendRequestNotification
            {
                FromUid = player.PlayerUid,
                FromName = player.Name,
                Type = "blocked"
            };

            await _handler.SendEventAsync(friendSession.Client, ServiceName, "OnFriendBlocked",
                ByteString.CopyFrom(notification.ToByteArray()));
        }
    }

    // ==================== HELPER METHODS ====================

    private async Task<List<PlayerFriend>> GetPlayersWithStatus(List<string> uids, RelationshipStatus defaultStatus)
    {
        if (uids.Count == 0)
            return new List<PlayerFriend>();

        var players = await _database.GetPlayersByUidsAsync(uids);
        var result = new List<PlayerFriend>();

        foreach (var player in players)
        {
            result.Add(CreatePlayerFriend(player, defaultStatus));
        }

        return result;
    }

    private async Task SendPlayerFriendListAsync(TcpClient client, string requestId, List<PlayerFriend> friends)
    {
        var result = new BinaryValue { IsNull = false };
        foreach (var friend in friends)
        {
            result.Array.Add(ByteString.CopyFrom(friend.ToByteArray()));
        }
        await _handler.WriteProtoResponseAsync(client, requestId, result, null);
    }

    private async Task SendEmptyArrayAsync(TcpClient client, string requestId)
    {
        var result = new BinaryValue { IsNull = false };
        await _handler.WriteProtoResponseAsync(client, requestId, result, null);
    }

    private async Task SendLongAsync(TcpClient client, string requestId, long value)
    {
        var longValue = new Axlebolt.RpcSupport.Protobuf.Long { Value = value };
        var result = new BinaryValue 
        { 
            IsNull = false, 
            One = ByteString.CopyFrom(longValue.ToByteArray()) 
        };
        await _handler.WriteProtoResponseAsync(client, requestId, result, null);
    }

    private async Task SendStatusAsync(TcpClient client, string requestId, RelationshipStatus status)
    {
        var intValue = new Integer { Value = (int)status };
        var result = new BinaryValue
        {
            IsNull = false,
            One = ByteString.CopyFrom(intValue.ToByteArray())
        };
        await _handler.WriteProtoResponseAsync(client, requestId, result, null);
    }
}