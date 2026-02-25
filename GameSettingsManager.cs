using StandRiseServer.Core;

namespace StandRiseServer;

public static class GameSettingsManager
{
    public static async Task ListGameSettings(DatabaseService database)
    {
        Console.WriteLine("\n=== Current Game Settings ===");
        
        try
        {
            var settings = await database.GetGameSettingsAsync();
            if (settings.Count == 0)
            {
                Console.WriteLine("No game settings found in database");
                return;
            }

            Console.WriteLine($"{"Key",-20} {"Value",-30}");
            Console.WriteLine(new string('-', 55));
            
            foreach (var setting in settings)
            {
                Console.WriteLine($"{setting.Key,-20} {setting.Value,-30}");
            }
            
            Console.WriteLine($"\nTotal settings: {settings.Count}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Error listing settings: {ex.Message}");
        }
    }

    public static async Task UpdateGameSetting(DatabaseService database)
    {
        Console.WriteLine("\n=== Update Game Setting ===");
        
        Console.Write("Enter setting key: ");
        var key = Console.ReadLine()?.Trim();
        if (string.IsNullOrEmpty(key))
        {
            Console.WriteLine("Invalid key");
            return;
        }

        Console.Write("Enter new value: ");
        var value = Console.ReadLine()?.Trim();
        if (string.IsNullOrEmpty(value))
        {
            Console.WriteLine("Invalid value");
            return;
        }

        try
        {
            await database.UpsertGameSettingAsync(key, value);
            Console.WriteLine($"✅ Successfully updated setting: {key} = {value}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Error updating setting: {ex.Message}");
        }
    }

    public static async Task SetGameVersion(DatabaseService database)
    {
        Console.WriteLine("\n=== Set Game Version ===");
        
        Console.Write("Enter new game version: ");
        var version = Console.ReadLine()?.Trim();
        if (string.IsNullOrEmpty(version))
        {
            Console.WriteLine("Invalid version");
            return;
        }

        try
        {
            // Update in GameSettings
            await database.UpsertGameSettingAsync("game_version", version);
            
            // Check if this version exists in Hashes
            var hashCount = await database.CountHashByVersionAsync(version);
            if (hashCount == 0)
            {
                Console.WriteLine($"⚠️  Warning: Version {version} not found in Hashes collection");
                Console.WriteLine("You may need to add this version using version-manager");
            }
            
            Console.WriteLine($"✅ Game version updated to: {version}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Error setting version: {ex.Message}");
        }
    }

    public static void ShowHelp()
    {
        Console.WriteLine("\n=== Game Settings Manager Commands ===");
        Console.WriteLine("list         - List all game settings");
        Console.WriteLine("update       - Update a game setting");
        Console.WriteLine("set-version  - Set game version");
        Console.WriteLine("help         - Show this help");
        Console.WriteLine("exit         - Exit settings manager");
    }

    public static async Task RunInteractive(DatabaseService database)
    {
        Console.WriteLine("\n⚙️  Game Settings Manager");
        Console.WriteLine("Type 'help' for available commands");

        while (true)
        {
            Console.Write("\nsettings-manager> ");
            var command = Console.ReadLine()?.Trim().ToLower();

            switch (command)
            {
                case "list":
                    await ListGameSettings(database);
                    break;
                case "update":
                    await UpdateGameSetting(database);
                    break;
                case "set-version":
                    await SetGameVersion(database);
                    break;
                case "help":
                    ShowHelp();
                    break;
                case "exit":
                    return;
                case "":
                    continue;
                default:
                    Console.WriteLine($"Unknown command: {command}. Type 'help' for available commands.");
                    break;
            }
        }
    }
}