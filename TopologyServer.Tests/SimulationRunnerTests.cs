using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace TopologyServer.Tests;

public class SimulationRunnerTests
{
    [Fact]
    public void OneRead_CompletesInNineteenMilliseconds()
    {
        var run = Run(new() { DurationSeconds = 1, Workload = new() { RequestsPerSecond = 1, ReadPercentage = 100 } });
        Assert.Equal(1, run.Summary.Requests.Succeeded);
        Assert.Equal(19, run.Summary.Requests.SuccessfulLatency.P95Ms);
        Assert.All(run.Summary.Edges, edge => { Assert.Equal(1, edge.Calls); Assert.Equal(1, edge.Responses); });
    }

    [Fact]
    public void PerEdgeDelay_AndWriteProcessing_AffectLatency()
    {
        var run = Run(new() { DurationSeconds = 1, Workload = new() { RequestsPerSecond = 1, WritePercentage = 100 }, EdgeDelayMs = new() { ["cs"] = 3, ["sd"] = 7 } });
        Assert.Equal(45, run.Summary.Requests.SuccessfulLatency.P50Ms);
        Assert.Equal(1, run.Summary.Requests.DatabaseWritesCompleted);
    }

    [Fact]
    public void SlowDatabase_CausesBacklogAndCapacityRelievesIt()
    {
        var config = new SimulationConfiguration
        {
            DurationSeconds = 5, Workload = new() { RequestsPerSecond = 100, ReadPercentage = 100, ClientTimeoutMs = 10000 },
            Defaults = new() { NetworkDelayMs = 0, ServiceProcessingMs = 1, ServiceConcurrency = 100,
                DatabasePoolSize = 100, DatabaseConcurrency = 1, DatabaseReadProcessingMs = 20,
                ServiceQueueCapacity = 1000, DatabaseQueueCapacity = 1000, DatabasePoolQueueCapacity = 1000 }
        };
        var slow = Run(config);
        config.Defaults.DatabaseConcurrency = 4;
        var fast = Run(config);
        Assert.True(slow.Summary.Resources.Single(r => r.Kind == SimulationResourceKind.DatabaseExecution).QueueDepth > 0);
        Assert.Equal(0, fast.Summary.Resources.Single(r => r.Kind == SimulationResourceKind.DatabaseExecution).QueueDepth);
        Assert.True(fast.Summary.Requests.Succeeded > slow.Summary.Requests.Succeeded);
        Assert.All(slow.Summary.Resources, r => Assert.InRange(r.Active, 0, r.Capacity));
        config.Defaults.DatabaseConcurrency = 1;
        config.Defaults.DatabasePoolSize = 2;
        config.Defaults.ServiceConcurrency = 4;
        var upstream = Run(config);
        Assert.True(upstream.Summary.Resources.Single(r => r.Kind == SimulationResourceKind.ServiceExecution).QueueDepth > 0);
        Assert.True(upstream.Summary.Resources.Single(r => r.Kind == SimulationResourceKind.DatabasePool).QueueDepth > 0);
    }

    [Fact]
    public void DeadlineWins_AndLateWriteDoesNotBecomeSuccess()
    {
        var run = Run(new() { DurationSeconds = 1, Workload = new() { RequestsPerSecond = 1, WritePercentage = 100, ClientTimeoutMs = 10 } });
        Assert.Equal(1, run.Summary.Requests.TimedOut);
        Assert.Equal(1, run.Summary.Requests.DatabaseWritesCompleted);
        Assert.Equal(0, run.Summary.Requests.Succeeded);
        Assert.All(run.Summary.Resources, r => Assert.Equal(0, r.Active));
        var exact = Run(new() { DurationSeconds = 1, Workload = new() { RequestsPerSecond = 1, ReadPercentage = 100, ClientTimeoutMs = 19 } });
        Assert.Equal(1, exact.Summary.Requests.TimedOut);
    }

    [Fact]
    public void TimeoutBeforeServiceFinishes_DoesNotCallDatabase()
    {
        var run = Run(new() { DurationSeconds = 1, Workload = new() { RequestsPerSecond = 1, ClientTimeoutMs = 2 } });
        Assert.Equal(1, run.Summary.Requests.TimedOut);
        Assert.Equal(0, run.Summary.Edges.Single(e => e.EdgeId == "sd").Calls);
        Assert.All(run.Summary.Resources, resource => { Assert.Equal(0, resource.Active); Assert.Equal(0, resource.QueueDepth); });
    }

    [Fact]
    public void DatabaseConnectionCeiling_RestrictsPoolAndUtilizationIsTimeWeighted()
    {
        var design = Design();
        design.Nodes[2].Properties["maxConnections"] = 1;
        var run = new SimulationRunner().Run(design, new()
        {
            DurationSeconds = 1, Workload = new() { RequestsPerSecond = 1, ReadPercentage = 100 },
            Defaults = new() { NetworkDelayMs = 0, DatabaseReadProcessingMs = 20 }
        }).Execution!;
        Assert.Equal(1, run.Summary.Resources.Single(r => r.Kind == SimulationResourceKind.DatabasePool).Capacity);
        Assert.Equal(.005, run.Summary.Resources.Single(r => r.Kind == SimulationResourceKind.DatabaseExecution).UtilizationRatio!.Value, 8);
    }

    [Fact]
    public void CancelledAndOutputLimitedRuns_AreMarkedIncomplete()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var cancelled = new SimulationRunner().Run(Design(), new(), cancellation.Token).Execution!;
        Assert.Equal(SimulationStopReason.Cancelled, cancelled.StopReason);
        Assert.Equal(0, cancelled.Summary.Requests.Generated);
        var limited = Run(new() { DurationSeconds = 2, MetricIntervalMs = 100, Limits = new() { MaxRetainedRecords = 36 } });
        Assert.Equal(SimulationStopReason.RetainedRecordLimit, limited.StopReason);
        Assert.Equal(limited.ElapsedMicroseconds, limited.Timeline[^1].AtMicroseconds);
        Assert.Equal(limited.Summary.Requests.Generated, limited.Timeline.Sum(s => s.Measurements.Requests.Generated));
    }

    [Fact]
    public void ServiceQueue_IsBoundedAndFifo()
    {
        var engine = new SimulationEngine(1);
        var resource = new SimulationResource(engine, "service", SimulationResourceKind.ServiceExecution, 1, 2);
        var order = new List<long>();
        var requests = Enumerable.Range(0, 4).Select(i => new SimulationRequest(i, 0, 100, true)).ToArray();
        foreach (var request in requests) resource.Acquire(request, () => order.Add(request.Id));
        Assert.Equal(1, resource.Snapshot().Rejected);
        resource.Release(requests[0]);
        resource.Release(requests[1]);
        Assert.Equal(new long[] { 0, 1, 2 }, order);
    }

    [Fact]
    public void HorizonAndTimeline_AccountForAllRequests()
    {
        var run = Run(new() { DurationSeconds = 2, MetricIntervalMs = 100, Workload = new() { RequestsPerSecond = 1000, ClientTimeoutMs = 100 } });
        var r = run.Summary.Requests;
        Assert.Equal(2000, r.Generated);
        Assert.Equal(r.Generated, r.Succeeded + r.ExplicitlyFailed + r.TimedOut + r.InFlight);
        Assert.Equal(r.Generated, run.Timeline.Sum(s => s.Measurements.Requests.Generated));
        Assert.Equal(r.Succeeded, run.Timeline.Sum(s => s.Measurements.Requests.Succeeded));
        Assert.Equal(r.TimedOut, run.Timeline.Sum(s => s.Measurements.Requests.TimedOut));
        Assert.Equal(2_000_000, run.Timeline[^1].AtMicroseconds);
        Assert.All(run.Timeline.SelectMany(s => s.Measurements.Resources), r => Assert.InRange(r.UtilizationRatio ?? 0, 0, 1.00000001));
    }

    [Fact]
    public void SameSeedAndDesign_ProduceIdenticalMeasuredResults()
    {
        var config = new SimulationConfiguration { DurationSeconds = 2, RandomSeed = 42 };
        Assert.Equal(JsonSerializer.Serialize(Run(config)), JsonSerializer.Serialize(Run(config)));
        Assert.Null(config.Workload.RequestsPerSecond);
    }

    [Fact]
    public void ZeroTraffic_HasNoLatencyAndUnusedNodesHaveNoEffect()
    {
        var config = new SimulationConfiguration { DurationSeconds = 1, Workload = new() { RequestsPerSecond = 0 } };
        var design = Design();
        var before = new SimulationRunner().Run(design, config).Execution!;
        design.Nodes.Add(new() { Id = "cache", Type = ComponentType.Cache });
        var after = new SimulationRunner().Run(design, config).Execution!;
        Assert.Equal(JsonSerializer.Serialize(before.Summary), JsonSerializer.Serialize(after.Summary));
        Assert.Equal(0, after.Summary.Requests.Generated);
        Assert.Null(after.Summary.Requests.SuccessfulLatency.P95Ms);
        Assert.Equal(new[] { "cache" }, after.UnusedNodeIds);
    }

    [Fact]
    public void RequestLimit_ProducesPartialMeasuredRun()
    {
        var run = Run(new() { DurationSeconds = 2, Limits = new() { MaxGeneratedRequests = 10 } });
        Assert.Equal(SimulationExecutionStatus.Incomplete, run.Status);
        Assert.Equal(SimulationStopReason.GeneratedRequestLimit, run.StopReason);
        Assert.Equal(10, run.Summary.Requests.Generated);
        Assert.Equal(run.ElapsedMicroseconds, run.Timeline[^1].AtMicroseconds);
    }

    [Fact]
    public void InvalidSettings_AreRejectedBeforeExecution()
    {
        Assert.Throws<ArgumentException>(() => Run(new() { Workload = new() { ReadPercentage = 80, WritePercentage = 30 } }));
        Assert.Throws<ArgumentException>(() => Run(new() { Defaults = new() { ServiceConcurrency = 0 } }));
        Assert.Throws<ArgumentException>(() => Run(new() { Defaults = new() { DatabaseReadProcessingMs = double.NaN } }));
        Assert.Throws<ArgumentException>(() => Run(new() { EdgeDelayMs = new() { ["unknown"] = 1 } }));
        Assert.Throws<ArgumentException>(() => Run(new() { ScheduledEvents = [new()] }));
    }

    private static SimulationExecutionResult Run(SimulationConfiguration config) => new SimulationRunner().Run(Design(), config).Execution!;

    public static Design Design() => new()
    {
        Nodes = [new() { Id = "client", Type = ComponentType.Client }, new() { Id = "service", Type = ComponentType.Service }, new() { Id = "db", Type = ComponentType.Database }],
        Edges = [new() { Id = "cs", SourceNodeId = "client", TargetNodeId = "service" }, new() { Id = "sd", SourceNodeId = "service", TargetNodeId = "db" }]
    };
}

public class SimulationPreviewTests : IClassFixture<TestWebApplicationFactory>
{
    private readonly TestWebApplicationFactory _factory;
    public SimulationPreviewTests(TestWebApplicationFactory factory) => _factory = factory;

    [Fact]
    public async Task Preview_RunsSubmittedDesignWithoutSaving()
    {
        using var client = _factory.CreateClient();
        var design = SimulationRunnerTests.Design();
        var response = await client.PostAsJsonAsync("/api/simulations/preview", new SimulationPreviewDto
        {
            Design = new() { Nodes = design.Nodes, Edges = design.Edges },
            Configuration = new() { DurationSeconds = 1, Workload = new() { RequestsPerSecond = 1, ReadPercentage = 100 } }
        });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<SimulationResult>(new JsonSerializerOptions(JsonSerializerDefaults.Web) { Converters = { new JsonStringEnumConverter() } });
        Assert.Equal(19, result!.Execution!.Summary.Requests.SuccessfulLatency.P95Ms);
    }

    [Fact]
    public async Task Preview_RejectsUnsupportedTopology()
    {
        using var client = _factory.CreateClient();
        var response = await client.PostAsJsonAsync("/api/simulations/preview", new SimulationPreviewDto { Design = new(), Configuration = new() });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("Client", await response.Content.ReadAsStringAsync());
    }
}
