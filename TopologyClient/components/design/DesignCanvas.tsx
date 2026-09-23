"use client";

import { useRef, useState } from "react";
import type { ComponentLibraryItem } from "@/components/component-library/componentLibraryItems";
import { dragPayloadType } from "@/components/component-library/ComponentLibraryItem";
import { WelcomePanel } from "@/components/home/WelcomePanel";
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

const nodeSize = 80;
const zoomStep = 0.1;
const minZoom = 0.5;
const maxZoom = 2;
const emptyCanvasState: CanvasState = { nodes: [], edges: [] };

export function DesignCanvas() {
  const canvasRef = useRef<HTMLDivElement>(null);
  const [history, setHistory] = useState<CanvasHistory>({ past: [], present: emptyCanvasState, future: [] });
  const [activeDrag, setActiveDrag] = useState<ActiveDrag | null>(null);
  const [activeConnection, setActiveConnection] = useState<ActiveConnection | null>(null);
  const [nodeContextMenu, setNodeContextMenu] = useState<NodeContextMenuState | null>(null);
  const [canvasContextMenu, setCanvasContextMenu] = useState<CanvasContextMenuState | null>(null);
  const [clipboardNode, setClipboardNode] = useState<DesignNode | null>(null);
  const [zoom, setZoom] = useState(1);

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

    const position = getCanvasPosition(event.clientX, event.clientY, bounds, zoom);
    const newNode: DesignNode = {
      id: `${component.id}-${crypto.randomUUID()}`,
      component,
      x: position.x,
      y: position.y
    };

    commitCanvasState({ nodes: [...nodes, newNode], edges });
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
      offsetX: event.clientX - bounds.left - node.x * zoom,
      offsetY: event.clientY - bounds.top - node.y * zoom,
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

    if (activeConnection) {
      const pointer = getCanvasPoint(event.clientX, event.clientY, bounds, zoom);
      setActiveConnection({ ...activeConnection, toX: pointer.x, toY: pointer.y });
      return;
    }

    if (!activeDrag) {
      return;
    }

    const nextX = (event.clientX - bounds.left - activeDrag.offsetX) / zoom;
    const nextY = (event.clientY - bounds.top - activeDrag.offsetY) / zoom;
    const position = clampPosition(nextX, nextY, bounds, zoom);

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
    finishConnection(event);
    finishDrag();
  }

  function finishConnection(event: React.PointerEvent<HTMLDivElement>) {
    if (!activeConnection) {
      return;
    }

    const targetNodeId = getConnectionTargetNodeId(event.target);
    if (targetNodeId && targetNodeId !== activeConnection.fromNodeId) {
      const edgeExists = edges.some(
        (edge) => edge.fromNodeId === activeConnection.fromNodeId && edge.toNodeId === targetNodeId
      );

      if (!edgeExists) {
        const newEdge: DesignEdge = {
          id: `${activeConnection.fromNodeId}-${targetNodeId}-${crypto.randomUUID()}`,
          fromNodeId: activeConnection.fromNodeId,
          toNodeId: targetNodeId
        };

        commitCanvasState({ nodes, edges: [...edges, newEdge] });
      }
    }

    setActiveConnection(null);
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

    const position = getCanvasPosition(event.clientX, event.clientY, bounds, zoom);
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
    const bounds = canvasRef.current?.getBoundingClientRect();
    const duplicate = createDuplicateNode(node, bounds, zoom);
    commitCanvasState({ nodes: [...nodes, duplicate], edges });
  }

  function pasteCopiedNode(position?: { x: number; y: number }) {
    if (!clipboardNode) {
      return;
    }

    const bounds = canvasRef.current?.getBoundingClientRect();
    const pastedNode = position
      ? {
          ...clipboardNode,
          id: `${clipboardNode.component.id}-${crypto.randomUUID()}`,
          x: position.x,
          y: position.y
        }
      : createDuplicateNode(clipboardNode, bounds, zoom);

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
      onPointerMove={handlePointerMove}
      onPointerUp={handlePointerUp}
      onPointerCancel={() => {
        setActiveConnection(null);
        finishDrag();
      }}
      className="relative min-h-screen overflow-hidden"
      aria-label="Design room"
    >
      {nodes.length === 0 ? <WelcomePanel /> : null}

      <div
        className="absolute inset-0 origin-top-left"
        style={{ transform: `scale(${zoom})`, width: `${100 / zoom}%`, height: `${100 / zoom}%` }}
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
    </section>
  );
}

function getCanvasPoint(clientX: number, clientY: number, bounds: DOMRect, zoom: number) {
  return {
    x: (clientX - bounds.left) / zoom,
    y: (clientY - bounds.top) / zoom
  };
}

function getCanvasPosition(clientX: number, clientY: number, bounds: DOMRect, zoom: number) {
  const point = getCanvasPoint(clientX, clientY, bounds, zoom);
  return clampPosition(point.x - nodeSize / 2, point.y - nodeSize / 2, bounds, zoom);
}

function clampPosition(x: number, y: number, bounds: DOMRect, zoom: number) {
  const maxX = bounds.width / zoom - nodeSize;
  const maxY = bounds.height / zoom - nodeSize;

  return {
    x: Math.min(Math.max(0, x), Math.max(0, maxX)),
    y: Math.min(Math.max(0, y), Math.max(0, maxY))
  };
}

function createDuplicateNode(node: DesignNode, bounds: DOMRect | undefined, zoom: number) {
  const fallbackBounds = { width: 1200, height: 800 } as DOMRect;
  const availableBounds = bounds ?? fallbackBounds;
  const position = clampPosition(node.x + 28, node.y + 28, availableBounds, zoom);

  return {
    ...node,
    id: `${node.component.id}-${crypto.randomUUID()}`,
    x: position.x,
    y: position.y
  };
}

function getConnectionTargetNodeId(target: EventTarget) {
  if (!(target instanceof HTMLElement)) {
    return null;
  }

  return target.closest<HTMLElement>("[data-connect-node-id]")?.dataset.connectNodeId ?? null;
}

function areCanvasStatesEqual(first: CanvasState, second: CanvasState) {
  return JSON.stringify(first) === JSON.stringify(second);
}

function roundZoom(value: number) {
  return Math.round(value * 10) / 10;
}
