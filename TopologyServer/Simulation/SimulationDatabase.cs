namespace TopologyServer;

public sealed class SimulationDatabase : ISimulationDependency
{
    private readonly SimulationEngine _engine;
    private readonly SimulationNetwork _network;
    private readonly SimulationBehaviorDefaults _settings;
    public SimulationResource Pool { get; }
    public SimulationResource Execution { get; }
    public long ReadsCompleted { get; private set; }
    public long WritesCompleted { get; private set; }
    public Action<SimulationRequest, string?> OnResponse { get; set; } = (_, _) => { };

    public SimulationDatabase(SimulationEngine engine, SimulationNetwork network, CompiledTopology topology, SimulationBehaviorDefaults settings)
    {
        _engine = engine;
        _network = network;
        _settings = settings;
        Pool = new(engine, topology.Service.Definition.NodeId, SimulationResourceKind.DatabasePool,
            Math.Min(settings.DatabasePoolSize, settings.DatabaseMaxConnections), settings.DatabasePoolQueueCapacity);
        Execution = new(engine, topology.Database.Definition.NodeId, SimulationResourceKind.DatabaseExecution,
            settings.DatabaseConcurrency, settings.DatabaseQueueCapacity);
    }

    public void Call(SimulationRequest request)
    {
        if (!Pool.Acquire(request, () => _network.Send(1, false, () => Arrive(request))))
            OnResponse(request, "ConnectionPoolOverloaded");
    }

    private void Arrive(SimulationRequest request)
    {
        if (!request.IsPending) { Pool.Release(request); return; }
        if (!Execution.Acquire(request, () => Process(request))) Respond(request, "DatabaseOverloaded");
    }

    private void Process(SimulationRequest request)
    {
        request.DatabaseExecuting = true;
        var duration = request.IsRead ? _settings.DatabaseReadProcessingMs : _settings.DatabaseWriteProcessingMs;
        _engine.Schedule(_engine.NowMicroseconds + SimulationTime.FromMilliseconds(duration), SimulationEventPriority.Ordinary, _ =>
        {
            request.DatabaseExecuting = false;
            if (request.IsRead) ReadsCompleted++; else WritesCompleted++;
            Execution.Release(request);
            Respond(request, null);
        });
    }

    private void Respond(SimulationRequest request, string? error)
    {
        _network.Send(1, true, () =>
        {
            Pool.Release(request);
            if (request.IsPending) OnResponse(request, error);
        });
    }

    public void Cancel(SimulationRequest request)
    {
        if (request.DatabaseExecuting) return;
        Execution.Release(request);
        Pool.Release(request);
    }
}
