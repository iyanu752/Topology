namespace TopologyServer;

public class RunSimulationDto
{
    public SimulationScenario Scenario { get; set; }
    public int? TrafficPerSecond { get; set; }
    public int? ReadPercentage { get; set; }
    public int? WritePercentage { get; set; }
    public bool? HasCriticalWrites { get; set; }
    public List<string> FailedNodeIds { get; set; } = [];
    public string? Region { get; set; }
    public int? DurationSeconds { get; set; }
    public int? CacheHitRatePercentage { get; set; }
    public int? PayloadSizeKb { get; set; }
    public int? FailedTargetCount { get; set; }
}