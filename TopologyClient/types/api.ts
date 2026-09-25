import type {
  AccessType,
  ComponentCategory,
  ComponentType,
  SimulationRiskLevel,
  SimulationScenario,
  ValidationSeverity
} from "./enums";

export type User = {
  id?: string;
  auth0UserId: string;
  userName?: string | null;
  email?: string | null;
  createdAt: string;
};

export type Room = {
  id?: string;
  ownerUserId: string;
  memberUserIds: string[];
  type: AccessType;
  roomKey?: number | null;
  createdAt: string;
};

export type CreateRoomDto = {
  type: AccessType;
  roomKey?: number | null;
};

export type JoinRoomDto = {
  id: string;
  roomKey?: number | null;
};

export type DeleteRoomDto = {
  id: string;
  roomKey?: number | null;
};

export type ComponentPropertyDefinition = {
  key: string;
  label: string;
  inputType?: string | null;
  defaultValue?: unknown;
  options: string[];
  min?: number | null;
  max?: number | null;
  required: boolean;
};

export type ComponentDefinition = {
  id?: string | null;
  type: ComponentType;
  label?: string | null;
  category: ComponentCategory;
  description: string;
  properties: ComponentPropertyDefinition[];
};

export type NodeProperties = Record<string, unknown>;

export type DesignNode = {
  id: string;
  type: ComponentType;
  label: string;
  x: number;
  y: number;
  properties: NodeProperties;
};

export type DesignEdge = {
  id: string;
  sourceNodeId: string;
  targetNodeId: string;
  label?: string | null;
};

export type Design = {
  id?: string | null;
  roomId: string;
  updatedByUserId: string;
  nodes: DesignNode[];
  edges: DesignEdge[];
  revision: number;
  updatedAt: string;
};

export type SaveDesignDto = {
  nodes: DesignNode[];
  edges: DesignEdge[];
};

export type DesignValidationIssue = {
  severity: ValidationSeverity;
  message: string;
  nodeId?: string | null;
  edgeId?: string | null;
  propertyKey?: string | null;
};

export type DesignValidationResult = {
  isValid: boolean;
  issues: DesignValidationIssue[];
};

export type ConnectionRule = {
  id?: string | null;
  sourceType: ComponentType;
  targetType: ComponentType;
  isAllowed: boolean;
  severity: ValidationSeverity;
  message: string;
};

export type RunSimulationDto = {
  scenario: SimulationScenario;
  trafficPerSecond?: number | null;
  readPercentage?: number | null;
  writePercentage?: number | null;
  hasCriticalWrites?: boolean | null;
  failedNodeIds: string[];
};

export type SimulationNodeStatus = "Online" | "Degraded" | "Saturated" | "Offline";

export type SimulationEdgeStatus = "Healthy" | "Degraded" | "Saturated" | "Broken";

export type SimulationNodeResult = {
  nodeId: string;
  status: SimulationNodeStatus;
  message: string;
  loadPercentage?: number | null;
  metrics: Record<string, unknown>;
};

export type SimulationEdgeResult = {
  edgeId: string;
  sourceNodeId: string;
  targetNodeId: string;
  status: SimulationEdgeStatus;
  message: string;
  trafficPerSecond?: number | null;
};

export type SimulationResult = {
  scenario: SimulationScenario;
  riskLevel: SimulationRiskLevel;
  riskScore: number;
  findings: string[];
  impact: string[];
  recommendations: string[];
  affectedNodeIds: string[];
  nodeResults: SimulationNodeResult[];
  edgeResults: SimulationEdgeResult[];
};

