using System;
using System.Security.Claims;

namespace TopologyServer;

public interface IAuthService
{
    Task<User>GetOrCreateUserAsync(ClaimsPrincipal principal);

}
