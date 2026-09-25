import type { DesignNode } from "./DesignNodeCard";
import { ContextMenuButton } from "./CanvasContextMenu";

type NodeContextMenuProps = {
  node: DesignNode;
  onClose: () => void;
  onCopy: (node: DesignNode) => void;
  onCut: (node: DesignNode) => void;
  onDelete: (node: DesignNode) => void;
  onDuplicate: (node: DesignNode) => void;
  x: number;
  y: number;
};

export function NodeContextMenu({ node, onClose, onCopy, onCut, onDelete, onDuplicate, x, y }: NodeContextMenuProps) {
  return (
    <div
      data-canvas-interactive="true"
      className="fixed z-40 w-44 rounded-md border border-zinc-800 bg-zinc-950 p-1 text-sm text-zinc-200 shadow-2xl shadow-black/40"
      style={{ left: x, top: y }}
      role="menu"
      onClick={(event) => event.stopPropagation()}
      onContextMenu={(event) => {
        event.preventDefault();
        event.stopPropagation();
      }}
      onPointerDown={(event) => event.stopPropagation()}
    >
      <ContextMenuButton onClick={() => { onCopy(node); onClose(); }}>Copy</ContextMenuButton>
      <ContextMenuButton onClick={() => { onCut(node); onClose(); }}>Cut</ContextMenuButton>
      <ContextMenuButton onClick={() => { onDuplicate(node); onClose(); }}>Duplicate</ContextMenuButton>
      <ContextMenuButton danger onClick={() => { onDelete(node); onClose(); }}>Delete</ContextMenuButton>
    </div>
  );
}
