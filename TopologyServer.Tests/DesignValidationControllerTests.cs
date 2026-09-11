using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace TopologyServer.Tests;

public sealed class DesignValidationControllerTests : IClassFixture<TestWebApplicationFactory>
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    private readonly TestWebApplicationFactory _factory;

    public DesignValidationControllerTests(TestWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task ValidateDesign_WithAccessToken_ReturnsValidationResult()
    {
        ResetFakes();
        _factory.DesignValidationService.Result = new DesignValidationResult
        {
            Issues =
            [
                new DesignValidationIssue
                {
                    Severity = ValidationSeverity.Error,
                    Message = "Clients should not connect directly to databases.",
                    EdgeId = "edge-1"
                }
            ]
        };
        using var client = CreateAuthenticatedClient();
        var dto = new SaveDesignDto
        {
            Nodes =
            [
                new Node { Id = "client-1", Type = ComponentType.Client, Label = "Client" },
                new Node { Id = "db-1", Type = ComponentType.Database, Label = "Database" }
            ],
            Edges =
            [
                new Edge { Id = "edge-1", SourceNodeId = "client-1", TargetNodeId = "db-1" }
            ]
        };

        var response = await client.PostAsJsonAsync("/api/rooms/room-1/design/validate", dto, JsonOptions);
        var result = await response.Content.ReadFromJsonAsync<DesignValidationResult>(JsonOptions);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(result);
        Assert.False(result.IsValid);
        Assert.Single(result.Issues);
    }

    [Fact]
    public async Task ValidateDesign_WithoutAccessToken_ReturnsUnauthorized()
    {
        ResetFakes();
        using var client = _factory.CreateClient();
        var dto = new SaveDesignDto { Nodes = [], Edges = [] };

        var response = await client.PostAsJsonAsync("/api/rooms/room-1/design/validate", dto, JsonOptions);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task ValidateDesign_WhenRoomDoesNotExist_ReturnsNotFound()
    {
        ResetFakes();
        _factory.RoomAccessService.ThrowsNotFound = true;
        using var client = CreateAuthenticatedClient();
        var dto = new SaveDesignDto { Nodes = [], Edges = [] };

        var response = await client.PostAsJsonAsync("/api/rooms/missing-room/design/validate", dto, JsonOptions);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task ValidateDesign_WhenUserIsNotRoomMember_ReturnsForbidden()
    {
        ResetFakes();
        _factory.RoomAccessService.ThrowsUnauthorized = true;
        using var client = CreateAuthenticatedClient();
        var dto = new SaveDesignDto { Nodes = [], Edges = [] };

        var response = await client.PostAsJsonAsync("/api/rooms/room-1/design/validate", dto, JsonOptions);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    private HttpClient CreateAuthenticatedClient()
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(TestAuthHandler.SchemeName);
        return client;
    }

    private void ResetFakes()
    {
        _factory.RoomAccessService.ThrowsNotFound = false;
        _factory.RoomAccessService.ThrowsUnauthorized = false;
        _factory.DesignValidationService.Result = new DesignValidationResult();
    }
}