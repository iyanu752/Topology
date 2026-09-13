using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace TopologyServer.Tests;

public sealed class ComponentLibraryControllerTests : IClassFixture<TestWebApplicationFactory>
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    private readonly TestWebApplicationFactory _factory;

    public ComponentLibraryControllerTests(TestWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task GetComponents_ReturnsComponentLibrary()
    {
        using var client = _factory.CreateClient();

        var response = await client.GetAsync("/api/components");
        var components = await response.Content.ReadFromJsonAsync<List<ComponentDefinition>>(JsonOptions);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(components);
        Assert.Contains(components, component => component.Type == ComponentType.Database);
        Assert.Contains(components, component => component.Type == ComponentType.Service);
        Assert.Contains(components, component => component.Type == ComponentType.ApiGateway);
    }

    [Fact]
    public async Task GetComponent_WhenComponentExists_ReturnsComponent()
    {
        using var client = _factory.CreateClient();

        var response = await client.GetAsync("/api/components/Database");
        var component = await response.Content.ReadFromJsonAsync<ComponentDefinition>(JsonOptions);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(component);
        Assert.Equal(ComponentType.Database, component.Type);
        Assert.Equal(ComponentCategory.Data, component.Category);
    }

    [Fact]
    public async Task GetComponent_WhenApiGatewayExists_ReturnsComponent()
    {
        using var client = _factory.CreateClient();

        var response = await client.GetAsync("/api/components/ApiGateway");
        var component = await response.Content.ReadFromJsonAsync<ComponentDefinition>(JsonOptions);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(component);
        Assert.Equal(ComponentType.ApiGateway, component.Type);
        Assert.Equal(ComponentCategory.Network, component.Category);
    }

    [Fact]
    public async Task GetComponent_WhenComponentDoesNotExist_ReturnsNotFound()
    {
        using var client = _factory.CreateClient();

        var response = await client.GetAsync("/api/components/Queue");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task SeedComponents_ReturnsNoContent()
    {
        using var client = _factory.CreateClient();

        var response = await client.PostAsync("/api/components/seed", null);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.True(_factory.ComponentLibraryService.SeedCalled);
    }
}
