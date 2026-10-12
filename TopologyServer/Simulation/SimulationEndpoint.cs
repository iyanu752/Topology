namespace TopologyServer;

public sealed class SimulationEndpoint
{
    private readonly SimulationEngine _engine;
    private readonly string _kind;
    private readonly double _duration;
    private readonly int _limit;
    private readonly long _window;
    private readonly int _reliability;
    private long _windowNumber = -1;
    private int _admitted;
    private readonly Dictionary<long, (SimulationRequest Request, Action<string?> Reply)> _pending = [];
    private readonly SimulationEndpointMetrics _metrics;
    public SimulationResource Resource { get; }

    public SimulationEndpoint(SimulationEngine engine, string nodeId, bool gateway, SimulationConfiguration config)
    {
        _engine = engine;
        _kind = gateway ? "Gateway" : "ExternalApi";
        _duration = gateway ? config.Gateway.ProcessingMs : config.ExternalApi.LatencyMs;
        _limit = gateway ? config.Gateway.RateLimitPerSecond : config.ExternalApi.RateLimitPerMinute;
        _window = gateway ? 1000000 : 60000000;
        _reliability = gateway ? 100 : config.ExternalApi.ReliabilityPercentage;
        _metrics = new() { NodeId = nodeId };
        Resource = new(engine, nodeId, gateway ? SimulationResourceKind.GatewayExecution : SimulationResourceKind.ExternalApiExecution,
            gateway ? config.Gateway.Concurrency : config.ExternalApi.Concurrency,
            gateway ? config.Gateway.QueueCapacity : config.ExternalApi.QueueCapacity);
    }

    public void Execute(SimulationRequest request, Action<string?> reply)
    {
        if (!request.IsRunnable) return;
        _metrics.Received++;
        if (!Resource.IsAvailable) { _metrics.Failed++; reply(_kind + "Unavailable"); return; }
        var window = _engine.NowMicroseconds / _window;
        if (window != _windowNumber) { _windowNumber = window; _admitted = 0; }
        if (_limit > 0 && _admitted >= _limit) { _metrics.RateLimited++; reply(_kind + "RateLimited"); return; }
        _admitted++;
        _pending.Add(request.Id, (request, reply));
        if (!Resource.Acquire(request, () =>
            _engine.Schedule(_engine.NowMicroseconds + SimulationTime.FromMilliseconds(_duration), SimulationEventPriority.Ordinary, _ =>
            {
                if (!_pending.Remove(request.Id, out var pending)) return;
                Resource.Release(request);
                var failed = _reliability < 100 && _engine.Random.NextDouble() * 100 >= _reliability;
                if (failed) _metrics.Failed++; else _metrics.Completed++;
                pending.Reply(failed ? _kind + "Failure" : null);
            })))
        {
            _pending.Remove(request.Id);
            _metrics.Saturated++;
            reply(_kind + "Saturated");
        }
    }

    public void Cancel(SimulationRequest request)
    {
        _pending.Remove(request.Id);
        Resource.Release(request);
    }

    public void SetAvailable(bool available)
    {
        Resource.SetAvailable(available);
        if (available) return;
        var interrupted = _pending.Values.ToArray();
        _pending.Clear();
        foreach (var pending in interrupted) { _metrics.Failed++; pending.Reply(_kind + "Unavailable"); }
    }

    public SimulationEndpointMetrics Snapshot() => new() { NodeId = _metrics.NodeId, Received = _metrics.Received,
        Completed = _metrics.Completed, RateLimited = _metrics.RateLimited, Saturated = _metrics.Saturated, Failed = _metrics.Failed };
}
