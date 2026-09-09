using System;

namespace TopologyServer;

public class Edge
{
    public string Id {get; set;} = Guid.NewGuid().ToString();
    public string SourceNodeId {get; set;} = string.Empty;
    public string TargetNodeId {get; set;} = string.Empty;
    public string? Label {get; set;}

}
