using System.Text.Json;

namespace TopologyServer.Tests;

public class SimulationTopologyCompilerTests
{
    private readonly SimulationTopologyCompiler _compiler = new();

    [Fact]
    public void ValidDesign_CompilesInRequestOrder()
    {
        var topology = _compiler.Compile(CreateDesign());
        Assert.Equal("design", topology.DesignId);
        Assert.Equal(2, topology.DesignRevision);
        Assert.Equal(ComponentType.Client, topology.Client.Definition.Type);
        Assert.Equal(ComponentType.Service, topology.Service.Definition.Type);
        Assert.Equal(ComponentType.Database, topology.Database.Definition.Type);
        Assert.Equal(new[] { "client-service", "service-db" }, topology.Routes.Select(r => r.EdgeId));
        Assert.Equal(3, topology.Components.Count);
        Assert.Empty(topology.UnusedNodeIds);
    }

    [Fact]
    public void Compilations_IsolateNestedPropertiesAndRuntimeState()
    {
        var design = CreateDesign();
        var nested = new Dictionary<string, object> { ["values"] = new List<int> { 1, 2 } };
        design.Nodes[1].Properties["extra"] = nested;
        var first = _compiler.Compile(design);
        var second = _compiler.Compile(design);
        ((List<int>)nested["values"])[0] = 99;
        design.Nodes[1].Label = "changed";
        design.Edges[0].TargetNodeId = "db";
        design.Revision = 3;
        first.Service.IsAvailable = false;

        Assert.True(second.Service.IsAvailable);
        Assert.Equal(1, first.Service.Definition.Properties["extra"].GetProperty("values")[0].GetInt32());
        Assert.Equal("Service", first.Service.Definition.Label);
        Assert.Equal("service", first.Routes[0].TargetNodeId);
        Assert.Equal(2, first.DesignRevision);
        Assert.Throws<NotSupportedException>(() =>
            ((IDictionary<string, JsonElement>)first.Service.Definition.Properties).Clear());
    }

    [Fact]
    public void JsonProperties_SurviveSourceDocumentDisposal()
    {
        var design = CreateDesign();
        CompiledTopology topology;
        using (var document = JsonDocument.Parse("{\"replicas\":1,\"extra\":{\"nested\":true}}"))
        {
            design.Nodes[1].Properties["replicas"] = document.RootElement.GetProperty("replicas");
            design.Nodes[1].Properties["extra"] = document.RootElement.GetProperty("extra");
            topology = _compiler.Compile(design);
        }
        Assert.True(topology.Service.Definition.Properties["extra"].GetProperty("nested").GetBoolean());
    }

    [Fact]
    public void UnreachableNodes_AreUnusedAndDoNotAddRuntimeCapacity()
    {
        var design = CreateDesign();
        design.Nodes.Add(new Node { Id = "unused", Type = ComponentType.Cache });
        design.Edges.Add(new Edge { Id = "unused-edge", SourceNodeId = "unused", TargetNodeId = "unused" });
        var topology = _compiler.Compile(design);
        Assert.Equal(new[] { "unused" }, topology.UnusedNodeIds);
        Assert.Equal(3, topology.Components.Count);
        Assert.Equal(2, topology.Routes.Count);
    }

    [Fact]
    public void ReorderingSavedLists_DoesNotChangeCompiledOutput()
    {
        var design = CreateDesign();
        design.Nodes[1].Properties["cpuCores"] = 4;
        design.Nodes[1].Properties["memoryGb"] = 8;
        var first = _compiler.Compile(design);
        design.Nodes.Reverse();
        design.Edges.Reverse();
        var second = _compiler.Compile(design);
        Assert.Equal(first.Components.Keys, second.Components.Keys);
        Assert.Equal(first.Routes, second.Routes);
        Assert.Equal(first.Assumptions, second.Assumptions);
        Assert.Equal(2, first.Assumptions.Count);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(2)]
    public void RequiresExactlyOneClient(int count)
    {
        var design = CreateDesign();
        if (count == 0) design.Nodes[0].Type = ComponentType.Service;
        else design.Nodes.Add(new Node { Id = "other-client", Type = ComponentType.Client });
        AssertIssue(design, "InvalidEntryPoint");
    }

    [Theory]
    [InlineData("missing", "MissingDependency")]
    [InlineData("branch", "AmbiguousRoute")]
    [InlineData("parallel", "AmbiguousRoute")]
    [InlineData("cycle", "UnsupportedRoute")]
    [InlineData("reverse", "UnsupportedRoute")]
    [InlineData("unsupported", "UnsupportedRoute")]
    public void InvalidRoutes_AreRejected(string mutation, string code)
    {
        var design = CreateDesign();
        switch (mutation)
        {
            case "missing": design.Edges.RemoveAt(1); break;
            case "branch": design.Edges.Add(new Edge { Id = "branch", SourceNodeId = "service", TargetNodeId = "client" }); break;
            case "parallel": design.Edges.Add(new Edge { Id = "parallel", SourceNodeId = "client", TargetNodeId = "service" }); break;
            case "cycle": design.Edges[1].TargetNodeId = "client"; break;
            case "reverse": design.Edges.Add(new Edge { Id = "reverse", SourceNodeId = "db", TargetNodeId = "service" }); break;
            case "unsupported": design.Nodes[1].Type = ComponentType.ApiGateway; break;
        }
        AssertIssue(design, code);
    }

    [Theory]
    [InlineData("node", "DuplicateNodeId")]
    [InlineData("edge", "DuplicateEdgeId")]
    [InlineData("endpoint", "MissingEndpoint")]
    [InlineData("blank", "InvalidNodeId")]
    public void MalformedGraph_HasActionableErrors(string mutation, string code)
    {
        var design = CreateDesign();
        switch (mutation)
        {
            case "node": design.Nodes.Add(new Node { Id = "service" }); break;
            case "edge": design.Edges.Add(design.Edges[0]); break;
            case "endpoint": design.Edges[0].TargetNodeId = "absent"; break;
            case "blank": design.Nodes[0].Id = " "; break;
        }
        AssertIssue(design, code);
    }

    [Theory]
    [InlineData(1, "replicas", "2")]
    [InlineData(2, "replicas", "0")]
    [InlineData(2, "readReplicas", "1")]
    [InlineData(2, "maxConnections", "1.5")]
    [InlineData(2, "maxConnections", "bad")]
    [InlineData(0, "requestsPerSecond", "-1")]
    public void UnsupportedNumericSettings_AreRejected(int index, string key, string value)
    {
        var design = CreateDesign();
        design.Nodes[index].Properties[key] = value;
        AssertIssue(design, "UnsupportedPropertyValue");
    }

    [Fact]
    public void NumericRepresentations_AndZeroTrafficAreAccepted()
    {
        var design = CreateDesign();
        design.Nodes[0].Properties["requestsPerSecond"] = 0;
        design.Nodes[1].Properties["replicas"] = "1";
        design.Nodes[2].Properties["replicas"] = 1L;
        design.Nodes[2].Properties["readReplicas"] = 0m;
        design.Nodes[2].Properties["maxConnections"] = 100.0;
        Assert.Empty(_compiler.Compile(design).Assumptions);
    }

    private void AssertIssue(Design design, string code)
    {
        var exception = Assert.Throws<SimulationCompilationException>(() => _compiler.Compile(design));
        Assert.Contains(exception.Issues, issue => issue.Code == code && !string.IsNullOrWhiteSpace(issue.Message));
    }

    private static Design CreateDesign() => new()
    {
        Id = "design", Revision = 2,
        Nodes =
        [
            new() { Id = "client", Type = ComponentType.Client, Label = "Client" },
            new() { Id = "service", Type = ComponentType.Service, Label = "Service" },
            new() { Id = "db", Type = ComponentType.Database, Label = "Database" }
        ],
        Edges =
        [
            new() { Id = "client-service", SourceNodeId = "client", TargetNodeId = "service" },
            new() { Id = "service-db", SourceNodeId = "service", TargetNodeId = "db" }
        ]
    };
}
