using System.Security.Claims;
using MongoDB.Driver;

namespace TopologyServer;

public class DesignService : IDesignService
{
    private readonly IMongoCollection<Design> _design;
    private readonly ILogger<DesignService> _logger;
    private readonly IAuthService _authService;
    private readonly IRoomAccessService _roomAccessService;

    public DesignService(
        IMongoDatabase database,
        ILogger<DesignService> logger,
        IAuthService authService,
        IRoomAccessService roomAccessService)
    {
        _design = database.GetCollection<Design>("Design");
        _logger = logger;
        _authService = authService;
        _roomAccessService = roomAccessService;
    }

    public async Task<Design?> GetDesignAsync(ClaimsPrincipal principal, string roomId)
    {
        var user = await _authService.GetOrCreateUserAsync(principal);
        var room = await _roomAccessService.EnsureRoomMemberAsync(roomId, user.Id!);

        return await _design.Find(design => design.RoomId == room.Id).FirstOrDefaultAsync();
    }

    public async Task<Design> SaveDesignAsync(ClaimsPrincipal principal, string roomId, SaveDesignDto saveDesignDto)
    {
        var user = await _authService.GetOrCreateUserAsync(principal);
        await _roomAccessService.EnsureRoomMemberAsync(roomId, user.Id!);

        var existingDesign = await _design.Find(design => design.RoomId == roomId).FirstOrDefaultAsync();
        if (existingDesign is null)
        {
            _logger.LogInformation("Design does not exist, creating a new design");
            var newDesign = new Design
            {
                RoomId = roomId,
                UpdatedByUserId = user.Id!,
                Nodes = saveDesignDto.Nodes,
                Edges = saveDesignDto.Edges,
                UpdatedAt = DateTime.UtcNow,
                Revision = 1
            };

            await _design.InsertOneAsync(newDesign);
            return newDesign;
        }

        var updatedAt = DateTime.UtcNow;
        var update = Builders<Design>.Update
            .Set(design => design.Nodes, saveDesignDto.Nodes)
            .Set(design => design.Edges, saveDesignDto.Edges)
            .Set(design => design.UpdatedByUserId, user.Id!)
            .Set(design => design.UpdatedAt, updatedAt)
            .Inc(design => design.Revision, 1);

        var updatedDesign = await _design.FindOneAndUpdateAsync(
            design => design.RoomId == roomId,
            update,
            new FindOneAndUpdateOptions<Design>
            {
                ReturnDocument = ReturnDocument.After
            });

        return updatedDesign;
    }

    public async Task<Design> CreateInitialDesignAsync(string roomId, string userId)
    {
        var newDesign = new Design
        {
            RoomId = roomId,
            UpdatedByUserId = userId,
            Revision = 0
        };

        await _design.InsertOneAsync(newDesign);
        return newDesign;
    }

    public async Task<bool> DeleteDesignAsync(ClaimsPrincipal principal, string roomId)
    {
        var user = await _authService.GetOrCreateUserAsync(principal);
        await _roomAccessService.EnsureRoomOwnerAsync(roomId, user.Id!);

        var result = await _design.DeleteOneAsync(design => design.RoomId == roomId);
        return result.DeletedCount > 0;
    }
}