using System.Text.Json;

namespace TopologyServer.Tests;

public class SimulationExtensionsTests
{
    [Fact]
    public void TrafficChangesAndRamp_ReplaceArrivalsWithoutDuplicates()
    {
        var config = Config();
        config.Workload.RequestsPerSecond = 10;
        config.ScheduledEvents = [new() { Type = ScheduledSimulationEventType.TrafficChange, AtMicroseconds = 1000000, RequestsPerSecond = 20 }];
        Assert.Equal(30, Run(config).Summary.Requests.Generated);
        config.ScheduledEvents = [new() { Type = ScheduledSimulationEventType.TrafficRamp, AtMicroseconds = 0, RequestsPerSecond = 0, EndAtMicroseconds = 1000000, EndRequestsPerSecond = 20 }];
        Assert.Equal(20, Run(config).Summary.Requests.Generated);
        config.ScheduledEvents = [new() { Type = ScheduledSimulationEventType.TrafficChange, AtMicroseconds = 0, RequestsPerSecond = 0 }];
        Assert.Equal(0, Run(config).Summary.Requests.Generated);
    }

    [Fact]
    public void DatabaseOutage_AbortsWorkAndRecoveryRestoresNewRequests()
    {
        var config = Config();
        config.Defaults.DatabaseReadProcessingMs = 200;
        config.ScheduledEvents = [Event("db", false, 100000), Event("db", true, 1000000)];
        var result = Run(config);
        Assert.Equal(2, result.StateTransitions.Count);
        Assert.True(result.Summary.Requests.ExplicitlyFailed > 0);
        Assert.True(result.Timeline.Where(s => s.AtMicroseconds > 1200000).Sum(s => s.Measurements.Requests.Succeeded) > 0);
        Assert.Equal(900000, result.Summary.Resources.Single(r => r.Kind == SimulationResourceKind.DatabaseExecution).UnavailableMicroseconds);
        Assert.Equal(0, result.Timeline.Where(s => s.AtMicroseconds <= 1000000).Sum(s => s.Measurements.Requests.DatabaseReadsCompleted));
        AssertAccounting(result);
    }

    [Fact]
    public void FailureWinsOverDatabaseCompletionAtSameTimestamp()
    {
        var config = Config();
        config.Workload.RequestsPerSecond = 1;
        config.ScheduledEvents = [Event("db", false, 17000)];
        var result = Run(config);
        Assert.Equal(0, result.Summary.Requests.DatabaseReadsCompleted);
        Assert.Equal(2, result.Summary.Requests.ExplicitlyFailed);
    }

    [Fact]
    public void ReplicaFailures_AreIndependentAndHealthDetectionChangesRouting()
    {
        var design = WithLoadBalancer();
        design.Nodes.Single(n => n.Id == "service").Properties["replicas"] = 2;
        var config = Config();
        config.Workload.RequestsPerSecond = 100;
        config.Routing.HealthCheckIntervalMs = 100;
        config.Routing.DetectionDelayMs = 200;
        config.ScheduledEvents = [Event("service", false, 100000, "service:1"), Event("service", true, 1000000, "service:1")];
        var result = Run(config, design);
        var replicas = result.Summary.Resources.Where(r => r.Kind == SimulationResourceKind.ServiceExecution).ToArray();
        Assert.Equal(2, replicas.Length);
        Assert.Equal(900000, replicas.Single(r => r.ReplicaId == "service:1").UnavailableMicroseconds);
        Assert.Equal(0, replicas.Single(r => r.ReplicaId == "service:2").UnavailableMicroseconds);
        Assert.True(result.Timeline.Where(s => s.AtMicroseconds <= 400000).Sum(s => s.Measurements.Requests.ExplicitlyFailed) > 0);
        Assert.Equal(0, result.Timeline.Where(s => s.IntervalStartMicroseconds >= 400000 && s.AtMicroseconds <= 900000).Sum(s => s.Measurements.Requests.ExplicitlyFailed));
        AssertAccounting(result);
    }

    [Fact]
    public void LeastConnectionsAndRoundRobin_AreRepeatable()
    {
        var design = WithLoadBalancer();
        design.Nodes.Single(n => n.Id == "service").Properties["replicas"] = 3;
        foreach (var policy in Enum.GetValues<SimulationRoutingPolicy>())
        {
            var config = Config(); config.Routing.Policy = policy;
            Assert.Equal(JsonSerializer.Serialize(Run(config, design)), JsonSerializer.Serialize(Run(config, design)));
        }
    }

    [Fact]
    public void ColdCacheWarmsAndReducesDatabaseReads()
    {
        var config = Config(); config.Workload.KeySpaceSize = 1;
        var result = Run(config, WithCache());
        Assert.Equal(1, result.Summary.Cache.Misses);
        Assert.Equal(19, result.Summary.Cache.Hits);
        Assert.Equal(1, result.Summary.Requests.DatabaseReadsCompleted);
        Assert.Equal(1, result.Summary.Cache.Entries);
        Assert.Equal(0, result.Timeline[0].Measurements.Cache.Hits);
    }

    [Fact]
    public void CacheExpiryEvictionAndInvalidation_PreventStaleRefill()
    {
        var engine = new SimulationEngine(1);
        var cache = new SimulationCache(engine, new() { Capacity = 1, TtlMs = 10 });
        var first = cache.Lookup(1); cache.Populate(1, first.Version, first.Generation);
        var second = cache.Lookup(2); cache.Populate(2, second.Version, second.Generation);
        Assert.Equal(1, cache.Snapshot().Evictions);
        var miss = cache.Lookup(1);
        cache.Invalidate(1);
        cache.Populate(1, miss.Version, miss.Generation);
        Assert.False(cache.Lookup(1).Hit);
        engine.Schedule(10000, SimulationEventPriority.Ordinary, _ => Assert.False(cache.Lookup(2).Hit));
        engine.Run();
        Assert.Equal(0, cache.Snapshot().Entries);
    }

    [Fact]
    public void CacheFailureFlushesAndReadsFallBackToDatabase()
    {
        var config = Config(); config.Workload.KeySpaceSize = 1;
        config.ScheduledEvents = [Event("cache", false, 500000), Event("cache", true, 1000000)];
        var result = Run(config, WithCache());
        Assert.True(result.Summary.Cache.Hits > 0);
        Assert.True(result.Summary.Cache.Misses > 1);
        Assert.Equal(0, result.Summary.Requests.ExplicitlyFailed);
        AssertAccounting(result);
    }

    [Fact]
    public void QueueAcknowledgesBeforeJobsCompleteAndRespectsCapacity()
    {
        var config = Config(); config.Workload.ReadPercentage = 0;
        config.Queue.WorkerConcurrency = 1; config.Queue.WorkerProcessingMs = 500;
        config.Queue.Capacity = 3;
        var result = Run(config, WithQueue());
        Assert.True(result.Summary.Jobs.Accepted > result.Summary.Jobs.Completed);
        Assert.True(result.Summary.Jobs.Rejected > 0);
        Assert.Equal(result.Summary.Jobs.Accepted, result.Summary.Jobs.Completed + result.Summary.Jobs.DeadLettered + result.Summary.Jobs.Lost + result.Summary.Jobs.Waiting + result.Summary.Jobs.Active);
        Assert.True(result.Timeline[0].Measurements.Requests.Succeeded > result.Timeline[0].Measurements.Jobs.Completed);
        AssertAccounting(result);
    }

    [Fact]
    public void WorkerFailure_RedeliversAndEventuallyAcknowledges()
    {
        var config = Config(); config.Workload.ReadPercentage = 0;
        config.Queue.WorkerProcessingMs = 100;
        config.ScheduledEvents = [Event("worker", false, 50000), Event("worker", true, 300000)];
        var result = Run(config, WithQueue());
        Assert.True(result.Summary.Jobs.Redeliveries > 0);
        Assert.True(result.Summary.Jobs.Completed > 0);
        Assert.Equal(0, result.Summary.Jobs.DeadLettered);
    }

    [Fact]
    public void AcknowledgementTimeout_StopsAtDeliveryLimit()
    {
        var config = Config(); config.Workload.ReadPercentage = 0; config.Workload.RequestsPerSecond = 1;
        config.Queue.WorkerProcessingMs = 100;
        config.Queue.AcknowledgementTimeoutMs = 10; config.Queue.MaxDeliveries = 2;
        var result = Run(config, WithQueue());
        Assert.Equal(2, result.Summary.Jobs.DeadLettered);
        Assert.Equal(2, result.Summary.Jobs.Redeliveries);
        Assert.Equal(0, result.Summary.Jobs.Completed);
    }

    [Fact]
    public void InvalidTargetsAndConflictingEvents_AreRejected()
    {
        var config = Config(); config.ScheduledEvents = [Event("missing", false, 0)];
        Assert.Throws<ArgumentException>(() => Run(config));
        config.ScheduledEvents = [Event("service", false, 0, "service:9")];
        Assert.Throws<ArgumentException>(() => Run(config));
        config.ScheduledEvents = [Event("db", false, 0), Event("db", true, 0)];
        Assert.Throws<ArgumentException>(() => Run(config));
    }

    [Fact]
    public void CombinedWorkflow_PreservesCountersAcrossOutages()
    {
        var design = WithQueue();
        design.Nodes.Add(new() { Id = "cache", Type = ComponentType.Cache });
        design.Nodes.Add(new() { Id = "lb", Type = ComponentType.LoadBalancer });
        design.Edges[0].TargetNodeId = "lb";
        design.Edges.Add(new() { Id = "ls", SourceNodeId = "lb", TargetNodeId = "service" });
        design.Edges.Add(new() { Id = "sc", SourceNodeId = "service", TargetNodeId = "cache" });
        design.Nodes.Single(n => n.Id == "service").Properties["replicas"] = 2;
        design.Nodes.Single(n => n.Id == "worker").Properties["replicas"] = 2;
        var config = Config(); config.Workload.ReadPercentage = 50; config.Workload.RequestsPerSecond = 100; config.Workload.KeySpaceSize = 5;
        config.ScheduledEvents = [Event("service", false, 200000, "service:1"), Event("db", false, 400000), Event("db", true, 800000),
            Event("service", true, 1000000, "service:1"), Event("queue", false, 1200000), Event("queue", true, 1400000)];
        var result = Run(config, design);
        AssertAccounting(result);
        Assert.True(result.Summary.Cache.Hits > 0);
        Assert.True(result.Summary.Jobs.Completed > 0);
        var jobs = result.Summary.Jobs;
        Assert.Equal(jobs.Accepted, jobs.Completed + jobs.DeadLettered + jobs.Lost + jobs.Waiting + jobs.Active);
        Assert.Equal(jobs.Accepted, result.Timeline.Sum(s => s.Measurements.Jobs.Accepted));
        Assert.Equal(jobs.Completed, result.Timeline.Sum(s => s.Measurements.Jobs.Completed));
        Assert.Equal(result.Summary.Cache.Hits, result.Timeline.Sum(s => s.Measurements.Cache.Hits));
        Assert.Equal(6, result.StateTransitions.Count);
    }

    [Fact]
    public void FailedService_DoesNotStartItsQueuedDatabaseCalls()
    {
        var config = Config(); config.Workload.RequestsPerSecond = 100;
        config.Defaults.DatabaseConcurrency = 1; config.Defaults.DatabaseReadProcessingMs = 100;
        config.ScheduledEvents = [Event("service", false, 50000)];
        var result = Run(config);
        Assert.Equal(1, result.Summary.Requests.DatabaseReadsCompleted);
        Assert.All(result.Summary.Resources, r => { Assert.Equal(0, r.Active); Assert.Equal(0, r.QueueDepth); });
        AssertAccounting(result);
    }

    [Fact]
    public void SuccessfulWritesInvalidateCacheDuringRealWorkflow()
    {
        var config = Config(); config.Workload.ReadPercentage = 50; config.Workload.KeySpaceSize = 1;
        var result = Run(config, WithCache());
        Assert.True(result.Summary.Cache.Invalidations > 0);
        Assert.True(result.Summary.Cache.Hits > 0);
        Assert.True(result.Summary.Requests.DatabaseWritesCompleted > 0);
    }

    [Fact]
    public void QueueFailureLosesStoredJobsAndRecoveryAcceptsNewOnes()
    {
        var config = Config(); config.Workload.ReadPercentage = 0;
        config.Queue.WorkerProcessingMs = 500;
        config.ScheduledEvents = [Event("queue", false, 200000), Event("queue", true, 1000000)];
        var result = Run(config, WithQueue());
        Assert.True(result.Summary.Jobs.Lost > 0);
        Assert.True(result.Timeline.Where(s => s.IntervalStartMicroseconds >= 1000000).Sum(s => s.Measurements.Jobs.Accepted) > 0);
        var jobs = result.Summary.Jobs;
        Assert.Equal(jobs.Accepted, jobs.Completed + jobs.DeadLettered + jobs.Lost + jobs.Waiting + jobs.Active);
    }

    [Fact]
    public void ClientFailureStopsArrivalsAndRecoveryUsesLatestRate()
    {
        var config = Config();
        config.ScheduledEvents = [Event("client", false, 100000),
            new() { Type = ScheduledSimulationEventType.TrafficChange, AtMicroseconds = 500000, RequestsPerSecond = 20 }, Event("client", true, 1000000)];
        var result = Run(config);
        Assert.Equal(21, result.Summary.Requests.Generated);
        AssertAccounting(result);
    }

    [Fact]
    public void LoadBalancerFailureBreaksOutstandingCallsEvenAfterQuickRecovery()
    {
        var config = Config(); config.Workload.RequestsPerSecond = 1;
        config.ScheduledEvents = [Event("lb", false, 5000), Event("lb", true, 6000)];
        var result = Run(config, WithLoadBalancer());
        Assert.Equal(1, result.Summary.Requests.ExplicitlyFailed);
        Assert.Equal(1, result.Summary.Requests.Succeeded);
        Assert.All(result.Summary.Resources, r => Assert.Equal(0, r.Active));
        AssertAccounting(result);
    }

    private static void AssertAccounting(SimulationExecutionResult result)
    {
        var r = result.Summary.Requests;
        Assert.Equal(r.Generated, r.Succeeded + r.ExplicitlyFailed + r.TimedOut + r.InFlight);
        Assert.Equal(r.Generated, result.Timeline.Sum(s => s.Measurements.Requests.Generated));
        Assert.All(result.Summary.Resources, resource => Assert.InRange(resource.Active, 0, resource.Capacity));
    }

    private static SimulationConfiguration Config() => new() { DurationSeconds = 2, MetricIntervalMs = 100, Workload = new() { RequestsPerSecond = 10, ReadPercentage = 100 } };
    private static SimulationExecutionResult Run(SimulationConfiguration config, Design? design = null) => new SimulationRunner().Run(design ?? SimulationRunnerTests.Design(), config).Execution!;
    private static ScheduledSimulationEvent Event(string node, bool recovery, long time, string? replica = null) => new()
    { Type = recovery ? ScheduledSimulationEventType.ComponentRecovery : ScheduledSimulationEventType.ComponentFailure, TargetNodeId = node, TargetReplicaId = replica, AtMicroseconds = time };
    private static Design WithLoadBalancer()
    {
        var design = SimulationRunnerTests.Design();
        design.Nodes.Add(new() { Id = "lb", Type = ComponentType.LoadBalancer });
        design.Edges[0].TargetNodeId = "lb";
        design.Edges.Add(new() { Id = "lbs", SourceNodeId = "lb", TargetNodeId = "service" });
        return design;
    }
    private static Design WithCache()
    {
        var design = SimulationRunnerTests.Design();
        design.Nodes.Add(new() { Id = "cache", Type = ComponentType.Cache });
        design.Edges.Add(new() { Id = "sc", SourceNodeId = "service", TargetNodeId = "cache" });
        return design;
    }
    private static Design WithQueue()
    {
        var design = SimulationRunnerTests.Design();
        design.Nodes.Add(new() { Id = "queue", Type = ComponentType.Queue });
        design.Nodes.Add(new() { Id = "worker", Type = ComponentType.Service });
        design.Edges.AddRange([new() { Id = "sq", SourceNodeId = "service", TargetNodeId = "queue" },
            new() { Id = "qw", SourceNodeId = "queue", TargetNodeId = "worker" }, new() { Id = "wd", SourceNodeId = "worker", TargetNodeId = "db" }]);
        return design;
    }
}
