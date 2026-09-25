import type { PointerEvent } from "react";
import { ComponentLibraryIcon } from "@/components/component-library/ComponentLibraryItem";
import type { ComponentLibraryItem } from "@/components/component-library/componentLibraryItems";

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

export function DesignNodeCard({ node, onConnectionStart, onContextMenu, onPointerDown }: DesignNodeCardProps) {
  return (
    <div
      data-canvas-interactive="true"
      role="button"
      tabIndex={0}
      onContextMenu={(event) => onContextMenu(event, node)}
      onPointerDown={(event) => onPointerDown(event, node)}
      className="absolute flex h-20 w-20 touch-none cursor-grab select-none items-center justify-center rounded-md border border-emerald-500/50 bg-zinc-900 shadow-xl shadow-black/30 transition hover:border-emerald-300 active:cursor-grabbing"
      style={{ left: node.x, top: node.y }}
      aria-label={`Move ${node.component.name}`}
    >
      <ComponentLibraryIcon item={node.component} size="canvas" />

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

