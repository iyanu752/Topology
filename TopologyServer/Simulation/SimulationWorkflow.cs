namespace TopologyServer;

public sealed class SimulationWorkflow
{
    private sealed class Replica(string nodeId, string id, SimulationResource execution, SimulationResource pool)
    {
        public string NodeId { get; } = nodeId;
        public string Id { get; } = id;
        public SimulationResource Execution { get; } = execution;
        public SimulationResource Pool { get; } = pool;
        public bool Available { get; set; } = true;
        public bool Healthy { get; set; } = true;
        public int Generation { get; set; }
    }

    private sealed class Work(SimulationRequest request, Replica replica, Action<string?> reply)
    {
        public SimulationRequest Request { get; } = request;
        public Replica Replica { get; } = replica;
        public Action<string?> Reply { get; } = reply;
        public bool ServiceDone { get; set; }
        public bool DatabaseArrived { get; set; }
        public bool DatabaseDone { get; set; }
        public long CacheVersion { get; set; }
        public long CacheGeneration { get; set; }
    }

    private readonly SimulationEngine _engine;
    private readonly CompiledTopology _topology;
    private readonly SimulationConfiguration _config;
    private readonly SimulationNetwork _network;
    private readonly SimulationClient _client;
    private readonly List<Replica> _services = [];
    private readonly List<Replica> _workers = [];
    private readonly Dictionary<long, Work> _work = [];
    private readonly Dictionary<long, SimulationRequest> _atLoadBalancer = [];
    private readonly SimulationResource _database;
    private readonly SimulationResource _connections;
    private int _roundRobin;
    public List<SimulationResource> Resources { get; } = [];
    public List<SimulationStateTransition> Transitions { get; } = [];
    public SimulationCache Cache { get; }
    public SimulationJobQueue Jobs { get; }
    public long ReadsCompleted { get; private set; }
    public long WritesCompleted { get; private set; }

    public SimulationWorkflow(SimulationEngine engine, CompiledTopology topology, SimulationConfiguration config, SimulationNetwork network, SimulationClient client)
    {
        _engine = engine; _topology = topology; _config = config; _network = network; _client = client;
        Cache = new(engine, config.Cache);
        Jobs = new(engine, config.Queue);
        _database = new(engine, topology.Database.Definition.NodeId, SimulationResourceKind.DatabaseExecution, config.Defaults.DatabaseConcurrency, config.Defaults.DatabaseQueueCapacity, $"{topology.Database.Definition.NodeId}:1");
        _connections = new(engine, topology.Database.Definition.NodeId, SimulationResourceKind.DatabaseConnections, config.Defaults.DatabaseMaxConnections, 100000);
        AddReplicas(topology.Service, _services, false);
        if (topology.Worker != null) AddReplicas(topology.Worker, _workers, true);
        Resources.Add(_connections); Resources.Add(_database);
        Jobs.CanDeliver = () => _workers.Any(r => r.Available && r.Execution.Active < config.Queue.WorkerConcurrency);
        Jobs.Deliver = (request, done) =>
        {
            var replica = _workers.First(r => r.Available && r.Execution.Active < config.Queue.WorkerConcurrency);
            Begin(request, replica, error => _network.Send(topology.Queue!.Definition.NodeId, replica.NodeId, true, () => done(error)), true);
        };
        Jobs.CancelAttempt = Cancel;
        client.OnRequest = SendFromClient;
        client.OnTimeout = Cancel;
    }

    private void AddReplicas(SimulationRuntimeComponent component, List<Replica> target, bool worker)
    {
        for (var i = 1; i <= SimulationConfigurationResolver.ReadInteger(component, "replicas", 1); i++)
        {
            var id = $"{component.Definition.NodeId}:{i}";
            var execution = new SimulationResource(_engine, component.Definition.NodeId,
                worker ? SimulationResourceKind.WorkerExecution : SimulationResourceKind.ServiceExecution,
                worker ? _config.Queue.WorkerConcurrency : _config.Defaults.ServiceConcurrency,
                worker ? 0 : _config.Defaults.ServiceQueueCapacity, id);
            var pool = new SimulationResource(_engine, component.Definition.NodeId, SimulationResourceKind.DatabasePool,
                Math.Min(_config.Defaults.DatabasePoolSize, _config.Defaults.DatabaseMaxConnections), _config.Defaults.DatabasePoolQueueCapacity, id);
            target.Add(new(component.Definition.NodeId, id, execution, pool));
            Resources.Add(execution); Resources.Add(pool);
        }
    }

    public void Start()
    {
        foreach (var item in _config.ScheduledEvents)
            _engine.Schedule(item.AtMicroseconds, SimulationEventPriority.ScenarioChange, _ =>
            {
                if (item.Type == ScheduledSimulationEventType.TrafficChange) _client.ChangeRate(item.RequestsPerSecond!.Value);
                else SetAvailable(item.TargetNodeId!, item.TargetReplicaId, item.Type == ScheduledSimulationEventType.ComponentRecovery);
            });
        if (_topology.LoadBalancer != null) ScheduleHealthCheck(_config.Routing.HealthCheckIntervalMs * 1000L);
    }

    private void ScheduleHealthCheck(long at)
    {
        _engine.Schedule(at, SimulationEventPriority.Ordinary, _ =>
        {
            if (_topology.LoadBalancer!.IsAvailable)
                foreach (var replica in _services)
                {
                    var observed = replica.Available;
                    var generation = replica.Generation;
                    if (replica.Healthy != observed)
                        _engine.Schedule(_engine.NowMicroseconds + _config.Routing.DetectionDelayMs * 1000L, SimulationEventPriority.Ordinary,
                            _ => { if (generation == replica.Generation) replica.Healthy = observed; });
                }
            ScheduleHealthCheck(at + _config.Routing.HealthCheckIntervalMs * 1000L);
        });
    }

    private void SendFromClient(SimulationRequest request)
    {
        var entry = _topology.LoadBalancer ?? _topology.Service;
        _network.Send(_topology.Client.Definition.NodeId, entry.Definition.NodeId, false, () =>
        {
            if (!request.IsPending) return;
            if (!entry.IsAvailable) { ReturnToClient(request, "EntryUnavailable", false); return; }
            if (_topology.LoadBalancer != null) _atLoadBalancer[request.Id] = request;
            var candidates = _services.Where(r => _topology.LoadBalancer == null ? r.Available : r.Healthy).ToArray();
            if (candidates.Length == 0) { ReturnToClient(request, "NoHealthyReplica", false); return; }
            var replica = _config.Routing.Policy == SimulationRoutingPolicy.LeastConnections
                ? candidates.MinBy(r => r.Execution.Active + r.Execution.Snapshot().QueueDepth)!
                : candidates[_roundRobin++ % candidates.Length];
            if (_topology.LoadBalancer == null) Begin(request, replica, error => ReturnToClient(request, error, false), false);
            else _network.Send(entry.Definition.NodeId, replica.NodeId, false,
                () => Begin(request, replica, error => ReturnToClient(request, error, true), false));
        });
    }

    private void ReturnToClient(SimulationRequest request, string? error, bool fromService)
    {
        void Return()
        {
            _atLoadBalancer.Remove(request.Id);
            _network.Send(_topology.Client.Definition.NodeId, (_topology.LoadBalancer ?? _topology.Service).Definition.NodeId, true,
                () => { _client.Receive(request, error); if (error != null) Cancel(request); });
        }
        if (_topology.LoadBalancer != null && fromService)
            _network.Send(_topology.LoadBalancer.Definition.NodeId, _topology.Service.Definition.NodeId, true,
                () => { if (_topology.LoadBalancer.IsAvailable && _atLoadBalancer.ContainsKey(request.Id)) Return(); });
        else Return();
    }

    private void Begin(SimulationRequest request, Replica replica, Action<string?> reply, bool worker)
    {
        if (!request.IsPending) return;
        request.ReplicaId = replica.Id;
        if (!replica.Available) { reply("ServiceUnavailable"); return; }
        var work = new Work(request, replica, reply);
        _work[request.Id] = work;
        if (!replica.Execution.Acquire(request, () =>
        {
            void Process() => _engine.Schedule(_engine.NowMicroseconds + SimulationTime.FromMilliseconds(worker ? _config.Queue.WorkerProcessingMs : _config.Defaults.ServiceProcessingMs),
                SimulationEventPriority.Ordinary, _ => { if (Live(work)) Dependency(work, worker); });
            if (worker) _network.Send(_topology.Queue!.Definition.NodeId, replica.NodeId, false, () => { if (Live(work)) Process(); });
            else Process();
        })) Finish(work, "ServiceOverloaded");
    }

    private bool Live(Work work) => !work.ServiceDone && work.Request.IsRunnable && work.Replica.Available;

    private void Dependency(Work work, bool worker)
    {
        if (!worker && !work.Request.IsRead && _topology.Queue != null)
        {
            _network.Send(work.Replica.NodeId, _topology.Queue.Definition.NodeId, false, () =>
            {
                if (!Live(work)) return;
                var accepted = Jobs.Enqueue(work.Request.Key);
                work.Request.AcceptedAsJob = accepted;
                _network.Send(work.Replica.NodeId, _topology.Queue.Definition.NodeId, true, () => Finish(work, accepted ? null : "QueueRejected"));
            });
        }
        else if (work.Request.IsRead && _topology.Cache != null)
        {
            _network.Send(work.Replica.NodeId, _topology.Cache.Definition.NodeId, false, () =>
                _engine.Schedule(_engine.NowMicroseconds + SimulationTime.FromMilliseconds(_config.Cache.LookupMs), SimulationEventPriority.Ordinary, _ =>
                {
                    if (!Live(work)) return;
                    var lookup = Cache.Lookup(work.Request.Key);
                    work.CacheVersion = lookup.Version; work.CacheGeneration = lookup.Generation;
                    _network.Send(work.Replica.NodeId, _topology.Cache.Definition.NodeId, true, () =>
                    {
                        if (!Live(work)) return;
                        if (lookup.Hit) Finish(work, null); else CallDatabase(work);
                    });
                }));
        }
        else CallDatabase(work);
    }

    private void CallDatabase(Work work)
    {
        if (!Live(work)) return;
        if (!work.Replica.Pool.Acquire(work.Request, () =>
        {
            if (!_connections.Acquire(work.Request, () => _network.Send(work.Replica.NodeId, _topology.Database.Definition.NodeId, false, () => DatabaseArrive(work))))
            { work.Replica.Pool.Release(work.Request); Finish(work, "DatabaseUnavailable"); }
        })) Finish(work, "ConnectionPoolOverloaded");
    }

    private void DatabaseArrive(Work work)
    {
        if (!Live(work)) { CleanupDatabase(work); return; }
        work.DatabaseArrived = true;
        if (!_database.IsAvailable) { DatabaseReply(work, "DatabaseUnavailable"); return; }
        if (!_database.Acquire(work.Request, () =>
        {
            work.Request.DatabaseExecuting = true;
            var duration = work.Request.IsRead ? _config.Defaults.DatabaseReadProcessingMs : _config.Defaults.DatabaseWriteProcessingMs;
            _engine.Schedule(_engine.NowMicroseconds + SimulationTime.FromMilliseconds(duration), SimulationEventPriority.Ordinary, _ =>
            {
                if (work.DatabaseDone) return;
                work.Request.DatabaseExecuting = false;
                if (work.Request.IsRead) ReadsCompleted++;
                else { WritesCompleted++; Cache.Invalidate(work.Request.Key); }
                _database.Release(work.Request);
                DatabaseReply(work, null);
            });
        })) DatabaseReply(work, "DatabaseOverloaded");
    }

    private void DatabaseReply(Work work, string? error)
    {
        if (work.DatabaseDone) return;
        work.DatabaseDone = true;
        work.Request.DatabaseExecuting = false;
        _network.Send(work.Replica.NodeId, _topology.Database.Definition.NodeId, true, () =>
        {
            CleanupDatabase(work);
            if (Live(work) && error == null && work.Request.IsRead && _topology.Cache != null)
            {
                _network.Send(work.Replica.NodeId, _topology.Cache.Definition.NodeId, false, () =>
                {
                    Cache.Populate(work.Request.Key, work.CacheVersion, work.CacheGeneration);
                    _network.Send(work.Replica.NodeId, _topology.Cache.Definition.NodeId, true, () => Finish(work, null));
                });
            }
            else Finish(work, error);
        });
    }

    private void CleanupDatabase(Work work)
    {
        _database.Release(work.Request); _connections.Release(work.Request); work.Replica.Pool.Release(work.Request);
        if (work.ServiceDone) _work.Remove(work.Request.Id);
    }

    private void Finish(Work work, string? error)
    {
        if (work.ServiceDone) return;
        work.ServiceDone = true;
        work.Replica.Execution.Release(work.Request);
        if (!work.Request.DatabaseExecuting) _work.Remove(work.Request.Id);
        if (work.Request.IsPending) work.Reply(error);
    }

    public void Cancel(SimulationRequest request)
    {
        _atLoadBalancer.Remove(request.Id);
        if (!_work.TryGetValue(request.Id, out var work)) return;
        Finish(work, null);
        if (!request.DatabaseExecuting) CleanupDatabase(work);
    }

    public void SetAvailable(string nodeId, string? replicaId, bool available)
    {
        var component = _topology.Components[nodeId];
        var replicas = _services.Concat(_workers).Where(r => r.NodeId == nodeId && (replicaId == null || r.Id == replicaId)).ToArray();
        if (replicas.Length > 0)
        {
            foreach (var replica in replicas)
            {
                if (replica.Available == available) continue;
                Transition(nodeId, replica.Id, replica.Available, available);
                replica.Available = available; replica.Generation++;
                replica.Execution.SetAvailable(available);
                if (!available)
                {
                    var interrupted = _work.Values.Where(w => w.Replica == replica && !w.ServiceDone).ToArray();
                    foreach (var work in interrupted) work.Request.IsCancelled = true;
                    foreach (var work in interrupted)
                    {
                        Finish(work, "ServiceUnavailable");
                        if (!work.Request.DatabaseExecuting) CleanupDatabase(work);
                    }
                }
            }
            component.IsAvailable = _services.Concat(_workers).Any(r => r.NodeId == nodeId && r.Available);
            Jobs.Pump();
            return;
        }
        if (component.IsAvailable == available) return;
        Transition(nodeId, null, component.IsAvailable, available);
        component.IsAvailable = available;
        if (component == _topology.LoadBalancer && !available)
            foreach (var request in _atLoadBalancer.Values.ToArray()) ReturnToClient(request, "LoadBalancerUnavailable", false);
        if (component == _topology.Database)
        {
            _database.SetAvailable(available);
            if (!available)
                foreach (var work in _work.Values.Where(w => w.DatabaseArrived && !w.DatabaseDone).ToArray()) DatabaseReply(work, "DatabaseUnavailable");
        }
        if (component == _topology.Cache) Cache.SetAvailable(available);
        if (component == _topology.Queue) Jobs.SetAvailable(available);
        if (component == _topology.Client) _client.SetAvailable(available);
    }

    private void Transition(string node, string? replica, bool before, bool after)
    {
        if (!_engine.TryRetainRecords()) return;
        Transitions.Add(new() { AtMicroseconds = _engine.NowMicroseconds, NodeId = node, ReplicaId = replica,
            PreviousStatus = before ? SimulationNodeStatus.Online : SimulationNodeStatus.Offline,
            Status = after ? SimulationNodeStatus.Online : SimulationNodeStatus.Offline, Reason = after ? "Scheduled recovery" : "Scheduled failure" });
    }

    public List<SimulationNodeResult> NodeSnapshots() => _topology.Components.Values.Select(component =>
    {
        var resources = Resources.Where(r => r.Snapshot().NodeId == component.Definition.NodeId).Select(r => r.Snapshot()).ToArray();
        var waiting = resources.Sum(r => r.QueueDepth);
        var load = resources.Length == 0 ? 0 : resources.Max(r => r.Active / (double)r.Capacity);
        return new SimulationNodeResult { NodeId = component.Definition.NodeId,
            Status = !component.IsAvailable ? SimulationNodeStatus.Offline : waiting > 0 || load >= 1 ? SimulationNodeStatus.Saturated : SimulationNodeStatus.Online,
            LoadPercentage = (int)Math.Round(load * 100), Message = component.IsAvailable ? $"{waiting} waiting" : "Unavailable" };
    }).ToList();
}
