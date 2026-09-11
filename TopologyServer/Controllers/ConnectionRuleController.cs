using Microsoft.AspNetCore.Mvc;

namespace TopologyServer;

[ApiController]
[Route("api/connection-rules")]
public class ConnectionRuleController : ControllerBase
{
    private readonly IConnectionRuleService _connectionRuleService;

    public ConnectionRuleController(IConnectionRuleService connectionRuleService)
    {
        _connectionRuleService = connectionRuleService;
    }

    [HttpGet]
    public async Task<IActionResult> GetRules()
    {
        var rules = await _connectionRuleService.GetRulesAsync();
        return Ok(rules);
    }

    [HttpGet("{sourceType}/{targetType}")]
    public async Task<IActionResult> GetRule(ComponentType sourceType, ComponentType targetType)
    {
        var rule = await _connectionRuleService.GetRuleAsync(sourceType, targetType);
        return rule is null ? NotFound("Connection rule not found") : Ok(rule);
    }

    [HttpPost("seed")]
    public async Task<IActionResult> SeedRules()
    {
        await _connectionRuleService.SeedDefaultRulesAsync();
        return NoContent();
    }
}