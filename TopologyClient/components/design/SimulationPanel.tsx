"use client";

import { useState } from "react";
import type { SimulationScenario } from "@/types";

export type SimulationPanelConfig = {
  scenario: SimulationScenario;
  trafficPerSecond: number;
  readPercentage: number;
  writePercentage: number;
  hasCriticalWrites: boolean;
};

type SimulationPanelProps = {
  onRun: (config: SimulationPanelConfig) => void;
};

const scenarios: Array<{ value: SimulationScenario; label: string }> = [
  { value: "NormalTraffic", label: "Normal traffic" },
  { value: "HighTraffic", label: "High traffic" },
  { value: "DatabaseFailure", label: "Database failure" },
  { value: "CacheFailure", label: "Cache failure" },
  { value: "QueueBacklog", label: "Queue backlog" },
  { value: "ExternalApiFailure", label: "External API failure" },
  { value: "HighLatency", label: "High latency" },
  { value: "ReadHeavyWorkload", label: "Read-heavy workload" },
  { value: "WriteHeavyWorkload", label: "Write-heavy workload" },
  { value: "SuddenUserGrowth", label: "Sudden user growth" }
];

export function SimulationPanel({ onRun }: SimulationPanelProps) {
  const [scenario, setScenario] = useState<SimulationScenario>("HighTraffic");
  const [trafficPerSecond, setTrafficPerSecond] = useState(1200);
  const [readPercentage, setReadPercentage] = useState(70);
  const writePercentage = 100 - readPercentage;
  const [hasCriticalWrites, setHasCriticalWrites] = useState(false);

  return (
    <section
      data-canvas-interactive="true"
      className="fixed left-4 top-4 z-30 w-[340px] max-w-[calc(100vw-2rem)] rounded-lg border border-zinc-800 bg-zinc-950/95 p-4 text-zinc-100 shadow-2xl shadow-black/40 backdrop-blur md:left-24"
      aria-label="Simulation panel"
    >
      <div className="flex items-center justify-between gap-3">
        <div>
          <h2 className="text-sm font-semibold uppercase">Simulation</h2>
          <p className="mt-1 text-xs text-zinc-500">{trafficPerSecond.toLocaleString()} req/s</p>
        </div>
        <button
          type="button"
          onClick={() => onRun({ scenario, trafficPerSecond, readPercentage, writePercentage, hasCriticalWrites })}
          className="rounded-md border border-emerald-400/40 bg-emerald-400/10 px-3 py-2 text-sm font-semibold text-emerald-200 transition hover:bg-emerald-400/20 focus:outline-none focus:ring-2 focus:ring-emerald-400"
        >
          Run
        </button>
      </div>

      <div className="mt-4 space-y-4">
        <label className="block">
          <span className="text-[11px] font-semibold uppercase text-zinc-500">Scenario</span>
          <select
            value={scenario}
            onChange={(event) => setScenario(event.target.value as SimulationScenario)}
            className="mt-2 h-10 w-full rounded-md border border-zinc-800 bg-zinc-900 px-3 text-sm text-zinc-100 outline-none transition focus:border-emerald-400 focus:ring-2 focus:ring-emerald-400/30"
          >
            {scenarios.map((option) => (
              <option key={option.value} value={option.value}>
                {option.label}
              </option>
            ))}
          </select>
        </label>

        <label className="block">
          <span className="flex items-center justify-between text-[11px] font-semibold uppercase text-zinc-500">
            <span>Traffic</span>
            <span>{trafficPerSecond.toLocaleString()} req/s</span>
          </span>
          <input
            type="range"
            min="10"
            max="20000"
            step="10"
            value={trafficPerSecond}
            onChange={(event) => setTrafficPerSecond(Number(event.target.value))}
            className="mt-3 w-full accent-emerald-300"
          />
        </label>

        <label className="block">
          <span className="flex items-center justify-between text-[11px] font-semibold uppercase text-zinc-500">
            <span>Read / write</span>
            <span>{readPercentage}% / {writePercentage}%</span>
          </span>
          <input
            type="range"
            min="0"
            max="100"
            step="5"
            value={readPercentage}
            onChange={(event) => setReadPercentage(Number(event.target.value))}
            className="mt-3 w-full accent-emerald-300"
          />
        </label>

        <label className="flex items-center justify-between gap-3 rounded-md border border-zinc-800 bg-zinc-900 px-3 py-2">
          <span className="text-sm text-zinc-300">Critical writes</span>
          <input
            type="checkbox"
            checked={hasCriticalWrites}
            onChange={(event) => setHasCriticalWrites(event.target.checked)}
            className="h-4 w-4 accent-emerald-300"
          />
        </label>
      </div>
    </section>
  );
}
