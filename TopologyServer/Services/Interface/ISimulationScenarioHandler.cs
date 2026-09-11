namespace TopologyServer;

public interface ISimulationScenarioHandler
{
    SimulationScenario Scenario { get; }
    SimulationResult Simulate(Design design, RunSimulationDto runSimulationDto);
}
