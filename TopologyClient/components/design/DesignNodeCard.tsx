import type { PointerEvent } from "react";
import { ComponentLibraryIcon } from "@/components/component-library/ComponentLibraryItem";
import type { ComponentLibraryItem } from "@/components/component-library/componentLibraryItems";

export const designNodeSize = {
  width: 244,
  height: 136
} as const;

export type DesignNode = {
  id: string;
  component: ComponentLibraryItem;
  x: number;
  y: number;
};

export type DesignEdge = {
  id: string;
  fromNodeId: string;
  toNodeId: string;
};

type DesignNodeCardProps = {
  node: DesignNode;
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

export function DesignNodeCard({ node, onConnectionStart, onContextMenu, onPointerDown }: DesignNodeCardProps) {
  const profile = getNodeCardProfile(node.component);

  return (
    <div
      data-canvas-interactive="true"
      role="button"
      tabIndex={0}
      onContextMenu={(event) => onContextMenu(event, node)}
      onPointerDown={(event) => onPointerDown(event, node)}
      className="absolute touch-none cursor-grab select-none overflow-hidden rounded-lg border border-zinc-700/80 bg-zinc-950 shadow-2xl shadow-black/40 transition hover:border-emerald-300/80 active:cursor-grabbing"
      style={{ left: node.x, top: node.y, width: designNodeSize.width, height: designNodeSize.height }}
      aria-label={`Move ${node.component.name}`}
    >
      <div className="flex h-full flex-col p-4">
        <div className="flex items-start justify-between gap-3">
          <div className="flex min-w-0 items-center gap-2">
            <span className="flex h-7 w-7 shrink-0 items-center justify-center rounded-md border border-zinc-800 bg-zinc-900">
              <ComponentLibraryIcon item={node.component} size="sidebar" />
            </span>
            <div className="min-w-0">
              <div className="truncate text-sm font-semibold uppercase text-zinc-100">{node.component.name}</div>
              <div className="mt-0.5 truncate text-[11px] uppercase text-zinc-500">{profile.caption}</div>
            </div>
          </div>

          <span className="shrink-0 rounded border border-emerald-400/30 bg-emerald-400/10 px-2 py-1 text-[11px] font-semibold uppercase text-emerald-200">
            Ready
          </span>
        </div>

        <div className="mt-4 grid flex-1 grid-cols-[1fr_70px] gap-4">
          <div className="space-y-2.5 self-end">
            {profile.bars.map((value, index) => (
              <div key={`${node.id}-bar-${index}`} className="h-2.5 overflow-hidden rounded-sm border border-zinc-800 bg-zinc-900">
                <div className="h-full rounded-sm bg-emerald-400/60" style={{ width: `${value}%` }} />
              </div>
            ))}
          </div>

          <div className="flex flex-col justify-end border-l border-zinc-800 pl-4">
            <span className="text-[11px] font-semibold uppercase text-zinc-500">{profile.metricLabel}</span>
            <span className="mt-2 text-sm font-semibold text-emerald-200">{profile.metricValue}</span>
          </div>
        </div>
      </div>

      <button
        type="button"
        aria-label={`Connect from ${node.component.name}`}
        data-connect-node-id={node.id}
        onPointerDown={(event) => onConnectionStart(event, node)}
        className="absolute -right-2 top-1/2 h-5 w-5 -translate-y-1/2 cursor-crosshair rounded-full border-2 border-zinc-950 bg-emerald-300 shadow-lg shadow-black/30 transition hover:scale-110 hover:bg-emerald-200 focus:outline-none focus:ring-2 focus:ring-emerald-200"
      />
    </div>
  );
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
