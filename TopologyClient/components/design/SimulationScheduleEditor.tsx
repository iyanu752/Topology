import type { ScheduledSimulationEvent } from "@/types";

export type SimulationTarget = { id: string; label: string; replicas: number };

export function SimulationScheduleEditor({ events, onChange, targets, duration }: {
  events: ScheduledSimulationEvent[]; onChange: (events: ScheduledSimulationEvent[]) => void;
  targets: SimulationTarget[]; duration: number;
}) {
  function update(index: number, patch: Partial<ScheduledSimulationEvent>) {
    onChange(events.map((event, i) => i === index ? { ...event, ...patch } : event));
  }
  const inputClass = "w-full rounded border border-zinc-700 bg-zinc-900 p-1 text-zinc-100";
  return <details className="text-xs"><summary className="cursor-pointer text-zinc-300">Scheduled traffic, failures and recovery</summary>
    <p className="my-2 text-zinc-500">Use two traffic changes for a spike. Ramps change once per second. Times are measured from the start of the run.</p>
    {events.map((event, index) => {
      const traffic = event.type === "TrafficChange" || event.type === "TrafficRamp";
      const target = targets.find(t => t.id === event.targetNodeId);
      return <div key={index} className="my-2 space-y-2 rounded border border-zinc-800 p-2">
        <div className="flex gap-2"><select aria-label={`Event ${index + 1} type`} className={inputClass} value={event.type} onChange={e => {
          const type = e.target.value as ScheduledSimulationEvent["type"];
          onChange(events.map((old, i) => i !== index ? old : { type, atMicroseconds: old.atMicroseconds,
            ...(type === "TrafficChange" || type === "TrafficRamp" ? { requestsPerSecond: 100 } : { targetNodeId: targets[0]?.id }),
            ...(type === "TrafficRamp" ? { endAtMicroseconds: duration * 1000000, endRequestsPerSecond: 1000 } : {}) }));
        }}><option value="TrafficChange">Change traffic</option><option value="TrafficRamp">Ramp traffic</option><option value="ComponentFailure">Fail component</option><option value="ComponentRecovery">Restore component</option></select>
          <button type="button" aria-label={`Remove event ${index + 1}`} onClick={() => onChange(events.filter((_, i) => i !== index))}>Remove</button></div>
        <label className="block">At (seconds)<input aria-label={`Event ${index + 1} time`} className={inputClass} type="number" min={0} max={duration} step={.001} required value={event.atMicroseconds / 1000000} onChange={e => update(index, { atMicroseconds: Math.round(e.target.valueAsNumber * 1000000) })} /></label>
        {traffic ? <label className="block">Requests / second<input aria-label={`Event ${index + 1} rate`} className={inputClass} type="number" required min={0} max={100000} value={event.requestsPerSecond ?? 0} onChange={e => update(index, { requestsPerSecond: e.target.valueAsNumber })} /></label> : <>
          <select aria-label={`Event ${index + 1} target`} required className={inputClass} value={event.targetNodeId ?? ""} onChange={e => update(index, { targetNodeId: e.target.value, targetReplicaId: null })}><option value="">Choose component</option>{targets.map(t => <option key={t.id} value={t.id}>{t.label}</option>)}</select>
          <select aria-label={`Event ${index + 1} replica`} className={inputClass} value={event.targetReplicaId ?? ""} onChange={e => update(index, { targetReplicaId: e.target.value || null })}><option value="">All replicas</option>{Array.from({ length: Math.min(100, Math.max(1, target?.replicas ?? 1)) }, (_, i) => <option key={i} value={`${target?.id}:${i + 1}`}>Replica {i + 1}</option>)}</select>
        </>}
        {event.type === "TrafficRamp" && <div className="grid grid-cols-2 gap-2"><label>End (seconds)<input aria-label={`Event ${index + 1} end time`} required className={inputClass} type="number" min={event.atMicroseconds / 1000000 + .001} max={duration} step={.001} value={(event.endAtMicroseconds ?? 0) / 1000000} onChange={e => update(index, { endAtMicroseconds: Math.round(e.target.valueAsNumber * 1000000) })} /></label><label>End rate<input aria-label={`Event ${index + 1} end rate`} required className={inputClass} type="number" min={0} max={100000} value={event.endRequestsPerSecond ?? 0} onChange={e => update(index, { endRequestsPerSecond: e.target.valueAsNumber })} /></label></div>}
      </div>;
    })}
    <button type="button" className="my-2 text-emerald-300 underline" onClick={() => onChange([...events, { type: "TrafficChange", atMicroseconds: 0, requestsPerSecond: 100 }])}>Add scheduled event</button>
  </details>;
}
