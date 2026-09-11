namespace TopologyServer;

public class HighTrafficSimulationHandler : ISimulationScenarioHandler
{
    public SimulationScenario Scenario => SimulationScenario.HighTraffic;

    public SimulationResult Simulate(Design design, RunSimulationDto runSimulationDto)
    {
        var dto = new RunSimulationDto
        {
            Scenario = SimulationScenario.HighTraffic,
            TrafficPerSecond = runSimulationDto.TrafficPerSecond ?? 2000,
            ReadPercentage = runSimulationDto.ReadPercentage,
            WritePercentage = runSimulationDto.WritePercentage,
            HasCriticalWrites = runSimulationDto.HasCriticalWrites,
            FailedNodeIds = runSimulationDto.FailedNodeIds
        };

        return TrafficSimulationEvaluator.Evaluate(design, dto, isHighTrafficScenario: true);
    }
}
