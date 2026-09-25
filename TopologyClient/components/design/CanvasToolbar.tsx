import type { ReactNode } from "react";

type CanvasToolbarProps = {
  canLoadSavedDesign: boolean;
  canRedo: boolean;
  canUndo: boolean;
  onExportFile: () => void;
  onImportFile: () => void;
  onLoadBrowser: () => void;
  onRedo: () => void;
  onSaveBrowser: () => void;
  onUndo: () => void;
  onZoomIn: () => void;
  onZoomOut: () => void;
  zoomPercentage: number;
};

export function CanvasToolbar({
  canLoadSavedDesign,
  canRedo,
  canUndo,
  onExportFile,
  onImportFile,
  onLoadBrowser,
  onRedo,
  onSaveBrowser,
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

      <IconButton label="Undo" onClick={onUndo} disabled={!canUndo}>
        <path d="M9 8H4V3" />
        <path d="M4.5 8.5A8 8 0 1 1 7 18.3" />
      </IconButton>

      <IconButton label="Redo" onClick={onRedo} disabled={!canRedo}>
        <path d="M15 8h5V3" />
        <path d="M19.5 8.5A8 8 0 1 0 17 18.3" />
      </IconButton>

      <IconButton label="Save to browser" onClick={onSaveBrowser}>
        <path d="M5 4h11l3 3v13H5z" />
        <path d="M8 4v6h8" />
        <path d="M8 17h8" />
      </IconButton>

      <IconButton label="Load from browser" onClick={onLoadBrowser} disabled={!canLoadSavedDesign}>
        <path d="M4 6h16" />
        <path d="M6 6v13h12V6" />
        <path d="M9 12h6" />
        <path d="M12 9v6" />
      </IconButton>

      <IconButton label="Export JSON" onClick={onExportFile}>
        <path d="M12 3v12" />
        <path d="m7 10 5 5 5-5" />
        <path d="M5 21h14" />
      </IconButton>

      <IconButton label="Import JSON" onClick={onImportFile}>
        <path d="M12 21V9" />
        <path d="m7 14 5-5 5 5" />
        <path d="M5 3h14" />
      </IconButton>
    </div>
  );
}

type IconButtonProps = {
  children: ReactNode;
  disabled?: boolean;
  label: string;
  onClick: () => void;
};

function IconButton({ children, disabled = false, label, onClick }: IconButtonProps) {
  return (
    <button
      type="button"
      onClick={onClick}
      disabled={disabled}
      className="flex h-10 w-10 items-center justify-center rounded-md border border-zinc-800 bg-zinc-950/95 text-zinc-300 shadow-2xl shadow-black/30 transition hover:bg-zinc-900 hover:text-zinc-50 focus:outline-none focus:ring-2 focus:ring-emerald-400 disabled:cursor-not-allowed disabled:opacity-40"
      aria-label={label}
      title={label}
    >
      <svg aria-hidden="true" viewBox="0 0 24 24" className="h-5 w-5" fill="none" stroke="currentColor" strokeLinecap="round" strokeLinejoin="round" strokeWidth="1.8">
        {children}
      </svg>
    </button>
  );
}

