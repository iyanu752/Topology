namespace TopologyServer;

public enum SimulationScenario
{
    NormalTraffic,
    HighTraffic,
    DatabaseFailure,
    CacheFailure,
    QueueBacklog,
    ExternalApiFailure,
    HighLatency,
    ReadHeavyWorkload,
    WriteHeavyWorkload,
    SuddenUserGrowth
}
