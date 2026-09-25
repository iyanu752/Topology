namespace TopologyServer;

public static class TrafficSimulationEvaluator
{
    public static SimulationResult Evaluate(Design design, RunSimulationDto runSimulationDto, bool isHighTrafficScenario)
    {
        var services = design.Nodes.Where(node => node.Type == ComponentType.Service).ToList();
        var databases = design.Nodes.Where(node => node.Type == ComponentType.Database).ToList();
        var caches = design.Nodes.Where(node => node.Type == ComponentType.Cache).ToList();
        var queues = design.Nodes.Where(node => node.Type == ComponentType.Queue).ToList();
        var apiGateways = design.Nodes.Where(node => node.Type == ComponentType.ApiGateway).ToList();
        var loadBalancers = design.Nodes.Where(node => node.Type == ComponentType.LoadBalancer).ToList();
        var findings = new List<string>();
        var impact = new List<string>();
        var recommendations = new List<string>();
        var affectedNodeIds = new HashSet<string>();
        var nodeResults = CreateDefaultNodeResults(design);
        var riskScore = 0;
        var trafficPerSecond = runSimulationDto.TrafficPerSecond ?? (isHighTrafficScenario ? 2000 : 100);
        var readPercentage = runSimulationDto.ReadPercentage ?? 70;
        var writePercentage = runSimulationDto.WritePercentage ?? Math.Max(0, 100 - readPercentage);
        var serviceReplicaCount = services.Sum(service => NodePropertyReader.GetInt(service, "replicas", 1));
        var estimatedServiceCapacity = Math.Max(1, serviceReplicaCount) * 500;

        AddAffectedNodes(affectedNodeIds, services);

        if (!services.Any())
        {
            riskScore += 2;
            findings.Add("No service nodes were found to handle application traffic.");
            impact.Add("Incoming requests may have nowhere to be processed.");
            recommendations.Add("Add at least one service node for application logic.");
        }
        else
        {
            findings.Add($"The design has {serviceReplicaCount} service replica(s) available for traffic.");
        }

        if (trafficPerSecond <= estimatedServiceCapacity)
        {
            findings.Add($"Estimated service capacity can handle {trafficPerSecond} requests per second.");
            MarkNodes(nodeResults, services, SimulationNodeStatus.Online, "Service capacity is within the estimated limit.", GetLoadPercentage(trafficPerSecond, estimatedServiceCapacity));
        }
        else
        {
            riskScore += isHighTrafficScenario ? 3 : 2;
            findings.Add($"Traffic of {trafficPerSecond} requests per second is above the estimated service capacity of {estimatedServiceCapacity}.");
            impact.Add("Users may see slower responses, timeouts, or failed requests.");
            recommendations.Add("Increase service replicas or scale compute resources.");
            MarkNodes(nodeResults, services, SimulationNodeStatus.Saturated, "Service tier is above estimated capacity.", 99);
        }

        if (!apiGateways.Any())
        {
            riskScore += isHighTrafficScenario ? 2 : 1;
            findings.Add("No API gateway was found in front of the services.");
            recommendations.Add("Add an API gateway to centralize routing, auth, throttling, and request policies.");
        }
        else
        {
            AddAffectedNodes(affectedNodeIds, apiGateways);
            findings.Add("An API gateway is present for public API traffic.");

            var gatewayReplicaCount = apiGateways.Sum(gateway => NodePropertyReader.GetInt(gateway, "replicas", 1));
            var estimatedGatewayCapacity = Math.Max(1, gatewayReplicaCount) * 1000;
            var totalRateLimitPerMinute = apiGateways.Sum(gateway => NodePropertyReader.GetInt(gateway, "rateLimitPerMinute", 0));
            var hasRateLimit = totalRateLimitPerMinute > 0;
            var hasAuth = apiGateways.Any(gateway => NodePropertyReader.GetBool(gateway, "authEnabled", false));
            var gatewayStatus = SimulationNodeStatus.Online;
            var gatewayMessage = "API gateway capacity is within the estimated limit.";
            var gatewayLoad = GetLoadPercentage(trafficPerSecond, estimatedGatewayCapacity);

            if (trafficPerSecond > estimatedGatewayCapacity)
            {
                riskScore += 2;
                findings.Add($"Traffic of {trafficPerSecond} requests per second is above the estimated API gateway capacity of {estimatedGatewayCapacity}.");
                recommendations.Add("Scale API gateway replicas before high traffic events.");
                gatewayStatus = SimulationNodeStatus.Saturated;
                gatewayMessage = "API gateway is above estimated capacity.";
                gatewayLoad = 99;
            }
            else
            {
                findings.Add($"Estimated API gateway capacity can handle {trafficPerSecond} requests per second.");
            }

            if (!hasRateLimit && isHighTrafficScenario)
            {
                riskScore += 1;
                findings.Add("No API gateway rate limit is configured for the high traffic event.");
                recommendations.Add("Configure API gateway rate limits to protect downstream services.");
                if (gatewayStatus == SimulationNodeStatus.Online)
                {
                    gatewayStatus = SimulationNodeStatus.Degraded;
                    gatewayMessage = "API gateway is missing rate limits for high traffic.";
                }
            }
            else if (hasRateLimit)
            {
                findings.Add($"API gateway rate limiting is configured at {totalRateLimitPerMinute} request(s) per minute.");
            }

            if (!hasAuth)
            {
                riskScore += 1;
                findings.Add("API gateway auth is not enabled.");
                recommendations.Add("Enable auth at the API gateway for public API traffic.");
                if (gatewayStatus == SimulationNodeStatus.Online)
                {
                    gatewayStatus = SimulationNodeStatus.Degraded;
                    gatewayMessage = "API gateway is missing auth for public traffic.";
                }
            }

            MarkNodes(nodeResults, apiGateways, gatewayStatus, gatewayMessage, gatewayLoad);
        }

        if (trafficPerSecond > 500 && !loadBalancers.Any())
        {
            riskScore += 2;
            findings.Add("No load balancer was found for elevated traffic.");
            recommendations.Add("Add a load balancer to distribute traffic across service or API gateway replicas.");
        }
        else if (loadBalancers.Any())
        {
            findings.Add("A load balancer is present to distribute traffic.");
            AddAffectedNodes(affectedNodeIds, loadBalancers);
            MarkNodes(nodeResults, loadBalancers, SimulationNodeStatus.Online, "Load balancer is available to distribute traffic.", Math.Min(80, trafficPerSecond / 50));
        }

        if (readPercentage >= 70 && !caches.Any())
        {
            riskScore += isHighTrafficScenario ? 2 : 1;
            findings.Add("The workload is read-heavy, but no cache node was found.");
            recommendations.Add("Add a cache for frequently-read data to reduce database pressure.");
            MarkNodes(nodeResults, databases, SimulationNodeStatus.Degraded, "Read-heavy traffic may increase database pressure.", 82);
        }
        else if (caches.Any())
        {
            findings.Add("A cache is present for read-heavy traffic.");
            AddAffectedNodes(affectedNodeIds, caches);
            MarkNodes(nodeResults, caches, SimulationNodeStatus.Online, "Cache is available for read-heavy traffic.", Math.Min(75, readPercentage));
        }

        if (writePercentage >= 40)
        {
            if (!databases.Any())
            {
                riskScore += 2;
                findings.Add("The workload includes writes, but no database node was found.");
                impact.Add("Write operations cannot be persisted reliably.");
                recommendations.Add("Add a database for persistent writes.");
            }
            else
            {
                AddAffectedNodes(affectedNodeIds, databases);
                var databaseReplicaCount = databases.Sum(database => NodePropertyReader.GetInt(database, "replicas", 1));
                if (databaseReplicaCount <= 1 && trafficPerSecond > 500)
                {
                    riskScore += 2;
                    findings.Add("Write traffic depends on a database tier with no extra replicas configured.");
                    recommendations.Add("Add database replicas or partition writes when traffic grows.");
                    MarkNodes(nodeResults, databases, SimulationNodeStatus.Degraded, "Write traffic is pressuring a database tier with limited replicas.", 84);
                }
            }

            if (!queues.Any() && (trafficPerSecond > 1000 || runSimulationDto.HasCriticalWrites == true))
            {
                riskScore += 1;
                findings.Add("No queue was found to absorb write spikes or background work.");
                recommendations.Add("Use a queue for slow background jobs or bursty write workflows.");
            }
            else if (queues.Any())
            {
                findings.Add("A queue is present to absorb asynchronous work.");
                AddAffectedNodes(affectedNodeIds, queues);
                MarkNodes(nodeResults, queues, SimulationNodeStatus.Online, "Queue is available to absorb asynchronous work.", Math.Min(90, writePercentage + trafficPerSecond / 100));
            }
        }

        if (riskScore == 0)
        {
            impact.Add("The design appears able to withstand this traffic event with the current assumptions.");
            recommendations.Add("Keep monitoring real traffic and adjust capacity as usage grows.");
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
            AffectedNodeIds = affectedNodeIds.ToList(),
            NodeResults = nodeResults.Values.ToList(),
            EdgeResults = CreateEdgeResults(design, nodeResults, trafficPerSecond)
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

    private static void MarkNodes(
        Dictionary<string, SimulationNodeResult> nodeResults,
        IEnumerable<Node> nodes,
        SimulationNodeStatus status,
        string message,
        int loadPercentage)
    {
        foreach (var node in nodes)
        {
            if (!nodeResults.TryGetValue(node.Id, out var result))
            {
                continue;
            }

            if (GetNodeStatusWeight(status) >= GetNodeStatusWeight(result.Status))
            {
                result.Status = status;
                result.Message = message;
                result.LoadPercentage = Math.Clamp(loadPercentage, 0, 100);
            }
        }
    }

    private static List<SimulationEdgeResult> CreateEdgeResults(
        Design design,
        IReadOnlyDictionary<string, SimulationNodeResult> nodeResults,
        int trafficPerSecond)
    {
        return design.Edges.Select(edge =>
        {
            nodeResults.TryGetValue(edge.SourceNodeId, out var sourceResult);
            nodeResults.TryGetValue(edge.TargetNodeId, out var targetResult);
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

    private static int GetLoadPercentage(int trafficPerSecond, int capacity)
    {
        if (capacity <= 0)
        {
            return 100;
        }

        return Math.Clamp((int)Math.Round(trafficPerSecond / (double)capacity * 100), 0, 100);
    }

    private static void AddAffectedNodes(HashSet<string> affectedNodeIds, IEnumerable<Node> nodes)
    {
        foreach (var node in nodes)
        {
            affectedNodeIds.Add(node.Id);
        }
    }
}
