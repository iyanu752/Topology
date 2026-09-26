namespace TopologyServer;

public class SimulationResultBuilder
{
    private readonly Design _design;
    private readonly Dictionary<string, SimulationNodeResult> _nodeResults;
    private readonly HashSet<string> _affectedNodeIds = [];
    private readonly List<string> _findings = [];
    private readonly List<string> _impact = [];
    private readonly List<string> _recommendations = [];
    private int _riskScore;

    public SimulationResultBuilder(Design design)
    {
        _design = design;
        _nodeResults = design.Nodes.ToDictionary(
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
                    ["label"] = node.Label,
                    ["region"] = NodePropertyReader.GetString(node, "region", "unknown") ?? "unknown"
                }
            });
    }

    public SimulationResultBuilder AddRisk(int points)
    {
        _riskScore += points;
        return this;
    }

    public SimulationResultBuilder ReduceRisk(int points)
    {
        _riskScore -= points;
        return this;
    }

    public SimulationResultBuilder AddFinding(string finding)
    {
        _findings.Add(finding);
        return this;
    }

    public SimulationResultBuilder AddImpact(string impact)
    {
        _impact.Add(impact);
        return this;
    }

    public SimulationResultBuilder AddRecommendation(string recommendation)
    {
        _recommendations.Add(recommendation);
        return this;
    }

    public SimulationResultBuilder MarkAffected(Node node)
    {
        _affectedNodeIds.Add(node.Id);
        return this;
    }

    public SimulationResultBuilder MarkAffected(IEnumerable<Node> nodes)
    {
        foreach (var node in nodes)
        {
            _affectedNodeIds.Add(node.Id);
        }

        return this;
    }

    public SimulationResultBuilder MarkAffected(IEnumerable<string> nodeIds)
    {
        foreach (var nodeId in nodeIds)
        {
            _affectedNodeIds.Add(nodeId);
        }

        return this;
    }

    public SimulationResultBuilder MarkNode(
        Node node,
        SimulationNodeStatus status,
        string message,
        int loadPercentage,
        Dictionary<string, object>? metrics = null)
    {
        return MarkNode(node.Id, status, message, loadPercentage, metrics);
    }

    public SimulationResultBuilder MarkNode(
        string nodeId,
        SimulationNodeStatus status,
        string message,
        int loadPercentage,
        Dictionary<string, object>? metrics = null)
    {
        if (!_nodeResults.TryGetValue(nodeId, out var result))
        {
            return this;
        }

        if (GetNodeStatusWeight(status) >= GetNodeStatusWeight(result.Status))
        {
            result.Status = status;
            result.Message = message;
            result.LoadPercentage = Math.Clamp(loadPercentage, 0, 100);
        }

        if (metrics is not null)
        {
            foreach (var metric in metrics)
            {
                result.Metrics[metric.Key] = metric.Value;
            }
        }

        _affectedNodeIds.Add(nodeId);
        return this;
    }

    public SimulationResult Build(SimulationScenario scenario, int? trafficPerSecond = null)
    {
        var riskScore = Math.Max(0, _riskScore);

        return new SimulationResult
        {
            Scenario = scenario,
            RiskLevel = SimulationRiskScoring.GetRiskLevel(riskScore),
            RiskScore = riskScore,
            Findings = _findings.Distinct().ToList(),
            Impact = _impact.Distinct().ToList(),
            Recommendations = _recommendations.Distinct().ToList(),
            AffectedNodeIds = _affectedNodeIds.ToList(),
            NodeResults = _nodeResults.Values.ToList(),
            EdgeResults = CreateEdgeResults(trafficPerSecond)
        };
    }

    private List<SimulationEdgeResult> CreateEdgeResults(int? trafficPerSecond)
    {
        return _design.Edges.Select(edge =>
        {
            _nodeResults.TryGetValue(edge.SourceNodeId, out var sourceResult);
            _nodeResults.TryGetValue(edge.TargetNodeId, out var targetResult);
            var worstStatus = GetWorstNodeStatus(sourceResult?.Status, targetResult?.Status);

            return new SimulationEdgeResult
            {
                EdgeId = edge.Id,
                SourceNodeId = edge.SourceNodeId,
                TargetNodeId = edge.TargetNodeId,
                Status = MapNodeStatusToEdgeStatus(worstStatus),
                Message = GetEdgeMessage(worstStatus),
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
}