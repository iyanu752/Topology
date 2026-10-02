namespace TopologyServer;

public sealed class SimulationServiceRuntime
{
    private readonly SimulationEngine _engine;
    private readonly SimulationBehaviorDefaults _settings;
    private readonly ISimulationDependency _database;
    public SimulationResource Execution { get; }
    public Action<SimulationRequest, string?> OnResponse { get; set; } = (_, _) => { };

    public SimulationServiceRuntime(SimulationEngine engine, CompiledTopology topology,
        SimulationBehaviorDefaults settings, ISimulationDependency database)
    {
        _engine = engine;
        _settings = settings;
        _database = database;
        Execution = new(engine, topology.Service.Definition.NodeId, SimulationResourceKind.ServiceExecution,
            settings.ServiceConcurrency, settings.ServiceQueueCapacity);
        database.OnResponse = Respond;
    }

    public void Arrive(SimulationRequest request)
    {
        if (!request.IsPending) return;
        if (!Execution.Acquire(request, () => Process(request))) OnResponse(request, "ServiceOverloaded");
    }

    private void Process(SimulationRequest request)
    {
        _engine.Schedule(_engine.NowMicroseconds + SimulationTime.FromMilliseconds(_settings.ServiceProcessingMs),
            SimulationEventPriority.Ordinary, _ => { if (request.IsPending) _database.Call(request); });
    }

    private void Respond(SimulationRequest request, string? error)
    {
        Execution.Release(request);
        if (request.IsPending) OnResponse(request, error);
    }

    public void Cancel(SimulationRequest request)
    {
        _database.Cancel(request);
        Execution.Release(request);
    }
}
