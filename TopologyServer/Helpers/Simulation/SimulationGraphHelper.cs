namespace TopologyServer;

public static class SimulationGraphHelper
{
    public static List<string> GetConnectedNodeIds(Design design, IEnumerable<string> nodeIds)
    {
        var selectedIds = nodeIds.ToHashSet();
        var connectedIds = selectedIds.ToHashSet();

        foreach (var edge in design.Edges)
        {
            if (selectedIds.Contains(edge.SourceNodeId))
            {
                connectedIds.Add(edge.TargetNodeId);
            }

            if (selectedIds.Contains(edge.TargetNodeId))
            {
                connectedIds.Add(edge.SourceNodeId);
            }
        }

        return connectedIds.ToList();
    }
}
