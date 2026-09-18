"use client";

import Image from "next/image";
import type { ComponentLibraryItem as ComponentLibraryItemType } from "./componentLibraryItems";

type ComponentLibraryItemProps = {
  item: ComponentLibraryItemType;
};

const dragPayloadType = "application/x-topology-component";

export function ComponentLibraryItem({ item }: ComponentLibraryItemProps) {
  return (
    <button
      type="button"
      draggable
      aria-label={item.name}
      onDragStart={(event) => {
        event.dataTransfer.setData(dragPayloadType, JSON.stringify(item));
        event.dataTransfer.effectAllowed = "copy";
      }}
      className="group relative flex h-14 w-14 shrink-0 cursor-grab items-center justify-center rounded-md border border-zinc-800 bg-zinc-900 transition hover:border-emerald-400 hover:bg-zinc-800 focus:outline-none focus:ring-2 focus:ring-emerald-400 active:cursor-grabbing md:h-12 md:w-12"
    >
      <Image
        src={item.imageSrc}
        alt=""
        width={36}
        height={36}
        className="h-9 w-9 object-contain md:h-8 md:w-8"
        priority
      />

      <span className="pointer-events-none absolute bottom-[calc(100%+10px)] left-1/2 hidden min-w-32 -translate-x-1/2 rounded-md border border-zinc-700 bg-zinc-950 px-3 py-2 text-left text-xs text-zinc-200 shadow-xl group-hover:block group-focus-visible:block md:bottom-auto md:left-[calc(100%+12px)] md:top-1/2 md:translate-x-0 md:-translate-y-1/2">
        <strong className="block text-sm font-medium text-zinc-50">{item.name}</strong>
        <span className="mt-1 block leading-5 text-zinc-400">{item.description}</span>
      </span>
    </button>
  );
}

export { dragPayloadType };

