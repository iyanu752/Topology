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
            Description = "A browser, mobile app, desktop app, or CLI.",
            Properties =
            [
                Select("clientType", "Client Type", ["Browser", "Mobile", "Desktop", "CLI"], "Browser", true),
                Text("platform", "Platform", "Web", false)
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
                Number("replicas", "Replicas", "2", 1, 20, true),
                Number("rateLimitPerMinute", "Rate Limit Per Minute", "6000", 1, 1000000, false),
                Checkbox("authEnabled", "Auth Enabled", "true", false),
                Number("requestTimeoutMs", "Request Timeout MS", "3000", 100, 120000, false),
                Checkbox("retriesEnabled", "Retries Enabled", "true", false)
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
                Select("algorithm", "Algorithm", ["RoundRobin", "LeastConnections", "IpHash"], "RoundRobin", true),
                Number("replicas", "Replicas", "2", 1, 20, true),
                Text("healthCheckPath", "Health Check Path", "/health", false)
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
                Number("replicas", "Replicas", "1", 1, 100, true),
                Number("cpuCores", "CPU Cores", "1", 0.25, 128, false),
                Number("memoryMb", "Memory MB", "512", 128, 262144, false),
                Select("runtime", "Runtime", [".NET", "Node.js", "Python", "Go", "Java"], ".NET", false)
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
                Select("databaseType", "Database Type", ["SQL", "NoSQL"], "SQL", true),
                Select("engine", "Engine", ["Postgres", "MySQL", "MongoDB", "DynamoDB"], "Postgres", true),
                Number("replicas", "Replicas", "1", 1, 20, true),
                Number("storageGb", "Storage GB", "20", 1, 10000, true),
                Checkbox("backupEnabled", "Backup Enabled", "true", false),
                Checkbox("failoverEnabled", "Failover Enabled", "false", false)
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
                Select("engine", "Engine", ["Redis", "Memcached"], "Redis", true),
                Number("memoryMb", "Memory MB", "512", 128, 262144, true),
                Number("replicas", "Replicas", "1", 1, 20, false),
                Number("ttlSeconds", "TTL Seconds", "300", 1, 2592000, false)
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
                Select("queueType", "Queue Type", ["Queue", "Topic", "Stream"], "Queue", true),
                Number("partitions", "Partitions", "1", 1, 1000, false),
                Number("retentionHours", "Retention Hours", "24", 1, 8760, false),
                Number("maxMessageSizeKb", "Max Message Size KB", "256", 1, 10240, false)
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
                Text("provider", "Provider", "External Provider", false),
                Select("protocol", "Protocol", ["HTTP", "gRPC", "Webhook"], "HTTP", true),
                Number("rateLimitPerMinute", "Rate Limit Per Minute", "60", 1, 1000000, false)
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
