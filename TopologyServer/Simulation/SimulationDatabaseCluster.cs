namespace TopologyServer;

public sealed class SimulationDatabaseCluster
{
    private sealed class Replica(string id, bool promotable, SimulationResource execution, SimulationResource connections)
    {
        public string Id { get; } = id;
        public bool Promotable { get; } = promotable;
        public SimulationResource Execution { get; } = execution;
        public SimulationResource Connections { get; } = connections;
        public Dictionary<int, long> Versions { get; set; } = [];
        public long Version;
        public int Generation;
        public bool Available => Execution.IsAvailable;
    }

    private sealed class Operation(SimulationRequest request, string source, SimulationResource pool, Replica target, Action<string?> reply)
    {
        public SimulationRequest Request { get; } = request;
        public string Source { get; } = source;
        public SimulationResource Pool { get; } = pool;
        public Replica Target { get; } = target;
        public Action<string?> Reply { get; } = reply;
        public bool Done;
        public bool Sent;
    }

    private readonly SimulationEngine _engine;
    private readonly SimulationConfiguration _config;
    private readonly SimulationRuntimeComponent _component;
    private readonly SimulationNetwork _network;
    private readonly List<Replica> _replicas = [];
    private readonly Dictionary<long, Operation> _operations = [];
    private Replica _primary;
    private long _version;
    private int _term;
    private int _readIndex;
    private long _promotions;
    private long _staleReads;
    private long _rolledBackKeys;
    public long ReadsCompleted { get; private set; }
    public long WritesCompleted { get; private set; }
    public Action<int>? OnWriteCommitted { get; set; }
    public Action? OnPromoted { get; set; }
    public Action<string, bool, bool>? OnAvailabilityChanged { get; set; }
    public IEnumerable<SimulationResource> Resources => _replicas.SelectMany(r => new[] { r.Connections, r.Execution });

    public SimulationDatabaseCluster(SimulationEngine engine, CompiledTopology topology, SimulationConfiguration config, SimulationNetwork network)
    {
        _engine = engine; _config = config; _component = topology.Database; _network = network;
        var count = SimulationConfigurationResolver.ReadInteger(_component, "replicas", 1);
        var reads = SimulationConfigurationResolver.ReadInteger(_component, "readReplicas", 0);
        for (var i = 1; i <= count + reads; i++)
        {
            var node = _component.Definition.NodeId;
            var id = $"{node}:{i}";
            _replicas.Add(new(id, i <= count,
                new(engine, node, SimulationResourceKind.DatabaseExecution, config.Defaults.DatabaseConcurrency, config.Defaults.DatabaseQueueCapacity, id),
                new(engine, node, SimulationResourceKind.DatabaseConnections, config.Defaults.DatabaseMaxConnections, 100000, id)));
        }
        _primary = _replicas[0];
    }

    public void Execute(SimulationRequest request, string source, SimulationResource pool, Action<string?> reply)
    {
        var target = _primary;
        if (request.IsRead && _config.DatabaseReplication.ReadPreference == SimulationReadPreference.PreferReadReplica)
        {
            var readers = _replicas.Where(r => !r.Promotable && r.Available).ToArray();
            if (readers.Length > 0) target = readers[_readIndex++ % readers.Length];
        }
        var operation = new Operation(request, source, pool, target, reply);
        _operations.Add(request.Id, operation);
        if (!pool.Acquire(request, () =>
        {
            void Send() { operation.Sent = true; _network.Send(source, _component.Definition.NodeId, false, () => Arrive(operation)); }
            if (!target.Available)
                Send();
            else if (!target.Connections.Acquire(request, Send)) Complete(operation, "DatabaseUnavailable");
        }))
        {
            _operations.Remove(request.Id);
            reply("ConnectionPoolOverloaded");
        }
    }

    private void Arrive(Operation operation)
    {
        if (operation.Done || !_operations.ContainsKey(operation.Request.Id)) return;
        var request = operation.Request;
        var target = operation.Target;
        if (!request.IsRunnable) { Cancel(request); return; }
        if (!target.Available || !request.IsRead && target != _primary) { Complete(operation, "DatabaseUnavailable"); return; }
        if (!target.Execution.Acquire(request, () =>
        {
            request.DatabaseExecuting = true;
            var duration = request.IsRead ? _config.Defaults.DatabaseReadProcessingMs : _config.Defaults.DatabaseWriteProcessingMs;
            _engine.Schedule(_engine.NowMicroseconds + SimulationTime.FromMilliseconds(duration), SimulationEventPriority.Ordinary, _ =>
            {
                if (operation.Done) return;
                request.DatabaseExecuting = false;
                if (request.IsRead)
                {
                    ReadsCompleted++;
                    request.IsStaleRead = target.Versions.GetValueOrDefault(request.Key) < _primary.Versions.GetValueOrDefault(request.Key);
                    if (request.IsStaleRead) _staleReads++;
                }
                else
                {
                    WritesCompleted++;
                    target.Version = ++_version;
                    target.Versions[request.Key] = _version;
                    OnWriteCommitted?.Invoke(request.Key);
                    foreach (var replica in _replicas.Where(r => r != target && r.Available)) Replicate(replica, request.Key, _version);
                }
                target.Execution.Release(request);
                Complete(operation, null);
            });
        })) Complete(operation, "DatabaseOverloaded");
    }

    private void Complete(Operation operation, string? error)
    {
        if (operation.Done || !_operations.ContainsKey(operation.Request.Id)) return;
        operation.Done = true;
        operation.Request.DatabaseExecuting = false;
        operation.Target.Execution.Release(operation.Request);
        if (!operation.Sent)
        {
            Cleanup(operation);
            if (operation.Request.IsRunnable) operation.Reply(error);
            return;
        }
        _network.Send(operation.Source, _component.Definition.NodeId, true, () =>
        {
            Cleanup(operation);
            if (operation.Request.IsRunnable) operation.Reply(error);
        });
    }

    private void Cleanup(Operation operation)
    {
        _operations.Remove(operation.Request.Id);
        operation.Target.Execution.Release(operation.Request);
        operation.Target.Connections.Release(operation.Request);
        operation.Pool.Release(operation.Request);
    }

    public void Cancel(SimulationRequest request)
    {
        if (!_operations.TryGetValue(request.Id, out var operation) || request.DatabaseExecuting) return;
        operation.Done = true;
        Cleanup(operation);
    }

    private void Replicate(Replica replica, int key, long version)
    {
        var generation = replica.Generation;
        var term = _term;
        _engine.Schedule(_engine.NowMicroseconds + SimulationTime.FromMilliseconds(_config.DatabaseReplication.LagMs), SimulationEventPriority.Ordinary, _ =>
        {
            if (!replica.Available || generation != replica.Generation || term != _term) return;
            replica.Versions[key] = Math.Max(replica.Versions.GetValueOrDefault(key), version);
            replica.Version = Math.Max(replica.Version, version);
        });
    }

    private void CatchUp(Replica replica)
    {
        var source = _primary;
        if (!source.Available || source == replica) return;
        foreach (var pair in source.Versions) Replicate(replica, pair.Key, pair.Value);
    }

    public void SetAvailable(string? replicaId, bool available)
    {
        var changed = _replicas.Where(r => (replicaId == null || r.Id == replicaId) && r.Available != available).ToArray();
        foreach (var replica in changed)
        {
            OnAvailabilityChanged?.Invoke(replica.Id, replica.Available, available);
            replica.Generation++;
            replica.Execution.SetAvailable(available);
            replica.Connections.SetAvailable(available);
        }
        foreach (var replica in changed)
        {
            if (!available)
                foreach (var operation in _operations.Values.Where(o => o.Target == replica && !o.Done).ToArray()) Complete(operation, "DatabaseUnavailable");
            else if (replica != _primary)
            {
                replica.Versions.Clear(); replica.Version = 0;
                CatchUp(replica);
            }
        }
        _component.IsAvailable = _replicas.Any(r => r.Available);
        if (_primary.Available)
        {
            if (changed.Contains(_primary)) foreach (var replica in _replicas.Where(r => r != _primary && r.Available)) CatchUp(replica);
            return;
        }
        if (!_config.DatabaseReplication.FailoverEnabled || changed.Length == 0) return;
        var previous = _primary;
        var generation = previous.Generation;
        var term = _term;
        _engine.Schedule(_engine.NowMicroseconds + SimulationTime.FromMilliseconds(_config.DatabaseReplication.DetectionDelayMs) + SimulationTime.FromMilliseconds(_config.DatabaseReplication.PromotionDelayMs),
            SimulationEventPriority.Ordinary, _ =>
            {
                if (term != _term || previous.Available || generation != previous.Generation) return;
                var candidate = _replicas.Where(r => r.Promotable && r.Available).OrderByDescending(r => r.Version).ThenBy(r => r.Id, StringComparer.Ordinal).FirstOrDefault();
                if (candidate == null) return;
                _rolledBackKeys += previous.Versions.Count(p => candidate.Versions.GetValueOrDefault(p.Key) < p.Value);
                _primary = candidate; _term++; _promotions++;
                OnPromoted?.Invoke();
                foreach (var replica in _replicas.Where(r => r != candidate && r.Available))
                { replica.Generation++; replica.Versions.Clear(); replica.Version = 0; CatchUp(replica); }
            });
    }

    public SimulationReplicationMetrics Snapshot() => new()
    {
        PrimaryReplicaId = _primary.Id, Promotions = _promotions, StaleReads = _staleReads, RolledBackKeys = _rolledBackKeys,
        Replicas = _replicas.Select(r => new SimulationDatabaseReplicaMetrics { ReplicaId = r.Id,
            Role = r == _primary ? "Primary" : r.Promotable ? "Standby" : "ReadReplica", IsAvailable = r.Available, AppliedVersion = r.Version }).ToList()
    };
}
