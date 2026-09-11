using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace TopologyServer;

[Route("api/rooms/{roomId}/simulations")]
[ApiController]
[Authorize]
public class SimulationController : ControllerBase
{
    private readonly ISimulationService _simulationService;
    private readonly ILogger<SimulationController> _logger;

    public SimulationController(ISimulationService simulationService, ILogger<SimulationController> logger)
    {
        _simulationService = simulationService;
        _logger = logger;
    }

    [HttpPost("run")]
    public async Task<IActionResult> RunSimulation(string roomId, RunSimulationDto runSimulationDto)
    {
        try
        {
            var result = await _simulationService.RunSimulationAsync(User, roomId, runSimulationDto);
            return Ok(result);
        }
        catch (KeyNotFoundException)
        {
            _logger.LogWarning("Design or room not found while running simulation for room {RoomId}", roomId);
            return NotFound("Design not found");
        }
        catch (UnauthorizedAccessException)
        {
            _logger.LogWarning("Unauthorized simulation request for room {RoomId}", roomId);
            return Forbid();
        }
        catch (NotSupportedException exception)
        {
            _logger.LogWarning(exception, "Unsupported simulation scenario requested for room {RoomId}", roomId);
            return BadRequest(exception.Message);
        }
    }
}
