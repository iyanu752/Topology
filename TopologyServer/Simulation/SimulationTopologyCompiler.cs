using System.Collections.ObjectModel;
using System.Globalization;
using System.Text.Json;

namespace TopologyServer;

public sealed class SimulationTopologyCompiler
{
    public CompiledTopology Compile(Design design)
    {
        ArgumentNullException.ThrowIfNull(design);
        var issues = new List<SimulationCompilationIssue>();
        if (design.Nodes == null || design.Edges == null)
            throw Failure("MissingGraph", "The design must contain node and edge lists.");

        var nodes = new Dictionary<string, Node>(StringComparer.Ordinal);
        foreach (var node in design.Nodes)
        {
            if (node == null || string.IsNullOrWhiteSpace(node.Id))
                issues.Add(new("InvalidNodeId", "Every node must have a non-empty ID."));
            else if (!nodes.TryAdd(node.Id, node))
                issues.Add(new("DuplicateNodeId", $"Node ID '{node.Id}' is repeated.", node.Id));
        }

        var edgeIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (var edge in design.Edges)
        {
            if (edge == null || string.IsNullOrWhiteSpace(edge.Id))
            {
                issues.Add(new("InvalidEdgeId", "Every edge must have a non-empty ID."));
                continue;
            }
            if (!edgeIds.Add(edge.Id))
                issues.Add(new("DuplicateEdgeId", $"Edge ID '{edge.Id}' is repeated.", EdgeId: edge.Id));
            if (edge.SourceNodeId == null || edge.TargetNodeId == null ||
                !nodes.ContainsKey(edge.SourceNodeId) || !nodes.ContainsKey(edge.TargetNodeId))
                issues.Add(new("MissingEndpoint", $"Edge '{edge.Id}' must connect two existing nodes.", EdgeId: edge.Id));
        }
        ThrowIfAny(issues);

        var clients = nodes.Values.Where(n => n.Type == ComponentType.Client).ToArray();
        if (clients.Length != 1)
            throw Failure("InvalidEntryPoint", "The initial simulation requires exactly one Client node.");

        var outgoing = design.Edges.GroupBy(e => e.SourceNodeId)
            .ToDictionary(g => g.Key, g => g.OrderBy(e => e.Id, StringComparer.Ordinal).ToArray(), StringComparer.Ordinal);
        var client = clients[0];
        var clientEdge = RequireRoute(client, ComponentType.Service, outgoing, nodes);
        var service = nodes[clientEdge.TargetNodeId];
        var serviceEdge = RequireRoute(service, ComponentType.Database, outgoing, nodes);
        var database = nodes[serviceEdge.TargetNodeId];
        if (outgoing.TryGetValue(database.Id, out var databaseEdges) && databaseEdges.Length > 0)
            throw Failure("UnsupportedRoute", "The Database must end the request path. Remove its outgoing calls; responses do not need reverse edges.", database.Id, databaseEdges[0].Id);

        var active = new[] { client, service, database };
        var activeIds = active.Select(n => n.Id).ToHashSet(StringComparer.Ordinal);
        var definitions = new Dictionary<string, SimulationComponentDefinition>(StringComparer.Ordinal);
        var assumptions = new List<string>();
        foreach (var node in active.OrderBy(n => n.Id, StringComparer.Ordinal))
        {
            if (node.Properties == null)
                throw Failure("InvalidProperties", $"Node '{node.Id}' must have a property dictionary.", node.Id);
            var properties = new Dictionary<string, JsonElement>(StringComparer.Ordinal);
            foreach (var pair in node.Properties.OrderBy(p => p.Key, StringComparer.Ordinal))
            {
                try
                {
                    properties.Add(pair.Key, pair.Value is JsonElement element ? element.Clone() : JsonSerializer.SerializeToElement(pair.Value));
                }
                catch (Exception exception) when (exception is JsonException or NotSupportedException or InvalidOperationException or ArgumentException)
                {
                    throw Failure("InvalidProperty", $"Property '{pair.Key}' on node '{node.Id}' must be a valid JSON value.", node.Id);
                }
            }

            var modeled = new HashSet<string>(StringComparer.Ordinal);
            if (node.Type == ComponentType.Client)
            {
                modeled.Add("requestsPerSecond");
                ValidateInteger(node, properties, "requestsPerSecond", 0, SimulationExecutionLimits.MaximumRequestsPerSecond);
            }
            else
            {
                modeled.Add("replicas");
                ValidateInteger(node, properties, "replicas", 1, 1);
                if (node.Type == ComponentType.Database)
                {
                    modeled.UnionWith(["readReplicas", "maxConnections"]);
                    ValidateInteger(node, properties, "readReplicas", 0, 0);
                    ValidateInteger(node, properties, "maxConnections", 1, 1_000_000);
                }
            }
            foreach (var key in properties.Keys.Where(key => !modeled.Contains(key)))
                assumptions.Add($"Node '{node.Id}': property '{key}' is not modeled by the initial simulation.");
            definitions.Add(node.Id, new(node.Id, node.Type, node.Label,
                new ReadOnlyDictionary<string, JsonElement>(properties)));
        }

        return new CompiledTopology(design.Id, design.Revision,
            new(definitions[client.Id]), new(definitions[service.Id]), new(definitions[database.Id]),
            new[] { clientEdge, serviceEdge }.Select(e => new SimulationRoute(e.Id, e.SourceNodeId, e.TargetNodeId)),
            nodes.Keys.Where(id => !activeIds.Contains(id)), assumptions);
    }

    private static Edge RequireRoute(Node source, ComponentType expected,
        Dictionary<string, Edge[]> outgoing, Dictionary<string, Node> nodes)
    {
        if (!outgoing.TryGetValue(source.Id, out var edges) || edges.Length == 0)
            throw Failure("MissingDependency", $"Node '{source.Id}' needs one outgoing connection to a {expected}.", source.Id);
        if (edges.Length != 1)
            throw Failure("AmbiguousRoute", $"Node '{source.Id}' has multiple outgoing connections. The initial simulation supports one {expected} target.", source.Id);
        var edge = edges[0];
        if (nodes[edge.TargetNodeId].Type != expected)
            throw Failure("UnsupportedRoute", $"Connection '{edge.Id}' must lead from {source.Type} to {expected}. Cycles and other component paths are not supported yet.", source.Id, edge.Id);
        return edge;
    }

    private static void ValidateInteger(Node node, Dictionary<string, JsonElement> properties, string key, int minimum, int maximum)
    {
        if (!properties.TryGetValue(key, out var value)) return;
        decimal number = 0;
        var valid = value.ValueKind switch
        {
            JsonValueKind.Number => value.TryGetDecimal(out number),
            JsonValueKind.String => decimal.TryParse(value.GetString(), NumberStyles.Float, CultureInfo.InvariantCulture, out number),
            _ => false
        };
        if (!valid || number != decimal.Truncate(number) || number < minimum || number > maximum)
            throw Failure("UnsupportedPropertyValue", $"Node '{node.Id}' property '{key}' must be an integer between {minimum} and {maximum} for the initial simulation.", node.Id);
    }

    private static SimulationCompilationException Failure(string code, string message, string? nodeId = null, string? edgeId = null)
        => new([new(code, message, nodeId, edgeId)]);

    private static void ThrowIfAny(List<SimulationCompilationIssue> issues)
    {
        if (issues.Count > 0)
            throw new SimulationCompilationException(issues.OrderBy(i => i.NodeId, StringComparer.Ordinal)
                .ThenBy(i => i.EdgeId, StringComparer.Ordinal).ThenBy(i => i.Code, StringComparer.Ordinal).ToArray());
    }
}
