type CanvasToolbarProps = {
  canRedo: boolean;
  canUndo: boolean;
  onRedo: () => void;
  onUndo: () => void;
  onZoomIn: () => void;
  onZoomOut: () => void;
  zoomPercentage: number;
};

export function CanvasToolbar({
  canRedo,
  canUndo,
  onRedo,
  onUndo,
  onZoomIn,
  onZoomOut,
  zoomPercentage
}: CanvasToolbarProps) {
  return (
    <div data-canvas-interactive="true" className="fixed bottom-24 left-1/2 z-30 flex -translate-x-1/2 items-center gap-2 md:bottom-5">
      <div className="flex h-10 items-center overflow-hidden rounded-md border border-zinc-800 bg-zinc-950/95 text-sm shadow-2xl shadow-black/30 backdrop-blur">
        <button
          type="button"
          onClick={onZoomOut}
          className="flex h-10 w-10 items-center justify-center text-zinc-300 transition hover:bg-zinc-900 hover:text-zinc-50 focus:outline-none focus:ring-2 focus:ring-inset focus:ring-emerald-400"
          aria-label="Zoom out"
        >
          -
        </button>
        <span className="flex h-10 min-w-16 items-center justify-center border-x border-zinc-800 px-3 text-zinc-200">
          {zoomPercentage}%
        </span>
        <button
          type="button"
          onClick={onZoomIn}
          className="flex h-10 w-10 items-center justify-center text-zinc-300 transition hover:bg-zinc-900 hover:text-zinc-50 focus:outline-none focus:ring-2 focus:ring-inset focus:ring-emerald-400"
          aria-label="Zoom in"
        >
          +
        </button>
      </div>

      <button
        type="button"
        onClick={onUndo}
        disabled={!canUndo}
        className="flex h-10 w-10 items-center justify-center rounded-md border border-zinc-800 bg-zinc-950/95 text-zinc-300 shadow-2xl shadow-black/30 transition hover:bg-zinc-900 hover:text-zinc-50 disabled:cursor-not-allowed disabled:opacity-40"
        aria-label="Undo"
      >
        <svg aria-hidden="true" viewBox="0 0 24 24" className="h-5 w-5" fill="none" stroke="currentColor" strokeWidth="1.8">
          <path d="M9 8H4V3" />
          <path d="M4.5 8.5A8 8 0 1 1 7 18.3" />
        </svg>
      </button>

      <button
        type="button"
        onClick={onRedo}
        disabled={!canRedo}
        className="flex h-10 w-10 items-center justify-center rounded-md border border-zinc-800 bg-zinc-950/95 text-zinc-300 shadow-2xl shadow-black/30 transition hover:bg-zinc-900 hover:text-zinc-50 disabled:cursor-not-allowed disabled:opacity-40"
        aria-label="Redo"
      >
        <svg aria-hidden="true" viewBox="0 0 24 24" className="h-5 w-5" fill="none" stroke="currentColor" strokeWidth="1.8">
          <path d="M15 8h5V3" />
          <path d="M19.5 8.5A8 8 0 1 0 17 18.3" />
        </svg>
      </button>
    </div>
  );
}

