using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using TopologyServer;

namespace TopologyServer.Tests;

public sealed class TestWebApplicationFactory : WebApplicationFactory<Program>
{
    public TestWebApplicationFactory()
    {
        Environment.SetEnvironmentVariable("Auth0__Domain", "test.auth0.com");
        Environment.SetEnvironmentVariable("Auth0__Audience", "https://topology-test-api");
        Environment.SetEnvironmentVariable("ConnectionStrings__MongoDB", "mongodb://localhost:27017");
    }

    public FakeAuthService AuthService { get; } = new();
    public FakeRoomService RoomService { get; } = new();
    public FakeDesignService DesignService { get; } = new();
    public FakeComponentLibraryService ComponentLibraryService { get; } = new();
    public FakeRoomAccessService RoomAccessService { get; } = new();
    public FakeConnectionRuleService ConnectionRuleService { get; } = new();
    public FakeDesignValidationService DesignValidationService { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        builder.ConfigureAppConfiguration(configuration =>
        {
            configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Auth0:Domain"] = "test.auth0.com",
                ["Auth0:Audience"] = "https://topology-test-api",
                ["ConnectionStrings:MongoDB"] = "mongodb://localhost:27017"
            });
        });

        builder.ConfigureTestServices(services =>
        {
            services.AddAuthentication(options =>
                {
                    options.DefaultAuthenticateScheme = TestAuthHandler.SchemeName;
                    options.DefaultChallengeScheme = TestAuthHandler.SchemeName;
                    options.DefaultForbidScheme = TestAuthHandler.SchemeName;
                })
                .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>(TestAuthHandler.SchemeName, _ => { });

            services.RemoveAll<IAuthService>();
            services.RemoveAll<IRoomService>();
            services.RemoveAll<IDesignService>();
            services.RemoveAll<IComponentLibraryService>();
            services.RemoveAll<IRoomAccessService>();
            services.RemoveAll<IConnectionRuleService>();
            services.RemoveAll<IDesignValidationService>();
            services.AddSingleton<IAuthService>(AuthService);
            services.AddSingleton<IRoomService>(RoomService);
            services.AddSingleton<IDesignService>(DesignService);
            services.AddSingleton<IComponentLibraryService>(ComponentLibraryService);
            services.AddSingleton<IRoomAccessService>(RoomAccessService);
            services.AddSingleton<IConnectionRuleService>(ConnectionRuleService);
            services.AddSingleton<IDesignValidationService>(DesignValidationService);
        });
    }
}

public sealed class FakeAuthService : IAuthService
{
    public User CurrentUser { get; set; } = new()
    {
        Id = "user-1",
        Auth0UserId = "auth0|integration-test-user",
        UserName = "Integration Test User",
        Email = "test@example.com",
        CreatedAt = DateTime.UtcNow
    };

    public bool? DeleteResult { get; set; } = true;

    public Task<User> GetOrCreateUserAsync(ClaimsPrincipal principal) => Task.FromResult(CurrentUser);

    public Task<bool?> DeleteUserAsync(ClaimsPrincipal principal) => Task.FromResult(DeleteResult);
}

public sealed class FakeRoomService : IRoomService
{
    public Room CreateResult { get; set; } = new()
    {
        Id = "room-1",
        OwnerUserId = "user-1",
        MemberUserIds = ["user-1"],
        Type = AccessType.Public,
        CreatedAt = DateTime.UtcNow
    };

    public Room? JoinResult { get; set; } = new()
    {
        Id = "room-1",
        OwnerUserId = "user-1",
        MemberUserIds = ["user-1", "user-2"],
        Type = AccessType.Public,
        CreatedAt = DateTime.UtcNow
    };

    public bool JoinThrowsUnauthorized { get; set; }
    public bool? DeleteResult { get; set; } = true;
    public bool DeleteThrowsUnauthorized { get; set; }

    public Task<Room> CreateRoomAsync(ClaimsPrincipal claimsPrincipal, CreateRoomDto createRoomDto)
    {
        CreateResult.Type = createRoomDto.Type;
        CreateResult.RoomKey = createRoomDto.RoomKey;
        return Task.FromResult(CreateResult);
    }

    public Task<Room?> JoinRoomAsync(ClaimsPrincipal claimsPrincipal, JoinRoomDto joinRoomDto)
    {
        if (JoinThrowsUnauthorized)
        {
            throw new UnauthorizedAccessException("Invalid room key");
        }

        return Task.FromResult(JoinResult);
    }

    public Task<bool?> DeleteRoomAsync(ClaimsPrincipal claimsPrincipal, DeleteRoomDto deleteRoomDto)
    {
        if (DeleteThrowsUnauthorized)
        {
            throw new UnauthorizedAccessException("Only room owners can delete room");
        }

        return Task.FromResult(DeleteResult);
    }
}

public sealed class FakeDesignService : IDesignService
{
    public Design? GetResult { get; set; } = new()
    {
        Id = "design-1",
        RoomId = "room-1",
        UpdatedByUserId = "user-1",
        Nodes = [],
        Edges = [],
        Revision = 1,
        UpdatedAt = DateTime.UtcNow
    };

    public Design SaveResult { get; set; } = new()
    {
        Id = "design-1",
        RoomId = "room-1",
        UpdatedByUserId = "user-1",
        Nodes = [],
        Edges = [],
        Revision = 2,
        UpdatedAt = DateTime.UtcNow
    };

    public bool DeleteResult { get; set; } = true;
    public bool ThrowsNotFound { get; set; }
    public bool ThrowsUnauthorized { get; set; }
    public bool ThrowsValidation { get; set; }

    public Task<Design?> GetDesignAsync(ClaimsPrincipal principal, string roomId)
    {
        ThrowIfConfigured();
        return Task.FromResult(GetResult);
    }

    public Task<Design> SaveDesignAsync(ClaimsPrincipal principal, string roomId, SaveDesignDto saveDesignDto)
    {
        ThrowIfConfigured();
        SaveResult.RoomId = roomId;
        SaveResult.Nodes = saveDesignDto.Nodes;
        SaveResult.Edges = saveDesignDto.Edges;
        return Task.FromResult(SaveResult);
    }

    public Task<Design> CreateInitialDesignAsync(string roomId, string userId)
    {
        return Task.FromResult(new Design
        {
            Id = "design-1",
            RoomId = roomId,
            UpdatedByUserId = userId,
            Nodes = [],
            Edges = [],
            Revision = 0,
            UpdatedAt = DateTime.UtcNow
        });
    }

    public Task<bool> DeleteDesignAsync(ClaimsPrincipal principal, string roomId)
    {
        ThrowIfConfigured();
        return Task.FromResult(DeleteResult);
    }

    private void ThrowIfConfigured()
    {
        if (ThrowsNotFound)
        {
            throw new KeyNotFoundException("Room not found");
        }

        if (ThrowsUnauthorized)
        {
            throw new UnauthorizedAccessException("Forbidden");
        }

        if (ThrowsValidation)
        {
            throw new DesignValidationException(new DesignValidationResult
            {
                Issues =
                [
                    new DesignValidationIssue
                    {
                        Severity = ValidationSeverity.Error,
                        Message = "Design is invalid",
                        EdgeId = "edge-1"
                    }
                ]
            });
        }
    }
}

public sealed class FakeComponentLibraryService : IComponentLibraryService
{
    public IReadOnlyList<ComponentDefinition> Components { get; set; } =
    [
        new ComponentDefinition
        {
            Id = "component-database",
            Type = ComponentType.Database,
            Label = "Database",
            Category = ComponentCategory.Data,
            Description = "Stores persistent application data.",
            Properties =
            [
                new ComponentPropertyDefinition
                {
                    Key = "engine",
                    Label = "Engine",
                    InputType = "select",
                    DefaultValue = "Postgres",
                    Options = ["Postgres", "MySQL", "MongoDB"],
                    Required = true
                }
            ]
        },
        new ComponentDefinition
        {
            Id = "component-service",
            Type = ComponentType.Service,
            Label = "Service",
            Category = ComponentCategory.Compute,
            Description = "Runs application logic.",
            Properties = []
        }
    ];

    public bool SeedCalled { get; private set; }

    public Task<IReadOnlyList<ComponentDefinition>> GetComponentsAsync() => Task.FromResult(Components);

    public Task<ComponentDefinition?> GetComponentByTypeAsync(ComponentType type)
    {
        return Task.FromResult(Components.FirstOrDefault(component => component.Type == type));
    }

    public Task SeedDefaultComponentsAsync()
    {
        SeedCalled = true;
        return Task.CompletedTask;
    }
}

public sealed class FakeRoomAccessService : IRoomAccessService
{
    public bool ThrowsNotFound { get; set; }
    public bool ThrowsUnauthorized { get; set; }

    public Task<Room> EnsureRoomMemberAsync(string roomId, string userId) => EnsureAccessAsync(roomId, userId);

    public Task<Room> EnsureRoomOwnerAsync(string roomId, string userId) => EnsureAccessAsync(roomId, userId);

    private Task<Room> EnsureAccessAsync(string roomId, string userId)
    {
        if (ThrowsNotFound)
        {
            throw new KeyNotFoundException("Room not found");
        }

        if (ThrowsUnauthorized)
        {
            throw new UnauthorizedAccessException("Forbidden");
        }

        return Task.FromResult(new Room
        {
            Id = roomId,
            OwnerUserId = userId,
            MemberUserIds = [userId],
            Type = AccessType.Public,
            CreatedAt = DateTime.UtcNow
        });
    }
}

public sealed class FakeConnectionRuleService : IConnectionRuleService
{
    public IReadOnlyList<ConnectionRule> Rules { get; set; } =
    [
        new ConnectionRule
        {
            SourceType = ComponentType.Client,
            TargetType = ComponentType.Database,
            IsAllowed = false,
            Severity = ValidationSeverity.Error,
            Message = "Clients should not connect directly to databases."
        },
        new ConnectionRule
        {
            SourceType = ComponentType.Service,
            TargetType = ComponentType.Database,
            IsAllowed = true,
            Severity = ValidationSeverity.Info,
            Message = "Services can connect to databases."
        }
    ];

    public bool SeedCalled { get; private set; }

    public Task<IReadOnlyList<ConnectionRule>> GetRulesAsync() => Task.FromResult(Rules);

    public Task<ConnectionRule?> GetRuleAsync(ComponentType sourceType, ComponentType targetType)
    {
        return Task.FromResult(Rules.FirstOrDefault(rule => rule.SourceType == sourceType && rule.TargetType == targetType));
    }

    public Task SeedDefaultRulesAsync()
    {
        SeedCalled = true;
        return Task.CompletedTask;
    }
}

public sealed class FakeDesignValidationService : IDesignValidationService
{
    public DesignValidationResult Result { get; set; } = new();

    public Task<DesignValidationResult> ValidateDesignAsync(SaveDesignDto design) => Task.FromResult(Result);
}