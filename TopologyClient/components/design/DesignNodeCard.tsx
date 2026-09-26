import type { PointerEvent } from "react";
import { ComponentLibraryIcon } from "@/components/component-library/ComponentLibraryItem";
import type { ComponentLibraryItem } from "@/components/component-library/componentLibraryItems";
import type { SimulationNodeResult } from "@/types";
import type { NodeConfiguration } from "./nodeConfiguration";

export const designNodeSize = {
  width: 244,
  height: 136
} as const;

export type NodeStatus = "Online" | "Degraded" | "Saturated" | "Offline";

export type DesignNode = {
  id: string;
  component: ComponentLibraryItem;
  configuration: NodeConfiguration;
  status: NodeStatus;
  x: number;
  y: number;
};

export type DesignEdge = {
  id: string;
  fromNodeId: string;
  toNodeId: string;
};

type DesignNodeCardProps = {
  isSelected: boolean;
  node: DesignNode;
  simulationResult?: SimulationNodeResult;
  onConnectionStart: (event: PointerEvent<HTMLButtonElement>, node: DesignNode) => void;
  onContextMenu: (event: React.MouseEvent<HTMLDivElement>, node: DesignNode) => void;
  onPointerDown: (event: PointerEvent<HTMLDivElement>, node: DesignNode) => void;
};

type NodeCardProfile = {
  caption: string;
  metricLabel: string;
  metricValue: string;
  bars: number[];
};

type NodeConfigurationSummary = {
  caption?: string;
  metricLabel?: string;
  metricValue?: string;
};

type StatusStyle = {
  badgeClassName: string;
  barClassName: string;
  borderClassName: string;
  metricClassName: string;
};

export function DesignNodeCard({ isSelected, node, simulationResult, onConnectionStart, onContextMenu, onPointerDown }: DesignNodeCardProps) {
  const profile = getNodeCardProfile(node.component);
  const configurationSummary = getNodeConfigurationSummary(node);
  const status = simulationResult?.status ?? node.status;
  const statusStyle = getStatusStyle(status);
  const bars = getStatusBars(profile.bars, status, simulationResult?.loadPercentage);
  const title = getNodeTitle(node);
  const caption = simulationResult?.message ?? configurationSummary.caption ?? profile.caption;
  const metricLabel = configurationSummary.metricLabel ?? profile.metricLabel;
  const metricValue = configurationSummary.metricValue ?? profile.metricValue;

  return (
    <div
      data-canvas-interactive="true"
      data-node-status={status}
      role="button"
      tabIndex={0}
      onContextMenu={(event) => onContextMenu(event, node)}
      onPointerDown={(event) => onPointerDown(event, node)}
      className={`absolute touch-none cursor-grab select-none overflow-hidden rounded-lg border bg-zinc-950 shadow-2xl shadow-black/40 transition active:cursor-grabbing ${statusStyle.borderClassName} ${isSelected ? "ring-2 ring-emerald-300/80" : ""}`}
      style={{ left: node.x, top: node.y, width: designNodeSize.width, height: designNodeSize.height }}
      aria-label={`Move ${title}`}
    >
      <div className="flex h-full flex-col p-4">
        <div className="flex items-start justify-between gap-3">
          <div className="flex min-w-0 items-center gap-2">
            <span className="flex h-7 w-7 shrink-0 items-center justify-center rounded-md border border-zinc-800 bg-zinc-900">
              <ComponentLibraryIcon item={node.component} size="sidebar" />
            </span>
            <div className="min-w-0">
              <div className="truncate text-sm font-semibold uppercase text-zinc-100">{title}</div>
              <div className="mt-0.5 truncate text-[11px] uppercase text-zinc-500">{caption}</div>
            </div>
          </div>

          <span className={`shrink-0 rounded border px-2 py-1 text-[11px] font-semibold uppercase ${statusStyle.badgeClassName}`}>
            {status}
          </span>
        </div>

        <div className="mt-4 grid flex-1 grid-cols-[1fr_70px] gap-4">
          <div className="space-y-2.5 self-end">
            {bars.map((value, index) => (
              <div key={`${node.id}-bar-${index}`} className="h-2.5 overflow-hidden rounded-sm border border-zinc-800 bg-zinc-900">
                <div className={`h-full rounded-sm ${statusStyle.barClassName}`} style={{ width: `${value}%` }} />
              </div>
            ))}
          </div>

          <div className="flex min-w-0 flex-col justify-end border-l border-zinc-800 pl-4">
            <span className="truncate text-[11px] font-semibold uppercase text-zinc-500">{getMetricLabel(metricLabel, status, simulationResult)}</span>
            <span className={`mt-2 truncate text-sm font-semibold ${statusStyle.metricClassName}`}>{getMetricValue(metricValue, status, simulationResult)}</span>
          </div>
        </div>
      </div>

      <button
        type="button"
        aria-label={`Connect from ${title}`}
        data-connect-node-id={node.id}
        onPointerDown={(event) => onConnectionStart(event, node)}
        className="absolute -right-2 top-1/2 h-5 w-5 -translate-y-1/2 cursor-crosshair rounded-full border-2 border-zinc-950 bg-emerald-300 shadow-lg shadow-black/30 transition hover:scale-110 hover:bg-emerald-200 focus:outline-none focus:ring-2 focus:ring-emerald-200"
      />
    </div>
  );
}

function getStatusStyle(status: NodeStatus): StatusStyle {
  switch (status) {
    case "Online":
      return {
        badgeClassName: "border-emerald-400/30 bg-emerald-400/10 text-emerald-200",
        barClassName: "bg-emerald-400/60",
        borderClassName: "border-zinc-700/80 hover:border-emerald-300/80",
        metricClassName: "text-emerald-200"
      };
    case "Degraded":
      return {
        badgeClassName: "border-amber-300/40 bg-amber-300/10 text-amber-200",
        barClassName: "bg-amber-300/70",
        borderClassName: "border-amber-300/40 hover:border-amber-200/80",
        metricClassName: "text-amber-200"
      };
    case "Saturated":
      return {
        badgeClassName: "border-rose-300/40 bg-rose-300/10 text-rose-200",
        barClassName: "bg-rose-300/80",
        borderClassName: "border-rose-300/45 hover:border-rose-200/80",
        metricClassName: "text-rose-200"
      };
    case "Offline":
      return {
        badgeClassName: "border-zinc-600 bg-zinc-800/80 text-zinc-400",
        barClassName: "bg-zinc-700",
        borderClassName: "border-zinc-800 opacity-65 hover:border-zinc-700",
        metricClassName: "text-zinc-500"
      };
  }
}

function getStatusBars(bars: number[], status: NodeStatus, loadPercentage?: number | null) {
  if (typeof loadPercentage === "number") {
    const load = Math.min(Math.max(loadPercentage, 0), 100);
    return [load, Math.max(6, Math.round(load * 0.82)), Math.max(6, Math.round(load * 0.64))];
  }

  switch (status) {
    case "Online":
      return bars;
    case "Degraded":
      return bars.map((value) => Math.min(84, value + 18));
    case "Saturated":
      return bars.map(() => 96);
    case "Offline":
      return bars.map(() => 6);
  }
}

function getMetricLabel(label: string, status: NodeStatus, simulationResult?: SimulationNodeResult) {
  if (simulationResult?.loadPercentage !== undefined && simulationResult.loadPercentage !== null) {
    return "Load";
  }

  return status === "Offline" ? "Status" : label;
}

function getMetricValue(value: string, status: NodeStatus, simulationResult?: SimulationNodeResult) {
  if (simulationResult?.loadPercentage !== undefined && simulationResult.loadPercentage !== null) {
    return `${simulationResult.loadPercentage}%`;
  }

  switch (status) {
    case "Online":
      return value;
    case "Degraded":
      return "Slow";
    case "Saturated":
      return "99%";
    case "Offline":
      return "Down";
  }
}

function getNodeTitle(node: DesignNode) {
  const displayName = getStringConfig(node.configuration, "displayName");
  return displayName || node.component.name;
}

function getNodeConfigurationSummary(node: DesignNode): NodeConfigurationSummary {
  const { component, configuration } = node;

  switch (component.id) {
    case "client": {
      const expectedUsers = getNumberConfig(configuration, "expectedUsers");
      const requestsPerSecond = getNumberConfig(configuration, "requestsPerSecond");

      return {
        caption: expectedUsers ? `${formatNumber(expectedUsers)} users` : undefined,
        metricLabel: "RPS",
        metricValue: formatRate(requestsPerSecond)
      };
    }
    case "api-gateway": {
      const authRequired = getBooleanConfig(configuration, "authRequired");
      const rateLimit = getNumberConfig(configuration, "rateLimitPerSecond");

      return {
        caption: authRequired === false ? "Auth off" : "Auth on",
        metricLabel: "Limit",
        metricValue: formatRate(rateLimit)
      };
    }
    case "load-balancer": {
      const strategy = getStringConfig(configuration, "routingStrategy");
      const targetCount = getNumberConfig(configuration, "targetCount");

      return {
        caption: strategy ? formatLabel(strategy) : undefined,
        metricLabel: "Targets",
        metricValue: targetCount ? formatNumber(targetCount) : undefined
      };
    }
    case "service": {
      const replicas = getNumberConfig(configuration, "replicas");
      const cpuCores = getNumberConfig(configuration, "cpuCores");
      const memoryGb = getNumberConfig(configuration, "memoryGb");
      const maxRequestsPerSecond = getNumberConfig(configuration, "maxRequestsPerSecond");

      return {
        caption: `${formatCount(replicas, "inst")} / ${formatCount(cpuCores, "CPU")} / ${formatSize(memoryGb, "GB")}`,
        metricLabel: "RPS",
        metricValue: formatRate(maxRequestsPerSecond)
      };
    }
    case "database": {
      const databaseType = getStringConfig(configuration, "databaseType");
      const storageGb = getNumberConfig(configuration, "storageGb");
      const replicas = getNumberConfig(configuration, "replicas");
      const readReplicas = getNumberConfig(configuration, "readReplicas") ?? 0;

      return {
        caption: `${databaseType || "Database"} / ${formatSize(storageGb, "GB")}`,
        metricLabel: "Replicas",
        metricValue: formatNumber((replicas ?? 1) + readReplicas)
      };
    }
    case "cache": {
      const cacheType = getStringConfig(configuration, "cacheType");
      const memoryGb = getNumberConfig(configuration, "memoryGb");
      const ttlSeconds = getNumberConfig(configuration, "ttlSeconds");

      return {
        caption: `${cacheType || "Cache"} / ${formatSize(memoryGb, "GB")}`,
        metricLabel: "TTL",
        metricValue: ttlSeconds ? `${formatNumber(ttlSeconds)}s` : undefined
      };
    }
    case "queue": {
      const throughputPerSecond = getNumberConfig(configuration, "throughputPerSecond");
      const deadLetterQueueEnabled = getBooleanConfig(configuration, "deadLetterQueueEnabled");

      return {
        caption: deadLetterQueueEnabled ? "DLQ enabled" : "No DLQ",
        metricLabel: "RPS",
        metricValue: formatRate(throughputPerSecond)
      };
    }
    case "cdn": {
      const edgeLocations = getNumberConfig(configuration, "edgeLocations");
      const cacheTtlSeconds = getNumberConfig(configuration, "cacheTtlSeconds");

      return {
        caption: edgeLocations ? `${formatNumber(edgeLocations)} edge locations` : undefined,
        metricLabel: "TTL",
        metricValue: cacheTtlSeconds ? `${formatNumber(cacheTtlSeconds)}s` : undefined
      };
    }
    case "object-storage": {
      const storageGb = getNumberConfig(configuration, "storageGb");
      const versioningEnabled = getBooleanConfig(configuration, "versioningEnabled");

      return {
        caption: versioningEnabled ? "Versioned files" : "File store",
        metricLabel: "Storage",
        metricValue: formatSize(storageGb, "GB")
      };
    }
    case "external-api": {
      const averageLatencyMs = getNumberConfig(configuration, "averageLatencyMs");
      const rateLimitPerMinute = getNumberConfig(configuration, "rateLimitPerMinute");

      return {
        caption: averageLatencyMs ? `${formatNumber(averageLatencyMs)}ms avg` : undefined,
        metricLabel: "Limit",
        metricValue: rateLimitPerMinute ? `${formatNumber(rateLimitPerMinute)}/m` : undefined
      };
    }
    default:
      return {};
  }
}

function getNodeCardProfile(component: ComponentLibraryItem): NodeCardProfile {
  switch (component.id) {
    case "client":
      return { caption: "User entry", metricLabel: "Sessions", metricValue: "1.2k", bars: [52, 38, 64] };
    case "api-gateway":
      return { caption: "API edge", metricLabel: "Routes", metricValue: "12", bars: [72, 46, 58] };
    case "load-balancer":
      return { caption: "Routing", metricLabel: "Targets", metricValue: "3", bars: [35, 74, 42] };
    case "service":
      return { caption: "Your code", metricLabel: "CPU", metricValue: "24%", bars: [54, 54, 54] };
    case "database":
      return { caption: "Your data", metricLabel: "Rows", metricValue: "12,480", bars: [28, 58, 58] };
    case "cache":
      return { caption: "Fast reads", metricLabel: "Hit rate", metricValue: "86%", bars: [82, 68, 56] };
    case "queue":
      return { caption: "Async work", metricLabel: "Depth", metricValue: "42", bars: [38, 61, 48] };
    case "cdn":
      return { caption: "Static edge", metricLabel: "Hit rate", metricValue: "91%", bars: [88, 72, 64] };
    case "object-storage":
      return { caption: "Files", metricLabel: "Stored", metricValue: "20 GB", bars: [45, 32, 51] };
    case "external-api":
      return { caption: "Vendor", metricLabel: "Limit", metricValue: "60/m", bars: [44, 28, 36] };
    default:
      return { caption: "Component", metricLabel: "Load", metricValue: "0%", bars: [40, 40, 40] };
  }
}

function getStringConfig(configuration: NodeConfiguration, key: string) {
  const value = configuration[key];
  return typeof value === "string" && value.trim() ? value : null;
}

function getNumberConfig(configuration: NodeConfiguration, key: string) {
  const value = configuration[key];
  return typeof value === "number" && Number.isFinite(value) ? value : null;
}

function getBooleanConfig(configuration: NodeConfiguration, key: string) {
  const value = configuration[key];
  return typeof value === "boolean" ? value : null;
}

function formatRate(value: number | null | undefined) {
  return typeof value === "number" ? `${formatNumber(value)}/s` : undefined;
}

function formatSize(value: number | null | undefined, unit: string) {
  return typeof value === "number" ? `${formatNumber(value)} ${unit}` : "-";
}

function formatCount(value: number | null | undefined, unit: string) {
  return typeof value === "number" ? `${formatNumber(value)} ${unit}` : `- ${unit}`;
}

function formatNumber(value: number) {
  return value.toLocaleString("en-US");
}

function formatLabel(value: string) {
  return value.replace(/-/g, " ");
}
