using System;

namespace TopologyServer;

public interface IRoomAccessService
{
    Task<Room> EnsureRoomMemberAsync(string roomId, string userId);
    Task<Room> EnsureRoomOwnerAsync(string roomId, string userId);

}
