"use client";

import type { SimulationConfiguration } from "@/types";

type Section = "gateway" | "externalApi" | "retries" | "circuitBreaker" | "databaseReplication";
export type ReliabilitySettings = { [K in Section]: Required<NonNullable<SimulationConfiguration[K]>> };

export const initialReliabilitySettings: ReliabilitySettings = {
  gateway: { processingMs: 1, concurrency: 32, queueCapacity: 100, rateLimitPerSecond: 1000 },
  externalApi: { latencyMs: 250, concurrency: 10, queueCapacity: 100, rateLimitPerMinute: 60, reliabilityPercentage: 99 },
  retries: { maxAttempts: 1, baseDelayMs: 100, maxDelayMs: 1000, jitterPercentage: 0, retryWrites: false },
  circuitBreaker: { enabled: false, failureThreshold: 3, openDurationMs: 500 },
  databaseReplication: { readPreference: "Primary", lagMs: 100, detectionDelayMs: 100, promotionDelayMs: 200, failoverEnabled: false },
};

export function SimulationReliabilitySettings({ value, onChange }: { value: ReliabilitySettings; onChange: (value: ReliabilitySettings) => void }) {
  function update<K extends Section, P extends keyof ReliabilitySettings[K]>(section: K, key: P, next: ReliabilitySettings[K][P]) {
    onChange({ ...value, [section]: { ...value[section], [key]: next } });
  }
  function number<K extends Section>(section: K, key: keyof ReliabilitySettings[K], label: string, min: number, max: number, step = 1) {
    const current = value[section][key] as number;
    return <label className="block text-xs text-zinc-400">{label}<input aria-label={label} type="number" required min={min} max={max} step={step}
      value={Number.isNaN(current) ? "" : current} onChange={e => update(section, key, e.target.valueAsNumber as ReliabilitySettings[K][typeof key])}
      className="mt-1 w-full rounded border border-zinc-700 bg-zinc-900 p-2 text-zinc-100" /></label>;
  }
  return <>
    <details><summary className="cursor-pointer text-xs text-zinc-300">Gateway and external API</summary>
      <p className="my-2 text-xs text-zinc-500">Connect a gateway before the service or an external API beside it. These settings control their simulated behavior. A rate limit of zero means unlimited.</p>
      <div className="grid grid-cols-2 gap-2">
        {number("gateway", "processingMs", "Gateway time (ms)", .1, 3600000, .1)}
        {number("gateway", "concurrency", "Gateway slots", 1, 100000)}
        {number("gateway", "queueCapacity", "Gateway waiting slots", 0, 100000)}
        {number("gateway", "rateLimitPerSecond", "Gateway limit / second", 0, 1000000)}
        {number("externalApi", "latencyMs", "External API time (ms)", .1, 3600000, .1)}
        {number("externalApi", "concurrency", "External API slots", 1, 100000)}
        {number("externalApi", "queueCapacity", "External API waiting slots", 0, 100000)}
        {number("externalApi", "rateLimitPerMinute", "External API limit / minute", 0, 1000000)}
        {number("externalApi", "reliabilityPercentage", "External API success (%)", 0, 100)}
      </div>
    </details>
    <details><summary className="cursor-pointer text-xs text-zinc-300">Retries and circuit breakers</summary>
      <p className="my-2 text-xs text-zinc-500">Retry failed database and external API calls. Attempts include the first call. Waiting keeps a service slot occupied. Rate-limit rejections are not retried.</p>
      <div className="grid grid-cols-2 gap-2">
        {number("retries", "maxAttempts", "Maximum attempts", 1, 10)}
        {number("retries", "baseDelayMs", "Retry delay (ms)", .1, 3600000, .1)}
        {number("retries", "maxDelayMs", "Maximum retry delay (ms)", value.retries.baseDelayMs, 3600000, .1)}
        {number("retries", "jitterPercentage", "Retry jitter (%)", 0, 100)}
        {number("circuitBreaker", "failureThreshold", "Failures to open circuit", 1, 100000)}
        {number("circuitBreaker", "openDurationMs", "Circuit open time (ms)", .1, 3600000, .1)}
      </div>
      <label className="my-2 block text-xs text-zinc-300"><input type="checkbox" checked={value.retries.retryWrites} onChange={e => update("retries", "retryWrites", e.target.checked)} /> Retry writes (can duplicate work)</label>
      <label className="my-2 block text-xs text-zinc-300"><input type="checkbox" checked={value.circuitBreaker.enabled} onChange={e => update("circuitBreaker", "enabled", e.target.checked)} /> Enable circuit breakers</label>
    </details>
    <details><summary className="cursor-pointer text-xs text-zinc-300">Database replication and failover</summary>
      <p className="my-2 text-xs text-zinc-500">In database properties, replicas includes the primary and standbys. Read replicas adds separate read-only instances. Replication is delayed, so stale reads and lost recent writes are possible.</p>
      <label className="block text-xs text-zinc-400">Database read routing<select aria-label="Database read routing" value={value.databaseReplication.readPreference}
        onChange={e => update("databaseReplication", "readPreference", e.target.value as "Primary" | "PreferReadReplica")} className="my-2 w-full rounded bg-zinc-900 p-2">
        <option value="Primary">Primary</option><option value="PreferReadReplica">Prefer read replicas</option>
      </select></label>
      <div className="grid grid-cols-2 gap-2">
        {number("databaseReplication", "lagMs", "Replication lag (ms)", 0, 3600000, .1)}
        {number("databaseReplication", "detectionDelayMs", "Primary failure detection (ms)", 0, 3600000, .1)}
        {number("databaseReplication", "promotionDelayMs", "Promotion delay (ms)", .1, 3600000, .1)}
      </div>
      <label className="my-2 block text-xs text-zinc-300"><input type="checkbox" checked={value.databaseReplication.failoverEnabled} onChange={e => update("databaseReplication", "failoverEnabled", e.target.checked)} /> Enable database failover</label>
    </details>
  </>;
}
