namespace TopologyServer;
public class SimulationConfiguration
{
    public int DurationSeconds { get; set; } = 60;
    public int RandomSeed { get; set; } = 1;
    public int MetricIntervalMs { get; set; } = 1000;
    public SimulationWorkload Workload { get; set; } = new();
    public SimulationBehaviorDefaults Defaults { get; set; } = new();
    public SimulationExecutionLimits Limits { get; set; } = new();
    public List<ScheduledSimulationEvent> ScheduledEvents { get; set; } = [];
    public Dictionary<string, double> EdgeDelayMs { get; set; } = [];
    public SimulationRoutingSettings Routing { get; set; } = new();
    public SimulationCacheSettings Cache { get; set; } = new();
    public SimulationQueueSettings Queue { get; set; } = new();
}

public class SimulationWorkload
{
    public int? RequestsPerSecond { get; set; }
    public int? ReadPercentage { get; set; }
    public int? WritePercentage { get; set; }
    public double ClientTimeoutMs { get; set; } = 1000;
    public int KeySpaceSize { get; set; } = 100;
}
public class SimulationBehaviorDefaults
{
    public double NetworkDelayMs { get; set; } = 1;
    public double ServiceProcessingMs { get; set; } = 5;
    public int ServiceConcurrency { get; set; } = 16;
    public int ServiceQueueCapacity { get; set; } = 100;
    public int DatabasePoolSize { get; set; } = 8;
    public int DatabasePoolQueueCapacity { get; set; } = 100;
    public int DatabaseMaxConnections { get; set; } = 100;
    public double DatabaseReadProcessingMs { get; set; } = 10;
    public double DatabaseWriteProcessingMs { get; set; } = 20;
    public int DatabaseConcurrency { get; set; } = 4;
    public int DatabaseQueueCapacity { get; set; } = 100;
}
public class SimulationExecutionLimits
{
    public const int MaximumDurationSeconds = 3600;
    public const int MaximumRequestsPerSecond = 100_000;
    public const int MaximumProcessedEvents = 5_000_000;
    public const int MaximumPendingEvents = 500_000;
    public const int MaximumGeneratedRequests = 100_000;
    public const int MaximumRetainedRecords = 100_000;

    public int MaxProcessedEvents { get; set; } = MaximumProcessedEvents;
    public int MaxPendingEvents { get; set; } = MaximumPendingEvents;
    public int MaxGeneratedRequests { get; set; } = MaximumGeneratedRequests;
    public int MaxRetainedRecords { get; set; } = MaximumRetainedRecords;
}

public enum ScheduledSimulationEventType { TrafficChange, ComponentFailure, ComponentRecovery, TrafficRamp }

public class ScheduledSimulationEvent
{
    public ScheduledSimulationEventType Type { get; set; }
    public long AtMicroseconds { get; set; }
    public string? TargetNodeId { get; set; }
    public string? TargetReplicaId { get; set; }
    public int? RequestsPerSecond { get; set; }
    public long? EndAtMicroseconds { get; set; }
    public int? EndRequestsPerSecond { get; set; }
}

public enum SimulationRoutingPolicy { RoundRobin, LeastConnections }

public class SimulationRoutingSettings
{
    public SimulationRoutingPolicy Policy { get; set; } = SimulationRoutingPolicy.RoundRobin;
    public int HealthCheckIntervalMs { get; set; } = 100;
    public int DetectionDelayMs { get; set; } = 100;
}

public class SimulationCacheSettings
{
    public int Capacity { get; set; } = 100;
    public int TtlMs { get; set; } = 30000;
    public double LookupMs { get; set; } = 1;
}

public class SimulationQueueSettings
{
    public int Capacity { get; set; } = 100;
    public int WorkerConcurrency { get; set; } = 2;
    public double WorkerProcessingMs { get; set; } = 10;
    public int MaxDeliveries { get; set; } = 3;
    public int RetryDelayMs { get; set; } = 100;
    public int AcknowledgementTimeoutMs { get; set; } = 30000;
}
