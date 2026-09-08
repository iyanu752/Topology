using System;

namespace TopologyServer;

public class CreateRoomDto
{
    public AccessType Type {get; set;}
    public string? RoomKey {get; set;}
}
