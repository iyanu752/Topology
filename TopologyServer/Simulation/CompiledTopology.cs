using System.Collections.ObjectModel;
using System.Text.Json;

namespace TopologyServer;

public sealed record SimulationCompilationIssue(string Code, string Message, string? NodeId = null, string? EdgeId = null);

public sealed class SimulationCompilationException(IReadOnlyList<SimulationCompilationIssue> issues)
    : Exception(string.Join(" ", issues.Select(issue => issue.Message)))
{
    public IReadOnlyList<SimulationCompilationIssue> Issues { get; } = Array.AsReadOnly(issues.ToArray());
}

public sealed record SimulationComponentDefinition(
    string NodeId,
    ComponentType Type,
    string Label,
    IReadOnlyDictionary<string, JsonElement> Properties);

public sealed class SimulationRuntimeComponent(SimulationComponentDefinition definition)
{
    public SimulationComponentDefinition Definition { get; } = definition;
    public bool IsAvailable { get; set; } = true;
}

public sealed record SimulationRoute(string EdgeId, string SourceNodeId, string TargetNodeId);

public sealed class CompiledTopology
{
    public string? DesignId { get; }
    public int DesignRevision { get; }
    public SimulationRuntimeComponent Client { get; }
    public SimulationRuntimeComponent Service { get; }
    public SimulationRuntimeComponent Database { get; }
    public IReadOnlyDictionary<string, SimulationRuntimeComponent> Components { get; }
    public IReadOnlyList<SimulationRoute> Routes { get; }
    public IReadOnlyList<string> UnusedNodeIds { get; }
    public IReadOnlyList<string> Assumptions { get; }

    internal CompiledTopology(string? designId, int revision, SimulationRuntimeComponent client,
        SimulationRuntimeComponent service, SimulationRuntimeComponent database,
        IEnumerable<SimulationRoute> routes, IEnumerable<string> unusedNodeIds, IEnumerable<string> assumptions)
    {
        DesignId = designId;
        DesignRevision = revision;
        Client = client;
        Service = service;
        Database = database;
        Components = new ReadOnlyDictionary<string, SimulationRuntimeComponent>(
            new[] { client, service, database }.OrderBy(c => c.Definition.NodeId, StringComparer.Ordinal)
                .ToDictionary(c => c.Definition.NodeId, StringComparer.Ordinal));
        Routes = Array.AsReadOnly(routes.ToArray());
        UnusedNodeIds = Array.AsReadOnly(unusedNodeIds.Order(StringComparer.Ordinal).ToArray());
        Assumptions = Array.AsReadOnly(assumptions.Order(StringComparer.Ordinal).ToArray());
    }
}
