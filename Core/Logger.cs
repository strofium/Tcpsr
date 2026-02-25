namespace StandRiseServer.Core;

/// <summary>
/// Профессиональная система логирования
/// </summary>
public static class Logger
{
    private static readonly object _lock = new();
    
    public static void Startup(string message)
    {
        Log("STARTUP", message, ConsoleColor.Cyan);
    }
    
    public static void Service(string serviceName, string version, string platform, string action, string playerUid)
    {
        var msg = $"{serviceName} | v{version} | {platform} | {action} | uid:{playerUid}";
        Log("SERVICE", msg, ConsoleColor.Green);
    }
    
    public static void Service(string serviceName, string action, string playerUid)
    {
        var msg = $"{serviceName} | {action} | uid:{playerUid}";
        Log("SERVICE", msg, ConsoleColor.Green);
    }
    
    public static void Info(string message)
    {
        Log("INFO", message, ConsoleColor.White);
    }
    
    public static void Warning(string message)
    {
        Log("WARN", message, ConsoleColor.Yellow);
    }
    
    public static void Error(string message)
    {
        Log("ERROR", message, ConsoleColor.Red);
    }
    
    public static void Debug(string message)
    {
#if DEBUG
        Log("DEBUG", message, ConsoleColor.Gray);
#endif
    }
    
    public static void RpcLoad(string serviceName)
    {
        Log("RPC", $"Load {serviceName}", ConsoleColor.Magenta);
    }
    
    private static void Log(string level, string message, ConsoleColor color)
    {
        lock (_lock)
        {
            var timestamp = DateTime.Now.ToString("HH:mm:ss");
            Console.ForegroundColor = ConsoleColor.DarkGray;
            Console.Write($"[{timestamp}] ");
            Console.ForegroundColor = color;
            Console.Write($"[{level}] ");
            Console.ForegroundColor = ConsoleColor.White;
            Console.WriteLine(message);
            Console.ResetColor();
        }
    }
}