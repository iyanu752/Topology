import { designNodeSize, type DesignEdge, type DesignNode } from "./DesignNodeCard";

type DesignEdgeLayerProps = {
  draftEdge: { fromNodeId: string; toX: number; toY: number } | null;
  edges: DesignEdge[];
  nodes: DesignNode[];
};

export function DesignEdgeLayer({ draftEdge, edges, nodes }: DesignEdgeLayerProps) {
  const nodeById = new Map(nodes.map((node) => [node.id, node]));

  return (
    <svg className="absolute inset-0 h-full w-full overflow-visible" aria-hidden="true">
      <defs>
        <marker id="edge-arrow" markerHeight="8" markerWidth="8" orient="auto" refX="7" refY="4">
          <path d="M 0 0 L 8 4 L 0 8 z" className="fill-emerald-300" />
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

        return (
          <line
            key={edge.id}
            x1={from.x}
            y1={from.y}
            x2={to.x}
            y2={to.y}
            className="stroke-emerald-300/80"
            strokeWidth="2"
            markerEnd="url(#edge-arrow)"
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
