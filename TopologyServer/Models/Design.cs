using System;

namespace TopologyServer;

public class Design
{
    public string? Id {get; set;}
    public string RoomId {get; set;} = string.Empty;
    public string UpdatedByUserId {get; set;} = string.Empty;
    public List<Node> Nodes {get; set;} = [];
    public List<Edge> Edges {get; set;} = [];
    public int Revision {get; set;}
    public DateTime UpdatedAt {get; set;} = DateTime.UtcNow;
}
