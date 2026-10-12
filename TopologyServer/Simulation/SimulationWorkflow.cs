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
    private readonly Dictionary<long, (SimulationRequest Request, int Depth)> _ingress = [];
    private readonly Dictionary<long, int> _returning = [];
    private readonly SimulationEndpoint? _gateway;
    private readonly SimulationEndpoint? _external;
    public SimulationDatabaseCluster Database { get; }
    public SimulationResilience Resilience { get; }
    private int _roundRobin;
    public List<SimulationResource> Resources { get; } = [];
    public List<SimulationStateTransition> Transitions { get; } = [];
    public SimulationCache Cache { get; }
    public SimulationJobQueue Jobs { get; }
    public long ReadsCompleted => Database.ReadsCompleted;
    public long WritesCompleted => Database.WritesCompleted;
    public List<SimulationEndpointMetrics> EndpointSnapshots() => new[] { _gateway, _external }.OfType<SimulationEndpoint>().Select(e => e.Snapshot()).ToList();

    public SimulationWorkflow(SimulationEngine engine, CompiledTopology topology, SimulationConfiguration config, SimulationNetwork network, SimulationClient client)
    {
        _engine = engine; _topology = topology; _config = config; _network = network; _client = client;
        Cache = new(engine, config.Cache);
        Jobs = new(engine, config.Queue);
        Database = new(engine, topology, config, network);
        Database.OnWriteCommitted = Cache.Invalidate;
        Database.OnPromoted = () => { Cache.SetAvailable(false); Cache.SetAvailable(_topology.Cache?.IsAvailable ?? true); };
        Database.OnAvailabilityChanged = (id, before, after) => Transition(topology.Database.Definition.NodeId, id, before, after);
        Resilience = new(engine, config.Retries, config.CircuitBreaker);
        if (topology.Gateway != null) { _gateway = new(engine, topology.Gateway.Definition.NodeId, true, config); Resources.Add(_gateway.Resource); }
        if (topology.ExternalApi != null) { _external = new(engine, topology.ExternalApi.Definition.NodeId, false, config); Resources.Add(_external.Resource); }
        AddReplicas(topology.Service, _services, false);
        if (topology.Worker != null) AddReplicas(topology.Worker, _workers, true);
        Resources.AddRange(Database.Resources);
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
        Forward(request, 0);
    }

    private void Forward(SimulationRequest request, int index)
    {
        if (!request.IsRunnable || _returning.ContainsKey(request.Id)) return;
        var path = _topology.IngressRoutes;
        var route = path[index];
        _network.Send(route.SourceNodeId, route.TargetNodeId, false, () =>
        {
            if (!request.IsRunnable || _returning.ContainsKey(request.Id)) return;
            _ingress[request.Id] = (request, index + 1);
            var entry = _topology.Components[route.TargetNodeId];
            if (!entry.IsAvailable) { ReturnToClient(request, "EntryUnavailable"); return; }
            if (entry == _topology.Gateway)
            {
                _gateway!.Execute(request, error => { if (error != null) ReturnToClient(request, error); else Forward(request, index + 1); });
                return;
            }
            if (entry == _topology.LoadBalancer) { Forward(request, index + 1); return; }
            var candidates = _services.Where(r => _topology.LoadBalancer == null ? r.Available : r.Healthy).ToArray();
            if (candidates.Length == 0) { ReturnToClient(request, "NoHealthyReplica"); return; }
            var replica = _config.Routing.Policy == SimulationRoutingPolicy.LeastConnections
                ? candidates.MinBy(r => r.Execution.Active + r.Execution.Snapshot().QueueDepth)!
                : candidates[_roundRobin++ % candidates.Length];
            Begin(request, replica, error => ReturnToClient(request, error), false);
        });
    }

    private void ReturnToClient(SimulationRequest request, string? error, bool interrupted = false)
    {
        if (!_ingress.TryGetValue(request.Id, out var state) || !interrupted && _returning.ContainsKey(request.Id)) return;
        var version = _returning.GetValueOrDefault(request.Id) + 1;
        _returning[request.Id] = version;
        var path = _topology.IngressRoutes;
        void Return(int depth)
        {
            if (!request.IsPending || _returning.GetValueOrDefault(request.Id) != version) return;
            if (depth == 0)
            {
                _ingress.Remove(request.Id); _returning.Remove(request.Id);
                _client.Receive(request, error);
                if (error != null) Cancel(request);
                return;
            }
            var route = path[depth - 1];
            _network.Send(route.SourceNodeId, route.TargetNodeId, true, () =>
            {
                if (depth == 1 || _topology.Components[route.SourceNodeId].IsAvailable) Return(depth - 1);
            });
        }
        Return(state.Depth);
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
                SimulationEventPriority.Ordinary, _ => { if (Live(work)) StartDependencies(work, worker); });
            if (worker) _network.Send(_topology.Queue!.Definition.NodeId, replica.NodeId, false, () => { if (Live(work)) Process(); });
            else Process();
        })) Finish(work, "ServiceOverloaded");
    }

    private bool Live(Work work) => !work.ServiceDone && work.Request.IsRunnable && work.Replica.Available;

    private void StartDependencies(Work work, bool worker)
    {
        if (worker || _external == null) { Dependency(work, worker); return; }
        var target = _topology.ExternalApi!.Definition.NodeId;
        Resilience.Execute(work.Request, $"{work.Replica.Id}->{target}", (attempt, done) =>
            _network.Send(work.Replica.NodeId, target, false, () => _external.Execute(attempt,
                error => _network.Send(work.Replica.NodeId, target, true, () => done(error)))), _external.Cancel,
            error => { if (!Live(work)) return; if (error != null) Finish(work, error); else Dependency(work, false); });
    }

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
        Resilience.Execute(work.Request, $"{work.Replica.Id}->{_topology.Database.Definition.NodeId}",
            (attempt, done) => Database.Execute(attempt, work.Replica.NodeId, work.Replica.Pool, done), Database.Cancel,
            error =>
            {
                if (!Live(work)) return;
                if (error == null && work.Request.IsRead && !work.Request.IsStaleRead && _topology.Cache != null)
                {
                    _network.Send(work.Replica.NodeId, _topology.Cache.Definition.NodeId, false, () =>
                    {
                        if (!Live(work)) return;
                        Cache.Populate(work.Request.Key, work.CacheVersion, work.CacheGeneration);
                        _network.Send(work.Replica.NodeId, _topology.Cache.Definition.NodeId, true, () => Finish(work, null));
                    });
                }
                else Finish(work, error);
            });
    }
    private void Finish(Work work, string? error)
    {
        if (work.ServiceDone) return;
        work.ServiceDone = true;
        work.Replica.Execution.Release(work.Request);
        _work.Remove(work.Request.Id);
        if (work.Request.IsPending) work.Reply(error);
    }

    public void Cancel(SimulationRequest request)
    {
        _ingress.Remove(request.Id); _returning.Remove(request.Id);
        _gateway?.Cancel(request);
        Resilience.Cancel(request);
        if (!_work.TryGetValue(request.Id, out var work)) return;
        Finish(work, null);
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
                        Resilience.Cancel(work.Request);
                    }
                }
            }
            component.IsAvailable = _services.Concat(_workers).Any(r => r.NodeId == nodeId && r.Available);
            Jobs.Pump();
            return;
        }
        if (component == _topology.Database) { Database.SetAvailable(replicaId, available); return; }
        if (component.IsAvailable == available) return;
        Transition(nodeId, null, component.IsAvailable, available);
        component.IsAvailable = available;
        if ((component == _topology.LoadBalancer || component == _topology.Gateway) && !available)
        {
            var depth = _topology.IngressRoutes.ToList().FindIndex(r => r.TargetNodeId == nodeId) + 1;
            foreach (var state in _ingress.Values.Where(s => s.Depth >= depth).ToArray())
            {
                _ingress[state.Request.Id] = (state.Request, depth);
                ReturnToClient(state.Request, component == _topology.Gateway ? "GatewayUnavailable" : "LoadBalancerUnavailable", true);
            }
        }
        if (component == _topology.Gateway) _gateway!.SetAvailable(available);
        if (component == _topology.ExternalApi) _external!.SetAvailable(available);
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
