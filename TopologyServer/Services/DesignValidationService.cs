namespace TopologyServer;

public class DesignValidationService : IDesignValidationService
{
    private readonly IConnectionRuleService _connectionRuleService;
    private readonly IComponentLibraryService _componentLibraryService;
    private readonly ILogger<DesignValidationService> _logger;

    public DesignValidationService(
        IConnectionRuleService connectionRuleService,
        IComponentLibraryService componentLibraryService,
        ILogger<DesignValidationService> logger)
    {
        _connectionRuleService = connectionRuleService;
        _componentLibraryService = componentLibraryService;
        _logger = logger;
    }

    public async Task<DesignValidationResult> ValidateDesignAsync(SaveDesignDto design)
    {
        var result = new DesignValidationResult();
        var nodesById = design.Nodes
            .Where(node => !string.IsNullOrWhiteSpace(node.Id))
            .ToDictionary(node => node.Id, node => node);

        ValidateNodeIds(design, result);
        ValidateEdgesReferenceExistingNodes(design, nodesById, result);
        ValidateDuplicateAndSelfConnections(design, result);
        await ValidateComponentPropertiesAsync(design, result);
        ValidateConfigurationReadiness(design, result);
        await ValidateConnectionRulesAsync(design, nodesById, result);

        _logger.LogInformation(
            "Validated design with {NodeCount} nodes, {EdgeCount} edges, {IssueCount} issues",
            design.Nodes.Count,
            design.Edges.Count,
            result.Issues.Count);

        return result;
    }

    private static void ValidateNodeIds(SaveDesignDto design, DesignValidationResult result)
    {
        foreach (var node in design.Nodes.Where(node => string.IsNullOrWhiteSpace(node.Id)))
        {
            result.Issues.Add(new DesignValidationIssue
            {
                Severity = ValidationSeverity.Error,
                Message = "Every node must have an id.",
                NodeId = node.Id
            });
        }

        var duplicateNodeIds = design.Nodes
            .Where(node => !string.IsNullOrWhiteSpace(node.Id))
            .GroupBy(node => node.Id)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key);

        foreach (var nodeId in duplicateNodeIds)
        {
            result.Issues.Add(new DesignValidationIssue
            {
                Severity = ValidationSeverity.Error,
                Message = $"Node id '{nodeId}' is duplicated.",
                NodeId = nodeId
            });
        }
    }

    private static void ValidateEdgesReferenceExistingNodes(
        SaveDesignDto design,
        Dictionary<string, Node> nodesById,
        DesignValidationResult result)
    {
        foreach (var edge in design.Edges)
        {
            if (string.IsNullOrWhiteSpace(edge.SourceNodeId) || !nodesById.ContainsKey(edge.SourceNodeId))
            {
                result.Issues.Add(new DesignValidationIssue
                {
                    Severity = ValidationSeverity.Error,
                    Message = "Edge source node does not exist.",
                    EdgeId = edge.Id
                });
            }

            if (string.IsNullOrWhiteSpace(edge.TargetNodeId) || !nodesById.ContainsKey(edge.TargetNodeId))
            {
                result.Issues.Add(new DesignValidationIssue
                {
                    Severity = ValidationSeverity.Error,
                    Message = "Edge target node does not exist.",
                    EdgeId = edge.Id
                });
            }
        }
    }

    private static void ValidateDuplicateAndSelfConnections(SaveDesignDto design, DesignValidationResult result)
    {
        foreach (var edge in design.Edges.Where(edge => edge.SourceNodeId == edge.TargetNodeId))
        {
            result.Issues.Add(new DesignValidationIssue
            {
                Severity = ValidationSeverity.Error,
                Message = "A node cannot connect to itself.",
                EdgeId = edge.Id,
                NodeId = edge.SourceNodeId
            });
        }

        var duplicateEdges = design.Edges
            .GroupBy(edge => new { edge.SourceNodeId, edge.TargetNodeId })
            .Where(group => group.Count() > 1);

        foreach (var duplicate in duplicateEdges)
        {
            result.Issues.Add(new DesignValidationIssue
            {
                Severity = ValidationSeverity.Warning,
                Message = $"Duplicate connection from {duplicate.Key.SourceNodeId} to {duplicate.Key.TargetNodeId}."
            });
        }
    }

    private async Task ValidateComponentPropertiesAsync(SaveDesignDto design, DesignValidationResult result)
    {
        var componentDefinitions = await _componentLibraryService.GetComponentsAsync();
        var definitionsByType = componentDefinitions.ToDictionary(component => component.Type, component => component);

        foreach (var node in design.Nodes)
        {
            if (!definitionsByType.TryGetValue(node.Type, out var definition))
            {
                result.Issues.Add(new DesignValidationIssue
                {
                    Severity = ValidationSeverity.Warning,
                    Message = $"Component definition {node.Type} does not exist.",
                    NodeId = node.Id
                });
                continue;
            }

            foreach (var property in definition.Properties)
            {
                node.Properties.TryGetValue(property.Key, out var value);

                if (property.Required && IsMissing(value))
                {
                    result.Issues.Add(new DesignValidationIssue
                    {
                        Severity = ValidationSeverity.Error,
                        Message = $"Required property '{property.Key}' is missing.",
                        NodeId = node.Id,
                        PropertyKey = property.Key
                    });
                    continue;
                }

                if (!IsMissing(value) && property.InputType == "number")
                {
                    ValidateNumericProperty(node, property, value, result);
                }

                if (!IsMissing(value) && property.InputType == "select")
                {
                    ValidateSelectProperty(node, property, value, result);
                }
            }
        }
    }

    private async Task ValidateConnectionRulesAsync(
        SaveDesignDto design,
        Dictionary<string, Node> nodesById,
        DesignValidationResult result)
    {
        foreach (var edge in design.Edges)
        {
            if (!nodesById.TryGetValue(edge.SourceNodeId, out var sourceNode) ||
                !nodesById.TryGetValue(edge.TargetNodeId, out var targetNode))
            {
                continue;
            }

            var rule = await _connectionRuleService.GetRuleAsync(sourceNode.Type, targetNode.Type);
            if (rule is null)
            {
                result.Issues.Add(new DesignValidationIssue
                {
                    Severity = ValidationSeverity.Warning,
                    Message = $"No connection rule exists for {sourceNode.Type} -> {targetNode.Type}.",
                    EdgeId = edge.Id
                });
                continue;
            }

            if (!rule.IsAllowed)
            {
                result.Issues.Add(new DesignValidationIssue
                {
                    Severity = rule.Severity,
                    Message = rule.Message,
                    EdgeId = edge.Id
                });
            }
        }
    }

    private static void ValidateConfigurationReadiness(SaveDesignDto design, DesignValidationResult result)
    {
        var clients = design.Nodes.Where(node => node.Type == ComponentType.Client).ToList();
        var apiGateways = design.Nodes.Where(node => node.Type == ComponentType.ApiGateway).ToList();
        var loadBalancers = design.Nodes.Where(node => node.Type == ComponentType.LoadBalancer).ToList();
        var services = design.Nodes.Where(node => node.Type == ComponentType.Service).ToList();
        var databases = design.Nodes.Where(node => node.Type == ComponentType.Database).ToList();
        var queues = design.Nodes.Where(node => node.Type == ComponentType.Queue).ToList();

        if (clients.Any() && services.Any() && !apiGateways.Any())
        {
            result.Issues.Add(new DesignValidationIssue
            {
                Severity = ValidationSeverity.Warning,
                Message = "The design has clients and services but no API gateway for public traffic."
            });
        }

        if (services.Sum(service => NodePropertyReader.GetInt(service, "replicas", 1)) > 1 && !loadBalancers.Any())
        {
            result.Issues.Add(new DesignValidationIssue
            {
                Severity = ValidationSeverity.Warning,
                Message = "Multiple service replicas are configured, but no load balancer is present to distribute traffic."
            });
        }

        foreach (var gateway in apiGateways)
        {
            var rateLimitPerSecond = NodePropertyReader.GetInt(gateway, "rateLimitPerSecond", 0);
            var rateLimitPerMinute = NodePropertyReader.GetInt(gateway, "rateLimitPerMinute", 0);
            if (rateLimitPerSecond <= 0 && rateLimitPerMinute <= 0)
            {
                result.Issues.Add(new DesignValidationIssue
                {
                    Severity = ValidationSeverity.Warning,
                    Message = "API gateway has no rate limit configured.",
                    NodeId = gateway.Id,
                    PropertyKey = "rateLimitPerSecond"
                });
            }

            if (!NodePropertyReader.GetBool(gateway, "authRequired", "authEnabled", false))
            {
                result.Issues.Add(new DesignValidationIssue
                {
                    Severity = ValidationSeverity.Warning,
                    Message = "API gateway auth is disabled or missing.",
                    NodeId = gateway.Id,
                    PropertyKey = "authRequired"
                });
            }
        }

        foreach (var service in services)
        {
            var replicas = NodePropertyReader.GetInt(service, "replicas", 1);
            var maxRequestsPerSecond = NodePropertyReader.GetInt(service, "maxRequestsPerSecond", replicas * 100);
            if (replicas <= 1)
            {
                result.Issues.Add(new DesignValidationIssue
                {
                    Severity = ValidationSeverity.Warning,
                    Message = "Service has only one replica configured, creating a single point of failure.",
                    NodeId = service.Id,
                    PropertyKey = "replicas"
                });
            }

            if (maxRequestsPerSecond <= 0)
            {
                result.Issues.Add(new DesignValidationIssue
                {
                    Severity = ValidationSeverity.Error,
                    Message = "Service maxRequestsPerSecond must be greater than zero.",
                    NodeId = service.Id,
                    PropertyKey = "maxRequestsPerSecond"
                });
            }
        }

        foreach (var database in databases)
        {
            var replicas = NodePropertyReader.GetInt(database, "replicas", 1);
            var readReplicas = NodePropertyReader.GetInt(database, "readReplicas", 0);
            var hasFailover = NodePropertyReader.GetBool(database, "failoverEnabled", false);
            var hasBackup = NodePropertyReader.GetBool(database, "backupEnabled", false);

            if (replicas + readReplicas <= 1 && !hasFailover)
            {
                result.Issues.Add(new DesignValidationIssue
                {
                    Severity = ValidationSeverity.Warning,
                    Message = "Database has no replica or failover configured.",
                    NodeId = database.Id,
                    PropertyKey = "replicas"
                });
            }

            if (!hasBackup)
            {
                result.Issues.Add(new DesignValidationIssue
                {
                    Severity = ValidationSeverity.Warning,
                    Message = "Database backups are disabled or missing.",
                    NodeId = database.Id,
                    PropertyKey = "backupEnabled"
                });
            }
        }

        foreach (var queue in queues)
        {
            if (!NodePropertyReader.GetBool(queue, "deadLetterQueueEnabled", false))
            {
                result.Issues.Add(new DesignValidationIssue
                {
                    Severity = ValidationSeverity.Warning,
                    Message = "Queue has no dead-letter queue configured.",
                    NodeId = queue.Id,
                    PropertyKey = "deadLetterQueueEnabled"
                });
            }
        }
    }
    private static bool IsMissing(object? value)
    {
        return value is null || value is string text && string.IsNullOrWhiteSpace(text);
    }

    private static void ValidateNumericProperty(
        Node node,
        ComponentPropertyDefinition property,
        object? value,
        DesignValidationResult result)
    {
        if (!double.TryParse(value?.ToString(), out var numericValue))
        {
            result.Issues.Add(new DesignValidationIssue
            {
                Severity = ValidationSeverity.Error,
                Message = $"Property '{property.Key}' must be a number.",
                NodeId = node.Id,
                PropertyKey = property.Key
            });
            return;
        }

        if (property.Min.HasValue && numericValue < property.Min.Value)
        {
            result.Issues.Add(new DesignValidationIssue
            {
                Severity = ValidationSeverity.Error,
                Message = $"Property '{property.Key}' must be at least {property.Min.Value}.",
                NodeId = node.Id,
                PropertyKey = property.Key
            });
        }

        if (property.Max.HasValue && numericValue > property.Max.Value)
        {
            result.Issues.Add(new DesignValidationIssue
            {
                Severity = ValidationSeverity.Error,
                Message = $"Property '{property.Key}' must be at most {property.Max.Value}.",
                NodeId = node.Id,
                PropertyKey = property.Key
            });
        }
    }

    private static void ValidateSelectProperty(
        Node node,
        ComponentPropertyDefinition property,
        object? value,
        DesignValidationResult result)
    {
        var selectedValue = value?.ToString();

        if (selectedValue is not null && property.Options.Count > 0 && !property.Options.Contains(selectedValue))
        {
            result.Issues.Add(new DesignValidationIssue
            {
                Severity = ValidationSeverity.Error,
                Message = $"Property '{property.Key}' has an unsupported option '{selectedValue}'.",
                NodeId = node.Id,
                PropertyKey = property.Key
            });
        }
    }
}