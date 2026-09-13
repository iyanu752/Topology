using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace TopologyServer.Tests;

public sealed class ConnectionRuleControllerTests : IClassFixture<TestWebApplicationFactory>
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    private readonly TestWebApplicationFactory _factory;

    public ConnectionRuleControllerTests(TestWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task GetRules_ReturnsConnectionRules()
    {
        using var client = _factory.CreateClient();

        var response = await client.GetAsync("/api/connection-rules");
        var rules = await response.Content.ReadFromJsonAsync<List<ConnectionRule>>(JsonOptions);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(rules);
        Assert.Contains(rules, rule => rule.SourceType == ComponentType.Client && rule.TargetType == ComponentType.Database);
        Assert.Contains(rules, rule => rule.SourceType == ComponentType.Client && rule.TargetType == ComponentType.ApiGateway);
        Assert.Contains(rules, rule => rule.SourceType == ComponentType.ApiGateway && rule.TargetType == ComponentType.Service);
    }

    [Fact]
    public async Task GetRule_WhenRuleExists_ReturnsRule()
    {
        using var client = _factory.CreateClient();

        var response = await client.GetAsync("/api/connection-rules/Client/Database");
        var rule = await response.Content.ReadFromJsonAsync<ConnectionRule>(JsonOptions);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(rule);
        Assert.False(rule.IsAllowed);
        Assert.Equal(ValidationSeverity.Error, rule.Severity);
    }

    [Fact]
    public async Task GetRule_WhenApiGatewayRuleExists_ReturnsRule()
    {
        using var client = _factory.CreateClient();

        var response = await client.GetAsync("/api/connection-rules/Client/ApiGateway");
        var rule = await response.Content.ReadFromJsonAsync<ConnectionRule>(JsonOptions);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(rule);
        Assert.True(rule.IsAllowed);
        Assert.Equal(ValidationSeverity.Info, rule.Severity);
    }

    [Fact]
    public async Task GetRule_WhenRuleDoesNotExist_ReturnsNotFound()
    {
        using var client = _factory.CreateClient();

        var response = await client.GetAsync("/api/connection-rules/Cache/ExternalApi");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task SeedRules_ReturnsNoContent()
    {
        using var client = _factory.CreateClient();

        var response = await client.PostAsync("/api/connection-rules/seed", null);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.True(_factory.ConnectionRuleService.SeedCalled);
    }
}
