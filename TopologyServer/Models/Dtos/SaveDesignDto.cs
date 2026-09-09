using System;

namespace TopologyServer;

public class SaveDesignDto
{
    public List<Node> Nodes {get; set;} = [];
    public List<Edge> Edges {get; set;} = [];

}
