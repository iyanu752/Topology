"use client";

import { useState } from "react";
import type { SimulationConfiguration, SimulationResult } from "@/types";
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
};

export function SimulationPanel({ onRun, onCancel, onExample, busy, error, result, frame, onFrame }: Props) {
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

  function field(label: string, value: number, set: (value: number) => void, min: number, max: number, step = 1) {
    return <label className="block text-xs text-zinc-400">{label}<input aria-label={label} type="number" required min={min} max={max} step={step} value={Number.isNaN(value) ? "" : value}
      onChange={e => set(e.target.valueAsNumber)} className="mt-1 w-full rounded border border-zinc-700 bg-zinc-900 p-2 text-zinc-100" /></label>;
  }

  return <section data-canvas-interactive="true" aria-label="Simulation panel"
    className="fixed left-4 top-4 z-30 max-h-[calc(100vh-2rem)] w-[340px] max-w-[calc(100vw-2rem)] overflow-y-auto rounded-lg border border-zinc-800 bg-zinc-950/95 p-4 text-zinc-100 shadow-2xl md:left-24">
    <h2 className="text-sm font-semibold">Simulation</h2>
    <p className="mt-1 text-xs text-zinc-400">Connect one client → service → database. Each service and database needs one replica.</p>
    <button type="button" disabled={busy} onClick={onExample} className="my-2 text-xs text-emerald-300 underline disabled:opacity-50">Add a sample to an empty canvas</button>
    <form onSubmit={e => { e.preventDefault(); onRun({ durationSeconds: duration, randomSeed: seed, metricIntervalMs: 250,
      workload: { requestsPerSecond: traffic, readPercentage: reads, writePercentage: 100 - reads, clientTimeoutMs: timeout },
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
        <button className="w-full rounded bg-emerald-400 px-3 py-2 text-sm font-semibold text-zinc-950" type="submit">{busy ? "Running…" : "Run simulation"}</button>
      </fieldset>
    </form>
    {busy && <button type="button" onClick={onCancel} className="mt-2 w-full text-sm text-zinc-300">Cancel run</button>}
    {error && <p role="alert" className="mt-3 text-sm text-red-300">{error}</p>}
    {result?.execution && <SimulationPlayback execution={result.execution} frame={frame} onFrame={onFrame} />}
  </section>;
}
