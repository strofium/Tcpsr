using System.Net.Sockets;
using Google.Protobuf;
using Axlebolt.RpcSupport.Protobuf;
using Axlebolt.Bolt.Protobuf;
using Lobby = Axlebolt.Bolt.Protobuf.Lobby;

namespace GameBot;

public class GameBotClient
{
    private TcpClient? _client;
    private NetworkStream? _stream;
    private readonly string _serverHost;
    private readonly int _serverPort;
    private readonly string _token;
    private bool _running;
    private int _requestId;
    
    public string Token => _token;
    public bool IsConnected => _client?.Connected ?? false;
    public string? CurrentLobbyId { get; private set; }
    
    public GameBotClient(string host, int port, string token)
    {
        _serverHost = host;
        _serverPort = port;
        _token = token;
    }
    
    public bool AutoJoinLobbies { get; set; } = true;
    
    public async Task StartAsync()
    {
        _running = true;
        
        while (_running)
        {
            try
            {
                await ConnectAndRunAsync();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"🤖 [{_token[..8]}] Error: {ex.Message}");
            }
            
            if (_running)
            {
                Console.WriteLine($"🤖 [{_token[..8]}] Reconnecting in 5s...");
                await Task.Delay(5000);
            }
        }
    }
    
    private async Task ConnectAndRunAsync()
    {
        Console.WriteLine($"🤖 [{_token[..8]}] Connecting...");
        
        _client = new TcpClient();
        await _client.ConnectAsync(_serverHost, _serverPort);
        _stream = _client.GetStream();
        
        // Handshake
        var handshakeSuccess = await SendHandshakeAsync();
        if (!handshakeSuccess)
        {
            Console.WriteLine($"🤖 [{_token[..8]}] Handshake failed!");
            return;
        }
        
        Console.WriteLine($"🤖 [{_token[..8]}] ✅ Connected!");
        
        // Heartbeat + Read + Auto-join loops
        var heartbeatTask = HeartbeatLoopAsync();
        var readTask = ReadLoopAsync();
        var autoJoinTask = AutoJoinLoopAsync();
        
        await Task.WhenAny(heartbeatTask, readTask, autoJoinTask);
        
        Console.WriteLine($"🤖 [{_token[..8]}] Disconnected.");
    }
    
    private async Task<bool> SendHandshakeAsync()
    {
        var handshake = new Handshake { Ticket = _token };
        var request = new RpcRequest
        {
            Id = GetNextRequestId(),
            ServiceName = "HandshakeRemoteService",
            MethodName = "protoHandshake"
        };
        request.Params.Add(new BinaryValue 
        { 
            IsNull = false, 
            One = ByteString.CopyFrom(handshake.ToByteArray()) 
        });
        
        await SendRequestAsync(request);
        
        var response = await ReadResponseAsync();
        return response != null && response.RpcResponse?.Exception == null;
    }
    
    public async Task JoinLobbyAsync(string lobbyId)
    {
        if (!IsConnected) return;
        
        var request = new RpcRequest
        {
            Id = GetNextRequestId(),
            ServiceName = "MatchmakingRemoteService",
            MethodName = "joinLobby"
        };
        
        // Param: lobbyId (string)
        var lobbyIdProto = new Axlebolt.RpcSupport.Protobuf.String { Value = lobbyId };
        request.Params.Add(new BinaryValue 
        { 
            IsNull = false, 
            One = ByteString.CopyFrom(lobbyIdProto.ToByteArray()) 
        });
        
        // Param: playerType (int) - 0 = MEMBER
        var playerType = new Axlebolt.RpcSupport.Protobuf.Integer { Value = 0 };
        request.Params.Add(new BinaryValue 
        { 
            IsNull = false, 
            One = ByteString.CopyFrom(playerType.ToByteArray()) 
        });
        
        await SendRequestAsync(request);
        CurrentLobbyId = lobbyId;
        
        Console.WriteLine($"🤖 [{_token[..8]}] Joining lobby {lobbyId}");
    }
    
    public async Task LeaveLobbyAsync()
    {
        if (!IsConnected || string.IsNullOrEmpty(CurrentLobbyId)) return;
        
        var request = new RpcRequest
        {
            Id = GetNextRequestId(),
            ServiceName = "MatchmakingRemoteService",
            MethodName = "leaveLobby"
        };
        
        await SendRequestAsync(request);
        
        Console.WriteLine($"🤖 [{_token[..8]}] Left lobby {CurrentLobbyId}");
        CurrentLobbyId = null;
    }
    
    public async Task CreateLobbyAsync(string name = "Bot Lobby")
    {
        if (!IsConnected) return;
        
        var request = new RpcRequest
        {
            Id = GetNextRequestId(),
            ServiceName = "MatchmakingRemoteService",
            MethodName = "createLobby"
        };
        
        // Param: name
        var nameProto = new Axlebolt.RpcSupport.Protobuf.String { Value = name };
        request.Params.Add(new BinaryValue 
        { 
            IsNull = false, 
            One = ByteString.CopyFrom(nameProto.ToByteArray()) 
        });
        
        // Param: lobbyType (int) - 2 = PUBLIC
        var lobbyType = new Axlebolt.RpcSupport.Protobuf.Integer { Value = 2 };
        request.Params.Add(new BinaryValue 
        { 
            IsNull = false, 
            One = ByteString.CopyFrom(lobbyType.ToByteArray()) 
        });
        
        await SendRequestAsync(request);
        
        Console.WriteLine($"🤖 [{_token[..8]}] Creating lobby '{name}'");
    }
    
    public async Task SetReadyAsync(bool ready = true)
    {
        if (!IsConnected) return;
        
        var request = new RpcRequest
        {
            Id = GetNextRequestId(),
            ServiceName = "MatchmakingRemoteService",
            MethodName = "setReady"
        };
        
        var readyProto = new Axlebolt.RpcSupport.Protobuf.Boolean { Value = ready };
        request.Params.Add(new BinaryValue 
        { 
            IsNull = false, 
            One = ByteString.CopyFrom(readyProto.ToByteArray()) 
        });
        
        await SendRequestAsync(request);
        
        Console.WriteLine($"🤖 [{_token[..8]}] Set ready: {ready}");
    }
    
    private async Task HeartbeatLoopAsync()
    {
        while (_running && IsConnected)
        {
            try
            {
                await SendHeartbeatAsync();
                await Task.Delay(10000);
            }
            catch
            {
                break;
            }
        }
    }
    
    private async Task SendHeartbeatAsync()
    {
        if (_stream == null) return;
        
        var lengthBytes = BitConverter.GetBytes(1);
        if (BitConverter.IsLittleEndian)
            Array.Reverse(lengthBytes);
        
        var heartbeat = lengthBytes.Concat(new byte[] { 0x01 }).ToArray();
        await _stream.WriteAsync(heartbeat);
        await _stream.FlushAsync();
    }
    
    private async Task ReadLoopAsync()
    {
        var buffer = new byte[8192];
        var messageBuffer = new List<byte>();
        
        while (_running && IsConnected && _stream != null)
        {
            try
            {
                var bytesRead = await _stream.ReadAsync(buffer);
                if (bytesRead == 0) break;
                
                for (int i = 0; i < bytesRead; i++)
                    messageBuffer.Add(buffer[i]);
                
                while (messageBuffer.Count >= 4)
                {
                    var lengthBytes = messageBuffer.Take(4).ToArray();
                    if (BitConverter.IsLittleEndian)
                        Array.Reverse(lengthBytes);
                    var messageLength = BitConverter.ToInt32(lengthBytes, 0);
                    
                    // Heartbeat response
                    if (messageLength == 1 && messageBuffer.Count >= 5)
                    {
                        messageBuffer.RemoveRange(0, 5);
                        continue;
                    }
                    
                    var totalSize = 4 + messageLength;
                    if (messageBuffer.Count < totalSize) break;
                    
                    var messageData = messageBuffer.Skip(4).Take(messageLength).ToArray();
                    messageBuffer.RemoveRange(0, totalSize);
                    
                    try
                    {
                        var response = ResponseMessage.Parser.ParseFrom(messageData);
                        HandleResponse(response);
                    }
                    catch { }
                }
            }
            catch
            {
                break;
            }
        }
    }
    
    private void HandleResponse(ResponseMessage response)
    {
        if (response.EventResponse != null)
        {
            var evt = response.EventResponse;
            Console.WriteLine($"🤖 [{_token[..8]}] Event: {evt.EventName}");
            
            // Handle lobby events
            if (evt.EventName == "lobbyUpdated" || evt.EventName == "onLobbyUpdated")
            {
                // Lobby was updated
            }
            else if (evt.EventName == "gameStarted" || evt.EventName == "onGameStarted")
            {
                Console.WriteLine($"🤖 [{_token[..8]}] 🎮 Game started!");
            }
        }
        else if (response.RpcResponse != null)
        {
            var rpc = response.RpcResponse;
            
            // Обработка списка лобби
            if (rpc.Return != null && !rpc.Return.IsNull && rpc.Return.Array.Count > 0)
            {
                _ = Task.Run(async () =>
                {
                    try
                    {
                        // Парсим лобби и ищем подходящее
                        foreach (var lobbyData in rpc.Return.Array)
                        {
                            try
                            {
                                var lobby = Lobby.Parser.ParseFrom(lobbyData);
                                
                                // Проверяем: лобби открыто, есть игроки, но не заполнено
                                if (lobby.Joinable && 
                                    lobby.Members.Count > 0 && 
                                    lobby.Members.Count < lobby.MaxMembers &&
                                    string.IsNullOrEmpty(CurrentLobbyId))
                                {
                                    Console.WriteLine($"🤖 [{_token[..8]}] Found lobby: {lobby.Name} ({lobby.Members.Count}/{lobby.MaxMembers})");
                                    await JoinLobbyAsync(lobby.Id);
                                    break;
                                }
                            }
                            catch { }
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"🤖 [{_token[..8]}] Error processing lobby list: {ex.Message}");
                    }
                });
            }
        }
    }
    
    private async Task<ResponseMessage?> ReadResponseAsync()
    {
        if (_stream == null) return null;
        
        var buffer = new byte[8192];
        var bytesRead = await _stream.ReadAsync(buffer);
        
        if (bytesRead < 4) return null;
        
        var lengthBytes = buffer.Take(4).ToArray();
        if (BitConverter.IsLittleEndian)
            Array.Reverse(lengthBytes);
        var messageLength = BitConverter.ToInt32(lengthBytes, 0);
        
        if (bytesRead < 4 + messageLength) return null;
        
        var messageData = buffer.Skip(4).Take(messageLength).ToArray();
        return ResponseMessage.Parser.ParseFrom(messageData);
    }
    
    private async Task SendRequestAsync(RpcRequest request)
    {
        if (_stream == null) return;
        
        var messageBytes = request.ToByteArray();
        var lengthBytes = BitConverter.GetBytes(messageBytes.Length);
        if (BitConverter.IsLittleEndian)
            Array.Reverse(lengthBytes);
        
        var fullMessage = lengthBytes.Concat(messageBytes).ToArray();
        await _stream.WriteAsync(fullMessage);
        await _stream.FlushAsync();
    }
    
    private string GetNextRequestId()
    {
        return $"bot_{_token[..8]}_{++_requestId}";
    }
    
    private async Task AutoJoinLoopAsync()
    {
        while (_running && IsConnected)
        {
            try
            {
                // Если бот не в лобби и автоприсоединение включено
                if (AutoJoinLobbies && string.IsNullOrEmpty(CurrentLobbyId))
                {
                    await RequestLobbyListAndJoinAsync();
                }
                
                await Task.Delay(5000); // Проверяем каждые 5 секунд
            }
            catch (Exception ex)
            {
                Console.WriteLine($"🤖 [{_token[..8]}] AutoJoin error: {ex.Message}");
            }
        }
    }
    
    public async Task RequestLobbyListAndJoinAsync()
    {
        if (!IsConnected) return;
        
        var request = new RpcRequest
        {
            Id = GetNextRequestId(),
            ServiceName = "MatchmakingRemoteService",
            MethodName = "requestLobbyList"
        };
        
        await SendRequestAsync(request);
        
        // Ждем ответ со списком лобби
        await Task.Delay(1000);
    }
    
    public void Stop()
    {
        _running = false;
        _client?.Close();
    }
}
