namespace TopologyServer;

public static class CapacityCalculator
{
    public static int GetServiceCapacity(Node service)
    {
        var replicas = NodePropertyReader.GetInt(service, "replicas", 1);
        var maxRequestsPerSecond = NodePropertyReader.GetInt(service, "maxRequestsPerSecond", 100);
        var cpuCores = NodePropertyReader.GetDouble(service, "cpuCores", 1);
        var cpuCapacityBoost = Math.Max(1, (int)Math.Round(cpuCores));

        return Math.Max(1, replicas) * Math.Max(1, maxRequestsPerSecond) * cpuCapacityBoost;
    }

    public static int GetTotalServiceCapacity(IEnumerable<Node> services)
    {
        return services.Sum(GetServiceCapacity);
    }

    public static int GetApiGatewayCapacity(Node gateway)
    {
        var rateLimitPerSecond = NodePropertyReader.GetInt(gateway, "rateLimitPerSecond", 0);
        if (rateLimitPerSecond > 0)
        {
            return rateLimitPerSecond;
        }

        var rateLimitPerMinute = NodePropertyReader.GetInt(gateway, "rateLimitPerMinute", 0);
        if (rateLimitPerMinute > 0)
        {
            return Math.Max(1, rateLimitPerMinute / 60);
        }

        var replicas = NodePropertyReader.GetInt(gateway, "replicas", 1);
        return Math.Max(1, replicas) * 1000;
    }

    public static int GetTotalApiGatewayCapacity(IEnumerable<Node> gateways)
    {
        return gateways.Sum(GetApiGatewayCapacity);
    }

    public static int GetQueueThroughput(Node queue)
    {
        return NodePropertyReader.GetInt(queue, "throughputPerSecond", 500);
    }

    public static int GetTotalQueueThroughput(IEnumerable<Node> queues)
    {
        return queues.Sum(GetQueueThroughput);
    }

    public static int GetDatabaseConnectionCapacity(Node database)
    {
        return NodePropertyReader.GetInt(database, "maxConnections", 100) * 10;
    }

    public static int GetTotalDatabaseConnectionCapacity(IEnumerable<Node> databases)
    {
        return databases.Sum(GetDatabaseConnectionCapacity);
    }

    public static int GetDatabaseReplicaCount(Node database)
    {
        return NodePropertyReader.GetInt(database, "replicas", 1) + NodePropertyReader.GetInt(database, "readReplicas", 0);
    }

    public static int GetLoadBalancerTargetCount(Node loadBalancer)
    {
        return NodePropertyReader.GetInt(loadBalancer, "targetCount", 1);
    }

    public static int GetLoadPercentage(int load, int capacity)
    {
        if (capacity <= 0)
        {
            return 100;
        }

        return Math.Clamp((int)Math.Round(load / (double)capacity * 100), 0, 100);
    }
}