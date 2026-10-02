using System.Globalization;
using System.Text.Json;

namespace TopologyServer;

public static class SimulationConfigurationResolver
{
    public static SimulationConfiguration Resolve(SimulationConfiguration input, CompiledTopology topology)
    {
        var config = JsonSerializer.Deserialize<SimulationConfiguration>(JsonSerializer.Serialize(input))!;
        if (config.Workload == null || config.Defaults == null || config.Limits == null || config.ScheduledEvents == null || config.EdgeDelayMs == null || config.Routing == null || config.Cache == null || config.Queue == null)
            throw new ArgumentException("Configuration sections cannot be null.");
        _ = new SimulationEngine(config.DurationSeconds, config.RandomSeed, config.Limits);
        ValidateSchedule(config, topology);
        if (!Enum.IsDefined(config.Routing.Policy)) throw new ArgumentException("Unknown routing policy.");
        Count(config.Routing.HealthCheckIntervalMs, 1, 3600000, "Health check interval");
        Count(config.Routing.DetectionDelayMs, 0, 3600000, "Detection delay");
        Count(config.Cache.Capacity, 0, 100000, "Cache capacity");
        Count(config.Cache.TtlMs, 1, 3600000, "Cache TTL");
        Duration(config.Cache.LookupMs, false, "Cache lookup time");
        Count(config.Queue.Capacity, 0, 100000, "Queue capacity");
        Count(config.Queue.WorkerConcurrency, 1, 1000, "Worker concurrency");
        Count(config.Queue.MaxDeliveries, 1, 100, "Maximum deliveries");
        Count(config.Queue.RetryDelayMs, 1, 3600000, "Redelivery delay");
        Count(config.Queue.AcknowledgementTimeoutMs, 1, 3600000, "Acknowledgement timeout");
        Duration(config.Queue.WorkerProcessingMs, false, "Worker processing time");
        Count(config.MetricIntervalMs, 1, 3_600_000, "Metric interval");
        var w = config.Workload;
        Count(w.KeySpaceSize, 1, 100000, "Key space size");
        w.RequestsPerSecond ??= ReadInteger(topology.Client, "requestsPerSecond", 100);
        w.ReadPercentage ??= w.WritePercentage.HasValue ? 100 - w.WritePercentage.Value : 70;
        w.WritePercentage ??= 100 - w.ReadPercentage.Value;
        Count(w.RequestsPerSecond.Value, 0, SimulationExecutionLimits.MaximumRequestsPerSecond, "Traffic");
        Count(w.ReadPercentage.Value, 0, 100, "Read percentage");
        Count(w.WritePercentage.Value, 0, 100, "Write percentage");
        if (w.ReadPercentage + w.WritePercentage != 100) throw new ArgumentException("Read and write percentages must add up to 100.");
        Duration(w.ClientTimeoutMs, false, "Client timeout");
        var d = config.Defaults;
        Duration(d.NetworkDelayMs, true, "Network delay");
        Duration(d.ServiceProcessingMs, false, "Service processing time");
        Duration(d.DatabaseReadProcessingMs, false, "Database read time");
        Duration(d.DatabaseWriteProcessingMs, false, "Database write time");
        Count(d.ServiceConcurrency, 1, 100_000, "Service concurrency");
        Count(d.DatabasePoolSize, 1, 100_000, "Connection pool size");
        Count(d.DatabaseConcurrency, 1, 100_000, "Database concurrency");
        d.DatabaseMaxConnections = ReadInteger(topology.Database, "maxConnections", d.DatabaseMaxConnections);
        Count(d.DatabaseMaxConnections, 1, 1_000_000, "Database connections");
        Count(d.ServiceQueueCapacity, 0, 100_000, "Service queue");
        Count(d.DatabasePoolQueueCapacity, 0, 100_000, "Pool queue");
        Count(d.DatabaseQueueCapacity, 0, 100_000, "Database queue");
        foreach (var (id, delay) in config.EdgeDelayMs)
        {
            if (!topology.Routes.Any(r => r.EdgeId == id)) throw new ArgumentException($"Network delay references an unused or unknown edge '{id}'.");
            Duration(delay, true, "Edge delay");
        }
        foreach (var route in topology.Routes) config.EdgeDelayMs.TryAdd(route.EdgeId, d.NetworkDelayMs);
        return config;
    }

    public static int ReadInteger(SimulationRuntimeComponent component, string key, int fallback)
    {
        if (!component.Definition.Properties.TryGetValue(key, out var value)) return fallback;
        return decimal.ToInt32(decimal.Parse(value.ToString(), NumberStyles.Float, CultureInfo.InvariantCulture));
    }

    private static void ValidateSchedule(SimulationConfiguration config, CompiledTopology topology)
    {
        if (config.ScheduledEvents.Count > 1000) throw new ArgumentException("At most 1,000 scheduled events are supported.");
        foreach (var ramp in config.ScheduledEvents.Where(e => e?.Type == ScheduledSimulationEventType.TrafficRamp))
            if (config.ScheduledEvents.Any(e => e != null && !ReferenceEquals(e, ramp) &&
                e.Type is ScheduledSimulationEventType.TrafficChange or ScheduledSimulationEventType.TrafficRamp &&
                e.AtMicroseconds >= ramp.AtMicroseconds && e.AtMicroseconds <= ramp.EndAtMicroseconds))
                throw new ArgumentException("Traffic ramps cannot overlap other traffic events.");
        var duration = config.DurationSeconds * 1000000L;
        var expanded = new List<ScheduledSimulationEvent>();
        foreach (var item in config.ScheduledEvents)
        {
            if (item == null || !Enum.IsDefined(item.Type) || item.AtMicroseconds < 0 || item.AtMicroseconds > duration)
                throw new ArgumentException("Scheduled event type or time is invalid.");
            if (item.Type is ScheduledSimulationEventType.TrafficChange or ScheduledSimulationEventType.TrafficRamp)
            {
                if (item.TargetNodeId != null || item.TargetReplicaId != null || !item.RequestsPerSecond.HasValue)
                    throw new ArgumentException("Traffic events require a rate and must not target a component.");
                Count(item.RequestsPerSecond.Value, 0, SimulationExecutionLimits.MaximumRequestsPerSecond, "Scheduled traffic");
                if (item.Type == ScheduledSimulationEventType.TrafficRamp)
                {
                    if (!item.EndAtMicroseconds.HasValue || item.EndAtMicroseconds <= item.AtMicroseconds || item.EndAtMicroseconds > duration || !item.EndRequestsPerSecond.HasValue)
                        throw new ArgumentException("A ramp requires an end time after its start and an end rate.");
                    Count(item.EndRequestsPerSecond.Value, 0, SimulationExecutionLimits.MaximumRequestsPerSecond, "Ramp end rate");
                    var length = item.EndAtMicroseconds.Value - item.AtMicroseconds;
                    for (long offset = 0; offset < length; offset += 1000000)
                        expanded.Add(new() { Type = ScheduledSimulationEventType.TrafficChange, AtMicroseconds = item.AtMicroseconds + offset,
                            RequestsPerSecond = (int)Math.Round(item.RequestsPerSecond.Value + (item.EndRequestsPerSecond.Value - item.RequestsPerSecond.Value) * (offset / (double)length), MidpointRounding.AwayFromZero) });
                    expanded.Add(new() { Type = ScheduledSimulationEventType.TrafficChange, AtMicroseconds = item.EndAtMicroseconds.Value, RequestsPerSecond = item.EndRequestsPerSecond });
                }
                else
                {
                    if (item.EndAtMicroseconds.HasValue || item.EndRequestsPerSecond.HasValue) throw new ArgumentException("Only ramps can specify end times and end rates.");
                    expanded.Add(item);
                }
            }
            else
            {
                if (item.TargetNodeId == null || !topology.Components.TryGetValue(item.TargetNodeId, out var target))
                    throw new ArgumentException("Failure and recovery events must target a component used by the workflow.");
                if (item.RequestsPerSecond.HasValue || item.EndAtMicroseconds.HasValue || item.EndRequestsPerSecond.HasValue)
                    throw new ArgumentException("Component events cannot specify traffic settings.");
                var replicas = ReadInteger(target, "replicas", 1);
                if (item.TargetReplicaId != null && !Enumerable.Range(1, replicas).Select(i => $"{target.Definition.NodeId}:{i}").Contains(item.TargetReplicaId))
                    throw new ArgumentException($"Unknown replica '{item.TargetReplicaId}'. Replica IDs use nodeId:1, nodeId:2, etc.");
                expanded.Add(item);
            }
            if (expanded.Count > 10000) throw new ArgumentException("The expanded schedule exceeds 10,000 events.");
        }
        foreach (var group in expanded.GroupBy(e => (e.AtMicroseconds, e.TargetNodeId)))
        {
            var events = group.ToArray();
            if (events.Length > 1 && (events.Any(e => e.TargetReplicaId == null) || events.GroupBy(e => e.TargetReplicaId).Any(g => g.Count() > 1)))
                throw new ArgumentException("Scheduled events conflict at the same time and target.");
        }
        config.ScheduledEvents = expanded.OrderBy(e => e.AtMicroseconds).ThenBy(e => e.TargetNodeId, StringComparer.Ordinal).ThenBy(e => e.TargetReplicaId, StringComparer.Ordinal).ToList();
    }

    private static void Count(int value, int min, int max, string name)
    {
        if (value < min || value > max) throw new ArgumentException($"{name} must be between {min} and {max}.");
    }

    private static void Duration(double value, bool allowZero, string name)
    {
        if (!double.IsFinite(value) || value < 0 || value > 3_600_000 || (!allowZero && SimulationTime.FromMilliseconds(value) == 0))
            throw new ArgumentException($"{name} must be a valid {(allowZero ? "non-negative" : "positive")} duration up to one hour.");
    }
}
