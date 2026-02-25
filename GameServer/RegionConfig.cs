namespace StandRiseServer.GameServer;

/// <summary>
/// Configuration for game server regions
/// </summary>
public static class RegionConfig
{
    /// <summary>
    /// Get the default regions JSON for game settings
    /// </summary>
    public static string GetDefaultRegionsJson()
    {
        var regions = new
        {
            Servers = new[]
            {
                new PhotonServerConfig
                {
                    Location = "test",
                    DisplayName = "Test Server",
                    Dns = "127.0.0.1",
                    Ip = "127.0.0.1",
                    Online = true,
                    Enabled = true
                },
                new PhotonServerConfig
                {
                    Location = "msk",
                    DisplayName = "Russia",
                    Dns = "95.179.143.136",
                    Ip = "95.179.143.136",
                    Online = true,
                    Enabled = true
                },
                new PhotonServerConfig
                {
                    Location = "fra",
                    DisplayName = "Europe",
                    Dns = "95.179.143.136",
                    Ip = "95.179.143.136",
                    Online = true,
                    Enabled = true
                }
            }
        };

        return System.Text.Json.JsonSerializer.Serialize(regions);
    }

    /// <summary>
    /// Create a single region JSON
    /// </summary>
    public static string CreateRegionJson(string location, string displayName, string ip)
    {
        var regions = new
        {
            Servers = new[]
            {
                new PhotonServerConfig
                {
                    Location = location,
                    DisplayName = displayName,
                    Dns = ip,
                    Ip = ip,
                    Online = true,
                    Enabled = true
                }
            }
        };

        return System.Text.Json.JsonSerializer.Serialize(regions);
    }
}

public class PhotonServerConfig
{
    public string Location { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public string Dns { get; set; } = "";
    public string Ip { get; set; } = "";
    public bool Online { get; set; }
    public bool Enabled { get; set; }
}
