using System.Security.Claims;

namespace TopologyServer;

public class SimulationService : ISimulationService
{
    private readonly IDesignService _designService;
    private readonly IReadOnlyDictionary<SimulationScenario, ISimulationScenarioHandler> _handlers;
    private readonly ILogger<SimulationService> _logger;

    public SimulationService(
        IDesignService designService,
        IEnumerable<ISimulationScenarioHandler> handlers,
        ILogger<SimulationService> logger)
    {
        _designService = designService;
        _handlers = handlers.ToDictionary(handler => handler.Scenario);
        _logger = logger;
    }

    public async Task<SimulationResult> RunSimulationAsync(ClaimsPrincipal principal, string roomId, RunSimulationDto runSimulationDto)
    {
        var design = await _designService.GetDesignAsync(principal, roomId);
        if (design == null)
        {
            _logger.LogWarning("Design not found for simulation in room {RoomId}", roomId);
            throw new KeyNotFoundException("Design not found");
        }

        if (!_handlers.TryGetValue(runSimulationDto.Scenario, out var handler))
        {
            _logger.LogWarning("Unsupported simulation scenario {Scenario} for room {RoomId}", runSimulationDto.Scenario, roomId);
            throw new NotSupportedException("Simulation scenario is not supported yet");
        }

        return handler.Simulate(design, runSimulationDto);
    }
}
