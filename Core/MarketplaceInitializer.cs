using StandRiseServer.Models;
using StandRiseServer.Config;
using MongoDB.Driver;
using static StandRiseServer.Core.Logger;

namespace StandRiseServer.Core;

public static class MarketplaceInitializer
{
    public static async Task InitializeAsync(DatabaseService database)
    {
        Logger.Startup("Initializing marketplace...");
        
        // Инициализируем конфигурацию рынка
        await InitializeMarketplaceConfigAsync(database);
        
        // Инициализируем определения кейсов
        await InitializeCaseDefinitionsAsync(database);
        
        Logger.Startup("Marketplace initialized successfully");
    }
    
    private static async Task InitializeMarketplaceConfigAsync(DatabaseService database)
    {
        var configCollection = database.GetCollection<MarketplaceConfig>("marketplace_config");
        
        var existingConfig = await configCollection.Find(x => x.ConfigId == "default").FirstOrDefaultAsync();
        if (existingConfig == null)
        {
            var defaultConfig = new MarketplaceConfig
            {
                ConfigId = "default",
                Enabled = true,
                CommissionPercent = 0.05f, // 5%
                MinCommission = 1.0f,
                //CurrencyId = 101, // Silver
                CurrencyId = 102, // Gold
                MinPrice = 0.03f,
                MaxPrice = 32000000.0f,
                MaxActiveListings = 10,
                ListingDurationHours = 168 // 7 дней
            };
            
            await configCollection.InsertOneAsync(defaultConfig);
            Logger.Startup("Created default marketplace config");
        }
        else
        {
            Logger.Startup("Marketplace config already exists");
        }
    }
    
    private static async Task InitializeCaseDefinitionsAsync(DatabaseService database)
    {
        var caseCollection = database.GetCollection<CaseDefinition>("case_definitions");
        
        var existingCount = await caseCollection.CountDocumentsAsync(FilterDefinition<CaseDefinition>.Empty);
        if (existingCount > 0)
        {
            await caseCollection.DeleteManyAsync(FilterDefinition<CaseDefinition>.Empty);
        }
        
        var caseDefinitions = CaseDropConfig.GetAllCaseDefinitions();
        await caseCollection.InsertManyAsync(caseDefinitions);
        
        try
        {
            var indexKeys = Builders<CaseDefinition>.IndexKeys.Ascending(x => x.CaseId);
            await caseCollection.Indexes.CreateOneAsync(new CreateIndexModel<CaseDefinition>(indexKeys, new CreateIndexOptions { Unique = true }));
        }
        catch (Exception ex)
        {
             // Ignore index creation errors to allow server startup on limited DBs
             Logger.Error($"Failed to create index for CaseDefinition: {ex.Message}");
        }
        
        Logger.Startup($"Loaded {caseDefinitions.Count} case definitions");
    }
}