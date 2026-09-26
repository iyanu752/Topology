import { componentLibraryItems, type ComponentLibraryItem } from "@/components/component-library/componentLibraryItems";
import type { ComponentType, Design, NodeProperties, SaveDesignDto } from "@/types";
import type { DesignEdge as CanvasEdge, DesignNode as CanvasNode, NodeStatus } from "./DesignNodeCard";
import { createNodeConfiguration } from "./nodeConfiguration";

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

const libraryIdByComponentType: Record<ComponentType, string | undefined> = {
  Client: "client",
  ApiGateway: "api-gateway",
  LoadBalancer: "load-balancer",
  Service: "service",
  Database: "database",
  Cache: "cache",
  Queue: "queue",
  ExternalApi: "external-api"
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
        label: getStringProperty(node.configuration, "displayName") ?? node.component.name,
        x: node.x,
        y: node.y,
        properties: {
          ...node.configuration,
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

export function mapBackendDesignToCanvasDesign(design: Design): CanvasDesignState {
  return {
    nodes: design.nodes.map((node) => {
      const component = getComponentForBackendNode(node.type, node.properties);
      const status = getNodeStatus(node.properties);

      return {
        id: node.id,
        component,
        configuration: createNodeConfiguration(component, {
          ...node.properties,
          displayName: getStringProperty(node.properties, "displayName") ?? node.label ?? component.name
        }),
        status,
        x: node.x,
        y: node.y
      };
    }),
    edges: design.edges.map((edge) => ({
      id: edge.id,
      fromNodeId: edge.sourceNodeId,
      toNodeId: edge.targetNodeId
    }))
  };
}

function getComponentForBackendNode(type: ComponentType, properties: NodeProperties): ComponentLibraryItem {
  const libraryId = getStringProperty(properties, "componentLibraryId") ?? libraryIdByComponentType[type];
  const component = componentLibraryItems.find((item) => item.id === libraryId);

  if (!component) {
    throw new Error(`${type} is not supported by the frontend component library yet.`);
  }

  return component;
}

function getNodeStatus(properties: NodeProperties): NodeStatus {
  const status = getStringProperty(properties, "status");

  return status === "Online" || status === "Degraded" || status === "Saturated" || status === "Offline"
    ? status
    : "Online";
}

function getStringProperty(properties: NodeProperties, key: string) {
  const value = properties[key];
  return typeof value === "string" && value.trim() ? value : null;
}