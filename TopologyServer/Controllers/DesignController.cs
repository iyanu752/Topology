using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace TopologyServer;

[Route("api/rooms/{roomId}/design")]
[ApiController]
[Authorize]
public class DesignController : ControllerBase
{
    private readonly IDesignService _designService;
    private readonly ILogger<DesignController> _logger;

    public DesignController(IDesignService designService, ILogger<DesignController> logger)
    {
        _designService = designService;
        _logger = logger;
    }

    [HttpGet]
    public async Task<IActionResult> GetDesign(string roomId)
    {
        try
        {
            var design = await _designService.GetDesignAsync(User, roomId);
            return design is null ? NotFound("Design not found") : Ok(design);
        }
        catch (KeyNotFoundException)
        {
            _logger.LogWarning("Room not found while loading design for room {RoomId}", roomId);
            return NotFound("Room not found");
        }
        catch (UnauthorizedAccessException)
        {
            _logger.LogWarning("Unauthorized design read for room {RoomId}", roomId);
            return Forbid();
        }
    }

    [HttpPut]
    public async Task<IActionResult> SaveDesign(string roomId, SaveDesignDto saveDesignDto)
    {
        try
        {
            var design = await _designService.SaveDesignAsync(User, roomId, saveDesignDto);
            return Ok(design);
        }
        catch (DesignValidationException exception)
        {
            _logger.LogWarning("Design validation failed for room {RoomId}", roomId);
            return BadRequest(exception.Result);
        }
        catch (KeyNotFoundException)
        {
            _logger.LogWarning("Room not found while saving design for room {RoomId}", roomId);
            return NotFound("Room not found");
        }
        catch (UnauthorizedAccessException)
        {
            _logger.LogWarning("Unauthorized design save for room {RoomId}", roomId);
            return Forbid();
        }
    }

    [HttpDelete]
    public async Task<IActionResult> DeleteDesign(string roomId)
    {
        try
        {
            var deleted = await _designService.DeleteDesignAsync(User, roomId);
            return deleted ? NoContent() : NotFound("Design not found");
        }
        catch (KeyNotFoundException)
        {
            _logger.LogWarning("Room not found while deleting design for room {RoomId}", roomId);
            return NotFound("Room not found");
        }
        catch (UnauthorizedAccessException)
        {
            _logger.LogWarning("Unauthorized design delete for room {RoomId}", roomId);
            return Forbid();
        }
    }
}