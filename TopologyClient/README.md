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
one database instance. Changes to the design clear the previous run so its
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
events and results use `nodeId:1`, `nodeId:2`, etc. Database replicas remain unsupported.

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
pools share one global database connection ceiling and hold their local slots while
waiting for a database connection. Adding service or worker replicas does not
multiply database capacity.

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
