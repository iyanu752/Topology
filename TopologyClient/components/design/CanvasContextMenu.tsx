import type { MouseEvent, PointerEvent, ReactNode } from "react";

type CanvasContextMenuProps = {
  canPaste: boolean;
  onClose: () => void;
  onPaste: () => void;
  x: number;
  y: number;
};

export function CanvasContextMenu({ canPaste, onClose, onPaste, x, y }: CanvasContextMenuProps) {
  return (
    <div
      data-canvas-interactive="true"
      className="fixed z-40 w-44 rounded-md border border-zinc-800 bg-zinc-950 p-1 text-sm text-zinc-200 shadow-2xl shadow-black/40"
      style={{ left: x, top: y }}
      role="menu"
      onClick={stopMousePropagation}
      onContextMenu={(event) => {
        event.preventDefault();
        event.stopPropagation();
      }}
      onPointerDown={stopPointerPropagation}
    >
      {canPaste ? (
        <ContextMenuButton
          onClick={() => {
            onPaste();
            onClose();
          }}
        >
          Paste
        </ContextMenuButton>
      ) : (
        <div className="px-3 py-2 text-zinc-500">No actions available</div>
      )}
    </div>
  );
}

type ContextMenuButtonProps = {
  children: ReactNode;
  danger?: boolean;
  onClick: () => void;
};

export function ContextMenuButton({ children, danger = false, onClick }: ContextMenuButtonProps) {
  return (
    <button
      type="button"
      onClick={(event) => {
        event.stopPropagation();
        onClick();
      }}
      onPointerDown={stopPointerPropagation}
      className={`block w-full rounded px-3 py-2 text-left transition hover:bg-zinc-900 focus:outline-none focus:ring-2 focus:ring-inset focus:ring-emerald-400 ${
        danger ? "text-red-400 hover:text-red-300" : "text-zinc-200 hover:text-zinc-50"
      }`}
      role="menuitem"
    >
      {children}
    </button>
  );
}

function stopMousePropagation(event: MouseEvent<HTMLDivElement>) {
  event.stopPropagation();
}

function stopPointerPropagation(event: PointerEvent<HTMLElement>) {
  event.stopPropagation();
}
