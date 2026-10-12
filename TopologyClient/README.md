# Topology Client

Next.js app using TypeScript, the App Router, and Tailwind CSS.

## Trying the simulation

Start the backend and frontend, then use **Add a sample to an empty canvas** in
the simulation panel. Click **Run simulation** to send requests through a client,
service, and database. Use **Play**, the speed selector, or the timeline slider
to inspect the recorded run.

Open **Service and database settings** and increase the database read time to
see waiting requests build up. Increasing database slots can relieve that limit.
The model supports one client, one logical service with up to 100 replicas, and
one logical database with primary, standby, and read replicas. Changes to the design clear the previous run so its
measurements are not shown on a different diagram.

## Failures, replicas, caching and jobs

Use **Scheduled traffic, failures and recovery** to add changes at specific times.
Two traffic changes make a spike. A ramp interpolates rates in one-second steps,
including its final rate at the end time. Overlapping traffic events are rejected.
Failure wins over a completion at the same timestamp. Failed components discard
their own waiting work and abort their own active work; recovery starts them empty.
Database work already running can outlive a failed caller or a client timeout.

Add `Client → LoadBalancer → Service` to use health checks. Set `replicas` on the
service node; replicas have independent execution slots and pools. The panel offers
round-robin or least-connections routing, a health-check interval, and detection
delay. Traffic can reach a failed replica until detection completes. With no load
balancer node, direct routing uses immediate availability. Replica IDs in API
events and results use `nodeId:1`, `nodeId:2`, etc.

For caching, add `Service → Cache` alongside `Service → Database`. Reads check
the cache first and populate it after a miss. Entries start empty, use LRU eviction,
expire at the configured TTL, and are invalidated when a matching write completes.
Requests choose keys uniformly from the configured key space. A cache outage clears
its entries and reads fall back to the database. Run settings control cache entry
capacity and TTL; memory size and vendor settings remain unmodeled.

For jobs, retain `Service → Database` for reads and add
`Service → Queue → Worker → Database`. The worker is a separate Service node using
the same database, and may have its own replicas. Writes are acknowledged when the
queue accepts them, while workers finish in the background. Queue capacity counts
both waiting and unacknowledged jobs. Worker concurrency and processing time control
consumer throughput. Failed deliveries retry after a delay, up to the configured
delivery limit; exhausted jobs are counted separately. An acknowledgement timeout
can cause redelivery even if the previous database write finishes later, so delivery
is at least once. There is no duplicate-write suppression yet.

The initial queue is volatile. Queue failure loses stored and unacknowledged jobs;
healthy workers can still finish work they already received. Results distinguish
accepted, completed, exhausted, lost, waiting and active jobs. Service connection
pools hold their local slots while waiting for a connection on the selected database
replica. Adding service or worker replicas does not multiply database capacity.

## Gateways, retries and database failover

Add an API Gateway between the client and service. A load balancer can sit before
or after the gateway. Gateways have processing time, execution slots, a waiting
queue, and a rate limit. Add `Service → ExternalApi` alongside the database edge
to make the service call an external dependency before its cache, database, or queue
work. Workers only call the database. External APIs have latency, slots, a queue,
success percentage, and a rate limit. All these values come from the simulation
panel or API configuration; saved gateway/API metadata does not override them.

Rate limits use fixed windows starting at virtual time zero: one second for the
gateway and one minute for the external API. Zero disables a limit. Requests that
pass the rate check consume allowance even if capacity is exhausted. Results label
rate-limit rejection separately from saturation and dependency failures.

Retries are disabled by default (`maxAttempts: 1`). Higher values retry transient
database and external API failures with exponential backoff capped by the maximum
delay. Jitter randomly reduces that delay by up to the chosen percentage, using the
run's seed. Rate-limit rejections and open circuits are not retried. Writes require
an explicit opt-in. Waiting for a retry holds the service slot and uses the original
client deadline. Attempts and retries have separate counters; every transmitted
attempt adds traffic to its edge. Job redelivery remains a separate mechanism.

Circuit breakers are also off by default. Each service/worker replica has a breaker
for each dependency. Consecutive transient failures open it, which rejects new
calls locally. After the open duration, one call tests the dependency. Success
closes the breaker; failure opens it again. A cancelled test releases its probe
slot. Deadline timeouts cancel work but do not count as completed breaker failures.

On a database node, `replicas: 2` creates one primary and one promotable standby;
`readReplicas: 1` adds a separate read-only replica. IDs follow that order: `db:1`,
`db:2`, `db:3`. Each instance has its own execution and connection limits. Writes
always use the primary. Reads use the primary by default; **Prefer read replicas**
spreads reads among available read-only replicas, falling back to the primary.
Standbys do not serve reads until promoted.

Replication copies key versions after the configured lag. A write succeeds when
the primary finishes, without waiting for other copies. Reads observe the version
at execution completion. Stale replica reads are counted and do not populate cache.
This is a simplified eventual-consistency model: no transactions, quorum reads,
consensus protocol, or duplicate-write suppression. Cache is cleared on promotion.

Enable failover to promote the freshest available standby after detection plus
promotion delay. If no standby is available, the primary stays unavailable. A
read-only replica cannot be promoted. Recent key versions missing on the promoted
standby are counted as rolled-back keys, not as an exact number of lost writes.
Pending replication from the old primary cannot overwrite the new primary's data.
The old primary rejoins as a standby and catches up after the lag; it does not
automatically become primary again. Failure discards active/queued operations;
primary recovery before promotion preserves its data and cancels that promotion.

Playback shows retries, rejection causes, breaker states, database roles, applied
versions, promotions, stale reads, and rolled-back keys. Earlier callers can omit
all new configuration sections. The legacy evaluator remains unchanged.

The canvas uses `POST /api/simulations/preview` with a `design` and `configuration`.
This endpoint runs the submitted diagram without saving it. It limits concurrent
runs and stops long runs after 20 seconds of real time. The authenticated room API
also accepts an optional `configuration`; omitting it keeps the existing evaluator.
Run configuration controls the new engine independently of legacy scenario fields.
`edgeDelayMs` can override one-way network delay by edge ID; otherwise each edge
uses `defaults.networkDelayMs` (1 ms by default).

Simulation durations use virtual time. A run may stop early when its request,
event, or output limit is reached; the panel reports that reason. In-flight
requests at the end are counted separately from failures and timeouts.

## Commands

```bash
npm run dev
npm run build
npm run start
```
