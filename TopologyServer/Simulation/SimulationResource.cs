namespace TopologyServer;

public sealed class SimulationResource(SimulationEngine engine, string nodeId, SimulationResourceKind kind, int capacity, int queueCapacity, string? replicaId = null)
{
    private readonly HashSet<long> _active = [];
    private readonly LinkedList<(SimulationRequest Request, Action Start)> _queue = [];
    private long _lastTime;
    private double _busyMicroseconds;
    private long _rejected;
    private long _unavailableMicroseconds;
    public bool IsAvailable { get; private set; } = true;
    public int Active => _active.Count;
    public void SetAvailable(bool available)
    {
        Accumulate();
        IsAvailable = available;
        if (!available) { _active.Clear(); _queue.Clear(); }
    }

    public bool Acquire(SimulationRequest request, Action start)
    {
        Accumulate();
        if (!IsAvailable) { _rejected++; return false; }
        if (_active.Count < capacity)
        {
            _active.Add(request.Id);
            start();
            return true;
        }
        if (_queue.Count < queueCapacity)
        {
            _queue.AddLast((request, start));
            return true;
        }
        _rejected++;
        return false;
    }

    public void Release(SimulationRequest request)
    {
        Accumulate();
        _active.Remove(request.Id);
        for (var item = _queue.First; item != null;)
        {
            var next = item.Next;
            if (item.Value.Request.Id == request.Id || !item.Value.Request.IsRunnable) _queue.Remove(item);
            item = next;
        }
        while (IsAvailable && _active.Count < capacity && _queue.First is { } first)
        {
            _queue.RemoveFirst();
            if (!first.Value.Request.IsRunnable) continue;
            _active.Add(first.Value.Request.Id);
            first.Value.Start();
        }
    }

    public SimulationResourceMetrics Snapshot()
    {
        Accumulate();
        return new()
        {
            NodeId = nodeId, ReplicaId = replicaId, Kind = kind, Capacity = capacity, Active = _active.Count,
            QueueDepth = _queue.Count, QueueCapacity = queueCapacity, Rejected = _rejected,
            UtilizationRatio = engine.NowMicroseconds == 0 ? null : _busyMicroseconds / (capacity * (double)engine.NowMicroseconds),
            UnavailableMicroseconds = _unavailableMicroseconds, IsAvailable = IsAvailable
        };
    }

    private void Accumulate()
    {
        _busyMicroseconds += (engine.NowMicroseconds - _lastTime) * (double)_active.Count;
        if (!IsAvailable) _unavailableMicroseconds += engine.NowMicroseconds - _lastTime;
        _lastTime = engine.NowMicroseconds;
    }
}
