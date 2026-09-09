using MongoDB.Driver;

namespace TopologyServer;

public class RoomAccessService : IRoomAccessService
{
    private readonly IMongoCollection<Room> _rooms;
    private readonly ILogger<RoomAccessService> _logger;

    public RoomAccessService(IMongoDatabase database, ILogger<RoomAccessService> logger)
    {
        _rooms = database.GetCollection<Room>("Room");
        _logger = logger;
    }

    public async Task<Room> EnsureRoomMemberAsync(string roomId, string userId)
    {
        var room = await GetRoomAsync(roomId);

        if (!room.MemberUserIds.Contains(userId))
        {
            _logger.LogWarning(
                "User {UserId} is not a member of room {RoomId}",
                userId,
                roomId);
            throw new UnauthorizedAccessException("User is not a member of this room");
        }

        _logger.LogInformation("User {UserId} is a member of room {RoomId}", userId, roomId);
        return room;
    }

    public async Task<Room> EnsureRoomOwnerAsync(string roomId, string userId)
    {
        var room = await GetRoomAsync(roomId);

        if (room.OwnerUserId != userId)
        {
            _logger.LogWarning(
                "User {UserId} is not the owner of room {RoomId}",
                userId,
                roomId);
            throw new UnauthorizedAccessException("Only the room owner can perform this action");
        }

        _logger.LogInformation("User {UserId} is the owner of room {RoomId}", userId, roomId);
        return room;
    }

    private async Task<Room> GetRoomAsync(string roomId)
    {
        var room = await _rooms.Find(room => room.Id == roomId).FirstOrDefaultAsync();

        if (room is null)
        {
            _logger.LogWarning("Room {RoomId} not found", roomId);
            throw new KeyNotFoundException("Room not found");
        }

        return room;
    }
}