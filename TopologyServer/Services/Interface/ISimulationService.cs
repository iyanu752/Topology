using System;
using System.Security.Claims;

namespace TopologyServer;

public interface ISimulationService
{
    Task<SimulationResult> RunSimulationAsync(ClaimsPrincipal principal, string roomId, RunSimulationDto runSimulationDto);

}
