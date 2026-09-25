namespace TopologyServer;

public interface IComponentLibraryService
{
    Task<IReadOnlyList<ComponentDefinition>> GetComponentsAsync();
    Task<ComponentDefinition?> GetComponentByTypeAsync(ComponentType type);
    Task SeedDefaultComponentsAsync(bool overwriteExisting = true);
}