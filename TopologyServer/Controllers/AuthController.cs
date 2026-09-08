using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace TopologyServer.Controllers;

[ApiController]
[Route("api/[controller]")]
public sealed class AuthController : ControllerBase
{
    public readonly IAuthService _authService;

    public AuthController (IAuthService authService)
    {
        _authService = authService;
    }


    [HttpGet("public")]
    [AllowAnonymous]
    public IActionResult Public() => Ok(new { Message = "Auth0 JWT authentication is configured." });

    [HttpGet("profile")]
    [Authorize]
    public async Task <IActionResult> Profile()
    {
        var user = await _authService.GetOrCreateUserAsync(User);
        return Ok(user);
    }


}