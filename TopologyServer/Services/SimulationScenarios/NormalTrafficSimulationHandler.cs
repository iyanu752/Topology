namespace TopologyServer;

public class NormalTrafficSimulationHandler : ISimulationScenarioHandler
{
    public SimulationScenario Scenario => SimulationScenario.NormalTraffic;

    public SimulationResult Simulate(Design design, RunSimulationDto runSimulationDto)
    {
        var dto = new RunSimulationDto
        {
            Scenario = SimulationScenario.NormalTraffic,
            TrafficPerSecond = runSimulationDto.TrafficPerSecond ?? 100,
            ReadPercentage = runSimulationDto.ReadPercentage,
            WritePercentage = runSimulationDto.WritePercentage,
            HasCriticalWrites = runSimulationDto.HasCriticalWrites,
            FailedNodeIds = runSimulationDto.FailedNodeIds
        };

        return TrafficSimulationEvaluator.Evaluate(design, dto, isHighTrafficScenario: false);
    }
}
