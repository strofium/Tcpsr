using StandRiseServer.Models;
using StandRiseServer.Config;
using MongoDB.Driver;
using static StandRiseServer.Core.Logger;

namespace StandRiseServer.Core;

public static class InventoryInitializer
{
    public static async Task InitializeAsync(DatabaseService database)
    {
        Logger.Startup("Loading inventory definitions...");
        
        var collection = database.GetCollection<InventoryItemDefinition>("inventory_definitions");
        
        var existingCount = await collection.CountDocumentsAsync(FilterDefinition<InventoryItemDefinition>.Empty);
        if (existingCount > 0)
        {
            await collection.DeleteManyAsync(FilterDefinition<InventoryItemDefinition>.Empty);
        }
        
        var definitions = SkinsConfig.GetAllItems();
        AddMissingItems(definitions);
        
        await collection.InsertManyAsync(definitions);
        
        try 
        {
            var indexKeys = Builders<InventoryItemDefinition>.IndexKeys.Ascending(x => x.ItemId);
            await collection.Indexes.CreateOneAsync(new CreateIndexModel<InventoryItemDefinition>(indexKeys, new CreateIndexOptions { Unique = true }));
        }
        catch (Exception ex)
        {
            // Ignore index creation errors (e.g. disk full on free tier) to allow server to start
            Logger.Error($"Failed to create index for InventoryItemDefinition: {ex.Message}");
        }
        
        Logger.Startup($"Loaded {definitions.Count} inventory definitions");
    }
    
    /// <summary>
    /// Добавляет недостающие предметы если нужно
    /// </summary>
    private static void AddMissingItems(List<InventoryItemDefinition> definitions)
    {
        var existingIds = definitions.Select(d => d.ItemId).ToHashSet();
        
        // Можно добавить дополнительные предметы если нужно
        // Пока оставляем пустым
    }
}