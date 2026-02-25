using System.Net.Sockets;
using Google.Apis.Auth.OAuth2;
using Google.Apis.Auth.OAuth2.Flows;
using Google.Apis.Oauth2.v2;
using Google.Apis.Services;
using Google.Protobuf;
using Axlebolt.RpcSupport.Protobuf;
using Axlebolt.Bolt.Protobuf;
using StandRiseServer.Core;
using StandRiseServer.Models;
using StandRiseServer.Utils;
using MongoDB.Bson;

namespace StandRiseServer.Services;

public class GoogleAuthService
{
    private readonly ProtobufHandler _handler;
    private readonly DatabaseService _database;
    private readonly SessionManager _sessionManager;
    
    private const string GoogleClientId = "336961445695-0v7m8659clq3p3a1ia86ofitbe419sqf.apps.googleusercontent.com";
    private const string GoogleClientSecret = "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l";

    public GoogleAuthService(ProtobufHandler handler, DatabaseService database, SessionManager sessionManager)
    {
        _handler = handler;
        _database = database;
        _sessionManager = sessionManager;
        
        _handler.RegisterHandler("GoogleAuthRemoteService", "protoAuth", HandleProtoAuthAsync);
        _handler.RegisterHandler("GoogleAuthRemoteService", "protoAuthSecured", HandleProtoAuthSecuredAsync);
        _handler.RegisterHandler("GoogleAuthRemoteService", "auth", HandleAuthAsync);
    }

    private async Task HandleAuthAsync(TcpClient client, RpcRequest request)
    {
        try 
        {
            // [Rpc("auth")] string gameId, string gameVersion, Platform platform, string authCode
            if (request.Params.Count < 4) return;
            
            var gameId = request.Params[0].One.ToStringUtf8();
            var gameVersion = request.Params[1].One.ToStringUtf8();
            var authCode = request.Params[3].One.ToStringUtf8();

            Console.WriteLine($"🔍 GoogleAuth (auth): V={gameVersion}, Code={authCode[..5]}...");
            await ProcessAuthAsync(client, request.Id, authCode, gameVersion, null);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ GoogleAuth (auth) error: {ex.Message}");
        }
    }

    private async Task HandleProtoAuthSecuredAsync(TcpClient client, RpcRequest request)
    {
        try
        {
            if (request.Params.Count < 2) return;

            // Params[0] is AuthGoogle (likely V1)
            var flexible = ParseFlexibleAuthGoogle(request.Params[0].One);
            // Params[1] is AppVerification (Client structure)
            var verification = ParseAppVerification(request.Params[1].One);

            Console.WriteLine($"🔍 GoogleAuth (secured): V={flexible.GameVersion}, Code={flexible.AuthCode[..5]}...");
            await ProcessAuthAsync(client, request.Id, flexible.AuthCode, flexible.GameVersion, verification);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ GoogleAuth (secured) error: {ex.Message}");
        }
    }

    private async Task HandleProtoAuthAsync(TcpClient client, RpcRequest request)
    {
        try
        {
            if (request.Params.Count == 0) return;

            var flexible = ParseFlexibleAuthGoogle(request.Params[0].One);
            
            Console.WriteLine($"🔍 GoogleAuth (proto): V={flexible.GameVersion}, Code={flexible.AuthCode[..Math.Min(5, flexible.AuthCode.Length)]}...");
            await ProcessAuthAsync(client, request.Id, flexible.AuthCode, flexible.GameVersion, flexible.Verification);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ GoogleAuth (proto) error: {ex.Message}");
        }
    }

    private async Task ProcessAuthAsync(TcpClient client, string guid, string authCode, string gameVersion, Verification? verification)
    {
        if (string.IsNullOrEmpty(authCode))
        {
            Console.WriteLine("❌ GoogleAuth: AuthCode is empty.");
            await SendError(client, guid, 2001);
            return;
        }

        var googleUserId = await GetGoogleUserIdAsync(authCode);
        if (string.IsNullOrEmpty(googleUserId))
        {
            Console.WriteLine("❌ GoogleAuth: Failed to exchange AuthCode.");
            await SendError(client, guid, 2001);
            return;
        }

        string deviceId = verification?.DeviceId ?? "android_device";
        if (await _database.CountHwidAsync(deviceId) == 0)
        {
            await _database.InsertHwidAsync(deviceId);
        }

        var player = await _database.GetPlayerByAuthTokenAsync(googleUserId);
        if (player == null)
        {
            await CreateNewPlayerAsync(client, guid, googleUserId, verification, gameVersion);
        }
        else
        {
            await UpdateExistingPlayerAsync(client, guid, googleUserId, verification, gameVersion);
        }
    }

    private async Task SendError(TcpClient client, string guid, int code)
    {
        await _handler.WriteProtoResponseAsync(client, guid, null, 
            new RpcException { Id = guid, Code = code, Property = null });
    }

    private struct FlexibleRequest { public string AuthCode; public string GameVersion; public Verification Verification; }

    private FlexibleRequest ParseFlexibleAuthGoogle(Google.Protobuf.ByteString data)
    {
        var result = new FlexibleRequest { AuthCode = "", GameVersion = "" };
        var input = data.CreateCodedInput();
        uint tag;
        bool isV1 = false;

        // Peak to see if it's V1 (tag 4 exists) or V2 (tag 1 is authCode, likely starts with small string)
        while ((tag = input.ReadTag()) != 0)
        {
            int field = Google.Protobuf.WireFormat.GetTagFieldNumber(tag);
            if (field == 4) isV1 = true;
            
            switch (field)
            {
                case 1:
                    var s1 = input.ReadString();
                    if (!isV1 && s1.Length > 20) result.AuthCode = s1; // Likely V2 AuthCode
                    break;
                case 2:
                    if (isV1) result.GameVersion = input.ReadString();
                    else 
                    {
                        try { result.Verification = Verification.Parser.ParseFrom(input.ReadBytes()); }
                        catch { input.SkipLastField(); }
                    }
                    break;
                case 3:
                    if (!isV1) result.GameVersion = input.ReadString();
                    else input.SkipLastField();
                    break;
                case 4:
                    result.AuthCode = input.ReadString();
                    isV1 = true;
                    break;
                default:
                    input.SkipLastField();
                    break;
            }
        }
        return result;
    }

    private Verification ParseAppVerification(Google.Protobuf.ByteString data)
    {
        var v = new Verification();
        var input = data.CreateCodedInput();
        uint tag;
        while ((tag = input.ReadTag()) != 0)
        {
            switch (Google.Protobuf.WireFormat.GetTagFieldNumber(tag))
            {
                case 1: v.IsRooted = input.ReadBool(); break;
                case 2: v.ApkHash = input.ReadString(); break;
                case 3: v.AllApps.Add(input.ReadString()); break;
                default: input.SkipLastField(); break;
            }
        }
        return v;
    }

    private async Task CreateNewPlayerAsync(TcpClient client, string guid, string googleUserId, 
        Verification? verification, string gameVersion)
    {
        Console.WriteLine($"🆕 GoogleAuth: Registering {googleUserId[..5]}...");
        
        await ValidateGameVersionAsync(guid, verification, gameVersion, client);

        var nextUid = await _database.GetNextPlayerUidAsync();
        var timestamp = Converters.GetCurrentTimestamp();
        var newToken = Converters.CalculateMD5(googleUserId);

        var newPlayer = new Models.Player
        {
            PlayerUid = nextUid.ToString(),
            OriginalUid = nextUid,
            Id = MongoDB.Bson.ObjectId.GenerateNewId(),
            Name = $"TK_{Converters.GetRandomInt(9999)}",
            RegistrationDate = DateTimeOffset.FromUnixTimeSeconds(timestamp).DateTime,
            AuthToken = googleUserId,
            Token = newToken,
            LastHwid = verification?.DeviceId ?? "android",
            NoDetectRoot = true,
            Stats = CreateDefaultStats(),
            FileStorage = CreateDefaultFileStorage()
        };

        await _database.InsertPlayerAsync(newPlayer);
        await SendSuccess(client, guid, newToken);
    }

    private async Task UpdateExistingPlayerAsync(TcpClient client, string guid, string googleUserId,
        Verification? verification, string gameVersion)
    {
        var player = await _database.GetPlayerByAuthTokenAsync(googleUserId);
        if (player == null) return;

        Console.WriteLine($"✅ GoogleAuth: Welcome back, {player.Name}");

        var timestamp = Converters.GetCurrentTimestamp();
        var newToken = Converters.CalculateMD5(googleUserId + timestamp);

        player.Token = newToken;
        if (verification != null) player.LastHwid = verification.DeviceId;

        await _database.UpdatePlayerAsync(player);
        await ValidateGameVersionAsync(guid, verification, gameVersion, client);
        await SendSuccess(client, guid, newToken);
    }

    private async Task SendSuccess(TcpClient client, string guid, string token)
    {
        var tokenString = new Axlebolt.RpcSupport.Protobuf.String { Value = token };
        var result = new BinaryValue { IsNull = false, One = tokenString.ToByteString() };
        await _handler.WriteProtoResponseAsync(client, guid, result, null);
    }

    private async Task<bool> ValidateGameVersionAsync(string guid, Verification? verification, 
        string gameVersion, TcpClient client)
    {
        if (string.IsNullOrEmpty(gameVersion)) return true;
        var hashCount = await _database.CountHashByVersionAsync(gameVersion);
        if (hashCount == 0) Console.WriteLine($"⚠️ GoogleAuth: Client version {gameVersion} unknown.");
        return true;
    }

    private bool PerformFinalValidationAsync(TcpClient client, string guid, 
        Models.Player player, Verification? verification)
    {
        if (player.IsBanned) return false;
        return true;
    }

    private async Task<string?> GetGoogleUserIdAsync(string authCode)
    {
        try
        {
            Console.WriteLine($"🔑 GoogleAuth: Exchange code (ID: {GoogleClientId[..10]}...)");
            var flow = new GoogleAuthorizationCodeFlow(new GoogleAuthorizationCodeFlow.Initializer
            {
                ClientSecrets = new ClientSecrets { ClientId = GoogleClientId, ClientSecret = GoogleClientSecret }
            });

            var tokenResponse = await flow.ExchangeCodeForTokenAsync("me", authCode, "postmessage", CancellationToken.None);
            var credential = new UserCredential(flow, "me", tokenResponse);
            var oauthService = new Oauth2Service(new BaseClientService.Initializer { HttpClientInitializer = credential });
            var userInfo = await oauthService.Userinfo.Get().ExecuteAsync();
            return userInfo.Id;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Google Auth API error: {ex.Message}");
            return null;
        }
    }

    private PlayerStats CreateDefaultStats()
    {
        var stats = new PlayerStats { ArrayCust = new List<StatItem>() };
        var statNames = new[] { "level_xp", "level_id", "ranked_rank", "ranked_current_mmr" };
        foreach (var statName in statNames)
        {
            var value = statName == "level_id" ? 1 : (statName == "ranked_current_mmr" ? 500 : 0);
            stats.ArrayCust.Add(new StatItem { Name = statName, IntValue = value, Type = "INT" });
        }
        return stats;
    }

    private List<FileStorageItem> CreateDefaultFileStorage()
    {
        var defaultJson = "{\"LastRoomId\":\"\",\"LastRegion\":\"\"}";
        var bytes = System.Text.Encoding.UTF8.GetBytes(defaultJson).Select(b => (int)b).ToList();
        return new List<FileStorageItem> { new FileStorageItem { Filename = "ranked_last_room_id", File = bytes } };
    }
}
