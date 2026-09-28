namespace TopologyServer;

public enum SimulationExecutionStatus { Completed, Incomplete }
public enum SimulationStopReason
{
    DurationReached, Cancelled, ProcessedEventLimit, PendingEventLimit,
    GeneratedRequestLimit, RetainedRecordLimit
}
public class SimulationExecutionResult
{
    public required string EngineVersion { get; set; }
    public required SimulationConfiguration ResolvedConfiguration { get; set; }
    public required SimulationExecutionStatus Status { get; set; }
    public required SimulationStopReason StopReason { get; set; }
    public long ElapsedMicroseconds { get; set; }
    public long ProcessedEvents { get; set; }
    public List<string> Assumptions { get; set; } = [];
    public List<string> UnusedNodeIds { get; set; } = [];
    public SimulationMeasurements Summary { get; set; } = new();
    public List<SimulationTimelineSample> Timeline { get; set; } = [];
    public List<SimulationStateTransition> StateTransitions { get; set; } = [];
}

public class SimulationMeasurements
{
    public SimulationRequestMetrics Requests { get; set; } = new();
    public List<SimulationResourceMetrics> Resources { get; set; } = [];
    public List<SimulationEdgeMetrics> Edges { get; set; } = [];
}

public class SimulationRequestMetrics
{
    public long Generated { get; set; }
    public long Succeeded { get; set; }
    public long ExplicitlyFailed { get; set; }
    public long TimedOut { get; set; }
    public long InFlight { get; set; }
    public long DatabaseReadsCompleted { get; set; }
    public long DatabaseWritesCompleted { get; set; }
    public double? SuccessfulRequestsPerSecond { get; set; }
    public double? TerminalErrorRatio { get; set; }
    public SimulationLatencyMetrics SuccessfulLatency { get; set; } = new();
    public Dictionary<string, long> FailureCountsByCause { get; set; } = [];
}

public class SimulationLatencyMetrics
{
    public double? P50Ms { get; set; }
    public double? P95Ms { get; set; }
    public double? P99Ms { get; set; }
}

public enum SimulationResourceKind { ServiceExecution, DatabasePool, DatabaseConnections, DatabaseExecution }

public class SimulationResourceMetrics
{
    public required string NodeId { get; set; }
    public string? ReplicaId { get; set; }
    public SimulationResourceKind Kind { get; set; }
    public int Capacity { get; set; }
    public int Active { get; set; }
    public int QueueDepth { get; set; }
    public int QueueCapacity { get; set; }
    public long Rejected { get; set; }
    public double? UtilizationRatio { get; set; }
    public long UnavailableMicroseconds { get; set; }
}

public class SimulationEdgeMetrics
{
    public required string EdgeId { get; set; }
    public long Calls { get; set; }
    public long Responses { get; set; }
}

public class SimulationTimelineSample
{
    public long AtMicroseconds { get; set; }
    public long IntervalStartMicroseconds { get; set; }
    public SimulationMeasurements Measurements { get; set; } = new();
}

public class SimulationStateTransition
{
    public long AtMicroseconds { get; set; }
    public required string NodeId { get; set; }
    public string? ReplicaId { get; set; }
    public SimulationNodeStatus PreviousStatus { get; set; }
    public SimulationNodeStatus Status { get; set; }
    public required string Reason { get; set; }
}
