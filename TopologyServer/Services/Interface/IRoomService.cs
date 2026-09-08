using System;
using System.Security.Claims;

namespace TopologyServer;

public interface IRoomService
{
    Task<Room>CreateRoomAsync(ClaimsPrincipal claimsPrincipal, CreateRoomDto createRoomDto);
    Task<Room?>JoinRoomAsync(ClaimsPrincipal claimsPrincipal, JoinRoomDto joinRoomDto);
    Task<bool?>DeleteRoomAsync(ClaimsPrincipal claimsPrincipal, DeleteRoomDto deleteRoomDto);

}
