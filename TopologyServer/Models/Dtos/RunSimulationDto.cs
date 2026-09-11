using System;

namespace TopologyServer;

public class RunSimulationDto
{
    public SimulationScenario Scenario { get; set; }
    public int? TrafficPerSecond { get; set; }
    public int? ReadPercentage { get; set; }
    public int? WritePercentage { get; set; }
    public bool? HasCriticalWrites { get; set; }
    public List<string> FailedNodeIds { get; set; } = [];
}
