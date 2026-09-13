import Image from "next/image";

type LibraryComponent = {
  id: string;
  name: string;
  description: string;
  imageSrc: string;
};

const components: LibraryComponent[] = [
  {
    id: "database",
    name: "Database",
    description: "Stores persistent application data.",
    imageSrc: "/assets/component-library/database/inactive.png"
  }
];

export function ComponentLibrarySidebar() {
  return (
    <aside
      aria-label="Component library"
      className="fixed inset-x-4 bottom-4 z-20 rounded-lg border border-zinc-800 bg-zinc-950/95 p-2 shadow-2xl shadow-black/30 backdrop-blur md:inset-x-auto md:bottom-auto md:left-4 md:top-1/2 md:w-[76px] md:-translate-y-1/2 md:p-3"
    >
      <div className="flex items-center justify-center gap-2 md:flex-col md:gap-3">
        {components.map((component) => (
          <button
            key={component.id}
            type="button"
            aria-label={component.name}
            className="group relative flex h-14 w-14 shrink-0 items-center justify-center rounded-md border border-zinc-800 bg-zinc-900 transition hover:border-emerald-400 hover:bg-zinc-800 focus:outline-none focus:ring-2 focus:ring-emerald-400 md:h-12 md:w-12"
          >
            <Image
              src={component.imageSrc}
              alt=""
              width={36}
              height={36}
              className="h-9 w-9 object-contain md:h-8 md:w-8"
              priority
            />

            <span className="pointer-events-none absolute bottom-[calc(100%+10px)] left-1/2 hidden min-w-32 -translate-x-1/2 rounded-md border border-zinc-700 bg-zinc-950 px-3 py-2 text-left text-xs text-zinc-200 shadow-xl group-hover:block group-focus-visible:block md:bottom-auto md:left-[calc(100%+12px)] md:top-1/2 md:translate-x-0 md:-translate-y-1/2">
              <strong className="block text-sm font-medium text-zinc-50">{component.name}</strong>
              <span className="mt-1 block leading-5 text-zinc-400">{component.description}</span>
            </span>
          </button>
        ))}
      </div>
    </aside>
  );
}

