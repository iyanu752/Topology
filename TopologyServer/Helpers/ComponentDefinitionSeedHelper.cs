namespace TopologyServer;

public class ComponentDefinitionSeedHelper
{
    public List<ComponentDefinition> GetDefaultComponents()
    {
        return
        [
            CreateClientDefinition(),
            CreateApiGatewayDefinition(),
            CreateLoadBalancerDefinition(),
            CreateServiceDefinition(),
            CreateDatabaseDefinition(),
            CreateCacheDefinition(),
            CreateQueueDefinition(),
            CreateExternalApiDefinition()
        ];
    }

    public ComponentDefinition CreateClientDefinition()
    {
        return new ComponentDefinition
        {
            Type = ComponentType.Client,
            Label = "Client",
            Category = ComponentCategory.Network,
            Description = "A browser, mobile app, desktop app, or CLI sending requests into the system.",
            Properties =
            [
                Text("displayName", "Display Name", "Client", false),
                Text("region", "Region", "us-east-1", false),
                Number("expectedUsers", "Expected Users", "1000", 1, 100000000, false),
                Number("requestsPerSecond", "Requests Per Second", "100", 0, 10000000, false)
            ]
        };
    }

    public ComponentDefinition CreateApiGatewayDefinition()
    {
        return new ComponentDefinition
        {
            Type = ComponentType.ApiGateway,
            Label = "API Gateway",
            Category = ComponentCategory.Network,
            Description = "Routes API traffic and centralizes cross cutting concerns like auth, rate limiting, retries, and request timeouts.",
            Properties =
            [
                Text("displayName", "Display Name", "API Gateway", false),
                Text("region", "Region", "us-east-1", false),
                Number("rateLimitPerSecond", "Rate Limit Per Second", "1000", 0, 10000000, false),
                Checkbox("authRequired", "Auth Required", "true", false),
                Number("timeoutMs", "Timeout MS", "30000", 100, 120000, false),
                Checkbox("cachingEnabled", "Caching Enabled", "false", false)
            ]
        };
    }

    public ComponentDefinition CreateLoadBalancerDefinition()
    {
        return new ComponentDefinition
        {
            Type = ComponentType.LoadBalancer,
            Label = "Load Balancer",
            Category = ComponentCategory.Network,
            Description = "Distributes incoming traffic across services or servers.",
            Properties =
            [
                Text("displayName", "Display Name", "Load Balancer", false),
                Text("region", "Region", "us-east-1", false),
                Select("routingStrategy", "Routing Strategy", ["round-robin", "least-connections", "ip-hash"], "round-robin", false),
                Checkbox("healthChecksEnabled", "Health Checks Enabled", "true", false),
                Number("targetCount", "Target Count", "1", 1, 1000, false)
            ]
        };
    }

    public ComponentDefinition CreateServiceDefinition()
    {
        return new ComponentDefinition
        {
            Type = ComponentType.Service,
            Label = "Service",
            Category = ComponentCategory.Compute,
            Description = "A deployable application service or API.",
            Properties =
            [
                Text("displayName", "Display Name", "Service", false),
                Text("region", "Region", "us-east-1", false),
                Number("replicas", "Replicas", "1", 1, 100, true),
                Number("cpuCores", "CPU Cores", "1", 0.25, 128, false),
                Number("memoryGb", "Memory GB", "1", 0.125, 1024, false),
                Number("maxRequestsPerSecond", "Max Requests Per Second", "100", 1, 10000000, false),
                Checkbox("autoscalingEnabled", "Autoscaling Enabled", "false", false)
            ]
        };
    }

    public ComponentDefinition CreateDatabaseDefinition()
    {
        return new ComponentDefinition
        {
            Type = ComponentType.Database,
            Label = "Database",
            Category = ComponentCategory.Data,
            Description = "Stores persistent application data.",
            Properties =
            [
                Text("displayName", "Display Name", "Database", false),
                Text("region", "Region", "us-east-1", false),
                Select("databaseType", "Database Type", ["Postgres", "MySQL", "MongoDB", "DynamoDB"], "Postgres", true),
                Number("storageGb", "Storage GB", "20", 1, 10000, true),
                Number("replicas", "Replicas", "1", 1, 20, true),
                Number("readReplicas", "Read Replicas", "0", 0, 100, false),
                Checkbox("backupEnabled", "Backup Enabled", "false", false),
                Checkbox("failoverEnabled", "Failover Enabled", "false", false),
                Number("maxConnections", "Max Connections", "100", 1, 1000000, false)
            ]
        };
    }

    public ComponentDefinition CreateCacheDefinition()
    {
        return new ComponentDefinition
        {
            Type = ComponentType.Cache,
            Label = "Cache",
            Category = ComponentCategory.Data,
            Description = "A fast in-memory store for cached data, sessions, or temporary state.",
            Properties =
            [
                Text("displayName", "Display Name", "Cache", false),
                Text("region", "Region", "us-east-1", false),
                Select("cacheType", "Cache Type", ["Redis", "Memcached"], "Redis", false),
                Number("memoryGb", "Memory GB", "1", 0.125, 1024, false),
                Number("ttlSeconds", "TTL Seconds", "300", 1, 2592000, false),
                Select("evictionPolicy", "Eviction Policy", ["lru", "lfu", "ttl", "none"], "lru", false)
            ]
        };
    }

    public ComponentDefinition CreateQueueDefinition()
    {
        return new ComponentDefinition
        {
            Type = ComponentType.Queue,
            Label = "Queue",
            Category = ComponentCategory.Data,
            Description = "A message queue, topic, or stream for asynchronous workloads.",
            Properties =
            [
                Text("displayName", "Display Name", "Queue", false),
                Text("region", "Region", "us-east-1", false),
                Number("throughputPerSecond", "Throughput Per Second", "500", 1, 10000000, false),
                Number("retentionHours", "Retention Hours", "24", 1, 8760, false),
                Checkbox("deadLetterQueueEnabled", "Dead Letter Queue Enabled", "false", false)
            ]
        };
    }

    public ComponentDefinition CreateExternalApiDefinition()
    {
        return new ComponentDefinition
        {
            Type = ComponentType.ExternalApi,
            Label = "External API",
            Category = ComponentCategory.Network,
            Description = "An external service, third party API, webhook provider, or vendor integration.",
            Properties =
            [
                Text("displayName", "Display Name", "External API", false),
                Text("region", "Region", "us-east-1", false),
                Number("averageLatencyMs", "Average Latency MS", "250", 1, 120000, false),
                Number("rateLimitPerMinute", "Rate Limit Per Minute", "60", 1, 1000000, false),
                Number("reliabilityPercentage", "Reliability Percentage", "99", 0, 100, false)
            ]
        };
    }

    private static ComponentPropertyDefinition Select(
        string key,
        string label,
        List<string> options,
        string defaultValue,
        bool required)
    {
        return new ComponentPropertyDefinition
        {
            Key = key,
            Label = label,
            InputType = "select",
            Options = options,
            DefaultValue = defaultValue,
            Required = required
        };
    }

    private static ComponentPropertyDefinition Number(
        string key,
        string label,
        string defaultValue,
        double min,
        double max,
        bool required)
    {
        return new ComponentPropertyDefinition
        {
            Key = key,
            Label = label,
            InputType = "number",
            DefaultValue = defaultValue,
            Min = min,
            Max = max,
            Required = required
        };
    }

    private static ComponentPropertyDefinition Checkbox(
        string key,
        string label,
        string defaultValue,
        bool required)
    {
        return new ComponentPropertyDefinition
        {
            Key = key,
            Label = label,
            InputType = "checkbox",
            DefaultValue = defaultValue,
            Required = required
        };
    }

    private static ComponentPropertyDefinition Text(
        string key,
        string label,
        string defaultValue,
        bool required)
    {
        return new ComponentPropertyDefinition
        {
            Key = key,
            Label = label,
            InputType = "text",
            DefaultValue = defaultValue,
            Required = required
        };
    }
}