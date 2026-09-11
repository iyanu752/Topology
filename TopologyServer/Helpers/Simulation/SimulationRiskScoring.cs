namespace TopologyServer;

public static class SimulationRiskScoring
{
    public static SimulationRiskLevel GetRiskLevel(int riskScore)
    {
        return Math.Max(0, riskScore) switch
        {
            <= 1 => SimulationRiskLevel.Low,
            <= 3 => SimulationRiskLevel.Medium,
            <= 5 => SimulationRiskLevel.High,
            _ => SimulationRiskLevel.Critical
        };
    }
}
