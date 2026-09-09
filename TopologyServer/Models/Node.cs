using System;

namespace TopologyServer;

public class Node
{
    public string Id {get; set;} = Guid.NewGuid().ToString(); 
    public NodeType Type {get; set;}
    public string Label {get; set;} = string.Empty;
    public double X {get; set;}
    public double Y {get; set;}
    public Dictionary<string, object> Properties {get; set;} = [];

}
