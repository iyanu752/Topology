namespace TopologyServer;

public class ConnectionRuleSeedHelper
{
    public List<ConnectionRule> GetDefaultRules()
    {
        return
        [
            Allow(ComponentType.Client, ComponentType.ApiGateway, "Clients can send API traffic through an API gateway."),
            Allow(ComponentType.Client, ComponentType.LoadBalancer, "Clients can send traffic through a load balancer."),
            Allow(ComponentType.Client, ComponentType.Service, "Clients can call public facing services, though an API gateway is preferred for public APIs."),
            Block(ComponentType.Client, ComponentType.Database, ValidationSeverity.Error, "Clients should not connect directly to databases."),
            Block(ComponentType.Client, ComponentType.Cache, ValidationSeverity.Warning, "Clients usually should not connect directly to caches."),
            Allow(ComponentType.ApiGateway, ComponentType.LoadBalancer, "API gateways can route traffic to a load balancer."),
            Allow(ComponentType.ApiGateway, ComponentType.Service, "API gateways can route requests to backend services."),
            Block(ComponentType.ApiGateway, ComponentType.Database, ValidationSeverity.Warning, "API gateways should usually route to services, not directly to databases."),
            Block(ComponentType.ApiGateway, ComponentType.Cache, ValidationSeverity.Warning, "API gateways should usually not connect directly to caches."),
            Allow(ComponentType.LoadBalancer, ComponentType.ApiGateway, "Load balancers can distribute traffic across API gateway replicas."),
            Allow(ComponentType.LoadBalancer, ComponentType.Service, "Load balancers can route traffic to services."),
            Block(ComponentType.LoadBalancer, ComponentType.Database, ValidationSeverity.Warning, "Load balancers usually route traffic to services, not databases."),
            Allow(ComponentType.Service, ComponentType.Database, "Services can read from and write to databases."),
            Allow(ComponentType.Service, ComponentType.Cache, "Services can use caches."),
            Allow(ComponentType.Service, ComponentType.Queue, "Services can publish jobs or events to queues."),
            Allow(ComponentType.Queue, ComponentType.Service, "Queues can feed services or workers."),
            Block(ComponentType.Database, ComponentType.Client, ValidationSeverity.Error, "Databases should not connect directly to clients."),
            Allow(ComponentType.Service, ComponentType.ExternalApi, "Services can call external APIs."),
            Allow(ComponentType.ExternalApi, ComponentType.Service, "External APIs can call webhook or callback services."),
            Allow(ComponentType.ExternalApi, ComponentType.ApiGateway, "External APIs can call public webhook endpoints through an API gateway.")
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