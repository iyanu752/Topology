"use client";

import { useEffect, useRef, useState } from "react";
import type { ComponentLibraryItem } from "@/components/component-library/componentLibraryItems";
import { componentLibraryItems } from "@/components/component-library/componentLibraryItems";
import { simulationService } from "@/services/simulationService";
import { dragPayloadType } from "@/components/component-library/ComponentLibraryItem";
import { WelcomePanel } from "@/components/home/WelcomePanel";
import { connectionRuleService } from "@/services";
import { ApiError } from "@/services/apiClient";
import type { SimulationEdgeResult, SimulationNodeResult, SimulationResult } from "@/types";
import { CanvasContextMenu } from "./CanvasContextMenu";
import { CanvasToolbar } from "./CanvasToolbar";
import { DesignEdgeLayer } from "./DesignEdgeLayer";
import { DesignNodeCard, designNodeSize, type DesignEdge, type DesignNode, type NodeStatus } from "./DesignNodeCard";
import { NodeContextMenu } from "./NodeContextMenu";
import { NodePropertiesPanel } from "./NodePropertiesPanel";
import { SimulationPanel, type SimulationPanelConfig } from "./SimulationPanel";
import { getComponentTypeForLibraryItem, mapCanvasDesignToSaveDesignDto } from "./canvasDesignMapper";
import { cloneNodeConfiguration, createDefaultNodeConfiguration, createNodeConfiguration } from "./nodeConfiguration";

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
const browserStorageKey = "topology.canvas.design.v1";

export function DesignCanvas() {
  const canvasRef = useRef<HTMLDivElement>(null);
  const fileInputRef = useRef<HTMLInputElement>(null);
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
  const [simulationBusy, setSimulationBusy] = useState(false);
  const [simulationError, setSimulationError] = useState<string | null>(null);
  const [simulationFrame, setSimulationFrame] = useState(0);
  const simulationAbort = useRef<AbortController | null>(null);
  const [hasSavedBrowserDesign, setHasSavedBrowserDesign] = useState(false);
  const [zoom, setZoom] = useState(1);
  const [pan, setPan] = useState<CanvasPan>({ x: 0, y: 0 });

  const { nodes, edges } = history.present;
  const selectedNode = selectedNodeId ? nodes.find((node) => node.id === selectedNodeId) ?? null : null;
  const sample = simulationResult?.execution?.timeline[simulationFrame];
  const simulationNodeResultsById = new Map<string, SimulationNodeResult>(simulationResult?.nodeResults.map(result => {
    if (!sample) return [result.nodeId, result];
    const recorded = sample.measurements.nodes.find(node => node.nodeId === result.nodeId);
    if (recorded) return [result.nodeId, recorded];
    const resources = sample.measurements.resources.filter(r => r.nodeId === result.nodeId);
    const waiting = resources.reduce((n, r) => n + r.queueDepth, 0);
    const load = Math.max(0, ...resources.map(r => r.active / r.capacity));
    return [result.nodeId, { ...result, loadPercentage: Math.round(load * 100), status: waiting > 0 || load >= 1 ? "Saturated" : "Online", message: `${waiting} waiting`, metrics: { queueDepth: waiting } }];
  }) ?? []);
  const simulationEdgeResultsById = new Map<string, SimulationEdgeResult>(simulationResult?.edgeResults.map(result => {
    if (!sample) return [result.edgeId, result];
    const edge = sample.measurements.edges.find(e => e.edgeId === result.edgeId);
    const seconds = (sample.atMicroseconds - sample.intervalStartMicroseconds) / 1000000;
    const broken = sample.measurements.nodes.some(node => (node.nodeId === result.sourceNodeId || node.nodeId === result.targetNodeId) && node.status === "Offline");
    return [result.edgeId, { ...result, status: broken ? "Broken" : "Healthy", trafficPerSecond: seconds > 0 ? Math.round((edge?.calls ?? 0) / seconds) : 0 }];
  }) ?? []);

  useEffect(() => {
    simulationAbort.current?.abort();
    setSimulationBusy(false);
    setSimulationResult(null);
    setSimulationError(null);
  }, [history.present]);

  useEffect(() => () => simulationAbort.current?.abort(), []);

  useEffect(() => {
    setHasSavedBrowserDesign(hasBrowserSavedDesign());
  }, []);

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
      configuration: createDefaultNodeConfiguration(component),
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
          configuration: cloneNodeConfiguration(clipboardNode.configuration),
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

  async function runSimulation(config: SimulationPanelConfig) {
    simulationAbort.current?.abort();
    const controller = new AbortController();
    simulationAbort.current = controller;
    setSimulationBusy(true);
    setSimulationError(null);
    setSimulationResult(null);
    try {
      const result = await simulationService.preview(mapCanvasDesignToSaveDesignDto(history.present), config, controller.signal);
      if (controller.signal.aborted) return;
      setSimulationFrame(0);
      setSimulationResult(result);
    } catch (error) {
      if (!controller.signal.aborted) setSimulationError(error instanceof Error ? error.message : "Simulation failed.");
    } finally {
      if (simulationAbort.current === controller) setSimulationBusy(false);
    }
  }

  function addSimulationExample() {
    if (nodes.length > 0) { showToast("Use an empty canvas to add the example. Your current design was kept."); return; }
    const sampleNodes = ["client", "service", "database"].map((id, index): DesignNode => {
      const component = componentLibraryItems.find(item => item.id === id)!;
      return { id: `example-${id}`, component, configuration: createDefaultNodeConfiguration(component), status: "Online", x: 470 + index * 290, y: 260 };
    });
    commitCanvasState({ nodes: sampleNodes, edges: [
      { id: "example-client-service", fromNodeId: sampleNodes[0].id, toNodeId: sampleNodes[1].id },
      { id: "example-service-database", fromNodeId: sampleNodes[1].id, toNodeId: sampleNodes[2].id }
    ] });
  }

  function saveDesignToBrowser() {
    try {
      window.localStorage.setItem(browserStorageKey, JSON.stringify(createPersistedDesign(history.present, pan, zoom)));
      setHasSavedBrowserDesign(true);
      showToast("Design saved in browser storage.");
    } catch {
      showToast("Could not save this design in browser storage.");
    }
  }

  function loadDesignFromBrowser() {
    try {
      const savedDesign = window.localStorage.getItem(browserStorageKey);
      if (!savedDesign) {
        showToast("No saved browser design found.");
        setHasSavedBrowserDesign(false);
        return;
      }

      applyPersistedDesign(parsePersistedDesign(JSON.parse(savedDesign)));
      showToast("Design loaded from browser storage.");
    } catch {
      showToast("Could not load the saved browser design.");
    }
  }

  function exportDesignToFile() {
    const payload = JSON.stringify(createPersistedDesign(history.present, pan, zoom), null, 2);
    const blob = new Blob([payload], { type: "application/json" });
    const url = URL.createObjectURL(blob);
    const anchor = document.createElement("a");

    anchor.href = url;
    anchor.download = `topology-design-${new Date().toISOString().slice(0, 10)}.json`;
    anchor.click();
    URL.revokeObjectURL(url);
    showToast("Design exported as JSON.");
  }

  function openImportFilePicker() {
    fileInputRef.current?.click();
  }

  async function importDesignFromFile(event: React.ChangeEvent<HTMLInputElement>) {
    const file = event.target.files?.[0];
    event.target.value = "";

    if (!file) {
      return;
    }

    try {
      const importedDesign = parsePersistedDesign(JSON.parse(await file.text()));
      applyPersistedDesign(importedDesign);
      showToast("Design imported from JSON.");
    } catch {
      showToast("Could not import that design file.");
    }
  }

  function applyPersistedDesign(persistedDesign: PersistedCanvasDesign) {
    closeContextMenus();
    setSimulationResult(null);
    setSelectedNodeId(null);
    setPan(persistedDesign.pan);
    setZoom(persistedDesign.zoom);
    setHistory({ past: [], present: persistedDesign.canvasState, future: [] });
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

      <SimulationPanel onRun={runSimulation} busy={simulationBusy} error={simulationError} result={simulationResult}
        targets={nodes.map(node => ({ id: node.id, label: String(node.configuration.displayName || node.component.name), replicas: Number(node.configuration.replicas ?? 1) }))}
        frame={simulationFrame} onFrame={setSimulationFrame} onExample={addSimulationExample}
        onCancel={() => { simulationAbort.current?.abort(); setSimulationBusy(false); }} />

      <CanvasToolbar
        canLoadSavedDesign={hasSavedBrowserDesign}
        canRedo={history.future.length > 0}
        canUndo={history.past.length > 0}
        onExportFile={exportDesignToFile}
        onImportFile={openImportFilePicker}
        onLoadBrowser={loadDesignFromBrowser}
        onRedo={redo}
        onSaveBrowser={saveDesignToBrowser}
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

      <input
        ref={fileInputRef}
        type="file"
        accept="application/json"
        className="hidden"
        onChange={importDesignFromFile}
      />

      {toast ? (
        <div className="fixed right-4 top-4 z-50 max-w-sm rounded-md border border-red-500/40 bg-zinc-950 px-4 py-3 text-sm text-red-100 shadow-2xl shadow-black/40" role="status">
          {toast.message}
        </div>
      ) : null}
    </section>
  );
}

type PersistedCanvasDesign = {
  version: 1;
  savedAt: string;
  canvasState: CanvasState;
  pan: CanvasPan;
  zoom: number;
};

function createPersistedDesign(canvasState: CanvasState, pan: CanvasPan, zoom: number): PersistedCanvasDesign {
  return {
    version: 1,
    savedAt: new Date().toISOString(),
    canvasState: normalizeCanvasState(canvasState),
    pan,
    zoom
  };
}

function parsePersistedDesign(value: unknown): PersistedCanvasDesign {
  if (!isRecord(value) || value.version !== 1) {
    throw new Error("Unsupported design file version.");
  }

  const canvasState = value.canvasState;
  const pan = value.pan;
  const zoom = value.zoom;

  if (!isCanvasState(canvasState) || !isPan(pan) || typeof zoom !== "number") {
    throw new Error("Invalid design file.");
  }

  return {
    version: 1,
    savedAt: typeof value.savedAt === "string" ? value.savedAt : new Date().toISOString(),
    canvasState: normalizeCanvasState(canvasState),
    pan,
    zoom: Math.min(Math.max(zoom, minZoom), maxZoom)
  };
}

function normalizeCanvasState(canvasState: CanvasState): CanvasState {
  return {
    nodes: canvasState.nodes.map(normalizeDesignNode),
    edges: canvasState.edges
  };
}

function normalizeDesignNode(node: DesignNode): DesignNode {
  return {
    ...node,
    configuration: createNodeConfiguration(node.component, node.configuration)
  };
}

function hasBrowserSavedDesign() {
  try {
    return Boolean(window.localStorage.getItem(browserStorageKey));
  } catch {
    return false;
  }
}

function isCanvasState(value: unknown): value is CanvasState {
  if (!isRecord(value) || !Array.isArray(value.nodes) || !Array.isArray(value.edges)) {
    return false;
  }

  return value.nodes.every(isDesignNode) && value.edges.every(isDesignEdge);
}

function isDesignNode(value: unknown): value is DesignNode {
  return (
    isRecord(value) &&
    typeof value.id === "string" &&
    isRecord(value.component) &&
    typeof value.component.id === "string" &&
    typeof value.component.name === "string" &&
    typeof value.component.description === "string" &&
    isNodeStatus(value.status) &&
    typeof value.x === "number" &&
    typeof value.y === "number"
  );
}

function isDesignEdge(value: unknown): value is DesignEdge {
  return (
    isRecord(value) &&
    typeof value.id === "string" &&
    typeof value.fromNodeId === "string" &&
    typeof value.toNodeId === "string"
  );
}

function isNodeStatus(value: unknown): value is NodeStatus {
  return value === "Online" || value === "Degraded" || value === "Saturated" || value === "Offline";
}

function isPan(value: unknown): value is CanvasPan {
  return isRecord(value) && typeof value.x === "number" && typeof value.y === "number";
}

function isRecord(value: unknown): value is Record<string, unknown> {
  return typeof value === "object" && value !== null;
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
    configuration: cloneNodeConfiguration(node.configuration),
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













