using System.Diagnostics;
using System.IO;
using StandRiseServer;
using StandRiseServer.Core;
using StandRiseServer.Services;
using StandRiseServer.GameServer;
using StandRiseServer.TelegramMarketBot;

// MongoDB Configuration
const string defaultMongoString = "mongodb://mongo:vvsHryKyEkNaiIgYtMBtkIqdQIOSZPPE@maglev.proxy.rlwy.net:26476";
string mongoConnectionString = Environment.GetEnvironmentVariable("MONGO_URL") ?? defaultMongoString;
string databaseName = Environment.GetEnvironmentVariable("MONGO_DB") ?? "Ryzen";

// Server Configuration
const int defaultPort = 2222;
int serverPort = int.TryParse(Environment.GetEnvironmentVariable("PORT"), out int port) ? port : defaultPort;
const int gameServerPort = 5055;

var runGameServer = args.Contains("--game-server");
var gameServerOnly = args.Contains("--game-server-only");

Logger.Startup("Standoff 2");
Logger.Startup("Load full RPC support");

try
{
    // Core
    Logger.Startup("Connecting to MongoDB...");
    var database = new DatabaseService(mongoConnectionString, databaseName);
    var sessionManager = new SessionManager();
    var protobufHandler = new ProtobufHandler(sessionManager, database);

    // Database init
    await database.InitializeDatabaseAsync();

    // Services
    Logger.RpcLoad("HandshakeRemoteService");
    var handshakeService = new HandshakeService(protobufHandler, database, sessionManager);
    
    Logger.RpcLoad("GoogleAuthRemoteService");
    var googleAuthService = new GoogleAuthService(protobufHandler, database, sessionManager);
    
    Logger.RpcLoad("PKConnectRemoteService");
    var pkConnectService = new PKConnectRemoteService(protobufHandler, database, sessionManager);
    
    Logger.RpcLoad("TokenAuthRemoteService");
    var tokenAuthService = new TokenAuthService(protobufHandler, database, sessionManager);
    
    Logger.RpcLoad("TestAuthRemoteService");
    var testAuthService = new TestAuthRemoteService(protobufHandler, database, sessionManager);
    
    Logger.RpcLoad("PlayerRemoteService");
    var playerService = new PlayerService(protobufHandler, database, sessionManager);
    
    Logger.RpcLoad("PlayerStatsRemoteService");
    var statsService = new StatsService(protobufHandler, database, sessionManager);
    
    Logger.RpcLoad("StorageRemoteService");
    var storageService = new StorageService(protobufHandler, database, sessionManager);
    
    Logger.RpcLoad("InventoryRemoteService");
    var inventoryService = new InventoryService(protobufHandler, database, sessionManager);
    
    Logger.RpcLoad("GameSettingsRemoteService");
    var gameSettingsService = new GameSettingsService(protobufHandler, database, sessionManager);
    
    Logger.RpcLoad("GameSettingsPlayerRemoteService");
    var gameSettingsPlayerService = new GameSettingsPlayerRemoteService(protobufHandler, database, sessionManager);
    
    Logger.RpcLoad("GameServerRemoteService");
    var gameServerRemoteService = new GameServerRemoteService(protobufHandler, database, sessionManager);
    
    Logger.RpcLoad("GameServerStatsRemoteService");
    var gameServerStatsService = new GameServerStatsRemoteService(protobufHandler, database, sessionManager);
    
    Logger.RpcLoad("BoltRemoteService");
    var boltService = new BoltRemoteService(protobufHandler, sessionManager);
    
    Logger.RpcLoad("MatchmakingRemoteService");
    var matchmakingService = new MatchmakingRemoteService(protobufHandler, sessionManager, database);
    
    Logger.RpcLoad("FriendsRemoteService");
    var friendsService = new FriendsRemoteService(protobufHandler, database, sessionManager);
    
    Logger.RpcLoad("ChatRemoteService");
    var chatService = new ChatRemoteService(protobufHandler, database, sessionManager);
    
    Logger.RpcLoad("MarketplaceRemoteService");
    var marketplaceService = new MarketplaceRemoteService(protobufHandler, database, sessionManager);
    
    Logger.RpcLoad("MarketplaceCleanupService");
    var marketplaceCleanupService = new MarketplaceCleanupService(database, protobufHandler, sessionManager);
    
    Logger.RpcLoad("AnalyticsRemoteService");
    var analyticsService = new AnalyticsRemoteService(protobufHandler, sessionManager);
    
    Logger.RpcLoad("GameEventRemoteService");
    var gameEventService = new GameEventService(protobufHandler, database, sessionManager);
    
    Logger.RpcLoad("CouponRemoteService");
    var couponService = new CouponService(protobufHandler, database, sessionManager);
    
    Logger.RpcLoad("ClanRemoteService");
    var clanService = new ClanService(protobufHandler, database, sessionManager);
    
    Logger.RpcLoad("AvatarRemoteService");
    var avatarService = new AvatarService(protobufHandler, database, sessionManager);
    
    Logger.RpcLoad("AdsRemoteService");
    var adsService = new AdsRemoteService(protobufHandler, database, sessionManager);
    
    Logger.RpcLoad("InAppRemoteService");
    var inAppService = new InAppService(protobufHandler, database, sessionManager);
    
    Logger.RpcLoad("HackDetectionRemoteService");
    var hackDetectionService = new HackDetectionService(protobufHandler);
    
    Logger.RpcLoad("GroupRemoteService");
    var groupService = new GroupService(protobufHandler, database, sessionManager);
    
    Logger.RpcLoad("AccountLinkRemoteService");
    var accountLinkService = new AccountLinkService(protobufHandler, database, sessionManager);

    Logger.RpcLoad("KeyAuthRemoteService");
    var keyAuthService = new KeyAuthService(protobufHandler, database, sessionManager);

    // TCP Server
    var tcpServer = new TcpServer(serverPort, protobufHandler, sessionManager, database);
    
    // Game Servers
    GameServerMain? gameServer = null;
    MatchmakingService? matchmaking = null;
    RankedGameServer? rankedServer = null;
    CasualGameServer? casualServer = null;
    
    if (runGameServer || gameServerOnly)
    {
        gameServer = new GameServerMain(gameServerPort, "test");
        matchmaking = new MatchmakingService(database, "0.0.0.0", gameServerPort);
        matchmaking.Start();
        _ = gameServer.StartAsync();
        
        rankedServer = new RankedGameServer(database, "0.0.0.0", 5056);
        rankedServer.Start();
        
        casualServer = new CasualGameServer(database, "0.0.0.0", 5055);
        casualServer.Start();
    }
    
    var shutdownRequested = false;
    Console.CancelKeyPress += (sender, e) =>
    {
        e.Cancel = true;
        shutdownRequested = true;
    };

    var adminConsole = new AdminConsole(database, sessionManager);
    
    _ = Task.Run(async () =>
    {
        while (!shutdownRequested)
        {
            var input = Console.ReadLine()?.Trim();
            if (input == null) 
            {
                 // Stdin closed (e.g. running in Docker non-interactively)
                 await Task.Delay(-1, CancellationToken.None);
                 break;
            }
            if (string.IsNullOrEmpty(input)) continue;

            var parts = input.Split(' ', 2);
            var command = parts[0].ToLower();
            var cmdArgs = parts.Length > 1 ? parts[1] : "";

            switch (command)
            {
                case "status":
                    Logger.Info($"Online: {sessionManager.GetAllSessions().Count()} players");
                    break;
                case "players":
                    foreach (var s in sessionManager.GetAllSessions())
                        Logger.Info($"  {s.Token[..8]}...");
                    break;
                case "admin":
                    await adminConsole.RunAsync();
                    break;
                case "stop":
                    shutdownRequested = true;
                    break;
                case "help":
                    Logger.Info("Commands: status, players, admin, stop");
                    break;
            }
        }
    });

    Logger.Startup($"Server ready on port {serverPort}");
    
    // Start Market Bot
    MarketBot? marketBot = null;
    string marketBotToken = Environment.GetEnvironmentVariable("MARKET_BOT_TOKEN") ?? "8534163771:AAGeJqVJLfOkxf0lbEbKKENqQnCcG4yn1zo";
    if (!string.IsNullOrEmpty(marketBotToken))
    {
        Logger.Startup("Starting Market Bot...");
        marketBot = new MarketBot(marketBotToken, mongoConnectionString, databaseName);
        _ = marketBot.StartAsync();
    }
    else
    {
        Logger.Startup("MARKET_BOT_TOKEN not set, Market Bot disabled");
    }
    
    // Start Telegram Bot
    _ = Task.Run(async () =>
    {
        try
        {
            // Try to find the project root (where the source code is)
            string baseDir = AppContext.BaseDirectory;
            string projectRoot = baseDir;
            
            // If we are in bin/Debug/..., go up to the project root
            while (!Directory.Exists(Path.Combine(projectRoot, "RyzenBot")) && Directory.GetParent(projectRoot) != null)
            {
                projectRoot = Directory.GetParent(projectRoot)!.FullName;
            }

            string botProjectDir = Path.Combine(projectRoot, "RyzenBot");
            string botDllPath = Path.Combine(baseDir, "RyzenBot.dll");
            bool useDll = File.Exists(botDllPath);
            
            Logger.Startup(useDll ? "Starting Telegram Bot (DLL)..." : $"Starting Telegram Bot (Source from {botProjectDir})...");
            
            var botProcess = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "dotnet",
                    Arguments = useDll ? "RyzenBot.dll" : $"run --project \"{Path.Combine(botProjectDir, "RyzenBot.csproj")}\"",
                    WorkingDirectory = useDll ? baseDir : botProjectDir,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                }
            };

            botProcess.OutputDataReceived += (s, e) => { if (e.Data != null) Logger.Info($"[Bot] {e.Data}"); };
            botProcess.ErrorDataReceived += (s, e) => { if (e.Data != null) Logger.Error($"[Bot Error] {e.Data}"); };

            botProcess.Start();
            botProcess.BeginOutputReadLine();
            botProcess.BeginErrorReadLine();
        }
        catch (Exception ex)
        {
            Logger.Error($"Failed to start Telegram Bot: {ex.Message}");
        }
    });

    _ = Task.Run(async () => await tcpServer.StartAsync());
    await Task.Delay(500);
    
    await adminConsole.RunAsync();

    Logger.Info("Shutting down...");
    marketBot?.Stop();
    tcpServer.Stop();
    gameServer?.Stop();
    matchmaking?.Stop();
    rankedServer?.Stop();
    casualServer?.Stop();
    sessionManager.Dispose();
}
catch (Exception ex)
{
    Logger.Error(ex.Message);
    Environment.Exit(1);
}
