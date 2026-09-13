using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace TopologyServer.Tests;

public sealed class SimulationControllerTests : IClassFixture<TestWebApplicationFactory>
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    private readonly TestWebApplicationFactory _factory;

    public SimulationControllerTests(TestWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task RunSimulation_WithLowTrafficAndDatabaseResilience_ReturnsLowRisk()
    {
        ResetDesignServiceWithDatabase(new Dictionary<string, object>
        {
            ["replicas"] = 3,
            ["backupEnabled"] = true,
            ["failoverEnabled"] = true
        });
        using var client = CreateAuthenticatedClient();
        var dto = new RunSimulationDto
        {
            Scenario = SimulationScenario.DatabaseFailure,
            TrafficPerSecond = 40,
            HasCriticalWrites = false,
            FailedNodeIds = ["db-1"]
        };

        var response = await client.PostAsJsonAsync("/api/rooms/room-1/simulations/run", dto, JsonOptions);
        var result = await response.Content.ReadFromJsonAsync<SimulationResult>(JsonOptions);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(result);
        Assert.Equal(SimulationRiskLevel.Low, result.RiskLevel);
        Assert.Contains("db-1", result.AffectedNodeIds);
        Assert.Contains(result.Findings, finding => finding.Contains("Traffic is currently low"));
    }

    [Fact]
    public async Task RunSimulation_WithHighTrafficCriticalWritesAndSingleDatabase_ReturnsCriticalRisk()
    {
        ResetDesignServiceWithDatabase(new Dictionary<string, object>
        {
            ["replicas"] = 1,
            ["backupEnabled"] = false,
            ["failoverEnabled"] = false
        });
        using var client = CreateAuthenticatedClient();
        var dto = new RunSimulationDto
        {
            Scenario = SimulationScenario.DatabaseFailure,
            TrafficPerSecond = 2500,
            HasCriticalWrites = true,
            FailedNodeIds = ["db-1"]
        };

        var response = await client.PostAsJsonAsync("/api/rooms/room-1/simulations/run", dto, JsonOptions);
        var result = await response.Content.ReadFromJsonAsync<SimulationResult>(JsonOptions);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(result);
        Assert.Equal(SimulationRiskLevel.Critical, result.RiskLevel);
        Assert.Contains("api-1", result.AffectedNodeIds);
        Assert.Contains(result.Recommendations, recommendation => recommendation.Contains("automatic failover"));
    }

    [Fact]
    public async Task RunSimulation_WithNormalTrafficAndBalancedDesign_ReturnsLowRisk()
    {
        ResetDesignServiceWithTrafficDesign(includeApiGateway: true, includeLoadBalancer: true, includeCache: true, includeQueue: true, serviceReplicas: 3, databaseReplicas: 2);
        using var client = CreateAuthenticatedClient();
        var dto = new RunSimulationDto
        {
            Scenario = SimulationScenario.NormalTraffic,
            TrafficPerSecond = 300,
            ReadPercentage = 70,
            WritePercentage = 30
        };

        var response = await client.PostAsJsonAsync("/api/rooms/room-1/simulations/run", dto, JsonOptions);
        var result = await response.Content.ReadFromJsonAsync<SimulationResult>(JsonOptions);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(result);
        Assert.Equal(SimulationRiskLevel.Low, result.RiskLevel);
        Assert.Contains(result.Impact, item => item.Contains("withstand this traffic event"));
    }

    [Fact]
    public async Task RunSimulation_WithHighReadTrafficAndMissingScalingComponents_ReturnsCriticalRisk()
    {
        ResetDesignServiceWithTrafficDesign(includeApiGateway: false, includeLoadBalancer: false, includeCache: false, includeQueue: false, serviceReplicas: 1, databaseReplicas: 1);
        using var client = CreateAuthenticatedClient();
        var dto = new RunSimulationDto
        {
            Scenario = SimulationScenario.HighTraffic,
            TrafficPerSecond = 2500,
            ReadPercentage = 85,
            WritePercentage = 15
        };

        var response = await client.PostAsJsonAsync("/api/rooms/room-1/simulations/run", dto, JsonOptions);
        var result = await response.Content.ReadFromJsonAsync<SimulationResult>(JsonOptions);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(result);
        Assert.Equal(SimulationRiskLevel.Critical, result.RiskLevel);
        Assert.Contains(result.Recommendations, recommendation => recommendation.Contains("API gateway"));
        Assert.Contains(result.Recommendations, recommendation => recommendation.Contains("load balancer"));
        Assert.Contains(result.Recommendations, recommendation => recommendation.Contains("cache"));
    }

    [Fact]
    public async Task RunSimulation_WithoutAccessToken_ReturnsUnauthorized()
    {
        ResetDesignServiceWithDatabase([]);
        using var client = _factory.CreateClient();
        var dto = new RunSimulationDto { Scenario = SimulationScenario.DatabaseFailure };

        var response = await client.PostAsJsonAsync("/api/rooms/room-1/simulations/run", dto, JsonOptions);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task RunSimulation_WhenDesignDoesNotExist_ReturnsNotFound()
    {
        _factory.DesignService.GetResult = null;
        _factory.DesignService.ThrowsNotFound = false;
        _factory.DesignService.ThrowsUnauthorized = false;
        using var client = CreateAuthenticatedClient();
        var dto = new RunSimulationDto { Scenario = SimulationScenario.DatabaseFailure };

        var response = await client.PostAsJsonAsync("/api/rooms/room-1/simulations/run", dto, JsonOptions);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task RunSimulation_WhenUserIsNotRoomMember_ReturnsForbidden()
    {
        ResetDesignServiceWithDatabase([]);
        _factory.DesignService.ThrowsUnauthorized = true;
        using var client = CreateAuthenticatedClient();
        var dto = new RunSimulationDto { Scenario = SimulationScenario.DatabaseFailure };

        var response = await client.PostAsJsonAsync("/api/rooms/room-1/simulations/run", dto, JsonOptions);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    private HttpClient CreateAuthenticatedClient()
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(TestAuthHandler.SchemeName);
        return client;
    }

    private void ResetDesignServiceWithDatabase(Dictionary<string, object> databaseProperties)
    {
        _factory.DesignService.GetResult = new Design
        {
            Id = "design-1",
            RoomId = "room-1",
            UpdatedByUserId = "user-1",
            Nodes =
            [
                new Node
                {
                    Id = "api-1",
                    Type = ComponentType.Service,
                    Label = "API Service",
                    X = 120,
                    Y = 80
                },
                new Node
                {
                    Id = "db-1",
                    Type = ComponentType.Database,
                    Label = "Primary Database",
                    X = 420,
                    Y = 80,
                    Properties = databaseProperties
                }
            ],
            Edges =
            [
                new Edge
                {
                    Id = "edge-1",
                    SourceNodeId = "api-1",
                    TargetNodeId = "db-1",
                    Label = "reads/writes"
                }
            ],
            Revision = 1,
            UpdatedAt = DateTime.UtcNow
        };
        ResetDesignServiceFlags();
    }

    private void ResetDesignServiceWithTrafficDesign(bool includeApiGateway, bool includeLoadBalancer, bool includeCache, bool includeQueue, int serviceReplicas, int databaseReplicas)
    {
        var nodes = new List<Node>
        {
            new Node
            {
                Id = "api-1",
                Type = ComponentType.Service,
                Label = "API Service",
                X = 120,
                Y = 80,
                Properties = new Dictionary<string, object>
                {
                    ["replicas"] = serviceReplicas
                }
            },
            new Node
            {
                Id = "db-1",
                Type = ComponentType.Database,
                Label = "Primary Database",
                X = 420,
                Y = 80,
                Properties = new Dictionary<string, object>
                {
                    ["replicas"] = databaseReplicas,
                    ["backupEnabled"] = true,
                    ["failoverEnabled"] = databaseReplicas > 1
                }
            }
        };

        var edges = new List<Edge>
        {
            new Edge
            {
                Id = "edge-api-db",
                SourceNodeId = "api-1",
                TargetNodeId = "db-1",
                Label = "reads/writes"
            }
        };

        if (includeApiGateway)
        {
            nodes.Add(new Node
            {
                Id = "gateway-1",
                Type = ComponentType.ApiGateway,
                Label = "API Gateway",
                X = 80,
                Y = 80,
                Properties = new Dictionary<string, object>
                {
                    ["replicas"] = 2,
                    ["rateLimitPerMinute"] = 120000,
                    ["authEnabled"] = true,
                    ["requestTimeoutMs"] = 3000,
                    ["retriesEnabled"] = true
                }
            });
            edges.Add(new Edge
            {
                Id = "edge-gateway-api",
                SourceNodeId = "gateway-1",
                TargetNodeId = "api-1",
                Label = "routes"
            });
        }

        if (includeLoadBalancer)
        {
            nodes.Add(new Node
            {
                Id = "lb-1",
                Type = ComponentType.LoadBalancer,
                Label = "Load Balancer",
                X = 40,
                Y = 80
            });
            edges.Add(new Edge
            {
                Id = "edge-lb-api",
                SourceNodeId = "lb-1",
                TargetNodeId = "api-1",
                Label = "routes"
            });
        }

        if (includeCache)
        {
            nodes.Add(new Node
            {
                Id = "cache-1",
                Type = ComponentType.Cache,
                Label = "Redis Cache",
                X = 300,
                Y = 180
            });
            edges.Add(new Edge
            {
                Id = "edge-api-cache",
                SourceNodeId = "api-1",
                TargetNodeId = "cache-1",
                Label = "reads"
            });
        }

        if (includeQueue)
        {
            nodes.Add(new Node
            {
                Id = "queue-1",
                Type = ComponentType.Queue,
                Label = "Job Queue",
                X = 300,
                Y = 280
            });
            edges.Add(new Edge
            {
                Id = "edge-api-queue",
                SourceNodeId = "api-1",
                TargetNodeId = "queue-1",
                Label = "publishes"
            });
        }

        _factory.DesignService.GetResult = new Design
        {
            Id = "design-1",
            RoomId = "room-1",
            UpdatedByUserId = "user-1",
            Nodes = nodes,
            Edges = edges,
            Revision = 1,
            UpdatedAt = DateTime.UtcNow
        };
        ResetDesignServiceFlags();
    }

    private void ResetDesignServiceFlags()
    {
        _factory.DesignService.ThrowsNotFound = false;
        _factory.DesignService.ThrowsUnauthorized = false;
        _factory.DesignService.ThrowsValidation = false;
    }
}

