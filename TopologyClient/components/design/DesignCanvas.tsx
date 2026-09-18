"use client";

import { useRef, useState } from "react";
import type { ComponentLibraryItem } from "@/components/component-library/componentLibraryItems";
import { dragPayloadType } from "@/components/component-library/ComponentLibraryItem";
import { WelcomePanel } from "@/components/home/WelcomePanel";
import { DesignNodeCard, type DesignNode } from "./DesignNodeCard";

type ActiveDrag = {
  nodeId: string;
  offsetX: number;
  offsetY: number;
};

const nodeSize = 80;

export function DesignCanvas() {
  const canvasRef = useRef<HTMLDivElement>(null);
  const [nodes, setNodes] = useState<DesignNode[]>([]);
  const [activeDrag, setActiveDrag] = useState<ActiveDrag | null>(null);

  function handleDrop(event: React.DragEvent<HTMLDivElement>) {
    event.preventDefault();

    const payload = event.dataTransfer.getData(dragPayloadType);
    if (!payload) {
      return;
    }

    const component = JSON.parse(payload) as ComponentLibraryItem;
    const bounds = canvasRef.current?.getBoundingClientRect();
    if (!bounds) {
      return;
    }

    const position = clampPosition(event.clientX - bounds.left - nodeSize / 2, event.clientY - bounds.top - nodeSize / 2, bounds);

    setNodes((currentNodes) => [
      ...currentNodes,
      {
        id: `${component.id}-${crypto.randomUUID()}`,
        component,
        x: position.x,
        y: position.y
      }
    ]);
  }

  function handleNodePointerDown(event: React.PointerEvent<HTMLButtonElement>, node: DesignNode) {
    const bounds = canvasRef.current?.getBoundingClientRect();
    if (!bounds) {
      return;
    }

    event.currentTarget.setPointerCapture(event.pointerId);
    setActiveDrag({
      nodeId: node.id,
      offsetX: event.clientX - bounds.left - node.x,
      offsetY: event.clientY - bounds.top - node.y
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

    const position = clampPosition(event.clientX - bounds.left - activeDrag.offsetX, event.clientY - bounds.top - activeDrag.offsetY, bounds);

    setNodes((currentNodes) =>
      currentNodes.map((node) => (node.id === activeDrag.nodeId ? { ...node, x: position.x, y: position.y } : node))
    );
  }

  return (
    <section
      ref={canvasRef}
      onDragOver={(event) => event.preventDefault()}
      onDrop={handleDrop}
      onPointerMove={handlePointerMove}
      onPointerUp={() => setActiveDrag(null)}
      onPointerCancel={() => setActiveDrag(null)}
      className="relative min-h-screen overflow-hidden"
      aria-label="Design room"
    >
      {nodes.length === 0 ? <WelcomePanel /> : null}

      {nodes.map((node) => (
        <DesignNodeCard key={node.id} node={node} onPointerDown={handleNodePointerDown} />
      ))}
    </section>
  );
}

function clampPosition(x: number, y: number, bounds: DOMRect) {
  return {
    x: Math.min(Math.max(0, x), Math.max(0, bounds.width - nodeSize)),
    y: Math.min(Math.max(0, y), Math.max(0, bounds.height - nodeSize))
  };
}
