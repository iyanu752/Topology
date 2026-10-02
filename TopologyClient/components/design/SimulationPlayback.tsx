"use client";

import { useEffect, useState } from "react";
import type { SimulationExecutionResult } from "@/types";

export function SimulationPlayback({ execution, frame, onFrame }: { execution: SimulationExecutionResult; frame: number; onFrame: (value: number) => void }) {
  const [playing, setPlaying] = useState(false);
  const [speed, setSpeed] = useState(1);
  const samples = execution.timeline;
  const sample = samples[frame];
  useEffect(() => {
    if (!playing || frame >= samples.length - 1) return;
    const delay = Math.max(16, (samples[frame + 1].atMicroseconds - samples[frame].atMicroseconds) / 1000 / speed);
    const timer = setTimeout(() => onFrame(frame + 1), delay);
    return () => clearTimeout(timer);
  }, [playing, frame, speed, samples, onFrame]);
  const total = execution.summary.requests;
  const current = sample?.measurements.requests;
  const format = (value: number | null | undefined) => value == null ? "—" : value.toFixed(1);
  const resourceLabels = { ServiceExecution: "Service slots", DatabasePool: "DB connections", DatabaseConnections: "DB connections", DatabaseExecution: "Database slots" };
  function chart(label: string, values: number[], color: string) {
    const max = Math.max(1, ...values);
    const points = values.map((value, i) => `${i * 280 / Math.max(1, values.length - 1)},${45 - value / max * 40}`).join(" ");
    return <div className="mt-3"><div className="flex justify-between text-xs text-zinc-400"><span>{label}</span><span>0–{format(max)}</span></div>
      <svg viewBox="0 0 280 50" role="img" aria-label={label} className="w-full"><polyline fill="none" stroke={color} strokeWidth="2" points={points} /><line x1={frame * 280 / Math.max(1, values.length - 1)} x2={frame * 280 / Math.max(1, values.length - 1)} y1="0" y2="50" stroke="#fff" opacity=".4" /></svg></div>;
  }
  return <div className="mt-4 border-t border-zinc-800 pt-3">
    <p className="text-xs text-zinc-300">{execution.status === "Incomplete" ? `Stopped early: ${execution.stopReason}` : "Run complete"} · {(execution.elapsedMicroseconds / 1000000).toFixed(2)} s</p>
    <dl className="my-3 grid grid-cols-2 gap-2 text-xs">
      {[["Generated", total.generated], ["Succeeded", total.succeeded], ["Failed", total.explicitlyFailed], ["Timed out", total.timedOut], ["Still running", total.inFlight], ["Overall p95 (ms)", format(total.successfulLatency.p95Ms)]].map(([label, value]) => <div key={label}><dt className="text-zinc-500">{label}</dt><dd>{value}</dd></div>)}
    </dl>
    {sample && <>
      <div className="flex items-center justify-between gap-2 text-xs">
        <button type="button" className="rounded border border-zinc-700 px-2 py-1" onClick={() => { if (frame >= samples.length - 1) { onFrame(0); setPlaying(true); } else setPlaying(!playing); }}>{playing && frame < samples.length - 1 ? "Pause" : "Play"}</button>
        <select aria-label="Playback speed" value={speed} onChange={e => setSpeed(Number(e.target.value))} className="rounded bg-zinc-900 p-1">{[.5, 1, 2, 4].map(s => <option key={s} value={s}>{s}×</option>)}</select>
        <span>{(sample.atMicroseconds / 1000000).toFixed(2)} s</span>
      </div>
      <input aria-label="Simulation timeline" className="mt-3 w-full accent-emerald-400" type="range" min={0} max={Math.max(0, samples.length - 1)} value={frame} onChange={e => { setPlaying(false); onFrame(Number(e.target.value)); }} />
      <p className="text-xs text-zinc-400">Selected interval: {format(current?.successfulRequestsPerSecond)} successful req/s · p95 {format(current?.successfulLatency.p95Ms)} ms</p>
      {chart("Successful requests / second", samples.map(s => s.measurements.requests.successfulRequestsPerSecond ?? 0), "#34d399")}
      {chart("Waiting requests", samples.map(s => s.measurements.resources.reduce((n, r) => n + r.queueDepth, 0)), "#fbbf24")}
      <table className="mt-2 w-full text-left text-xs"><caption className="mb-1 text-left text-zinc-400">Resources at selected time</caption><thead><tr><th>Resource</th><th>Active</th><th>Waiting</th></tr></thead><tbody>{sample.measurements.resources.map(r => <tr key={r.nodeId + r.kind}><td className="py-1">{resourceLabels[r.kind]}</td><td>{r.active}/{r.capacity}</td><td>{r.queueDepth}</td></tr>)}</tbody></table>
    </>}
    {execution.unusedNodeIds.length > 0 && <p className="mt-2 text-xs text-amber-300">{execution.unusedNodeIds.length} disconnected nodes were unused.</p>}
    <details className="mt-3 text-xs text-zinc-400"><summary>Model assumptions</summary><p className="my-2">Fixed processing times; one database operation per request. CPU, memory and backups do not change these results.</p>{execution.assumptions.map(a => <p key={a} className="my-1">{a}</p>)}</details>
  </div>;
}
