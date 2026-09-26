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
        var nodeResults = CreateDefaultNodeResults(design);
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
                Recommendations = ["Add a database node if the system stores data"],
                NodeResults = nodeResults.Values.ToList(),
                EdgeResults = CreateEdgeResults(design, nodeResults, new HashSet<string>(), trafficPerSecond)
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
                Recommendations = ["Choose a database node when running a database failure simulation"],
                NodeResults = nodeResults.Values.ToList(),
                EdgeResults = CreateEdgeResults(design, nodeResults, new HashSet<string>(), trafficPerSecond)
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
            var readReplicaCount = NodePropertyReader.GetInt(databaseNode, "readReplicas", 0);
            var totalReplicaCount = replicaCount + readReplicaCount;
            var backupEnabled = NodePropertyReader.GetBool(databaseNode, "backupEnabled", false);
            var failoverEnabled = NodePropertyReader.GetBool(databaseNode, "failoverEnabled", false);

            MarkNode(nodeResults, databaseNode, SimulationNodeStatus.Offline, "Database node failed during the simulation.", 0);

            if (totalReplicaCount <= 1)
            {
                riskScore += 2;
                findings.Add($"{databaseNode.Label} has no additional replicas configured.");
                recommendations.Add("Configure at least one replica for the database.");
            }
            else
            {
                riskScore -= 2;
                findings.Add($"{databaseNode.Label} has {totalReplicaCount} total database replica(s) configured.");
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

        var failedDatabaseNodeIds = failedDatabaseNodes.Select(node => node.Id).ToHashSet();
        var affectedNodeIds = SimulationGraphHelper.GetConnectedNodeIds(design, failedDatabaseNodeIds);
        var dependentNodeIds = affectedNodeIds.Where(nodeId => !failedDatabaseNodeIds.Contains(nodeId)).ToHashSet();
        var failedDatabasesCanFailOver = failedDatabaseNodes.All(databaseNode =>
            NodePropertyReader.GetBool(databaseNode, "failoverEnabled", false) &&
            NodePropertyReader.GetInt(databaseNode, "replicas", 1) + NodePropertyReader.GetInt(databaseNode, "readReplicas", 0) > 1);

        foreach (var dependentNodeId in dependentNodeIds)
        {
            if (failedDatabasesCanFailOver)
            {
                MarkNode(nodeResults, dependentNodeId, SimulationNodeStatus.Online, "Node stays online because database failover is configured.", 35);
                continue;
            }

            MarkNode(nodeResults, dependentNodeId, SimulationNodeStatus.Degraded, "Node depends on a failed database path.", 75);
        }

        if (affectedNodeIds.Count > failedDatabaseNodes.Count)
        {
            impact.Add(failedDatabasesCanFailOver ? "Services connected to the failed database can use configured failover paths." : "Services connected to the failed database may lose read or write access.");
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
            AffectedNodeIds = affectedNodeIds,
            NodeResults = nodeResults.Values.ToList(),
            EdgeResults = CreateEdgeResults(design, nodeResults, failedDatabaseNodeIds, trafficPerSecond)
        };
    }

    private static Dictionary<string, SimulationNodeResult> CreateDefaultNodeResults(Design design)
    {
        return design.Nodes.ToDictionary(
            node => node.Id,
            node => new SimulationNodeResult
            {
                NodeId = node.Id,
                Status = SimulationNodeStatus.Online,
                Message = "Node is operating normally.",
                LoadPercentage = 20,
                Metrics = new Dictionary<string, object>
                {
                    ["componentType"] = node.Type.ToString(),
                    ["label"] = node.Label
                }
            });
    }

    private static void MarkNode(
        Dictionary<string, SimulationNodeResult> nodeResults,
        Node node,
        SimulationNodeStatus status,
        string message,
        int loadPercentage)
    {
        MarkNode(nodeResults, node.Id, status, message, loadPercentage);
    }

    private static void MarkNode(
        Dictionary<string, SimulationNodeResult> nodeResults,
        string nodeId,
        SimulationNodeStatus status,
        string message,
        int loadPercentage)
    {
        if (!nodeResults.TryGetValue(nodeId, out var result))
        {
            return;
        }

        if (GetNodeStatusWeight(status) >= GetNodeStatusWeight(result.Status))
        {
            result.Status = status;
            result.Message = message;
            result.LoadPercentage = Math.Clamp(loadPercentage, 0, 100);
        }
    }

    private static List<SimulationEdgeResult> CreateEdgeResults(
        Design design,
        IReadOnlyDictionary<string, SimulationNodeResult> nodeResults,
        IReadOnlySet<string> failedNodeIds,
        int trafficPerSecond)
    {
        return design.Edges.Select(edge =>
        {
            nodeResults.TryGetValue(edge.SourceNodeId, out var sourceResult);
            nodeResults.TryGetValue(edge.TargetNodeId, out var targetResult);
            var touchesFailedNode = failedNodeIds.Contains(edge.SourceNodeId) || failedNodeIds.Contains(edge.TargetNodeId);
            var worstStatus = GetWorstNodeStatus(sourceResult?.Status, targetResult?.Status);

            return new SimulationEdgeResult
            {
                EdgeId = edge.Id,
                SourceNodeId = edge.SourceNodeId,
                TargetNodeId = edge.TargetNodeId,
                Status = touchesFailedNode ? SimulationEdgeStatus.Broken : MapNodeStatusToEdgeStatus(worstStatus),
                Message = touchesFailedNode ? "Connection touches a failed database node." : GetEdgeMessage(worstStatus),
                TrafficPerSecond = trafficPerSecond
            };
        }).ToList();
    }

    private static SimulationNodeStatus GetWorstNodeStatus(SimulationNodeStatus? first, SimulationNodeStatus? second)
    {
        var firstStatus = first ?? SimulationNodeStatus.Online;
        var secondStatus = second ?? SimulationNodeStatus.Online;
        return GetNodeStatusWeight(firstStatus) >= GetNodeStatusWeight(secondStatus) ? firstStatus : secondStatus;
    }

    private static int GetNodeStatusWeight(SimulationNodeStatus status)
    {
        return status switch
        {
            SimulationNodeStatus.Online => 0,
            SimulationNodeStatus.Degraded => 1,
            SimulationNodeStatus.Saturated => 2,
            SimulationNodeStatus.Offline => 3,
            _ => 0
        };
    }

    private static SimulationEdgeStatus MapNodeStatusToEdgeStatus(SimulationNodeStatus status)
    {
        return status switch
        {
            SimulationNodeStatus.Degraded => SimulationEdgeStatus.Degraded,
            SimulationNodeStatus.Saturated => SimulationEdgeStatus.Saturated,
            SimulationNodeStatus.Offline => SimulationEdgeStatus.Broken,
            _ => SimulationEdgeStatus.Healthy
        };
    }

    private static string GetEdgeMessage(SimulationNodeStatus status)
    {
        return status switch
        {
            SimulationNodeStatus.Degraded => "Traffic crosses a degraded component.",
            SimulationNodeStatus.Saturated => "Traffic crosses a saturated component.",
            SimulationNodeStatus.Offline => "Traffic path is broken by an offline component.",
            _ => "Traffic path is healthy."
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
