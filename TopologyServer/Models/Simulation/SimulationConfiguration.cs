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
}

public class SimulationWorkload
{
    public int? RequestsPerSecond { get; set; }
    public int? ReadPercentage { get; set; }
    public int? WritePercentage { get; set; }
    public double ClientTimeoutMs { get; set; } = 1000;
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

public enum ScheduledSimulationEventType { TrafficChange, ComponentFailure, ComponentRecovery }

public class ScheduledSimulationEvent
{
    public ScheduledSimulationEventType Type { get; set; }
    public long AtMicroseconds { get; set; }
    public string? TargetNodeId { get; set; }
    public string? TargetReplicaId { get; set; }
    public int? RequestsPerSecond { get; set; }
}
