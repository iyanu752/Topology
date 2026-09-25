import type { DesignNode, NodeStatus } from "./DesignNodeCard";

type NodePropertiesPanelProps = {
  node: DesignNode;
  onClose: () => void;
  onStatusChange: (status: NodeStatus) => void;
};

const nodeStatuses: NodeStatus[] = ["Online", "Degraded", "Saturated", "Offline"];

export function NodePropertiesPanel({ node, onClose, onStatusChange }: NodePropertiesPanelProps) {
  return (
    <aside
      data-canvas-interactive="true"
      className="fixed bottom-24 right-4 top-4 z-30 flex w-[320px] max-w-[calc(100vw-2rem)] flex-col rounded-lg border border-zinc-800 bg-zinc-950/95 shadow-2xl shadow-black/40 backdrop-blur md:bottom-4"
      aria-label="Node properties"
    >
      <div className="flex items-start justify-between gap-3 border-b border-zinc-800 p-4">
        <div className="min-w-0">
          <h2 className="truncate text-sm font-semibold uppercase text-zinc-100">{node.component.name}</h2>
          <p className="mt-1 truncate text-xs text-zinc-500">{node.component.id}</p>
        </div>
        <button
          type="button"
          onClick={onClose}
          className="flex h-8 w-8 shrink-0 items-center justify-center rounded-md border border-zinc-800 text-zinc-400 transition hover:bg-zinc-900 hover:text-zinc-100 focus:outline-none focus:ring-2 focus:ring-emerald-400"
          aria-label="Close node properties"
        >
          x
        </button>
      </div>

      <div className="flex-1 space-y-5 overflow-y-auto p-4">
        <section>
          <h3 className="text-[11px] font-semibold uppercase text-zinc-500">Description</h3>
          <p className="mt-2 text-sm leading-6 text-zinc-300">{node.component.description}</p>
        </section>

        <section>
          <label htmlFor="node-status" className="text-[11px] font-semibold uppercase text-zinc-500">
            Status
          </label>
          <select
            id="node-status"
            value={node.status}
            onChange={(event) => onStatusChange(event.target.value as NodeStatus)}
            className="mt-2 h-10 w-full rounded-md border border-zinc-800 bg-zinc-900 px-3 text-sm text-zinc-100 outline-none transition focus:border-emerald-400 focus:ring-2 focus:ring-emerald-400/30"
          >
            {nodeStatuses.map((status) => (
              <option key={status} value={status}>
                {status}
              </option>
            ))}
          </select>
        </section>

        <section className="grid grid-cols-2 gap-3">
          <ReadOnlyField label="X" value={Math.round(node.x).toString()} />
          <ReadOnlyField label="Y" value={Math.round(node.y).toString()} />
        </section>

        <section>
          <h3 className="text-[11px] font-semibold uppercase text-zinc-500">Node ID</h3>
          <p className="mt-2 break-all rounded-md border border-zinc-800 bg-zinc-900 px-3 py-2 text-xs text-zinc-400">{node.id}</p>
        </section>
      </div>
    </aside>
  );
}

type ReadOnlyFieldProps = {
  label: string;
  value: string;
};

function ReadOnlyField({ label, value }: ReadOnlyFieldProps) {
  return (
    <div>
      <div className="text-[11px] font-semibold uppercase text-zinc-500">{label}</div>
      <div className="mt-2 rounded-md border border-zinc-800 bg-zinc-900 px-3 py-2 text-sm text-zinc-200">{value}</div>
    </div>
  );
}
