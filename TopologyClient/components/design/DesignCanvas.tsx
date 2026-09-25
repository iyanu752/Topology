"use client";

import { useRef, useState } from "react";
import type { ComponentLibraryItem } from "@/components/component-library/componentLibraryItems";
import { dragPayloadType } from "@/components/component-library/ComponentLibraryItem";
import { WelcomePanel } from "@/components/home/WelcomePanel";
import { connectionRuleService } from "@/services";
import { ApiError } from "@/services/apiClient";
import type { SimulationEdgeResult, SimulationEdgeStatus, SimulationNodeResult, SimulationResult } from "@/types";
import { CanvasContextMenu } from "./CanvasContextMenu";
import { CanvasToolbar } from "./CanvasToolbar";
import { DesignEdgeLayer } from "./DesignEdgeLayer";
import { DesignNodeCard, designNodeSize, type DesignEdge, type DesignNode, type NodeStatus } from "./DesignNodeCard";
import { NodeContextMenu } from "./NodeContextMenu";
import { NodePropertiesPanel } from "./NodePropertiesPanel";
import { SimulationPanel, type SimulationPanelConfig } from "./SimulationPanel";
import { getComponentTypeForLibraryItem } from "./canvasDesignMapper";

type CanvasState = {
  nodes: DesignNode[];
  edges: DesignEdge[];
};

type ActiveDrag = {
  nodeId: string;
  offsetX: number;
  offsetY: number;
  startState: CanvasState;
};

type ActiveConnection = {
  fromNodeId: string;
  toX: number;
  toY: number;
};

type ActivePan = {
  pointerId: number;
  startX: number;
  startY: number;
  originX: number;
  originY: number;
};

type CanvasHistory = {
  past: CanvasState[];
  present: CanvasState;
  future: CanvasState[];
};

type NodeContextMenuState = {
  node: DesignNode;
  x: number;
  y: number;
};

type CanvasContextMenuState = {
  x: number;
  y: number;
  pasteX: number;
  pasteY: number;
};

type ToastState = {
  id: string;
  message: string;
};

type CanvasPan = {
  x: number;
  y: number;
};

const nodeWidth = designNodeSize.width;
const nodeHeight = designNodeSize.height;
const canvasWorldSize = 4000;
const zoomStep = 0.1;
const minZoom = 0.5;
const maxZoom = 2;
const emptyCanvasState: CanvasState = { nodes: [], edges: [] };

export function DesignCanvas() {
  const canvasRef = useRef<HTMLDivElement>(null);
  const toastTimeoutRef = useRef<ReturnType<typeof setTimeout> | null>(null);
  const [history, setHistory] = useState<CanvasHistory>({ past: [], present: emptyCanvasState, future: [] });
  const [activeDrag, setActiveDrag] = useState<ActiveDrag | null>(null);
  const [activeConnection, setActiveConnection] = useState<ActiveConnection | null>(null);
  const [activePan, setActivePan] = useState<ActivePan | null>(null);
  const [nodeContextMenu, setNodeContextMenu] = useState<NodeContextMenuState | null>(null);
  const [canvasContextMenu, setCanvasContextMenu] = useState<CanvasContextMenuState | null>(null);
  const [clipboardNode, setClipboardNode] = useState<DesignNode | null>(null);
  const [toast, setToast] = useState<ToastState | null>(null);
  const [selectedNodeId, setSelectedNodeId] = useState<string | null>(null);
  const [simulationResult, setSimulationResult] = useState<SimulationResult | null>(null);
  const [zoom, setZoom] = useState(1);
  const [pan, setPan] = useState<CanvasPan>({ x: 0, y: 0 });

  const { nodes, edges } = history.present;
  const selectedNode = selectedNodeId ? nodes.find((node) => node.id === selectedNodeId) ?? null : null;
  const simulationNodeResultsById = new Map(simulationResult?.nodeResults.map((result) => [result.nodeId, result]) ?? []);
  const simulationEdgeResultsById = new Map(simulationResult?.edgeResults.map((result) => [result.edgeId, result]) ?? []);

  function commitCanvasState(nextState: CanvasState) {
    setHistory((currentHistory) => ({
      past: [...currentHistory.past, currentHistory.present],
      present: nextState,
      future: []
    }));
  }

  function closeContextMenus() {
    setNodeContextMenu(null);
    setCanvasContextMenu(null);
  }

  function showToast(message: string) {
    if (toastTimeoutRef.current) {
      clearTimeout(toastTimeoutRef.current);
    }

    setToast({ id: crypto.randomUUID(), message });
    toastTimeoutRef.current = setTimeout(() => setToast(null), 4200);
  }

  function handleDrop(event: React.DragEvent<HTMLDivElement>) {
    event.preventDefault();
    closeContextMenus();

    const payload = event.dataTransfer.getData(dragPayloadType);
    if (!payload) {
      return;
    }

    const component = JSON.parse(payload) as ComponentLibraryItem;
    const bounds = canvasRef.current?.getBoundingClientRect();
    if (!bounds) {
      return;
    }

    const position = getCanvasPosition(event.clientX, event.clientY, bounds, zoom, pan);
    const newNode: DesignNode = {
      id: `${component.id}-${crypto.randomUUID()}`,
      component,
      status: "Online",
      x: position.x,
      y: position.y
    };

    setSimulationResult(null);
    commitCanvasState({ nodes: [...nodes, newNode], edges });
    setSelectedNodeId(newNode.id);
  }

  function handleCanvasPointerDown(event: React.PointerEvent<HTMLDivElement>) {
    if (event.button !== 0 || isCanvasInteractionTarget(event.target)) {
      return;
    }

    event.currentTarget.setPointerCapture(event.pointerId);
    closeContextMenus();
    setSelectedNodeId(null);
    setActivePan({
      pointerId: event.pointerId,
      startX: event.clientX,
      startY: event.clientY,
      originX: pan.x,
      originY: pan.y
    });
  }

  function handleNodePointerDown(event: React.PointerEvent<HTMLDivElement>, node: DesignNode) {
    if (event.button !== 0) {
      return;
    }

    const bounds = canvasRef.current?.getBoundingClientRect();
    if (!bounds) {
      return;
    }

    event.currentTarget.setPointerCapture(event.pointerId);
    closeContextMenus();
    setSelectedNodeId(node.id);
    setActiveDrag({
      nodeId: node.id,
      offsetX: event.clientX - bounds.left - pan.x - node.x * zoom,
      offsetY: event.clientY - bounds.top - pan.y - node.y * zoom,
      startState: history.present
    });
  }

  function handleConnectionStart(event: React.PointerEvent<HTMLButtonElement>, node: DesignNode) {
    if (event.button !== 0) {
      return;
    }

    event.preventDefault();
    event.stopPropagation();
    closeContextMenus();

    setActiveConnection({
      fromNodeId: node.id,
      toX: node.x + nodeWidth / 2,
      toY: node.y + nodeHeight / 2
    });
  }

  function handlePointerMove(event: React.PointerEvent<HTMLDivElement>) {
    const bounds = canvasRef.current?.getBoundingClientRect();
    if (!bounds) {
      return;
    }

    if (activePan) {
      setPan({
        x: activePan.originX + event.clientX - activePan.startX,
        y: activePan.originY + event.clientY - activePan.startY
      });
      return;
    }

    if (activeConnection) {
      const pointer = getCanvasPoint(event.clientX, event.clientY, bounds, zoom, pan);
      setActiveConnection({ ...activeConnection, toX: pointer.x, toY: pointer.y });
      return;
    }

    if (!activeDrag) {
      return;
    }

    const nextX = (event.clientX - bounds.left - pan.x - activeDrag.offsetX) / zoom;
    const nextY = (event.clientY - bounds.top - pan.y - activeDrag.offsetY) / zoom;
    const position = clampPosition(nextX, nextY);

    setHistory((currentHistory) => ({
      ...currentHistory,
      present: {
        ...currentHistory.present,
        nodes: currentHistory.present.nodes.map((node) =>
          node.id === activeDrag.nodeId ? { ...node, x: position.x, y: position.y } : node
        )
      }
    }));
  }

  function handlePointerUp(event: React.PointerEvent<HTMLDivElement>) {
    setActivePan(null);
    void finishConnection(event);
    finishDrag();
  }

  async function finishConnection(event: React.PointerEvent<HTMLDivElement>) {
    if (!activeConnection) {
      return;
    }

    const connection = activeConnection;
    setActiveConnection(null);

    const targetNodeId = getConnectionTargetNodeId(event.clientX, event.clientY);
    if (!targetNodeId || targetNodeId === connection.fromNodeId) {
      return;
    }

    const edgeExists = edges.some((edge) => edge.fromNodeId === connection.fromNodeId && edge.toNodeId === targetNodeId);
    if (edgeExists) {
      showToast("Those components are already connected.");
      return;
    }

    const sourceNode = nodes.find((node) => node.id === connection.fromNodeId);
    const targetNode = nodes.find((node) => node.id === targetNodeId);
    if (!sourceNode || !targetNode) {
      return;
    }

    const validation = await validateConnection(sourceNode, targetNode);
    if (!validation.isAllowed) {
      showToast(validation.message);
      return;
    }

    const newEdge: DesignEdge = {
      id: `${connection.fromNodeId}-${targetNodeId}-${crypto.randomUUID()}`,
      fromNodeId: connection.fromNodeId,
      toNodeId: targetNodeId
    };

    commitCanvasState({ nodes, edges: [...edges, newEdge] });
  }

  async function validateConnection(sourceNode: DesignNode, targetNode: DesignNode) {
    const sourceType = getComponentTypeForLibraryItem(sourceNode.component.id);
    const targetType = getComponentTypeForLibraryItem(targetNode.component.id);

    if (!sourceType || !targetType) {
      return {
        isAllowed: false,
        message: `${sourceNode.component.name} cannot be connected to ${targetNode.component.name} yet because it has no backend validation rule.`
      };
    }

    try {
      const rule = await connectionRuleService.getRule(sourceType, targetType);

      if (!rule.isAllowed) {
        return {
          isAllowed: false,
          message: rule.message || `${sourceNode.component.name} cannot be connected to ${targetNode.component.name}.`
        };
      }

      return { isAllowed: true, message: rule.message };
    } catch (error) {
      if (error instanceof ApiError && error.status === 404) {
        return {
          isAllowed: false,
          message: `${sourceNode.component.name} cannot be connected to ${targetNode.component.name} because no connection rule exists yet.`
        };
      }

      return {
        isAllowed: false,
        message: "Could not validate that connection. Make sure the API server is running, then try again."
      };
    }
  }

  function finishDrag() {
    if (!activeDrag) {
      return;
    }

    setHistory((currentHistory) => {
      if (areCanvasStatesEqual(activeDrag.startState, currentHistory.present)) {
        return currentHistory;
      }

      return {
        past: [...currentHistory.past, activeDrag.startState],
        present: currentHistory.present,
        future: []
      };
    });
    setActiveDrag(null);
  }

  function handleNodeContextMenu(event: React.MouseEvent<HTMLDivElement>, node: DesignNode) {
    event.preventDefault();
    event.stopPropagation();
    setCanvasContextMenu(null);
    setSelectedNodeId(node.id);
    setNodeContextMenu({ node, x: event.clientX, y: event.clientY });
  }

  function handleCanvasContextMenu(event: React.MouseEvent<HTMLDivElement>) {
    event.preventDefault();

    const bounds = canvasRef.current?.getBoundingClientRect();
    if (!bounds) {
      return;
    }

    const position = getCanvasPosition(event.clientX, event.clientY, bounds, zoom, pan);
    setNodeContextMenu(null);
    setCanvasContextMenu({
      x: event.clientX,
      y: event.clientY,
      pasteX: position.x,
      pasteY: position.y
    });
  }

  function undo() {
    closeContextMenus();
    setHistory((currentHistory) => {
      const previous = currentHistory.past.at(-1);
      if (!previous) {
        return currentHistory;
      }

      return {
        past: currentHistory.past.slice(0, -1),
        present: previous,
        future: [currentHistory.present, ...currentHistory.future]
      };
    });
  }

  function redo() {
    closeContextMenus();
    setHistory((currentHistory) => {
      const next = currentHistory.future[0];
      if (!next) {
        return currentHistory;
      }

      return {
        past: [...currentHistory.past, currentHistory.present],
        present: next,
        future: currentHistory.future.slice(1)
      };
    });
  }

  function copyNode(node: DesignNode) {
    setClipboardNode(node);
  }

  function cutNode(node: DesignNode) {
    setClipboardNode(node);
    removeNode(node);
  }

  function deleteNode(node: DesignNode) {
    removeNode(node);
  }

  function removeNode(node: DesignNode) {
    if (selectedNodeId === node.id) {
      setSelectedNodeId(null);
    }

    setSimulationResult(null);
    commitCanvasState({
      nodes: nodes.filter((currentNode) => currentNode.id !== node.id),
      edges: edges.filter((edge) => edge.fromNodeId !== node.id && edge.toNodeId !== node.id)
    });
  }

  function duplicateNode(node: DesignNode) {
    const duplicate = createDuplicateNode(node);
    setSimulationResult(null);
    commitCanvasState({ nodes: [...nodes, duplicate], edges });
    setSelectedNodeId(duplicate.id);
  }

  function pasteCopiedNode(position?: { x: number; y: number }) {
    if (!clipboardNode) {
      return;
    }

    const pastedNode = position
      ? {
          ...clipboardNode,
          id: `${clipboardNode.component.id}-${crypto.randomUUID()}`,
          status: clipboardNode.status ?? "Online",
          x: position.x,
          y: position.y
        }
      : createDuplicateNode(clipboardNode);

    setSimulationResult(null);
    commitCanvasState({ nodes: [...nodes, pastedNode], edges });
    setSelectedNodeId(pastedNode.id);
  }

  function updateSelectedNodeStatus(status: NodeStatus) {
    if (!selectedNode) {
      return;
    }

    setSimulationResult(null);
    commitCanvasState({
      nodes: nodes.map((node) => (node.id === selectedNode.id ? { ...node, status } : node)),
      edges
    });
  }

  function runSimulation(config: SimulationPanelConfig) {
    const result = createLocalSimulationResult(history.present, config);
    const label = config.scenario.replace(/([A-Z])/g, " $1").trim();

    setSimulationResult(result);
    showToast(`${label} painted on canvas at ${config.trafficPerSecond.toLocaleString()} req/s.`);
  }

  function zoomOut() {
    setZoom((currentZoom) => Math.max(minZoom, roundZoom(currentZoom - zoomStep)));
  }

  function zoomIn() {
    setZoom((currentZoom) => Math.min(maxZoom, roundZoom(currentZoom + zoomStep)));
  }

  return (
    <section
      ref={canvasRef}
      onClick={closeContextMenus}
      onContextMenu={handleCanvasContextMenu}
      onDragOver={(event) => event.preventDefault()}
      onDrop={handleDrop}
      onPointerDown={handleCanvasPointerDown}
      onPointerMove={handlePointerMove}
      onPointerUp={handlePointerUp}
      onPointerCancel={() => {
        setActiveConnection(null);
        setActivePan(null);
        finishDrag();
      }}
      className={`relative min-h-screen overflow-hidden ${activePan ? "cursor-grabbing" : "cursor-grab"}`}
      aria-label="Design room"
    >
      {nodes.length === 0 ? <WelcomePanel /> : null}

      <div
        className="absolute left-0 top-0 origin-top-left"
        style={{
          transform: `translate(${pan.x}px, ${pan.y}px) scale(${zoom})`,
          width: canvasWorldSize,
          height: canvasWorldSize
        }}
      >
        <DesignEdgeLayer draftEdge={activeConnection} edgeResultsById={simulationEdgeResultsById} edges={edges} nodes={nodes} />

        {nodes.map((node) => (
          <DesignNodeCard
            key={node.id}
            isSelected={node.id === selectedNodeId}
            node={node}
            simulationResult={simulationNodeResultsById.get(node.id)}
            onConnectionStart={handleConnectionStart}
            onContextMenu={handleNodeContextMenu}
            onPointerDown={handleNodePointerDown}
          />
        ))}
      </div>

      {selectedNode ? (
        <NodePropertiesPanel
          node={selectedNode}
          onClose={() => setSelectedNodeId(null)}
          onStatusChange={updateSelectedNodeStatus}
        />
      ) : null}

      <SimulationPanel onRun={runSimulation} />

      <CanvasToolbar
        canRedo={history.future.length > 0}
        canUndo={history.past.length > 0}
        onRedo={redo}
        onUndo={undo}
        onZoomIn={zoomIn}
        onZoomOut={zoomOut}
        zoomPercentage={Math.round(zoom * 100)}
      />

      {nodeContextMenu ? (
        <NodeContextMenu
          node={nodeContextMenu.node}
          onClose={() => setNodeContextMenu(null)}
          onCopy={copyNode}
          onCut={cutNode}
          onDelete={deleteNode}
          onDuplicate={duplicateNode}
          x={nodeContextMenu.x}
          y={nodeContextMenu.y}
        />
      ) : null}

      {canvasContextMenu ? (
        <CanvasContextMenu
          canPaste={Boolean(clipboardNode)}
          onClose={() => setCanvasContextMenu(null)}
          onPaste={() => pasteCopiedNode({ x: canvasContextMenu.pasteX, y: canvasContextMenu.pasteY })}
          x={canvasContextMenu.x}
          y={canvasContextMenu.y}
        />
      ) : null}

      {toast ? (
        <div className="fixed right-4 top-4 z-50 max-w-sm rounded-md border border-red-500/40 bg-zinc-950 px-4 py-3 text-sm text-red-100 shadow-2xl shadow-black/40" role="status">
          {toast.message}
        </div>
      ) : null}
    </section>
  );
}

function createLocalSimulationResult(canvasState: CanvasState, config: SimulationPanelConfig): SimulationResult {
  const nodeResults = canvasState.nodes.map((node): SimulationNodeResult => {
    const status = getLocalNodeStatus(node, config, canvasState);
    const loadPercentage = getLocalLoadPercentage(node, status, config);

    return {
      nodeId: node.id,
      status,
      message: getLocalNodeMessage(node, status, config),
      loadPercentage,
      metrics: {
        componentLibraryId: node.component.id,
        label: node.component.name
      }
    };
  });

  const nodeResultById = new Map(nodeResults.map((result) => [result.nodeId, result]));
  const edgeResults = canvasState.edges.map((edge): SimulationEdgeResult => {
    const sourceStatus = nodeResultById.get(edge.fromNodeId)?.status ?? "Online";
    const targetStatus = nodeResultById.get(edge.toNodeId)?.status ?? "Online";
    const status = getLocalEdgeStatus(sourceStatus, targetStatus);

    return {
      edgeId: edge.id,
      sourceNodeId: edge.fromNodeId,
      targetNodeId: edge.toNodeId,
      status,
      message: getLocalEdgeMessage(status),
      trafficPerSecond: config.trafficPerSecond
    };
  });

  const affectedNodeIds = nodeResults.filter((result) => result.status !== "Online").map((result) => result.nodeId);
  const riskScore = Math.min(8, nodeResults.reduce((score, result) => score + getNodeRiskScore(result.status), 0));

  return {
    scenario: config.scenario,
    riskLevel: getRiskLevel(riskScore),
    riskScore,
    findings: affectedNodeIds.length > 0 ? ["Simulation changed one or more node states on the canvas."] : ["Simulation kept all nodes online."],
    impact: affectedNodeIds.length > 0 ? ["Affected nodes and paths are highlighted on the canvas."] : ["No visible impact detected for this local preview."],
    recommendations: ["Backend simulation can replace this local preview once save/load auth flow is wired."],
    affectedNodeIds,
    nodeResults,
    edgeResults
  };
}

function getLocalNodeStatus(node: DesignNode, config: SimulationPanelConfig, canvasState: CanvasState): NodeStatus {
  if (config.scenario === "DatabaseFailure" && node.component.id === "database") {
    return "Offline";
  }

  if (config.scenario === "CacheFailure" && node.component.id === "cache") {
    return "Offline";
  }

  if (config.scenario === "QueueBacklog" && node.component.id === "queue") {
    return "Saturated";
  }

  if (config.scenario === "ExternalApiFailure" && node.component.id === "external-api") {
    return "Offline";
  }

  if (config.scenario === "DatabaseFailure" && isConnectedToComponent(node.id, "database", canvasState)) {
    return "Degraded";
  }

  if ((config.scenario === "HighTraffic" || config.scenario === "SuddenUserGrowth") && ["service", "api-gateway", "load-balancer"].includes(node.component.id)) {
    return config.trafficPerSecond > 1500 ? "Saturated" : "Degraded";
  }

  if (config.scenario === "HighLatency" && ["service", "database", "external-api"].includes(node.component.id)) {
    return "Degraded";
  }

  if (config.scenario === "ReadHeavyWorkload" && node.component.id === "database" && !hasComponent("cache", canvasState)) {
    return "Degraded";
  }

  if (config.scenario === "WriteHeavyWorkload" && ["database", "queue"].includes(node.component.id)) {
    return config.hasCriticalWrites ? "Saturated" : "Degraded";
  }

  return node.status;
}

function getLocalLoadPercentage(node: DesignNode, status: NodeStatus, config: SimulationPanelConfig) {
  if (status === "Offline") {
    return 0;
  }

  if (status === "Saturated") {
    return 99;
  }

  if (status === "Degraded") {
    return Math.min(88, Math.max(62, Math.round(config.trafficPerSecond / 30)));
  }

  if (["service", "api-gateway", "load-balancer"].includes(node.component.id)) {
    return Math.min(74, Math.max(18, Math.round(config.trafficPerSecond / 80)));
  }

  return 32;
}

function getLocalNodeMessage(node: DesignNode, status: NodeStatus, config: SimulationPanelConfig) {
  if (status === "Offline") {
    return `${node.component.name} is offline in this scenario.`;
  }

  if (status === "Saturated") {
    return `${node.component.name} is saturated by ${config.trafficPerSecond.toLocaleString()} req/s.`;
  }

  if (status === "Degraded") {
    return `${node.component.name} is degraded by the selected scenario.`;
  }

  return "Node stays online in this scenario.";
}

function getLocalEdgeStatus(sourceStatus: NodeStatus, targetStatus: NodeStatus): SimulationEdgeStatus {
  if (sourceStatus === "Offline" || targetStatus === "Offline") {
    return "Broken";
  }

  if (sourceStatus === "Saturated" || targetStatus === "Saturated") {
    return "Saturated";
  }

  if (sourceStatus === "Degraded" || targetStatus === "Degraded") {
    return "Degraded";
  }

  return "Healthy";
}

function getLocalEdgeMessage(status: SimulationEdgeStatus) {
  switch (status) {
    case "Broken":
      return "Traffic path is broken by an offline node.";
    case "Saturated":
      return "Traffic path crosses a saturated node.";
    case "Degraded":
      return "Traffic path crosses a degraded node.";
    case "Healthy":
      return "Traffic path is healthy.";
  }
}

function getNodeRiskScore(status: NodeStatus) {
  switch (status) {
    case "Offline":
      return 3;
    case "Saturated":
      return 2;
    case "Degraded":
      return 1;
    case "Online":
      return 0;
  }
}

function getRiskLevel(riskScore: number) {
  if (riskScore <= 1) {
    return "Low";
  }

  if (riskScore <= 3) {
    return "Medium";
  }

  if (riskScore <= 5) {
    return "High";
  }

  return "Critical";
}

function isConnectedToComponent(nodeId: string, componentId: string, canvasState: CanvasState) {
  const targetNodeIds = canvasState.nodes.filter((node) => node.component.id === componentId).map((node) => node.id);

  return canvasState.edges.some(
    (edge) =>
      (edge.fromNodeId === nodeId && targetNodeIds.includes(edge.toNodeId)) ||
      (edge.toNodeId === nodeId && targetNodeIds.includes(edge.fromNodeId))
  );
}

function hasComponent(componentId: string, canvasState: CanvasState) {
  return canvasState.nodes.some((node) => node.component.id === componentId);
}

function getCanvasPoint(clientX: number, clientY: number, bounds: DOMRect, zoom: number, pan: CanvasPan) {
  return {
    x: (clientX - bounds.left - pan.x) / zoom,
    y: (clientY - bounds.top - pan.y) / zoom
  };
}

function getCanvasPosition(clientX: number, clientY: number, bounds: DOMRect, zoom: number, pan: CanvasPan) {
  const point = getCanvasPoint(clientX, clientY, bounds, zoom, pan);
  return clampPosition(point.x - nodeWidth / 2, point.y - nodeHeight / 2);
}

function clampPosition(x: number, y: number) {
  const maxX = canvasWorldSize - nodeWidth;
  const maxY = canvasWorldSize - nodeHeight;

  return {
    x: Math.min(Math.max(0, x), maxX),
    y: Math.min(Math.max(0, y), maxY)
  };
}

function createDuplicateNode(node: DesignNode) {
  const position = clampPosition(node.x + 28, node.y + 28);

  return {
    ...node,
    id: `${node.component.id}-${crypto.randomUUID()}`,
    x: position.x,
    y: position.y
  };
}

function getConnectionTargetNodeId(clientX: number, clientY: number) {
  const target = document.elementFromPoint(clientX, clientY);

  if (!(target instanceof HTMLElement)) {
    return null;
  }

  return target.closest<HTMLElement>("[data-connect-node-id]")?.dataset.connectNodeId ?? null;
}

function isCanvasInteractionTarget(target: EventTarget) {
  return target instanceof HTMLElement && Boolean(target.closest("[data-canvas-interactive]"));
}

function areCanvasStatesEqual(first: CanvasState, second: CanvasState) {
  return JSON.stringify(first) === JSON.stringify(second);
}

function roundZoom(value: number) {
  return Math.round(value * 10) / 10;
}









