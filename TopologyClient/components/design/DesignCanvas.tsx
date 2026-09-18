"use client";

import { useRef, useState } from "react";
import type { ComponentLibraryItem } from "@/components/component-library/componentLibraryItems";
import { dragPayloadType } from "@/components/component-library/ComponentLibraryItem";
import { WelcomePanel } from "@/components/home/WelcomePanel";
import { CanvasContextMenu } from "./CanvasContextMenu";
import { CanvasToolbar } from "./CanvasToolbar";
import { DesignNodeCard, type DesignNode } from "./DesignNodeCard";
import { NodeContextMenu } from "./NodeContextMenu";

type ActiveDrag = {
  nodeId: string;
  offsetX: number;
  offsetY: number;
  startNodes: DesignNode[];
};

type CanvasHistory = {
  past: DesignNode[][];
  present: DesignNode[];
  future: DesignNode[][];
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

export function DesignCanvas() {
  const canvasRef = useRef<HTMLDivElement>(null);
  const [history, setHistory] = useState<CanvasHistory>({ past: [], present: [], future: [] });
  const [activeDrag, setActiveDrag] = useState<ActiveDrag | null>(null);
  const [nodeContextMenu, setNodeContextMenu] = useState<NodeContextMenuState | null>(null);
  const [canvasContextMenu, setCanvasContextMenu] = useState<CanvasContextMenuState | null>(null);
  const [clipboardNode, setClipboardNode] = useState<DesignNode | null>(null);
  const [zoom, setZoom] = useState(1);

  const nodes = history.present;

  function commitNodes(nextNodes: DesignNode[]) {
    setHistory((currentHistory) => ({
      past: [...currentHistory.past, currentHistory.present],
      present: nextNodes,
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

    commitNodes([...nodes, newNode]);
  }

  function handleNodePointerDown(event: React.PointerEvent<HTMLButtonElement>, node: DesignNode) {
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
      startNodes: nodes
    });
  }

  function handlePointerMove(event: React.PointerEvent<HTMLDivElement>) {
    if (!activeDrag) {
      return;
    }

    const bounds = canvasRef.current?.getBoundingClientRect();
    if (!bounds) {
      return;
    }

    const nextX = (event.clientX - bounds.left - activeDrag.offsetX) / zoom;
    const nextY = (event.clientY - bounds.top - activeDrag.offsetY) / zoom;
    const position = clampPosition(nextX, nextY, bounds, zoom);

    setHistory((currentHistory) => ({
      ...currentHistory,
      present: currentHistory.present.map((node) =>
        node.id === activeDrag.nodeId ? { ...node, x: position.x, y: position.y } : node
      )
    }));
  }

  function finishDrag() {
    if (!activeDrag) {
      return;
    }

    setHistory((currentHistory) => {
      if (areNodeListsEqual(activeDrag.startNodes, currentHistory.present)) {
        return currentHistory;
      }

      return {
        past: [...currentHistory.past, activeDrag.startNodes],
        present: currentHistory.present,
        future: []
      };
    });
    setActiveDrag(null);
  }

  function handleNodeContextMenu(event: React.MouseEvent<HTMLButtonElement>, node: DesignNode) {
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
    commitNodes(nodes.filter((currentNode) => currentNode.id !== node.id));
  }

  function deleteNode(node: DesignNode) {
    commitNodes(nodes.filter((currentNode) => currentNode.id !== node.id));
  }

  function duplicateNode(node: DesignNode) {
    const bounds = canvasRef.current?.getBoundingClientRect();
    const duplicate = createDuplicateNode(node, bounds, zoom);
    commitNodes([...nodes, duplicate]);
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

    commitNodes([...nodes, pastedNode]);
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
      onPointerUp={finishDrag}
      onPointerCancel={finishDrag}
      className="relative min-h-screen overflow-hidden"
      aria-label="Design room"
    >
      {nodes.length === 0 ? <WelcomePanel /> : null}

      <div
        className="absolute inset-0 origin-top-left"
        style={{ transform: `scale(${zoom})`, width: `${100 / zoom}%`, height: `${100 / zoom}%` }}
      >
        {nodes.map((node) => (
          <DesignNodeCard
            key={node.id}
            node={node}
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

function getCanvasPosition(clientX: number, clientY: number, bounds: DOMRect, zoom: number) {
  const x = (clientX - bounds.left) / zoom - nodeSize / 2;
  const y = (clientY - bounds.top) / zoom - nodeSize / 2;
  return clampPosition(x, y, bounds, zoom);
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

function areNodeListsEqual(first: DesignNode[], second: DesignNode[]) {
  return JSON.stringify(first) === JSON.stringify(second);
}

function roundZoom(value: number) {
  return Math.round(value * 10) / 10;
}
