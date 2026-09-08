using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;

namespace TopologyServer.Tests;

public sealed class AuthControllerTests : IClassFixture<TestWebApplicationFactory>
{
    private readonly TestWebApplicationFactory _factory;

    public AuthControllerTests(TestWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Public_ReturnsOk()
    {
        using var client = _factory.CreateClient();

        var response = await client.GetAsync("/api/auth/public");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Profile_WithoutAccessToken_ReturnsUnauthorized()
    {
        using var client = _factory.CreateClient();

        var response = await client.GetAsync("/api/auth/profile");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Profile_WithAccessToken_ReturnsCurrentUser()
    {
        using var client = CreateAuthenticatedClient();

        var response = await client.GetAsync("/api/auth/profile");
        var user = await response.Content.ReadFromJsonAsync<User>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(user);
        Assert.Equal("auth0|integration-test-user", user.Auth0UserId);
        Assert.Equal("test@example.com", user.Email);
    }

    [Fact]
    public async Task DeleteProfile_WhenUserExists_ReturnsNoContent()
    {
        _factory.AuthService.DeleteResult = true;
        using var client = CreateAuthenticatedClient();

        var response = await client.DeleteAsync("/api/auth/profile");

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    [Fact]
    public async Task DeleteProfile_WhenUserDoesNotExist_ReturnsNotFound()
    {
        _factory.AuthService.DeleteResult = null;
        using var client = CreateAuthenticatedClient();

        var response = await client.DeleteAsync("/api/auth/profile");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    private HttpClient CreateAuthenticatedClient()
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(TestAuthHandler.SchemeName);
        return client;
    }
}