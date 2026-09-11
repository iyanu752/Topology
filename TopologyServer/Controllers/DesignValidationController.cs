using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace TopologyServer;

[ApiController]
[Route("api/rooms/{roomId}/design/validate")]
[Authorize]
public class DesignValidationController : ControllerBase
{
    private readonly IAuthService _authService;
    private readonly IRoomAccessService _roomAccessService;
    private readonly IDesignValidationService _designValidationService;
    private readonly ILogger<DesignValidationController> _logger;

    public DesignValidationController(
        IAuthService authService,
        IRoomAccessService roomAccessService,
        IDesignValidationService designValidationService,
        ILogger<DesignValidationController> logger)
    {
        _authService = authService;
        _roomAccessService = roomAccessService;
        _designValidationService = designValidationService;
        _logger = logger;
    }

    [HttpPost]
    public async Task<IActionResult> ValidateDesign(string roomId, SaveDesignDto design)
    {
        try
        {
            var user = await _authService.GetOrCreateUserAsync(User);
            await _roomAccessService.EnsureRoomMemberAsync(roomId, user.Id!);

            var result = await _designValidationService.ValidateDesignAsync(design);
            return Ok(result);
        }
        catch (KeyNotFoundException)
        {
            _logger.LogWarning("Room not found while validating design for room {RoomId}", roomId);
            return NotFound("Room not found");
        }
        catch (UnauthorizedAccessException)
        {
            _logger.LogWarning("Unauthorized design validation for room {RoomId}", roomId);
            return Forbid();
        }
    }
}