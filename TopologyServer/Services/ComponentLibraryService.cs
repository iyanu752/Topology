using MongoDB.Bson;
using MongoDB.Driver;

namespace TopologyServer;

public class ComponentLibraryService : IComponentLibraryService
{
    private readonly IMongoCollection<ComponentDefinition> _components;
    private readonly ILogger<ComponentLibraryService> _logger;
    private readonly ComponentDefinitionSeedHelper _helper;

    public ComponentLibraryService(IMongoDatabase database, ILogger<ComponentLibraryService> logger, ComponentDefinitionSeedHelper helper)
    {
        _components = database.GetCollection<ComponentDefinition>("ComponentLibrary");
        _logger = logger;
        _helper = helper;
    }

    public async Task<IReadOnlyList<ComponentDefinition>> GetComponentsAsync()
    {
        return await _components.Find(_ => true).ToListAsync();
    }

    public async Task<ComponentDefinition?> GetComponentByTypeAsync(ComponentType type)
    {
        var component = await _components.Find(component => component.Type == type).FirstOrDefaultAsync();
        if (component == null)
        {
            _logger.LogWarning("Component type {Type} does not exist", type);
            return null;
        }

        return component;
    }

    public async Task SeedDefaultComponentsAsync(bool overwriteExisting = true)
    {
        var defaults = _helper.GetDefaultComponents();

        foreach (var component in defaults)
        {
            var filter = Builders<ComponentDefinition>.Filter.Where(existing => existing.Type == component.Type);

            var update = overwriteExisting
                ? Builders<ComponentDefinition>.Update
                    .SetOnInsert(existing => existing.Id, ObjectId.GenerateNewId().ToString())
                    .Set(existing => existing.Type, component.Type)
                    .Set(existing => existing.Label, component.Label)
                    .Set(existing => existing.Category, component.Category)
                    .Set(existing => existing.Description, component.Description)
                    .Set(existing => existing.Properties, component.Properties)
                : Builders<ComponentDefinition>.Update
                    .SetOnInsert(existing => existing.Id, ObjectId.GenerateNewId().ToString())
                    .SetOnInsert(existing => existing.Type, component.Type)
                    .SetOnInsert(existing => existing.Label, component.Label)
                    .SetOnInsert(existing => existing.Category, component.Category)
                    .SetOnInsert(existing => existing.Description, component.Description)
                    .SetOnInsert(existing => existing.Properties, component.Properties);

            await _components.UpdateOneAsync(filter, update, new UpdateOptions { IsUpsert = true });
        }

        _logger.LogInformation("Seeded {Count} component definitions. Overwrite existing: {OverwriteExisting}", defaults.Count, overwriteExisting);
    }
}
