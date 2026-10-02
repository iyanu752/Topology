namespace TopologyServer;

public sealed class SimulationNetwork(SimulationEngine engine, CompiledTopology topology, SimulationConfiguration configuration)
{
    private readonly Dictionary<string, SimulationEdgeMetrics> _counts = topology.Routes.ToDictionary(
        r => r.EdgeId, r => new SimulationEdgeMetrics { EdgeId = r.EdgeId });

    public void Send(int routeIndex, bool response, Action arrival)
    {
        var route = topology.Routes[routeIndex];
        var count = _counts[route.EdgeId];
        if (response) count.Responses++; else count.Calls++;
        engine.Schedule(engine.NowMicroseconds + SimulationTime.FromMilliseconds(configuration.EdgeDelayMs[route.EdgeId]),
            SimulationEventPriority.Ordinary, _ => arrival());
    }

    public List<SimulationEdgeMetrics> Snapshot() => _counts.Values.OrderBy(c => c.EdgeId, StringComparer.Ordinal)
        .Select(c => new SimulationEdgeMetrics { EdgeId = c.EdgeId, Calls = c.Calls, Responses = c.Responses }).ToList();
}
