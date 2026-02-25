using System.Net.Sockets;
using Axlebolt.RpcSupport.Protobuf;
using Axlebolt.Bolt.Protobuf;
using StandRiseServer.Core;
using Google.Protobuf;

namespace StandRiseServer.Services;

public class MatchmakingRemoteService
{
    private readonly ProtobufHandler _handler;
    private readonly SessionManager _sessionManager;
    private readonly DatabaseService _database;
    
    // Хранилище активных лобби
    private static readonly Dictionary<string, Lobby> _lobbies = new();
    private static readonly Dictionary<string, string> _playerLobbies = new(); // playerId -> lobbyId
    private static readonly Dictionary<string, List<LobbyInvite>> _playerInvites = new(); // playerId -> invites

    public MatchmakingRemoteService(ProtobufHandler handler, SessionManager sessionManager, DatabaseService database)
    {
        _handler = handler;
        _sessionManager = sessionManager;
        _database = database;
        
        Console.WriteLine("🎮 Registering MatchmakingRemoteService handlers...");
        _handler.RegisterHandler("MatchmakingRemoteService", "getInvitesToLobby", GetInvitesToLobbyAsync);
        _handler.RegisterHandler("MatchmakingRemoteService", "getLobbyInvites", GetInvitesToLobbyAsync);
        _handler.RegisterHandler("MatchmakingRemoteService", "findMatch", FindMatchAsync);
        _handler.RegisterHandler("MatchmakingRemoteService", "cancelMatchmaking", CancelMatchmakingAsync);
        _handler.RegisterHandler("MatchmakingRemoteService", "createLobby", CreateLobbyAsync);
        _handler.RegisterHandler("MatchmakingRemoteService", "createLobbyWithSpectators", CreateLobbyWithSpectatorsAsync);
        _handler.RegisterHandler("MatchmakingRemoteService", "joinLobby", JoinLobbyAsync);
        _handler.RegisterHandler("MatchmakingRemoteService", "joinLobbyAs", JoinLobbyAsAsync);
        _handler.RegisterHandler("MatchmakingRemoteService", "leaveLobby", LeaveLobbyAsync);
        _handler.RegisterHandler("MatchmakingRemoteService", "getLobby", GetLobbyAsync);
        _handler.RegisterHandler("MatchmakingRemoteService", "getLobbyMembers", GetLobbyMembersAsync);
        _handler.RegisterHandler("MatchmakingRemoteService", "getLobbyOwner", GetLobbyOwnerAsync);
        _handler.RegisterHandler("MatchmakingRemoteService", "inviteToLobby", InviteToLobbyAsync);
        _handler.RegisterHandler("MatchmakingRemoteService", "invitePlayerToLobby", InviteToLobbyAsync);
        _handler.RegisterHandler("MatchmakingRemoteService", "invitePlayerToLobbyAs", InviteToLobbyAsAsync);
        _handler.RegisterHandler("MatchmakingRemoteService", "kickPlayerFromLobby", KickPlayerFromLobbyAsync);
        _handler.RegisterHandler("MatchmakingRemoteService", "setLobbyOwner", SetLobbyOwnerAsync);
        _handler.RegisterHandler("MatchmakingRemoteService", "setLobbyData", SetLobbyDataAsync);
        _handler.RegisterHandler("MatchmakingRemoteService", "setLobbyType", SetLobbyTypeAsync);
        _handler.RegisterHandler("MatchmakingRemoteService", "setLobbyName", SetLobbyNameAsync);
        _handler.RegisterHandler("MatchmakingRemoteService", "setLobbyJoinable", SetLobbyJoinableAsync);
        _handler.RegisterHandler("MatchmakingRemoteService", "setLobbyMaxMembers", SetLobbyMaxMembersAsync);
        _handler.RegisterHandler("MatchmakingRemoteService", "setLobbyMaxSpectators", SetLobbyMaxSpectatorsAsync);
        _handler.RegisterHandler("MatchmakingRemoteService", "requestLobbyList", RequestLobbyListAsync);
        _handler.RegisterHandler("MatchmakingRemoteService", "changeLobbyPlayerType", ChangeLobbyPlayerTypeAsync);
        _handler.RegisterHandler("MatchmakingRemoteService", "changeLobbyOtherPlayerType", ChangeLobbyOtherPlayerTypeAsync);
        _handler.RegisterHandler("MatchmakingRemoteService", "sendLobbyChatMsg", SendLobbyChatMsgAsync);
        _handler.RegisterHandler("MatchmakingRemoteService", "refuseInvitationToLobby", RefuseInvitationToLobbyAsync);
        _handler.RegisterHandler("MatchmakingRemoteService", "revokePlayerInvitationToLobby", RevokePlayerInvitationToLobbyAsync);
        _handler.RegisterHandler("MatchmakingRemoteService", "deleteLobbyData", DeleteLobbyDataAsync);
        _handler.RegisterHandler("MatchmakingRemoteService", "setLobbyGameServer", SetLobbyGameServerAsync);
        _handler.RegisterHandler("MatchmakingRemoteService", "getLobbyGameServer", GetLobbyGameServerAsync);
        _handler.RegisterHandler("MatchmakingRemoteService", "getGameServerDetails", GetGameServerDetailsAsync);
        _handler.RegisterHandler("MatchmakingRemoteService", "getGameServerPlayers", GetGameServerPlayersAsync);
        _handler.RegisterHandler("MatchmakingRemoteService", "requestInternetServerList", RequestInternetServerListAsync);
        _handler.RegisterHandler("MatchmakingRemoteService", "setLobbyPhotonGame", SetLobbyPhotonGameAsync);
        _handler.RegisterHandler("MatchmakingRemoteService", "getLobbyPhotonGame", GetLobbyPhotonGameAsync);
        Console.WriteLine("🎮 MatchmakingRemoteService handlers registered!");
    }

    // Helper: получить клиента по playerId
    private TcpClient? GetClientByPlayerId(string playerId)
    {
        foreach (var session in _sessionManager.GetAllSessions())
        {
            var player = _database.GetPlayerByTokenAsync(session.Token).Result;
            if (player?.PlayerUid == playerId)
                return session.Client;
        }
        return null;
    }

    // Helper: отправить событие всем участникам лобби
    private async Task BroadcastToLobby(string lobbyId, string eventName, ByteString data, string? excludePlayerId = null)
    {
        if (!_lobbies.TryGetValue(lobbyId, out var lobby)) return;
        
        foreach (var member in lobby.Members)
        {
            if (member.Player?.Id == excludePlayerId) continue;
            var client = GetClientByPlayerId(member.Player?.Id ?? "");
            if (client != null)
            {
                try
                {
                    await _handler.SendEventAsync(client, "MatchmakingRemoteEventListener", eventName, data);
                }
                catch { }
            }
        }
        
        foreach (var spectator in lobby.Spectators)
        {
            if (spectator.Player?.Id == excludePlayerId) continue;
            var client = GetClientByPlayerId(spectator.Player?.Id ?? "");
            if (client != null)
            {
                try
                {
                    await _handler.SendEventAsync(client, "MatchmakingRemoteEventListener", eventName, data);
                }
                catch { }
            }
        }
    }

    private async Task GetInvitesToLobbyAsync(TcpClient client, RpcRequest request)
    {
        try
        {
            Console.WriteLine("🎮 GetInvitesToLobby");
            
            var session = _sessionManager.GetSessionByClient(client);
            if (session == null)
            {
                session = _sessionManager.GetAllSessions().FirstOrDefault();
                if (session != null) session.Client = client;
            }
            
            var player = session != null ? await _database.GetPlayerByTokenAsync(session.Token) : null;
            var playerId = player?.PlayerUid ?? "";
            
            var result = new BinaryValue { IsNull = false };
            
            // Возвращаем приглашения для этого игрока
            if (_playerInvites.TryGetValue(playerId, out var invites))
            {
                foreach (var invite in invites)
                {
                    result.Array.Add(ByteString.CopyFrom(invite.ToByteArray()));
                }
            }
            
            await _handler.WriteProtoResponseAsync(client, request.Id, result, null);
            Console.WriteLine($"🎮 GetInvitesToLobby: {result.Array.Count} invites");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ GetInvitesToLobby: {ex.Message}");
            var result = new BinaryValue { IsNull = false };
            await _handler.WriteProtoResponseAsync(client, request.Id, result, null);
        }
    }

    private async Task FindMatchAsync(TcpClient client, RpcRequest request)
    {
        try
        {
            Console.WriteLine("=== FindMatch Request ===");
            
            // Заглушка для поиска матча
            var result = new BinaryValue { IsNull = true };
            await _handler.WriteProtoResponseAsync(client, request.Id, result, null);
            Console.WriteLine("✅ FindMatch response sent");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error in FindMatch: {ex.Message}");
        }
    }

    private async Task CancelMatchmakingAsync(TcpClient client, RpcRequest request)
    {
        try
        {
            Console.WriteLine("=== CancelMatchmaking Request ===");
            
            var result = new BinaryValue { IsNull = true };
            await _handler.WriteProtoResponseAsync(client, request.Id, result, null);
            Console.WriteLine("✅ CancelMatchmaking response sent");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error in CancelMatchmaking: {ex.Message}");
        }
    }

    private async Task CreateLobbyAsync(TcpClient client, RpcRequest request)
    {
        try
        {
            Console.WriteLine("=== CreateLobby Request ===");
            
            var session = _sessionManager.GetSessionByClient(client);
            if (session == null)
            {
                session = _sessionManager.GetAllSessions().FirstOrDefault();
                if (session != null) session.Client = client;
            }
            
            if (session == null)
            {
                Console.WriteLine("❌ No session found for CreateLobby");
                await _handler.WriteProtoResponseAsync(client, request.Id, null,
                    new RpcException { Id = request.Id, Code = 401, Property = null });
                return;
            }

            // Получаем данные игрока из базы
            var player = await _database.GetPlayerByTokenAsync(session.Token);
            var playerId = player?.PlayerUid ?? session.Token;
            var playerName = player?.Name ?? "Player";
            var playerLevel = player?.Level ?? 1;
            var playerAvatar = player?.AvatarId ?? player?.Avatar ?? "";
            
            Console.WriteLine($"🎮 Player data for lobby:");
            Console.WriteLine($"   - PlayerId (PlayerUid): {playerId}");
            Console.WriteLine($"   - PlayerName: {playerName}");
            Console.WriteLine($"   - PlayerLevel: {playerLevel}");
            Console.WriteLine($"   - Session Token: {session.Token}");

            // Парсим параметры: name, lobbyType, maxMembers
            string lobbyName = playerName; // По умолчанию имя лобби = имя игрока
            LobbyType lobbyType = LobbyType.Public; // По умолчанию открытое лобби
            int maxMembers = 10;
            
            if (request.Params.Count > 0 && request.Params[0].One != null)
            {
                var nameStr = Axlebolt.RpcSupport.Protobuf.String.Parser.ParseFrom(request.Params[0].One);
                lobbyName = nameStr.Value;
            }
            if (request.Params.Count > 1 && request.Params[1].One != null)
            {
                var typeInt = Axlebolt.RpcSupport.Protobuf.Integer.Parser.ParseFrom(request.Params[1].One);
                lobbyType = (LobbyType)typeInt.Value;
            }
            if (request.Params.Count > 2 && request.Params[2].One != null)
            {
                var membersInt = Axlebolt.RpcSupport.Protobuf.Integer.Parser.ParseFrom(request.Params[2].One);
                maxMembers = membersInt.Value;
            }

            // Создаем лобби
            var lobbyId = Guid.NewGuid().ToString();
            var lobby = new Lobby
            {
                Id = lobbyId,
                OwnerPlayerId = playerId,
                Name = lobbyName,
                LobbyType = lobbyType,
                Joinable = true, // Лобби сразу открыто
                MaxMembers = maxMembers,
                MaxSpectators = 0
            };
            
            // Добавляем владельца как участника с правильными данными
            var ownerMember = new PlayerFriend
            {
                Player = new Axlebolt.Bolt.Protobuf.Player
                {
                    Id = playerId,
                    Uid = playerId,
                    Name = playerName,
                    AvatarId = playerAvatar,
                    TimeInGame = 0,
                    PlayerStatus = new PlayerStatus { OnlineStatus = OnlineStatus.StateOnline },
                    LogoutDate = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                    RegistrationDate = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
                },
                RelationshipStatus = RelationshipStatus.None,
                LastRelationshipUpdate = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
            };
            lobby.Members.Add(ownerMember);
            
            // Сохраняем лобби
            _lobbies[lobbyId] = lobby;
            _playerLobbies[playerId] = lobbyId;
            
            Console.WriteLine($"✅ Created lobby: {lobbyId}, Owner: {playerName} (Level {playerLevel}), Type: {lobbyType}, Joinable: {lobby.Joinable}");
            
            var result = new BinaryValue 
            { 
                IsNull = false, 
                One = ByteString.CopyFrom(lobby.ToByteArray()) 
            };
            
            await _handler.WriteProtoResponseAsync(client, request.Id, result, null);
            Console.WriteLine("✅ CreateLobby response sent");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error in CreateLobby: {ex.Message}");
            Console.WriteLine($"Stack: {ex.StackTrace}");
        }
    }
    
    private async Task CreateLobbyWithSpectatorsAsync(TcpClient client, RpcRequest request)
    {
        try
        {
            Console.WriteLine("=== CreateLobbyWithSpectators Request ===");
            
            var session = _sessionManager.GetSessionByClient(client);
            if (session == null)
            {
                session = _sessionManager.GetAllSessions().FirstOrDefault();
                if (session != null) session.Client = client;
            }
            
            if (session == null)
            {
                Console.WriteLine("❌ No session found for CreateLobbyWithSpectators");
                await _handler.WriteProtoResponseAsync(client, request.Id, null,
                    new RpcException { Id = request.Id, Code = 401, Property = null });
                return;
            }

            // Получаем данные игрока из базы
            var player = await _database.GetPlayerByTokenAsync(session.Token);
            
            Console.WriteLine($"🎮 Session token: {session.Token}");
            Console.WriteLine($"🎮 Player found: {player != null}");
            
            // Используем PlayerUid как ID игрока, Name как никнейм
            var playerId = player?.PlayerUid ?? session.Token;
            var playerName = player?.Name ?? "Player";
            var playerLevel = player?.Level ?? 1;
            var playerAvatar = player?.AvatarId ?? player?.Avatar ?? "";
            
            Console.WriteLine($"🎮 Player data for lobby with spectators:");
            Console.WriteLine($"   - PlayerId (PlayerUid): {playerId}");
            Console.WriteLine($"   - PlayerName: {playerName}");
            Console.WriteLine($"   - PlayerLevel: {playerLevel}");
            Console.WriteLine($"   - Avatar: {playerAvatar}");

            // Парсим параметры: name, lobbyType, maxMembers, maxSpectators
            string lobbyName = playerName; // По умолчанию имя лобби = имя игрока
            LobbyType lobbyType = LobbyType.Public; // По умолчанию открытое лобби
            int maxMembers = 10;
            int maxSpectators = 2;
            
            if (request.Params.Count > 0 && request.Params[0].One != null)
            {
                var nameStr = Axlebolt.RpcSupport.Protobuf.String.Parser.ParseFrom(request.Params[0].One);
                lobbyName = nameStr.Value;
            }
            if (request.Params.Count > 1 && request.Params[1].One != null)
            {
                var typeInt = Axlebolt.RpcSupport.Protobuf.Integer.Parser.ParseFrom(request.Params[1].One);
                lobbyType = (LobbyType)typeInt.Value;
            }
            if (request.Params.Count > 2 && request.Params[2].One != null)
            {
                var membersInt = Axlebolt.RpcSupport.Protobuf.Integer.Parser.ParseFrom(request.Params[2].One);
                maxMembers = membersInt.Value;
            }
            if (request.Params.Count > 3 && request.Params[3].One != null)
            {
                var spectatorsInt = Axlebolt.RpcSupport.Protobuf.Integer.Parser.ParseFrom(request.Params[3].One);
                maxSpectators = spectatorsInt.Value;
            }

            var lobbyId = Guid.NewGuid().ToString();
            var lobby = new Lobby
            {
                Id = lobbyId,
                OwnerPlayerId = playerId,
                Name = lobbyName,
                LobbyType = lobbyType,
                Joinable = true, // Лобби сразу открыто для присоединения
                MaxMembers = maxMembers,
                MaxSpectators = maxSpectators
            };
            
            // Создаем владельца лобби с правильными данными
            var ownerMember = new PlayerFriend
            {
                Player = new Axlebolt.Bolt.Protobuf.Player
                {
                    Id = playerId,
                    Uid = playerId,
                    Name = playerName,
                    AvatarId = playerAvatar,
                    TimeInGame = 0,
                    PlayerStatus = new PlayerStatus { OnlineStatus = OnlineStatus.StateOnline },
                    LogoutDate = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                    RegistrationDate = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
                },
                RelationshipStatus = RelationshipStatus.None,
                LastRelationshipUpdate = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
            };
            lobby.Members.Add(ownerMember);
            
            _lobbies[lobbyId] = lobby;
            _playerLobbies[playerId] = lobbyId;
            
            Console.WriteLine($"✅ Created lobby: {lobbyId}, Owner: {playerName} (Level {playerLevel}), Type: {lobbyType}, Joinable: {lobby.Joinable}");
            
            var result = new BinaryValue 
            { 
                IsNull = false, 
                One = ByteString.CopyFrom(lobby.ToByteArray()) 
            };
            
            await _handler.WriteProtoResponseAsync(client, request.Id, result, null);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error in CreateLobbyWithSpectators: {ex.Message}");
        }
    }

    private async Task JoinLobbyAsync(TcpClient client, RpcRequest request)
    {
        try
        {
            Console.WriteLine("=== JoinLobby Request ===");
            
            var session = _sessionManager.GetSessionByClient(client);
            if (session == null)
            {
                session = _sessionManager.GetAllSessions().FirstOrDefault();
                if (session != null) session.Client = client;
            }
            
            string lobbyId = "";
            if (request.Params.Count > 0 && request.Params[0].One != null)
            {
                var lobbyIdStr = Axlebolt.RpcSupport.Protobuf.String.Parser.ParseFrom(request.Params[0].One);
                lobbyId = lobbyIdStr.Value;
            }
            
            Console.WriteLine($"🎮 JoinLobby: lobbyId={lobbyId}");
            
            if (_lobbies.TryGetValue(lobbyId, out var lobby))
            {
                var player = await _database.GetPlayerByTokenAsync(session?.Token ?? "");
                var playerId = player?.PlayerUid ?? session?.Token ?? "";
                var playerName = player?.Name ?? "Player";
                
                // Проверяем не в лобби ли уже игрок
                if (lobby.Members.Any(m => m.Player?.Id == playerId))
                {
                    Console.WriteLine($"⚠️ Player {playerName} already in lobby");
                    var existingResult = new BinaryValue 
                    { 
                        IsNull = false, 
                        One = ByteString.CopyFrom(lobby.ToByteArray()) 
                    };
                    await _handler.WriteProtoResponseAsync(client, request.Id, existingResult, null);
                    return;
                }
                
                // Создаём нового участника
                var member = new PlayerFriend
                {
                    Player = new Axlebolt.Bolt.Protobuf.Player
                    {
                        Id = playerId,
                        Uid = playerId,
                        Name = playerName,
                        AvatarId = player?.AvatarId ?? player?.Avatar ?? "",
                        TimeInGame = player?.TimeInGame ?? 0,
                        PlayerStatus = new PlayerStatus { OnlineStatus = OnlineStatus.StateOnline },
                        LogoutDate = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                        RegistrationDate = player != null ? new DateTimeOffset(player.RegistrationDate).ToUnixTimeSeconds() : DateTimeOffset.UtcNow.ToUnixTimeSeconds()
                    },
                    RelationshipStatus = RelationshipStatus.None,
                    LastRelationshipUpdate = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
                };
                
                // Добавляем игрока в лобби
                lobby.Members.Add(member);
                _playerLobbies[playerId] = lobbyId;
                
                // Удаляем приглашение если было
                if (_playerInvites.TryGetValue(playerId, out var invites))
                {
                    invites.RemoveAll(i => i.LobbyId == lobbyId);
                }
                // Удаляем из Invites лобби (RepeatedField не имеет RemoveAll)
                var inviteToRemove = lobby.Invites.FirstOrDefault(i => i.Player?.Id == playerId);
                if (inviteToRemove != null)
                    lobby.Invites.Remove(inviteToRemove);
                
                Console.WriteLine($"✅ Player {playerName} joined lobby {lobbyId}");
                
                // Отправляем событие всем участникам лобби о новом игроке
                foreach (var existingMember in lobby.Members)
                {
                    if (existingMember.Player?.Id == playerId) continue; // Не отправляем себе
                    
                    var memberClient = GetClientByPlayerId(existingMember.Player?.Id ?? "");
                    if (memberClient != null)
                    {
                        try
                        {
                            await _handler.SendEventAsync(memberClient, "MatchmakingRemoteEventListener", "onPlayerJoinedLobby",
                                ByteString.CopyFrom(member.ToByteArray()));
                            Console.WriteLine($"📤 Sent onPlayerJoinedLobby to {existingMember.Player?.Name}");
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"❌ Failed to notify {existingMember.Player?.Name}: {ex.Message}");
                        }
                    }
                }
                
                var result = new BinaryValue 
                { 
                    IsNull = false, 
                    One = ByteString.CopyFrom(lobby.ToByteArray()) 
                };
                await _handler.WriteProtoResponseAsync(client, request.Id, result, null);
            }
            else
            {
                Console.WriteLine($"❌ Lobby {lobbyId} not found");
                await _handler.WriteProtoResponseAsync(client, request.Id, null,
                    new RpcException { Id = request.Id, Code = 404, Property = null });
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error in JoinLobby: {ex.Message}");
            Console.WriteLine($"Stack: {ex.StackTrace}");
        }
    }
    
    private async Task JoinLobbyAsAsync(TcpClient client, RpcRequest request)
    {
        try
        {
            Console.WriteLine("=== JoinLobbyAs Request ===");
            await JoinLobbyAsync(client, request);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error in JoinLobbyAs: {ex.Message}");
        }
    }

    private async Task LeaveLobbyAsync(TcpClient client, RpcRequest request)
    {
        try
        {
            Console.WriteLine("=== LeaveLobby Request ===");
            
            var session = _sessionManager.GetSessionByClient(client);
            if (session == null)
            {
                session = _sessionManager.GetAllSessions().FirstOrDefault();
                if (session != null) session.Client = client;
            }
            
            var player = await _database.GetPlayerByTokenAsync(session?.Token ?? "");
            var playerId = player?.PlayerUid ?? session?.Token ?? "";
            var playerName = player?.Name ?? "Player";
            
            if (_playerLobbies.TryGetValue(playerId, out var lobbyId))
            {
                if (_lobbies.TryGetValue(lobbyId, out var lobby))
                {
                    // Находим участника для удаления
                    var memberToRemove = lobby.Members.FirstOrDefault(m => m.Player?.Id == playerId);
                    if (memberToRemove != null)
                    {
                        lobby.Members.Remove(memberToRemove);
                        
                        // Отправляем событие всем оставшимся участникам
                        foreach (var member in lobby.Members)
                        {
                            var memberClient = GetClientByPlayerId(member.Player?.Id ?? "");
                            if (memberClient != null)
                            {
                                try
                                {
                                    await _handler.SendEventAsync(memberClient, "MatchmakingRemoteEventListener", "onPlayerLeftLobby",
                                        ByteString.CopyFrom(memberToRemove.ToByteArray()));
                                    Console.WriteLine($"📤 Sent onPlayerLeftLobby to {member.Player?.Name}");
                                }
                                catch { }
                            }
                        }
                    }
                    
                    // Если лобби пустое - удаляем его
                    if (lobby.Members.Count == 0)
                    {
                        _lobbies.Remove(lobbyId);
                        Console.WriteLine($"🗑️ Lobby {lobbyId} deleted (empty)");
                    }
                    // Если владелец ушел - передаем владение
                    else if (lobby.OwnerPlayerId == playerId && lobby.Members.Count > 0)
                    {
                        var newOwner = lobby.Members[0];
                        lobby.OwnerPlayerId = newOwner.Player?.Id ?? "";
                        Console.WriteLine($"👑 New lobby owner: {newOwner.Player?.Name}");
                        
                        // Уведомляем всех о новом владельце
                        foreach (var member in lobby.Members)
                        {
                            var memberClient = GetClientByPlayerId(member.Player?.Id ?? "");
                            if (memberClient != null)
                            {
                                try
                                {
                                    await _handler.SendEventAsync(memberClient, "MatchmakingRemoteEventListener", "onLobbyOwnerChanged",
                                        ByteString.CopyFrom(newOwner.ToByteArray()));
                                }
                                catch { }
                            }
                        }
                    }
                }
                _playerLobbies.Remove(playerId);
                Console.WriteLine($"✅ Player {playerName} left lobby {lobbyId}");
            }
            
            var result = new BinaryValue { IsNull = true };
            await _handler.WriteProtoResponseAsync(client, request.Id, result, null);
            Console.WriteLine("✅ LeaveLobby response sent");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error in LeaveLobby: {ex.Message}");
            Console.WriteLine($"Stack: {ex.StackTrace}");
        }
    }

    private async Task GetLobbyAsync(TcpClient client, RpcRequest request)
    {
        try
        {
            Console.WriteLine("=== GetLobby Request ===");
            
            string lobbyId = "";
            if (request.Params.Count > 0 && request.Params[0].One != null)
            {
                var lobbyIdStr = Axlebolt.RpcSupport.Protobuf.String.Parser.ParseFrom(request.Params[0].One);
                lobbyId = lobbyIdStr.Value;
            }
            
            if (!string.IsNullOrEmpty(lobbyId) && _lobbies.TryGetValue(lobbyId, out var lobby))
            {
                var result = new BinaryValue 
                { 
                    IsNull = false, 
                    One = ByteString.CopyFrom(lobby.ToByteArray()) 
                };
                await _handler.WriteProtoResponseAsync(client, request.Id, result, null);
                Console.WriteLine($"✅ GetLobby response sent for {lobbyId}");
            }
            else
            {
                var result = new BinaryValue { IsNull = true };
                await _handler.WriteProtoResponseAsync(client, request.Id, result, null);
                Console.WriteLine("✅ GetLobby response sent (no lobby)");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error in GetLobby: {ex.Message}");
        }
    }
    
    private async Task GetLobbyMembersAsync(TcpClient client, RpcRequest request)
    {
        try
        {
            Console.WriteLine("=== GetLobbyMembers Request ===");
            
            string lobbyId = "";
            if (request.Params.Count > 0 && request.Params[0].One != null)
            {
                var lobbyIdStr = Axlebolt.RpcSupport.Protobuf.String.Parser.ParseFrom(request.Params[0].One);
                lobbyId = lobbyIdStr.Value;
            }
            
            var result = new BinaryValue { IsNull = false };
            
            if (!string.IsNullOrEmpty(lobbyId) && _lobbies.TryGetValue(lobbyId, out var lobby))
            {
                foreach (var member in lobby.Members)
                {
                    if (member.Player != null)
                    {
                        result.Array.Add(ByteString.CopyFrom(member.Player.ToByteArray()));
                    }
                }
            }
            
            await _handler.WriteProtoResponseAsync(client, request.Id, result, null);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error in GetLobbyMembers: {ex.Message}");
        }
    }
    
    private async Task GetLobbyOwnerAsync(TcpClient client, RpcRequest request)
    {
        try
        {
            Console.WriteLine("=== GetLobbyOwner Request ===");
            
            string lobbyId = "";
            if (request.Params.Count > 0 && request.Params[0].One != null)
            {
                var lobbyIdStr = Axlebolt.RpcSupport.Protobuf.String.Parser.ParseFrom(request.Params[0].One);
                lobbyId = lobbyIdStr.Value;
            }
            
            if (!string.IsNullOrEmpty(lobbyId) && _lobbies.TryGetValue(lobbyId, out var lobby))
            {
                var owner = lobby.Members.FirstOrDefault(m => m.Player?.Id == lobby.OwnerPlayerId);
                if (owner?.Player != null)
                {
                    var result = new BinaryValue 
                    { 
                        IsNull = false, 
                        One = ByteString.CopyFrom(owner.Player.ToByteArray()) 
                    };
                    await _handler.WriteProtoResponseAsync(client, request.Id, result, null);
                    return;
                }
            }
            
            var emptyResult = new BinaryValue { IsNull = true };
            await _handler.WriteProtoResponseAsync(client, request.Id, emptyResult, null);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error in GetLobbyOwner: {ex.Message}");
        }
    }

    private async Task InviteToLobbyAsync(TcpClient client, RpcRequest request)
    {
        try
        {
            Console.WriteLine("🎮 InviteToLobby");
            
            var session = _sessionManager.GetSessionByClient(client);
            if (session == null)
            {
                session = _sessionManager.GetAllSessions().FirstOrDefault();
                if (session != null) session.Client = client;
            }
            
            var player = session != null ? await _database.GetPlayerByTokenAsync(session.Token) : null;
            var playerId = player?.PlayerUid ?? "";
            
            // Получаем ID приглашаемого игрока
            string invitedPlayerId = "";
            if (request.Params.Count > 0 && request.Params[0].One != null)
            {
                invitedPlayerId = Axlebolt.RpcSupport.Protobuf.String.Parser.ParseFrom(request.Params[0].One).Value;
            }
            
            Console.WriteLine($"🎮 InviteToLobby: inviter={playerId}, invited={invitedPlayerId}");
            
            if (string.IsNullOrEmpty(invitedPlayerId))
            {
                Console.WriteLine("❌ InviteToLobby: invitedPlayerId is empty");
                var emptyResult = new BinaryValue { IsNull = true };
                await _handler.WriteProtoResponseAsync(client, request.Id, emptyResult, null);
                return;
            }
            
            if (!_playerLobbies.TryGetValue(playerId, out var lobbyId) || !_lobbies.TryGetValue(lobbyId, out var lobby))
            {
                Console.WriteLine($"❌ InviteToLobby: player {playerId} not in any lobby");
                var emptyResult = new BinaryValue { IsNull = true };
                await _handler.WriteProtoResponseAsync(client, request.Id, emptyResult, null);
                return;
            }
            
            // Создаем приглашение
            var inviterFriend = lobby.Members.FirstOrDefault(m => m.Player?.Id == playerId);
            var invite = new LobbyInvite
            {
                LobbyId = lobbyId,
                Inviter = inviterFriend?.Player,
                Timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                PlayerType = LobbyPlayerType.Member
            };
            
            // Добавляем в список приглашений игрока
            if (!_playerInvites.ContainsKey(invitedPlayerId))
                _playerInvites[invitedPlayerId] = new List<LobbyInvite>();
            
            // Удаляем старое приглашение в это лобби если было
            _playerInvites[invitedPlayerId].RemoveAll(i => i.LobbyId == lobbyId);
            _playerInvites[invitedPlayerId].Add(invite);
            
            // Добавляем в invites лобби
            var invitedPlayer = await _database.GetPlayerByUidAsync(invitedPlayerId);
            if (invitedPlayer != null)
            {
                // Удаляем старое приглашение (RepeatedField не имеет RemoveAll)
                var oldInvite = lobby.Invites.FirstOrDefault(i => i.Player?.Id == invitedPlayerId);
                if (oldInvite != null)
                    lobby.Invites.Remove(oldInvite);
                
                var invitedFriend = new PlayerFriend
                {
                    Player = new Axlebolt.Bolt.Protobuf.Player
                    {
                        Id = invitedPlayer.PlayerUid,
                        Uid = invitedPlayer.PlayerUid,
                        Name = invitedPlayer.Name,
                        AvatarId = invitedPlayer.AvatarId ?? invitedPlayer.Avatar ?? "",
                        TimeInGame = invitedPlayer.TimeInGame,
                        PlayerStatus = new PlayerStatus { OnlineStatus = OnlineStatus.StateOnline }
                    },
                    RelationshipStatus = RelationshipStatus.None
                };
                lobby.Invites.Add(invitedFriend);
                
                // Отправляем событие приглашенному игроку
                var invitedClient = GetClientByPlayerId(invitedPlayerId);
                if (invitedClient != null)
                {
                    Console.WriteLine($"📤 Sending onReceivedInviteToLobby to {invitedPlayer.Name}");
                    await _handler.SendEventAsync(invitedClient, "MatchmakingRemoteEventListener", "onReceivedInviteToLobby",
                        ByteString.CopyFrom(invite.ToByteArray()));
                }
                else
                {
                    Console.WriteLine($"⚠️ Invited player {invitedPlayer.Name} is offline");
                }
                
                // Отправляем событие всем в лобби о новом приглашении
                foreach (var member in lobby.Members)
                {
                    var memberClient = GetClientByPlayerId(member.Player?.Id ?? "");
                    if (memberClient != null)
                    {
                        try
                        {
                            await _handler.SendEventAsync(memberClient, "MatchmakingRemoteEventListener", "onNewPlayerInvitedToLobby",
                                ByteString.CopyFrom(invitedFriend.ToByteArray()));
                        }
                        catch { }
                    }
                }
                
                Console.WriteLine($"✅ Invited {invitedPlayer.Name} to lobby {lobbyId}");
            }
            else
            {
                Console.WriteLine($"❌ Invited player {invitedPlayerId} not found in database");
            }
            
            var result = new BinaryValue { IsNull = true };
            await _handler.WriteProtoResponseAsync(client, request.Id, result, null);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ InviteToLobby: {ex.Message}");
            Console.WriteLine($"Stack: {ex.StackTrace}");
            var result = new BinaryValue { IsNull = true };
            await _handler.WriteProtoResponseAsync(client, request.Id, result, null);
        }
    }
    
    private async Task InviteToLobbyAsAsync(TcpClient client, RpcRequest request)
    {
        try
        {
            Console.WriteLine("🎮 InviteToLobbyAs");
            await InviteToLobbyAsync(client, request);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ InviteToLobbyAs: {ex.Message}");
            var result = new BinaryValue { IsNull = true };
            await _handler.WriteProtoResponseAsync(client, request.Id, result, null);
        }
    }
    
    private async Task KickPlayerFromLobbyAsync(TcpClient client, RpcRequest request)
    {
        try
        {
            Console.WriteLine("🎮 KickPlayerFromLobby");
            
            var session = _sessionManager.GetSessionByClient(client);
            if (session == null)
            {
                session = _sessionManager.GetAllSessions().FirstOrDefault();
            }
            
            var player = await _database.GetPlayerByTokenAsync(session?.Token ?? "");
            var playerId = player?.PlayerUid ?? session?.Token ?? "";
            
            // Получаем ID игрока для кика
            string kickedPlayerId = "";
            if (request.Params.Count > 0 && request.Params[0].One != null)
            {
                var kickedIdStr = Axlebolt.RpcSupport.Protobuf.String.Parser.ParseFrom(request.Params[0].One);
                kickedPlayerId = kickedIdStr.Value;
            }
            
            // Находим лобби текущего игрока
            if (_playerLobbies.TryGetValue(playerId, out var lobbyId) && _lobbies.TryGetValue(lobbyId, out var lobby))
            {
                // Проверяем что текущий игрок - владелец
                if (lobby.OwnerPlayerId == playerId)
                {
                    var memberToKick = lobby.Members.FirstOrDefault(m => m.Player?.Id == kickedPlayerId);
                    if (memberToKick != null)
                    {
                        lobby.Members.Remove(memberToKick);
                        _playerLobbies.Remove(kickedPlayerId);
                        
                        // Отправляем событие кикнутому игроку
                        var kickedClient = GetClientByPlayerId(kickedPlayerId);
                        if (kickedClient != null)
                        {
                            var kickerFriend = lobby.Members.FirstOrDefault(m => m.Player?.Id == playerId);
                            await _handler.SendEventAsync(kickedClient, "MatchmakingRemoteEventListener", "onPlayerKickedFromLobby",
                                ByteString.CopyFrom(kickerFriend?.ToByteArray() ?? Array.Empty<byte>()),
                                ByteString.CopyFrom(memberToKick.ToByteArray()));
                        }
                        
                        // Отправляем событие всем в лобби
                        await BroadcastToLobby(lobbyId, "onPlayerKickedFromLobby",
                            ByteString.CopyFrom(memberToKick.ToByteArray()), kickedPlayerId);
                        
                        Console.WriteLine($"👢 Player {kickedPlayerId} kicked from lobby {lobbyId}");
                    }
                }
            }
            
            var result = new BinaryValue { IsNull = true };
            await _handler.WriteProtoResponseAsync(client, request.Id, result, null);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error in KickPlayerFromLobby: {ex.Message}");
        }
    }
    
    private async Task SetLobbyOwnerAsync(TcpClient client, RpcRequest request)
    {
        try
        {
            Console.WriteLine("=== SetLobbyOwner Request ===");
            
            var session = _sessionManager.GetSessionByClient(client);
            if (session == null)
            {
                session = _sessionManager.GetAllSessions().FirstOrDefault();
            }
            
            var player = await _database.GetPlayerByTokenAsync(session?.Token ?? "");
            var playerId = player?.PlayerUid ?? session?.Token ?? "";
            
            // Получаем ID нового владельца
            string newOwnerId = "";
            if (request.Params.Count > 0 && request.Params[0].One != null)
            {
                var ownerIdStr = Axlebolt.RpcSupport.Protobuf.String.Parser.ParseFrom(request.Params[0].One);
                newOwnerId = ownerIdStr.Value;
            }
            
            // Находим лобби и меняем владельца
            if (_playerLobbies.TryGetValue(playerId, out var lobbyId) && _lobbies.TryGetValue(lobbyId, out var lobby))
            {
                if (lobby.OwnerPlayerId == playerId)
                {
                    lobby.OwnerPlayerId = newOwnerId;
                    Console.WriteLine($"👑 New lobby owner: {newOwnerId}");
                }
            }
            
            var result = new BinaryValue { IsNull = true };
            await _handler.WriteProtoResponseAsync(client, request.Id, result, null);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error in SetLobbyOwner: {ex.Message}");
        }
    }
    
    private async Task SetLobbyDataAsync(TcpClient client, RpcRequest request)
    {
        try
        {
            Console.WriteLine("=== SetLobbyData Request ===");
            
            var session = _sessionManager.GetSessionByClient(client);
            if (session == null)
            {
                session = _sessionManager.GetAllSessions().FirstOrDefault();
            }
            
            var player = await _database.GetPlayerByTokenAsync(session?.Token ?? "");
            var playerId = player?.PlayerUid ?? session?.Token ?? "";
            
            // Парсим данные лобби (Dictionary с Content как MapField<string, string>)
            if (request.Params.Count > 0 && request.Params[0].One != null)
            {
                // Dictionary имеет поле Content типа MapField<string, string>
                // Парсим как raw bytes и обрабатываем вручную
                var dictBytes = request.Params[0].One.ToByteArray();
                
                if (_playerLobbies.TryGetValue(playerId, out var lobbyId) && _lobbies.TryGetValue(lobbyId, out var lobby))
                {
                    // Простой парсинг - пробуем распарсить как key-value пары
                    // Формат protobuf map: tag + length + key + value
                    try
                    {
                        using var stream = new System.IO.MemoryStream(dictBytes);
                        using var reader = new System.IO.BinaryReader(stream);
                        
                        while (stream.Position < stream.Length)
                        {
                            // Читаем tag
                            var tag = reader.ReadByte();
                            if (tag == 0) break;
                            
                            // Пропускаем сложный парсинг, просто логируем
                            Console.WriteLine($"📝 Lobby data received ({dictBytes.Length} bytes)");
                            break;
                        }
                    }
                    catch
                    {
                        Console.WriteLine($"📝 Lobby data set (raw: {dictBytes.Length} bytes)");
                    }
                }
            }
            
            var result = new BinaryValue { IsNull = true };
            await _handler.WriteProtoResponseAsync(client, request.Id, result, null);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error in SetLobbyData: {ex.Message}");
        }
    }
    
    private async Task SetLobbyTypeAsync(TcpClient client, RpcRequest request)
    {
        try
        {
            Console.WriteLine("=== SetLobbyType Request ===");
            
            var session = _sessionManager.GetSessionByClient(client);
            if (session == null)
            {
                session = _sessionManager.GetAllSessions().FirstOrDefault();
            }
            
            var player = await _database.GetPlayerByTokenAsync(session?.Token ?? "");
            var playerId = player?.PlayerUid ?? session?.Token ?? "";
            
            // Получаем новый тип лобби
            LobbyType newType = LobbyType.Public;
            if (request.Params.Count > 0 && request.Params[0].One != null)
            {
                var typeInt = Axlebolt.RpcSupport.Protobuf.Integer.Parser.ParseFrom(request.Params[0].One);
                newType = (LobbyType)typeInt.Value;
            }
            
            if (_playerLobbies.TryGetValue(playerId, out var lobbyId) && _lobbies.TryGetValue(lobbyId, out var lobby))
            {
                lobby.LobbyType = newType;
                Console.WriteLine($"🔒 Lobby type changed to: {newType}");
            }
            
            var result = new BinaryValue { IsNull = true };
            await _handler.WriteProtoResponseAsync(client, request.Id, result, null);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error in SetLobbyType: {ex.Message}");
        }
    }
    
    private async Task SetLobbyNameAsync(TcpClient client, RpcRequest request)
    {
        try
        {
            Console.WriteLine("=== SetLobbyName Request ===");
            
            var session = _sessionManager.GetSessionByClient(client);
            if (session == null)
            {
                session = _sessionManager.GetAllSessions().FirstOrDefault();
            }
            
            var player = await _database.GetPlayerByTokenAsync(session?.Token ?? "");
            var playerId = player?.PlayerUid ?? session?.Token ?? "";
            
            // Получаем новое имя лобби
            string newName = "";
            if (request.Params.Count > 0 && request.Params[0].One != null)
            {
                var nameStr = Axlebolt.RpcSupport.Protobuf.String.Parser.ParseFrom(request.Params[0].One);
                newName = nameStr.Value;
            }
            
            if (_playerLobbies.TryGetValue(playerId, out var lobbyId) && _lobbies.TryGetValue(lobbyId, out var lobby))
            {
                lobby.Name = newName;
                Console.WriteLine($"📝 Lobby name changed to: {newName}");
            }
            var result = new BinaryValue { IsNull = true };
            await _handler.WriteProtoResponseAsync(client, request.Id, result, null);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error in SetLobbyName: {ex.Message}");
        }
    }
    
    private async Task SetLobbyJoinableAsync(TcpClient client, RpcRequest request)
    {
        try
        {
            Console.WriteLine("=== SetLobbyJoinable Request ===");
            
            var session = _sessionManager.GetSessionByClient(client);
            if (session == null)
            {
                session = _sessionManager.GetAllSessions().FirstOrDefault();
            }
            
            var player = await _database.GetPlayerByTokenAsync(session?.Token ?? "");
            var playerId = player?.PlayerUid ?? session?.Token ?? "";
            
            // Получаем значение joinable
            bool joinable = true;
            if (request.Params.Count > 0 && request.Params[0].One != null)
            {
                var joinableBool = Axlebolt.RpcSupport.Protobuf.Boolean.Parser.ParseFrom(request.Params[0].One);
                joinable = joinableBool.Value;
            }
            
            if (_playerLobbies.TryGetValue(playerId, out var lobbyId) && _lobbies.TryGetValue(lobbyId, out var lobby))
            {
                lobby.Joinable = joinable;
                Console.WriteLine($"🚪 Lobby joinable set to: {joinable}");
            }
            
            var result = new BinaryValue { IsNull = true };
            await _handler.WriteProtoResponseAsync(client, request.Id, result, null);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error in SetLobbyJoinable: {ex.Message}");
        }
    }
    
    private async Task SetLobbyMaxMembersAsync(TcpClient client, RpcRequest request)
    {
        try
        {
            Console.WriteLine("=== SetLobbyMaxMembers Request ===");
            
            var session = _sessionManager.GetSessionByClient(client);
            if (session == null)
            {
                session = _sessionManager.GetAllSessions().FirstOrDefault();
            }
            
            var player = await _database.GetPlayerByTokenAsync(session?.Token ?? "");
            var playerId = player?.PlayerUid ?? session?.Token ?? "";
            
            // Получаем максимальное количество участников
            int maxMembers = 10;
            if (request.Params.Count > 0 && request.Params[0].One != null)
            {
                var maxInt = Axlebolt.RpcSupport.Protobuf.Integer.Parser.ParseFrom(request.Params[0].One);
                maxMembers = maxInt.Value;
            }
            
            if (_playerLobbies.TryGetValue(playerId, out var lobbyId) && _lobbies.TryGetValue(lobbyId, out var lobby))
            {
                lobby.MaxMembers = maxMembers;
                Console.WriteLine($"👥 Lobby max members set to: {maxMembers}");
            }
            
            var result = new BinaryValue { IsNull = true };
            await _handler.WriteProtoResponseAsync(client, request.Id, result, null);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error in SetLobbyMaxMembers: {ex.Message}");
        }
    }
    
    private async Task SetLobbyMaxSpectatorsAsync(TcpClient client, RpcRequest request)
    {
        try
        {
            Console.WriteLine("=== SetLobbyMaxSpectators Request ===");
            
            var session = _sessionManager.GetSessionByClient(client);
            if (session == null)
            {
                session = _sessionManager.GetAllSessions().FirstOrDefault();
            }
            
            var player = await _database.GetPlayerByTokenAsync(session?.Token ?? "");
            var playerId = player?.PlayerUid ?? session?.Token ?? "";
            
            // Получаем максимальное количество зрителей
            int maxSpectators = 2;
            if (request.Params.Count > 0 && request.Params[0].One != null)
            {
                var maxInt = Axlebolt.RpcSupport.Protobuf.Integer.Parser.ParseFrom(request.Params[0].One);
                maxSpectators = maxInt.Value;
            }
            
            if (_playerLobbies.TryGetValue(playerId, out var lobbyId) && _lobbies.TryGetValue(lobbyId, out var lobby))
            {
                lobby.MaxSpectators = maxSpectators;
                Console.WriteLine($"👁️ Lobby max spectators set to: {maxSpectators}");
            }
            
            var result = new BinaryValue { IsNull = true };
            await _handler.WriteProtoResponseAsync(client, request.Id, result, null);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error in SetLobbyMaxSpectators: {ex.Message}");
        }
    }
    
    private async Task RequestLobbyListAsync(TcpClient client, RpcRequest request)
    {
        try
        {
            Console.WriteLine("=== RequestLobbyList Request ===");
            
            var result = new BinaryValue { IsNull = false };
            
            // Возвращаем публичные лобби
            foreach (var lobby in _lobbies.Values.Where(l => l.LobbyType == LobbyType.Public && l.Joinable))
            {
                result.Array.Add(ByteString.CopyFrom(lobby.ToByteArray()));
            }
            
            await _handler.WriteProtoResponseAsync(client, request.Id, result, null);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error in RequestLobbyList: {ex.Message}");
        }
    }
    
    private async Task ChangeLobbyPlayerTypeAsync(TcpClient client, RpcRequest request)
    {
        try
        {
            Console.WriteLine("=== ChangeLobbyPlayerType Request ===");
            
            var session = _sessionManager.GetSessionByClient(client);
            if (session == null)
            {
                session = _sessionManager.GetAllSessions().FirstOrDefault();
            }
            
            var player = await _database.GetPlayerByTokenAsync(session?.Token ?? "");
            var playerId = player?.PlayerUid ?? session?.Token ?? "";
            
            // Получаем новый тип игрока (Member, Spectator и т.д.)
            LobbyPlayerType playerType = LobbyPlayerType.Member;
            if (request.Params.Count > 0 && request.Params[0].One != null)
            {
                var typeInt = Axlebolt.RpcSupport.Protobuf.Integer.Parser.ParseFrom(request.Params[0].One);
                playerType = (LobbyPlayerType)typeInt.Value;
            }
            
            if (_playerLobbies.TryGetValue(playerId, out var lobbyId) && _lobbies.TryGetValue(lobbyId, out var lobby))
            {
                // Находим игрока в лобби и меняем его тип
                var member = lobby.Members.FirstOrDefault(m => m.Player?.Id == playerId);
                if (member != null)
                {
                    // Тип игрока хранится в LobbyPlayerType
                    Console.WriteLine($"🔄 Player {playerId} type changed to: {playerType}");
                }
            }
            
            var result = new BinaryValue { IsNull = true };
            await _handler.WriteProtoResponseAsync(client, request.Id, result, null);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error in ChangeLobbyPlayerType: {ex.Message}");
        }
    }
    
    private async Task ChangeLobbyOtherPlayerTypeAsync(TcpClient client, RpcRequest request)
    {
        try
        {
            Console.WriteLine("=== ChangeLobbyOtherPlayerType Request ===");
            
            var session = _sessionManager.GetSessionByClient(client);
            if (session == null)
            {
                session = _sessionManager.GetAllSessions().FirstOrDefault();
            }
            
            var player = await _database.GetPlayerByTokenAsync(session?.Token ?? "");
            var playerId = player?.PlayerUid ?? session?.Token ?? "";
            
            // Получаем ID игрока и новый тип
            string targetPlayerId = "";
            LobbyPlayerType playerType = LobbyPlayerType.Member;
            
            if (request.Params.Count > 0 && request.Params[0].One != null)
            {
                var targetIdStr = Axlebolt.RpcSupport.Protobuf.String.Parser.ParseFrom(request.Params[0].One);
                targetPlayerId = targetIdStr.Value;
            }
            if (request.Params.Count > 1 && request.Params[1].One != null)
            {
                var typeInt = Axlebolt.RpcSupport.Protobuf.Integer.Parser.ParseFrom(request.Params[1].One);
                playerType = (LobbyPlayerType)typeInt.Value;
            }
            
            if (_playerLobbies.TryGetValue(playerId, out var lobbyId) && _lobbies.TryGetValue(lobbyId, out var lobby))
            {
                // Только владелец может менять тип других игроков
                if (lobby.OwnerPlayerId == playerId)
                {
                    var targetMember = lobby.Members.FirstOrDefault(m => m.Player?.Id == targetPlayerId);
                    if (targetMember != null)
                    {
                        Console.WriteLine($"🔄 Player {targetPlayerId} type changed to: {playerType} by owner");
                    }
                }
            }
            
            var result = new BinaryValue { IsNull = true };
            await _handler.WriteProtoResponseAsync(client, request.Id, result, null);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error in ChangeLobbyOtherPlayerType: {ex.Message}");
        }
    }
    
    private async Task SendLobbyChatMsgAsync(TcpClient client, RpcRequest request)
    {
        try
        {
            Console.WriteLine("=== SendLobbyChatMsg Request ===");
            
            var session = _sessionManager.GetSessionByClient(client);
            if (session == null)
            {
                session = _sessionManager.GetAllSessions().FirstOrDefault();
                if (session != null) session.Client = client;
            }
            
            var player = await _database.GetPlayerByTokenAsync(session?.Token ?? "");
            var playerId = player?.PlayerUid ?? session?.Token ?? "";
            var playerName = player?.Name ?? "Player";
            
            // Получаем сообщение
            string message = "";
            if (request.Params.Count > 0 && request.Params[0].One != null)
            {
                var msgStr = Axlebolt.RpcSupport.Protobuf.String.Parser.ParseFrom(request.Params[0].One);
                message = msgStr.Value;
            }
            
            Console.WriteLine($"💬 SendLobbyChatMsg: player={playerName}, message={message}");
            
            if (_playerLobbies.TryGetValue(playerId, out var lobbyId) && _lobbies.TryGetValue(lobbyId, out var lobby))
            {
                Console.WriteLine($"💬 [{lobbyId}] {playerName}: {message}");
                
                // Создаём данные сообщения
                var senderIdProto = new Axlebolt.RpcSupport.Protobuf.String { Value = playerId };
                var senderNameProto = new Axlebolt.RpcSupport.Protobuf.String { Value = playerName };
                var messageProto = new Axlebolt.RpcSupport.Protobuf.String { Value = message };
                
                // Отправляем сообщение всем участникам лобби (включая отправителя)
                foreach (var member in lobby.Members)
                {
                    var memberClient = GetClientByPlayerId(member.Player?.Id ?? "");
                    if (memberClient != null)
                    {
                        try
                        {
                            await _handler.SendEventAsync(memberClient, "MatchmakingRemoteEventListener", "onLobbyChatMsg",
                                ByteString.CopyFrom(senderIdProto.ToByteArray()),
                                ByteString.CopyFrom(senderNameProto.ToByteArray()),
                                ByteString.CopyFrom(messageProto.ToByteArray()));
                            Console.WriteLine($"💬 Sent chat to {member.Player?.Name}");
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"❌ Failed to send chat to {member.Player?.Name}: {ex.Message}");
                        }
                    }
                }
                
                // Также отправляем зрителям
                foreach (var spectator in lobby.Spectators)
                {
                    var spectatorClient = GetClientByPlayerId(spectator.Player?.Id ?? "");
                    if (spectatorClient != null)
                    {
                        try
                        {
                            await _handler.SendEventAsync(spectatorClient, "MatchmakingRemoteEventListener", "onLobbyChatMsg",
                                ByteString.CopyFrom(senderIdProto.ToByteArray()),
                                ByteString.CopyFrom(senderNameProto.ToByteArray()),
                                ByteString.CopyFrom(messageProto.ToByteArray()));
                        }
                        catch { }
                    }
                }
            }
            else
            {
                Console.WriteLine($"💬 Player {playerName} not in any lobby");
            }
            
            var result = new BinaryValue { IsNull = true };
            await _handler.WriteProtoResponseAsync(client, request.Id, result, null);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error in SendLobbyChatMsg: {ex.Message}");
            Console.WriteLine($"Stack: {ex.StackTrace}");
        }
    }
    
    private async Task RefuseInvitationToLobbyAsync(TcpClient client, RpcRequest request)
    {
        try
        {
            Console.WriteLine("=== RefuseInvitationToLobby Request ===");
            
            // Получаем ID лобби
            string lobbyId = "";
            if (request.Params.Count > 0 && request.Params[0].One != null)
            {
                var lobbyIdStr = Axlebolt.RpcSupport.Protobuf.String.Parser.ParseFrom(request.Params[0].One);
                lobbyId = lobbyIdStr.Value;
            }
            
            Console.WriteLine($"❌ Invitation to lobby {lobbyId} refused");
            
            var result = new BinaryValue { IsNull = true };
            await _handler.WriteProtoResponseAsync(client, request.Id, result, null);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error in RefuseInvitationToLobby: {ex.Message}");
        }
    }
    
    private async Task RevokePlayerInvitationToLobbyAsync(TcpClient client, RpcRequest request)
    {
        try
        {
            Console.WriteLine("=== RevokePlayerInvitationToLobby Request ===");
            
            // Получаем ID игрока
            string revokedPlayerId = "";
            if (request.Params.Count > 0 && request.Params[0].One != null)
            {
                var playerIdStr = Axlebolt.RpcSupport.Protobuf.String.Parser.ParseFrom(request.Params[0].One);
                revokedPlayerId = playerIdStr.Value;
            }
            
            Console.WriteLine($"🚫 Invitation for player {revokedPlayerId} revoked");
            
            var result = new BinaryValue { IsNull = true };
            await _handler.WriteProtoResponseAsync(client, request.Id, result, null);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error in RevokePlayerInvitationToLobby: {ex.Message}");
        }
    }
    
    private async Task DeleteLobbyDataAsync(TcpClient client, RpcRequest request)
    {
        try
        {
            Console.WriteLine("=== DeleteLobbyData Request ===");
            
            var session = _sessionManager.GetSessionByClient(client);
            if (session == null)
            {
                session = _sessionManager.GetAllSessions().FirstOrDefault();
            }
            
            var player = await _database.GetPlayerByTokenAsync(session?.Token ?? "");
            var playerId = player?.PlayerUid ?? session?.Token ?? "";
            
            // Получаем ключи для удаления
            var keysToDelete = new List<string>();
            if (request.Params.Count > 0 && request.Params[0].Array != null)
            {
                foreach (var keyBytes in request.Params[0].Array)
                {
                    var keyStr = Axlebolt.RpcSupport.Protobuf.String.Parser.ParseFrom(keyBytes);
                    keysToDelete.Add(keyStr.Value);
                }
            }
            
            if (_playerLobbies.TryGetValue(playerId, out var lobbyId) && _lobbies.TryGetValue(lobbyId, out var lobby))
            {
                foreach (var key in keysToDelete)
                {
                    lobby.Data.Remove(key);
                    Console.WriteLine($"🗑️ Deleted lobby data key: {key}");
                }
            }
            
            var result = new BinaryValue { IsNull = true };
            await _handler.WriteProtoResponseAsync(client, request.Id, result, null);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error in DeleteLobbyData: {ex.Message}");
        }
    }
    
    private async Task SetLobbyGameServerAsync(TcpClient client, RpcRequest request)
    {
        try
        {
            Console.WriteLine("=== SetLobbyGameServer Request ===");
            
            var session = _sessionManager.GetSessionByClient(client);
            if (session == null)
            {
                session = _sessionManager.GetAllSessions().FirstOrDefault();
            }
            
            var player = await _database.GetPlayerByTokenAsync(session?.Token ?? "");
            var playerId = player?.PlayerUid ?? session?.Token ?? "";
            
            // Получаем ID игрового сервера
            string gameServerId = "";
            if (request.Params.Count > 0 && request.Params[0].One != null)
            {
                var serverIdStr = Axlebolt.RpcSupport.Protobuf.String.Parser.ParseFrom(request.Params[0].One);
                gameServerId = serverIdStr.Value;
            }
            
            if (_playerLobbies.TryGetValue(playerId, out var lobbyId) && _lobbies.TryGetValue(lobbyId, out var lobby))
            {
                // Сохраняем ID сервера в данных лобби
                lobby.Data["gameServerId"] = gameServerId;
                Console.WriteLine($"🎮 Lobby game server set to: {gameServerId}");
            }
            
            var result = new BinaryValue { IsNull = true };
            await _handler.WriteProtoResponseAsync(client, request.Id, result, null);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error in SetLobbyGameServer: {ex.Message}");
        }
    }
    
    private async Task GetLobbyGameServerAsync(TcpClient client, RpcRequest request)
    {
        try
        {
            Console.WriteLine("=== GetLobbyGameServer Request ===");
            
            string lobbyId = "";
            if (request.Params.Count > 0 && request.Params[0].One != null)
            {
                var lobbyIdStr = Axlebolt.RpcSupport.Protobuf.String.Parser.ParseFrom(request.Params[0].One);
                lobbyId = lobbyIdStr.Value;
            }
            
            if (!string.IsNullOrEmpty(lobbyId) && _lobbies.TryGetValue(lobbyId, out var lobby))
            {
                if (lobby.Data.TryGetValue("gameServerId", out var gameServerId))
                {
                    var serverDetails = new GameServerDetails
                    {
                        Id = gameServerId,
                        Ip = "127.0.0.1",
                        Port = 7777
                    };
                    
                    var result = new BinaryValue 
                    { 
                        IsNull = false, 
                        One = ByteString.CopyFrom(serverDetails.ToByteArray()) 
                    };
                    await _handler.WriteProtoResponseAsync(client, request.Id, result, null);
                    return;
                }
            }
            
            var emptyResult = new BinaryValue { IsNull = true };
            await _handler.WriteProtoResponseAsync(client, request.Id, emptyResult, null);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error in GetLobbyGameServer: {ex.Message}");
        }
    }
    
    private async Task GetGameServerDetailsAsync(TcpClient client, RpcRequest request)
    {
        try
        {
            Console.WriteLine("=== GetGameServerDetails Request ===");
            
            string gameServerId = "";
            if (request.Params.Count > 0 && request.Params[0].One != null)
            {
                var serverIdStr = Axlebolt.RpcSupport.Protobuf.String.Parser.ParseFrom(request.Params[0].One);
                gameServerId = serverIdStr.Value;
            }
            
            // Возвращаем детали сервера
            var serverDetails = new GameServerDetails
            {
                Id = gameServerId,
                Ip = "127.0.0.1",
                Port = 7777
            };
            
            var result = new BinaryValue 
            { 
                IsNull = false, 
                One = ByteString.CopyFrom(serverDetails.ToByteArray()) 
            };
            await _handler.WriteProtoResponseAsync(client, request.Id, result, null);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error in GetGameServerDetails: {ex.Message}");
        }
    }
    
    private async Task GetGameServerPlayersAsync(TcpClient client, RpcRequest request)
    {
        try
        {
            Console.WriteLine("=== GetGameServerPlayers Request ===");
            
            string gameServerId = "";
            if (request.Params.Count > 0 && request.Params[0].One != null)
            {
                var serverIdStr = Axlebolt.RpcSupport.Protobuf.String.Parser.ParseFrom(request.Params[0].One);
                gameServerId = serverIdStr.Value;
            }
            
            // Возвращаем пустой массив игроков
            var result = new BinaryValue { IsNull = false };
            await _handler.WriteProtoResponseAsync(client, request.Id, result, null);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error in GetGameServerPlayers: {ex.Message}");
        }
    }
    
    private async Task RequestInternetServerListAsync(TcpClient client, RpcRequest request)
    {
        try
        {
            Console.WriteLine("=== RequestInternetServerList Request ===");
            
            // Парсим фильтры: map, freePlayerSlots, maxPlayers, withPassword
            string map = "";
            int? freePlayerSlots = null;
            int? maxPlayers = null;
            bool withPassword = false;
            
            if (request.Params.Count > 0 && request.Params[0].One != null)
            {
                var mapStr = Axlebolt.RpcSupport.Protobuf.String.Parser.ParseFrom(request.Params[0].One);
                map = mapStr.Value;
            }
            
            Console.WriteLine($"🌐 Searching servers: map={map}");
            
            // Возвращаем пустой массив серверов
            var result = new BinaryValue { IsNull = false };
            await _handler.WriteProtoResponseAsync(client, request.Id, result, null);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error in RequestInternetServerList: {ex.Message}");
        }
    }
    
    private async Task SetLobbyPhotonGameAsync(TcpClient client, RpcRequest request)
    {
        try
        {
            Console.WriteLine("=== SetLobbyPhotonGame Request ===");
            
            var session = _sessionManager.GetSessionByClient(client);
            if (session == null)
            {
                session = _sessionManager.GetAllSessions().FirstOrDefault();
            }
            
            var player = await _database.GetPlayerByTokenAsync(session?.Token ?? "");
            var playerId = player?.PlayerUid ?? session?.Token ?? "";
            
            // Парсим PhotonGame
            if (request.Params.Count > 0 && request.Params[0].One != null)
            {
                var photonGame = PhotonGame.Parser.ParseFrom(request.Params[0].One);
                
                if (_playerLobbies.TryGetValue(playerId, out var lobbyId) && _lobbies.TryGetValue(lobbyId, out var lobby))
                {
                    // Сохраняем Photon данные
                    lobby.Data["photonRoomName"] = photonGame.RoomName;
                    Console.WriteLine($"🎮 Lobby Photon game set: {photonGame.RoomName}");
                }
            }
            
            var result = new BinaryValue { IsNull = true };
            await _handler.WriteProtoResponseAsync(client, request.Id, result, null);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error in SetLobbyPhotonGame: {ex.Message}");
        }
    }
    
    private async Task GetLobbyPhotonGameAsync(TcpClient client, RpcRequest request)
    {
        try
        {
            Console.WriteLine("=== GetLobbyPhotonGame Request ===");
            
            string lobbyId = "";
            if (request.Params.Count > 0 && request.Params[0].One != null)
            {
                var lobbyIdStr = Axlebolt.RpcSupport.Protobuf.String.Parser.ParseFrom(request.Params[0].One);
                lobbyId = lobbyIdStr.Value;
            }
            
            if (!string.IsNullOrEmpty(lobbyId) && _lobbies.TryGetValue(lobbyId, out var lobby))
            {
                if (lobby.Data.TryGetValue("photonRoomName", out var roomName))
                {
                    var photonGame = new PhotonGame
                    {
                        RoomName = roomName
                    };
                    
                    var result = new BinaryValue 
                    { 
                        IsNull = false, 
                        One = ByteString.CopyFrom(photonGame.ToByteArray()) 
                    };
                    await _handler.WriteProtoResponseAsync(client, request.Id, result, null);
                    return;
                }
            }
            
            var emptyResult = new BinaryValue { IsNull = true };
            await _handler.WriteProtoResponseAsync(client, request.Id, emptyResult, null);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error in GetLobbyPhotonGame: {ex.Message}");
        }
    }
}
