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

    public async Task<bool?> DeleteUserAsync(ClaimsPrincipal principal)
    {
        var auth0UserId = principal.FindFirst("sub")?.Value;
        if (string.IsNullOrWhiteSpace(auth0UserId))
        {
            _logger.LogWarning("Cannot delete user because Auth0 user id was not found");
            return false;
        }
        var result = await _users.DeleteOneAsync(user => user.Auth0UserId == auth0UserId);

        if (result.DeletedCount == 0)
        {
            _logger.LogInformation("No user found to delete");
            return null;
        } 

        _logger.LogInformation("Deleted user with Auth0 id {Auth0UserId}", auth0UserId);
        return true;
    }
}
