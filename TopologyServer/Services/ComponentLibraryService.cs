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
    

    public async  Task<IReadOnlyList<ComponentDefinition>> GetComponentsAsync()
    {
        return await _components.Find(_ => true).ToListAsync();
    }

    public async Task<ComponentDefinition?>GetComponentByTypeAsync(ComponentType type)
    {
        var component =await _components.Find(component => component.Type == type).FirstOrDefaultAsync();
        if (component == null)
        {
            _logger.LogWarning("Type does not exist");
            return null;
        }
        return component;
    }

    public async Task SeedDefaultComponentsAsync()
    {
      var defaults = _helper.GetDefaultComponents();
      foreach (var component in defaults)
        {
            await _components.ReplaceOneAsync( existing => existing.Type == component.Type, component,
            new ReplaceOptions {IsUpsert = true });
        }   
    }

}