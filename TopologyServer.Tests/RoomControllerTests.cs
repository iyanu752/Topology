using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace TopologyServer.Tests;

public sealed class RoomControllerTests : IClassFixture<TestWebApplicationFactory>
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };
    private readonly TestWebApplicationFactory _factory;

    public RoomControllerTests(TestWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task CreateRoom_WithAccessToken_ReturnsCreatedRoom()
    {
        using var client = CreateAuthenticatedClient();
        var dto = new CreateRoomDto { Type = AccessType.Private, RoomKey = 123456 };

        var response = await client.PostAsJsonAsync("/api/room/create", dto);
        var room = await response.Content.ReadFromJsonAsync<Room>(JsonOptions);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(room);
        Assert.Equal(AccessType.Private, room.Type);
        Assert.Equal(123456, room.RoomKey);
    }

    [Fact]
    public async Task CreateRoom_WithoutAccessToken_ReturnsUnauthorized()
    {
        using var client = _factory.CreateClient();
        var dto = new CreateRoomDto { Type = AccessType.Public };

        var response = await client.PostAsJsonAsync("/api/room/create", dto);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task JoinRoom_WhenRoomExists_ReturnsRoom()
    {
        _factory.RoomService.JoinThrowsUnauthorized = false;
        _factory.RoomService.JoinResult = new Room
        {
            Id = "room-1",
            OwnerUserId = "user-1",
            MemberUserIds = ["user-1", "user-2"],
            Type = AccessType.Public,
            CreatedAt = DateTime.UtcNow
        };
        using var client = CreateAuthenticatedClient();
        var dto = new JoinRoomDto { Id = "room-1" };

        var response = await client.PostAsJsonAsync("/api/room/join", dto);
        var room = await response.Content.ReadFromJsonAsync<Room>(JsonOptions);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(room);
        Assert.Equal("room-1", room.Id);
    }

    [Fact]
    public async Task JoinRoom_WhenRoomDoesNotExist_ReturnsNotFound()
    {
        _factory.RoomService.JoinThrowsUnauthorized = false;
        _factory.RoomService.JoinResult = null;
        using var client = CreateAuthenticatedClient();
        var dto = new JoinRoomDto { Id = "missing-room" };

        var response = await client.PostAsJsonAsync("/api/room/join", dto);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task JoinRoom_WithInvalidPrivateRoomKey_ReturnsForbidden()
    {
        _factory.RoomService.JoinThrowsUnauthorized = true;
        using var client = CreateAuthenticatedClient();
        var dto = new JoinRoomDto { Id = "room-1", RoomKey = 111111 };

        var response = await client.PostAsJsonAsync("/api/room/join", dto);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task DeleteRoom_WhenOwnerDeletes_ReturnsNoContent()
    {
        _factory.RoomService.DeleteThrowsUnauthorized = false;
        _factory.RoomService.DeleteResult = true;
        using var client = CreateAuthenticatedClient();
        var request = CreateDeleteRequest(new DeleteRoomDto { Id = "room-1" });

        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    [Fact]
    public async Task DeleteRoom_WhenRoomDoesNotExist_ReturnsNotFound()
    {
        _factory.RoomService.DeleteThrowsUnauthorized = false;
        _factory.RoomService.DeleteResult = null;
        using var client = CreateAuthenticatedClient();
        var request = CreateDeleteRequest(new DeleteRoomDto { Id = "missing-room" });

        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task DeleteRoom_WhenUserIsNotOwner_ReturnsForbidden()
    {
        _factory.RoomService.DeleteThrowsUnauthorized = true;
        using var client = CreateAuthenticatedClient();
        var request = CreateDeleteRequest(new DeleteRoomDto { Id = "room-1" });

        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    private HttpClient CreateAuthenticatedClient()
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(TestAuthHandler.SchemeName);
        return client;
    }

    private static HttpRequestMessage CreateDeleteRequest(DeleteRoomDto dto)
    {
        return new HttpRequestMessage(HttpMethod.Delete, "/api/room")
        {
            Content = JsonContent.Create(dto)
        };
    }
}