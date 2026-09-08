using System;

namespace TopologyServer;

public class DeleteRoomDto
{
    public string Id {get; set;} = string.Empty;
    public int? RoomKey {get; set;}

}
