using System;

namespace TopologyServer;

public class SimulationResult
{
    public SimulationScenario Scenario { get; set; }
    public SimulationRiskLevel RiskLevel { get; set; }
    public int RiskScore { get; set; }
    public List<string> Findings { get; set; } = [];
    public List<string> Impact { get; set; } = [];
    public List<string> Recommendations { get; set; } = [];
    public List<string> AffectedNodeIds { get; set; } = [];
    public List<SimulationNodeResult> NodeResults { get; set; } = [];
    public List<SimulationEdgeResult> EdgeResults { get; set; } = [];
}

public class SimulationNodeResult
{
    public string NodeId { get; set; } = string.Empty;
    public SimulationNodeStatus Status { get; set; } = SimulationNodeStatus.Online;
    public string Message { get; set; } = string.Empty;
    public int? LoadPercentage { get; set; }
    public Dictionary<string, object> Metrics { get; set; } = [];
}

public class SimulationEdgeResult
{
    public string EdgeId { get; set; } = string.Empty;
    public string SourceNodeId { get; set; } = string.Empty;
    public string TargetNodeId { get; set; } = string.Empty;
    public SimulationEdgeStatus Status { get; set; } = SimulationEdgeStatus.Healthy;
    public string Message { get; set; } = string.Empty;
    public int? TrafficPerSecond { get; set; }
}

public enum SimulationNodeStatus
{
    Online,
    Degraded,
    Saturated,
    Offline
}

public enum SimulationEdgeStatus
{
    Healthy,
    Degraded,
    Saturated,
    Broken
}
