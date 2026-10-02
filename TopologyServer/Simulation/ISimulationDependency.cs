namespace TopologyServer;

public interface ISimulationDependency
{
    Action<SimulationRequest, string?> OnResponse { get; set; }
    void Call(SimulationRequest request);
    void Cancel(SimulationRequest request);
}
