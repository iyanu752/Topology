namespace TopologyServer;

public class ConnectionRuleSeedHelper
{
    public List<ConnectionRule> GetDefaultRules()
    {
        return
        [
            Allow(ComponentType.Client, ComponentType.LoadBalancer, "Clients can send traffic through a load balancer."),
            Allow(ComponentType.Client, ComponentType.Service, "Clients can call public-facing services."),
            Block(ComponentType.Client, ComponentType.Database, ValidationSeverity.Error, "Clients should not connect directly to databases."),
            Block(ComponentType.Client, ComponentType.Cache, ValidationSeverity.Warning, "Clients usually should not connect directly to caches."),
            Allow(ComponentType.LoadBalancer, ComponentType.Service, "Load balancers can route traffic to services."),
            Block(ComponentType.LoadBalancer, ComponentType.Database, ValidationSeverity.Warning, "Load balancers usually route traffic to services, not databases."),
            Allow(ComponentType.Service, ComponentType.Database, "Services can read from and write to databases."),
            Allow(ComponentType.Service, ComponentType.Cache, "Services can use caches."),
            Allow(ComponentType.Service, ComponentType.Queue, "Services can publish jobs or events to queues."),
            Allow(ComponentType.Queue, ComponentType.Service, "Queues can feed services or workers."),
            Block(ComponentType.Database, ComponentType.Client, ValidationSeverity.Error, "Databases should not connect directly to clients."),
            Allow(ComponentType.Service, ComponentType.ExternalApi, "Services can call external APIs."),
            Allow(ComponentType.ExternalApi, ComponentType.Service, "External APIs can call webhook or callback services.")
        ];
    }

    private static ConnectionRule Allow(ComponentType source, ComponentType target, string message)
    {
        return new ConnectionRule
        {
            SourceType = source,
            TargetType = target,
            IsAllowed = true,
            Severity = ValidationSeverity.Info,
            Message = message
        };
    }

    private static ConnectionRule Block(
        ComponentType source,
        ComponentType target,
        ValidationSeverity severity,
        string message)
    {
        return new ConnectionRule
        {
            SourceType = source,
            TargetType = target,
            IsAllowed = false,
            Severity = severity,
            Message = message
        };
    }
}