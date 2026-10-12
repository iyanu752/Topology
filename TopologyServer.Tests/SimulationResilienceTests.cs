using System.Text.Json;

namespace TopologyServer.Tests;

public class SimulationResilienceTests
{
    [Fact]
    public void GatewayLimitsDistinguishRateFromCapacity()
    {
        var config = Config();
        config.Gateway.RateLimitPerSecond = 2;
        var result = Run(config, Gateway());
        Assert.Equal(4, result.Summary.Requests.Succeeded);
        Assert.Equal(16, result.Summary.Endpoints.Single().RateLimited);
        Assert.Equal(0, result.Summary.Endpoints.Single().Saturated);
        config.Gateway.RateLimitPerSecond = 0;
        config.Gateway.Concurrency = 1;
        config.Gateway.QueueCapacity = 0;
        config.Gateway.ProcessingMs = 150;
        result = Run(config, Gateway());
        Assert.Equal(10, result.Summary.Endpoints.Single().Saturated);
        Assert.Equal(0, result.Summary.Endpoints.Single().RateLimited);
        Assert.True(result.Summary.Requests.FailureCountsByCause.ContainsKey("GatewaySaturated"));
    }

    [Fact]
    public void GatewayAndLoadBalancerSupportBothOrders()
    {
        foreach (var reverse in new[] { false, true })
        {
            var design = Gateway();
            design.Nodes.Add(new() { Id = "lb", Type = ComponentType.LoadBalancer });
            if (reverse) { design.Edges[0].TargetNodeId = "lb"; design.Edges.Add(new() { Id = "lg", SourceNodeId = "lb", TargetNodeId = "gateway" }); }
            else { design.Edges.Single(e => e.Id == "gs").TargetNodeId = "lb"; design.Edges.Add(new() { Id = "ls", SourceNodeId = "lb", TargetNodeId = "service" }); }
            var result = Run(Config(), design);
            Assert.Equal(20, result.Summary.Requests.Succeeded);
            Assert.All(result.Summary.Edges, e => { Assert.Equal(20, e.Calls); Assert.Equal(20, e.Responses); });
            Assert.Equal(24, result.Summary.Requests.SuccessfulLatency.P50Ms);
        }
    }

    [Fact]
    public void ExternalLatencyFailureAndCapacityAffectClientResults()
    {
        var config = Config();
        config.ExternalApi.LatencyMs = 10;
        var result = Run(config, External());
        Assert.Equal(31, result.Summary.Requests.SuccessfulLatency.P50Ms);
        config.ExternalApi.ReliabilityPercentage = 0;
        result = Run(config, External());
        Assert.Equal(20, result.Summary.Requests.FailureCountsByCause["ExternalApiFailure"]);
        Assert.Equal(0, result.Summary.Requests.DatabaseReadsCompleted);
        config.ExternalApi.ReliabilityPercentage = 100;
        config.ExternalApi.LatencyMs = 150;
        config.ExternalApi.Concurrency = 1;
        config.ExternalApi.QueueCapacity = 0;
        result = Run(config, External());
        Assert.Equal(10, result.Summary.Endpoints.Single().Saturated);
    }

    [Fact]
    public void BoundedRetriesIncreaseDependencyTrafficWithoutGeneratingClients()
    {
        var config = Config();
        config.ExternalApi.LatencyMs = 1;
        config.ExternalApi.ReliabilityPercentage = 0;
        config.Retries.MaxAttempts = 3;
        config.Retries.BaseDelayMs = 1;
        var result = Run(config, External());
        Assert.Equal(20, result.Summary.Requests.Generated);
        Assert.Equal(60, result.Summary.Edges.Single(e => e.EdgeId == "se").Calls);
        Assert.Equal(40, result.Summary.Retries.RetryAttempts);
        Assert.Equal(60, result.Summary.Retries.FailuresByCause["ExternalApiFailure"]);
        Assert.Equal(result.Summary.Retries.RetryAttempts, result.Timeline.Sum(t => t.Measurements.Retries.RetryAttempts));
        config.Workload.ReadPercentage = 0;
        Assert.Equal(0, Run(config, External()).Summary.Retries.RetryAttempts);
        config.Retries.RetryWrites = true;
        Assert.Equal(40, Run(config, External()).Summary.Retries.RetryAttempts);
    }

    [Fact]
    public void RateLimitsDoNotCauseRetriesOrOpenCircuits()
    {
        var config = Config();
        config.ExternalApi.RateLimitPerMinute = 1;
        config.ExternalApi.LatencyMs = 1;
        config.Retries.MaxAttempts = 3;
        config.CircuitBreaker.Enabled = true;
        config.CircuitBreaker.FailureThreshold = 1;
        var result = Run(config, External());
        Assert.Equal(19, result.Summary.Endpoints.Single().RateLimited);
        Assert.Equal(0, result.Summary.Retries.RetryAttempts);
        Assert.Equal(0, result.Summary.Retries.CircuitOpened);
    }

    [Fact]
    public void RetryBackoffCanBridgeDatabaseRecovery()
    {
        var config = Config();
        config.Workload.RequestsPerSecond = 1;
        config.Retries.MaxAttempts = 3;
        config.Retries.BaseDelayMs = 100;
        config.ScheduledEvents = [Event("db", false, 0), Event("db", true, 50000)];
        var result = Run(config);
        Assert.Equal(2, result.Summary.Requests.Succeeded);
        Assert.Equal(1, result.Summary.Retries.RetryAttempts);
        Assert.Equal(3, result.Summary.Edges.Single(e => e.EdgeId == "sd").Calls);
    }

    [Fact]
    public void OpenCircuitBlocksTrafficAndProbeClosesAfterRecovery()
    {
        var config = Config();
        config.CircuitBreaker.Enabled = true;
        config.CircuitBreaker.FailureThreshold = 1;
        config.CircuitBreaker.OpenDurationMs = 500;
        config.ExternalApi.LatencyMs = 10;
        config.ScheduledEvents = [Event("external", false, 0), Event("external", true, 300000)];
        var result = Run(config, External());
        Assert.Equal(1, result.Summary.Retries.CircuitOpened);
        Assert.True(result.Summary.Retries.CircuitRejected > 0);
        Assert.All(result.Summary.Retries.Circuits, c => Assert.Equal(SimulationCircuitState.Closed, c.State));
        Assert.True(result.Summary.Requests.Succeeded > 0);
    }

    [Fact]
    public void DeadlinesCancelQueuedApiWorkAndPendingRetries()
    {
        var config = Config();
        config.Workload.ClientTimeoutMs = 50;
        config.ExternalApi.LatencyMs = 200;
        config.ExternalApi.Concurrency = 1;
        config.Retries.MaxAttempts = 10;
        var result = Run(config, External());
        Assert.Equal(20, result.Summary.Requests.TimedOut);
        Assert.Equal(0, result.Summary.Retries.RetryAttempts);
        Assert.All(result.Summary.Resources, r => { Assert.Equal(0, r.Active); Assert.Equal(0, r.QueueDepth); });
    }

    [Fact]
    public void JitterFailuresAndCircuitsAreRepeatable()
    {
        var config = Config();
        config.ExternalApi.LatencyMs = 1;
        config.ExternalApi.ReliabilityPercentage = 30;
        config.Retries.MaxAttempts = 4;
        config.Retries.JitterPercentage = 100;
        config.CircuitBreaker.Enabled = true;
        var first = Run(config, External());
        Assert.Equal(JsonSerializer.Serialize(first), JsonSerializer.Serialize(Run(config, External())));
        config.RandomSeed++;
        Assert.NotEqual(JsonSerializer.Serialize(first.Summary), JsonSerializer.Serialize(Run(config, External()).Summary));
    }

    [Fact]
    public void ReplicaReadsCanBeStaleWhilePrimaryReadsAreCurrent()
    {
        var config = Config();
        config.Workload.ReadPercentage = 50;
        config.Workload.KeySpaceSize = 1;
        config.DatabaseReplication.LagMs = 300;
        config.DatabaseReplication.ReadPreference = SimulationReadPreference.PreferReadReplica;
        var result = Run(config, Replicated());
        Assert.True(result.Summary.Replication.StaleReads > 0);
        config.DatabaseReplication.ReadPreference = SimulationReadPreference.Primary;
        Assert.Equal(0, Run(config, Replicated()).Summary.Replication.StaleReads);
        Assert.Equal(3, result.Summary.Replication.Replicas.Count);
    }

    [Fact]
    public void FailoverWaitsForPromotionThenKeepsNewPrimaryOnRecovery()
    {
        var config = Config();
        config.Workload.ReadPercentage = 0;
        config.DatabaseReplication.FailoverEnabled = true;
        config.DatabaseReplication.LagMs = 1;
        config.DatabaseReplication.DetectionDelayMs = 100;
        config.DatabaseReplication.PromotionDelayMs = 200;
        config.ScheduledEvents = [Event("db", false, 200000, "db:1"), Event("db", true, 1000000, "db:1")];
        var result = Run(config, Replicated());
        Assert.Equal("db:2", result.Summary.Replication.PrimaryReplicaId);
        Assert.Equal(1, result.Summary.Replication.Promotions);
        Assert.Equal(0, result.Summary.Replication.RolledBackKeys);
        Assert.Equal(3, result.Summary.Requests.ExplicitlyFailed);
        Assert.Equal("Standby", result.Summary.Replication.Replicas.Single(r => r.ReplicaId == "db:1").Role);
        Assert.Equal(0, result.Timeline.Where(t => t.AtMicroseconds <= 500000 && t.IntervalStartMicroseconds >= 200000).Sum(t => t.Measurements.Requests.DatabaseWritesCompleted));
    }

    [Fact]
    public void LaggingStandbyPromotionReportsRollbackAndReadReplicasCannotPromote()
    {
        var config = Config();
        config.Workload.ReadPercentage = 0;
        config.Workload.KeySpaceSize = 1;
        config.DatabaseReplication.FailoverEnabled = true;
        config.DatabaseReplication.LagMs = 1000;
        config.ScheduledEvents = [Event("db", false, 100000, "db:1")];
        var result = Run(config, Replicated());
        Assert.Equal(1, result.Summary.Replication.RolledBackKeys);
        var design = Replicated();
        design.Nodes.Single(n => n.Id == "db").Properties["replicas"] = 1;
        result = Run(config, design);
        Assert.Equal(0, result.Summary.Replication.Promotions);
        Assert.Equal("db:1", result.Summary.Replication.PrimaryReplicaId);
        Assert.Equal(1, result.Summary.Requests.Succeeded);
    }

    [Fact]
    public void NewSettingsAreValidatedAndReplicaSchedulesIncludeReaders()
    {
        var config = Config();
        config.ScheduledEvents = [Event("db", false, 0, "db:3")];
        Assert.False(Run(config, Replicated()).Summary.Replication.Replicas.Single(r => r.ReplicaId == "db:3").IsAvailable);
        config.Retries.MaxAttempts = 11;
        Assert.Throws<ArgumentException>(() => Run(config));
        config = Config(); config.ExternalApi.ReliabilityPercentage = 101;
        Assert.Throws<ArgumentException>(() => Run(config));
        config = Config(); config.Gateway = null!;
        Assert.Throws<ArgumentException>(() => Run(config));
    }

    [Fact]
    public void GatewayFailureDuringResponseCannotBeHiddenByQuickRecovery()
    {
        var config = Config();
        config.Workload.RequestsPerSecond = 1;
        config.ScheduledEvents = [Event("gateway", false, 20500), Event("gateway", true, 20750)];
        var result = Run(config, Gateway());
        Assert.Equal(1, result.Summary.Requests.ExplicitlyFailed);
        Assert.Equal(1, result.Summary.Requests.Succeeded);
        Assert.All(result.Summary.Resources, r => Assert.Equal(0, r.Active));
    }

    [Fact]
    public void ExternalFailureClearsActiveAndQueuedWorkBeforeRecovery()
    {
        var config = Config();
        config.ExternalApi.Concurrency = 1;
        config.ExternalApi.LatencyMs = 250;
        config.ScheduledEvents = [Event("external", false, 200000), Event("external", true, 500000),
            new() { Type = ScheduledSimulationEventType.TrafficChange, AtMicroseconds = 700000, RequestsPerSecond = 0 }];
        var result = Run(config, External());
        Assert.Equal(5, result.Summary.Requests.ExplicitlyFailed);
        Assert.Equal(2, result.Summary.Requests.Succeeded);
        Assert.Equal(5, result.Summary.Endpoints.Single().Failed);
        Assert.All(result.Summary.Resources, r => { Assert.Equal(0, r.Active); Assert.Equal(0, r.QueueDepth); });
    }

    [Fact]
    public void ServiceFailureCancelsBackoffWithoutSendingAnotherAttempt()
    {
        var config = Config();
        config.Workload.RequestsPerSecond = 1;
        config.ExternalApi.LatencyMs = 1;
        config.ExternalApi.ReliabilityPercentage = 0;
        config.Retries.MaxAttempts = 4;
        config.ScheduledEvents = [Event("service", false, 20000)];
        var result = Run(config, External());
        Assert.Equal(1, result.Summary.Retries.Attempts);
        Assert.Equal(0, result.Summary.Retries.RetryAttempts);
        Assert.All(result.Summary.Resources, r => Assert.Equal(0, r.Active));
    }

    [Fact]
    public void CancelledHalfOpenProbeAllowsAnotherProbeAndIgnoresLateSuccess()
    {
        var engine = new SimulationEngine(1);
        var resilience = new SimulationResilience(engine, new(), new() { Enabled = true, FailureThreshold = 1, OpenDurationMs = 100 });
        var sent = 0;
        var completed = 0;
        var probe = new SimulationRequest(2, 100000, 1000000, true);
        void Call(SimulationRequest request, bool fail)
        {
            resilience.Execute(request, "service:1->external", (_, done) =>
            {
                sent++;
                if (fail) done("ExternalApiFailure");
                else engine.Schedule(engine.NowMicroseconds + 100000, SimulationEventPriority.Ordinary, _ => done(null));
            }, _ => { }, error => { if (error == null) completed++; });
        }
        engine.Schedule(0, SimulationEventPriority.Ordinary, _ => Call(new(1, 0, 1000000, true), true));
        engine.Schedule(100000, SimulationEventPriority.Ordinary, _ => Call(probe, false));
        engine.Schedule(100000, SimulationEventPriority.Ordinary, _ => Call(new(3, 100000, 1000000, true), false));
        engine.Schedule(150000, SimulationEventPriority.Ordinary, _ => resilience.Cancel(probe));
        engine.Schedule(160000, SimulationEventPriority.Ordinary, _ => Call(new(4, 160000, 1000000, true), false));
        engine.Run();
        Assert.Equal(3, sent);
        Assert.Equal(1, completed);
        Assert.Equal(1, resilience.Snapshot().CircuitRejected);
        Assert.Equal(SimulationCircuitState.Closed, resilience.Snapshot().Circuits.Single().State);
    }

    [Fact]
    public void OldReplicationCannotRestoreRolledBackDataAfterPromotion()
    {
        var config = Config();
        config.Workload.RequestsPerSecond = 1;
        config.Workload.ReadPercentage = 0;
        config.DatabaseReplication.FailoverEnabled = true;
        config.DatabaseReplication.LagMs = 1000;
        config.ScheduledEvents = [Event("db", false, 100000, "db:1"), Event("db", true, 600000, "db:1"),
            new() { Type = ScheduledSimulationEventType.TrafficChange, AtMicroseconds = 200000, RequestsPerSecond = 0 }];
        var result = Run(config, Replicated());
        Assert.Equal(1, result.Summary.Replication.RolledBackKeys);
        Assert.Equal(1, result.Summary.Requests.DatabaseWritesCompleted);
        Assert.All(result.Summary.Replication.Replicas, r => Assert.Equal(0, r.AppliedVersion));
    }

    [Fact]
    public void PrimaryRecoveryBeforePromotionPreservesRoleAndData()
    {
        var config = Config();
        config.Workload.ReadPercentage = 0;
        config.DatabaseReplication.FailoverEnabled = true;
        config.ScheduledEvents = [Event("db", false, 100000, "db:1"), Event("db", true, 200000, "db:1")];
        var result = Run(config, Replicated());
        Assert.Equal("db:1", result.Summary.Replication.PrimaryReplicaId);
        Assert.Equal(0, result.Summary.Replication.Promotions);
        Assert.Equal(19, result.Summary.Requests.DatabaseWritesCompleted);
        Assert.Equal(19, result.Summary.Replication.Replicas.Single(r => r.Role == "Primary").AppliedVersion);
    }

    [Fact]
    public void RepeatedPrimaryFailuresRestartPromotionDelay()
    {
        var config = Config();
        config.DatabaseReplication.FailoverEnabled = true;
        config.ScheduledEvents = [Event("db", false, 100000, "db:1"), Event("db", true, 200000, "db:1"), Event("db", false, 300000, "db:1")];
        var result = Run(config, Replicated());
        Assert.Equal(0, result.Timeline.Where(t => t.AtMicroseconds < 600000).Sum(t => t.Measurements.Replication.Promotions));
        Assert.Equal(1, result.Timeline.Single(t => t.AtMicroseconds == 600000).Measurements.Replication.Promotions);
    }

    private static SimulationConfiguration Config() => new() { DurationSeconds = 2, MetricIntervalMs = 100,
        Workload = new() { RequestsPerSecond = 10, ReadPercentage = 100 }, ExternalApi = new() { RateLimitPerMinute = 0, ReliabilityPercentage = 100 } };
    private static SimulationExecutionResult Run(SimulationConfiguration config, Design? design = null) => new SimulationRunner().Run(design ?? SimulationRunnerTests.Design(), config).Execution!;
    private static ScheduledSimulationEvent Event(string node, bool recovery, long at, string? replica = null) => new()
    { Type = recovery ? ScheduledSimulationEventType.ComponentRecovery : ScheduledSimulationEventType.ComponentFailure, AtMicroseconds = at, TargetNodeId = node, TargetReplicaId = replica };
    private static Design Gateway()
    {
        var design = SimulationRunnerTests.Design();
        design.Nodes.Add(new() { Id = "gateway", Type = ComponentType.ApiGateway });
        design.Edges[0].TargetNodeId = "gateway";
        design.Edges.Add(new() { Id = "gs", SourceNodeId = "gateway", TargetNodeId = "service" });
        return design;
    }
    private static Design External()
    {
        var design = SimulationRunnerTests.Design();
        design.Nodes.Add(new() { Id = "external", Type = ComponentType.ExternalApi });
        design.Edges.Add(new() { Id = "se", SourceNodeId = "service", TargetNodeId = "external" });
        return design;
    }
    private static Design Replicated()
    {
        var design = SimulationRunnerTests.Design();
        var db = design.Nodes.Single(n => n.Id == "db");
        db.Properties["replicas"] = 2;
        db.Properties["readReplicas"] = 1;
        return design;
    }
}
