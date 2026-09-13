export type AccessType = "Public" | "Private";

export type ComponentType =
  | "Database"
  | "Service"
  | "Cache"
  | "Client"
  | "ApiGateway"
  | "Queue"
  | "LoadBalancer"
  | "ExternalApi";

export type ComponentCategory = "Data" | "Compute" | "Network";

export type ValidationSeverity = "Info" | "Warning" | "Error";

export type SimulationScenario =
  | "NormalTraffic"
  | "HighTraffic"
  | "DatabaseFailure"
  | "CacheFailure"
  | "QueueBacklog"
  | "ExternalApiFailure"
  | "HighLatency"
  | "ReadHeavyWorkload"
  | "WriteHeavyWorkload"
  | "SuddenUserGrowth";

export type SimulationRiskLevel = "Low" | "Medium" | "High" | "Critical";
