using System.Net.Sockets;
using Axlebolt.RpcSupport.Protobuf;
using Axlebolt.Bolt.Protobuf;
using StandRiseServer.Core;
using Google.Protobuf;

namespace StandRiseServer.Services;

/// <summary>
/// Сервис для управления игровыми настройками игрока
/// Обрабатывает получение, обновление и удаление персональных настроек игрока
/// </summary>
public class GameSettingsPlayerRemoteService
{
    private readonly ProtobufHandler _handler;
    private readonly DatabaseService _database;
    private readonly SessionManager _sessionManager;

    public GameSettingsPlayerRemoteService(ProtobufHandler handler, DatabaseService database, SessionManager sessionManager)
    {
        _handler = handler;
        _database = database;
        _sessionManager = sessionManager;
        
        _handler.RegisterHandler("GameSettingsPlayerRemoteService", "getPlayerGameSettings", GetPlayerGameSettingsAsync);
        _handler.RegisterHandler("GameSettingsPlayerRemoteService", "updatePlayerGameSettings", UpdatePlayerGameSettingsAsync);
        _handler.RegisterHandler("GameSettingsPlayerRemoteService", "deletePlayerGameSettings", DeletePlayerGameSettingsAsync);
        _handler.RegisterHandler("GameSettingsPlayerRemoteService", "resetPlayerGameSettings", ResetPlayerGameSettingsAsync);
    }

    /// <summary>
    /// Получает все игровые настройки игрока
    /// </summary>
    private async Task GetPlayerGameSettingsAsync(TcpClient client, RpcRequest request)
    {
        try
        {
            Console.WriteLine("=== GetPlayerGameSettings Request ===");
            var session = _sessionManager.GetSessionByClient(client);
            if (session == null)
            {
                Console.WriteLine("❌ No session found");
                await SendUnauthorizedAsync(client, request.Id);
                return;
            }

            var player = await _database.GetPlayerByTokenAsync(session.Token);
            if (player == null)
            {
                Console.WriteLine("❌ Player not found");
                await SendUnauthorizedAsync(client, request.Id);
                return;
            }

            var result = new BinaryValue { IsNull = false };
            
            // Сериализуем настройки игрока
            var settings = player.Settings;
            var gameSettingProto = new GameSetting
            {
                Key = "playerSettings",
                Type = SettingType.String,
                Value = SerializePlayerSettings(settings)
            };
            
            result.Array.Add(ByteString.CopyFrom(gameSettingProto.ToByteArray()));

            Console.WriteLine($"✅ GetPlayerGameSettings successful for: {player.Name}");
            await _handler.WriteProtoResponseAsync(client, request.Id, result, null);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Error in GetPlayerGameSettings: {ex.Message}");
            await SendUnauthorizedAsync(client, request.Id);
        }
    }

    /// <summary>
    /// Обновляет игровые настройки игрока
    /// </summary>
    private async Task UpdatePlayerGameSettingsAsync(TcpClient client, RpcRequest request)
    {
        try
        {
            Console.WriteLine("=== UpdatePlayerGameSettings Request ===");
            var session = _sessionManager.GetSessionByClient(client);
            if (session == null || request.Params.Count == 0)
            {
                await SendUnauthorizedAsync(client, request.Id);
                return;
            }

            var player = await _database.GetPlayerByTokenAsync(session.Token);
            if (player == null)
            {
                await SendUnauthorizedAsync(client, request.Id);
                return;
            }

            // Парсим новые настройки из параметров
            var settingsData = request.Params[0];
            if (settingsData.One != null && settingsData.One.Length > 0)
            {
                var gameSetting = GameSetting.Parser.ParseFrom(settingsData.One);
                UpdatePlayerSettingsFromProto(player.Settings, gameSetting);
                
                await _database.UpdatePlayerAsync(player);
                Console.WriteLine($"✅ UpdatePlayerGameSettings successful for: {player.Name}");
            }

            var result = new BinaryValue { IsNull = true };
            await _handler.WriteProtoResponseAsync(client, request.Id, result, null);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Error in UpdatePlayerGameSettings: {ex.Message}");
            await SendUnauthorizedAsync(client, request.Id);
        }
    }

    /// <summary>
    /// Удаляет конкретную настройку игрока
    /// </summary>
    private async Task DeletePlayerGameSettingsAsync(TcpClient client, RpcRequest request)
    {
        try
        {
            Console.WriteLine("=== DeletePlayerGameSettings Request ===");
            var session = _sessionManager.GetSessionByClient(client);
            if (session == null || request.Params.Count == 0)
            {
                await SendUnauthorizedAsync(client, request.Id);
                return;
            }

            var player = await _database.GetPlayerByTokenAsync(session.Token);
            if (player == null)
            {
                await SendUnauthorizedAsync(client, request.Id);
                return;
            }

            // Получаем ID настройки для удаления
            var settingId = request.Params[0].One != null ? 
                System.Text.Encoding.UTF8.GetString(request.Params[0].One.ToByteArray()) : "";
            ResetPlayerSettingByKey(player.Settings, settingId);
            
            await _database.UpdatePlayerAsync(player);
            Console.WriteLine($"✅ DeletePlayerGameSettings successful for: {player.Name}");

            var result = new BinaryValue { IsNull = true };
            await _handler.WriteProtoResponseAsync(client, request.Id, result, null);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Error in DeletePlayerGameSettings: {ex.Message}");
            await SendUnauthorizedAsync(client, request.Id);
        }
    }

    /// <summary>
    /// Сбрасывает все настройки игрока на значения по умолчанию
    /// </summary>
    private async Task ResetPlayerGameSettingsAsync(TcpClient client, RpcRequest request)
    {
        try
        {
            Console.WriteLine("=== ResetPlayerGameSettings Request ===");
            var session = _sessionManager.GetSessionByClient(client);
            if (session == null)
            {
                await SendUnauthorizedAsync(client, request.Id);
                return;
            }

            var player = await _database.GetPlayerByTokenAsync(session.Token);
            if (player == null)
            {
                await SendUnauthorizedAsync(client, request.Id);
                return;
            }

            // Сбрасываем все настройки на значения по умолчанию
            player.Settings = new Models.PlayerSettings();
            
            await _database.UpdatePlayerAsync(player);
            Console.WriteLine($"✅ ResetPlayerGameSettings successful for: {player.Name}");

            var result = new BinaryValue { IsNull = true };
            await _handler.WriteProtoResponseAsync(client, request.Id, result, null);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Error in ResetPlayerGameSettings: {ex.Message}");
            await SendUnauthorizedAsync(client, request.Id);
        }
    }

    private string SerializePlayerSettings(Models.PlayerSettings settings)
    {
        return System.Text.Json.JsonSerializer.Serialize(settings);
    }

    private void UpdatePlayerSettingsFromProto(Models.PlayerSettings settings, GameSetting proto)
    {
        // Парсим JSON из proto и обновляем настройки
        try
        {
            var newSettings = System.Text.Json.JsonSerializer.Deserialize<Models.PlayerSettings>(proto.Value);
            if (newSettings != null)
            {
                settings.MouseSensitivity = newSettings.MouseSensitivity;
                settings.Crosshair = newSettings.Crosshair;
                settings.PreferredRegion = newSettings.PreferredRegion;
                settings.MasterVolume = newSettings.MasterVolume;
                settings.MusicVolume = newSettings.MusicVolume;
                settings.EffectsVolume = newSettings.EffectsVolume;
                settings.GraphicsQuality = newSettings.GraphicsQuality;
                settings.Resolution = newSettings.Resolution;
                settings.Fullscreen = newSettings.Fullscreen;
                settings.ShowOnlineStatus = newSettings.ShowOnlineStatus;
                settings.AllowFriendRequests = newSettings.AllowFriendRequests;
                settings.ShowMatchHistory = newSettings.ShowMatchHistory;
                settings.EnableNotifications = newSettings.EnableNotifications;
                settings.EnableSoundNotifications = newSettings.EnableSoundNotifications;
                settings.Language = newSettings.Language;
                settings.TimeZone = newSettings.TimeZone;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"⚠️ Error parsing player settings: {ex.Message}");
        }
    }

    private void ResetPlayerSettingByKey(Models.PlayerSettings settings, string key)
    {
        switch (key.ToLower())
        {
            case "mousesensitivity":
                settings.MouseSensitivity = 1.0f;
                break;
            case "crosshair":
                settings.Crosshair = 0;
                break;
            case "preferredregion":
                settings.PreferredRegion = "auto";
                break;
            case "mastervolume":
                settings.MasterVolume = 1.0f;
                break;
            case "musicvolume":
                settings.MusicVolume = 0.5f;
                break;
            case "effectsvolume":
                settings.EffectsVolume = 1.0f;
                break;
            case "graphicsquality":
                settings.GraphicsQuality = 2;
                break;
            case "resolution":
                settings.Resolution = 0;
                break;
            case "fullscreen":
                settings.Fullscreen = true;
                break;
            case "language":
                settings.Language = "en";
                break;
            case "timezone":
                settings.TimeZone = "UTC";
                break;
        }
    }

    private async Task SendUnauthorizedAsync(TcpClient client, string guid)
    {
        await _handler.WriteProtoResponseAsync(client, guid, null,
            new RpcException { Id = guid, Code = 401, Property = null });
    }
}
