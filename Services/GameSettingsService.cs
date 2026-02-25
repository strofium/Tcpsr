using System.Net.Sockets;
using Axlebolt.RpcSupport.Protobuf;
using Axlebolt.Bolt.Protobuf;
using StandRiseServer.Core;
using Google.Protobuf;

namespace StandRiseServer.Services;

public class GameSettingsService
{
    private readonly ProtobufHandler _handler;
    private readonly DatabaseService _database;
    private readonly SessionManager _sessionManager;

    public GameSettingsService(ProtobufHandler handler, DatabaseService database, SessionManager sessionManager)
    {
        _handler = handler;
        _database = database;
        _sessionManager = sessionManager;
        
        _handler.RegisterHandler("GameSettingsRemoteService", "getGameSettingsEncrypted", GetGameSettingsAsync);
        _handler.RegisterHandler("GameSettingsRemoteService", "getGameSettings", GetGameSettingsAsync);
    }

    private async Task GetGameSettingsAsync(TcpClient client, RpcRequest request)
    {
        try
        {
            var session = _sessionManager.GetSessionByClient(client);
            if (session == null)
            {
                await SendUnauthorizedAsync(client, request.Id);
                return;
            }

            var settings = await _database.GetGameSettingsAsync();
            var result = new BinaryValue { IsNull = false };
            
            foreach (var setting in settings)
            {
                var gameSetting = new GameSetting
                {
                    Key = setting.Key,
                    Value = setting.Value,
                    Type = SettingType.String
                };
                
                // Parse as appropriate type
                if (int.TryParse(setting.Value, out int intValue))
                {
                    gameSetting.IntValue = intValue;
                    gameSetting.Type = SettingType.Integer;
                }
                else if (float.TryParse(setting.Value, System.Globalization.NumberStyles.Any, 
                    System.Globalization.CultureInfo.InvariantCulture, out float floatValue))
                {
                    gameSetting.FloatValue = floatValue;
                    gameSetting.Type = SettingType.Float;
                }
                else if (bool.TryParse(setting.Value, out bool boolValue))
                {
                    gameSetting.BoolValue = boolValue;
                    gameSetting.Type = SettingType.Bool;
                }
                
                result.Array.Add(ByteString.CopyFrom(gameSetting.ToByteArray()));
            }

            await _handler.WriteProtoResponseAsync(client, request.Id, result, null);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"GameSettings error: {ex.Message}");
        }
    }

    private async Task SendUnauthorizedAsync(TcpClient client, string guid)
    {
        await _handler.WriteProtoResponseAsync(client, guid, null,
            new RpcException { Id = guid, Code = 401, Property = null });
    }
}
