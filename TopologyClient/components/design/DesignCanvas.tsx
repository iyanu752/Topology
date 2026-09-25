"use client";

import { useRef, useState } from "react";
import type { ComponentLibraryItem } from "@/components/component-library/componentLibraryItems";
import { dragPayloadType } from "@/components/component-library/ComponentLibraryItem";
import { WelcomePanel } from "@/components/home/WelcomePanel";
import { connectionRuleService } from "@/services";
import type { ComponentType } from "@/types";
import { ApiError } from "@/services/apiClient";
import { CanvasContextMenu } from "./CanvasContextMenu";
import { CanvasToolbar } from "./CanvasToolbar";
import { DesignEdgeLayer } from "./DesignEdgeLayer";
import { DesignNodeCard, type DesignEdge, type DesignNode } from "./DesignNodeCard";
import { NodeContextMenu } from "./NodeContextMenu";

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

const nodeSize = 80;
const canvasWorldSize = 4000;
const zoomStep = 0.1;
const minZoom = 0.5;
const maxZoom = 2;
const emptyCanvasState: CanvasState = { nodes: [], edges: [] };

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
  const [zoom, setZoom] = useState(1);
  const [pan, setPan] = useState<CanvasPan>({ x: 0, y: 0 });

  const { nodes, edges } = history.present;

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
      x: position.x,
      y: position.y
    };

    commitCanvasState({ nodes: [...nodes, newNode], edges });
  }

  function handleCanvasPointerDown(event: React.PointerEvent<HTMLDivElement>) {
    if (event.button !== 0 || isCanvasInteractionTarget(event.target)) {
      return;
    }

    event.currentTarget.setPointerCapture(event.pointerId);
    closeContextMenus();
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
      toX: node.x + nodeSize / 2,
      toY: node.y + nodeSize / 2
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
    const sourceType = componentTypeByLibraryId[sourceNode.component.id];
    const targetType = componentTypeByLibraryId[targetNode.component.id];

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
    commitCanvasState({
      nodes: nodes.filter((currentNode) => currentNode.id !== node.id),
      edges: edges.filter((edge) => edge.fromNodeId !== node.id && edge.toNodeId !== node.id)
    });
  }

  function duplicateNode(node: DesignNode) {
    const duplicate = createDuplicateNode(node);
    commitCanvasState({ nodes: [...nodes, duplicate], edges });
  }

  function pasteCopiedNode(position?: { x: number; y: number }) {
    if (!clipboardNode) {
      return;
    }

    const pastedNode = position
      ? {
          ...clipboardNode,
          id: `${clipboardNode.component.id}-${crypto.randomUUID()}`,
          x: position.x,
          y: position.y
        }
      : createDuplicateNode(clipboardNode);

    commitCanvasState({ nodes: [...nodes, pastedNode], edges });
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
        <DesignEdgeLayer draftEdge={activeConnection} edges={edges} nodes={nodes} />

        {nodes.map((node) => (
          <DesignNodeCard
            key={node.id}
            node={node}
            onConnectionStart={handleConnectionStart}
            onContextMenu={handleNodeContextMenu}
            onPointerDown={handleNodePointerDown}
          />
        ))}
      </div>

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

function getCanvasPoint(clientX: number, clientY: number, bounds: DOMRect, zoom: number, pan: CanvasPan) {
  return {
    x: (clientX - bounds.left - pan.x) / zoom,
    y: (clientY - bounds.top - pan.y) / zoom
  };
}

function getCanvasPosition(clientX: number, clientY: number, bounds: DOMRect, zoom: number, pan: CanvasPan) {
  const point = getCanvasPoint(clientX, clientY, bounds, zoom, pan);
  return clampPosition(point.x - nodeSize / 2, point.y - nodeSize / 2);
}

function clampPosition(x: number, y: number) {
  const maxPosition = canvasWorldSize - nodeSize;

  return {
    x: Math.min(Math.max(0, x), maxPosition),
    y: Math.min(Math.max(0, y), maxPosition)
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
