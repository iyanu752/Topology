import type { SimulationNodeStatus } from "./api";
export type SimulationConfiguration = {
  durationSeconds?: number;
  randomSeed?: number;
  metricIntervalMs?: number;
  workload?: SimulationWorkload;
  defaults?: SimulationBehaviorDefaults;
  limits?: SimulationExecutionLimits;
  scheduledEvents?: ScheduledSimulationEvent[];
  edgeDelayMs?: Record<string, number>;
};

export type SimulationWorkload = {
  requestsPerSecond?: number | null;
  readPercentage?: number | null;
  writePercentage?: number | null;
  clientTimeoutMs?: number;
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
  type: "TrafficChange" | "ComponentFailure" | "ComponentRecovery";
  atMicroseconds: number;
  targetNodeId?: string | null;
  targetReplicaId?: string | null;
  requestsPerSecond?: number | null;
};
export type ResolvedSimulationConfiguration = Required<Omit<SimulationConfiguration,
  "workload" | "defaults" | "limits">> & {
  workload: {
    requestsPerSecond: number;
    readPercentage: number;
    writePercentage: number;
    clientTimeoutMs: number;
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
  kind: "ServiceExecution" | "DatabasePool" | "DatabaseConnections" | "DatabaseExecution";
  capacity: number;
  active: number;
  queueDepth: number;
  queueCapacity: number;
  rejected: number;
  utilizationRatio: number | null;
  unavailableMicroseconds: number;
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
