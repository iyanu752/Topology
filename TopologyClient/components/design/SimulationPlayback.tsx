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
  const resourceLabels = { ServiceExecution: "Service slots", DatabasePool: "Pool connections", DatabaseConnections: "DB connections", DatabaseExecution: "Database slots", WorkerExecution: "Worker slots", GatewayExecution: "Gateway slots", ExternalApiExecution: "External API slots" };
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
      {chart("Dependency retry attempts", samples.map(s => s.measurements.retries.retryAttempts), "#fb923c")}
      <p className="mt-2 text-xs text-zinc-300">This interval: {sample.measurements.retries.attempts} dependency attempts, including {sample.measurements.retries.retryAttempts} retries. Circuit breakers blocked {sample.measurements.retries.circuitRejected} calls.</p>
      {sample.measurements.endpoints.map(e => <p key={e.nodeId} className="mt-2 text-xs text-zinc-300">{e.nodeId}: {e.rateLimited} rate-limit rejections, {e.saturated} capacity rejections, {e.failed} failures this interval.</p>)}
      <details className="mt-3 text-xs text-zinc-400"><summary>Circuits at selected time</summary>{sample.measurements.retries.circuits.length === 0 ? <p>No active circuit breakers.</p> : sample.measurements.retries.circuits.map(c => <p key={c.scope} className="my-1 break-all">{c.scope}: {c.state} ({c.consecutiveFailures} consecutive failures)</p>)}</details>
      <table className="mt-3 w-full text-left text-xs"><caption className="mb-1 text-left text-zinc-400">Database replicas at selected time</caption><thead><tr><th>Replica</th><th>Role</th><th>Version</th></tr></thead><tbody>{sample.measurements.replication.replicas.map(r => <tr key={r.replicaId}><td className="max-w-28 truncate py-1" title={r.replicaId}>{r.replicaId}{!r.isAvailable && <span className="block text-red-300">Offline</span>}</td><td>{r.role}</td><td>{r.appliedVersion}</td></tr>)}</tbody></table>
      <table className="mt-2 w-full text-left text-xs"><caption className="mb-1 text-left text-zinc-400">Resources at selected time</caption><thead><tr><th>Resource</th><th>Active</th><th>Waiting</th></tr></thead><tbody>{sample.measurements.resources.map(r => <tr key={r.nodeId + r.replicaId + r.kind}><td className="py-1">{resourceLabels[r.kind]}{r.replicaId && <span className="block max-w-40 truncate text-zinc-500" title={r.replicaId}>{r.replicaId}</span>}{!r.isAvailable && <span className="text-red-300"> Offline</span>}</td><td>{r.active}/{r.capacity}</td><td>{r.queueDepth}</td></tr>)}</tbody></table>
      <p className="mt-3 text-xs text-zinc-300">Cache this interval: {sample.measurements.cache.hits} hits / {sample.measurements.cache.misses} misses · {sample.measurements.cache.entries} entries</p>
      <p className="mt-2 text-xs text-zinc-300">Jobs at this time: {sample.measurements.jobs.waiting} waiting · {sample.measurements.jobs.active} running</p>
    </>}
    <p className="mt-3 text-xs text-zinc-300">Jobs over the whole run: {execution.summary.jobs.accepted} accepted · {execution.summary.jobs.completed} completed · {execution.summary.jobs.redeliveries} redeliveries · {execution.summary.jobs.deadLettered} exhausted · {execution.summary.jobs.lost} lost</p>
    {execution.stateTransitions.length > 0 && <details className="mt-3 text-xs text-zinc-400"><summary>Failures and recovery</summary>{execution.stateTransitions.map((event, i) => <p key={i} className="my-1">{(event.atMicroseconds / 1000000).toFixed(2)} s: {event.replicaId ?? event.nodeId} — {event.reason}</p>)}</details>}
    {execution.unusedNodeIds.length > 0 && <p className="mt-2 text-xs text-amber-300">{execution.unusedNodeIds.length} disconnected nodes were unused.</p>}
    <p className="mt-3 text-xs text-zinc-300">Whole run: {execution.summary.retries.retryAttempts} dependency retries, {execution.summary.replication.promotions} promotions, {execution.summary.replication.staleReads} stale reads, {execution.summary.replication.rolledBackKeys} keys rolled back during promotion.</p>
    <details className="mt-3 text-xs text-zinc-400"><summary>Request failures</summary>{Object.entries(total.failureCountsByCause).map(([cause, count]) => <p key={cause}>{cause}: {count}</p>)}</details>
    <details className="mt-3 text-xs text-zinc-400"><summary>Model assumptions</summary><p className="my-2">Fixed processing times. Retries and redeliveries can add database operations. CPU, memory and backups do not change these results.</p>{execution.assumptions.map(a => <p key={a} className="my-1">{a}</p>)}</details>
  </div>;
}
