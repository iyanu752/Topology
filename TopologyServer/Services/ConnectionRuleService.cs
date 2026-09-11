using MongoDB.Driver;

namespace TopologyServer;

public class ConnectionRuleService : IConnectionRuleService
{
    private readonly IMongoCollection<ConnectionRule> _rules;
    private readonly ConnectionRuleSeedHelper _seedHelper;
    private readonly ILogger<ConnectionRuleService> _logger;

    public ConnectionRuleService(
        IMongoDatabase database,
        ConnectionRuleSeedHelper seedHelper,
        ILogger<ConnectionRuleService> logger)
    {
        _rules = database.GetCollection<ConnectionRule>("ConnectionRules");
        _seedHelper = seedHelper;
        _logger = logger;
    }

    public async Task<IReadOnlyList<ConnectionRule>> GetRulesAsync()
    {
        return await _rules
            .Find(_ => true)
            .SortBy(rule => rule.SourceType)
            .ThenBy(rule => rule.TargetType)
            .ToListAsync();
    }

    public async Task<ConnectionRule?> GetRuleAsync(ComponentType sourceType, ComponentType targetType)
    {
        return await _rules
            .Find(rule => rule.SourceType == sourceType && rule.TargetType == targetType)
            .FirstOrDefaultAsync();
    }

    public async Task SeedDefaultRulesAsync()
    {
        var defaults = _seedHelper.GetDefaultRules();

        foreach (var rule in defaults)
        {
            await _rules.ReplaceOneAsync(
                existing => existing.SourceType == rule.SourceType && existing.TargetType == rule.TargetType,
                rule,
                new ReplaceOptions { IsUpsert = true });
        }

        _logger.LogInformation("Seeded {Count} connection rules", defaults.Count);
    }
}