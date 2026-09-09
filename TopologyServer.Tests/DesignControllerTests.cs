using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace TopologyServer.Tests;

public sealed class DesignControllerTests : IClassFixture<TestWebApplicationFactory>
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    private readonly TestWebApplicationFactory _factory;

    public DesignControllerTests(TestWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task GetDesign_WithAccessToken_ReturnsDesign()
    {
        ResetDesignService();
        using var client = CreateAuthenticatedClient();

        var response = await client.GetAsync("/api/rooms/room-1/design");
        var design = await response.Content.ReadFromJsonAsync<Design>(JsonOptions);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(design);
        Assert.Equal("room-1", design.RoomId);
    }

    [Fact]
    public async Task GetDesign_WithoutAccessToken_ReturnsUnauthorized()
    {
        ResetDesignService();
        using var client = _factory.CreateClient();

        var response = await client.GetAsync("/api/rooms/room-1/design");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetDesign_WhenDesignDoesNotExist_ReturnsNotFound()
    {
        ResetDesignService();
        _factory.DesignService.GetResult = null;
        using var client = CreateAuthenticatedClient();

        var response = await client.GetAsync("/api/rooms/room-1/design");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GetDesign_WhenUserIsNotRoomMember_ReturnsForbidden()
    {
        ResetDesignService();
        _factory.DesignService.ThrowsUnauthorized = true;
        using var client = CreateAuthenticatedClient();

        var response = await client.GetAsync("/api/rooms/room-1/design");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task SaveDesign_WithAccessToken_ReturnsSavedDesign()
    {
        ResetDesignService();
        using var client = CreateAuthenticatedClient();
        var dto = new SaveDesignDto
        {
            Nodes =
            [
                new Node
                {
                    Id = "node-1",
                    Type = NodeType.Server,
                    Label = "API Server",
                    X = 120,
                    Y = 80
                }
            ],
            Edges = []
        };

        var response = await client.PutAsJsonAsync("/api/rooms/room-1/design", dto, JsonOptions);
        var design = await response.Content.ReadFromJsonAsync<Design>(JsonOptions);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(design);
        Assert.Single(design.Nodes);
        Assert.Equal("API Server", design.Nodes[0].Label);
    }

    [Fact]
    public async Task SaveDesign_WithoutAccessToken_ReturnsUnauthorized()
    {
        ResetDesignService();
        using var client = _factory.CreateClient();
        var dto = new SaveDesignDto { Nodes = [], Edges = [] };

        var response = await client.PutAsJsonAsync("/api/rooms/room-1/design", dto, JsonOptions);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task SaveDesign_WhenRoomDoesNotExist_ReturnsNotFound()
    {
        ResetDesignService();
        _factory.DesignService.ThrowsNotFound = true;
        using var client = CreateAuthenticatedClient();
        var dto = new SaveDesignDto { Nodes = [], Edges = [] };

        var response = await client.PutAsJsonAsync("/api/rooms/missing-room/design", dto, JsonOptions);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task SaveDesign_WhenUserIsNotRoomMember_ReturnsForbidden()
    {
        ResetDesignService();
        _factory.DesignService.ThrowsUnauthorized = true;
        using var client = CreateAuthenticatedClient();
        var dto = new SaveDesignDto { Nodes = [], Edges = [] };

        var response = await client.PutAsJsonAsync("/api/rooms/room-1/design", dto, JsonOptions);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task DeleteDesign_WhenDesignExists_ReturnsNoContent()
    {
        ResetDesignService();
        _factory.DesignService.DeleteResult = true;
        using var client = CreateAuthenticatedClient();

        var response = await client.DeleteAsync("/api/rooms/room-1/design");

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    [Fact]
    public async Task DeleteDesign_WhenDesignDoesNotExist_ReturnsNotFound()
    {
        ResetDesignService();
        _factory.DesignService.DeleteResult = false;
        using var client = CreateAuthenticatedClient();

        var response = await client.DeleteAsync("/api/rooms/room-1/design");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task DeleteDesign_WhenUserIsNotRoomOwner_ReturnsForbidden()
    {
        ResetDesignService();
        _factory.DesignService.ThrowsUnauthorized = true;
        using var client = CreateAuthenticatedClient();

        var response = await client.DeleteAsync("/api/rooms/room-1/design");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    private HttpClient CreateAuthenticatedClient()
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(TestAuthHandler.SchemeName);
        return client;
    }

    private void ResetDesignService()
    {
        _factory.DesignService.GetResult = new Design
        {
            Id = "design-1",
            RoomId = "room-1",
            UpdatedByUserId = "user-1",
            Nodes = [],
            Edges = [],
            Revision = 1,
            UpdatedAt = DateTime.UtcNow
        };
        _factory.DesignService.SaveResult = new Design
        {
            Id = "design-1",
            RoomId = "room-1",
            UpdatedByUserId = "user-1",
            Nodes = [],
            Edges = [],
            Revision = 2,
            UpdatedAt = DateTime.UtcNow
        };
        _factory.DesignService.DeleteResult = true;
        _factory.DesignService.ThrowsNotFound = false;
        _factory.DesignService.ThrowsUnauthorized = false;
    }
}