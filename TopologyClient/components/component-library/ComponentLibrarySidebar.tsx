import { ComponentLibraryItem } from "./ComponentLibraryItem";
import { componentLibraryItems } from "./componentLibraryItems";

export function ComponentLibrarySidebar() {
  return (
    <aside
      aria-label="Component library"
      className="fixed inset-x-4 bottom-4 z-20 rounded-lg border border-zinc-800 bg-zinc-950/95 p-2 shadow-2xl shadow-black/30 backdrop-blur md:inset-x-auto md:bottom-auto md:left-4 md:top-1/2 md:w-[76px] md:-translate-y-1/2 md:p-3"
    >
      <div className="flex items-center justify-center gap-2 md:flex-col md:gap-3">
        {componentLibraryItems.map((item) => (
          <ComponentLibraryItem key={item.id} item={item} />
        ))}
      </div>
    </aside>
  );
}
