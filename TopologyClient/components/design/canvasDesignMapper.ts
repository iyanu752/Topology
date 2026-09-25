import type { SaveDesignDto, ComponentType } from "@/types";
import type { DesignEdge as CanvasEdge, DesignNode as CanvasNode } from "./DesignNodeCard";

type CanvasDesignState = {
  nodes: CanvasNode[];
  edges: CanvasEdge[];
};

const componentTypeByLibraryId: Record<string, ComponentType | undefined> = {
  client: "Client",
  "api-gateway": "ApiGateway",
  "load-balancer": "LoadBalancer",
  service: "Service",
  database: "Database",
  cache: "Cache",
  queue: "Queue",
  "external-api": "ExternalApi"
};

export function getComponentTypeForLibraryItem(libraryItemId: string) {
  return componentTypeByLibraryId[libraryItemId] ?? null;
}

export function mapCanvasDesignToSaveDesignDto(canvasState: CanvasDesignState): SaveDesignDto {
  return {
    nodes: canvasState.nodes.map((node) => {
      const type = getComponentTypeForLibraryItem(node.component.id);

      if (!type) {
        throw new Error(`${node.component.name} is not supported by the backend design model yet.`);
      }

      return {
        id: node.id,
        type,
        label: node.component.name,
        x: node.x,
        y: node.y,
        properties: {
          status: node.status,
          componentLibraryId: node.component.id
        }
      };
    }),
    edges: canvasState.edges.map((edge) => ({
      id: edge.id,
      sourceNodeId: edge.fromNodeId,
      targetNodeId: edge.toNodeId,
      label: null
    }))
  };
}
