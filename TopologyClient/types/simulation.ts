import type { SimulationNodeStatus, SimulationNodeResult } from "./api";
export type SimulationConfiguration = {
  durationSeconds?: number;
  randomSeed?: number;
  metricIntervalMs?: number;
  workload?: SimulationWorkload;
  defaults?: SimulationBehaviorDefaults;
  limits?: SimulationExecutionLimits;
  scheduledEvents?: ScheduledSimulationEvent[];
  edgeDelayMs?: Record<string, number>;
  routing?: { policy?: "RoundRobin" | "LeastConnections"; healthCheckIntervalMs?: number; detectionDelayMs?: number };
  cache?: { capacity?: number; ttlMs?: number; lookupMs?: number };
  queue?: { capacity?: number; workerConcurrency?: number; workerProcessingMs?: number; maxDeliveries?: number; retryDelayMs?: number; acknowledgementTimeoutMs?: number };
  gateway?: { processingMs?: number; concurrency?: number; queueCapacity?: number; rateLimitPerSecond?: number };
  externalApi?: { latencyMs?: number; concurrency?: number; queueCapacity?: number; rateLimitPerMinute?: number; reliabilityPercentage?: number };
  retries?: { maxAttempts?: number; baseDelayMs?: number; maxDelayMs?: number; jitterPercentage?: number; retryWrites?: boolean };
  circuitBreaker?: { enabled?: boolean; failureThreshold?: number; openDurationMs?: number };
  databaseReplication?: { readPreference?: "Primary" | "PreferReadReplica"; lagMs?: number; detectionDelayMs?: number; promotionDelayMs?: number; failoverEnabled?: boolean };
};

export type SimulationWorkload = {
  requestsPerSecond?: number | null;
  readPercentage?: number | null;
  writePercentage?: number | null;
  clientTimeoutMs?: number;
  keySpaceSize?: number;
};

export type SimulationBehaviorDefaults = {
  networkDelayMs?: number;
  serviceProcessingMs?: number;
  serviceConcurrency?: number;
  serviceQueueCapacity?: number;
  databasePoolSize?: number;
  databasePoolQueueCapacity?: number;
  databaseMaxConnections?: number;
  databaseReadProcessingMs?: number;
  databaseWriteProcessingMs?: number;
  databaseConcurrency?: number;
  databaseQueueCapacity?: number;
};

export type SimulationExecutionLimits = {
  maxProcessedEvents?: number;
  maxPendingEvents?: number;
  maxGeneratedRequests?: number;
  maxRetainedRecords?: number;
};

export type ScheduledSimulationEvent = {
  type: "TrafficChange" | "ComponentFailure" | "ComponentRecovery" | "TrafficRamp";
  atMicroseconds: number;
  targetNodeId?: string | null;
  targetReplicaId?: string | null;
  requestsPerSecond?: number | null;
  endAtMicroseconds?: number | null;
  endRequestsPerSecond?: number | null;
};
export type ResolvedSimulationConfiguration = Required<Omit<SimulationConfiguration,
  "workload" | "defaults" | "limits">> & {
  workload: {
    requestsPerSecond: number;
    readPercentage: number;
    writePercentage: number;
    clientTimeoutMs: number;
    keySpaceSize: number;
  };
  defaults: Required<SimulationBehaviorDefaults>;
  limits: Required<SimulationExecutionLimits>;
};

export type SimulationExecutionResult = {
  engineVersion: string;
  resolvedConfiguration: ResolvedSimulationConfiguration;
  status: "Completed" | "Incomplete";
  stopReason: "DurationReached" | "Cancelled" | "ProcessedEventLimit" |
    "PendingEventLimit" | "GeneratedRequestLimit" | "RetainedRecordLimit";
  elapsedMicroseconds: number;
  processedEvents: number;
  assumptions: string[];
  unusedNodeIds: string[];
  summary: SimulationMeasurements;
  timeline: SimulationTimelineSample[];
  stateTransitions: SimulationStateTransition[];
};

export type SimulationMeasurements = {
  requests: SimulationRequestMetrics;
  resources: SimulationResourceMetrics[];
  edges: SimulationEdgeMetrics[];
  nodes: SimulationNodeResult[];
  cache: { hits: number; misses: number; evictions: number; invalidations: number; entries: number };
  jobs: { accepted: number; completed: number; deadLettered: number; redeliveries: number; rejected: number; lost: number; waiting: number; active: number };
  endpoints: { nodeId: string; received: number; completed: number; rateLimited: number; saturated: number; failed: number }[];
  retries: { attempts: number; retryAttempts: number; circuitRejected: number; circuitOpened: number; failuresByCause: Record<string, number>;
    circuits: { scope: string; state: "Closed" | "Open" | "HalfOpen"; consecutiveFailures: number }[] };
  replication: { primaryReplicaId: string | null; promotions: number; staleReads: number; rolledBackKeys: number;
    replicas: { replicaId: string; role: "Primary" | "Standby" | "ReadReplica"; isAvailable: boolean; appliedVersion: number }[] };
};

export type SimulationRequestMetrics = {
  generated: number;
  succeeded: number;
  explicitlyFailed: number;
  timedOut: number;
  inFlight: number;
  databaseReadsCompleted: number;
  databaseWritesCompleted: number;
  successfulRequestsPerSecond: number | null;
  terminalErrorRatio: number | null;
  successfulLatency: SimulationLatencyMetrics;
  failureCountsByCause: Record<string, number>;
};

export type SimulationLatencyMetrics = {
  p50Ms: number | null;
  p95Ms: number | null;
  p99Ms: number | null;
};

export type SimulationResourceMetrics = {
  nodeId: string;
  replicaId: string | null;
  kind: "ServiceExecution" | "DatabasePool" | "DatabaseConnections" | "DatabaseExecution" | "WorkerExecution" | "GatewayExecution" | "ExternalApiExecution";
  capacity: number;
  active: number;
  queueDepth: number;
  queueCapacity: number;
  rejected: number;
  utilizationRatio: number | null;
  unavailableMicroseconds: number;
  isAvailable: boolean;
};

export type SimulationEdgeMetrics = {
  edgeId: string;
  calls: number;
  responses: number;
};

export type SimulationTimelineSample = {
  atMicroseconds: number;
  intervalStartMicroseconds: number;
  measurements: SimulationMeasurements;
};

export type SimulationStateTransition = {
  atMicroseconds: number;
  nodeId: string;
  replicaId: string | null;
  previousStatus: SimulationNodeStatus;
  status: SimulationNodeStatus;
  reason: string;
};
