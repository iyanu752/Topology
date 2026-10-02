"use client";

import { useState } from "react";
import type { SimulationConfiguration, SimulationResult, ScheduledSimulationEvent } from "@/types";
import { SimulationScheduleEditor, type SimulationTarget } from "./SimulationScheduleEditor";
import { SimulationPlayback } from "./SimulationPlayback";

export type SimulationPanelConfig = SimulationConfiguration;

type Props = {
  onRun: (config: SimulationPanelConfig) => void;
  onCancel: () => void;
  onExample: () => void;
  busy: boolean;
  error: string | null;
  result: SimulationResult | null;
  frame: number;
  onFrame: (frame: number) => void;
  targets: SimulationTarget[];
};

export function SimulationPanel({ onRun, onCancel, onExample, busy, error, result, frame, onFrame, targets }: Props) {
  const [traffic, setTraffic] = useState(100);
  const [duration, setDuration] = useState(10);
  const [seed, setSeed] = useState(1);
  const [reads, setReads] = useState(70);
  const [timeout, setTimeoutMs] = useState(1000);
  const [delay, setDelay] = useState(1);
  const [serviceTime, setServiceTime] = useState(5);
  const [serviceSlots, setServiceSlots] = useState(16);
  const [pool, setPool] = useState(8);
  const [dbSlots, setDbSlots] = useState(4);
  const [readTime, setReadTime] = useState(10);
  const [writeTime, setWriteTime] = useState(20);
  const [queue, setQueue] = useState(100);
  const [events, setEvents] = useState<ScheduledSimulationEvent[]>([]);
  const [policy, setPolicy] = useState<"RoundRobin" | "LeastConnections">("RoundRobin");
  const [healthInterval, setHealthInterval] = useState(100);
  const [detectionDelay, setDetectionDelay] = useState(100);
  const [keys, setKeys] = useState(100);
  const [cacheCapacity, setCacheCapacity] = useState(100);
  const [cacheTtl, setCacheTtl] = useState(30000);
  const [jobCapacity, setJobCapacity] = useState(100);
  const [workerSlots, setWorkerSlots] = useState(2);
  const [workerTime, setWorkerTime] = useState(10);
  const [deliveries, setDeliveries] = useState(3);
  const [ackTimeout, setAckTimeout] = useState(30000);

  function field(label: string, value: number, set: (value: number) => void, min: number, max: number, step = 1) {
    return <label className="block text-xs text-zinc-400">{label}<input aria-label={label} type="number" required min={min} max={max} step={step} value={Number.isNaN(value) ? "" : value}
      onChange={e => set(e.target.valueAsNumber)} className="mt-1 w-full rounded border border-zinc-700 bg-zinc-900 p-2 text-zinc-100" /></label>;
  }

  return <section data-canvas-interactive="true" aria-label="Simulation panel"
    className="fixed left-4 top-4 z-30 max-h-[calc(100vh-2rem)] w-[340px] max-w-[calc(100vw-2rem)] overflow-y-auto rounded-lg border border-zinc-800 bg-zinc-950/95 p-4 text-zinc-100 shadow-2xl md:left-24">
    <h2 className="text-sm font-semibold">Simulation</h2>
    <p className="mt-1 text-xs text-zinc-400">Connect client → service → database. Optionally add a load balancer before the service, a cache beside it, or service → queue → worker → database.</p>
    <button type="button" disabled={busy} onClick={onExample} className="my-2 text-xs text-emerald-300 underline disabled:opacity-50">Add a sample to an empty canvas</button>
    <form onSubmit={e => { e.preventDefault(); onRun({ durationSeconds: duration, randomSeed: seed, metricIntervalMs: 250,
      workload: { requestsPerSecond: traffic, readPercentage: reads, writePercentage: 100 - reads, clientTimeoutMs: timeout, keySpaceSize: keys },
      scheduledEvents: events,
      routing: { policy, healthCheckIntervalMs: healthInterval, detectionDelayMs: detectionDelay },
      cache: { capacity: cacheCapacity, ttlMs: cacheTtl },
      queue: { capacity: jobCapacity, workerConcurrency: workerSlots, workerProcessingMs: workerTime, maxDeliveries: deliveries, acknowledgementTimeoutMs: ackTimeout },
      defaults: { networkDelayMs: delay, serviceProcessingMs: serviceTime, serviceConcurrency: serviceSlots,
        databasePoolSize: pool, databaseConcurrency: dbSlots, databaseReadProcessingMs: readTime, databaseWriteProcessingMs: writeTime,
        serviceQueueCapacity: queue, databasePoolQueueCapacity: queue, databaseQueueCapacity: queue }
    }); }}>
      <fieldset disabled={busy} className="space-y-3 disabled:opacity-60">
        <div className="grid grid-cols-2 gap-2">
          {field("Requests / second", traffic, setTraffic, 0, 100000)}
          {field("Duration (seconds)", duration, setDuration, 1, 3600)}
          {field("Random seed", seed, setSeed, -2147483648, 2147483647)}
          {field("Reads (%)", reads, setReads, 0, 100)}
          {field("Timeout (ms)", timeout, setTimeoutMs, 1, 3600000)}
          {field("Network delay (ms)", delay, setDelay, 0, 3600000, .1)}
        </div>
        <details><summary className="cursor-pointer text-xs text-zinc-300">Service and database settings</summary>
          <div className="mt-2 grid grid-cols-2 gap-2">
            {field("Service time (ms)", serviceTime, setServiceTime, .1, 3600000, .1)}
            {field("Service slots", serviceSlots, setServiceSlots, 1, 100000)}
            {field("Pool connections", pool, setPool, 1, 100000)}
            {field("Database slots", dbSlots, setDbSlots, 1, 100000)}
            {field("Read time (ms)", readTime, setReadTime, .1, 3600000, .1)}
            {field("Write time (ms)", writeTime, setWriteTime, .1, 3600000, .1)}
            {field("Waiting slots per queue", queue, setQueue, 0, 100000)}
          </div>
        </details>
        <details><summary className="cursor-pointer text-xs text-zinc-300">Replicas, cache and background jobs</summary>
          <p className="my-2 text-xs text-zinc-500">Set service and worker replica counts in their node properties. Each replica has its own slots. Queue capacity includes unacknowledged jobs.</p>
          <label className="block text-xs text-zinc-400">Routing<select aria-label="Routing policy" className="my-2 w-full rounded bg-zinc-900 p-2" value={policy} onChange={e => setPolicy(e.target.value as typeof policy)}><option value="RoundRobin">Round robin</option><option value="LeastConnections">Least connections</option></select></label>
          <div className="grid grid-cols-2 gap-2">
            {field("Health check (ms)", healthInterval, setHealthInterval, 1, 3600000)}
            {field("Detection delay (ms)", detectionDelay, setDetectionDelay, 0, 3600000)}
            {field("Distinct request keys", keys, setKeys, 1, 100000)}
            {field("Cache entries", cacheCapacity, setCacheCapacity, 0, 100000)}
            {field("Cache TTL (ms)", cacheTtl, setCacheTtl, 1, 3600000)}
            {field("Job queue capacity", jobCapacity, setJobCapacity, 0, 100000)}
            {field("Worker slots", workerSlots, setWorkerSlots, 1, 1000)}
            {field("Worker time (ms)", workerTime, setWorkerTime, .1, 3600000, .1)}
            {field("Maximum deliveries", deliveries, setDeliveries, 1, 100)}
            {field("Job acknowledgement (ms)", ackTimeout, setAckTimeout, 1, 3600000)}
          </div>
        </details>
        <SimulationScheduleEditor events={events} onChange={setEvents} targets={targets} duration={duration} />
        <button className="w-full rounded bg-emerald-400 px-3 py-2 text-sm font-semibold text-zinc-950" type="submit">{busy ? "Running…" : "Run simulation"}</button>
      </fieldset>
    </form>
    {busy && <button type="button" onClick={onCancel} className="mt-2 w-full text-sm text-zinc-300">Cancel run</button>}
    {error && <p role="alert" className="mt-3 text-sm text-red-300">{error}</p>}
    {result?.execution && <SimulationPlayback execution={result.execution} frame={frame} onFrame={onFrame} />}
  </section>;
}
