using System;
using System.Security.Claims;
using MongoDB.Driver;

namespace TopologyServer;

public class RoomService : IRoomService
{
    private readonly IMongoCollection<Room> _room;
    private readonly ILogger<RoomService> _logger;

    private readonly IAuthService _authService;

    public RoomService(IMongoDatabase database, ILogger<RoomService> logger, IAuthService authService)
    {
        _room = database.GetCollection<Room>("Room");
        _logger = logger;
        _authService = authService;
    } 

    public async Task<Room> CreateRoomAsync(ClaimsPrincipal claimsPrincipal, CreateRoomDto createRoomDto)
    {
        var user = await _authService.GetOrCreateUserAsync(claimsPrincipal);
        var room = new Room
        {
            OwnerUserId = user.Id!,
            MemberUserIds = [user.Id!],
            Type = createRoomDto.Type,
            CreatedAt = DateTime.UtcNow
        };
        await _room.InsertOneAsync(room);
        _logger.LogInformation("Room Created Sucessfully");
        return room;
    }

    public async Task<Room?> JoinRoomAsync (ClaimsPrincipal claimsPrincipal, JoinRoomDto joinRoomDto)
    {
        var user = await _authService.GetOrCreateUserAsync(claimsPrincipal);
        var room = await _room.Find(room => room.Id == joinRoomDto.Id).FirstOrDefaultAsync();
        if (room is null)
        {
            _logger.LogWarning("Room not found");
            return null;
        }
        if (room.Type == AccessType.Private && room.RoomKey != joinRoomDto.RoomKey)
        {
            _logger.LogWarning("Your room key is invalid");
            throw new UnauthorizedAccessException("Invalid room key");
        }

        if (room.MemberUserIds.Contains(user.Id!))
        {
            return room;
        }
        var update = Builders<Room>.Update.AddToSet(room => room.MemberUserIds, user.Id!);
        await _room.UpdateOneAsync(existingroom => existingroom.Id ==  room.Id, update);
        room.MemberUserIds.Add(user.Id!);
        _logger.LogInformation("Room created successfully");
        return room;
    }

    public async Task<bool?> DeleteRoomAsync(ClaimsPrincipal claimsPrincipal, DeleteRoomDto deleteRoomDto)
    {
        var user = await _authService.GetOrCreateUserAsync(claimsPrincipal);
        var room = await _room.Find(room => room.Id == deleteRoomDto.Id).FirstOrDefaultAsync();
        if (room == null)
        {
            _logger.LogWarning("Room does not exist(delete)");
            return null;
        }

        if(room.OwnerUserId != user.Id)
        {
            _logger.LogWarning("Only room owners can create room");
            throw new UnauthorizedAccessException("Only room owners can delete room");
        } 
        var result = await _room.DeleteOneAsync(room.Id);
        _logger.LogInformation("Room created successfully");
        return result.DeletedCount > 0;
    }
}
