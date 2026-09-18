import Image from "next/image";
import type { PointerEvent } from "react";
import type { ComponentLibraryItem } from "@/components/component-library/componentLibraryItems";

export type DesignNode = {
  id: string;
  component: ComponentLibraryItem;
  x: number;
  y: number;
};

type DesignNodeCardProps = {
  node: DesignNode;
  onPointerDown: (event: PointerEvent<HTMLButtonElement>, node: DesignNode) => void;
};

export function DesignNodeCard({ node, onPointerDown }: DesignNodeCardProps) {
  return (
    <button
      type="button"
      onPointerDown={(event) => onPointerDown(event, node)}
      className="absolute flex h-20 w-20 touch-none cursor-grab select-none items-center justify-center rounded-md border border-emerald-500/50 bg-zinc-900 shadow-xl shadow-black/30 transition hover:border-emerald-300 active:cursor-grabbing"
      style={{ left: node.x, top: node.y }}
      aria-label={`Move ${node.component.name}`}
    >
      <Image
        src={node.component.activeImageSrc ?? node.component.imageSrc}
        alt=""
        width={52}
        height={52}
        className="h-[52px] w-[52px] object-contain"
        draggable={false}
      />
    </button>
  );
}

