namespace TopologyServer;

public interface IConnectionRuleService
{
    Task<IReadOnlyList<ConnectionRule>> GetRulesAsync();
    Task<ConnectionRule?> GetRuleAsync(ComponentType sourceType, ComponentType targetType);
    Task SeedDefaultRulesAsync();
}