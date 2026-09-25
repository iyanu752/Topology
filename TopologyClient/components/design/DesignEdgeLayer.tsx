import type { SimulationEdgeResult, SimulationEdgeStatus } from "@/types";
import { designNodeSize, type DesignEdge, type DesignNode } from "./DesignNodeCard";

type DesignEdgeLayerProps = {
  draftEdge: { fromNodeId: string; toX: number; toY: number } | null;
  edgeResultsById: Map<string, SimulationEdgeResult>;
  edges: DesignEdge[];
  nodes: DesignNode[];
};

export function DesignEdgeLayer({ draftEdge, edgeResultsById, edges, nodes }: DesignEdgeLayerProps) {
  const nodeById = new Map(nodes.map((node) => [node.id, node]));

  return (
    <svg className="absolute inset-0 h-full w-full overflow-visible" aria-hidden="true">
      <defs>
        <marker id="edge-arrow" markerHeight="8" markerWidth="8" orient="auto" refX="7" refY="4">
          <path d="M 0 0 L 8 4 L 0 8 z" className="fill-emerald-300" />
        </marker>
        <marker id="edge-arrow-warning" markerHeight="8" markerWidth="8" orient="auto" refX="7" refY="4">
          <path d="M 0 0 L 8 4 L 0 8 z" className="fill-amber-300" />
        </marker>
        <marker id="edge-arrow-danger" markerHeight="8" markerWidth="8" orient="auto" refX="7" refY="4">
          <path d="M 0 0 L 8 4 L 0 8 z" className="fill-rose-300" />
        </marker>
      </defs>

      {edges.map((edge) => {
        const fromNode = nodeById.get(edge.fromNodeId);
        const toNode = nodeById.get(edge.toNodeId);

        if (!fromNode || !toNode) {
          return null;
        }

        const from = getNodeCenter(fromNode);
        const to = getNodeCenter(toNode);
        const edgeResult = edgeResultsById.get(edge.id);
        const style = getEdgeStyle(edgeResult?.status ?? "Healthy");

        return (
          <line
            key={edge.id}
            x1={from.x}
            y1={from.y}
            x2={to.x}
            y2={to.y}
            className={style.className}
            strokeDasharray={style.dashArray}
            strokeWidth={style.strokeWidth}
            markerEnd={style.markerEnd}
          />
        );
      })}

      {draftEdge ? <DraftEdge draftEdge={draftEdge} nodeById={nodeById} /> : null}
    </svg>
  );
}

type DraftEdgeProps = {
  draftEdge: { fromNodeId: string; toX: number; toY: number };
  nodeById: Map<string, DesignNode>;
};

function DraftEdge({ draftEdge, nodeById }: DraftEdgeProps) {
  const fromNode = nodeById.get(draftEdge.fromNodeId);

  if (!fromNode) {
    return null;
  }

  const from = getNodeCenter(fromNode);

  return (
    <line
      x1={from.x}
      y1={from.y}
      x2={draftEdge.toX}
      y2={draftEdge.toY}
      className="stroke-emerald-200/70"
      strokeDasharray="6 5"
      strokeWidth="2"
    />
  );
}

function getNodeCenter(node: DesignNode) {
  return {
    x: node.x + designNodeSize.width / 2,
    y: node.y + designNodeSize.height / 2
  };
}

function getEdgeStyle(status: SimulationEdgeStatus) {
  switch (status) {
    case "Degraded":
      return {
        className: "stroke-amber-300/80",
        dashArray: "8 6",
        markerEnd: "url(#edge-arrow-warning)",
        strokeWidth: 2.5
      };
    case "Saturated":
      return {
        className: "stroke-rose-300/85",
        dashArray: undefined,
        markerEnd: "url(#edge-arrow-danger)",
        strokeWidth: 3
      };
    case "Broken":
      return {
        className: "stroke-rose-300/70",
        dashArray: "3 8",
        markerEnd: "url(#edge-arrow-danger)",
        strokeWidth: 2.5
      };
    case "Healthy":
      return {
        className: "stroke-emerald-300/80",
        dashArray: undefined,
        markerEnd: "url(#edge-arrow)",
        strokeWidth: 2
      };
  }
}
