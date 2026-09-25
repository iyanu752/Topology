using MongoDB.Bson;
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

    public async Task SeedDefaultRulesAsync(bool overwriteExisting = true)
    {
        var defaults = _seedHelper.GetDefaultRules();

        foreach (var rule in defaults)
        {
            var filter = Builders<ConnectionRule>.Filter.Where(existing =>
                existing.SourceType == rule.SourceType && existing.TargetType == rule.TargetType);

            var update = overwriteExisting
                ? Builders<ConnectionRule>.Update
                    .SetOnInsert(existing => existing.Id, ObjectId.GenerateNewId().ToString())
                    .Set(existing => existing.SourceType, rule.SourceType)
                    .Set(existing => existing.TargetType, rule.TargetType)
                    .Set(existing => existing.IsAllowed, rule.IsAllowed)
                    .Set(existing => existing.Severity, rule.Severity)
                    .Set(existing => existing.Message, rule.Message)
                : Builders<ConnectionRule>.Update
                    .SetOnInsert(existing => existing.Id, ObjectId.GenerateNewId().ToString())
                    .SetOnInsert(existing => existing.SourceType, rule.SourceType)
                    .SetOnInsert(existing => existing.TargetType, rule.TargetType)
                    .SetOnInsert(existing => existing.IsAllowed, rule.IsAllowed)
                    .SetOnInsert(existing => existing.Severity, rule.Severity)
                    .SetOnInsert(existing => existing.Message, rule.Message);

            await _rules.UpdateOneAsync(filter, update, new UpdateOptions { IsUpsert = true });
        }

        _logger.LogInformation("Seeded {Count} connection rules. Overwrite existing: {OverwriteExisting}", defaults.Count, overwriteExisting);
    }
}