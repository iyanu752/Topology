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
}
