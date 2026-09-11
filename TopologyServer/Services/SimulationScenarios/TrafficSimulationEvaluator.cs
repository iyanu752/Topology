namespace TopologyServer;

public static class TrafficSimulationEvaluator
{
    public static SimulationResult Evaluate(Design design, RunSimulationDto runSimulationDto, bool isHighTrafficScenario)
    {
        var services = design.Nodes.Where(node => node.Type == ComponentType.Service).ToList();
        var databases = design.Nodes.Where(node => node.Type == ComponentType.Database).ToList();
        var caches = design.Nodes.Where(node => node.Type == ComponentType.Cache).ToList();
        var queues = design.Nodes.Where(node => node.Type == ComponentType.Queue).ToList();
        var loadBalancers = design.Nodes.Where(node => node.Type == ComponentType.LoadBalancer).ToList();
        var findings = new List<string>();
        var impact = new List<string>();
        var recommendations = new List<string>();
        var affectedNodeIds = new HashSet<string>();
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
        }
        else
        {
            riskScore += isHighTrafficScenario ? 3 : 2;
            findings.Add($"Traffic of {trafficPerSecond} requests per second is above the estimated service capacity of {estimatedServiceCapacity}.");
            impact.Add("Users may see slower responses, timeouts, or failed requests.");
            recommendations.Add("Increase service replicas or scale compute resources.");
        }

        if (trafficPerSecond > 500 && !loadBalancers.Any())
        {
            riskScore += 2;
            findings.Add("No load balancer was found for elevated traffic.");
            recommendations.Add("Add a load balancer to distribute traffic across service replicas.");
        }
        else if (loadBalancers.Any())
        {
            findings.Add("A load balancer is present to distribute traffic.");
            AddAffectedNodes(affectedNodeIds, loadBalancers);
        }

        if (readPercentage >= 70 && !caches.Any())
        {
            riskScore += isHighTrafficScenario ? 2 : 1;
            findings.Add("The workload is read-heavy, but no cache node was found.");
            recommendations.Add("Add a cache for frequently-read data to reduce database pressure.");
        }
        else if (caches.Any())
        {
            findings.Add("A cache is present for read-heavy traffic.");
            AddAffectedNodes(affectedNodeIds, caches);
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
            AffectedNodeIds = affectedNodeIds.ToList()
        };
    }

    private static void AddAffectedNodes(HashSet<string> affectedNodeIds, IEnumerable<Node> nodes)
    {
        foreach (var node in nodes)
        {
            affectedNodeIds.Add(node.Id);
        }
    }
}
