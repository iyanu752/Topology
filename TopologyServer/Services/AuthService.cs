using System;
using System.Security.Claims;
using MongoDB.Driver;

namespace TopologyServer;

public class AuthService : IAuthService
{
    private readonly IMongoCollection<User> _users;
    private readonly ILogger<AuthService> _logger;

    public AuthService(IMongoDatabase database , ILogger<AuthService> logger)
    {   _users = database.GetCollection<User>("Users");
        _logger = logger;
    }

    public async Task<User> GetOrCreateUserAsync(ClaimsPrincipal principal)
    {
        var auth0UserId = principal.FindFirst("sub")?.Value;
        var email = principal.FindFirst("email")?.Value;
        var name = principal.FindFirst("name")?.Value;

        var existingUser = await _users.Find(user => user.Auth0UserId == auth0UserId).FirstOrDefaultAsync();

        if ( existingUser is not null)
        {
            _logger.LogInformation("User already exists, returning existing user");
            return existingUser;
        }
        var newUser = new User
        {
            Auth0UserId =  auth0UserId!,
            Email = email,
            UserName = name,
            CreatedAt = DateTime.UtcNow
        };
        await _users.InsertOneAsync(newUser);
        _logger.LogInformation("New user created");
        return newUser; 
    }

}
