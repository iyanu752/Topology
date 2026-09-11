namespace TopologyServer;

public class DatabaseFailureSimulationHandler : ISimulationScenarioHandler
{
    public SimulationScenario Scenario => SimulationScenario.DatabaseFailure;

    public SimulationResult Simulate(Design design, RunSimulationDto runSimulationDto)
    {
        var databaseNodes = design.Nodes.Where(node => node.Type == ComponentType.Database).ToList();
        var failedDatabaseNodes = GetFailedDatabaseNodes(databaseNodes, runSimulationDto);
        var findings = new List<string>();
        var impact = new List<string>();
        var recommendations = new List<string>();
        var riskScore = 0;
        var trafficPerSecond = runSimulationDto.TrafficPerSecond ?? 0;
        var hasCriticalWrites = runSimulationDto.HasCriticalWrites == true;

        if (!databaseNodes.Any())
        {
            return new SimulationResult
            {
                Scenario = runSimulationDto.Scenario,
                RiskLevel = SimulationRiskLevel.Low,
                RiskScore = 0,
                Findings = ["No database nodes were found in this design"],
                Impact = ["No database failure impact was found"],
                Recommendations = ["Add a database node if the system stores data"]
            };
        }

        if (!failedDatabaseNodes.Any())
        {
            return new SimulationResult
            {
                Scenario = runSimulationDto.Scenario,
                RiskLevel = SimulationRiskLevel.Low,
                RiskScore = 0,
                Findings = ["The failed node ids do not match any database node in this design"],
                Impact = ["No database outage was detected from the selected failed nodes"],
                Recommendations = ["Choose a database node when running a database failure simulation"]
            };
        }

        if (databaseNodes.Count == 1)
        {
            riskScore += 2;
            findings.Add("The design uses a single database node.");
            recommendations.Add("Add database replicas before this system needs higher availability.");
        }

        if (trafficPerSecond <= 100)
        {
            findings.Add("Traffic is currently low.");
        }
        else if (trafficPerSecond <= 1000)
        {
            riskScore += 1;
            findings.Add("Traffic is moderate, so a database outage could affect a noticeable number of requests.");
        }
        else
        {
            riskScore += 2;
            findings.Add("Traffic is high, so a database outage could affect many requests quickly.");
            recommendations.Add("Load test the database tier and add capacity before high traffic events.");
        }

        if (hasCriticalWrites)
        {
            riskScore += 2;
            findings.Add("The simulation includes critical writes.");
            impact.Add("Failed writes could cause data loss, failed transactions, or a broken checkout/signup flow.");
            recommendations.Add("Protect critical writes with retries, idempotency keys, and durable queues where useful.");
        }

        foreach (var databaseNode in failedDatabaseNodes)
        {
            var replicaCount = NodePropertyReader.GetInt(databaseNode, "replicas", 1);
            var backupEnabled = NodePropertyReader.GetBool(databaseNode, "backupEnabled", false);
            var failoverEnabled = NodePropertyReader.GetBool(databaseNode, "failoverEnabled", false);

            if (replicaCount <= 1)
            {
                riskScore += 2;
                findings.Add($"{databaseNode.Label} has no additional replicas configured.");
                recommendations.Add("Configure at least one replica for the database.");
            }
            else
            {
                riskScore -= 2;
                findings.Add($"{databaseNode.Label} has {replicaCount} replicas configured.");
            }

            if (!backupEnabled)
            {
                riskScore += 1;
                findings.Add($"{databaseNode.Label} does not show backups enabled.");
                recommendations.Add("Enable automated backups and test restore procedures.");
            }
            else
            {
                riskScore -= 1;
                findings.Add($"{databaseNode.Label} has backups enabled.");
            }

            if (!failoverEnabled)
            {
                riskScore += 2;
                findings.Add($"{databaseNode.Label} does not show automatic failover enabled.");
                recommendations.Add("Add automatic failover so the app can continue using a healthy replica.");
            }
            else
            {
                riskScore -= 2;
                findings.Add($"{databaseNode.Label} has failover enabled.");
            }
        }

        var affectedNodeIds = SimulationGraphHelper.GetConnectedNodeIds(design, failedDatabaseNodes.Select(node => node.Id));
        if (affectedNodeIds.Count > failedDatabaseNodes.Count)
        {
            impact.Add("Services connected to the failed database may lose read or write access.");
        }

        if (!impact.Any())
        {
            impact.Add("A database failure would cause limited impact based on the current traffic and resilience settings.");
        }

        riskScore = Math.Max(0, riskScore);

        return new SimulationResult
        {
            Scenario = runSimulationDto.Scenario,
            RiskLevel = SimulationRiskScoring.GetRiskLevel(riskScore),
            RiskScore = riskScore,
            Findings = findings.Distinct().ToList(),
            Impact = impact.Distinct().ToList(),
            Recommendations = recommendations.Distinct().ToList(),
            AffectedNodeIds = affectedNodeIds
        };
    }

    private static List<Node> GetFailedDatabaseNodes(List<Node> databaseNodes, RunSimulationDto runSimulationDto)
    {
        if (!runSimulationDto.FailedNodeIds.Any())
        {
            return databaseNodes;
        }

        return databaseNodes
            .Where(node => runSimulationDto.FailedNodeIds.Contains(node.Id))
            .ToList();
    }
}
