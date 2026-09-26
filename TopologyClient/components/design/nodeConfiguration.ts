import type { ComponentLibraryItem } from "@/components/component-library/componentLibraryItems";

export type NodeConfigurationValue = string | number | boolean | null;

export type NodeConfiguration = Record<string, NodeConfigurationValue>;

const defaultConfigurationByComponentId: Record<string, NodeConfiguration> = {
  client: {
    expectedUsers: 1000,
    requestsPerSecond: 100
  },
  "api-gateway": {
    rateLimitPerSecond: 1000,
    authRequired: true,
    timeoutMs: 30000,
    cachingEnabled: false
  },
  "load-balancer": {
    routingStrategy: "round-robin",
    healthChecksEnabled: true,
    targetCount: 1
  },
  service: {
    replicas: 1,
    cpuCores: 1,
    memoryGb: 1,
    maxRequestsPerSecond: 100,
    autoscalingEnabled: false
  },
  database: {
    databaseType: "Postgres",
    storageGb: 20,
    replicas: 1,
    readReplicas: 0,
    backupEnabled: false,
    failoverEnabled: false,
    maxConnections: 100
  },
  cache: {
    cacheType: "Redis",
    memoryGb: 1,
    ttlSeconds: 300,
    evictionPolicy: "lru"
  },
  queue: {
    throughputPerSecond: 500,
    retentionHours: 24,
    deadLetterQueueEnabled: false
  },
  cdn: {
    cacheTtlSeconds: 3600,
    edgeLocations: 5,
    originShieldEnabled: false
  },
  "object-storage": {
    storageGb: 100,
    versioningEnabled: false,
    backupEnabled: false
  },
  "external-api": {
    averageLatencyMs: 250,
    rateLimitPerMinute: 60,
    reliabilityPercentage: 99
  }
};

export function createDefaultNodeConfiguration(component: ComponentLibraryItem): NodeConfiguration {
  return createNodeConfiguration(component);
}

export function createNodeConfiguration(component: ComponentLibraryItem, configuration?: unknown): NodeConfiguration {
  const existingConfiguration = isNodeConfiguration(configuration) ? configuration : {};

  return {
    ...createCommonNodeConfiguration(component),
    ...(defaultConfigurationByComponentId[component.id] ?? {}),
    ...existingConfiguration
  };
}

export function cloneNodeConfiguration(configuration: NodeConfiguration): NodeConfiguration {
  return { ...configuration };
}

export function isNodeConfiguration(value: unknown): value is NodeConfiguration {
  if (typeof value !== "object" || value === null || Array.isArray(value)) {
    return false;
  }

  return Object.values(value).every(
    (propertyValue) =>
      propertyValue === null ||
      typeof propertyValue === "string" ||
      typeof propertyValue === "number" ||
      typeof propertyValue === "boolean"
  );
}

function createCommonNodeConfiguration(component: ComponentLibraryItem): NodeConfiguration {
  return {
    displayName: component.name,
    region: "us-east-1",
    notes: null
  };
}