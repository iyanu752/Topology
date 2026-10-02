namespace TopologyServer;

public sealed class SimulationCache(SimulationEngine engine, SimulationCacheSettings settings)
{
    private readonly Dictionary<int, (long Expiry, long Used)> _entries = [];
    private readonly Dictionary<int, long> _versions = [];
    private long _sequence;
    private long _generation;
    private readonly SimulationCacheMetrics _metrics = new();
    public bool IsAvailable { get; private set; } = true;

    public (bool Hit, long Version, long Generation) Lookup(int key)
    {
        Expire();
        if (IsAvailable && _entries.TryGetValue(key, out var entry))
        {
            _entries[key] = (entry.Expiry, _sequence++);
            _metrics.Hits++;
            return (true, _versions.GetValueOrDefault(key), _generation);
        }
        _metrics.Misses++;
        return (false, _versions.GetValueOrDefault(key), _generation);
    }

    public void Populate(int key, long version, long generation)
    {
        if (!IsAvailable || settings.Capacity == 0 || generation != _generation || version != _versions.GetValueOrDefault(key)) return;
        Expire();
        if (!_entries.ContainsKey(key) && _entries.Count >= settings.Capacity)
        {
            _entries.Remove(_entries.MinBy(e => e.Value.Used).Key);
            _metrics.Evictions++;
        }
        _entries[key] = (engine.NowMicroseconds + settings.TtlMs * 1000L, _sequence++);
    }

    public void Invalidate(int key)
    {
        _versions[key] = _versions.GetValueOrDefault(key) + 1;
        if (_entries.Remove(key)) _metrics.Invalidations++;
    }

    public void SetAvailable(bool available)
    {
        IsAvailable = available;
        _generation++;
        _entries.Clear();
    }

    public SimulationCacheMetrics Snapshot()
    {
        Expire();
        return new() { Hits = _metrics.Hits, Misses = _metrics.Misses, Evictions = _metrics.Evictions,
            Invalidations = _metrics.Invalidations, Entries = _entries.Count };
    }

    private void Expire()
    {
        foreach (var key in _entries.Where(e => e.Value.Expiry <= engine.NowMicroseconds).Select(e => e.Key).ToArray()) _entries.Remove(key);
    }
}
