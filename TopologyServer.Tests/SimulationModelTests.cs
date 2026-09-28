using System.Text.Json;
using System.Text.Json.Serialization;

namespace TopologyServer.Tests;

public class SimulationModelTests
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    [Fact]
    public void LegacyRequest_LeavesNewConfigurationAbsent()
    {
        var request = JsonSerializer.Deserialize<RunSimulationDto>(
            """{"scenario":"NormalTraffic","trafficPerSecond":40,"durationSeconds":15}""", JsonOptions)!;

        Assert.Null(request.Configuration);
        Assert.Equal(40, request.TrafficPerSecond);
        Assert.Equal(15, request.DurationSeconds);
        using var result = JsonDocument.Parse(JsonSerializer.Serialize(new SimulationResult(), JsonOptions));
        Assert.False(result.RootElement.TryGetProperty("execution", out _));
        Assert.True(result.RootElement.TryGetProperty("nodeResults", out _));
    }

    [Fact]
    public void Configuration_PreservesZeroTrafficAndMissingMixForResolution()
    {
        var request = JsonSerializer.Deserialize<RunSimulationDto>(
            """{"configuration":{"workload":{"requestsPerSecond":0,"writePercentage":100}}}""", JsonOptions)!;

        var config = Assert.IsType<SimulationConfiguration>(request.Configuration);
        Assert.Equal(60, config.DurationSeconds);
        Assert.Equal(1, config.RandomSeed);
        Assert.Equal(0, config.Workload.RequestsPerSecond);
        Assert.Null(config.Workload.ReadPercentage);
        Assert.Equal(100, config.Workload.WritePercentage);
        Assert.Equal(1000, config.Workload.ClientTimeoutMs);
        Assert.Equal(4, config.Defaults.DatabaseConcurrency);
        Assert.Equal(100_000, config.Limits.MaxGeneratedRequests);
    }

    [Fact]
    public void NewResult_RoundTripsScheduleIncompleteStatusAndUnknownLatency()
    {
        var result = new SimulationResult
        {
            Execution = new SimulationExecutionResult
            {
                EngineVersion = "test-v1",
                Status = SimulationExecutionStatus.Incomplete,
                StopReason = SimulationStopReason.GeneratedRequestLimit,
                ElapsedMicroseconds = 3_600_000_000L,
                ResolvedConfiguration = new SimulationConfiguration
                {
                    RandomSeed = 42,
                    ScheduledEvents =
                    [
                        new() { Type = ScheduledSimulationEventType.TrafficChange, AtMicroseconds = 0, RequestsPerSecond = 0 },
                        new() { Type = ScheduledSimulationEventType.ComponentFailure, AtMicroseconds = 1000, TargetNodeId = "db", TargetReplicaId = "db-1" },
                        new() { Type = ScheduledSimulationEventType.ComponentRecovery, AtMicroseconds = 2000, TargetNodeId = "db" }
                    ]
                },
                Timeline = [new() { AtMicroseconds = 1000 }]
            }
        };

        var json = JsonSerializer.Serialize(result, JsonOptions);
        var copy = JsonSerializer.Deserialize<SimulationResult>(json, JsonOptions)!.Execution!;
        Assert.Contains("\"stopReason\":\"GeneratedRequestLimit\"", json);
        Assert.Equal(3_600_000_000L, copy.ElapsedMicroseconds);
        Assert.Equal(SimulationExecutionStatus.Incomplete, copy.Status);
        Assert.Equal(42, copy.ResolvedConfiguration.RandomSeed);
        Assert.Equal(3, copy.ResolvedConfiguration.ScheduledEvents.Count);
        Assert.Equal("db-1", copy.ResolvedConfiguration.ScheduledEvents[1].TargetReplicaId);
        Assert.Null(copy.Summary.Requests.SuccessfulLatency.P95Ms);
        Assert.Single(copy.Timeline);
    }
}
