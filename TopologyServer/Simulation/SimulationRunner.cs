namespace TopologyServer;

public sealed class SimulationRunner
{
    public SimulationResult Run(Design design, SimulationConfiguration input, CancellationToken cancellationToken = default)
    {
        if (design.Nodes == null || design.Edges == null) throw new ArgumentException("Node and edge lists are required.");
        if (design.Nodes.Count > 500 || design.Edges.Count > 1000) throw new ArgumentException("A simulation supports up to 500 nodes and 1,000 edges.");
        var topology = new SimulationTopologyCompiler().Compile(design);
        var config = SimulationConfigurationResolver.Resolve(input, topology);
        var engine = new SimulationEngine(config.DurationSeconds, config.RandomSeed, config.Limits);
        var network = new SimulationNetwork(engine, topology, config);
        var client = new SimulationClient(engine, config.Workload);
        var workflow = new SimulationWorkflow(engine, topology, config, network, client);
        var timeline = new List<SimulationTimelineSample>();
        var resources = workflow.Resources;
        SimulationMeasurements? previous = null;
        long previousTime = 0;
        var recordsPerSnapshot = 3 + resources.Count + topology.Routes.Count + topology.Components.Count;
        var finalReserved = engine.TryRetainRecords(recordsPerSnapshot * 2);

        SimulationMeasurements Measure()
        {
            var requests = client.Requests;
            var succeeded = requests.Where(r => r.Outcome == SimulationRequestOutcome.Succeeded).ToArray();
            var failed = requests.Count(r => r.Outcome == SimulationRequestOutcome.Failed);
            var timedOut = requests.Count(r => r.Outcome == SimulationRequestOutcome.TimedOut);
            var terminal = succeeded.Length + failed + timedOut;
            var latencies = succeeded.Select(r => (r.FinishedAt!.Value - r.CreatedAt) / 1000.0).Order().ToArray();
            double? Percentile(double p) => latencies.Length == 0 ? null : latencies[(int)Math.Ceiling(p * latencies.Length) - 1];
            return new()
            {
                Requests = new()
                {
                    Generated = requests.Count, Succeeded = succeeded.Length, ExplicitlyFailed = failed, TimedOut = timedOut,
                    InFlight = requests.Count(r => r.IsPending), DatabaseReadsCompleted = workflow.ReadsCompleted,
                    DatabaseWritesCompleted = workflow.WritesCompleted,
                    SuccessfulRequestsPerSecond = engine.NowMicroseconds == 0 ? null : succeeded.Length * 1_000_000.0 / engine.NowMicroseconds,
                    TerminalErrorRatio = terminal == 0 ? null : (failed + timedOut) / (double)terminal,
                    SuccessfulLatency = new() { P50Ms = Percentile(.5), P95Ms = Percentile(.95), P99Ms = Percentile(.99) },
                    FailureCountsByCause = requests.Where(r => r.FailureCause != null).GroupBy(r => r.FailureCause!)
                        .ToDictionary(g => g.Key, g => (long)g.Count())
                },
                Resources = resources.Select(r => r.Snapshot()).ToList(), Edges = network.Snapshot(),
                Cache = workflow.Cache.Snapshot(), Jobs = workflow.Jobs.Snapshot(), Nodes = workflow.NodeSnapshots()
            };
        }

        void Sample(bool reserve)
        {
            if (reserve && !engine.TryRetainRecords(recordsPerSnapshot)) return;
            var cumulative = Measure();
            var interval = Difference(cumulative, previous, engine.NowMicroseconds, previousTime, client.Requests);
            timeline.Add(new() { AtMicroseconds = engine.NowMicroseconds, IntervalStartMicroseconds = previousTime, Measurements = interval });
            previous = cumulative;
            previousTime = engine.NowMicroseconds;
        }

        void ScheduleSample(long at)
        {
            if (at >= engine.DurationMicroseconds) return;
            engine.Schedule(at, SimulationEventPriority.Observation, _ =>
            {
                Sample(true);
                ScheduleSample(at + config.MetricIntervalMs * 1000L);
            });
        }

        if (finalReserved)
        {
            client.Start();
            workflow.Start();
            ScheduleSample(config.MetricIntervalMs * 1000L);
        }
        var run = engine.Run(cancellationToken);
        var summary = finalReserved ? Measure() : new SimulationMeasurements();
        if (finalReserved && (timeline.Count == 0 || previousTime != run.ElapsedMicroseconds)) Sample(false);
        var execution = new SimulationExecutionResult
        {
            EngineVersion = SimulationEngine.Version, ResolvedConfiguration = config, Status = run.Status,
            StopReason = run.StopReason, ElapsedMicroseconds = run.ElapsedMicroseconds, ProcessedEvents = run.ProcessedEvents,
            Summary = summary, Timeline = timeline, Assumptions = topology.Assumptions.Concat(new[] {
                "Traffic ramps change in one-second steps, with a final step at the specified end time.",
                "Service pools hold their slots while waiting for the shared database connection limit.",
                "Cache entries use LRU eviction and start empty. Cache outages flush entries; reads fall back to the database.",
                "The job queue is volatile: queue failure loses waiting and unacknowledged jobs. Active work on healthy workers may still finish.",
                "Job delivery is at least once. A write can finish after its acknowledgement times out, so redelivery may repeat it."
            }).ToList(), UnusedNodeIds = topology.UnusedNodeIds.ToList(), StateTransitions = workflow.Transitions
        };
        return new SimulationResult
        {
            Scenario = SimulationScenario.NormalTraffic, Execution = execution,
            Findings = [$"Generated {summary.Requests.Generated} requests; {summary.Requests.Succeeded} succeeded, {summary.Requests.TimedOut} timed out, and {summary.Requests.InFlight} remain in flight."],
            NodeResults = summary.Nodes,
            EdgeResults = topology.Routes.Select(r => new SimulationEdgeResult
            {
                EdgeId = r.EdgeId, SourceNodeId = r.SourceNodeId, TargetNodeId = r.TargetNodeId,
                Status = topology.Components[r.SourceNodeId].IsAvailable && topology.Components[r.TargetNodeId].IsAvailable ? SimulationEdgeStatus.Healthy : SimulationEdgeStatus.Broken,
                TrafficPerSecond = run.ElapsedMicroseconds == 0 ? 0 : (int)Math.Round(summary.Edges.FirstOrDefault(e => e.EdgeId == r.EdgeId)?.Calls * 1_000_000.0 / run.ElapsedMicroseconds ?? 0)
            }).ToList()
        };
    }

    private static SimulationMeasurements Difference(SimulationMeasurements current, SimulationMeasurements? previous,
        long now, long start, List<SimulationRequest> requests)
    {
        var result = System.Text.Json.JsonSerializer.Deserialize<SimulationMeasurements>(System.Text.Json.JsonSerializer.Serialize(current))!;
        var r = result.Requests;
        result.Cache.Hits -= previous?.Cache.Hits ?? 0;
        result.Cache.Misses -= previous?.Cache.Misses ?? 0;
        result.Cache.Evictions -= previous?.Cache.Evictions ?? 0;
        result.Cache.Invalidations -= previous?.Cache.Invalidations ?? 0;
        result.Jobs.Accepted -= previous?.Jobs.Accepted ?? 0;
        result.Jobs.Completed -= previous?.Jobs.Completed ?? 0;
        result.Jobs.DeadLettered -= previous?.Jobs.DeadLettered ?? 0;
        result.Jobs.Redeliveries -= previous?.Jobs.Redeliveries ?? 0;
        result.Jobs.Rejected -= previous?.Jobs.Rejected ?? 0;
        result.Jobs.Lost -= previous?.Jobs.Lost ?? 0;
        var p = previous?.Requests ?? new SimulationRequestMetrics();
        r.Generated -= p.Generated; r.Succeeded -= p.Succeeded; r.ExplicitlyFailed -= p.ExplicitlyFailed; r.TimedOut -= p.TimedOut;
        r.DatabaseReadsCompleted -= p.DatabaseReadsCompleted; r.DatabaseWritesCompleted -= p.DatabaseWritesCompleted;
        var elapsed = now - start;
        r.SuccessfulRequestsPerSecond = elapsed == 0 ? null : r.Succeeded * 1_000_000.0 / elapsed;
        var terminal = r.Succeeded + r.ExplicitlyFailed + r.TimedOut;
        r.TerminalErrorRatio = terminal == 0 ? null : (r.ExplicitlyFailed + r.TimedOut) / (double)terminal;
        var latencies = requests.Where(q => q.Outcome == SimulationRequestOutcome.Succeeded && (previous == null || q.FinishedAt > start))
            .Select(q => (q.FinishedAt!.Value - q.CreatedAt) / 1000.0).Order().ToArray();
        double? Percentile(double percentile) => latencies.Length == 0 ? null : latencies[(int)Math.Ceiling(percentile * latencies.Length) - 1];
        r.SuccessfulLatency = new() { P50Ms = Percentile(.5), P95Ms = Percentile(.95), P99Ms = Percentile(.99) };
        foreach (var key in r.FailureCountsByCause.Keys.ToArray()) r.FailureCountsByCause[key] -= p.FailureCountsByCause.GetValueOrDefault(key);
        for (var i = 0; i < result.Resources.Count; i++)
        {
            var resource = result.Resources[i];
            var old = previous?.Resources[i];
            resource.Rejected -= old?.Rejected ?? 0;
            resource.UnavailableMicroseconds -= old?.UnavailableMicroseconds ?? 0;
            resource.UtilizationRatio = elapsed == 0 ? null : ((resource.UtilizationRatio ?? 0) * now - (old?.UtilizationRatio ?? 0) * start) / elapsed;
        }
        for (var i = 0; i < result.Edges.Count; i++)
        {
            result.Edges[i].Calls -= previous?.Edges[i].Calls ?? 0;
            result.Edges[i].Responses -= previous?.Edges[i].Responses ?? 0;
        }
        return result;
    }
}
