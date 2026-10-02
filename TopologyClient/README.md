# Topology Client

Next.js app using TypeScript, the App Router, and Tailwind CSS.

## Trying the simulation

Start the backend and frontend, then use **Add a sample to an empty canvas** in
the simulation panel. Click **Run simulation** to send requests through a client,
service, and database. Use **Play**, the speed selector, or the timeline slider
to inspect the recorded run.

Open **Service and database settings** and increase the database read time to
see waiting requests build up. Increasing database slots can relieve that limit.
The initial model supports one service and one database instance. Other connected
component types and scheduled failures are not supported yet. Changes to the
design clear the previous run so its measurements are not shown on a different
diagram.

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
