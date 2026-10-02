using System.Globalization;
using System.Text.Json;

namespace TopologyServer;

public static class SimulationConfigurationResolver
{
    public static SimulationConfiguration Resolve(SimulationConfiguration input, CompiledTopology topology)
    {
        var config = JsonSerializer.Deserialize<SimulationConfiguration>(JsonSerializer.Serialize(input))!;
        if (config.Workload == null || config.Defaults == null || config.Limits == null || config.ScheduledEvents == null || config.EdgeDelayMs == null)
            throw new ArgumentException("Configuration sections cannot be null.");
        _ = new SimulationEngine(config.DurationSeconds, config.RandomSeed, config.Limits);
        if (config.ScheduledEvents.Count > 0) throw new ArgumentException("Scheduled events are not supported yet.");
        Count(config.MetricIntervalMs, 1, 3_600_000, "Metric interval");
        var w = config.Workload;
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

    private static int ReadInteger(SimulationRuntimeComponent component, string key, int fallback)
    {
        if (!component.Definition.Properties.TryGetValue(key, out var value)) return fallback;
        return decimal.ToInt32(decimal.Parse(value.ToString(), NumberStyles.Float, CultureInfo.InvariantCulture));
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
