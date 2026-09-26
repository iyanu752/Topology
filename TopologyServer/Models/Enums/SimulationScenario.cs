namespace TopologyServer;

public enum SimulationScenario
{
    NormalTraffic,
    HighTraffic,
    DatabaseFailure,
    ServerFailure,
    CacheMissStorm,
    QueueBacklog,
    ApiGatewayBottleneck,
    LoadBalancerRoutingFailure,
    RegionalOutage,
    CacheFailure,
    ExternalApiFailure,
    HighLatency,
    ReadHeavyWorkload,
    WriteHeavyWorkload,
    SuddenUserGrowth
}